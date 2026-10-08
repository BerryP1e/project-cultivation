using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>保留实际手部曲线；三指源骨架补全五指目标，并限制人形关节的异常外翻。</summary>
public static class HandMotionRetargeting
{
    public static string CurveName(string muscle)
    {
        var parts=muscle.Split(' ');
        return parts.Length>=3&&(parts[0]=="Left"||parts[0]=="Right")&&new[]{"Thumb","Index","Middle","Ring","Little"}.Contains(parts[1])
            ?parts[0]+"Hand."+parts[1]+"."+string.Join(" ",parts.Skip(2)):muscle;
    }
    public static string MuscleName(string curve)
    {
        if(curve.StartsWith("LeftHand."))return "Left "+curve.Substring(9).Replace('.', ' ');
        if(curve.StartsWith("RightHand."))return "Right "+curve.Substring(10).Replace('.', ' ');
        return curve;
    }
    public static void Apply(AnimationClip clip,Avatar sourceAvatar=null)
    {
        // HumanTrait's UI labels differ from the serialized Animator finger properties.
        // Unknown spaced labels are accepted by SetEditorCurve but never animate a finger.
        foreach(var old in AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Animator)).ToArray()){
            string name=CurveName(old.propertyName);if(name==old.propertyName)continue;
            var binding=EditorCurveBinding.FloatCurve(old.path,typeof(Animator),name);
            if(AnimationUtility.GetEditorCurve(clip,binding)==null)AnimationUtility.SetEditorCurve(clip,binding,AnimationUtility.GetEditorCurve(clip,old));
            AnimationUtility.SetEditorCurve(clip,old,null);
        }
        if(sourceAvatar!=null){
            var mapped=sourceAvatar.humanDescription.human.Select(b=>b.humanName).ToArray();
            foreach(string side in new[]{"Left","Right"})
            foreach(string finger in new[]{"Thumb","Index","Middle","Ring","Little"}){
                // Stylized source hands drive several visible fingers with a single chain.
                // Use that motion for the target's extra fingers instead of leaving them stiff.
                bool missing=!mapped.Contains(side+" "+finger+" Proximal");
                if(missing&&finger!="Thumb"){
                    string fallback=mapped.Contains(side+" Middle Proximal")?"Middle":"Index";
                    if(mapped.Contains(side+" "+fallback+" Proximal"))
                        foreach(string suffix in new[]{"1 Stretched","2 Stretched","3 Stretched","Spread"})Copy(clip,side+" "+fallback+" "+suffix,side+" "+finger+" "+suffix);
                }
                if(!missing&&!mapped.Contains(side+" "+finger+" Distal"))
                    Copy(clip,side+" "+finger+" 2 Stretched",side+" "+finger+" 3 Stretched");
            }
        }
        foreach(var binding in AnimationUtility.GetCurveBindings(clip)){
            if(binding.type!=typeof(Animator))continue;
            string name=binding.propertyName;
            if(!(name.Contains("Hand ")||name.Contains("Stretched")||name.Contains("Spread")))continue;
            var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;
            // Values outside [-1,1] exceed the Avatar's anatomical muscle limits. They are
            // especially visible at the wrist when transferring stylized legacy animations.
            if(!keys.Any(k=>k.value < -1f||k.value>1f))continue;
            for(int i=0;i<keys.Length;i++)keys[i].value=Mathf.Clamp(keys[i].value,-1f,1f);
            var limited=new AnimationCurve(keys){preWrapMode=curve.preWrapMode,postWrapMode=curve.postWrapMode};
            for(int i=0;i<limited.length;i++){
                AnimationUtility.SetKeyLeftTangentMode(limited,i,AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(limited,i,AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip,binding,limited);
        }
    }

    public static Avatar AvatarFor(AnimationClip source)
    {
        var path=AssetDatabase.GetAssetPath(source);
        if(string.IsNullOrEmpty(path)||!(AssetImporter.GetAtPath(path) is ModelImporter importer))return null;
        return importer.sourceAvatar!=null?importer.sourceAvatar:AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a=>a.isValid&&a.isHuman);
    }
    static void Copy(AnimationClip clip,string from,string to)
    {
        var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),CurveName(from)));
        if(curve!=null)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),CurveName(to)),curve);
    }
}
