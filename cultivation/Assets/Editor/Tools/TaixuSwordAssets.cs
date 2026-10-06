using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

/// <summary>Generic 锦衣卫完整动作重定向和神秘人剑的手骨标定；不修改源模型、不保存场景。</summary>
public static class TaixuSwordAssets
{
    const string Folder = "Assets/resources/技能动作/太虚剑决/";
    const string Jin = "Assets/resources/一些第三方资源/锦衣卫/";
    const string Mystery = "Assets/resources/NPC/NPC/ShenMiRen_Animation/ShenMiRen@";
    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

    [MenuItem("修仙/动画/重建太虚剑决（不动场景）")]
    public static void Build()
    {
        BuildMotions();
        BuildWeapon();
        安全导入配置表.ImportAssetsOnly("功法表", "物品表");
        AssetDatabase.SaveAssets();
        Debug.Log("[太虚剑决] 完整三式、FightRun 左臂修正、Idle、神秘人剑与秘籍生成完毕；未保存场景");
    }

    public static void BuildMotions()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before baking");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var converted = new List<AnimationClip>();
        try
        {
            var a1 = Retarget("Attack001"); converted.Add(a1);
            var a2 = Retarget("Attack003"); converted.Add(a2);

            SaveHumanoid(a1, "A1", false);
            SaveHumanoid(a2, "A2", false);
            SaveHumanoid(Clip(Mystery + "Attack1.FBX"), "A3", false);
            BuildAdvance();
            SaveHumanoid(Clip(Mystery + "Idle.FBX"), "持剑_Idle", true);
            BuildFlight();
            AssetDatabase.SaveAssets();
            Debug.Log("[太虚剑决] 已保存三式、待机与 FightRun 移动；修正移动双臂与身体居中、不保存场景");
        }
        finally { foreach(var clip in converted)UnityEngine.Object.DestroyImmediate(clip); }
    }

    /// <summary>FightRun 保留身体和步伐，双臂使用普通 Run 的自然下垂摆臂，右手腕保持中立。</summary>
    public static void BuildAdvance()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before baking");
        var result = Retarget("FightRun001");
        try
        {
            var armSource = Clip(Mystery + "Run.FBX");
            // Replace arm muscles to remove face covering and a collapsed sword wrist. Body and legs
            // remain FightRun; sample the donor at matching normalized cycle phase.
            foreach(string muscle in HumanTrait.MuscleName)
            {
                string armMuscle=muscle.StartsWith("Right ") ? muscle.Substring(6) : muscle.StartsWith("Left ") ? muscle.Substring(5) : "";
                if(!(armMuscle.StartsWith("Shoulder ") || armMuscle.StartsWith("Arm ") ||
                     armMuscle.StartsWith("Forearm ") || armMuscle.StartsWith("Hand ")))continue;
                var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),muscle);
                var sourceCurve=AnimationUtility.GetEditorCurve(armSource,binding);
                if(sourceCurve==null)throw new InvalidOperationException("Run 缺失手臂曲线："+muscle);
                var curve=new AnimationCurve();int frames=Mathf.CeilToInt(result.length*60);
                for(int f=0;f<=frames;f++)
                    curve.AddKey(new Keyframe(result.length*f/frames,sourceCurve.Evaluate(armSource.length*f/frames)));
                for(int k=0;k<curve.length;k++) {
                    AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);
                }
                AnimationUtility.SetEditorCurve(result,binding,curve);
            }
            // Keep the sword wrist aligned with the forearm rather than folded downward.
            foreach(string wrist in new[]{"Right Hand Down-Up","Right Hand In-Out"})
                AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),wrist),AnimationCurve.Constant(0,result.length,0f));
            RelaxAdvancePosture(result);
            var aligned=AlignAdvancePelvis(result);UnityEngine.Object.DestroyImmediate(result);result=aligned;
            var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=true;
            settings.loopBlendPositionY=true;settings.keepOriginalPositionY=true;
            settings.loopBlendOrientation=true;settings.keepOriginalOrientation=true;
            AnimationUtility.SetAnimationClipSettings(result,settings);SetGrip(result,result.length);
            result.name="持剑_前进";Save(result,"持剑_前进");AssetDatabase.SaveAssets();
            Debug.Log("[太虚剑决] 移动改回锦衣卫 FightRun，修正左手挡脸、右臂握剑与身体侧倾；未修改待机和攻击");
        }
        finally { if(result!=null)UnityEngine.Object.DestroyImmediate(result); }
    }

    public static void BuildFlight()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before baking");
        foreach(var name in new[]{"持剑_Idle","持剑_前进","A1","A2","A3"}) {
            var upper=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+(name.StartsWith("持剑_")?"持剑_Idle":name)+".anim");
            if(upper==null)throw new InvalidOperationException("缺失上身动作："+name);
            Save(BakeFlight(upper,name+"_御风",name.StartsWith("持剑_")),name+"_御风");
        }
        AssetDatabase.SaveAssets();Debug.Log("[太虚剑决] 已烘焙御风待机、前进与三式攻击：持剑上身＋神秘人 Walk 漂浮下身");
    }

    static AnimationClip BakeFlight(AnimationClip upper,string name,bool loop)
    {
        var original=GameObject.Find("Player").GetComponentInChildren<Animator>(true);
        var top=UnityEngine.Object.Instantiate(original.gameObject);
        var bottom=UnityEngine.Object.Instantiate(original.gameObject);
        var graph=PlayableGraph.Create("TaixuFlightBake");
        try {
            Animator Prepare(GameObject rig) {
                foreach(var script in rig.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                rig.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var a=rig.GetComponent<Animator>();a.runtimeAnimatorController=null;
                a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;return a;
            }
            var a=Prepare(top);var b=Prepare(bottom);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            AnimationClipPlayable Connect(Animator animator,AnimationClip clip) {
                var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);
                var output=AnimationPlayableOutput.Create(graph,animator.name,animator);output.SetSourcePlayable(p);return p;
            }
            var lower=Clip(Mystery+"Walk.FBX");var upperPlayable=Connect(a,upper);var lowerPlayable=Connect(b,lower);graph.Play();
            var legBones=new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,
                HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
            var curves=Curves();var pose=new HumanPose();Quaternion previous=Quaternion.identity;
            int frames=Mathf.CeilToInt(upper.length*60);
            using(var handler=new HumanPoseHandler(a.avatar,top.transform))
            for(int frame=0;frame<=frames;frame++) {
                float time=upper.length*frame/frames;
                upperPlayable.SetTime(time);lowerPlayable.SetTime(lower.length*frame/frames);graph.Evaluate(0);
                var hips=a.GetBoneTransform(HumanBodyBones.Hips);var donorHips=b.GetBoneTransform(HumanBodyBones.Hips);
                var spine=a.GetBoneTransform(HumanBodyBones.Spine);var upperRotation=spine.rotation;
                if(name.StartsWith("持剑_")) {
                    var torso=a.GetBoneTransform(HumanBodyBones.Head).position-hips.position;
                    upperRotation=Quaternion.FromToRotation(torso.normalized,Vector3.up)*upperRotation;
                }
                var hipLine=b.GetBoneTransform(HumanBodyBones.RightUpperLeg).position-b.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position;
                donorHips.rotation=Quaternion.FromToRotation(hipLine.normalized,Vector3.right)*donorHips.rotation;
                hips.rotation=donorHips.rotation;
                var position=donorHips.position;position.x=0;hips.position=position;
                foreach(var bone in legBones) {
                    var destination=a.GetBoneTransform(bone);var donor=b.GetBoneTransform(bone);
                    if(destination!=null&&donor!=null)destination.localRotation=donor.localRotation;
                }
                // Restore the actual upper-body orientation; donor root lean must not pull it backward.
                spine.rotation=upperRotation;
                handler.GetHumanPose(ref pose);Add(curves,time,pose,ref previous);
            }
            return Write(curves,name,upper.length,loop);
        }
        finally {if(graph.IsValid())graph.Destroy();UnityEngine.Object.DestroyImmediate(top);UnityEngine.Object.DestroyImmediate(bottom);}
    }

    static AnimationClip AlignAdvancePelvis(AnimationClip source)
    {
        var original=GameObject.Find("Player").GetComponentInChildren<Animator>(true);
        var rig=UnityEngine.Object.Instantiate(original.gameObject);
        var graph=PlayableGraph.Create("TaixuPelvisBake");
        try
        {
            foreach(var script in rig.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            rig.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var animator=rig.GetComponent<Animator>();animator.runtimeAnimatorController=null;
            animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(graph,source);playable.SetApplyFootIK(false);
            var output=AnimationPlayableOutput.Create(graph,"Pose",animator);output.SetSourcePlayable(playable);graph.Play();
            var curves=Curves();var pose=new HumanPose();Quaternion previous=Quaternion.identity;
            int frames=Mathf.CeilToInt(source.length*60);
            using(var handler=new HumanPoseHandler(animator.avatar,rig.transform))
            for(int frame=0;frame<=frames;frame++)
            {
                float time=source.length*frame/frames;
                playable.SetTime(time);graph.Evaluate(0);
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
                var left=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var right=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                var upperRotation=spine.rotation;
                // Correct the actual hip joint line, not the humanoid centre-of-mass RootQ.
                // Preserve the upper-body pose while straightening the pelvis and leg chain.
                hips.rotation=Quaternion.FromToRotation((right.position-left.position).normalized,Vector3.right)*hips.rotation;
                var position=hips.position;position.x=0f;hips.position=position;
                spine.rotation=upperRotation;
                handler.GetHumanPose(ref pose);Add(curves,time,pose,ref previous);
            }
            return Write(curves,"持剑_前进",source.length,true);
        }
        finally {if(graph.IsValid())graph.Destroy();UnityEngine.Object.DestroyImmediate(rig);}
    }

    static void RelaxAdvancePosture(AnimationClip clip)
    {
        // Preserve FightRun forward lean; remove lateral tilt and leave facing to the controller.
        var root=new AnimationCurve[4];var adjusted=new AnimationCurve[4];
        for(int i=0;i<4;i++) {
            root[i]=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"RootQ."+"xyzw"[i]));
            adjusted[i]=new AnimationCurve();
        }
        int frames=Mathf.CeilToInt(clip.length*60);Quaternion previous=Quaternion.identity;
        for(int f=0;f<=frames;f++) {
            float time=clip.length*f/frames;
            var q=new Quaternion(root[0].Evaluate(time),root[1].Evaluate(time),root[2].Evaluate(time),root[3].Evaluate(time)).normalized;
            var forward=Vector3.ProjectOnPlane(q*Vector3.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.001f)forward=Vector3.forward;
            var up=q*Vector3.up;
            float pitch=Mathf.Atan2(Vector3.Dot(up,forward),Vector3.Dot(up,Vector3.up))*Mathf.Rad2Deg;
            // Use a forward, upright basis: source yaw and lateral roll must not skew locomotion.
            q=Quaternion.Euler(pitch,0,0);
            if(f>0&&Quaternion.Dot(previous,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
            previous=q;for(int i=0;i<4;i++)adjusted[i].AddKey(new Keyframe(time,q[i]));
        }
        for(int i=0;i<4;i++) {
            for(int k=0;k<adjusted[i].length;k++) {
                AnimationUtility.SetKeyLeftTangentMode(adjusted[i],k,AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(adjusted[i],k,AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"RootQ."+"xyzw"[i]),adjusted[i]);
        }
        foreach(string muscle in new[]{"Spine Left-Right","Chest Left-Right","UpperChest Left-Right","Neck Tilt Left-Right","Head Tilt Left-Right"}) {
            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),muscle);
            var source=AnimationUtility.GetEditorCurve(clip,binding);if(source==null)continue;
            var keys=source.keys;
            for(int i=0;i<keys.Length;i++) {
                keys[i].value=0f;keys[i].inTangent=0f;keys[i].outTangent=0f;
            }
            AnimationUtility.SetEditorCurve(clip,binding,new AnimationCurve(keys));
        }
        // Keep FightRun sagittal stride and knee bends, remove lateral crossing/twist.
        foreach(string side in new[]{"Left","Right"})
            foreach(string axis in new[]{"Upper Leg In-Out","Upper Leg Twist In-Out","Lower Leg Twist In-Out","Foot Twist In-Out"})
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),side+" "+axis),AnimationCurve.Constant(0,clip.length,0f));
        // Prevent the source body-centre translation from shifting the pelvis sideways.
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.x"),AnimationCurve.Constant(0,clip.length,0f));
        clip.EnsureQuaternionContinuity();
    }

    static AnimationClip Retarget(string motion)
    {
        var rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Jin + "K_JinYiWei001.fbx"));
        Avatar avatar = null;
        try
        {
            rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var bones = rig.GetComponentsInChildren<Transform>(true);
            var mapping = new Dictionary<string,string> {
                {"Hips","Bip01-Pelvis"},{"Spine","Bip01-Spine"},{"Chest","Bip01-Spine1"},{"UpperChest","Bip01-Spine2"},
                {"Neck","Bip01-Neck"},{"Head","Bip01-Head"}
            };
            foreach (string side in new[]{"Left","Right"})
            {
                string prefix = "Bip01-" + (side == "Left" ? "L" : "R") + "-";
                foreach (var pair in new[]{("Shoulder","Clavicle"),("UpperArm","UpperArm"),("LowerArm","Forearm"),("Hand","Hand"),
                    ("UpperLeg","Thigh"),("LowerLeg","Calf"),("Foot","Foot"),("Toes","Toe0")}) mapping[side+pair.Item1]=prefix+pair.Item2;
                // 源手只有三条两节手指；不强行映射缺失的末节和无名/小指。
                foreach (var finger in new[]{("Thumb",0),("Index",1),("Middle",2)})
                { mapping[side+finger.Item1+"Proximal"]=prefix+"Finger"+finger.Item2; mapping[side+finger.Item1+"Intermediate"]=prefix+"Finger"+finger.Item2+"1"; }
            }
            var desc = new HumanDescription {
                human = mapping.Where(p=>bones.Any(t=>t.name==p.Value)).Select(p=>new HumanBone {humanName=p.Key,boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),
                skeleton = bones.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),
                upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f
            };
            avatar=AvatarBuilder.BuildHumanAvatar(rig,desc);
            if (!avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("锦衣卫人形骨骼映射失败");
            var animator=rig.GetComponent<Animator>(); if(animator==null)animator=rig.AddComponent<Animator>();
            animator.runtimeAnimatorController=null; animator.avatar=avatar;
            var positions=bones.Select(t=>t.localPosition).ToArray(); var rotations=bones.Select(t=>t.localRotation).ToArray();
            var raw=Clip(Jin+"A_JinYiWei001@"+motion+".fbx");
            var lookup=bones.Select((t,i)=>new {path=AnimationUtility.CalculateTransformPath(t,rig.transform),i}).ToDictionary(x=>x.path,x=>x.i);
            var bindings=AnimationUtility.GetCurveBindings(raw).Where(b=>b.type==typeof(Transform)&&lookup.ContainsKey(b.path)).ToArray();
            var nativeCurves=bindings.Select(b=>AnimationUtility.GetEditorCurve(raw,b)).ToArray();
            int frames=Mathf.CeilToInt(raw.length*60); var curves=Curves(); var pose=new HumanPose(); Quaternion previous=Quaternion.identity;
            using(var handler=new HumanPoseHandler(avatar,rig.transform))
            for(int f=0;f<=frames;f++)
            {
                for(int b=0;b<bones.Length;b++){bones[b].localPosition=positions[b];bones[b].localRotation=rotations[b];}
                float time=raw.length*f/frames;
                // A Humanoid Animator ignores Generic SampleAnimation. Evaluate native bone curves explicitly.
                var ps=(Vector3[])positions.Clone();var qs=(Quaternion[])rotations.Clone();
                for(int j=0;j<bindings.Length;j++){
                    var binding=bindings[j];int index=lookup[binding.path];int axis="xyzw".IndexOf(binding.propertyName[binding.propertyName.Length-1]);
                    if(axis<0)continue;float value=nativeCurves[j].Evaluate(time);
                    if(binding.propertyName.StartsWith("m_LocalPosition."))ps[index][axis]=value;
                    else if(binding.propertyName.StartsWith("m_LocalRotation."))qs[index][axis]=value;
                }
                for(int j=0;j<bones.Length;j++){bones[j].localPosition=ps[j];bones[j].localRotation=qs[j].normalized;}
                handler.GetHumanPose(ref pose);
                Add(curves,time,pose,ref previous);
            }
            var result=Write(curves,motion,raw.length,false);
            Debug.Log("[太虚剑决] Generic → Humanoid "+motion+" "+raw.length.ToString("F3")+"s avatarScale="+animator.humanScale);
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(rig); if(avatar!=null)UnityEngine.Object.DestroyImmediate(avatar); }
    }
    static AnimationCurve[] Curves() => Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
    static void Add(AnimationCurve[] curves,float time,HumanPose pose,ref Quaternion previous)
    {
        var q=pose.bodyRotation; if(Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w); previous=q;
        for(int i=0;i<curves.Length;i++){int k=i-HumanTrait.MuscleCount;float v=k<0?pose.muscles[i]:k<3?pose.bodyPosition[k]:q[k-3];
            if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Nonfinite pose");curves[i].AddKey(new Keyframe(time,v));}
    }
    static AnimationClip Write(AnimationCurve[] curves,string name,float duration,bool loop)
    {
        var clip=new AnimationClip{name=name,frameRate=60};
        for(int i=0;i<curves.Length;i++){
            var c=curves[i];if(c.keys.All(k=>Mathf.Abs(k.value-c.keys[0].value)<.00001f))c=AnimationCurve.Linear(0,c.keys[0].value,duration,c.keys[0].value);
            for(int j=0;j<c.length;j++){AnimationUtility.SetKeyLeftTangentMode(c,j,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,j,AnimationUtility.TangentMode.Linear);}
            int k=i-HumanTrait.MuscleCount;string prop=k<0?HumanTrait.MuscleName[i]:k<3?"RootT."+"xyz"[k]:"RootQ."+"xyzw"[k-3];
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),prop),c);
        }
        SetGrip(clip,duration);
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=loop;
        settings.loopBlendPositionY=true;settings.keepOriginalPositionY=true;settings.loopBlendOrientation=true;settings.keepOriginalOrientation=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);return clip;
    }
    static void SaveHumanoid(AnimationClip source,string name,bool loop)
    {
        var result=new AnimationClip{name=name,frameRate=source.frameRate};
        foreach(var b in AnimationUtility.GetCurveBindings(source))if(b.type==typeof(Animator))AnimationUtility.SetEditorCurve(result,b,AnimationUtility.GetEditorCurve(source,b));
        var settings=AnimationUtility.GetAnimationClipSettings(source);settings.loopTime=loop; settings.loopBlendPositionY=true;settings.keepOriginalPositionY=true;
        settings.loopBlendOrientation=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(result,settings);SetGrip(result,source.length);Save(result,name);
    }
    static void SetGrip(AnimationClip clip,float duration)
    {
        // 自动绑定的缺失手指不应被重定向成张手；握剑时给右手稳定的自然握柄姿态。
        foreach(string muscle in HumanTrait.MuscleName)
            if(muscle.StartsWith("Right ") && (muscle.Contains("Stretched") || muscle.Contains("Spread")))
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),muscle),
                    AnimationCurve.Constant(0,duration,muscle.Contains("Spread") ? 0f : muscle.Contains("Thumb") ? -.25f : -.65f));
    }
    static void Save(AnimationClip clip,string name)
    {
        string path=Folder+name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing!=null){EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,path);
    }
    static void BuildWeapon()
    {
        var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/NPC/NPC/ShenMiRen/ShenMiRen.FBX"));
        var targetSource=GameObject.Find("Player").GetComponentInChildren<Animator>(true);
        var target=UnityEngine.Object.Instantiate(targetSource.gameObject); var holder=new GameObject("太虚剑决_神秘人剑");
        Mesh mesh=new Mesh{name="太虚剑决_神秘人剑"};
        try
        {
            var a=source.GetComponent<Animator>();var b=target.GetComponent<Animator>();a.runtimeAnimatorController=null;b.runtimeAnimatorController=null;
            source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var pose=new HumanPose{bodyPosition=Vector3.up,bodyRotation=Quaternion.identity,muscles=new float[HumanTrait.MuscleCount]};
            using(var hp=new HumanPoseHandler(a.avatar,a.transform))hp.SetHumanPose(ref pose);
            using(var hp=new HumanPoseHandler(b.avatar,b.transform))hp.SetHumanPose(ref pose);
            var hand=a.GetBoneTransform(HumanBodyBones.RightHand);var targetHand=b.GetBoneTransform(HumanBodyBones.RightHand);
            var smr=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="ShenMiRen_03");smr.BakeMesh(mesh);
            var rotation=Quaternion.Inverse(targetHand.rotation)*hand.rotation;
            float scale=b.humanScale/a.humanScale;
            var verts=mesh.vertices;
            for(int i=0;i<verts.Length;i++)verts[i]=rotation*(hand.InverseTransformPoint(smr.transform.TransformPoint(verts[i]))*scale);
            mesh.vertices=verts;var weights=new BoneWeight[verts.Length];for(int i=0;i<weights.Length;i++)weights[i]=new BoneWeight{boneIndex0=0,weight0=1};
            mesh.boneWeights=weights;mesh.bindposes=new[]{Matrix4x4.identity};mesh.RecalculateBounds();mesh.RecalculateNormals();
            const string meshPath="Assets/resources/Weapons/太虚剑决_神秘人剑.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(mesh,meshPath);
            var node=new GameObject("ShenMiRen_03");node.transform.SetParent(holder.transform,false);var render=node.AddComponent<SkinnedMeshRenderer>();
            render.sharedMesh=mesh;render.sharedMaterials=smr.sharedMaterials;render.bones=new[]{node.transform};render.rootBone=node.transform;render.updateWhenOffscreen=true;
            PrefabUtility.SaveAsPrefabAsset(holder,"Assets/resources/Weapons/太虚剑决_神秘人剑.prefab");
            Debug.Log("[太虚剑决] 神秘人03剑握持标定 scale="+scale+" neutral rotation="+rotation.eulerAngles+" length="+mesh.bounds.size);
        }
        finally {UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(holder);}
    }
    [MenuItem("修仙/调试/给予太虚剑决秘籍（仅当前 Play 背包）")]
    public static void GiveManual()
    {
        if(!EditorApplication.isPlaying)return;var panel=UnityEngine.Object.FindObjectOfType<UIPanelData>();
        var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_gongfa_taixu_jianjue.asset");
        if(panel!=null&&item!=null&&panel.物品数量(item)==0)panel.给物品(item,1);
    }
}
