using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>隔离的人物→木桩神通演示。复用施法资源、表现配置和共享曲线，只推进表现，不执行伤害/环境/消耗。</summary>
public sealed class UIAbilityPreview : MonoBehaviour, IDragHandler
{
    sealed class Visual { public GameObject root; public ParticleSystem[] systems; public float born, life; public Vector3 from,to; public float travel; public bool impacted; public GameObject impact; public float impactScale; public bool keepAtImpact; }
    sealed class Emission { public AbilityVfxEmitter source; public Transform parent; public float born,next; public int count; }
    public Camera 预览相机=>camera3d;
    public Transform 演示玩家=>humanRoot;
    public Transform 演示木桩=>dummy;
    public int 循环次数 {get;private set;}
    public int 展示渲染器数=>model?model.GetComponentsInChildren<Renderer>().Length:0;
    RawImage image;Transform stage,model,humanRoot,dummy,hand;Camera camera3d;RenderTexture target;
    readonly List<Visual> visuals=new List<Visual>();
    readonly List<Emission> emissions=new List<Emission>();
    readonly List<Material> ownedMaterials=new List<Material>();
    readonly List<Transform> swords=new List<Transform>();
    readonly List<SpiralSwordCurves.SwordFlight> flights=new List<SpiralSwordCurves.SwordFlight>();
    Transform appearanceSource,clonedVisual;PlayableGraph graph;AnimationMixerPlayable mixer;AnimationClipPlayable idlePlayable,castPlayable,volleyPlayable;
    PlayableGraph companionGraph;AnimationClipPlayable companionPlayable,companionPrevious;AnimationMixerPlayable companionMixer;
    CheDianYeTingDefinition companionDefinition;Transform companionDragon,companionHead;CheDianBreathVisual companionBreath;CheDianManifestation companionManifest;bool companionShown,companionReturning;
    AnimationClip companionClip,companionPreviousClip;float companionTime,companionPreviousTime,companionBlend;
    CheDianPlayerChannel companionPlayerChannel;bool companionFired;
    AnimationClip castClip,volleyClip;ActiveDivineAbility active;DivineAbilityDefinition selected;GameObject source;
    FallingSpellPattern falling;SpiralSwordPattern spiral;SpiralSwordCurves.ArcLengthRibbon ribbon;
    float clock,nextRender,cycleAge,cycleLength=3,yaw,releaseAt;int emitted;bool released,mechanical;
    Bounds bounds;MaterialPropertyBlock swordBlock;Text humanLabel,dummyLabel;
    Vector3 dummyHome;
    SkinnedMeshRenderer companionSkin;Vector3[] companionVertices;BoneWeight[] companionWeights;Matrix4x4[] companionBindposes,companionMatrices;
    public void 初始化(RawImage view){image=view;}
    void Build()
    {
        if(stage)return;
        stage=new GameObject("AbilityPreview_Runtime").transform;stage.position=new Vector3(25000+Mathf.Abs(GetInstanceID()%1000)*100,15000,17000);
        var cam=new GameObject("AbilityCamera",typeof(Camera));cam.transform.SetParent(stage,false);camera3d=cam.GetComponent<Camera>();camera3d.enabled=false;camera3d.clearFlags=CameraClearFlags.SolidColor;camera3d.backgroundColor=Color.clear;camera3d.nearClipPlane=.1f;camera3d.farClipPlane=60;camera3d.orthographic=true;camera3d.allowHDR=false;camera3d.cullingMask=1<<31;
        var light=new GameObject("AbilityPreviewLight",typeof(Light));light.transform.SetParent(stage,false);light.transform.localPosition=new Vector3(0,6,-4);var lamp=light.GetComponent<Light>();lamp.type=LightType.Point;lamp.intensity=.65f;lamp.range=30;lamp.shadows=LightShadows.None;
        humanRoot=new GameObject("PreviewCaster").transform;humanRoot.SetParent(stage,false);humanRoot.localPosition=new Vector3(0,0,2.2f);humanRoot.localRotation=Quaternion.Euler(0,180,0);
        var player=FindObjectOfType<PlayerController>();var appearance=player?player.GetComponent<玩家外观>():null;appearanceSource=appearance?appearance.玩家视觉:player?player.transform.Find("Player_Visual"):null;
        if(appearanceSource){clonedVisual=Clone(appearanceSource.gameObject,humanRoot).transform;clonedVisual.localPosition=Vector3.zero;clonedVisual.localRotation=Quaternion.identity;
            Animator animator=null;foreach(var a in clonedVisual.GetComponentsInChildren<Animator>(true)){a.runtimeAnimatorController=null;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;if(a.isHuman&&a.avatar&&a.avatar.isValid)animator=a;}
            foreach(var r in clonedVisual.GetComponentsInChildren<Renderer>(true))if(!(r is SkinnedMeshRenderer))r.enabled=false;
            if(animator){animator.Rebind();hand=animator.GetBoneTransform(HumanBodyBones.RightHand);graph=PlayableGraph.Create("AbilityPreview_Animation");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);mixer=AnimationMixerPlayable.Create(graph,3);var idle=Resources.Load<AnimationClip>("技能动作/御风八向/灵虚剑决/御风八向_悬停");if(idle){idlePlayable=AnimationClipPlayable.Create(graph,idle);idlePlayable.SetApplyFootIK(false);graph.Connect(idlePlayable,0,mixer,0);mixer.SetInputWeight(0,1);}AnimationPlayableOutput.Create(graph,"Caster",animator).SetSourcePlayable(mixer);graph.Play();graph.Evaluate(0);}
        }
        var prefab=Resources.Load<GameObject>("摆设/练功木桩");dummy=prefab?Clone(prefab,stage).transform:new GameObject("PreviewDummy").transform;dummy.SetParent(stage,false);dummy.localPosition=new Vector3(0,0,-2.2f);
        bool found=false;var size=new Bounds();foreach(var r in dummy.GetComponentsInChildren<Renderer>()){if(!found){size=r.bounds;found=true;}else size.Encapsulate(r.bounds);}if(found){float scale=1.45f/Mathf.Max(.1f,size.size.y);dummy.localScale*=scale;dummy.position+=dummy.position-new Vector3(size.center.x,size.min.y,size.center.z);}
        dummyHome=dummy.localPosition;
        var font=Resources.Load<Font>("Fonts/SimHei")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");humanLabel=UIBuildUtils.CreateText("PreviewCasterLabel",image.transform,font,"玩家",16,TextAnchor.MiddleCenter,UICharacterBackdrop.Muted);dummyLabel=UIBuildUtils.CreateText("PreviewDummyLabel",image.transform,font,"木桩",16,TextAnchor.MiddleCenter,UICharacterBackdrop.Muted);foreach(var label in new[]{humanLabel,dummyLabel}){label.raycastTarget=false;label.rectTransform.sizeDelta=new Vector2(90,26);}
        swordBlock=new MaterialPropertyBlock();同步外观(null);
    }
    static GameObject Clone(GameObject prefab,Transform parent)
    {
        var holder=new GameObject("PreviewCloneHolder");holder.transform.SetParent(parent,false);holder.SetActive(false);var clone=Instantiate(prefab,holder.transform,false);
        foreach(var b in clone.GetComponentsInChildren<MonoBehaviour>(true).Reverse())DestroyImmediate(b);
        foreach(var b in clone.GetComponentsInChildren<Collider>(true))DestroyImmediate(b);foreach(var b in clone.GetComponentsInChildren<Rigidbody>(true))DestroyImmediate(b);
        foreach(var b in clone.GetComponentsInChildren<AudioSource>(true))DestroyImmediate(b);foreach(var b in clone.GetComponentsInChildren<Camera>(true))DestroyImmediate(b);foreach(var b in clone.GetComponentsInChildren<Light>(true))DestroyImmediate(b);
        foreach(var node in clone.GetComponentsInChildren<Transform>(true))node.gameObject.layer=31;
        clone.transform.SetParent(parent,false);Destroy(holder);clone.SetActive(true);return clone;
    }
    public void 显示(IPanelEntry entry)
    {
        Build();ClearEffects();selected=entry as DivineAbilityDefinition;active=entry as ActiveDivineAbility;source=null;falling=null;spiral=null;mechanical=selected&&(selected.神通id=="ability_pingxu_yufeng"||selected.神通id==CheDianYeTingDefinition.AbilityId);clock=0;循环次数=0;
        dummy.localPosition=dummyHome;
        var path=active?active.特效资源路径:entry is PassiveDivineAbility passive?passive.特效资源路径:null;if(!string.IsNullOrEmpty(path))source=Resources.Load<GameObject>(path);
        if(selected&&selected.神通id=="ability_leiyun")source=Resources.Load<GameObject>("CombatVFX/CombatMagic/lightning-fx/lightning-cloud");
        falling=source?source.GetComponent<FallingSpellPattern>():null;spiral=source?source.GetComponent<SpiralSwordPattern>():null;
        castClip=active?Resources.Load<AnimationClip>("技能动作/"+active.施法动作):null;if(active&&active.结算方式==ActiveSkillKind.追踪弹)castClip=Resources.Load<AnimationClip>(HomingBoltSkillRunner.动作资源路径);
        if(active&&!castClip&&!mechanical)castClip=Resources.Load<AnimationClip>("技能动作/神通施法/能量02_前半施法");
        volleyClip=spiral?Resources.Load<AnimationClip>("技能动作/"+spiral.齐射动作):null;BindClip(ref castPlayable,castClip,1);BindClip(ref volleyPlayable,volleyClip,2);
        releaseAt=castClip?castClip.length*(falling?falling.出手进度:active&&active.结算方式==ActiveSkillKind.追踪弹?HomingBoltSkillRunner.放飞进度:.45f):.25f;
        cycleLength=Mathf.Max(1,castClip?castClip.length:1)+1;
        if(active&&active.结算方式==ActiveSkillKind.小剑阵)cycleLength=Mathf.Max(cycleLength,active.持续时长+1); // 完整动作/表现之间留一秒，不按真实冷却等待。
        if(spiral){releaseAt=castClip.length*spiral.出手进度+spiral.布阵时长+spiral.蓄势时长+(volleyClip?volleyClip.length*spiral.第二段出剑进度:0);cycleLength=Mathf.Max(releaseAt+1.8f,releaseAt+(volleyClip?volleyClip.length*(1-spiral.第二段出剑进度):0))+1;BuildSwords();}
        bounds=new Bounds(stage.position+Vector3.up*1.3f,spiral?new Vector3(5.8f,4,6.4f):new Vector3(2.5f,3.6f,6));BeginCycle();
        bool companion=active&&active.结算方式==ActiveSkillKind.伴龙随行;dummy.gameObject.SetActive(!companion);dummyLabel.gameObject.SetActive(!companion);
        if(companion)BuildCompanion();
    }
    void BuildCompanion()
    {
        var definition=Resources.Load<CheDianYeTingDefinition>(CheDianYeTingDefinition.ResourcePath);if(!definition||!definition.Ready)return;
        companionDefinition=definition;
        var dragon=Clone(definition.Companion,model);dragon.transform.position=humanRoot.TransformPoint(definition.SideOffset+Vector3.up*.15f);dragon.transform.rotation=humanRoot.rotation;dragon.transform.localScale=definition.Scale;
        companionDragon=dragon.transform;companionHead=dragon.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Bip001 Head");
        companionSkin=dragon.GetComponentInChildren<SkinnedMeshRenderer>();if(companionSkin&&companionSkin.sharedMesh.isReadable){companionVertices=companionSkin.sharedMesh.vertices;companionWeights=companionSkin.sharedMesh.boneWeights;companionBindposes=companionSkin.sharedMesh.bindposes;companionMatrices=new Matrix4x4[companionBindposes.Length];}
        var animator=dragon.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
        companionGraph=PlayableGraph.Create("AbilityPreview_Companion");companionGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);companionMixer=AnimationMixerPlayable.Create(companionGraph,2);AnimationPlayableOutput.Create(companionGraph,"Dragon",animator).SetSourcePlayable(companionMixer);companionGraph.Play();
        SetCompanionClip(definition.AirIdle);
        if(definition.BreathReady){dummy.gameObject.SetActive(true);dummyLabel.gameObject.SetActive(true);dummy.position=dragon.transform.position+humanRoot.forward*7;var facing=dummy.position-dragon.transform.position;facing.y=0;dragon.transform.rotation=Quaternion.LookRotation(facing);cycleLength=.75f+definition.FirstWindupSeconds+definition.BreathDuration+definition.ReturnToGhostSeconds+.75f;
            companionBreath=new CheDianBreathVisual(definition);companionBreath.Root.transform.SetParent(model,true);foreach(var t in companionBreath.Root.GetComponentsInChildren<Transform>())t.gameObject.layer=31;foreach(var p in companionBreath.Root.GetComponentsInChildren<ParticleSystem>()){var m=p.main;m.useUnscaledTime=true;}}
        companionManifest=new CheDianManifestation(dragon,definition,companionHead);companionManifest.ShowGhost(true);dragon.SetActive(false);companionShown=companionReturning=companionFired=false;
        var caster=clonedVisual?clonedVisual.GetComponentsInChildren<Animator>(true).FirstOrDefault(a=>a.isHuman):null;
        if(caster)companionPlayerChannel=new CheDianPlayerChannel(caster,definition);
        bounds=new Bounds(Vector3.Lerp(humanRoot.position,dummy.position,.45f)+Vector3.up*1.6f,new Vector3(12,6,13));
    }
    void SetCompanionClip(AnimationClip clip)
    {
        if(companionClip==clip)return;
        if(companionPrevious.IsValid()){companionMixer.DisconnectInput(1);companionPrevious.Destroy();}
        companionPrevious=companionPlayable;companionPreviousClip=companionClip;companionPreviousTime=companionTime;
        if(companionPrevious.IsValid()){companionMixer.DisconnectInput(0);companionGraph.Connect(companionPrevious,0,companionMixer,1);}
        companionClip=clip;companionPlayable=AnimationClipPlayable.Create(companionGraph,clip);companionPlayable.SetApplyFootIK(false);companionPlayable.SetApplyPlayableIK(false);companionGraph.Connect(companionPlayable,0,companionMixer,0);companionTime=0;companionBlend=0;
    }
    void TickCompanion(float dt)
    {
        var d=companionDefinition;if(!d||companionManifest==null)return;
        float begin=.75f,fire=begin+d.FirstWindupSeconds,end=fire+d.BreathDuration,recovery=end+d.RecoverySeconds,vanish=end;
        if(cycleAge<begin){if(companionShown){companionManifest.HideImmediately();companionDragon.gameObject.SetActive(false);companionShown=false;companionPlayerChannel?.End();}companionReturning=companionFired=false;companionManifest.Tick(dt,companionDragon.position+Vector3.up*1.4f);return;}
        if(!companionShown){companionDragon.gameObject.SetActive(true);companionManifest.Reveal(true);companionShown=true;companionPlayerChannel?.Begin();}
        if(cycleAge>=fire&&!companionFired){companionFired=true;companionPlayerChannel?.Fire();}
        if(cycleAge>=recovery)companionPlayerChannel?.End();
        if(cycleAge>=vanish&&!companionReturning){companionReturning=true;companionManifest.Dissolve();}
        if(companionReturning&&companionManifest.Hidden){companionDragon.gameObject.SetActive(false);companionManifest.ShowGhost(true);}
        var clip=cycleAge<fire?d.AirBreathStart:cycleAge<end?d.AirBreathLoop:cycleAge<recovery?d.AirBreathEnd:d.AirIdle;
        SetCompanionClip(clip);float rate=cycleAge<fire?clip.length/d.FirstWindupSeconds:cycleAge<recovery&&cycleAge>=end?clip.length/d.RecoverySeconds:1;
        companionTime+=dt*rate;companionPlayable.SetTime(clip.isLooping?Mathf.Repeat(companionTime,clip.length):Mathf.Min(companionTime,clip.length));companionBlend+=dt;float weight=companionPrevious.IsValid()?Mathf.SmoothStep(0,1,Mathf.Clamp01(companionBlend/.2f)):1;
        if(companionPrevious.IsValid()){companionPreviousTime+=dt;companionPrevious.SetTime(companionPreviousClip.isLooping?Mathf.Repeat(companionPreviousTime,companionPreviousClip.length):Mathf.Min(companionPreviousTime,companionPreviousClip.length));}companionMixer.SetInputWeight(0,weight);companionMixer.SetInputWeight(1,1-weight);companionGraph.Evaluate(0);
        companionManifest.Tick(dt,companionDragon.position+Vector3.up*1.4f);if(companionBreath==null||!companionHead)return;companionBreath.Tick(dt);var mouth=companionHead.TransformPoint(d.MouthOffset);
        if(cycleAge<fire)companionBreath.ShowCharge(mouth,Mathf.Clamp01((cycleAge-begin)/d.FirstWindupSeconds));
        else if(cycleAge<end){var point=dummy.position+Vector3.up*.8f;var aim=Quaternion.FromToRotation(companionHead.TransformDirection(Vector3.left),(point-mouth).normalized)*companionHead.rotation;companionHead.rotation=Quaternion.Slerp(companionHead.rotation,aim,.92f);mouth=companionHead.TransformPoint(d.MouthOffset);companionBreath.ShowBeam(mouth,point,Mathf.Clamp01((cycleAge-fire)/.1f));companionBreath.Hit(point);}
        else companionBreath.Fade(1);
    }
    void FitCompanion()
    {
        if(!companionSkin||companionVertices==null)return;
        var bones=companionSkin.bones;for(int i=0;i<companionMatrices.Length;i++)companionMatrices[i]=bones[i].localToWorldMatrix*companionBindposes[i];
        // Use actual skinned world vertices: BakeMesh repeats a non-unit display scale in this engine.
        for(int i=0;i<companionVertices.Length;i++){var w=companionWeights[i];var v=companionVertices[i];var p=companionMatrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+companionMatrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+companionMatrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+companionMatrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;bounds.Encapsulate(p);}
        foreach(var r in humanRoot.GetComponentsInChildren<Renderer>())if(r.enabled&&r.gameObject.activeInHierarchy)bounds.Encapsulate(r.bounds);
        foreach(var r in dummy.GetComponentsInChildren<Renderer>())if(r.enabled)bounds.Encapsulate(r.bounds);
    }
    void BindClip(ref AnimationClipPlayable playable,AnimationClip clip,int slot)
    {
        if(!graph.IsValid())return;if(playable.IsValid()){mixer.DisconnectInput(slot);playable.Destroy();}if(!clip)return;playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);graph.Connect(playable,0,mixer,slot);
    }
    void ClearEffects()
    {
        companionPlayerChannel?.Dispose();companionPlayerChannel=null;companionManifest?.Dispose();companionManifest=null;companionBreath?.Dispose();companionBreath=null;companionDefinition=null;companionClip=null;companionPlayable=default;companionPrevious=default;
        companionSkin=null;companionVertices=null;companionWeights=null;companionBindposes=null;companionMatrices=null;
        if(companionGraph.IsValid())companionGraph.Destroy();
        foreach(var v in visuals)if(v.root){v.root.SetActive(false);Destroy(v.root);}visuals.Clear();emissions.Clear();if(model){model.gameObject.SetActive(false);Destroy(model.gameObject);}foreach(var m in ownedMaterials)Destroy(m);ownedMaterials.Clear();swords.Clear();flights.Clear();model=new GameObject("AbilityVisual").transform;model.SetParent(stage,false);
    }
    void BuildSwords()
    {
        if(!spiral.剑群)return;var r=spiral.剑群.GetComponent<ParticleSystemRenderer>();var mat=new Material(Shader.Find("Standard"));mat.mainTexture=r.sharedMaterial.mainTexture;mat.color=new Color(.77f,.87f,1);mat.SetFloat("_Mode",2);mat.SetInt("_SrcBlend",5);mat.SetInt("_DstBlend",10);mat.SetInt("_ZWrite",0);mat.EnableKeyword("_ALPHABLEND_ON");mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.12f,.22f,.35f));mat.renderQueue=3000;ownedMaterials.Add(mat);
        ribbon=new SpiralSwordCurves.ArcLengthRibbon(SpiralSwordCurves.Ribbon.Create(42,spiral.圈数),0);
        for(int i=0;i<144;i++){var go=new GameObject("PreviewSword",typeof(MeshFilter),typeof(MeshRenderer));go.layer=31;go.transform.SetParent(model,false);go.GetComponent<MeshFilter>().sharedMesh=r.mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;go.transform.localScale=Vector3.one*spiral.剑缩放*.55f;swords.Add(go.transform);}
    }
    void BeginCycle(){cycleAge=0;emitted=0;released=false;循环次数++;flights.Clear();foreach(var sword in swords){sword.gameObject.SetActive(true);float u=(flights.Count+.5f)/swords.Count;var start=humanRoot.localPosition+ribbon.Evaluate(u)*.65f;var end=dummy.localPosition+SpiralSwordCurves.AreaOffset(flights.Count,swords.Count,1.3f);flights.Add(SpiralSwordCurves.SwordFlight.Create(humanRoot.localPosition,dummy.localPosition,end,start,ribbon.Tangent(u)*6,.14f+(flights.Count%5)*.035f,.55f));}
        if(!active&&!mechanical&&source)Spawn(source,humanRoot.position+Vector3.up*(selected.神通id=="ability_leiyun"?2:1),Quaternion.identity,.3f,cycleLength);
    }
    Visual Spawn(GameObject prefab,Vector3 at,Quaternion rotation,float scale,float life)
    {
        if(!prefab)return null;var go=Clone(prefab,model);go.transform.position=at;go.transform.rotation=rotation;go.transform.localScale=Vector3.one*scale;foreach(var trail in go.GetComponentsInChildren<TrailRenderer>(true))trail.enabled=false;
        var systems=go.GetComponentsInChildren<ParticleSystem>(true);foreach(var ps in systems){ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var m=ps.main;m.stopAction=ParticleSystemStopAction.None;m.scalingMode=ParticleSystemScalingMode.Hierarchy;var collisions=ps.collision;collisions.enabled=false;ps.Play(false);ps.Pause(false);}
        var visual=new Visual{root=go,systems=systems,born=clock,life=life};visuals.Add(visual);
        foreach(var emitter in prefab.GetComponentsInChildren<AbilityVfxEmitter>(true)){string path="";var node=emitter.transform;while(node!=prefab.transform&&node){path=node.name+(path.Length==0?"":"/"+path);node=node.parent;}var parent=path.Length==0?go.transform:go.transform.Find(path);if(parent)emissions.Add(new Emission{source=emitter,parent=parent,born=clock,next=clock+emitter.开始延迟});}
        var sword=prefab.GetComponent<AbilityFallingSword>();if(sword){visual.from=at;visual.to=new Vector3(at.x,dummy.position.y,at.z);visual.travel=Mathf.Max(.05f,(at.y-dummy.position.y)/Mathf.Max(.1f,sword.速度*scale));visual.keepAtImpact=true;visual.impact=sword.触地特效;visual.impactScale=scale;}
        return visual;
    }
    void Release()
    {
        if(mechanical)return;
        released=true;if(!active)return;var destination=dummy.position;
        if(active.结算方式==ActiveSkillKind.追踪弹){var v=Spawn(source,hand?hand.position:humanRoot.position+Vector3.up,Quaternion.identity,.25f,1.5f);if(v!=null){foreach(var ps in v.systems){var m=ps.main;m.simulationSpace=ParticleSystemSimulationSpace.Local;if(ps.name=="sphere-main"||ps.name=="crystals")ps.transform.localPosition=Vector3.zero;}v.from=v.root.transform.position;v.to=destination+Vector3.up*.8f;v.travel=.3f;v.impact=Resources.Load<GameObject>(string.IsNullOrEmpty(active.命中特效路径)?"CombatVFX/CombatMagic/lightning-fx/lightning-explode":active.命中特效路径);v.impactScale=.3f;}return;}
        if(falling||spiral)return;
        if(active.结算方式==ActiveSkillKind.定向水炮){var v=Spawn(source,humanRoot.position+Vector3.up*.8f,Quaternion.identity,1,1.4f);if(v!=null)AbilityVfxUtility.对准光束(v.root,v.root.transform.position,destination+Vector3.up*.8f,.035f);return;}
        var center=active.结算方式==ActiveSkillKind.四御护罩?humanRoot.position+Vector3.up*.7f:destination;
        Spawn(source,center,active.神通id=="ability_fentian_yanshu"?Quaternion.Euler(180,0,0):Quaternion.identity,active.神通id=="ability_fentian_yanshu"?.06f:active.结算方式==ActiveSkillKind.小剑阵?.16f:.3f,Mathf.Min(active.结算方式==ActiveSkillKind.小剑阵?4:3,cycleLength));
    }
    void Tick(float dt)
    {
        clock+=dt;cycleAge+=dt;if(cycleAge>=cycleLength)BeginCycle();
        if(companionGraph.IsValid())TickCompanion(dt);
        if(graph.IsValid()){float phase=cycleAge;float castWeight=castClip&&phase<castClip.length?Mathf.Min(Mathf.Clamp01(phase/.12f),Mathf.Clamp01((castClip.length-phase)/.15f)):0;float volleyWeight=0;
            if(spiral&&volleyClip){float t=cycleAge-(releaseAt-volleyClip.length*spiral.第二段出剑进度);volleyWeight=t>=0&&t<volleyClip.length?Mathf.Min(Mathf.Clamp01(t/.12f),Mathf.Clamp01((volleyClip.length-t)/.15f)):0;volleyPlayable.SetTime(Mathf.Clamp(t,0,volleyClip.length));castWeight*=1-volleyWeight;}
            if(castPlayable.IsValid())castPlayable.SetTime(Mathf.Min(phase,castClip.length));if(idlePlayable.IsValid())idlePlayable.SetTime(clock);mixer.SetInputWeight(0,1-castWeight-volleyWeight);mixer.SetInputWeight(1,castWeight);mixer.SetInputWeight(2,volleyWeight);graph.Evaluate(0);}
        if(companionPlayerChannel!=null){companionPlayerChannel.AimAt(dummy.position+Vector3.up*.8f);companionPlayerChannel.Tick(dt);}
        if(!released&&cycleAge>=releaseAt)Release();
        if(falling&&released)while(emitted<falling.数量&&cycleAge>=releaseAt+emitted*Mathf.Max(.01f,falling.间隔)){var end=dummy.position+SpiralSwordCurves.AreaOffset(emitted,falling.数量,1.25f);var start=end+Vector3.up*3;var v=Spawn(falling.飞行粒子.gameObject,start,Quaternion.LookRotation(Vector3.down,Vector3.forward),.3f,1.5f);if(v!=null){foreach(var ps in v.systems){var m=ps.main;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.startSpeed=0;var velocity=ps.velocityOverLifetime;velocity.enabled=false;}v.from=start;v.to=end;v.travel=.25f;v.impact=falling.命中特效;v.impactScale=.22f;}emitted++;}
        for(int i=0;i<swords.Count;i++){var sword=swords[i];float u=(i+.5f)/swords.Count;float launch=releaseAt+i*.004f;float flight=(cycleAge-launch)/.55f;sword.gameObject.SetActive(flight<1.8f);if(cycleAge<launch){sword.localPosition=humanRoot.localPosition+ribbon.Evaluate(Mathf.Clamp01(u+Mathf.Sin(clock*.3f)*.005f))*.65f;sword.localRotation=ribbon.SwordRotation(u);}else{sword.localPosition=flights[i].Evaluate(flight);var velocity=flights[i].Velocity(Mathf.Clamp01(flight));if(velocity.sqrMagnitude>.001f)sword.localRotation=Quaternion.LookRotation(velocity,Vector3.up);}float reveal=Mathf.Clamp01((cycleAge-u*spiral.布阵时长)/spiral.单剑显现时长);float edge=Mathf.Min(u,1-u)/spiral.端部虚化比例;float alpha=reveal*Mathf.Lerp(.08f,spiral.剑幕透明度,Mathf.Clamp01(edge));swordBlock.SetColor("_Color",new Color(.77f,.87f,1,alpha));sword.GetComponent<MeshRenderer>().SetPropertyBlock(swordBlock);}
        if(!active&&!mechanical&&selected&&(selected.神通id=="ability_leiyun"||selected.神通id=="ability_qianjie_leiyu")&&!released){released=true;Spawn(Resources.Load<GameObject>(LightningRayVfx.Path),dummy.position,Quaternion.Euler(-90,0,0),.22f,.7f);}
        for(int i=emissions.Count-1;i>=0;i--){var e=emissions[i];if(!e.parent||e.count>=e.source.生成次数){emissions.RemoveAt(i);continue;}if(clock<e.next)continue;e.count++;e.next=clock+Mathf.Max(.01f,e.source.生成间隔);foreach(var child in e.source.预制体){if(!child)continue;var random=e.source.随机位置;var offset=new Vector3(Random.Range(-random.x,random.x),Random.Range(-random.y,random.y),Random.Range(-random.z,random.z));var angle=e.source.随机角度;Spawn(child,e.parent.TransformPoint(offset),e.parent.rotation*Quaternion.Euler(Random.Range(-angle.x,angle.x),Random.Range(-angle.y,angle.y),Random.Range(-angle.z,angle.z)),Mathf.Abs(e.parent.lossyScale.x)*child.transform.localScale.x,3);}}
        for(int i=visuals.Count-1;i>=0;i--){var v=visuals[i];if(!v.root){visuals.RemoveAt(i);continue;}float age=clock-v.born;if(age>=v.life){Destroy(v.root);visuals.RemoveAt(i);continue;}if(v.travel>0){float t=Mathf.Clamp01(age/v.travel);v.root.transform.position=Vector3.Lerp(v.from,v.to,t);if(t>=1&&!v.impacted){v.impacted=true;if(!v.keepAtImpact)v.root.SetActive(false);Spawn(v.impact,v.to,Quaternion.identity,v.impactScale,2);continue;}}foreach(var ps in v.systems){if(!ps||!ps.gameObject.activeInHierarchy)continue;bool parentSystem=false;for(var parent=ps.transform.parent;parent&&parent!=v.root.transform.parent;parent=parent.parent)if(parent.GetComponent<ParticleSystem>()){parentSystem=true;break;}if(!parentSystem)ps.Simulate(dt,true,false,true);}}
    }
    void LateUpdate()
    {
        if(!stage||!image||Time.unscaledTime<nextRender)return;float dt=Mathf.Min(.1f,Time.unscaledTime-nextRender+1f/30);nextRender=Time.unscaledTime+1f/30;if(!UIInkMotion.减少动效)Tick(dt);
        var rect=image.rectTransform.rect;if(rect.width<1||rect.height<1)return;int width=Mathf.Clamp(Mathf.RoundToInt(rect.width),256,1024),height=Mathf.Clamp(Mathf.RoundToInt(rect.height),256,1024);
        if(!target||target.width!=width||target.height!=height){if(target){camera3d.targetTexture=null;target.Release();Destroy(target);}target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);target.Create();camera3d.targetTexture=target;image.texture=target;}
        if(companionGraph.IsValid())FitCompanion();
        var rotation=Quaternion.Euler(40,yaw,0);var extent=bounds.extents;var up=rotation*Vector3.up;var right=rotation*Vector3.right;float vertical=Mathf.Abs(up.x)*extent.x+Mathf.Abs(up.y)*extent.y+Mathf.Abs(up.z)*extent.z;float horizontal=Mathf.Abs(right.x)*extent.x+Mathf.Abs(right.y)*extent.y+Mathf.Abs(right.z)*extent.z;
        camera3d.aspect=width/(float)height;camera3d.orthographicSize=Mathf.Max(vertical,horizontal/camera3d.aspect)*1.04f;camera3d.transform.position=bounds.center+rotation*new Vector3(0,0,-22);camera3d.transform.LookAt(bounds.center);camera3d.Render();Label(humanLabel,humanRoot.position+Vector3.up*1.7f);Label(dummyLabel,dummy.position-Vector3.up*.25f);
    }
    void Label(Text label,Vector3 point){var uv=camera3d.WorldToViewportPoint(point);label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(uv.x,uv.y);label.rectTransform.anchoredPosition=Vector2.zero;}
    void 同步外观(AppearanceDefinition unused){if(!appearanceSource||!clonedVisual)return;var original=appearanceSource.GetComponentsInChildren<SkinnedMeshRenderer>(true);var copies=clonedVisual.GetComponentsInChildren<SkinnedMeshRenderer>(true);for(int i=0;i<Mathf.Min(original.Length,copies.Length);i++){copies[i].sharedMesh=original[i].sharedMesh;copies[i].sharedMaterials=original[i].sharedMaterials;copies[i].updateWhenOffscreen=true;}}
    public void OnDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)yaw-=e.delta.x*.2f;}
    void OnEnable(){玩家外观.外观变化+=同步外观;if(stage)stage.gameObject.SetActive(true);同步外观(null);nextRender=Time.unscaledTime;}
    void OnDisable(){玩家外观.外观变化-=同步外观;if(stage)stage.gameObject.SetActive(false);}
    void OnDestroy(){companionPlayerChannel?.Dispose();companionManifest?.Dispose();companionBreath?.Dispose();if(companionGraph.IsValid())companionGraph.Destroy();if(graph.IsValid())graph.Destroy();if(camera3d)camera3d.targetTexture=null;if(stage)Destroy(stage.gameObject);if(target){target.Release();Destroy(target);}foreach(var mat in ownedMaterials)Destroy(mat);}
}
