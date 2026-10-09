using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>从 Flying Mage 原地动作制作八向御风；不修改源 FBX、攻击或场景。</summary>
public static class YufengDirectionalBaker
{
    const string Source = "Assets/resources/Animation Library/Flying_Mage_Volume2/Animations/Humanoid/Inplace/";
    public const string Folder = "Assets/Animations/凭虚御风/八向御风/";
    const string Variants = "Assets/resources/技能动作/御风八向/";
    const string Controller = "Assets/Animations/PlayerLocomotion.controller";
    // 原玩家御风的标准根高度；独立于旧片段，重复构建不会累加高度补偿。
    const float RootHeight = .946f;
    static readonly string[] Names = { "悬停", "前", "右前", "右", "右后", "后", "左后", "左", "左前" };
    static readonly string[] Sources = { "idle_Flying", "move_Flying_front", "move_Flying_frontR45", "move_Flying_right", "move_Flying_backR45", "move_Flying_back", "move_Flying_backL45", "move_Flying_left", "move_Flying_frontL45" };
    static readonly Vector2[] Directions = { Vector2.zero, Vector2.up, new Vector2(.7071068f,.7071068f), Vector2.right, new Vector2(.7071068f,-.7071068f), Vector2.down, new Vector2(-.7071068f,-.7071068f), Vector2.left, new Vector2(-.7071068f,.7071068f) };

