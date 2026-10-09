using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>在真实玩家 Avatar 上检查八向选择、循环、持刀方向和脚下御剑。</summary>
public static class YufengDirectionalChecks
{
    [MenuItem("修仙/调试/验证八向御风（不动场景）")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止 Play");
        var target=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/better_player_test/better_player_test.fbx"));
        target.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);target.transform.localScale=Vector3.one;
        var animator=target.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        var sword=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/Weapons/灵虚剑决/灵虚剑.prefab"),target.transform);sword.name="灵虚剑";animator.Rebind();
        var node=sword.transform.Find("LingxuSword");var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
        var swordBounds=node.GetComponent<SkinnedMeshRenderer>().sharedMesh.bounds;
        Quaternion neutralHead;
        using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
        {
            var neutral=new HumanPose{bodyPosition=Vector3.up,bodyRotation=Quaternion.identity,muscles=new float[HumanTrait.MuscleCount]};
            handler.SetHumanPose(ref neutral);neutralHead=animator.GetBoneTransform(HumanBodyBones.Head).rotation;
        }
        var scenePlayer=GameObject.Find("Player");var sceneMovement=scenePlayer?scenePlayer.GetComponent<PlayerController>():null;
        float visualOffset=sceneMovement?sceneMovement.visualYawOffset:0f;
        var bones=Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>().Where(b=>b<HumanBodyBones.LastBone).Select(b=>animator.GetBoneTransform(b)).Where(b=>b).ToArray();
        var graph=PlayableGraph.Create("YufengChecks");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var carriedReference=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/resources/技能动作/八九玄功/持刀_站.anim");
        var ground=AnimationClipPlayable.Create(graph,carriedReference);ground.SetApplyFootIK(false);
        var groundOutput=AnimationPlayableOutput.Create(graph,"Ground",animator);groundOutput.SetSourcePlayable(ground);graph.Play();ground.SetTime(0);graph.Evaluate(0);
        var groundHand=hand.rotation;graph.DestroyOutput(groundOutput);graph.DestroyPlayable(ground);
        var unchanged=new[]{HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg}.Select(b=>animator.GetBoneTransform(b)).ToArray();
        var baselines=new System.Collections.Generic.Dictionary<string,Vector3[][]>();
        int count=0,facingCases=0;float maxLoopGap=0,maxLoopAngle=0,maxSwordAngle=0,maxFacingError=0,maxCarryAngle=0,maxBodyGap=0,minSwordClearance=float.MaxValue;
        try
        {
            var folders=new[]{YufengDirectionalBaker.Folder,"Assets/resources/技能动作/御风八向/八九玄功/","Assets/resources/技能动作/御风八向/太虚剑决/","Assets/resources/技能动作/御风八向/灵虚剑决/"};
            foreach(string folder in folders)foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{folder.TrimEnd('/')}))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                Require(clip.humanMotion&&clip.isLooping,"非人形循环 "+clip.name);
                Require(AnimationUtility.GetCurveBindings(clip).Count(b=>b.type==typeof(Animator)&&(b.propertyName.Contains("Hand.Thumb")||b.propertyName.Contains("Hand.Index")||b.propertyName.Contains("Hand.Middle")||b.propertyName.Contains("Hand.Ring")||b.propertyName.Contains("Hand.Little")))==40,"手指曲线缺失 "+clip.name);
                foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(Animator)&&b.propertyName.StartsWith("LeftHand.")||b.type==typeof(Animator)&&b.propertyName.StartsWith("RightHand.")))
                    Require(AnimationUtility.GetEditorCurve(clip,binding).keys.All(k=>Mathf.Abs(k.value)<.85f),"御风手指极端钩曲/张开 "+clip.name+" "+binding.propertyName);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
                var output=AnimationPlayableOutput.Create(graph,"Pose",animator);output.SetSourcePlayable(playable);graph.Play();
                Vector3[] first=null;Quaternion[] firstRot=null;
                if(folder==YufengDirectionalBaker.Folder)baselines[clip.name]=new Vector3[121][];
                for(int frame=0;frame<=120;frame++)
                {
                    playable.SetTime(frame==120?clip.length-.0001f:clip.length*frame/120);graph.Evaluate(0);
                    Require(target.transform.position.sqrMagnitude<.000001f && Quaternion.Angle(target.transform.rotation,Quaternion.identity)<.001f,"根节点被动画驱动 "+clip.name);
                    foreach(var bone in bones)Require(!float.IsNaN(bone.position.x)&&!float.IsNaN(bone.localRotation.w),"关节出现非法值 "+clip.name);
                    if(frame==0){first=bones.Select(b=>b.position).ToArray();firstRot=bones.Select(b=>b.rotation).ToArray();}
                    if(folder==YufengDirectionalBaker.Folder)baselines[clip.name][frame]=unchanged.Select(b=>b.position).ToArray();
                    if(folder.Contains("八九玄功"))
                    {
                        maxCarryAngle=Mathf.Max(maxCarryAngle,Quaternion.Angle(groundHand,hand.rotation));
                        for(int b=0;b<unchanged.Length;b++)maxBodyGap=Mathf.Max(maxBodyGap,Vector3.Distance(baselines[clip.name][frame][b],unchanged[b].position));
                    }
                    if(folder.Contains("灵虚剑决"))
                    {
                        maxSwordAngle=Mathf.Max(maxSwordAngle,Vector3.Angle(node.forward,target.transform.forward));
                        float feet=Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                        float top=node.TransformPoint(new Vector3(swordBounds.center.x,swordBounds.max.y,swordBounds.center.z)).y;
                        minSwordClearance=Mathf.Min(minSwordClearance,feet-top);
                    }
                    if(frame==120)for(int i=0;i<bones.Length;i++){maxLoopGap=Mathf.Max(maxLoopGap,Vector3.Distance(first[i],bones[i].position));maxLoopAngle=Mathf.Max(maxLoopAngle,Quaternion.Angle(firstRot[i],bones[i].rotation));}
                }
                if(clip.name=="御风八向_悬停")
                {
                    // 使用场景真正序列化的补偿与实际头部正面，不能只验证参数/骨架相对倾斜。
                    foreach(float yaw in new[]{0f,90f,180f,270f})
                    {
                        target.transform.rotation=Quaternion.Euler(0,yaw+visualOffset,0);
                        playable.SetTime(.4f);graph.Evaluate(0);
                        var front=animator.GetBoneTransform(HumanBodyBones.Head).rotation*Quaternion.Inverse(neutralHead)*Vector3.forward;
                        front.y=0;
                        float error=Vector3.Angle(front,Quaternion.Euler(0,yaw,0)*Vector3.forward);
                        maxFacingError=Mathf.Max(maxFacingError,error);facingCases++;
                        Require(error<5f,"御风视觉朝向偏离锁定方向 "+folder+" yaw="+yaw+" error="+error+" sceneOffset="+visualOffset);
                    }
                    target.transform.rotation=Quaternion.identity;
                }
                else if(folder==YufengDirectionalBaker.Folder)
                {
                    playable.SetTime(.4f);graph.Evaluate(0);var torso=animator.GetBoneTransform(HumanBodyBones.Head).position-animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    if(clip.name=="御风八向_前")Require(torso.z>.08f,"前进姿态未朝玩家 +Z 正面倾身");
                    if(clip.name=="御风八向_后")Require(torso.z<-.04f,"后退姿态方向不对");
                    if(clip.name=="御风八向_左")Require(torso.x<-.025f,"左移姿态方向不对");
                    if(clip.name=="御风八向_右")Require(torso.x>.025f,"右移姿态方向不对");
                }
                graph.DestroyOutput(output);graph.DestroyPlayable(playable);count++;
            }
            Require(count==36,"预期36段动作，实际"+count);
            Require(maxLoopGap<.012f && maxLoopAngle<3f,"循环接缝过大 "+maxLoopGap+"m / "+maxLoopAngle+"deg");
            Require(maxSwordAngle<.1f && minSwordClearance>.06f,"御剑方向/脚下间隙错误 "+maxSwordAngle+"deg / "+minSwordClearance+"m");
            Require(maxCarryAngle<3f,"持刀方向偏离地面参考 "+maxCarryAngle+"deg");
            Require(maxBodyGap<.003f,"校准持刀影响了躯干/腿部/左臂 "+maxBodyGap+"m");
            graph.Destroy();
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/PlayerLocomotion.controller");
            var tree=controller.layers[0].stateMachine.states.First(s=>s.state.name=="Yufeng_Forward").state.motion as BlendTree;
            Require(tree && tree.blendType==BlendTreeType.FreeformDirectional2D && tree.children.Length==9,"八向混合树不完整");
            animator.runtimeAnimatorController=controller;animator.Rebind();animator.SetBool("Flying",true);animator.SetBool("FlyMoving",true);
            foreach(var child in tree.children)
            {
                animator.SetFloat("FlyX",child.position.x);animator.SetFloat("FlyY",child.position.y);animator.Play("Yufeng_Forward",0,.3f);animator.Update(0);
                var weights=animator.GetCurrentAnimatorClipInfo(0);Require(weights.Any(w=>w.clip==child.motion && w.weight>.995f),"八向权重不正确 "+child.motion.name+" "+string.Join(",",weights.Select(w=>w.clip.name+":"+w.weight)));
            }
            Debug.Log("YUFENG_EIGHT_DIRECTION_PASS clips="+count+" samples="+(count*121)+" loopGap="+maxLoopGap.ToString("F5")+"m loopAngle="+maxLoopAngle.ToString("F3")+" swordAngle="+maxSwordAngle.ToString("F3")+" swordClearance="+minSwordClearance.ToString("F5")+"m carryAngle="+maxCarryAngle.ToString("F3")+"deg bodyGap="+maxBodyGap.ToString("F5")+"m directions=8+idle facingCases="+facingCases+" maxHeadFacingError="+maxFacingError.ToString("F3")+"deg sceneOffset="+visualOffset);
        }
        finally{if(graph.IsValid())graph.Destroy();Object.DestroyImmediate(target);}
    }
    static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
}
