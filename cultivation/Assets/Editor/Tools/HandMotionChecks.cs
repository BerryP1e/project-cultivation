using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

/// <summary>在真实玩家骨架上验证手指绑定与关节运动，不以曲线存在作为成功标准。</summary>
public static class HandMotionChecks
{
    [MenuItem("修仙/调试/验证三套近战手部重定向（不动场景）")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        var original=GameObject.Find("Player").GetComponentInChildren<Animator>(true);
        var target=Object.Instantiate(original.gameObject);
        foreach(var script in target.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
        target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
        var animator=target.GetComponent<Animator>();animator.runtimeAnimatorController=null;
        animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var bones=Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>()
            .Where(b=>b==HumanBodyBones.LeftHand||b==HumanBodyBones.RightHand||b>=HumanBodyBones.LeftThumbProximal&&b<=HumanBodyBones.RightLittleDistal).ToArray();
        var graph=PlayableGraph.Create("HandArticulationCheck");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        int checkedClips=0,animatedClips=0;
        try{
            foreach(var bone in bones)Require(animator.GetBoneTransform(bone),"玩家缺少手部关节 "+bone);
            foreach(string folder in new[]{"灵虚剑决","太虚剑决","八九玄功"})
            foreach(var clip in Resources.LoadAll<AnimationClip>("技能动作/"+folder)){
                var bindings=AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Animator)).ToArray();
                foreach(var binding in bindings)Require(HandMotionRetargeting.CurveName(binding.propertyName)==binding.propertyName,"无效的手指曲线名 "+clip.name+"/"+binding.propertyName);
                var fingers=bindings.Where(b=>b.propertyName.Contains("Stretched")||b.propertyName.Contains("Spread")).ToArray();
                Require(fingers.Length==40,"手指曲线不完整 "+folder+"/"+clip.name);
                foreach(var binding in bindings.Where(b=>b.propertyName.Contains("Hand ")||fingers.Contains(b)))
                    for(int i=0;i<=60;i++)Require(Mathf.Abs(AnimationUtility.GetEditorCurve(clip,binding).Evaluate(clip.length*i/60))<=1.001f,"手部肌肉超过 Avatar 范围 "+clip.name+"/"+binding.propertyName);
                bool animated=fingers.Select(b=>AnimationUtility.GetEditorCurve(clip,b)).Any(c=>c.keys.Max(k=>k.value)-c.keys.Min(k=>k.value)>.05f);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
                var output=AnimationPlayableOutput.Create(graph,"Sample",animator);output.SetSourcePlayable(playable);graph.Play();
                Quaternion[] first=null;float fingerAngle=0;
                for(int i=0;i<=60;i++){
                    playable.SetTime(clip.length*i/60);graph.Evaluate(0);
                    var rotations=bones.Select(b=>animator.GetBoneTransform(b).localRotation.normalized).ToArray();
                    if(i==0)first=rotations;
                    else for(int j=0;j<bones.Length;j++)if(bones[j]!=HumanBodyBones.LeftHand&&bones[j]!=HumanBodyBones.RightHand)fingerAngle=Mathf.Max(fingerAngle,Quaternion.Angle(first[j],rotations[j]));
                }
                Require(!animated||fingerAngle>1,"曲线有变化但实际手指未运动 "+folder+"/"+clip.name);
                if(animated)animatedClips++;checkedClips++;
                Debug.Log("[HandMotionChecks] "+folder+"/"+clip.name+" actualFingerRotation="+fingerAngle.ToString("F1"));
                graph.DestroyOutput(output);graph.DestroyPlayable(playable);
            }
            Debug.Log("HAND_ARTICULATION_CHECK_PASS wrists=2 fingers=30 clips="+checkedClips+" animated="+animatedClips);
        }finally{graph.Destroy();Object.DestroyImmediate(target);}
    }
    static void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
}