    [MenuItem("修仙/动画/重建八向御风（不动场景）")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play");
        var sources = Sources.Select(n => AssetDatabase.LoadAllAssetsAtPath(Source+n+"_inplace.fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"))).ToArray();
        if (sources.Any(c => !c.humanMotion || !c.isLooping)) throw new InvalidOperationException("御风源动作必须为循环 Humanoid");
        float height = Enumerable.Range(0,61).Average(i => Curve(sources[0], "RootT.y").Evaluate(sources[0].length*i/60));
        var clips = new AnimationClip[Names.Length];
        for (int i=0;i<clips.Length;i++)
        {
            var clip = Object.Instantiate(sources[i]);
            clip.name = "御风八向_"+Names[i];
            // 当前游玩场景的 visualYawOffset=0，Humanoid 动作正面为 +Z。
            // 保留源朝向，不能按未动画模型/旧代码的 -X 基准再减90度。
            var curves = Enumerable.Range(0,7).Select(_ => new AnimationCurve()).ToArray();
            var yaw = Quaternion.identity; Quaternion previous = Quaternion.identity;
            int frames = Mathf.RoundToInt(clip.length*60);
            for (int f=0;f<=frames;f++)
            {
                float t=clip.length*f/frames;
                var p=yaw*new Vector3(Curve(sources[i],"RootT.x").Evaluate(t),Curve(sources[i],"RootT.y").Evaluate(t)-height+RootHeight,Curve(sources[i],"RootT.z").Evaluate(t));
                var q=(yaw*new Quaternion(Curve(sources[i],"RootQ.x").Evaluate(t),Curve(sources[i],"RootQ.y").Evaluate(t),Curve(sources[i],"RootQ.z").Evaluate(t),Curve(sources[i],"RootQ.w").Evaluate(t))).normalized;
                if(f>0 && Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                previous=q;
                for(int k=0;k<7;k++)curves[k].AddKey(t,k<3?p[k]:q[k-3]);
            }
            for(int k=0;k<7;k++)Put(clip,"",typeof(Animator),k<3?"RootT."+"xyz"[k]:"RootQ."+"xyzw"[k-3],curves[k]);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime=true;settings.loopBlend=true;
            settings.loopBlendOrientation=true;settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;
            settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;
            AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
            YufengHandPoseBaker.Hands(clip,false);
            clips[i]=Save(clip,Folder+clip.name+".anim");
        }
        foreach(string profile in new[]{"八九玄功","太虚剑决","灵虚剑决"})
        {
            string gripPath=profile=="八九玄功"?"八九玄功/持刀_站":profile=="太虚剑决"?"太虚剑决/持剑_Idle":"灵虚剑决/持剑_Idle";
            var grip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/resources/技能动作/"+gripPath+".anim");
            if(!grip)throw new InvalidOperationException("缺少持械基准 "+gripPath);
            foreach(var source in clips)
            {
                var clip=Object.Instantiate(source);clip.name=source.name;
                YufengHandPoseBaker.Hands(clip,profile!="灵虚剑决");
                if(profile=="八九玄功")YufengHandPoseBaker.Carry(clip,grip);
                if(profile=="灵虚剑决")BakeSword(clip,grip);
                Save(clip,Variants+profile+"/"+clip.name+".anim");
            }
            if(profile=="灵虚剑决")ConfigureFlightSword(grip);
        }
        InstallController(AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller));
        AssetDatabase.SaveAssets();
        Debug.Log("[YufengDirectional] 待机+八向飞行及三套持械变体已生成；源 FBX、攻击与场景未修改");
    }

    public static bool InstallController(AnimatorController controller)
    {
        var clips=Names.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"御风八向_"+n+".anim")).ToArray();
        if(!controller || clips.Any(c=>!c))return false;
        foreach(string parameter in new[]{PlayerAnimationController.御风横向参数,PlayerAnimationController.御风纵向参数})
            if(!controller.parameters.Any(p=>p.name==parameter))controller.AddParameter(parameter,AnimatorControllerParameterType.Float);
        var tree=new BlendTree{name="御风八向",blendType=BlendTreeType.FreeformDirectional2D,blendParameter=PlayerAnimationController.御风横向参数,blendParameterY=PlayerAnimationController.御风纵向参数,useAutomaticThresholds=false};
        for(int i=0;i<clips.Length;i++)tree.AddChild(clips[i],Directions[i]);
        var saved=Save(tree,Folder+"御风八向.asset");
        foreach(var child in controller.layers[0].stateMachine.states)
        {
            var state=child.state;
            if(state.name=="Yufeng_Idle")state.motion=clips[0];
            else if(state.name=="Yufeng_Forward")state.motion=saved;
            else continue;
            state.iKOnFeet=false;
            foreach(var transition in state.transitions)if(transition.destinationState &&
                (transition.destinationState.name=="Yufeng_Idle"||transition.destinationState.name=="Yufeng_Forward"))
            {transition.hasFixedDuration=true;transition.duration=.18f;EditorUtility.SetDirty(transition);}
            EditorUtility.SetDirty(state);
        }
        EditorUtility.SetDirty(controller);return true;
    }

    static void BakeSword(AnimationClip clip,AnimationClip reference)
    {
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Tripo/better_player_test/better_player_test.fbx");
        var target=Object.Instantiate(original);target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
        var animator=target.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var sword=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/Weapons/灵虚剑决/灵虚剑.prefab"),target.transform);
        sword.name="灵虚剑";animator.Rebind();var node=sword.transform.Find("LingxuSword");
        var graph=PlayableGraph.Create("YufengSwordBake");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        try
        {
            var output=AnimationPlayableOutput.Create(graph,"Sword",animator);
            var ready=AnimationClipPlayable.Create(graph,reference);ready.SetApplyFootIK(false);output.SetSourcePlayable(ready);graph.Play();ready.SetTime(0);graph.Evaluate(0);
            var scale=node.localScale;
            var bounds=node.GetComponent<SkinnedMeshRenderer>().sharedMesh.bounds;
            var leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            graph.DestroyPlayable(ready);
            var motion=AnimationClipPlayable.Create(graph,clip);motion.SetApplyFootIK(false);output.SetSourcePlayable(motion);
            int frames=Mathf.RoundToInt(clip.length*60);var curves=Enumerable.Range(0,10).Select(_=>new AnimationCurve()).ToArray();Quaternion previous=Quaternion.identity;
            for(int f=0;f<=frames;f++)
            {
                float t=clip.length*f/frames;motion.SetTime(t);graph.Evaluate(0);
                // 此剑的剑尖是网格 +Z，御风时独立悬浮在双脚下；不再由手腕翻转剑身。
                var left=target.transform.InverseTransformPoint(leftFoot.position);var right=target.transform.InverseTransformPoint(rightFoot.position);
                var p=(left+right)*.5f;
                p.y=Mathf.Min(left.y,right.y)-.12f;
                p.z-=bounds.center.z*scale.z;
                var q=Quaternion.identity;
                if(f>0&&Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                for(int k=0;k<10;k++)curves[k].AddKey(t,k<3?p[k]:k<7?q[k-3]:scale[k-7]);
            }
            for(int k=0;k<10;k++)Put(clip,"灵虚剑/LingxuSword",typeof(Transform),k<3?"m_LocalPosition."+"xyz"[k]:k<7?"m_LocalRotation."+"xyzw"[k-3]:"m_LocalScale."+"xyz"[k-7],curves[k]);
            clip.EnsureQuaternionContinuity();
        }
        finally{graph.Destroy();Object.DestroyImmediate(target);}
    }

    internal static void ConfigureFlightSword(AnimationClip reference)
    {
        const string path="Assets/resources/Weapons/灵虚剑决/灵虚剑.prefab";
        var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Tripo/better_player_test/better_player_test.fbx"));
        target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
        var animator=target.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var sword=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),target.transform);sword.name="灵虚剑";animator.Rebind();
        var graph=PlayableGraph.Create("LingxuFlightRestReference");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        GameObject contents=null;
        try
        {
            var motion=AnimationClipPlayable.Create(graph,reference);motion.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,"Reference",animator).SetSourcePlayable(motion);graph.Play();motion.SetTime(0);graph.Evaluate(0);
            var node=sword.transform.Find("LingxuSword");
            contents=PrefabUtility.LoadPrefabContents(path);
            var pose=contents.GetComponent<LingxuFlightSword>();if(!pose)pose=contents.AddComponent<LingxuFlightSword>();
            pose.后方髋骨偏移=node.localPosition-target.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
            pose.后方旋转=node.localRotation;pose.后方缩放=node.localScale;
            PrefabUtility.SaveAsPrefabAsset(contents,path);
        }
        finally{if(contents)PrefabUtility.UnloadPrefabContents(contents);graph.Destroy();Object.DestroyImmediate(target);}
    }

    static AnimationCurve Curve(AnimationClip clip,string property)=>AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property));
    static void Put(AnimationClip clip,string path,Type type,string property,AnimationCurve curve)
    {
        for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,type,property),curve);
    }
    static T Save<T>(T asset,string path) where T:Object
    {
        string folder=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(folder);
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(asset,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(asset);return old;}
        AssetDatabase.CreateAsset(asset,path);return asset;
    }
    static void EnsureFolder(string folder)
    {
        if(AssetDatabase.IsValidFolder(folder))return;
        string parent=Path.GetDirectoryName(folder).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(folder));
    }
}
