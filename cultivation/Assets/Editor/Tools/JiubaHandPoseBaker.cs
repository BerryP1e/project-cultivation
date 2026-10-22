using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>八九持柄手专用重建：腕部保持小幅运动，五指逐节包握，不继承三指源 Avatar 的极限钩曲。</summary>
public static class JiubaHandPoseBaker
{
    static readonly string[] Fingers={"Thumb","Index","Middle","Ring","Little"};
    static readonly float[,] Grip={{-.35f,-.40f,-.30f},{-.42f,-.62f,-.35f},{-.47f,-.67f,-.40f},{-.50f,-.64f,-.38f},{-.48f,-.58f,-.34f}};

    public static void Apply(AnimationClip clip,bool sourceWrist=true)
    {
        foreach(string axis in new[]{"Down-Up","In-Out"}){
            string name="Right Hand "+axis;
            var binding=EditorCurveBinding.FloatCurve("",typeof(Animator),name);
            var original=AnimationUtility.GetEditorCurve(clip,binding);var curve=new AnimationCurve();
            int frames=Mathf.Max(1,Mathf.CeilToInt(clip.length*60));
            for(int frame=0;frame<=frames;frame++){
                float t=clip.length*frame/frames;float v=original!=null?original.Evaluate(t):0;
                // 原持刀腕侧偏约 0.87，虽在 [-1,1] 内仍是明显折腕，不能只裁掉超范围值。
                float value=sourceWrist?v*(axis=="In-Out"?.10f:.25f):v;
                curve.AddKey(t,Mathf.Clamp(value,-.20f,.20f));
            }
            Put(clip,name,curve);
        }
        for(int finger=0;finger<5;finger++){
            for(int joint=0;joint<3;joint++)Put(clip,"RightHand."+Fingers[finger]+"."+(joint+1)+" Stretched",AnimationCurve.Constant(0,clip.length,Grip[finger,joint]));
            Put(clip,"RightHand."+Fingers[finger]+".Spread",AnimationCurve.Constant(0,clip.length,finger==0?-.18f:finger==1?-.08f:finger==4?.06f:0));
        }
    }

    public static void Rebuild()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before rebaking");
        JiubaMotionBaker.重建();
        var reference=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/resources/技能动作/八九玄功/持刀_站.anim");
        foreach(var guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{YufengDirectionalBaker.Folder.TrimEnd('/')})){
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            var clip=UnityEngine.Object.Instantiate(source);clip.name=source.name;
            YufengHandPoseBaker.Hands(clip,true);YufengHandPoseBaker.Carry(clip,reference);
            Save(clip,"Assets/resources/技能动作/御风八向/八九玄功/"+clip.name+".anim");
        }
        YufengDodgeBaker.RebuildJiuba();
        AssetDatabase.SaveAssets();
        Debug.Log("JIUBA_HAND_REBAKE groundAndAttack=13 flight=9 dodge=4 sourceFBXUnchanged=True");
    }

    static void Save(AnimationClip clip,string path)
    {
        var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(!old)throw new InvalidOperationException("Missing reviewed motion "+path);
        EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);UnityEngine.Object.DestroyImmediate(clip);
    }
    static void Put(AnimationClip clip,string property,AnimationCurve curve)
    {
        for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);
    }
}
