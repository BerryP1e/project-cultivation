using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>制作舒展滑行循环；保留片段GUID、朝向、根高度与持械上身，不保存场景。</summary>
public static class YufengGlideBaker
{
    const string Path="Assets/Animations/凭虚御风/御风_前进.anim";
    const string Weapon="Assets/resources/技能动作/八九玄功/持刀_御风前进.anim";
    const float Duration=3.2f;
    [MenuItem("修仙/动画/制作飘逸御风前进（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
        if(old==null)throw new InvalidOperationException("Missing flight source");
        // 悬停是稳定的永久基准，重复烘焙不会累加上次的动作偏移。
        var basis=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/凭虚御风/御风_Idle.anim");
        var source=AnimationUtility.GetCurveBindings(basis).Where(b=>b.type==typeof(Animator)).ToDictionary(b=>b.propertyName,b=>AnimationUtility.GetEditorCurve(basis,b).Evaluate(0));
        var moving=AnimationUtility.GetCurveBindings(old).Where(b=>b.type==typeof(Animator)).ToDictionary(b=>b.propertyName,b=>AnimationUtility.GetEditorCurve(old,b).Evaluate(0));
        var standing=AssetDatabase.LoadAllAssetsAtPath("Assets/resources/Animation Library/1/UAL1_Standard.fbx").OfType<AnimationClip>().First(c=>c.name=="Armature|Idle_Loop");
        var neutral=AnimationUtility.GetCurveBindings(standing).Where(b=>b.type==typeof(Animator)).ToDictionary(b=>b.propertyName,b=>AnimationUtility.GetEditorCurve(standing,b).Evaluate(0));
        float Value(string name)=>source.TryGetValue(HandMotionRetargeting.CurveName(name),out var v)?v:0;
        var clip=new AnimationClip{name=old.name,frameRate=30};
        const int frames=96;
        var curves=new Dictionary<string,AnimationCurve>();
        var root=new Quaternion(moving["RootQ.x"],moving["RootQ.y"],moving["RootQ.z"],moving["RootQ.w"]).normalized;
        for(int f=0;f<=frames;f++)
        {
            float time=Duration*f/frames,phase=time/Duration*Mathf.PI*2;
            void Put(string name,float value){if(!curves.TryGetValue(name,out var c))curves[name]=c=new AnimationCurve();c.AddKey(time,value);}
            foreach(string name in HumanTrait.MuscleName)Put(name,Value(name));
            void Set(string name,float value){var c=curves[name];var keys=c.keys;keys[keys.Length-1].value=Mathf.Clamp(value,-1,1);c.keys=keys;}
            Set("Spine Front-Back",.12f+.022f*Mathf.Sin(phase));
            Set("Chest Front-Back",-.07f+.018f*Mathf.Sin(phase-.4f));
            Set("UpperChest Front-Back",.035f+.012f*Mathf.Sin(phase-.6f));
            Set("Spine Left-Right",.016f*Mathf.Sin(phase));
            Set("Chest Twist Left-Right",.018f*Mathf.Sin(phase-.3f));
            Set("Neck Nod Down-Up",Value("Neck Nod Down-Up")+.015f*Mathf.Sin(phase-.7f));
            for(int side=0;side<2;side++)
            {
                string prefix=side==0?"Left ":"Right ";float lag=side==0?0:.45f;
                Set(prefix+"Shoulder Down-Up",-.12f+.015f*Mathf.Sin(phase-lag));
                Set(prefix+"Arm Twist In-Out",neutral[prefix+"Arm Twist In-Out"]);
                Set(prefix+"Forearm Twist In-Out",neutral[prefix+"Forearm Twist In-Out"]);
                Set(prefix+"Hand In-Out",neutral[prefix+"Hand In-Out"]*.5f);
                Set(prefix+"Arm Down-Up",-.43f+.045f*Mathf.Sin(phase-lag));
                Set(prefix+"Arm Front-Back",.51f+.065f*Mathf.Sin(phase-lag-.3f));
                Set(prefix+"Forearm Stretch",(side==0?.73f:.79f)+.02f*Mathf.Sin(phase-lag-.6f));
                Set(prefix+"Hand Down-Up",neutral[prefix+"Hand Down-Up"]+.045f*Mathf.Sin(phase-lag-.9f));
                Set(prefix+"Upper Leg Front-Back",(side==0?.40f:.49f)+.04f*Mathf.Sin(phase-.6f));
                Set(prefix+"Upper Leg In-Out",Value(prefix+"Upper Leg In-Out")*.5f);
                Set(prefix+"Lower Leg Stretch",(side==0?.58f:.84f)+.045f*Mathf.Sin(phase-.9f));
                Set(prefix+"Foot Up-Down",Value(prefix+"Foot Up-Down")-.10f+.035f*Mathf.Sin(phase-1.15f));
            }
            // 根高度固定；浮动仅来自小幅骨骼姿态，避免抽动、穿模与移动逻辑抢高度。
            Put("RootT.x",moving["RootT.x"]);Put("RootT.y",moving["RootT.y"]);Put("RootT.z",moving["RootT.z"]);
            var q=Quaternion.Euler(7+Mathf.Sin(phase)*1.2f,root.eulerAngles.y,Mathf.Sin(phase-.3f)*.7f);
            Put("RootQ.x",q.x);Put("RootQ.y",q.y);Put("RootQ.z",q.z);Put("RootQ.w",q.w);
        }
        foreach(var pair in curves)
        {
            var keys=pair.Value.keys;
            if(keys.Max(k=>k.value)-keys.Min(k=>k.value)<.000001f)
            {AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HandMotionRetargeting.CurveName(pair.Key)),AnimationCurve.Constant(0,Duration,keys[0].value));continue;}
            for(int i=0;i<keys.Length;i++){float slope=i==0||i==frames?(keys[1].value-keys[frames-1].value)/(2*Duration/frames):(keys[i+1].value-keys[i-1].value)/(2*Duration/frames);keys[i].inTangent=keys[i].outTangent=slope;}
            pair.Value.keys=keys;AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),HandMotionRetargeting.CurveName(pair.Key)),pair.Value);
        }
        var settings=AnimationUtility.GetAnimationClipSettings(old);settings.loopTime=true;settings.stopTime=Duration;settings.keepOriginalPositionY=true;settings.loopBlendPositionY=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
        EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(clip);
        var weapon=AssetDatabase.LoadAssetAtPath<AnimationClip>(Weapon);
        if(weapon!=null)
        {
            // 保留杨戬持械上身，以新滑行下身替换旧腿部与根姿态。
            float height=AnimationUtility.GetEditorCurve(weapon,EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y")).Evaluate(0);
            var result=UnityEngine.Object.Instantiate(weapon);
            result.name="持刀_御风前进";
            foreach(var binding in AnimationUtility.GetCurveBindings(result))
            {
                string p=binding.propertyName;
                if(binding.type!=typeof(Animator))continue;
                if(p.StartsWith("Root")||p.Contains("Leg")||p.Contains("Foot")||p.Contains("Toes"))AnimationUtility.SetEditorCurve(result,binding,AnimationUtility.GetEditorCurve(old,binding));
                else {var c=AnimationUtility.GetEditorCurve(result,binding);AnimationUtility.SetEditorCurve(result,binding,AnimationCurve.Constant(0,Duration,c.Evaluate(0)));}
            }
            AnimationUtility.SetEditorCurve(result,EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y"),AnimationCurve.Constant(0,Duration,height));
            AnimationUtility.SetAnimationClipSettings(result,settings);EditorUtility.CopySerialized(result,weapon);EditorUtility.SetDirty(weapon);UnityEngine.Object.DestroyImmediate(result);
        }
        AssetDatabase.SaveAssets();Debug.Log("[YufengGlide] 3.2s authored cycle + steady weapon variant; GUIDs retained; scenes untouched");
    }
}
