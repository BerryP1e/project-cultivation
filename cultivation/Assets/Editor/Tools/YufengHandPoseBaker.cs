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
        JiubaHandPoseBaker.Apply(clip,false);
        JiubaCarryPoseBaker.Apply(clip);
    }

    public static void SpiritSwordCarry(AnimationClip clip,AnimationClip reference)
    {
        SpiritSwordFlightGripBaker.Apply(clip,reference);
    }

    static void Put(AnimationClip clip,string property,AnimationCurve curve)
    {
        for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);
    }
}
