using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>从定稿地面持握传递右臂和武器安装角，完整编码HumanPose保留御风身体。</summary>
public static class SpiritSwordFlightGripBaker
{
    const string Model="Assets/Art/Characters/Tripo/better_player_test/better_player_test.fbx";
    static readonly HumanBodyBones[] Arm={HumanBodyBones.RightShoulder,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
    public static void Apply(AnimationClip clip,AnimationClip reference)
    {
        if(!reference)throw new InvalidOperationException("Missing approved spirit sword ground grip");
        var approved=AnimationUtility.GetCurveBindings(reference);
        if(approved.Count(b=>b.type==typeof(Animator)&&b.propertyName.StartsWith("RightHand."))!=20 ||
           approved.Count(b=>b.type==typeof(Transform)&&b.path.Contains("/太虚剑决_神秘人剑/"))!=7)
            throw new InvalidOperationException("Approved spirit sword grip must include 20 finger and 7 weapon calibration curves");
        var scene=EditorSceneManager.NewPreviewScene();GameObject model=null;PlayableGraph graph=default;
        AnimationClip original=null;
        try{
            model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));SceneManager.MoveGameObjectToScene(model,scene);model.hideFlags=HideFlags.HideAndDontSave;model.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=Vector3.one;
            var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            graph=PlayableGraph.Create("Spirit sword approved carry");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var output=AnimationPlayableOutput.Create(graph,"Pose",animator);graph.Play();
            var ready=AnimationClipPlayable.Create(graph,reference);ready.SetApplyFootIK(false);output.SetSourcePlayable(ready);ready.SetTime(0);graph.Evaluate(0);
            var rotations=Arm.Select(b=>animator.GetBoneTransform(b).rotation).ToArray();
            original=Object.Instantiate(clip);var motion=AnimationClipPlayable.Create(graph,original);motion.SetApplyFootIK(false);output.SetSourcePlayable(motion);
            var curves=Enumerable.Range(0,HumanTrait.MuscleCount+7).Select(_=>new AnimationCurve()).ToArray();int frames=Mathf.Max(1,Mathf.CeilToInt(clip.length*60));var pose=new HumanPose();Quaternion previous=Quaternion.identity;
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform)){
                for(int f=0;f<=frames;f++){
                    float t=clip.length*f/frames;motion.SetTime(Mathf.Min(t,Mathf.Max(0,clip.length-.00001f)));graph.Evaluate(0);
                    // 地面已定稿的整条持剑臂保持其世界方向，身体仍保留本帧御风倾身。
                    for(int b=0;b<Arm.Length;b++)animator.GetBoneTransform(Arm[b]).rotation=rotations[b];
                    handler.GetHumanPose(ref pose);var q=pose.bodyRotation;if(f>0&&Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                    for(int k=0;k<curves.Length;k++){int root=k-HumanTrait.MuscleCount;curves[k].AddKey(t,root<0?pose.muscles[k]:root<3?pose.bodyPosition[root]:q[root-3]);}
                }
            }
            for(int k=0;k<curves.Length;k++){
                var curve=curves[k];for(int j=0;j<curve.length;j++){AnimationUtility.SetKeyLeftTangentMode(curve,j,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,j,AnimationUtility.TangentMode.Linear);}
                int root=k-HumanTrait.MuscleCount;string property=root<0?HandMotionRetargeting.CurveName(HumanTrait.MuscleName[k]):root<3?"RootT."+"xyz"[root]:"RootQ."+"xyzw"[root-3];
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);
            }
            // 手指闭合与武器实际安装角必须一起传递，不能用默认prefab姿态代替。
            foreach(var binding in AnimationUtility.GetCurveBindings(reference).Where(b=>b.type==typeof(Animator)&&b.propertyName.StartsWith("RightHand.")||b.type==typeof(Transform)&&b.path.Contains("/太虚剑决_神秘人剑/")))
                AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0,clip.length,AnimationUtility.GetEditorCurve(reference,binding).Evaluate(0)));
            clip.EnsureQuaternionContinuity();
        }finally{if(graph.IsValid())graph.Destroy();if(original)Object.DestroyImmediate(original);if(model)Object.DestroyImmediate(model);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
