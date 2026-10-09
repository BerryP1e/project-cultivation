using UnityEngine;

/// <summary>御风行进时御剑；待机/锁定战斗时收至身后。攻击期间交还原Prop1轨迹。</summary>
[DefaultExecutionOrder(100)]
public sealed class LingxuFlightSword : MonoBehaviour
{
    [Tooltip("由御风烘焙器从地面Ready标定，不手填手骨偏移")]
    public Vector3 后方髋骨偏移;
    public Quaternion 后方旋转=Quaternion.identity;
    public Vector3 后方缩放=Vector3.one;
    [Min(.05f)] public float 收放时长=.3f;
    [Min(0)] public float 出招后战斗保持=1.2f;

    Transform blade,hips,leftFoot,rightFoot;
    float swordCenter;
    Animator animator;
    PlayerController movement;
    YufengFlight flight;
    NpcTargeting targeting;
    PlayerAnimationController actions;
    float rear=1,combatUntil;
    static readonly int ActionHash=Animator.StringToHash("Action");

    void Awake()
    {
        blade=transform.Find("LingxuSword");
        animator=GetComponentInParent<Animator>();
        movement=GetComponentInParent<PlayerController>();
        if(!movement)return;
        flight=movement.GetComponent<YufengFlight>();
        targeting=movement.GetComponent<NpcTargeting>();
        actions=movement.GetComponent<PlayerAnimationController>();
        if(animator)
        {
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        if(blade)swordCenter=blade.GetComponent<SkinnedMeshRenderer>().sharedMesh.bounds.center.z;
    }

    void LateUpdate()
    {
        if(!flight&&movement)flight=movement.GetComponent<YufengFlight>();
        if(!blade||!hips||!leftFoot||!rightFoot||!movement||!flight||!flight.御风中){rear=1;return;}
        bool action=actions&&actions.动作播放中;
        if(animator.runtimeAnimatorController)
        {
            action|=animator.GetCurrentAnimatorStateInfo(0).shortNameHash==ActionHash;
            if(animator.IsInTransition(0))action|=animator.GetNextAnimatorStateInfo(0).shortNameHash==ActionHash;
        }
        // 出招以及收招过渡仍由同一Animator控制，保留剑的独立攻击与命中轨迹。
        if(action){combatUntil=Time.time+出招后战斗保持;rear=1;return;}
        bool locked=targeting&&targeting.Locked&&!targeting.Locked.IsDead;
        bool selected=targeting&&targeting.Selected&&!targeting.Selected.IsDead&&targeting.Selected.是敌对目标;
        bool combat=locked||selected||Time.time<combatUntil;
        bool moving=movement.水平速度.sqrMagnitude>.04f;
        rear=Mathf.MoveTowards(rear,!moving||combat?1:0,Time.deltaTime/Mathf.Max(.05f,收放时长));
        float blend=rear*rear*(3-2*rear);
        var pose=animator.transform.InverseTransformPoint(hips.position)+后方髋骨偏移;
        var left=animator.transform.InverseTransformPoint(leftFoot.position);var right=animator.transform.InverseTransformPoint(rightFoot.position);
        var underfoot=(left+right)*.5f;underfoot.y=Mathf.Min(left.y,right.y)-.12f;underfoot.z-=swordCenter*后方缩放.z;
        // 从本帧实际混合后的双脚求脚下姿态，不能以上一帧被收放改写的Transform作插值起点。
        // 否则Animator的常量曲线优化和方向混合会使剑逐帧积累偏移、穿进鞋底。
        blade.localPosition=Vector3.Lerp(underfoot,pose,blend);
        blade.localRotation=Quaternion.Slerp(Quaternion.identity,后方旋转,blend);
        blade.localScale=后方缩放;
    }
}
