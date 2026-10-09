using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

/// <summary>玄霄四式：修复后的原普攻、真实人形左右镜像、FlyingAtk02、FlyingAtk03_short。</summary>
public static class ThunderComboBaker
{
    const string Folder="Assets/resources/技能动作/玄霄雷诀";
    [MenuItem("修仙/动画/重建玄霄雷诀四式（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请停止Play后烘焙");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/resources/技能动作","玄霄雷诀");
        Mirror(AssetDatabase.LoadAssetAtPath<AnimationClip>(RemoteAttackMotionAssets.MotionPath));
        Flying("attack_FlyingAtk02","A3");Flying("attack_FlyingAtk03_short","A4");
        AssetDatabase.SaveAssets();Debug.Log("[玄霄雷诀] 四式已烘焙，保留腕指曲线与根高度，不动源FBX/场景");
    }
    static void Save(AnimationClip clip,string name)
    {
        clip.name=name;clip.wrapMode=WrapMode.Once;
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.loopBlend=false;
        if(name!="A2"){settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;}
        AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
        var path=Folder+"/"+name+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(old){EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(clip);}else AssetDatabase.CreateAsset(clip,path);
    }
    static void Flying(string name,string output)
    {
        const string sourceFolder="Assets/resources/Animation Library/Flying_Mage_Volume2/Animations/Humanoid/";
        var source=AssetDatabase.LoadAllAssetsAtPath(sourceFolder+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        var idle=AssetDatabase.LoadAllAssetsAtPath(sourceFolder+"Inplace/idle_Flying_inplace.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");var reference=AnimationUtility.GetEditorCurve(idle,binding);
        float height=Enumerable.Range(0,61).Average(i=>reference.Evaluate(idle.length*i/60f));
        var clip=Object.Instantiate(source);var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;
        for(int i=0;i<keys.Length;i++)keys[i].value+=.946f-height;curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
        HandMotionRetargeting.Apply(clip,HandMotionRetargeting.AvatarFor(source));Save(clip,output);
    }
    static void Mirror(AnimationClip source)
    {
        if(!source)throw new InvalidOperationException("缺少已修复的太虚炼气诀普攻");
        var rig=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Tripo/better_player_test/better_player_test.fbx"));
        var animator=rig.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var controller=new AnimatorController();controller.AddLayer("Mirror");var state=controller.layers[0].stateMachine.AddState("Pose");state.motion=source;state.mirror=true;controller.layers[0].stateMachine.defaultState=state;
        var graph=PlayableGraph.Create("ThunderMirror");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playable=AnimatorControllerPlayable.Create(graph,controller);AnimationPlayableOutput.Create(graph,"Pose",animator).SetSourcePlayable(playable);graph.Play();
        var clip=new AnimationClip{frameRate=120};var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();var pose=new HumanPose();Quaternion previous=Quaternion.identity;
        try{
            using(var handler=new HumanPoseHandler(animator.avatar,rig.transform))
            for(int i=0,frames=Mathf.CeilToInt(source.length*120);i<=frames;i++){
                float time=source.length*i/frames;playable.Play(state.nameHash,0,(float)i/frames);graph.Evaluate(0);handler.GetHumanPose(ref pose);
                for(int m=0;m<HumanTrait.MuscleCount;m++)curves[m].AddKey(time,pose.muscles[m]);
                var q=pose.bodyRotation;if(i>0&&Quaternion.Dot(previous,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                for(int j=0;j<3;j++)curves[HumanTrait.MuscleCount+j].AddKey(time,pose.bodyPosition[j]);
                for(int j=0;j<4;j++)curves[HumanTrait.MuscleCount+3+j].AddKey(time,q[j]);
            }
            for(int m=0;m<HumanTrait.MuscleCount;m++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HandMotionRetargeting.CurveName(HumanTrait.MuscleName[m])),curves[m]);
            // Controller将RootQ中的转体抽成根运动；GetHumanPose不包含这部分。
            // 根曲线直接反射源数据，保留原高度，避免镜像后丢失/重复转体。
            foreach(string property in new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}){
                var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),property);var curve=AnimationUtility.GetEditorCurve(source,binding);if(curve==null)continue;
                if(property=="RootT.x"||property=="RootQ.y"||property=="RootQ.z"){
                    var keys=curve.keys;for(int k=0;k<keys.Length;k++){keys[k].value=-keys[k].value;keys[k].inTangent=-keys[k].inTangent;keys[k].outTangent=-keys[k].outTangent;}curve.keys=keys;
                }
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            AnimationUtility.SetAnimationClipSettings(clip,AnimationUtility.GetAnimationClipSettings(source));
            HandMotionRetargeting.Apply(clip,animator.avatar);Save(clip,"A2");
        }finally{graph.Destroy();Object.DestroyImmediate(rig);Object.DestroyImmediate(controller);}
    }
}
