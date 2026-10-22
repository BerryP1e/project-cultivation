using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>胸部相对持戟重定向，只离线调整右肩臂腕和非攻击头颈；完整编码HumanPose以保留其余骨骼实际姿态。</summary>
public static class JiubaCarryPoseBaker
{
    const string Target="Assets/Art/Characters/Tripo/better_player_test/better_player_test.fbx";
    const string Yang="Assets/resources/NPC/Human/YangJian/YangJian_03.prefab";
    const string Idle="Assets/resources/NPC/Human/YangJian_Animation/YangJian@Idle.FBX";
    const string Library="Assets/resources/Animation Library/1/UAL1_Standard.fbx";
    const string Weapon="Assets/resources/Weapons/八九玄功_三尖两刃刀.prefab";
    const string Knight="Assets/resources/Animation Library/Magical-Knight_Set/Animation/Humanoid/move_run_01.fbx";
    const string KnightFk="Assets/resources/Animation Library/Magical-Knight_Set/Animation/Generic/move_run_01.fbx";
    static AnimationClip Load(string path,string name=null)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview")&&(name==null||c.name==name));
    sealed class Rig:IDisposable
    {
        public readonly GameObject root;public readonly Animator animator;
        readonly PlayableGraph graph;readonly AnimationPlayableOutput output;AnimationClip current;AnimationClipPlayable playable;
        AnimationClip rawClip; (Transform target,string property,AnimationCurve curve)[] rawCurves;
        readonly Transform[] transforms;readonly Vector3[] positions,scales;readonly Quaternion[] rotations;bool rawActive;
        public Rig(string path,Scene scene)
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));SceneManager.MoveGameObjectToScene(root,scene);root.hideFlags=HideFlags.HideAndDontSave;
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
            foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            animator=root.GetComponentInChildren<Animator>(true);
            if(!animator){animator=root.AddComponent<Animator>();animator.avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault()??(AssetImporter.GetAtPath(path) as ModelImporter)?.sourceAvatar;}
            if(!animator.avatar||!animator.avatar.isHuman)throw new InvalidOperationException("Humanoid reference avatar missing: "+path);
            animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            transforms=root.GetComponentsInChildren<Transform>(true);positions=transforms.Select(t=>t.localPosition).ToArray();rotations=transforms.Select(t=>t.localRotation).ToArray();scales=transforms.Select(t=>t.localScale).ToArray();
            graph=PlayableGraph.Create("Jiuba chest-relative bake");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);output=AnimationPlayableOutput.Create(graph,"Pose",animator);graph.Play();
        }
        public Transform Bone(HumanBodyBones bone)=>animator.GetBoneTransform(bone);
        public void Sample(AnimationClip clip,float time)
        {
            if(!clip.humanMotion){
                graph.Stop();rawActive=true;
                if(rawClip!=clip){rawClip=clip;rawCurves=AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Transform)).Select(b=>(root.transform.Find(b.path),b.propertyName,AnimationUtility.GetEditorCurve(clip,b))).ToArray();if(rawCurves.Any(c=>!c.Item1))throw new InvalidOperationException("Original FK source hierarchy mismatch");}
                var rotations=new System.Collections.Generic.Dictionary<Transform,Quaternion>();
                foreach(var entry in rawCurves){float v=entry.curve.Evaluate(time);int axis="xyzw".IndexOf(entry.property[entry.property.Length-1]);var t=entry.target;if(entry.property.StartsWith("m_LocalPosition.")){var p=t.localPosition;p[axis]=v;t.localPosition=p;}else if(entry.property.StartsWith("m_LocalRotation.")){if(!rotations.TryGetValue(t,out var q))q=t.localRotation;q[axis]=v;rotations[t]=q;}else if(entry.property.StartsWith("m_LocalScale.")){var s=t.localScale;s[axis]=v;t.localScale=s;}}
                foreach(var pair in rotations)pair.Key.localRotation=pair.Value.normalized;
                return;
            }
            if(rawActive){for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];transforms[i].localScale=scales[i];}rawActive=false;current=null;}
            graph.Play();
            if(current!=clip){if(playable.IsValid())graph.DestroyPlayable(playable);playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);output.SetSourcePlayable(playable);current=clip;}
            playable.SetTime(Mathf.Clamp(time,0,Mathf.Max(0,clip.length-.00001f)));graph.Evaluate(0);
        }
        public void Dispose(){if(graph.IsValid())graph.Destroy();Object.DestroyImmediate(root);}
    }
    // 两套Avatar的Chest骨坐标轴不同；人体关节基准才具有相同含义。
    static Quaternion Torso(Rig rig)
    {
        var right=(rig.Bone(HumanBodyBones.RightUpperArm).position-rig.Bone(HumanBodyBones.LeftUpperArm).position).normalized;
        var up=rig.Bone(HumanBodyBones.Chest).position-rig.Bone(HumanBodyBones.Hips).position;up=(up-right*Vector3.Dot(up,right)).normalized;
        return Quaternion.LookRotation(Vector3.Cross(right,up),up);
    }
    static Vector3 Grip(Rig rig,out Vector3 axis)
    {
        var hand=rig.Bone(HumanBodyBones.RightHand);var centres=new Vector3[4];
        var fingers=new[]{HumanBodyBones.RightIndexProximal,HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightRingProximal,HumanBodyBones.RightLittleProximal};
        for(int i=0;i<4;i++){
            var a=hand.InverseTransformPoint(rig.Bone(fingers[i]).position);var b=hand.InverseTransformPoint(rig.Bone((HumanBodyBones)((int)fingers[i]+1)).position);var c=hand.InverseTransformPoint(rig.Bone((HumanBodyBones)((int)fingers[i]+2)).position);
            var ab=b-a;var ac=c-a;var n=Vector3.Cross(ab,ac);if(n.sqrMagnitude<1e-16f)throw new InvalidOperationException("Degenerate authored grip");
            centres[i]=a+(Vector3.Cross(n,ab)*ac.sqrMagnitude+Vector3.Cross(ac,n)*ab.sqrMagnitude)/(2*n.sqrMagnitude);
        }
        axis=hand.TransformDirection((centres[0]-centres[3]).normalized);return hand.TransformPoint(centres.Aggregate(Vector3.zero,(s,v)=>s+v)/4);
    }
    static Vector3 Axis(SkinnedMeshRenderer renderer,out Vector3 mean)
    {
        var vertices=WeaponVertices(renderer);
        mean=vertices.Aggregate(Vector3.zero,(s,v)=>s+v)/vertices.Length;var centre=mean;var axis=new Vector3(.3f,.7f,.6f).normalized;
        for(int k=0;k<40;k++){var next=Vector3.zero;foreach(var v in vertices){var d=v-centre;next+=d*Vector3.Dot(d,axis);}axis=next.normalized;}return axis;
    }
    // 该武器是100%单骨骼蒙皮。直接取GPU所用的世界蒙皮矩阵，避免BakeMesh后
    // 再TransformPoint把挂载缩放重复乘入：旧标定因此把刀柄放到了拳头外面。
    static Vector3[] WeaponVertices(SkinnedMeshRenderer renderer)
    {
        var mesh=renderer.sharedMesh;
        if(renderer.bones.Length!=1||mesh.bindposes.Length!=1||mesh.boneWeights.Any(w=>w.boneIndex0!=0||Mathf.Abs(w.weight0-1)>1e-5f))throw new InvalidOperationException("Rigid weapon skin changed; recalibrate its mesh evaluation");
        var matrix=renderer.bones[0].localToWorldMatrix*mesh.bindposes[0];
        return mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();
    }
    // +Z 是三尖刀刃，-Z 是柄尾；不能用无符号主轴再任意翻转枪尖。
    static Vector3 SignedShaft(SkinnedMeshRenderer renderer)
    {
        var rest=renderer.sharedMesh.vertices;int tip=Enumerable.Range(0,rest.Length).OrderByDescending(i=>rest[i].z).First();int tail=Enumerable.Range(0,rest.Length).OrderBy(i=>rest[i].z).First();
        var vertices=WeaponVertices(renderer);return (vertices[tip]-vertices[tail]).normalized;
    }
    static Vector3 PalmNormal(Rig rig)
    {
        var wrist=rig.Bone(HumanBodyBones.RightHand).position;
        return Vector3.Cross(rig.Bone(HumanBodyBones.RightIndexProximal).position-wrist,rig.Bone(HumanBodyBones.RightThumbProximal).position-wrist).normalized;
    }
    // 通过同一网格的三点定义几何朝向，不假设武器骨坐标等于网格坐标。
    static Quaternion MeshFrame(SkinnedMeshRenderer renderer)
    {
        var v=WeaponVertices(renderer);var t=renderer.sharedMesh.triangles;
        for(int i=0;i<t.Length;i+=3){var a=v[t[i]];var x=v[t[i+1]]-a;var y=v[t[i+2]]-a;var n=Vector3.Cross(x,y);if(n.sqrMagnitude>1e-14f)return Quaternion.LookRotation(x,n);}
        throw new InvalidOperationException("Weapon has no nondegenerate triangle");
    }
    static void Segment(Rig target,Rig source,HumanBodyBones from,HumanBodyBones to,Quaternion map)
    {
        var bone=target.Bone(from);var next=target.Bone(to);
        bone.rotation=Quaternion.FromToRotation(next.position-bone.position,map*(source.Bone(to).position-source.Bone(from).position))*bone.rotation;
    }
    sealed class Context:IDisposable
    {
        readonly Scene scene;public readonly Rig target,source;readonly Rig groundSource;readonly GameObject weapon;
        readonly Quaternion groundFrame,groundHeadBind;readonly AnimationClip groundClip;
        public readonly Transform correction;readonly Quaternion handToWeapon,sourceMeshOffset,gripFrameInHand,restHead,restNeck;readonly AnimationClip idle;
        readonly SkinnedMeshRenderer sourceWeapon;
        public Context(bool calibrate,bool moving=false)
        {
            scene=EditorSceneManager.NewPreviewScene();
            try{
                target=new Rig(Target,scene);source=new Rig(Yang,scene);
                // FBX绑定姿态自身并不朝Unity +Z；先消除绑定躯干朝向，才是面朝前方的头。
                var bindFrame=Quaternion.Inverse(Torso(target));
                // 玩家面部建模自带俯角，日常视线轻抬，避免下巴持续压向胸口。
                restHead=Quaternion.AngleAxis(-12,Vector3.right)*bindFrame*target.Bone(HumanBodyBones.Head).rotation;var neck=target.Bone(HumanBodyBones.Neck);restNeck=neck?bindFrame*neck.rotation:Quaternion.identity;
                if(moving){
                    string reference=AssetDatabase.GetAssetPath(((ModelImporter)AssetImporter.GetAtPath(Knight)).sourceAvatar);
                    groundSource=new Rig(reference,scene);groundFrame=Quaternion.Inverse(Torso(groundSource));
                    groundClip=Load(KnightFk);
                    // 原FK参考的连续摆动围绕平均视线传递，去掉重定向后附带的静态俯角。
                    // 这是一次离线标定；每帧仍保留run01原有的点头、偏转和侧摆。
                    var sum=Vector4.zero;Quaternion first=Quaternion.identity;
                    for(int f=0;f<64;f++){
                        groundSource.Sample(groundClip,groundClip.length*f/64);
                        var q=groundFrame*groundSource.Bone(HumanBodyBones.Head).rotation;if(f==0)first=q;
                        float sign=Quaternion.Dot(first,q)<0?-1:1;sum+=new Vector4(q.x,q.y,q.z,q.w)*sign;
                    }
                    groundHeadBind=new Quaternion(sum.x,sum.y,sum.z,sum.w).normalized;
                }
                idle=Load(Idle);source.Sample(idle,0);
                sourceWeapon=source.root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.name.Contains("Weapon"));sourceWeapon.updateWhenOffscreen=true;
                var nativeWeaponFrame=MeshFrame(sourceWeapon);sourceMeshOffset=Quaternion.Inverse(sourceWeapon.bones[0].rotation)*nativeWeaponFrame;
                var nativeIdle=Load(Library,"Armature|Idle_Loop");
                var gripIdle=Object.Instantiate(nativeIdle);try{JiubaHandPoseBaker.Apply(gripIdle);target.Sample(gripIdle,gripIdle.length*.5f);}finally{Object.DestroyImmediate(gripIdle);}
                var hand=target.Bone(HumanBodyBones.RightHand);weapon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Weapon),hand);
                weapon.transform.localPosition=Vector3.zero;weapon.transform.localRotation=Quaternion.Euler(2.328f,254.529f,263.733f);weapon.transform.localScale=Vector3.one*.3173f;
                correction=weapon.transform.Find("GripCorrection");if(!correction)throw new InvalidOperationException("Missing grip correction");
                Grip(target,out var gripAxis);gripFrameInHand=Quaternion.Inverse(hand.rotation)*Quaternion.LookRotation(gripAxis,PalmNormal(target));
                var map=Torso(target)*Quaternion.Inverse(Torso(source));ArmPose(map);
                HandPose(map*Quaternion.LookRotation(SignedShaft(sourceWeapon),PalmNormal(source))*Quaternion.Inverse(gripFrameInHand));
                var renderer=weapon.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if(renderer.sharedMesh!=sourceWeapon.sharedMesh)throw new InvalidOperationException("Source and equipped weapon meshes must match exactly; inactive variants are not references");
                if(calibrate){
                    var axis=Axis(renderer,out var mean);var gp=Grip(target,out _);var anchor=mean+axis*Vector3.Dot(gp-mean,axis);var local=correction.InverseTransformPoint(anchor);
                    correction.rotation=map*nativeWeaponFrame*Quaternion.Inverse(MeshFrame(renderer))*correction.rotation;correction.position+=gp-correction.TransformPoint(local);
                }
                handToWeapon=Quaternion.Inverse(hand.rotation)*MeshFrame(renderer);
            }catch{Dispose();throw;}
        }
        public void Carry(float phase,bool preserveHead)
        {
            source.Sample(idle,Mathf.Clamp01(phase)*idle.length);var map=Torso(target)*Quaternion.Inverse(Torso(source));ArmPose(map);
            HandPose(map*sourceWeapon.bones[0].rotation*sourceMeshOffset*Quaternion.Inverse(handToWeapon));
            // 面向角色前进参考方向，不能让胸部前倾或某帧偏头成为固定视线。
            if(preserveHead){
                // 从Knight原骨架提取相对绑定头部的运动，再映射到玩家面部基准。
                // 直接沿用Humanoid目标头骨会叠加两套模型的静态俯角；固定世界头则会抹掉全部随动。
                groundSource.Sample(groundClip,phase*groundClip.length);
                var delta=groundFrame*groundSource.Bone(HumanBodyBones.Head).rotation*Quaternion.Inverse(groundHeadBind);
                target.Bone(HumanBodyBones.Head).rotation=delta*restHead;
            }else{
                var neck=target.Bone(HumanBodyBones.Neck);if(neck)neck.rotation=Quaternion.Slerp(neck.rotation,restNeck,.65f);
                target.Bone(HumanBodyBones.Head).rotation=restHead;
            }
        }
        void ArmPose(Quaternion map)
        {
            Segment(target,source,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,map);
            Segment(target,source,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,map);
        }
        void HandPose(Quaternion desired)
        {
            var hand=target.Bone(HumanBodyBones.RightHand);var lower=target.Bone(HumanBodyBones.RightLowerArm);var axis=(hand.position-lower.position).normalized;
            var from=Vector3.ProjectOnPlane(hand.rotation*gripFrameInHand*Vector3.up,axis);var to=Vector3.ProjectOnPlane(desired*gripFrameInHand*Vector3.up,axis);
            if(from.sqrMagnitude>1e-8f&&to.sqrMagnitude>1e-8f)lower.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(from,to,axis),axis)*lower.rotation;
            hand.rotation=desired;
        }
        public void Attack(AnimationClip clip,float time)
        {
            source.Sample(clip,time);var map=Torso(target)*Quaternion.Inverse(Torso(source));var upper=target.Bone(HumanBodyBones.RightUpperArm);var lower=target.Bone(HumanBodyBones.RightLowerArm);var hand=target.Bone(HumanBodyBones.RightHand);
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,map*(source.Bone(HumanBodyBones.RightLowerArm).position-source.Bone(HumanBodyBones.RightUpperArm).position).normalized)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,map*(source.Bone(HumanBodyBones.RightHand).position-source.Bone(HumanBodyBones.RightLowerArm).position).normalized)*lower.rotation;
            HandPose(map*sourceWeapon.bones[0].rotation*sourceMeshOffset*Quaternion.Inverse(handToWeapon));
            if(clip.name.EndsWith("Attack1",StringComparison.Ordinal)){
                // 从肩部抬起整条持械臂，保持肘、腕和握柄关系；不改身体和腿。
                // 起手与收招平滑回到源轨迹，只在刺击段消除向下俯角。
                float phase=time/Mathf.Max(.001f,clip.length);
                float weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.30f,phase))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.82f,phase)));
                var direction=SignedShaft(weapon.GetComponentInChildren<SkinnedMeshRenderer>(true));
                var forward=Vector3.ProjectOnPlane(direction,Vector3.up);
                if(direction.y<0&&forward.sqrMagnitude>.0001f)
                    upper.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(direction,forward.normalized),weight)*upper.rotation;
            }
        }
        public void Dispose(){groundSource?.Dispose();source?.Dispose();target?.Dispose();if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static void 校准武器()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        using(var context=new Context(true)){
            var prefab=PrefabUtility.LoadPrefabContents(Weapon);
            try{var correction=prefab.transform.Find("GripCorrection");correction.localPosition=context.correction.localPosition;correction.localRotation=context.correction.localRotation;PrefabUtility.SaveAsPrefabAsset(prefab,Weapon);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
    }
    public static void Apply(AnimationClip clip,AnimationClip attackSource=null)
    {
        bool moving=clip.name=="持刀_走"||clip.name=="持刀_跑"||clip.name.StartsWith("地面八向_");
        using(var context=new Context(false,moving))using(var handler=new HumanPoseHandler(context.target.animator.avatar,context.target.animator.transform)){
            var original=Object.Instantiate(clip);
            try{
                bool attack=attackSource!=null;
                // HumanPose的bodyRotation会随姿态重新分解，必须同时编码对应的躯干补偿；
                // 只写右臂肌肉或仅额外写RootQ会让原本没改的头、身体也转动。
                var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();int frames=Mathf.Max(1,Mathf.CeilToInt(clip.length*60));var pose=new HumanPose();Quaternion previous=Quaternion.identity;
                for(int f=0;f<=frames;f++){
                    float t=clip.length*f/frames;context.target.Sample(original,t);if(attack)context.Attack(attackSource,Mathf.Min(t,attackSource.length));else context.Carry(t/clip.length,moving);handler.GetHumanPose(ref pose);
                    var q=pose.bodyRotation;if(f>0&&Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                    for(int k=0;k<curves.Length;k++){int root=k-HumanTrait.MuscleCount;float v=root<0?pose.muscles[k]:root<3?pose.bodyPosition[root]:q[root-3];if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidOperationException("Invalid carry muscle");curves[k].AddKey(t,v);}
                }
                for(int k=0;k<curves.Length;k++){
                    var curve=curves[k];
                    if(curve.keys.All(key=>Mathf.Abs(key.value-curve[0].value)<.000001f))curve=AnimationCurve.Constant(0,clip.length,curve[0].value);
                    for(int j=0;j<curve.length;j++){AnimationUtility.SetKeyLeftTangentMode(curve,j,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,j,AnimationUtility.TangentMode.Linear);}
                    int root=k-HumanTrait.MuscleCount;string property=root<0?HandMotionRetargeting.CurveName(HumanTrait.MuscleName[k]):root<3?"RootT."+"xyz"[root]:"RootQ."+"xyzw"[root-3];
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);
                }
            }finally{Object.DestroyImmediate(original);}
        }
    }
}
