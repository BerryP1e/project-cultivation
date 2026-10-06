using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>青山剑：击杀成长、扇形剑列、错相螺旋出鞘与多方向穿刺。</summary>
public class QingshanSwordTreasure : MonoBehaviour
{
    public const string 法宝id="treasure_qingshan_sword";
    public enum SwordState { 悬浮, 飞行 }
    public enum UltimatePhase { Idle, Recall, Ascend, Gather, Dive, Pierce, Recover }
    public Vector3 模型朝向补偿=new Vector3(225,0,0);
    public Vector3 悬浮偏移=new Vector3(.55f,1.15f,-.45f);
    public Vector3 剑尖向下旋转=new Vector3(90,0,0);
    public GameObject 剑模型资源;
    public PlayerCombatStats 玩家战斗属性;
    public NpcTargeting 目标管理器;
    public bool 打印战斗日志;
    public float 飞行速度=14,悬浮跟随速度=10;
    [Header("悬浮跟随")]
    public float 悬浮缓动时间=.18f,悬浮转向时间=.2f;
    public float 悬浮起伏幅度=.07f,悬浮起伏周期=2.8f;
    public float 最大跟随拖尾=.85f;
    [Tooltip("整组飞剑额外向角色后方让出的距离") ]
    public float 悬浮后移距离=.65f;
    Vector3 hoverAnchor,hoverAnchorVelocity;Quaternion hoverFacing;bool hoverInitialized;float hoverTime;
    public float 基础索敌范围=4,每点神识范围=.8f,索敌范围上限=40;
    public float 万剑冷却=18,万剑伤害倍率=6;
    [Tooltip("万剑归宗巨剑相对普通飞剑的最小倍率，三剑起使用此大小") ]
    public float 巨剑基础倍率=10.13f;
    public float 万剑命中特效倍率=.22f,万剑消散时间=1.2f,巨剑贯穿时间=1.2f;
    UIPanelData 面板;
    NpcInstance 内部锁定,ultimateTarget;
    Transform 剑体,giant;
    QingshanSwordParticles particles;
    float phaseAge,phaseDuration,cooldownUntil;
    Vector3 giantStart,giantDirection,giantImpactPoint;
    float giantScale,giantSpawnTipHeight,giantContactHeight,giantPierceDistance;
    Vector3 giantTipOffset,pierceStart;QingshanSwordDissolve giantCut;
    readonly List<Blade> blades=new List<Blade>();
    class Blade { public Transform root;public Vector3 start,entry,exit,hoverVelocity;public float age,launchDuration;public int stage,pass;public bool hit; }
    public bool IsEquipped=>面板!=null && 面板.当前法宝!=null && 面板.当前法宝.法宝id==法宝id && 面板.已拥有(面板.当前法宝);
    public int SwordCount=>面板!=null?面板.青山剑数量:1;
    public Transform Sword=>剑体;
    public IReadOnlyList<Transform> Swords { get { var result=new List<Transform>();foreach(var b in blades)result.Add(b.root);return result; } }
    public NpcInstance 锁定目标=>目标管理器!=null?目标管理器.LockedNpc:内部锁定;
    public NpcInstance 攻击目标 {get;private set;}
    public NpcInstance AttackTarget=>攻击目标;
    public SwordState 状态 {get;private set;}
    public bool IsAttacking=>攻击目标!=null && UltimateState==UltimatePhase.Idle;
    public bool 攻击中=>IsAttacking;
    public UltimatePhase UltimateState {get;private set;}
    public float UltimateCooldown=>Mathf.Max(0,cooldownUntil-Time.time);
    public float AttackSpeedFactor=>玩家战斗属性!=null?Mathf.Max(.1f,玩家战斗属性.当前属性[AttributeType.AttackSpeed]):1;
    public float SpeedBonusFactor=>1+(AttackSpeedFactor-1)*.5f;
    public float ActualFlySpeed=>飞行速度*SpeedBonusFactor;
    public float SpiritSense=>玩家战斗属性!=null?玩家战斗属性.当前神识:0;
    public float SenseRange=>Mathf.Min(索敌范围上限,基础索敌范围+SpiritSense*每点神识范围);
    public float UnlockRange=>SenseRange;
    public event Action<NpcInstance,AttackResult> OnHitLanded;
    void Awake(){Resolve();}
    void Resolve(){if(面板==null)面板=FindObjectOfType<UIPanelData>();if(玩家战斗属性==null)玩家战斗属性=GetComponent<PlayerCombatStats>();if(目标管理器==null)目标管理器=GetComponent<NpcTargeting>();}
    void OnEnable()=>NpcInstance.CombatKilled+=Killed;
    void OnDisable(){NpcInstance.CombatKilled-=Killed;Clear();}
    void OnDestroy()=>Clear();
    void Killed(NpcInstance npc,ICombatStats source){
        Resolve();bool credited=ReferenceEquals(source,玩家战斗属性);
        if(source is NpcInstance attacker){var spirit=attacker.GetComponent<FormationSpirit>();credited=spirit!=null && spirit.管理器!=null && spirit.管理器.主人Transform==transform;}
        if(credited && IsEquipped)面板.记录青山剑击杀(npc,GetComponent<PlayerCultivation>()?.等级 ?? 1);
    }
    public Vector3 HoverLocal(int index,int count){
        var clearance=Vector3.back*Mathf.Max(0,悬浮后移距离);
        if(count==1)return 悬浮偏移+clearance;
        if(count==2)return new Vector3(index==0?-.55f:.55f,1.15f,-.45f)+clearance;
        float angle=Mathf.Lerp(-55,55,index/(float)(count-1))*Mathf.Deg2Rad;
        float radius=1.05f+(count-3)*.06f;
        return new Vector3(Mathf.Sin(angle)*radius,1.15f+Mathf.Cos(angle)*.65f,-.55f)+clearance;
    }
    public Quaternion HoverRotation(int index,int count){
        if(count<=2)return transform.rotation*Quaternion.Euler(剑尖向下旋转);
        float angle=Mathf.Lerp(-55,55,index/(float)(count-1))*Mathf.Deg2Rad;
        return Quaternion.LookRotation(transform.rotation*new Vector3(-Mathf.Sin(angle)*.7f,-Mathf.Cos(angle),0),transform.forward);
    }
    Transform CreateSword(string name){
        if(剑模型资源==null)剑模型资源=Resources.Load<GameObject>("法宝/青山剑/青山剑模型");
        if(剑模型资源==null)return null;
        var root=new GameObject(name).transform;var visual=Instantiate(剑模型资源,root,false);
        visual.name="Model";visual.transform.localRotation=Quaternion.Euler(模型朝向补偿);
        foreach(var collider in visual.GetComponentsInChildren<Collider>())collider.enabled=false;
        return root;
    }
    void EnsureBlades(){
        // 命中致死恰好升级时，也要先完成当前巨剑贯穿，再补出新增飞剑。
        if(UltimateState==UltimatePhase.Pierce || UltimateState==UltimatePhase.Recover)return;
        bool changed=blades.Count!=SwordCount;
        if(changed && UltimateState!=UltimatePhase.Idle)CancelUltimate();
        while(blades.Count<SwordCount){int i=blades.Count;var root=CreateSword("青山剑_"+(i+1));if(root==null)return;root.position=transform.TransformPoint(HoverLocal(i,SwordCount));root.rotation=HoverRotation(i,SwordCount);var blade=new Blade{root=root};blades.Add(blade);if(攻击目标!=null)ResetFlight(blade,i);}
        while(blades.Count>SwordCount){Destroy(blades[blades.Count-1].root.gameObject);blades.RemoveAt(blades.Count-1);}
        剑体=blades.Count>0?blades[0].root:null;
        if(changed && 攻击目标!=null)for(int i=0;i<blades.Count;i++)ResetFlight(blades[i],i);
    }
    void Update(){
        Resolve();if(!IsEquipped || (GetComponent<PlayerVitals>()?.IsDead ?? false)){Clear();return;}
        if(UiEscRegistry.SceneInputBlocked || (GetComponent<演出锁>()?.正在锁 ?? false))return;
        UpdateHoverAnchor();EnsureBlades();if(UltimateState!=UltimatePhase.Idle){UpdateUltimate();return;}
        var candidate=锁定目标;
        if(candidate==null || candidate.IsDead || Vector3.Distance(transform.position,candidate.transform.position)>SenseRange)candidate=null;
        if(candidate!=攻击目标){攻击目标=candidate;状态=candidate!=null?SwordState.飞行:SwordState.悬浮;for(int i=0;i<blades.Count;i++)ResetFlight(blades[i],i);}
        for(int i=0;i<blades.Count;i++){
            var b=blades[i];if(攻击目标==null)UpdateHover(b,i);
            else UpdateFlight(b,i);
        }
    }
    void UpdateHoverAnchor(){
        if(!hoverInitialized || Vector3.Distance(hoverAnchor,transform.position)>6){
            hoverAnchor=transform.position;hoverAnchorVelocity=Vector3.zero;hoverFacing=transform.rotation;hoverInitialized=true;
            // 场景内瞬移时重置跟随惯性，避免剑列从远处横穿场景。
            if(攻击目标==null && UltimateState==UltimatePhase.Idle)for(int i=0;i<blades.Count;i++){
                blades[i].root.position=transform.TransformPoint(HoverLocal(i,blades.Count));blades[i].hoverVelocity=Vector3.zero;
            }
        }
        hoverTime+=Time.deltaTime;
        hoverAnchor=Vector3.SmoothDamp(hoverAnchor,transform.position,ref hoverAnchorVelocity,Mathf.Max(.01f,悬浮缓动时间),Mathf.Infinity,Time.deltaTime);
        hoverAnchor=transform.position+Vector3.ClampMagnitude(hoverAnchor-transform.position,Mathf.Max(0,最大跟随拖尾));
        hoverFacing=Quaternion.Slerp(hoverFacing,transform.rotation,1-Mathf.Exp(-Time.deltaTime/Mathf.Max(.01f,悬浮转向时间)));
    }
    void UpdateHover(Blade blade,int index){
        float phase=hoverTime*Mathf.PI*2/Mathf.Max(.1f,悬浮起伏周期)+index*1.37f;
        var floatOffset=new Vector3(Mathf.Sin(phase*.73f)*.015f,Mathf.Sin(phase)*悬浮起伏幅度,Mathf.Cos(phase)*.02f);
        var target=hoverAnchor+hoverFacing*(HoverLocal(index,blades.Count)+floatOffset);
        blade.root.position=Vector3.SmoothDamp(blade.root.position,target,ref blade.hoverVelocity,.1f,Mathf.Max(悬浮跟随速度,ActualFlySpeed),Time.deltaTime);
        var orientation=hoverFacing*Quaternion.Inverse(transform.rotation)*HoverRotation(index,blades.Count)*Quaternion.Euler(Mathf.Sin(phase)*1.5f,0,Mathf.Cos(phase)*1.5f);
        blade.root.rotation=Quaternion.Slerp(blade.root.rotation,orientation,1-Mathf.Exp(-8*Time.deltaTime));
    }
    Vector3 Aim(NpcInstance target)=>target.transform.position+Vector3.up*.8f;
    public static Vector3 StrikeAxis(int index,int pass){float a=(index*137.50776f+pass*97)*Mathf.Deg2Rad;return new Vector3(Mathf.Cos(a),.12f+.12f*Mathf.Sin(a*1.7f),Mathf.Sin(a)).normalized;}
    void ResetFlight(Blade b,int index){b.hoverVelocity=Vector3.zero;b.stage=0;b.age=-index*.09f;b.pass=0;b.hit=false;b.start=b.root.position;if(攻击目标!=null){b.entry=StrikeAxis(index,0)*(2.2f+index*.08f);b.launchDuration=Mathf.Clamp(Vector3.Distance(b.start,Aim(攻击目标)+b.entry)/ActualFlySpeed,.45f,2);}}
    // 三段曲线共享端点切线；螺旋和抬升在端点的导数均为零，避免入刺/绕回突然折角。
    public static Vector3 FlightCurve(Vector3 start,Vector3 end,Vector3 startTangent,Vector3 endTangent,float t){
        float u=1-t;return u*u*u*start+3*u*u*t*(start+startTangent/3)+3*u*t*t*(end-endTangent/3)+t*t*t*end;
    }
    float StrikeDuration=>.5f/Mathf.Min(3,AttackSpeedFactor);
    void UpdateFlight(Blade b,int i){
        b.age+=Time.deltaTime;if(b.age<0)return;var center=Aim(攻击目标);var old=b.root.position;
        if(b.stage==0){
            float duration=b.launchDuration;float t=Mathf.Clamp01(b.age/duration);var end=center+b.entry;
            var forward=(end-b.start).normalized;var side=Vector3.Cross(forward,Vector3.up).normalized;if(side.sqrMagnitude<.01f)side=Vector3.right;var up=Vector3.Cross(side,forward).normalized;
            float phase=i*Mathf.PI*2/Mathf.Max(1,blades.Count)+t*Mathf.PI*2.5f;float envelope=Mathf.Pow(Mathf.Sin(t*Mathf.PI),2);
            var incoming=-b.entry*2/StrikeDuration;
            b.root.position=FlightCurve(b.start,end,end-b.start,incoming*duration,t)+(side*Mathf.Cos(phase)+up*Mathf.Sin(phase))*envelope*(blades.Count>1?.65f:0);
            if(t>=1){b.stage=1;b.age=0;b.exit=-b.entry;b.hit=false;}
        }else if(b.stage==1){float t=Mathf.Clamp01(b.age/StrikeDuration);b.root.position=center+Vector3.Lerp(b.entry,b.exit,t);
            if(!b.hit && SegmentDistance(center,old,b.root.position)<.65f){b.hit=true;Hit(攻击目标,b.root.forward,false);}
            if(t>=1){b.stage=2;b.age=0;b.pass++;b.entry=StrikeAxis(i,b.pass)*(2.2f+i*.08f);}
        }else {
            float duration=.65f/Mathf.Min(2,SpeedBonusFactor);float t=Mathf.Clamp01(b.age/duration);
            b.root.position=center+FlightCurve(b.exit,b.entry,b.exit*2/StrikeDuration*duration,-b.entry*2/StrikeDuration*duration,t)+Vector3.up*Mathf.Pow(Mathf.Sin(t*Mathf.PI),2)*(.35f+i*.04f);
            if(t>=1){b.stage=1;b.age=0;b.exit=-b.entry;b.hit=false;}
        }
        if(攻击目标!=null)b.root.position=new Vector3(b.root.position.x,Mathf.Max(攻击目标.transform.position.y+.1f,b.root.position.y),b.root.position.z);
        var movement=b.root.position-old;if(movement.sqrMagnitude>.00001f)b.root.rotation=Quaternion.Slerp(b.root.rotation,Quaternion.LookRotation(movement.normalized),1-Mathf.Exp(-24*Time.deltaTime));
    }
    static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b){var d=b-a;float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0;return Vector3.Distance(p,a+d*t);}
    void Hit(NpcInstance target,Vector3 direction,bool ultimate){
        if(target==null || target.IsDead || 玩家战斗属性==null)return;
        var result=target.ReceiveAttack(玩家战斗属性,ultimate?new AttackSpec(DamageNature.物理,AttackKind.主动神通,true,万剑伤害倍率):AttackSpec.物理普通攻击);
        if(result.命中 && !ultimate)SwordHitEffect.Spawn(Aim(target),direction,result.暴击);OnHitLanded?.Invoke(target,result);
    }
    public bool TryCastUltimate(){
        Resolve();if(!IsEquipped || UiEscRegistry.SceneInputBlocked || (GetComponent<PlayerVitals>()?.IsDead ?? false) || (GetComponent<演出锁>()?.正在锁 ?? false) || UltimateState!=UltimatePhase.Idle)return false;
        if(SwordCount<3){面板.ShowHint("三剑时解锁万剑归宗");return false;}
        if(UltimateCooldown>0){面板.ShowHint("万剑归宗尚在冷却");return false;}
        var target=锁定目标;if(target==null || target.IsDead || Vector3.Distance(transform.position,target.transform.position)>SenseRange){面板.ShowHint("请锁定神识范围内的目标");return false;}
        EnsureBlades();if(blades.Count!=SwordCount || 玩家战斗属性==null){面板.ShowHint("飞剑模型或战斗属性尚未就绪");return false;}ultimateTarget=target;cooldownUntil=Time.time+万剑冷却;攻击目标=null;状态=SwordState.悬浮;
        float distance=0;for(int i=0;i<blades.Count;i++)distance=Mathf.Max(distance,Vector3.Distance(blades[i].root.position,transform.TransformPoint(HoverLocal(i,blades.Count))));
        phaseDuration=Mathf.Max(.5f,distance/20+.1f);SetPhase(UltimatePhase.Recall);return true;
    }
    void SetPhase(UltimatePhase phase){UltimateState=phase;phaseAge=0;}
    void UpdateUltimate(){
        if(UltimateState!=UltimatePhase.Recover && UltimateState!=UltimatePhase.Pierce && (ultimateTarget==null || ultimateTarget.IsDead)){CancelUltimate();return;}
        phaseAge+=Time.deltaTime;
        if(UltimateState==UltimatePhase.Recall){for(int i=0;i<blades.Count;i++){var b=blades[i];b.root.position=Vector3.MoveTowards(b.root.position,transform.TransformPoint(HoverLocal(i,blades.Count)),20*Time.deltaTime);b.root.rotation=Quaternion.Slerp(b.root.rotation,HoverRotation(i,blades.Count),Time.deltaTime*12);}
            if(phaseAge>=phaseDuration){var starts=new List<Vector3>();foreach(var b in blades){QingshanSwordParticles.SwordPoints(b.root,48,starts);b.root.gameObject.SetActive(false);}var ends=new Vector3[starts.Count];for(int i=0;i<ends.Length;i++)ends[i]=starts[i]+Vector3.up*20;
                particles=QingshanSwordParticles.Play(starts.ToArray(),ends,.95f,true);SetPhase(UltimatePhase.Ascend);}}
        else if(UltimateState==UltimatePhase.Ascend && phaseAge>=1){
            giant=CreateSword("万剑归宗_凝聚巨剑");giantScale=Mathf.Max(.1f,巨剑基础倍率)+Mathf.Max(0,SwordCount-3)*.15f;
            giantDirection=Vector3.down;giant.rotation=Quaternion.LookRotation(Vector3.down,transform.forward);giant.localScale=Vector3.one*giantScale;
            giantImpactPoint=ultimateTarget.transform.position;giantContactHeight=TargetHeight(ultimateTarget);giantSpawnTipHeight=giantContactHeight+3;
            giantTipOffset=giant.TransformVector(QingshanSwordParticles.TipLocal());
            giant.position=giantImpactPoint+Vector3.up*giantSpawnTipHeight-giantTipOffset;
            var ends=new List<Vector3>();QingshanSwordParticles.SwordPoints(giant,800,ends);var starts=new Vector3[ends.Count];for(int i=0;i<starts.Length;i++)starts[i]=ends[i]+UnityEngine.Random.onUnitSphere*UnityEngine.Random.Range(3,6);
            particles=QingshanSwordParticles.Play(starts,ends.ToArray(),2,false);particles.TrackSword(giant,giantScale);giant.gameObject.SetActive(false);giantStart=giant.position;SetPhase(UltimatePhase.Gather);
        }else if(UltimateState==UltimatePhase.Gather){UpdateGiantAim();
            if(phaseAge>=1.6f){giant.gameObject.SetActive(true);giant.localScale=Vector3.one*giantScale*Mathf.SmoothStep(.05f,1,Mathf.Clamp01((phaseAge-1.6f)/.4f));}
            if(phaseAge>=2){giant.localScale=Vector3.one*giantScale;giantCut=giant.gameObject.AddComponent<QingshanSwordDissolve>();giantCut.Initialize();SetPhase(UltimatePhase.Dive);}
        }else if(UltimateState==UltimatePhase.Dive){
            float t=Mathf.Clamp01(phaseAge/.5f);UpdateGiantAim();
            giant.position=giantImpactPoint+Vector3.up*Mathf.Lerp(giantSpawnTipHeight,giantContactHeight,t*t)-giantTipOffset;
            if(t>=1){
                // 首次接触时结算一次伤害；目标死亡也继续完成整把剑的贯穿和消散。
                SpawnImpact(giantImpactPoint,Vector3.down);Hit(ultimateTarget,Vector3.down,true);
                pierceStart=giant.position;giantPierceDistance=QingshanSwordParticles.BladeLength(giant)+giantContactHeight+.25f;
                giantCut.UpdateCut(giantImpactPoint.y+.05f);SetPhase(UltimatePhase.Pierce);
            }
        }else if(UltimateState==UltimatePhase.Pierce){
            float t=Mathf.Clamp01(phaseAge/Mathf.Max(.1f,巨剑贯穿时间));
            giant.position=pierceStart+Vector3.down*(giantPierceDistance*t);giantCut.UpdateCut(giantImpactPoint.y+.05f);
            if(t>=1){Destroy(giant.gameObject);giant=null;giantCut=null;SetPhase(UltimatePhase.Recover);}
        }else if(UltimateState==UltimatePhase.Recover && phaseAge>=Mathf.Max(.1f,万剑消散时间))CancelUltimate();
    }
    static float TargetHeight(NpcInstance target){
        float height=.8f;foreach(var renderer in target.GetComponentsInChildren<Renderer>())
            if(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)height=Mathf.Max(height,renderer.bounds.max.y-target.transform.position.y);
        return Mathf.Clamp(height,.5f,5);
    }
    void UpdateGiantAim(){
        // 始终以目标根节点定位，方向保持垂直；补偿真实模型剑尖的横向轴心偏移。
        giantImpactPoint=Vector3.Lerp(giantImpactPoint,ultimateTarget.transform.position,1-Mathf.Exp(-12*Time.deltaTime));
        giantDirection=Vector3.down;
        if(UltimateState==UltimatePhase.Gather)giant.position=giantImpactPoint+Vector3.up*giantSpawnTipHeight-giantTipOffset;
    }
    void SpawnImpact(Vector3 point,Vector3 direction){
        var prefab=Resources.Load<GameObject>("特效/法术/AllEffects/EffectsSet_1(NotScriptBased)/Effects/Effect_07_OneHandSmash/Effect_07_OneHandSmash");if(prefab==null)return;
        var fx=Instantiate(prefab,point,Quaternion.LookRotation(direction));fx.name="万剑归宗_剑尖砸地";
        fx.transform.localScale*=Mathf.Max(.001f,万剑命中特效倍率)*Mathf.Max(1,giantScale/10.13f);
        foreach(var particle in fx.GetComponentsInChildren<ParticleSystem>(true)){var main=particle.main;main.simulationSpeed*=.55f;}
        Destroy(fx,8);
    }
    void CancelUltimate(){if(giant!=null)Destroy(giant.gameObject);giant=null;giantCut=null;if(particles!=null)Destroy(particles.gameObject);particles=null;ultimateTarget=null;UltimateState=UltimatePhase.Idle;foreach(var b in blades)if(b.root!=null)b.root.gameObject.SetActive(true);攻击目标=null;}
    void Clear(){hoverInitialized=false;hoverAnchorVelocity=Vector3.zero;CancelUltimate();foreach(var b in blades)if(b.root!=null)Destroy(b.root.gameObject);blades.Clear();剑体=null;攻击目标=null;状态=SwordState.悬浮;}
    public bool BeginAttack(){Resolve();if(!IsEquipped || UiEscRegistry.SceneInputBlocked || 锁定目标==null)return false;攻击目标=锁定目标;for(int i=0;i<blades.Count;i++)ResetFlight(blades[i],i);return true;}
    public void 设置锁定目标(NpcInstance npc){if(目标管理器!=null)目标管理器.Lock(npc,true);else 内部锁定=npc;}
    public void 停止攻击(){攻击目标=null;状态=SwordState.悬浮;}
}
