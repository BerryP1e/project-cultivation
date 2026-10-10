using System;
using UnityEngine;

public enum CombatHitState { Calculated, Hit, Miss, Immune, ShieldBlocked, NoDamage, InvalidTarget, InvalidAttack, Duplicate }

/// <summary>One actual contact, independent of its animation, projectile and visual lifetime.</summary>
public readonly struct CombatHitContext
{
    public readonly long 攻击序号;
    public readonly int 段数;
    public readonly string 来源id;
    public readonly object 来源;
    public readonly ICombatStats 攻击属性;
    public readonly AttackSpec 规则;
    public readonly Vector3 接触点, 来向;
    public readonly float 表现缩放;
    public readonly bool 播放命中表现;

    public CombatHitContext(ICombatStats stats, AttackSpec spec, object source, string sourceId,
        Vector3 point, Vector3 direction, int segment = 0, long attackId = 0,
        float visualScale = 1f, bool playHitVfx = true)
    {
        攻击序号 = attackId != 0 ? attackId : CombatDamagePipeline.新攻击序号();
        段数 = segment; 来源id = sourceId ?? ""; 来源 = source; 攻击属性 = stats; 规则 = spec;
        接触点 = point; 来向 = direction; 表现缩放 = visualScale; 播放命中表现 = playHitVfx;
    }
}

/// <summary>Application outcome; retains lethal information even when a training target immediately revives.</summary>
public readonly struct CombatDamageApplication
{
    public readonly float 实际伤害, 吸收伤害, 过量伤害;
    public readonly bool 致命;
    public readonly CombatHitState 状态;
    public CombatDamageApplication(float applied, float absorbed, float overkill, bool lethal, CombatHitState state)
    { 实际伤害 = applied; 吸收伤害 = absorbed; 过量伤害 = overkill; 致命 = lethal; 状态 = state; }
}

public readonly struct CombatHitEvent
{
    public readonly CombatHitContext 上下文;
    public readonly ICombatTarget 目标;
    public readonly AttackResult 结果;
    public CombatHitEvent(CombatHitContext context, ICombatTarget target, AttackResult result)
    { 上下文 = context; 目标 = target; 结果 = result; }
}
