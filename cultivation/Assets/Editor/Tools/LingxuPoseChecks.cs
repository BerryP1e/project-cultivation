using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

/// <summary>直接采样原 Legacy 模型，对比玩家的手臂方向、腕角和掌面；不依赖烘焙器。</summary>
public static class LingxuPoseChecks
{
    [MenuItem("修仙/调试/验证灵虚原动画与玩家手臂（不动场景）")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        const string path="Assets/resources/一些第三方资源/法师/";
        var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path+"FB0005Boss.FBX"));
        source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var target=Object.Instantiate(GameObject.Find("Player").GetComponentInChildren<Animator>(true).gameObject);
        foreach(var script in target.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
        target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
        var a=target.GetComponent<Animator>();a.runtimeAnimatorController=null;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var sourceBones=source.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name,t=>t);
        Transform S(string name)=>sourceBones["Bip001 "+name];
        var graph=PlayableGraph.Create("LingxuSourcePoseCheck");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        try{
            foreach(var pair in new[]{("Ready","持剑_Idle"),("Run","持剑_Run"),("AttackA","A1"),("AttackB","A2"),("SkillA","A3")}){
                var raw=AssetDatabase.LoadAllAssetsAtPath(path+"FB0005Boss@"+pair.Item1+".FBX").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                var clip=Resources.Load<AnimationClip>("技能动作/灵虚剑决/"+pair.Item2);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
                var output=AnimationPlayableOutput.Create(graph,"Player",a);output.SetSourcePlayable(playable);graph.Play();
                foreach(string side in new[]{"Left","Right"}){
                    string p=side=="Left"?"L ":"R ";
                    Transform T(string part)=>a.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),side+part));
                    var su=S(p+"UpperArm");var sl=S(p+"Forearm");var sh=S(p+"Hand");var sm=S(p+"Finger2");var st=S(p+"Finger0");
                    var tu=T("UpperArm");var tl=T("LowerArm");var th=T("Hand");var tm=T("MiddleProximal");var tt=T("ThumbProximal");
                    float upper=0,lower=0,palm=0,wrist=0,extraStep=0;Quaternion[] priorSource=null,priorTarget=null;
                    const int samples=600;
                    for(int f=0;f<=samples;f++){
                        float time=raw.length*f/samples;raw.SampleAnimation(source,time);playable.SetTime(time);graph.Evaluate(0);
                        var sf=Frame(S("L UpperArm").position,S("R UpperArm").position,S("Neck").position-S("Pelvis").position);
                        var tf=Frame(a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position,a.GetBoneTransform(HumanBodyBones.RightUpperArm).position,(a.GetBoneTransform(HumanBodyBones.Neck)??a.GetBoneTransform(HumanBodyBones.Head)).position-a.GetBoneTransform(HumanBodyBones.Hips).position);
                        upper=Mathf.Max(upper,Vector3.Angle(Quaternion.Inverse(sf)*(sl.position-su.position),Quaternion.Inverse(tf)*(tl.position-tu.position)));
                        lower=Mathf.Max(lower,Vector3.Angle(Quaternion.Inverse(sf)*(sh.position-sl.position),Quaternion.Inverse(tf)*(th.position-tl.position)));
                        palm=Mathf.Max(palm,Vector3.Angle(Quaternion.Inverse(sf)*Vector3.Cross(sm.position-sh.position,st.position-sh.position),Quaternion.Inverse(tf)*Vector3.Cross(tm.position-th.position,tt.position-th.position)));
                        wrist=Mathf.Max(wrist,Mathf.Abs(Vector3.Angle(sh.position-sl.position,sm.position-sh.position)-Vector3.Angle(th.position-tl.position,tm.position-th.position)));
                        var sr=new[]{su.rotation,sl.rotation,sh.rotation};var tr=new[]{tu.rotation,tl.rotation,th.rotation};
                        if(f>0)for(int i=0;i<3;i++)extraStep=Mathf.Max(extraStep,Quaternion.Angle(priorTarget[i],tr[i])-Quaternion.Angle(priorSource[i],sr[i]));
                        priorSource=sr;priorTarget=tr;
                    }
                    Debug.Log("[LingxuPoseChecks] "+pair.Item1+" "+side+" max degrees: upper="+upper+" forearm="+lower+" palm="+palm+" wrist="+wrist+" extra rotation step="+extraStep);
                    // The target's wrist limits deliberately soften the source's most extreme
                    // bends. Arm direction and interpolation must still follow the native pose.
                    if(upper>5||lower>6||palm>23||wrist>21||extraStep>8)throw new InvalidOperationException("灵虚手臂偏离源动画或出现插值扭转 "+pair.Item1+" "+side);
                }
                graph.DestroyOutput(output);graph.DestroyPlayable(playable);
            }
            Debug.Log("LINGXU_SOURCE_POSE_CHECK_PASS five clips, both arms, 601 samples each");
        }finally{graph.Destroy();Object.DestroyImmediate(source);Object.DestroyImmediate(target);}
    }
    static Quaternion Frame(Vector3 left,Vector3 right,Vector3 up)=>Quaternion.LookRotation(Vector3.Cross(right-left,up),up);
}
