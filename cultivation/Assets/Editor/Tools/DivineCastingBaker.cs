using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>全状态神通施法动作，保留源朝向与既有根高度，不改源FBX/场景。</summary>
public static class DivineCastingBaker
{
    [MenuItem("修仙/动画/重建神通施法（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请停止Play后烘焙");
        Bake("attack_FlyingAtk02","水龙炮_施法");
        Bake("attack_FlyingAtk01_short","追踪弹_施法");
        AssetDatabase.SaveAssets();
        Debug.Log("[神通施法] 全状态Attack02水龙炮43%左手出水；Attack01_short追踪弹0~50%右手凝聚、58%放飞；非循环");
    }

    static void Bake(string sourceName,string outputName)
    {
        const string folder="Assets/resources/Animation Library/Flying_Mage_Volume2/Animations/Humanoid/";
        var source=AssetDatabase.LoadAllAssetsAtPath(folder+sourceName+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        var idle=AssetDatabase.LoadAllAssetsAtPath(folder+"Inplace/idle_Flying_inplace.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        var clip=UnityEngine.Object.Instantiate(source);clip.name=outputName;clip.wrapMode=WrapMode.Once;
        var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),"RootT.y");
        var reference=AnimationUtility.GetEditorCurve(idle,binding);
        float height=Enumerable.Range(0,61).Average(i=>reference.Evaluate(idle.length*i/60f));
        var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;
        for(int i=0;i<keys.Length;i++)keys[i].value+=.946f-height;
        curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=false;settings.loopBlend=false;
        settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);
        // 施法需要源动作的腕指手势；不能套待机/持械的固定屈指与缩腕规则。
        HandMotionRetargeting.Apply(clip,HandMotionRetargeting.AvatarFor(source));
        clip.EnsureQuaternionContinuity();
        const string targetFolder="Assets/resources/技能动作/神通施法";
        if(!AssetDatabase.IsValidFolder(targetFolder))AssetDatabase.CreateFolder("Assets/resources/技能动作","神通施法");
        string path=targetFolder+"/"+outputName+".anim";var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(old){EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(clip);}
        else AssetDatabase.CreateAsset(clip,path);
    }
}
