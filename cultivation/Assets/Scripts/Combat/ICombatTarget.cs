using UnityEngine;

/// <summary>
/// **「锁定单位」** —— 飞弹与伤害统一的靶子。
///
/// ## 为什么要这一层
///
/// 以前的伤害代码到处写死「玩家」：NPC 的飞弹直接拿 <see cref="PlayerVitals"/> 当目标，
/// 玩家的锁定直接拿 <c>NpcInstance</c> 当目标。但策划说了：
/// **以后 NPC 也可能作为召唤单位帮玩家打怪** —— 那时候"敌人"就不一定是玩家了。
/// 所以统一抽象成「锁定单位」：**谁锁定了谁，谁就是被攻击的锁定单位**。
///
/// 说法规范：
///   · 玩家普攻 → 锁定单位 = 被锁定/选中的那个 NPC
///   · NPC 打玩家 → 锁定单位 = 玩家
///   · NPC 被召唤后打怪 → 锁定单位 = 它锁定的那只怪
///
/// ## 怎么用
///
/// ```csharp
/// ICombatTarget 目标 = CombatTargets.取(某个 NpcInstance 或 PlayerVitals);
/// var 结果 = 目标.受到攻击(攻击方属性, 规则);
/// ```
///
/// 这一层是薄适配：向 CombatDamagePipeline 提供属性、免疫状态与扣血入口。
/// 新攻击使用 CombatHitContext；受到攻击保留为兼容入口，同样经过统一管线。
/// </summary>
public interface ICombatTarget
{
    /// <summary>单位根节点（位置基准）</summary>
    Transform 根 { get; }

    /// <summary>判定点（胸口高度）。飞弹瞄准、命中特效、飘字都放这儿</summary>
    Vector3 判定点 { get; }

    /// <summary>已经倒下了（死了 / 已死亡）</summary>
    bool 已倒下 { get; }

    /// <summary>当前气血</summary>
    float 当前气血 { get; }

    /// <summary>显示名（日志用）</summary>
    string 名字 { get; }
    ICombatStats 战斗属性 { get; }
    bool 免疫伤害 { get; }
    CombatDamageApplication 应用伤害(float amount, CombatHitContext context);
    void 通知结算(AttackResult result);

    /// <summary>
    /// **挨一次攻击。** 伤害公式由统一管线走 <see cref="CombatCalculator"/>，
    /// 调用方只管把「攻击方属性 + 规则」递进来。
    /// </summary>
    /// <param name="攻击方属性">攻击方的战斗属性（<see cref="ICombatStats"/>）</param>
    /// <param name="规则">这一击的规则（伤害属性 / 攻击类别 / 倍率 / 必定命中）</param>
    /// <param name="攻击方">攻击方本体（可空，日志/仇恨用）</param>
    AttackResult 受到攻击(ICombatStats 攻击方属性, AttackSpec 规则, object 攻击方 = null);
}

/// <summary>把 <see cref="NpcInstance"/> 当成锁定单位</summary>
public sealed class NpcTarget : ICombatTarget
{
    public NpcInstance 单位 { get; }

    public NpcTarget(NpcInstance npc) { 单位 = npc; }

    public Transform 根 => 单位 != null ? 单位.transform : null;
    public Vector3 判定点 => 单位 != null ? 单位.transform.position + Vector3.up * 判定高度 : Vector3.zero;
    public bool 已倒下 => 单位 == null || 单位.IsDead;
    public float 当前气血 => 单位 != null ? 单位.CurrentHealth : 0f;
    public string 名字 => 单位 != null ? 单位.DisplayName : "（无）";
    public ICombatStats 战斗属性 => 单位;
    public bool 免疫伤害 => 单位 == null || 单位.无敌;
    public CombatDamageApplication 应用伤害(float amount, CombatHitContext context) => 单位.应用战斗伤害(amount, context);
    public void 通知结算(AttackResult result) => 单位.通知战斗结算(result);

    /// <summary>瞄准高度（大致半身）</summary>
    public static float 判定高度 = 1.1f;

    /// <summary>兼容入口，同样返回统一管线的实际扣血结果。</summary>
    public AttackResult 受到攻击(ICombatStats 攻击方属性, AttackSpec 规则, object 攻击方 = null)
        => CombatDamagePipeline.命中(this, 攻击方属性, 规则, 攻击方);
}

/// <summary>把 <see cref="PlayerVitals"/> 当成锁定单位</summary>
public sealed class PlayerTarget : ICombatTarget
{
    public PlayerVitals 气血 { get; }

    public PlayerTarget(PlayerVitals vitals) { 气血 = vitals; }

    public Transform 根 => 气血 != null ? 气血.transform : null;
    public Vector3 判定点 => 气血 != null ? 气血.transform.position + Vector3.up * 判定高度 : Vector3.zero;
    public bool 已倒下 => 气血 == null || 气血.已死亡;
    public float 当前气血 => 气血 != null ? 气血.当前气血 : 0f;
    public string 名字 => 气血 != null ? 气血.name : "（无）";
    public ICombatStats 战斗属性 => 气血 != null ? 气血.属性 : null;
    public bool 免疫伤害 => 气血 == null || 气血.无敌;
    public CombatDamageApplication 应用伤害(float amount, CombatHitContext context) => 气血.应用战斗伤害(amount, context.规则.伤害属性);
    public void 通知结算(AttackResult result) => 气血.通知战斗结算(result);

    public static float 判定高度 = 1.1f;

    /// <summary>
    /// 玩家兼容入口：统一计算，再由 PlayerVitals 应用护盾、扣血和死亡。
    /// </summary>
    public AttackResult 受到攻击(ICombatStats 攻击方属性, AttackSpec 规则, object 攻击方 = null)
    {
        return CombatDamagePipeline.命中(this, 攻击方属性, 规则, 攻击方);
    }
}

/// <summary>把任意对象转成「锁定单位」</summary>
public static class CombatTargets
{
    /// <summary>取一个锁定单位。传 <see cref="NpcInstance"/> 或 <see cref="PlayerVitals"/>；其它类型返回 null</summary>
    public static ICombatTarget 取(object 谁)
    {
        switch (谁)
        {
            case NpcTarget t: return t;
            case PlayerTarget t: return t;
            case NpcInstance n: return n == null ? null : new NpcTarget(n);
            case PlayerVitals v: return v == null ? null : new PlayerTarget(v);
        }
        // 传进来的是别的组件（比如某个子物件）→ 往上找
        if (谁 is Component c)
        {
            if (c != null)
            {
                var n = c.GetComponentInParent<NpcInstance>();
                if (n != null) return new NpcTarget(n);
                var v = c.GetComponentInParent<PlayerVitals>();
                if (v != null) return new PlayerTarget(v);
            }
        }
        return null;
    }

    /// <summary>某个 GameObject 身上的锁定单位（先找 NPC，再找玩家）</summary>
    public static ICombatTarget 从(GameObject go)
    {
        if (go == null) return null;
        var n = go.GetComponentInParent<NpcInstance>();
        if (n != null) return new NpcTarget(n);
        var v = go.GetComponentInParent<PlayerVitals>();
        return v != null ? new PlayerTarget(v) : null;
    }

    /// <summary>取 <see cref="NpcTarget"/> 里包的 NPC（不是 NPC 就返回 null）</summary>
    public static NpcInstance 取Npc(this ICombatTarget t) => (t as NpcTarget)?.单位;

    /// <summary>取 <see cref="PlayerTarget"/> 里包的玩家气血（不是玩家就返回 null）</summary>
    public static PlayerVitals 取玩家(this ICombatTarget t) => (t as PlayerTarget)?.气血;
}
