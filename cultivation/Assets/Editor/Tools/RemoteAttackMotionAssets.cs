using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

/// <summary>从嫦娥原始 Attack1 重烘通用远程施法；保留当前片段在玩家上的髋高，不修改源 FBX/场景。</summary>
public static class RemoteAttackMotionAssets
{
    public const string MotionPath="Assets/resources/技能动作/普攻_远程_01.anim";
    public const string ModelPath="Assets/resources/NPC/Human/ChangE/ChangE.FBX";
    const string SourcePath="Assets/resources/NPC/Human/ChangE_Animation/ChangE@Attack1.FBX";
    const string TempFolder="Assets/__RemoteAttackBakeTemp";

    // Reimport a disposable copy as Generic: the original Humanoid importer has already
    // solved the joints and cannot serve as an independent source of the authored poses.
    public static string CreateNativeCopy()
    {
        if(AssetDatabase.IsValidFolder(TempFolder))throw new InvalidOperationException("临时远程烘焙目录已存在，请先检查其内容："+TempFolder);
        AssetDatabase.CreateFolder("Assets","__RemoteAttackBakeTemp");
        string path=TempFolder+"/Attack1.FBX";
        AssetDatabase.CopyAsset(SourcePath,path);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.NoAvatar;
        importer.sourceAvatar=null;importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();return path;
    }
    public static void DeleteNativeCopy()=>AssetDatabase.DeleteAsset(TempFolder);

