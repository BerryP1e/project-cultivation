using UnityEngine;

/// <summary>单位局部的接触停顿：只暂停动作和自主移动，不改Time.timeScale或伤害结算。</summary>
[DefaultExecutionOrder(10000)]
public sealed class CombatHitStop : MonoBehaviour
{
    Animator[] animators;
    float[] speeds;
    float until, nextAllowed;
    long lastAttack;
    bool held;
    public int 触发次数 { get; private set; }
    public bool 正在暂停 => held && Time.unscaledTime < until;

    public static bool 正在顿帧(Transform unit)
    {
        if(!unit)return false;
        var stop=unit.GetComponent<CombatHitStop>();
        return stop && stop.正在暂停;
    }

    public static void 成功接触(CombatHitEvent hit,CombatImpactCatalog config,CombatImpactCatalog.Entry rule)
    {
        if(!config || !hit.结果.有效接触)return;
        var mode=rule!=null?rule.顿帧方式:CombatHitStopMode.自动;
        if(mode==CombatHitStopMode.关闭)return;
        bool physical=hit.上下文.规则.伤害属性==DamageNature.物理;
        float duration=physical?(hit.结果.暴击?config.暴击顿帧:config.物理顿帧):config.特殊顿帧;
        if(duration<=0)return;
        var target=hit.目标?.根;
        var source=CombatTargets.取(hit.上下文.攻击属性)?.根 ?? CombatTargets.取(hit.上下文.来源)?.根;
        bool both=mode==CombatHitStopMode.双方 || (mode==CombatHitStopMode.自动 && physical && source && target
            && Vector3.Distance(source.position,target.position)<=config.双方顿帧距离);
        Pause(target,duration,config.顿帧触发间隔,hit.上下文.攻击序号);
        if(both && source!=target)Pause(source,duration,config.顿帧触发间隔,hit.上下文.攻击序号);
    }

    static void Pause(Transform unit,float duration,float interval,long attack)
    {
        if(!unit || !unit.gameObject.activeInHierarchy)return;
        var stop=unit.GetComponent<CombatHitStop>() ?? unit.gameObject.AddComponent<CombatHitStop>();
        if(!stop.enabled)return;
        stop.Trigger(duration,interval,attack);
    }

    void Trigger(float duration,float interval,long attack)
    {
        float now=Time.unscaledTime;
        // 同一扫击的群攻只暂停攻击者一次；连续脉冲不能延长正在进行的停顿。
        if(正在暂停 || now<nextAllowed || (attack!=0 && attack==lastAttack))return;
        Restore();
        animators=GetComponentsInChildren<Animator>();speeds=new float[animators.Length];
        for(int i=0;i<animators.Length;i++){speeds[i]=animators[i].speed;animators[i].speed=0;}
        until=now+Mathf.Clamp(duration,0,.15f);nextAllowed=now+Mathf.Max(duration,interval);
        lastAttack=attack;held=true;触发次数++;
    }

    void Update()
    {
        if(!held)return;
        if(!正在暂停){Restore();return;}
        for(int i=0;i<animators.Length;i++)if(animators[i]){
            // 其他动画驱动若更新了倍率，记录新倍率，恢复时不丢失减速或攻速变化。
            if(animators[i].speed!=0)speeds[i]=animators[i].speed;
            animators[i].speed=0;
        }
    }

    void LateUpdate()
    {
        if(正在暂停)foreach(var animator in animators)if(animator)animator.speed=0;
    }

    void Restore()
    {
        if(!held)return;
        var player=GetComponent<PlayerAnimationController>();
        for(int i=0;i<animators.Length;i++)if(animators[i] && animators[i].speed==0)
            animators[i].speed=player && player.动作已定住?0:speeds[i];
        held=false;
    }
    void OnDisable()=>Restore();
}
