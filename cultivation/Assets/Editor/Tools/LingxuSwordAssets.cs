using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>临时 Avatar 重定向 Legacy 法师动作，并提取独立剑蒙皮；不修改 FBX 或场景。</summary>
public static class LingxuSwordAssets
{
    const string Source="Assets/resources/一些第三方资源/法师/";
    const string Folder="Assets/resources/技能动作/灵虚剑决/";
    const string Weapons="Assets/resources/Weapons/灵虚剑决/";
    // Keep the mesh origin at the grip, while retaining Prop1's authored independent motion.
    static Vector3 GripInPropSpace()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"FB0005Boss.FBX");
        var prop=source.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Bip001 Prop1");
        return prop.InverseTransformPoint(new Vector3(1.645857f,1.85f,.01f));
    }
    static AnimationClip Clip(string name)=>AssetDatabase.LoadAllAssetsAtPath(Source+"FB0005Boss@"+name+".FBX").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));

    sealed class Rig : IDisposable
    {
        public readonly GameObject root;
        public readonly Animator animator;
        public readonly Avatar avatar;
        public readonly Transform[] bones;
        readonly Vector3[] positions,scales;
        readonly Quaternion[] rotations;
        readonly Dictionary<string,int> lookup;
        public Rig()
        {
            root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Source+"FB0005Boss.FBX"));
            root.name="LingxuSampling";root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            foreach(var legacy in root.GetComponentsInChildren<Animation>())UnityEngine.Object.DestroyImmediate(legacy);
            bones=root.GetComponentsInChildren<Transform>(true);
            positions=bones.Select(t=>t.localPosition).ToArray();rotations=bones.Select(t=>t.localRotation).ToArray();scales=bones.Select(t=>t.localScale).ToArray();
            var mapping=new Dictionary<string,string>{{"Hips","Bip001 Pelvis"},{"Spine","Bip001 Spine"},{"Chest","Bip001 Spine1"},{"Neck","Bip001 Neck"},{"Head","Bip001 Head"}};
            foreach(var side in new[]{"Left","Right"}){
                string prefix="Bip001 "+(side=="Left"?"L":"R")+" ";
                foreach(var pair in new[]{("Shoulder","Clavicle"),("UpperArm","UpperArm"),("LowerArm","Forearm"),("Hand","Hand"),("UpperLeg","Thigh"),("LowerLeg","Calf"),("Foot","Foot"),("Toes","Toe0")})mapping[side+pair.Item1]=prefix+pair.Item2;
                foreach(var finger in new[]{("Thumb",0),("Index",1),("Middle",2)}){
                    mapping[side+" "+finger.Item1+" Proximal"]=prefix+"Finger"+finger.Item2;
                    mapping[side+" "+finger.Item1+" Intermediate"]=prefix+"Finger"+finger.Item2+"1";
                    mapping[side+" "+finger.Item1+" Distal"]=prefix+"Finger"+finger.Item2+"2";
                }
            }
            // The model is authored with lowered arms. BuildHumanAvatar assumes the supplied
            // skeleton is the humanoid reference pose; an A-pose here shifts the entire arm
            // range (previously ~60 degrees) and corrupts the forearm/wrist twist reference.
            CalibrateReferencePose();
            var description=new HumanDescription{
                human=mapping.Select(p=>new HumanBone{humanName=p.Key,boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),
                skeleton=bones.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),
                upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f
            };
            avatar=AvatarBuilder.BuildHumanAvatar(root,description);
            if(!avatar.isValid||!avatar.isHuman)throw new InvalidOperationException("法师人形骨骼映射失败");
            animator=root.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;
            foreach(var pair in mapping){
                var bone=(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),pair.Key.Replace(" ",""));
                if(!animator.GetBoneTransform(bone))throw new InvalidOperationException("Avatar 漏映射骨骼："+pair.Key);
            }
            // The reference is only for Avatar construction. Every sampled frame starts from
            // the original FBX defaults, so native animation and the independent Prop1 stay intact.
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];bones[i].localScale=scales[i];}
            lookup=bones.Select((t,i)=>new{path=AnimationUtility.CalculateTransformPath(t,root.transform),i}).ToDictionary(x=>x.path,x=>x.i);
        }
        void CalibrateReferencePose()
        {
            Transform Bone(string name)=>bones.First(t=>t.name==name);
            foreach(string side in new[]{"L","R"}){
                string prefix="Bip001 "+side+" ";var direction=side=="L"?Vector3.left:Vector3.right;
                var shoulder=Bone(prefix+"Clavicle");var upper=Bone(prefix+"UpperArm");var lower=Bone(prefix+"Forearm");var hand=Bone(prefix+"Hand");
                shoulder.rotation=Quaternion.FromToRotation(upper.position-shoulder.position,direction)*shoulder.rotation;
                upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,direction)*lower.rotation;
                // T-pose palms face down, fingertips point outwards, thumbs point forward.
                // Construct this frame geometrically; Biped local axes differ on each side.
                var finger=Bone(prefix+"Finger2").position-hand.position;
                var thumb=Bone(prefix+"Finger0").position-Bone(prefix+"Finger2").position;
                var normal=Vector3.Cross(finger,thumb).normalized*(side=="L"?1f:-1f);
                var current=Quaternion.LookRotation(finger,normal);
                var desired=Quaternion.LookRotation(direction,Vector3.up);
                hand.rotation=desired*Quaternion.Inverse(current)*hand.rotation;
            }
        }
        public AnimationClip Bake(string motion,string name,bool loop)
        {
            var raw=Clip(motion);var bindings=AnimationUtility.GetCurveBindings(raw).Where(b=>b.type==typeof(Transform)&&lookup.ContainsKey(b.path)).ToArray();
            var native=bindings.Select(b=>AnimationUtility.GetEditorCurve(raw,b)).ToArray();
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
            var pose=new HumanPose();Quaternion previous=Quaternion.identity;Vector3 start=Vector3.zero;
            int frames=Mathf.CeilToInt(raw.length*60);
            int swordFrames=Mathf.CeilToInt(raw.length*240);
            var swordPositions=new Vector3[swordFrames+1];var swordRotations=new Quaternion[swordFrames+1];var swordScales=new Vector3[swordFrames+1];var hipPositions=new Vector3[swordFrames+1];
            var prop=bones.First(t=>t.name=="Bip001 Prop1");var hips=animator.GetBoneTransform(HumanBodyBones.Hips);var grip=GripInPropSpace();
            void Sample(float time){
                var ps=(Vector3[])positions.Clone();var qs=(Quaternion[])rotations.Clone();var ss=(Vector3[])scales.Clone();
                for(int j=0;j<bindings.Length;j++){
                    var binding=bindings[j];int index=lookup[binding.path],axis="xyzw".IndexOf(binding.propertyName[binding.propertyName.Length-1]);if(axis<0)continue;
                    float value=native[j].Evaluate(time);
                    if(binding.propertyName.Contains("LocalPosition"))ps[index][axis]=value;
                    else if(binding.propertyName.Contains("LocalRotation"))qs[index][axis]=value;
                    else if(binding.propertyName.Contains("LocalScale"))ss[index][axis]=value;
                }
                for(int j=0;j<bones.Length;j++){bones[j].localPosition=ps[j];bones[j].localRotation=qs[j].normalized;bones[j].localScale=ss[j];}
            }
            using(var handler=new HumanPoseHandler(avatar,root.transform))
            for(int frame=0;frame<=frames;frame++){
                float time=raw.length*frame/frames;Sample(time);
                handler.GetHumanPose(ref pose);
                if(frame==0)start=pose.bodyPosition;
                // In-place X/Z: gameplay controls travel. Keep authored vertical movement and torso pose.
                pose.bodyPosition.x-=start.x;pose.bodyPosition.z-=start.z;
                var q=pose.bodyRotation;if(frame>0&&Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                for(int i=0;i<curves.Length;i++){
                    int k=i-HumanTrait.MuscleCount;float value=k<0?pose.muscles[i]:k<3?pose.bodyPosition[k]:q[k-3];
                    if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("非法人形姿态 "+motion);
                    curves[i].AddKey(new Keyframe(time,value));
                }
            }
            var result=new AnimationClip{name=name,frameRate=60};
            for(int i=0;i<curves.Length;i++){
                var c=curves[i];for(int j=0;j<c.length;j++){AnimationUtility.SetKeyLeftTangentMode(c,j,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,j,AnimationUtility.TangentMode.Linear);}
                int k=i-HumanTrait.MuscleCount;string property=k<0?HumanTrait.MuscleName[i]:k<3?"RootT."+"xyz"[k]:"RootQ."+"xyzw"[k-3];
                AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),property),c);
            }
            result.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(result);settings.loopTime=loop;
            settings.keepOriginalPositionY=true;settings.loopBlendPositionY=true;settings.keepOriginalOrientation=true;settings.loopBlendOrientation=true;
            AnimationUtility.SetAnimationClipSettings(result,settings);
            HandMotionRetargeting.Apply(result,avatar);
            CalibratePlayerArms(result,Sample);
            // Prop1 spins much faster than the body during AttackA. Densely sample only the
            // ten sword curves so interpolation retains the arc without bloating muscle curves.
            for(int frame=0;frame<=swordFrames;frame++){
                Sample(raw.length*frame/swordFrames);
                swordPositions[frame]=prop.TransformPoint(grip);swordRotations[frame]=prop.rotation;swordScales[frame]=prop.lossyScale;hipPositions[frame]=hips.position;
            }
            BakeSword(result,swordPositions,swordRotations,swordScales,hipPositions,animator.humanScale);
            Debug.Log("[灵虚剑决] "+motion+" → "+name+" "+raw.length+"s（身体 + Prop1 剑轨迹）");return result;
        }
        void CalibratePlayerArms(AnimationClip clip,Action<float> sample)
        {
            var original=GameObject.Find("Player").GetComponentInChildren<Animator>(true);
            var target=UnityEngine.Object.Instantiate(original.gameObject);
            var graph=PlayableGraph.Create("LingxuArmCalibration");
            try{
                foreach(var script in target.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
                var a=target.GetComponent<Animator>();a.runtimeAnimatorController=null;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(graph,"Body",a).SetSourcePlayable(playable);graph.Play();
                var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();
                var pose=new HumanPose();Quaternion previous=Quaternion.identity;
                using(var handler=new HumanPoseHandler(a.avatar,target.transform))
                for(int f=0,frames=Mathf.CeilToInt(clip.length*240);f<=frames;f++){
                    float time=clip.length*f/frames;sample(time);playable.SetTime(time);graph.Evaluate(0);
                    var from=TorsoFrame(animator);var to=TorsoFrame(a);var transfer=to*Quaternion.Inverse(from);
                    foreach(string side in new[]{"Left","Right"}){
                        Transform S(string part)=>animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+part));
                        Transform T(string part)=>a.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+part));
                        var su=S("UpperArm");var sl=S("LowerArm");var sh=S("Hand");
                        var tu=T("UpperArm");var tl=T("LowerArm");var th=T("Hand");
                        var upper=transfer*(sl.position-su.position).normalized;
                        var lower=transfer*(sh.position-sl.position).normalized;
                        var currentUpper=(tl.position-tu.position).normalized;var currentLower=(th.position-tl.position).normalized;
                        var normal=Vector3.Cross(upper,lower);var currentNormal=Vector3.Cross(currentUpper,currentLower);
                        // Match the elbow plane as well as the upper-arm direction. Merely aiming
                        // each segment independently can put the elbow bend outside its hinge.
                        if(normal.sqrMagnitude>.0001f&&currentNormal.sqrMagnitude>.0001f)
                            tu.rotation=Quaternion.LookRotation(upper,normal)*Quaternion.Inverse(Quaternion.LookRotation(currentUpper,currentNormal))*tu.rotation;
                        else tu.rotation=Quaternion.FromToRotation(currentUpper,upper)*tu.rotation;
                        tl.rotation=Quaternion.FromToRotation(th.position-tl.position,lower)*tl.rotation;
                        var desired=transfer*PalmFrame(sh,S("MiddleProximal"),S("ThumbProximal"));
                        th.rotation=desired*Quaternion.Inverse(PalmFrame(th,T("MiddleProximal"),T("ThumbProximal")))*th.rotation;
                    }
                    handler.GetHumanPose(ref pose);var q=pose.bodyRotation;
                    if(f>0&&Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                    for(int i=0;i<curves.Length;i++){
                        int k=i-HumanTrait.MuscleCount;float v=k<0?pose.muscles[i]:k<3?pose.bodyPosition[k]:q[k-3];
                        curves[i].AddKey(new Keyframe(time,v));
                    }
                }
                for(int i=0;i<curves.Length;i++){
                    int n=i-HumanTrait.MuscleCount;
                    // Only corrected arm muscles and their centre-of-mass compensation need
                    // dense sampling. Keep the original body/legs/finger animation untouched.
                    if(n<0){var muscle=HumanTrait.MuscleName[i];if(!(muscle.Contains(" Arm ")||muscle.Contains(" Forearm ")||muscle.Contains(" Hand ")))continue;}
                    var c=curves[i];for(int k=0;k<c.length;k++){AnimationUtility.SetKeyLeftTangentMode(c,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,k,AnimationUtility.TangentMode.Linear);}
                    if(c.keys.All(k=>Mathf.Abs(k.value-c.keys[0].value)<.00001f))c=AnimationCurve.Linear(0,c.keys[0].value,clip.length,c.keys[0].value);
                    string name=n<0?HandMotionRetargeting.CurveName(HumanTrait.MuscleName[i]):n<3?"RootT."+"xyz"[n]:"RootQ."+"xyzw"[n-3];
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),name),c);
                }
                HandMotionRetargeting.Apply(clip);clip.EnsureQuaternionContinuity();
            }finally{graph.Destroy();UnityEngine.Object.DestroyImmediate(target);}
        }
        public void Dispose(){UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(avatar);}
    }

    static Quaternion TorsoFrame(Animator a)
    {
        var right=a.GetBoneTransform(HumanBodyBones.RightUpperArm).position-a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
        var up=(a.GetBoneTransform(HumanBodyBones.Neck)??a.GetBoneTransform(HumanBodyBones.Head)).position-a.GetBoneTransform(HumanBodyBones.Hips).position;
        return Quaternion.LookRotation(Vector3.Cross(right,up).normalized,up);
    }
    static Quaternion PalmFrame(Transform hand,Transform middle,Transform thumb)
    {
        var finger=middle.position-hand.position;
        return Quaternion.LookRotation(finger,Vector3.Cross(finger,thumb.position-hand.position));
    }

    static void BakeSword(AnimationClip clip,Vector3[] positions,Quaternion[] rotations,Vector3[] scales,Vector3[] hips,float sourceScale)
    {
        var player=GameObject.Find("Player");var original=player?player.GetComponentInChildren<Animator>(true):null;
        if(!original)throw new InvalidOperationException("请在有 Player 的场景重建，用玩家 Avatar 标定独立剑轨迹");
        var target=UnityEngine.Object.Instantiate(original.gameObject);
        var graph=PlayableGraph.Create("LingxuSwordBake");
        try{
            foreach(var script in target.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
            var animator=target.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
            var output=AnimationPlayableOutput.Create(graph,"Body",animator);output.SetSourcePlayable(playable);graph.Play();
            float ratio=animator.humanScale/sourceScale;
            var curves=Enumerable.Range(0,10).Select(_=>new AnimationCurve()).ToArray();Quaternion previous=Quaternion.identity;
            for(int frame=0;frame<positions.Length;frame++){
                float time=clip.length*frame/(positions.Length-1);playable.SetTime(time);graph.Evaluate(0);
                // Anchor to the retargeted pelvis, not the hand: the original sword flies independently.
                // This also removes authored horizontal root travel exactly as the body bake does.
                var p=target.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position)+(positions[frame]-hips[frame])*ratio;
                var q=rotations[frame];if(frame>0&&Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                var s=scales[frame]*ratio;
                for(int i=0;i<10;i++)curves[i].AddKey(new Keyframe(time,i<3?p[i]:i<7?q[i-3]:s[i-7]));
            }
            for(int i=0;i<10;i++){
                var c=curves[i];for(int j=0;j<c.length;j++){AnimationUtility.SetKeyLeftTangentMode(c,j,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,j,AnimationUtility.TangentMode.Linear);}
                string property=i<3?"m_LocalPosition."+"xyz"[i]:i<7?"m_LocalRotation."+"xyzw"[i-3]:"m_LocalScale."+"xyz"[i-7];
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("灵虚剑/LingxuSword",typeof(Transform),property),c);
            }
            clip.EnsureQuaternionContinuity();
        }finally{if(graph.IsValid())graph.Destroy();UnityEngine.Object.DestroyImmediate(target);}
    }

    [MenuItem("修仙/动画/重建灵虚剑决（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请停止 Play 后重建");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Weapons);AssetDatabase.Refresh();
        using(var rig=new Rig()){
            foreach(var entry in new[]{("AttackA","A1",false),("AttackB","A2",false),("SkillA","A3",false),("Ready","持剑_Idle",true),("Run","持剑_Run",true)})
                Save(rig.Bake(entry.Item1,entry.Item2,entry.Item3),Folder+entry.Item2+".anim");
        }
        BuildWeapon();
        YufengDirectionalBaker.ConfigureFlightSword(AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"持剑_Idle.anim"));
        安全导入配置表.ImportAssetsOnly("功法表","物品表");AssetDatabase.SaveAssets();
        Debug.Log("[灵虚剑决] 三式、Ready、Run、独立剑与秘籍已生成；源 FBX 与场景未修改");
    }
    static void Save(UnityEngine.Object value,string path)
    {
        var existing=AssetDatabase.LoadMainAssetAtPath(path);
        if(existing){
            if(existing is Mesh mesh&&value is Mesh fresh){
                // CopySerialized can leave Mesh's native vertex cache at the previous geometry.
                mesh.Clear();mesh.indexFormat=fresh.indexFormat;mesh.vertices=fresh.vertices;mesh.normals=fresh.normals;mesh.tangents=fresh.tangents;mesh.uv=fresh.uv;
                mesh.boneWeights=fresh.boneWeights;mesh.bindposes=fresh.bindposes;mesh.subMeshCount=fresh.subMeshCount;
                for(int i=0;i<fresh.subMeshCount;i++)mesh.SetTriangles(fresh.GetTriangles(i),i);
                mesh.bounds=fresh.bounds;
            }else EditorUtility.CopySerialized(value,existing);
            EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(value);
        }
        else AssetDatabase.CreateAsset(value,path);
    }
    static void BuildWeapon()
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"FB0005Boss.FBX").GetComponentInChildren<SkinnedMeshRenderer>();
        var mesh=source.sharedMesh;var weights=mesh.boneWeights;int swordBone=Array.FindIndex(source.bones,t=>t.name=="Bip001 Prop1");
        var indices=Enumerable.Range(0,mesh.vertexCount).Where(i=>weights[i].boneIndex0==swordBone&&weights[i].weight0>.99f).ToArray();
        if(indices.Length!=204)throw new InvalidOperationException("源剑蒙皮分区发生变化，请重新检查提取范围");
        var remap=indices.Select((id,i)=>new{id,i}).ToDictionary(x=>x.id,x=>x.i);
        var original=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
        var bind=mesh.bindposes[swordBone];var normalMatrix=bind.inverse.transpose;var grip=GripInPropSpace();
        var result=new Mesh{name="灵虚剑"};
        result.vertices=indices.Select(i=>bind.MultiplyPoint3x4(original[i])-grip).ToArray();
        result.normals=indices.Select(i=>normalMatrix.MultiplyVector(normals[i]).normalized).ToArray();result.uv=indices.Select(i=>uv[i]).ToArray();
        result.subMeshCount=mesh.subMeshCount;int triangles=0;
        for(int s=0;s<mesh.subMeshCount;s++){
            var src=mesh.GetTriangles(s);var dst=new List<int>();
            for(int t=0;t<src.Length;t+=3)if(remap.ContainsKey(src[t])&&remap.ContainsKey(src[t+1])&&remap.ContainsKey(src[t+2])){dst.Add(remap[src[t]]);dst.Add(remap[src[t+1]]);dst.Add(remap[src[t+2]]);}
            result.SetTriangles(dst,s);triangles+=dst.Count/3;
        }
        result.boneWeights=indices.Select(_=>new BoneWeight{boneIndex0=0,weight0=1}).ToArray();result.bindposes=new[]{Matrix4x4.identity};result.RecalculateBounds();result.RecalculateTangents();
        Save(result,Weapons+"灵虚剑.asset");
        var holder=new GameObject("灵虚剑");
        try{
            var node=new GameObject("LingxuSword");node.transform.SetParent(holder.transform,false);var renderer=node.AddComponent<SkinnedMeshRenderer>();
            var material=new Material(Shader.Find("Standard")){name="灵虚剑"};
            material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"texture/b_3007_Wuyaoyuji_AlphaCO.tga.png");
            material.SetFloat("_Glossiness",.32f);material.SetFloat("_Metallic",.25f);Save(material,Weapons+"灵虚剑.mat");
            renderer.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Weapons+"灵虚剑.asset");renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Weapons+"灵虚剑.mat");
            renderer.rootBone=node.transform;renderer.bones=new[]{node.transform};renderer.updateWhenOffscreen=true;
            PrefabUtility.SaveAsPrefabAsset(holder,Weapons+"灵虚剑.prefab");
        }finally{UnityEngine.Object.DestroyImmediate(holder);}
        Debug.Log("[灵虚剑决] 提取剑 "+indices.Length+" 顶点 / "+triangles+" 三角形，保留 Prop1 骨骼空间、UV 与源贴图；轨迹与大小按 Avatar 比例烘焙");
    }
    [MenuItem("修仙/调试/给予灵虚剑决秘籍（仅当前 Play 背包）")]
    public static void GiveManual()
    {
        if(!EditorApplication.isPlaying)return;var panel=UnityEngine.Object.FindObjectOfType<UIPanelData>();
        var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_gongfa_lingxu_jianjue.asset");
        if(panel&&item&&panel.物品数量(item)==0)panel.给物品(item,1);
    }
}
