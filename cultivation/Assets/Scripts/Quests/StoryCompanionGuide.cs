using UnityEngine;

/// <summary>御风陪同引路。领路者始终在玩家前方几步，玩家停下时也等候。</summary>
[DisallowMultipleComponent]
public class StoryCompanionGuide : MonoBehaviour
{
    任务管理器 owner;QuestDefinition stage;NpcAiBase ai;bool aiWasEnabled;string motion;
    public void Configure(任务管理器 manager,QuestDefinition definition)
    {
        StopGuide();owner=manager;stage=definition;ai=GetComponent<NpcAiBase>();aiWasEnabled=ai!=null && ai.enabled;
        if(ai!=null)ai.enabled=false;enabled=true;motion=null;
    }
    public void StopGuide(){enabled=false;}
    void OnDisable(){if(ai!=null)ai.enabled=aiWasEnabled;Play(gameObject,"Idle");motion=null;}
    void Update()
    {
        if(owner==null || stage==null || owner.取当前阶段(stage.任务id)?.id!=stage.id){StopGuide();return;}
        var player=物品使用器.取玩家物体();if(player==null)return;
        if(DialogueUI.正在显示 || 黑幕字幕.有幕在显示 || UiEscRegistry.SceneInputBlocked){SetMotion("Yufeng_Idle");return;}
        Vector3 direction=stage.坐标-player.transform.position;direction.y=0;
        float distance=direction.magnitude;
        // 只领先三米，不让玩家停下后师兄独自飞去目的地。
        Vector3 target=player.transform.position+(distance>.1f?direction.normalized*Mathf.Min(3f,Mathf.Max(0,distance-stage.到达半径*.5f)):player.transform.right*2f);
        target.y=player.transform.position.y+.45f;
        var delta=target-transform.position;
        if(delta.magnitude>.25f)
        {
            transform.position=Vector3.MoveTowards(transform.position,target,Mathf.Clamp(delta.magnitude*2f,3f,9f)*Time.deltaTime);
            delta.y=0;if(delta.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta)*Quaternion.Euler(0,ai!=null?ai.模型朝向补偿:0,0),8f*Time.deltaTime);
            SetMotion("Yufeng_Forward");
        }
        else SetMotion("Yufeng_Idle");
    }
    void SetMotion(string state){if(motion==state)return;motion=state;Play(gameObject,state);}
    public static void Play(GameObject actor,string state)
    {
        var wrapper=actor.GetComponent<NpcAnimator>();if(wrapper!=null && wrapper.Play(state))return;
        var animator=actor.GetComponentInChildren<Animator>();
        if(animator!=null && animator.runtimeAnimatorController!=null && animator.HasState(0,Animator.StringToHash(state)))animator.CrossFade(state,.15f);
    }
}
