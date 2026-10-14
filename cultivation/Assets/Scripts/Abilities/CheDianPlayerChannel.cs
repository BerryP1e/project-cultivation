using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>A3 upper-body channel over the live locomotion pose, synchronized to each dragon spray.</summary>
public sealed class CheDianPlayerChannel : IDisposable
{
    readonly CheDianYeTingCompanion owner;
    readonly CheDianYeTingDefinition definition;
    readonly Animator previewAnimator;
    PlayerAnimationController controller;
    Animator target;
    GameObject sampleRoot;
    PlayableGraph graph;
    AnimationClipPlayable motion;
    Transform[] targetBones, sampleBones;
    float[] weights;
    float age, shotAge=-1, strength, sampleTime;
    Transform spine, leftArm, rightArm, leftHand;
    Vector3 aimPoint;
    bool charging, repeating, held;
    public bool Active => held;
    public float SampleTime => sampleTime;
    public int Recoils { get; private set; }
    public void AimAt(Vector3 point){aimPoint=point;}
    public CheDianPlayerChannel(CheDianYeTingCompanion owner){this.owner=owner;definition=owner.Definition;}
    public CheDianPlayerChannel(Animator previewAnimator,CheDianYeTingDefinition definition){this.previewAnimator=previewAnimator;this.definition=definition;}
    public bool Begin()
    {
        if(held)return true;
        controller=owner?owner.GetComponent<PlayerAnimationController>():null;
        if(controller&&!controller.占用施法(owner))return false;
        if(owner){owner.GetComponent<BasicRemoteAttack01>()?.中断未出手普攻();owner.GetComponent<BasicJiuba01>()?.中断未出手普攻();}
        if(controller)
        {
            controller.停止动作();
            var animator=controller.animator;var flight=owner.GetComponent<YufengFlight>();
            string state=flight&&flight.御风流程中?"Yufeng_Idle":"Locomotion";
            if(animator&&animator.HasState(0,Animator.StringToHash(state)))animator.CrossFadeInFixedTime(state,.25f);
        }
        held=true;age=0;shotAge=-1;charging=true;repeating=false;EnsureRig();return true;
    }
    public void Prepare(bool repeat){charging=true;repeating=repeat;age=0;shotAge=-1;}
    public void Fire(){charging=false;shotAge=0;Recoils++;}
    public void Hold(){charging=false;shotAge=-1;}
    public void End(){held=false;controller?.释放施法(owner);}
    void EnsureRig()
    {
        var next=controller?controller.animator:previewAnimator;
        if(target==next&&sampleRoot)return;
        ClearRig();target=next;
        var clip=Resources.Load<AnimationClip>("技能动作/玄霄雷诀/A3");
        if(!target||!target.isHuman||!target.avatar||!clip)return;
        // Only copy transforms: no meshes, colliders, behaviours, or their Awake callbacks.
        Transform Copy(Transform source,Transform parent)
        {
            var node=new GameObject(source.name).transform;node.SetParent(parent,false);
            node.localPosition=source.localPosition;node.localRotation=source.localRotation;node.localScale=source.localScale;
            for(int i=0;i<source.childCount;i++)Copy(source.GetChild(i),node);
            return node;
        }
        sampleRoot=Copy(target.transform,null).gameObject;sampleRoot.name="掣电烨霆_A3骨架取样";sampleRoot.hideFlags=HideFlags.HideAndDontSave;
        var animator=sampleRoot.AddComponent<Animator>();animator.avatar=target.avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();
        var targets=new List<Transform>();var samples=new List<Transform>();var blend=new List<float>();
        for(int i=0;i<(int)HumanBodyBones.LastBone;i++)
        {
            var bone=(HumanBodyBones)i;float weight=Weight(bone);if(weight<=0)continue;
            var a=target.GetBoneTransform(bone);var b=animator.GetBoneTransform(bone);
            if(a&&b){targets.Add(a);samples.Add(b);blend.Add(weight);}
        }
        targetBones=targets.ToArray();sampleBones=samples.ToArray();weights=blend.ToArray();
        spine=target.GetBoneTransform(HumanBodyBones.Spine)??target.GetBoneTransform(HumanBodyBones.Chest);
        leftArm=target.GetBoneTransform(HumanBodyBones.LeftUpperArm);rightArm=target.GetBoneTransform(HumanBodyBones.RightUpperArm);leftHand=target.GetBoneTransform(HumanBodyBones.LeftHand);
        graph=PlayableGraph.Create("CheDianPlayerA3");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        motion=AnimationClipPlayable.Create(graph,clip);motion.SetApplyFootIK(false);motion.SetApplyPlayableIK(false);
        AnimationPlayableOutput.Create(graph,"A3",animator).SetSourcePlayable(motion);graph.Play();
    }
    static float Weight(HumanBodyBones bone)
    {
        if(bone==HumanBodyBones.Hips)return .25f;
        if(bone==HumanBodyBones.Spine||bone==HumanBodyBones.Chest||bone==HumanBodyBones.UpperChest)return .68f;
        if(bone==HumanBodyBones.Neck||bone==HumanBodyBones.Head)return .55f;
        if(bone==HumanBodyBones.LeftShoulder||bone==HumanBodyBones.LeftUpperArm||bone==HumanBodyBones.LeftLowerArm||bone==HumanBodyBones.LeftHand)return 1;
        if(bone>=HumanBodyBones.LeftThumbProximal&&bone<=HumanBodyBones.LeftLittleDistal)return 1;
        if(bone==HumanBodyBones.RightShoulder||bone==HumanBodyBones.RightUpperArm||bone==HumanBodyBones.RightLowerArm||bone==HumanBodyBones.RightHand)return .55f;
        return 0;
    }
    public void Tick(float dt)
    {
        if(dt<=0)return;
        if(held)EnsureRig();
        strength=Mathf.MoveTowards(strength,held?1:0,dt/(held?.22f:.65f));
        if(!graph.IsValid()||!target||strength<=0){if(!held&&sampleRoot){sampleRoot.SetActive(false);graph.Stop();}return;}
        sampleRoot.SetActive(true);graph.Play();age+=dt;
        if(charging)sampleTime=repeating?Mathf.Lerp(.52f,.64f,Mathf.Clamp01(age/definition.RepeatWindupSeconds)):Mathf.Lerp(0,.64f,Mathf.Clamp01(age/definition.FirstWindupSeconds));
        else if(shotAge>=0)
        {
            shotAge+=dt;
            // Replay the original backward impulse; omit the lowered-arm recovery.
            sampleTime=shotAge<.22f?Mathf.Lerp(.64f,1f,shotAge/.22f):shotAge<.52f?Mathf.Lerp(1f,.60f,Mathf.SmoothStep(0,1,(shotAge-.22f)/.30f)):.60f+Mathf.Sin(age*2.4f)*.025f;
        }
        else sampleTime=.60f+Mathf.Sin(age*2.4f)*.025f;
        motion.SetTime(sampleTime);graph.Evaluate(0);
        var player=controller?controller.playerController:null;bool moving=player&&player.CurrentSpeed>.15f;
        for(int i=0;i<targetBones.Length;i++)if(targetBones[i]&&sampleBones[i])
        {
            float weight=weights[i]*strength;if(moving&&weights[i]<1)weight*=.8f;
            targetBones[i].localRotation=Quaternion.Slerp(targetBones[i].localRotation,sampleBones[i].localRotation,weight);
        }
        if(held&&owner&&owner.Breath.Busy)aimPoint=owner.Breath.AimPoint;
        if(spine&&leftArm&&rightArm&&leftHand)
        {
            var center=(leftArm.position+rightArm.position)*.5f;
            var forward=Vector3.Cross(rightArm.position-leftArm.position,Vector3.up);forward.y=0;
            var toward=aimPoint-center;toward.y=0;
            if(forward.sqrMagnitude>.001f&&toward.sqrMagnitude>.001f)
                spine.rotation=Quaternion.AngleAxis(Vector3.SignedAngle(forward,toward,Vector3.up)*strength,Vector3.up)*spine.rotation;
            var armRay=leftHand.position-leftArm.position;var goal=aimPoint-leftArm.position;
            if(armRay.sqrMagnitude>.001f&&goal.sqrMagnitude>.001f)
                leftArm.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(armRay,goal),strength)*leftArm.rotation;
        }
    }
    void ClearRig(){if(graph.IsValid())graph.Destroy();if(sampleRoot)UnityEngine.Object.Destroy(sampleRoot);sampleRoot=null;targetBones=sampleBones=null;weights=null;}
    public void Dispose(){End();ClearRig();}
}
