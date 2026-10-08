using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

/// <summary>直接采样 Generic 副本，对照实际玩家；不使用烘焙器的姿态采样函数。</summary>
public static class RemoteAttackMotionChecks
{
    [MenuItem("修仙/调试/验证远程普攻原动画与玩家关节（不动场景）")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止运行");
        string path=RemoteAttackMotionAssets.CreateNativeCopy();
        GameObject source=null,target=null;var graph=PlayableGraph.Create("RemoteNativePoseCheck");
        try{
            var raw=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RemoteAttackMotionAssets.ModelPath));
            Object.DestroyImmediate(source.GetComponent<Animator>());source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            target=Object.Instantiate(GameObject.Find("Player").GetComponentInChildren<Animator>(true).gameObject);
            foreach(var script in target.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
            var a=target.GetComponent<Animator>();a.runtimeAnimatorController=null;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(RemoteAttackMotionAssets.MotionPath);
            var bindings=AnimationUtility.GetCurveBindings(clip);
            if(bindings.Any(b=>b.type!=typeof(Animator)||b.propertyName.Contains("TDOF")||b.propertyName.Contains("HandT")||b.propertyName.Contains("FootT")))
                throw new InvalidOperationException("远程普攻仍有源骨骼路径/关节平移/IK锚点");
            foreach(var b in bindings){
                var c=AnimationUtility.GetEditorCurve(clip,b);
                if(c.keys.Any(k=>float.IsNaN(k.value)||float.IsInfinity(k.value)))throw new InvalidOperationException("非法关节曲线");
                if((b.propertyName.Contains("Hand ")||b.propertyName.Contains("Stretched")||b.propertyName.Contains("Spread"))&&c.keys.Any(k=>Mathf.Abs(k.value)>1.0001f))
                    throw new InvalidOperationException("手腕/手指越限 "+b.propertyName);
            }
            if(bindings.Count(b=>b.propertyName.StartsWith("LeftHand.")||b.propertyName.StartsWith("RightHand."))!=40)
                throw new InvalidOperationException("五指曲线缺失");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var play=AnimationClipPlayable.Create(graph,clip);play.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,"Player",a).SetSourcePlayable(play);graph.Play();
            var bones=source.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t);
            Transform S(string name)=>bones["Bip01 "+name];
            Transform T(string name)=>a.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),name));
            foreach(string side in new[]{"Left","Right"}){
                string p=side=="Left"?"L ":"R ";float upper=0,lower=0,palm=0,wrist=0,thigh=0,calf=0,foot=0,torso=0,extraStep=0;
                Quaternion[] priorSource=null,priorTarget=null;
                for(int f=0;f<=600;f++){
                    float t=clip.length*f/600;raw.SampleAnimation(source,t);play.SetTime(t);graph.Evaluate(0);
                    var sf=Frame(S("L UpperArm").position,S("R UpperArm").position,S("Neck").position-S("Pelvis").position);
                    var tf=Frame(T("LeftUpperArm").position,T("RightUpperArm").position,(T("Neck")??T("Head")).position-T("Hips").position);
                    torso=Mathf.Max(torso,Quaternion.Angle(sf,tf));
                    float Err(Vector3 x,Vector3 y)=>Vector3.Angle(Quaternion.Inverse(sf)*x,Quaternion.Inverse(tf)*y);
                    upper=Mathf.Max(upper,Err(S(p+"Forearm").position-S(p+"UpperArm").position,T(side+"LowerArm").position-T(side+"UpperArm").position));
                    lower=Mathf.Max(lower,Err(S(p+"Hand").position-S(p+"Forearm").position,T(side+"Hand").position-T(side+"LowerArm").position));
                    palm=Mathf.Max(palm,Err(Vector3.Cross(S(p+"Finger1").position-S(p+"Hand").position,S(p+"Finger0").position-S(p+"Hand").position),Vector3.Cross(T(side+"IndexProximal").position-T(side+"Hand").position,T(side+"ThumbProximal").position-T(side+"Hand").position)));
                    wrist=Mathf.Max(wrist,Mathf.Abs(Vector3.Angle(S(p+"Hand").position-S(p+"Forearm").position,S(p+"Finger1").position-S(p+"Hand").position)-Vector3.Angle(T(side+"Hand").position-T(side+"LowerArm").position,T(side+"IndexProximal").position-T(side+"Hand").position)));
                    thigh=Mathf.Max(thigh,Err(S(p+"Calf").position-S(p+"Thigh").position,T(side+"LowerLeg").position-T(side+"UpperLeg").position));
                    calf=Mathf.Max(calf,Err(S(p+"Foot").position-S(p+"Calf").position,T(side+"Foot").position-T(side+"LowerLeg").position));
                    foot=Mathf.Max(foot,Err(S(p+"Toe0").position-S(p+"Foot").position,T(side+"Toes").position-T(side+"Foot").position));
                    var sr=new[]{S(p+"UpperArm").rotation,S(p+"Forearm").rotation,S(p+"Hand").rotation,S(p+"Thigh").rotation,S(p+"Calf").rotation};
                    var tr=new[]{T(side+"UpperArm").rotation,T(side+"LowerArm").rotation,T(side+"Hand").rotation,T(side+"UpperLeg").rotation,T(side+"LowerLeg").rotation};
                    if(f>0)for(int i=0;i<sr.Length;i++)extraStep=Mathf.Max(extraStep,Quaternion.Angle(priorTarget[i],tr[i])-Quaternion.Angle(priorSource[i],sr[i]));
                    priorSource=sr;priorTarget=tr;
                }
                Debug.Log("[RemoteAttackMotionChecks] "+side+" max degrees upper="+upper+" forearm="+lower+" palm="+palm+" wrist="+wrist+" thigh="+thigh+" calf="+calf+" foot="+foot+" torso="+torso+" extraStep="+extraStep);
                // The two-finger source and five-finger player have different palm layouts;
                // wrist range limiting also softens the source's strongest right wrist bend.
                if(upper>3||lower>3||palm>8||wrist>12||thigh>2||calf>2||foot>2||torso>2||extraStep>8)
                    throw new InvalidOperationException("远程普攻偏离源动作或出现关节跳转 "+side);
            }
            Debug.Log("REMOTE_NATIVE_POSE_CHECK_PASS Attack1, both arms/legs, 601 samples, 40 finger curves");
        }finally{graph.Destroy();if(source)Object.DestroyImmediate(source);if(target)Object.DestroyImmediate(target);RemoteAttackMotionAssets.DeleteNativeCopy();}
    }
    static Quaternion Frame(Vector3 left,Vector3 right,Vector3 up)=>Quaternion.LookRotation(Vector3.Cross(right-left,up),up);
}
