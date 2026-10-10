using System;
using UnityEngine;

/// <summary>Authoritative biological hit pipeline. Environment contacts never enter this damage calculator.</summary>
public static class CombatDamagePipeline
{
    static long sequence;
    public static event Action<CombatHitEvent> 结算完成;
    public static event Action<CombatHitEvent> 实际受伤;
    public static event Action<CombatHitEvent> 击杀;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { sequence = 0; 结算完成 = null; 实际受伤 = null; 击杀 = null; }
    public static long 新攻击序号() => ++sequence;

    public static AttackResult 命中(NpcInstance target, CombatHitContext context)
        => 命中(target != null ? new NpcTarget(target) : null, context);

    public static AttackResult 命中(ICombatTarget target, CombatHitContext context)
    {
        var result = new AttackResult { 伤害属性 = context.规则.伤害属性, 攻击类别 = context.规则.攻击类别,
            已应用 = true, 状态 = CombatHitState.InvalidTarget };
        if (target == null || target.根 == null || target.已倒下 || target.战斗属性 == null) return result;
        if (context.攻击属性 == null) { result.状态 = CombatHitState.InvalidAttack; return result; }
        if (target.免疫伤害) result.状态 = CombatHitState.Immune;
        else
        {
            result = CombatCalculator.Resolve(context.攻击属性, target.战斗属性, context.规则);
            result.已应用 = true;
            result.计算伤害 = result.伤害;
            result.伤害 = 0f;
            if (!result.命中) result.状态 = CombatHitState.Miss;
            else if (float.IsNaN(result.计算伤害) || float.IsInfinity(result.计算伤害)) result.状态 = CombatHitState.InvalidAttack;
            else if (result.计算伤害 <= 0f) result.状态 = CombatHitState.NoDamage;
            else
            {
                var applied = target.应用伤害(result.计算伤害, context);
                result.伤害 = applied.实际伤害; result.吸收伤害 = applied.吸收伤害;
                result.过量伤害 = applied.过量伤害; result.致命 = applied.致命; result.状态 = applied.状态;
            }
        }
        var hit = new CombatHitEvent(context, target, result);
        // Legacy views receive the same final result as the new event stream.
        try { target.通知结算(result); } catch (Exception error) { Debug.LogException(error); }
        try { CombatImpactPipeline.生物命中(hit); } catch (Exception error) { Debug.LogException(error); }
        Publish(结算完成, hit);
        if (result.伤害 > 0f) Publish(实际受伤, hit);
        if (result.致命) Publish(击杀, hit);
        return result;
    }

    public static AttackResult 命中(ICombatTarget target, ICombatStats stats, AttackSpec spec, object source = null)
    {
        var point = target != null ? target.判定点 : Vector3.zero;
        var origin = source is Component c && c ? c.transform.position : point;
        return 命中(target, new CombatHitContext(stats, spec, source, source?.GetType().Name, point, point - origin));
    }

    static void Publish(Action<CombatHitEvent> handlers, CombatHitEvent hit)
    {
        if (handlers == null) return;
        foreach (Action<CombatHitEvent> callback in handlers.GetInvocationList())
            try { callback(hit); } catch (Exception error) { Debug.LogException(error); }
    }
}