    public sealed class NativeRig : IDisposable
    {
        public readonly GameObject root;
        public readonly Animator animator;
        public readonly Avatar avatar;
        readonly Transform[] bones;
        readonly Vector3[] positions,scales;
        readonly Quaternion[] rotations;
        readonly EditorCurveBinding[] bindings;
        readonly AnimationCurve[] curves;
        readonly int[] indices;
        public NativeRig(AnimationClip raw)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            root=Object.Instantiate(model);root.name=model.name;
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var description=root.GetComponent<Animator>().avatar.humanDescription;
            Object.DestroyImmediate(root.GetComponent<Animator>());
            bones=root.GetComponentsInChildren<Transform>(true);
            positions=bones.Select(t=>t.localPosition).ToArray();rotations=bones.Select(t=>t.localRotation).ToArray();scales=bones.Select(t=>t.localScale).ToArray();
            var named=bones.ToDictionary(t=>t.name,t=>t);
            // Start from the importer's reference skeleton, not Attack1's first frame.
            foreach(var b in description.skeleton)if(named.TryGetValue(b.name,out var t)){t.localPosition=b.position;t.localRotation=b.rotation;t.localScale=b.scale;}
            foreach(string side in new[]{"L","R"}){
                Transform B(string part)=>named["Bip01 "+side+" "+part];
                Vector3 direction=side=="L"?Vector3.left:Vector3.right;
                var shoulder=B("Clavicle");var upper=B("UpperArm");var lower=B("Forearm");var hand=B("Hand");
                shoulder.rotation=Quaternion.FromToRotation(upper.position-shoulder.position,direction)*shoulder.rotation;
                upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,direction)*lower.rotation;
                var finger=B("Finger1").position-hand.position;
                var normal=Vector3.Cross(finger,B("Finger0").position-hand.position)*(side=="L"?1f:-1f);
                hand.rotation=Quaternion.LookRotation(direction,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(finger,normal))*hand.rotation;
            }
            description.skeleton=bones.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
            avatar=AvatarBuilder.BuildHumanAvatar(root,description);
            if(!avatar.isHuman||!avatar.isValid)throw new InvalidOperationException("嫦娥临时 Avatar 无效");
            animator=root.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;
            var lookup=bones.Select((t,i)=>new{path=AnimationUtility.CalculateTransformPath(t,root.transform),i}).ToDictionary(x=>x.path,x=>x.i);
            bindings=AnimationUtility.GetCurveBindings(raw).Where(b=>b.type==typeof(Transform)&&lookup.ContainsKey(b.path)).ToArray();
            curves=bindings.Select(b=>AnimationUtility.GetEditorCurve(raw,b)).ToArray();indices=bindings.Select(b=>lookup[b.path]).ToArray();
            Sample(0);
        }
        public void Sample(float time)
        {
            var ps=(Vector3[])positions.Clone();var qs=(Quaternion[])rotations.Clone();var ss=(Vector3[])scales.Clone();
            for(int j=0;j<bindings.Length;j++){
                string property=bindings[j].propertyName;int axis="xyzw".IndexOf(property[property.Length-1]);if(axis<0)continue;
                int i=indices[j];float v=curves[j].Evaluate(time);
                if(property.Contains("LocalPosition"))ps[i][axis]=v;
                else if(property.Contains("LocalRotation"))qs[i][axis]=v;
                else if(property.Contains("LocalScale"))ss[i][axis]=v;
            }
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=ps[i];bones[i].localRotation=qs[i].normalized;bones[i].localScale=ss[i];}
        }
        public void Dispose(){Object.DestroyImmediate(root);Object.DestroyImmediate(avatar);}
    }

    [MenuItem("修仙/动画/重建太虚炼气诀共用普攻（保留高度，不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止运行");
        var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(MotionPath);
        var original=GameObject.Find("Player")?.GetComponentInChildren<Animator>(true);
        if(!old||!original)throw new InvalidOperationException("需要现有普攻片段和场景 Player 以保留高度");
        string path=CreateNativeCopy();
        try{
            var raw=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            if(Mathf.Abs(old.length-raw.length)>.001f)throw new InvalidOperationException("源动作与现有动作时长不一致");
            using(var source=new NativeRig(raw))Bake(source,raw,old,original);
        }finally{DeleteNativeCopy();}
    }
    static void Bake(NativeRig source,AnimationClip raw,AnimationClip old,Animator original)
    {
        var target=Object.Instantiate(original.gameObject);
        foreach(var script in target.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
        target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
        var a=target.GetComponent<Animator>();a.runtimeAnimatorController=null;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        // Separate animation streams: old clips contain TDOFs that otherwise leak through
        // a shared Animator when switching to a muscle-only clip during the height audit.
        var reference=Object.Instantiate(target);var referenceAnimator=reference.GetComponent<Animator>();
        var graph=PlayableGraph.Create("RemoteAttackHeightReference");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var play=AnimationClipPlayable.Create(graph,old);play.SetApplyFootIK(false);
        AnimationPlayableOutput.Create(graph,"Height",referenceAnimator).SetSourcePlayable(play);graph.Play();
        var result=new AnimationClip{name=old.name,frameRate=240};
        try{
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            var pose=new HumanPose();Quaternion previous=Quaternion.identity;
            using(var sourceHandler=new HumanPoseHandler(source.avatar,source.root.transform))
            using(var targetHandler=new HumanPoseHandler(a.avatar,target.transform))
            for(int f=0,frames=Mathf.CeilToInt(raw.length*240);f<=frames;f++){
                float time=raw.length*f/frames;play.SetTime(time);graph.Evaluate(0);
                float height=referenceAnimator.GetBoneTransform(HumanBodyBones.Hips).position.y;
                source.Sample(time);sourceHandler.GetHumanPose(ref pose);
                // This player has no separate humanoid Neck. Do not stack the source's
                // neck sway onto the head; keep the cast's gaze readable instead.
                for(int i=0;i<pose.muscles.Length;i++){
                    string muscle=HumanTrait.MuscleName[i];
                    if(muscle.StartsWith("Neck "))pose.muscles[i]=0;
                    else if(muscle.StartsWith("Head "))pose.muscles[i]=Mathf.Clamp(pose.muscles[i]*.3f,-.25f,.25f);
                }
                // Only rotation/flexion is transferred. Never import the source's floating
                // elevation, extra cloth paths, IK anchors or translation DOFs.
                pose.bodyPosition=new Vector3(0,1,0);targetHandler.SetHumanPose(ref pose);
                // Match the torso's geometric frame as well. Muscle retargeting alone can
                // keep limb angles plausible while leaning/turning the whole body incorrectly.
                var pelvis=a.GetBoneTransform(HumanBodyBones.Hips);
                pelvis.rotation=TorsoFrame(source.animator)*Quaternion.Inverse(TorsoFrame(a))*pelvis.rotation;
                var transfer=TorsoFrame(a)*Quaternion.Inverse(TorsoFrame(source.animator));
                foreach(string side in new[]{"Left","Right"}){
                    Transform S(string part)=>source.animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+part));
                    Transform T(string part)=>a.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+part));
                    AlignChain(S("UpperArm"),S("LowerArm"),S("Hand"),T("UpperArm"),T("LowerArm"),T("Hand"),transfer);
                    var desired=transfer*PalmFrame(S("Hand"),S("IndexProximal"),S("ThumbProximal"));
                    T("Hand").rotation=desired*Quaternion.Inverse(PalmFrame(T("Hand"),T("IndexProximal"),T("ThumbProximal")))*T("Hand").rotation;
                    AlignChain(S("UpperLeg"),S("LowerLeg"),S("Foot"),T("UpperLeg"),T("LowerLeg"),T("Foot"),transfer);
                    var foot=transfer*Quaternion.LookRotation(S("Toes").position-S("Foot").position,S("LowerLeg").position-S("Foot").position);
                    var current=Quaternion.LookRotation(T("Toes").position-T("Foot").position,T("LowerLeg").position-T("Foot").position);
                    T("Foot").rotation=foot*Quaternion.Inverse(current)*T("Foot").rotation;
                }
                // Keep the already compensated hip trajectory exactly, including its small
                // authored rise/fall. Moving Hips shifts the whole skeleton without changing joints.
                var hip=a.GetBoneTransform(HumanBodyBones.Hips);hip.position+=Vector3.up*(height-hip.position.y);
                targetHandler.GetHumanPose(ref pose);var q=pose.bodyRotation;
                if(f>0&&Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                for(int i=0;i<curves.Length;i++){
                    int n=i-HumanTrait.MuscleCount;float v=n<0?pose.muscles[i]:n<3?pose.bodyPosition[n]:q[n-3];
                    if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("非法姿态数据");
                    curves[i].AddKey(new Keyframe(time,v));
                }
            }
            for(int i=0;i<curves.Length;i++){
                var c=curves[i];for(int k=0;k<c.length;k++){AnimationUtility.SetKeyLeftTangentMode(c,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,k,AnimationUtility.TangentMode.Linear);}
                if(c.keys.All(k=>Mathf.Abs(k.value-c.keys[0].value)<.00001f))c=AnimationCurve.Linear(0,c.keys[0].value,raw.length,c.keys[0].value);
                int n=i-HumanTrait.MuscleCount;string property=n<0?HandMotionRetargeting.CurveName(HumanTrait.MuscleName[i]):n<3?"RootT."+"xyz"[n]:"RootQ."+"xyzw"[n-3];
                AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),property),c);
            }
            HandMotionRetargeting.Apply(result,source.avatar);result.EnsureQuaternionContinuity();
            var settings=AnimationUtility.GetAnimationClipSettings(old);
            // Attack1 turns through a full circle. Bake its orientation into the body; the
            // player controller owns locomotion and applyRootMotion=false would discard yaw.
            settings.keepOriginalOrientation=true;settings.loopBlendOrientation=true;
            settings.keepOriginalPositionY=true;settings.loopBlendPositionY=true;
            AnimationUtility.SetAnimationClipSettings(result,settings);
            // Limiting wrists/fingers changes the humanoid centre of mass slightly. Preserve
            // height on the *played result*, after that final solve, rather than just on the
            // pre-encoding skeleton. RootT is normalized by the target's humanScale.
            var baked=AnimationClipPlayable.Create(graph,result);baked.SetApplyFootIK(false);
            var output=AnimationPlayableOutput.Create(graph,"ResultHeight",a);output.SetSourcePlayable(baked);
            for(int pass=0;pass<2;pass++){
                var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");
                var prior=AnimationUtility.GetEditorCurve(result,binding);var corrected=new AnimationCurve();
                float maximumHeightError=0;
                for(int f=0,frames=Mathf.CeilToInt(raw.length*240);f<=frames;f++){
                    float time=raw.length*f/frames;
                    play.SetTime(time);baked.SetTime(time);graph.Evaluate(0);
                    float height=referenceAnimator.GetBoneTransform(HumanBodyBones.Hips).position.y;
                    float delta=height-a.GetBoneTransform(HumanBodyBones.Hips).position.y;
                    maximumHeightError=Mathf.Max(maximumHeightError,Mathf.Abs(delta));
                    corrected.AddKey(new Keyframe(time,prior.Evaluate(time)+delta/a.humanScale));
                }
                for(int i=0;i<corrected.length;i++){AnimationUtility.SetKeyLeftTangentMode(corrected,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(corrected,i,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(result,binding,corrected);
                Debug.Log("[RemoteAttackMotionAssets] height pass "+pass+" error="+maximumHeightError);
                // Recreate the playable: Unity caches the clip data used by a live playable.
                graph.DestroyPlayable(baked);baked=AnimationClipPlayable.Create(graph,result);baked.SetApplyFootIK(false);output.SetSourcePlayable(baked);
            }
            // Keep the original GUID and resource name: all basic attacks and shared spells
            // immediately use the corrected motion without scene/config changes.
            EditorUtility.CopySerialized(result,old);EditorUtility.SetDirty(old);AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MotionPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("REMOTE_ATTACK_REBAKE_COMPLETE 1.2s, native Attack1, 240Hz, original height and GUID retained");
        }finally{graph.Destroy();Object.DestroyImmediate(target);Object.DestroyImmediate(reference);Object.DestroyImmediate(result);}
    }
    static void AlignChain(Transform su,Transform sl,Transform se,Transform tu,Transform tl,Transform te,Quaternion transfer)
    {
        var upper=transfer*(sl.position-su.position).normalized;var lower=transfer*(se.position-sl.position).normalized;
        var currentUpper=(tl.position-tu.position).normalized;var currentLower=(te.position-tl.position).normalized;
        var normal=Vector3.Cross(upper,lower);var currentNormal=Vector3.Cross(currentUpper,currentLower);
        if(normal.sqrMagnitude>.0001f&&currentNormal.sqrMagnitude>.0001f)
            tu.rotation=Quaternion.LookRotation(upper,normal)*Quaternion.Inverse(Quaternion.LookRotation(currentUpper,currentNormal))*tu.rotation;
        else tu.rotation=Quaternion.FromToRotation(currentUpper,upper)*tu.rotation;
        tl.rotation=Quaternion.FromToRotation(te.position-tl.position,lower)*tl.rotation;
    }
    static Quaternion TorsoFrame(Animator a)
    {
        var right=a.GetBoneTransform(HumanBodyBones.RightUpperArm).position-a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
        var up=(a.GetBoneTransform(HumanBodyBones.Neck)??a.GetBoneTransform(HumanBodyBones.Head)).position-a.GetBoneTransform(HumanBodyBones.Hips).position;
        return Quaternion.LookRotation(Vector3.Cross(right,up),up);
    }
    static Quaternion PalmFrame(Transform hand,Transform index,Transform thumb)=>Quaternion.LookRotation(index.position-hand.position,Vector3.Cross(index.position-hand.position,thumb.position-hand.position));
}
