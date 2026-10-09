using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

/// <summary>御风手部离线烘焙：自然屈指、持柄与地面持刀角度；运行时无需 IK。</summary>
public static class YufengHandPoseBaker
{
    static readonly string[] Fingers={"Thumb","Index","Middle","Ring","Little"};
    // 逐节区分自然屈曲，避免三个指节全部拉到极值形成钩爪。
    static readonly float[,] Free={{-.10f,.25f,.35f},{.15f,.10f,.20f},{.05f,0,.12f},{-.02f,-.05f,.05f},{-.08f,-.12f,0}};
    static readonly float[,] Grip={{-.60f,0,0},{-.42f,-.62f,-.35f},{-.47f,-.67f,-.40f},{-.50f,-.64f,-.38f},{-.48f,-.58f,-.34f}};

    public static void Hands(AnimationClip clip,bool rightGrip)
    {
        foreach(string side in new[]{"Left","Right"})
        {
            bool holding=side=="Right"&&rightGrip;
            for(int finger=0;finger<5;finger++)
            {
                for(int joint=0;joint<3;joint++)
                {
                    string property=side+"Hand."+Fingers[finger]+"."+(joint+1)+" Stretched";
                    var curve=new AnimationCurve();int frames=Mathf.RoundToInt(clip.length*60);
                    for(int f=0;f<=frames;f++)
                    {
                        float t=clip.length*f/frames;
                        float breathe=holding?0:.025f*Mathf.Cos(2*Mathf.PI*f/frames+finger*.55f+joint*.25f);
                        curve.AddKey(t,(holding?Grip:Free)[finger,joint]+breathe);
                    }
                    Put(clip,property,curve);
                }
                float spread=finger==0?(holding?-.40f:.10f):finger==1?-.08f:finger==4?.06f:0;
                Put(clip,side+"Hand."+Fingers[finger]+".Spread",AnimationCurve.Constant(0,clip.length,spread));
            }
            foreach(string axis in new[]{"Down-Up","In-Out"})
            {
                string property=side+" Hand "+axis;
                var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property));
                if(curve==null)continue;
                var keys=curve.keys;
                for(int i=0;i<keys.Length;i++)keys[i].value=Mathf.Clamp(keys[i].value*.45f,-.35f,.35f);
                curve.keys=keys;Put(clip,property,curve);
            }
        }
    }

    public static void Carry(AnimationClip clip,AnimationClip reference)
    {
        var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/better_player_test/better_player_test.fbx"));
        target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
        var animator=target.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var graph=PlayableGraph.Create("YufengCarryBake");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        using(var handler=new HumanPoseHandler(animator.avatar,target.transform))try
        {
            var output=AnimationPlayableOutput.Create(graph,"Carry",animator);
            var ready=AnimationClipPlayable.Create(graph,reference);ready.SetApplyFootIK(false);output.SetSourcePlayable(ready);graph.Play();ready.SetTime(0);graph.Evaluate(0);
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);var lower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var upperRotation=upper.rotation;var lowerRotation=lower.rotation;var handRotation=hand.rotation;
            // 只校准持刀一侧的臂腕；躯干、左臂和八向腿部动作保持原样。
            var indices=Enumerable.Range(0,HumanTrait.MuscleCount).Where(i=>HumanTrait.MuscleName[i].StartsWith("Right ")&&(HumanTrait.MuscleName[i].Contains(" Arm ")||HumanTrait.MuscleName[i].Contains(" Forearm ")||HumanTrait.MuscleName[i].Contains(" Hand "))).ToArray();
            graph.DestroyPlayable(ready);
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);output.SetSourcePlayable(playable);
            var curves=Enumerable.Range(0,indices.Length+7).Select(_=>new AnimationCurve()).ToArray();int frames=Mathf.RoundToInt(clip.length*60);
            Quaternion previous=Quaternion.identity;
            for(int frame=0;frame<=frames;frame++)
            {
                float t=clip.length*frame/frames;playable.SetTime(t);graph.Evaluate(0);
                // 保持地面参考的上臂/前臂/腕三段世界方向，不能只强扭手腕补偿刀身。
                upper.rotation=upperRotation;lower.rotation=lowerRotation;hand.rotation=handRotation;
                var pose=new HumanPose();handler.GetHumanPose(ref pose);
                var q=pose.bodyRotation;
                if(frame>0 && Quaternion.Dot(q,previous)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);previous=q;
                for(int i=0;i<curves.Length;i++)
                {
                    int root=i-indices.Length;
                    float value=root<0?Mathf.Clamp(pose.muscles[indices[i]],-1,1):root<3?pose.bodyPosition[root]:q[root-3];
                    curves[i].AddKey(t,value);
                }
            }
            for(int i=0;i<curves.Length;i++)
            {
                int root=i-indices.Length;
                Put(clip,root<0?HumanTrait.MuscleName[indices[i]]:root<3?"RootT."+"xyz"[root]:"RootQ."+"xyzw"[root-3],curves[i]);
            }
            clip.EnsureQuaternionContinuity();
        }
        finally{graph.Destroy();Object.DestroyImmediate(target);}
    }

    static void Put(AnimationClip clip,string property,AnimationCurve curve)
    {
        for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);
    }
}
