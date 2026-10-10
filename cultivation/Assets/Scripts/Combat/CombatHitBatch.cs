using System.Collections.Generic;
using UnityEngine;

/// <summary>One pulse or sweep: deduplicate by target root, not by collider. New pulse = new batch.</summary>
public sealed class CombatHitBatch
{
    readonly HashSet<int> targets = new HashSet<int>();
    public readonly long 攻击序号 = CombatDamagePipeline.新攻击序号();
    public AttackResult 命中(ICombatTarget target, CombatHitContext context)
    {
        if (target == null || !target.根) return new AttackResult { 已应用 = true, 状态 = CombatHitState.InvalidTarget };
        if (!targets.Add(target.根.GetInstanceID())) return new AttackResult { 已应用 = true, 状态 = CombatHitState.Duplicate };
        return CombatDamagePipeline.命中(target, new CombatHitContext(context.攻击属性, context.规则, context.来源,
            context.来源id, context.接触点, context.来向, context.段数, 攻击序号, context.表现缩放, context.播放命中表现));
    }
}
