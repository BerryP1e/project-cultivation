using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>地面方向动作复用 Knight 原地步态，保留前进片段和既有御风控制器。</summary>
public static class GroundDirectionalBaker
{
    const string Source="Assets/resources/Animation Library/Magical-Knight_Set/Animation/Humanoid/inplace/";
    const string Folder="Assets/Animations/地面八向/";
    static readonly string[] Names={"前","右前","右","右后","后","左后","左","左前"};
    static readonly string[] Suffix={"front","frontR45","right","backR45","back","backL45","left","frontL45"};
    static readonly Vector2[] Directions={Vector2.up,new Vector2(.7071068f,.7071068f),Vector2.right,new Vector2(.7071068f,-.7071068f),Vector2.down,new Vector2(-.7071068f,-.7071068f),Vector2.left,new Vector2(-.7071068f,.7071068f)};
    [MenuItem("修仙/动画/重建地面八向（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/PlayerLocomotion.controller");
        var state=controller.layers[0].stateMachine.states.Select(s=>s.state).First(s=>s.name=="Locomotion");
        var main=state.motion as BlendTree;if(!main)throw new InvalidOperationException("Locomotion tree missing");
        foreach(string parameter in new[]{"MoveX","MoveY"})if(!controller.parameters.Any(p=>p.name==parameter))controller.AddParameter(parameter,AnimatorControllerParameterType.Float);
        var children=main.children;
        for(int speed=0;speed<2;speed++){
            var original=children[speed+1].motion;
            var forward=original is BlendTree old?old.children[0].motion:original;
            var tree=original as BlendTree;
            if(!tree){tree=new BlendTree{name=speed==0?"地面八向行走":"地面八向奔跑"};AssetDatabase.AddObjectToAsset(tree,controller);}
            tree.blendType=BlendTreeType.SimpleDirectional2D;tree.blendParameter="MoveX";tree.blendParameterY="MoveY";tree.useAutomaticThresholds=false;
            var directional=new ChildMotion[8];
            for(int i=0;i<8;i++){
                string name="地面八向_"+(speed==0?"走_":"跑_")+Names[i];
                AnimationClip source=AssetDatabase.LoadAllAssetsAtPath(Source+"strafe_"+(speed==0?"walk":"run")+"_strafe_"+Suffix[i]+"_inplace.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                AnimationClip basis=Object.Instantiate(source);basis.name=name;BakeRoot(basis);basis=Save(basis,Folder+name+".anim");
                directional[i]=new ChildMotion{motion=i==0?forward:basis,position=Directions[i],timeScale=1};
                var carried=Object.Instantiate(i==0?Resources.Load<AnimationClip>("技能动作/八九玄功/持刀_"+(speed==0?"走":"跑")):basis);carried.name=name;
                if(i>0){JiubaHandPoseBaker.Apply(carried);JiubaCarryPoseBaker.Apply(carried);}
                BakeRoot(carried);Save(carried,"Assets/resources/技能动作/地面八向/八九玄功/"+name+".anim");
            }
            tree.children=directional;children[speed+1].motion=tree;EditorUtility.SetDirty(tree);
        }
        main.children=children;EditorUtility.SetDirty(main);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        Debug.Log("[GroundDirectional] Knight地面八向与持戟变体已安装，未改动场景与御风状态");
    }
    static void BakeRoot(AnimationClip clip)
    {
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;
        settings.loopBlendOrientation=true;settings.loopBlendPositionXZ=true;settings.loopBlendPositionY=true;
        settings.keepOriginalOrientation=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalPositionY=true;
        AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
    }
    static AnimationClip Save(AnimationClip clip,string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(clip,path);return clip;
    }
}
