using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 范围伤害的执行体。挂在一个临时生成的特效物件上，负责三件事：
///
///   1. 到「首次造成伤害时间」时结算第一次（填 0 就是生成瞬间立刻结算）
///   2. 之后每隔「伤害间隔」再结算一次，直到「持续时长」用完
///   3. 到时间把自己（连同特效）一起销毁
///
/// 伤害**全部走 <see cref="CombatCalculator"/>**，攻击类别固定是【主动神通】，
/// 伤害属性（物理 / 特殊）与技能倍率取自神通资产 —— 所以焚天炎术走的就是
/// 「特殊主动神通」那一格。
///
/// 范围填 0 时退化成单体：只打锁定目标一下。
///
/// 打谁：
///   · 范围内的**敌人**（对主角好感度 &lt; 0）全打
///   · **锁定目标无视好感度** —— 玩家锁了它又按了技能，就该对它生效
///     （和飞剑「手动右键锁定不看好感度」的规则保持一致）
/// </summary>
public class AreaSkillRunner : MonoBehaviour
{
    [Tooltip("命中时打一条日志")]
    public bool 打印日志 = true;

    [Tooltip("★ 勾上（默认）：特效存活**至少**播完 prefab 的自然总时长（× `存活倍率`）。\n\n" +
             "为什么：`Effect_13_DangerClose`（焚天炎术）的自然总时长是 **5.0 秒**\n" +
             "（`Effect_13_Shell`：duration 4.0 + life 1.0），而表里「持续时长」= 3 秒\n" +
             "→ 到 3 秒就把特效连同执行体一起 Destroy，火焰柱还没落完就没了\n" +
             "（用户 2026-09-29 报的）。和冰刺 / 寒墟 / 玄霄雷诀同一套口径\n" +
             "（见 docs/guides/沧澜寒渊录.md §2.4）。")]
    public bool 存活按特效自动 = true;

    [Tooltip("自动存活再乘这个系数（留点余量让它彻底淡完）")]
    public float 存活倍率 = 1.05f;

    ActiveDivineAbility 神通;
    ICombatStats 攻击方;
    Vector3 中心;
    LayerMask 敌人层;
    NpcInstance 单体目标;

    /// <summary>伤害窗口（= 持续时长；持续时长为 0 时用「一次性存活」）</summary>
    float 伤害结束时间;
    /// <summary>物件存活终点：**不小于**伤害窗口，且至少让特效播完</summary>
    float 结束时间;
    float 下次结算时间;
    float 已结算总伤害;
    int 已结算次数;
    bool 环境已结算;

    /// <summary>累计结算出的总伤害（调试 / 测试用）</summary>
    public float 累计伤害 => 已结算总伤害;

    /// <summary>累计结算次数（**按目标计**：一次 AoE 打中 3 个敌人算 3 次）</summary>
    public int 结算次数 => 已结算次数;

    /// <summary>初始化。第一次结算的时机由神通的「首次造成伤害时间」决定。</summary>
    /// <param name="一次性存活">持续时长为 0 时，特效播多久后销毁</param>
    public void 初始化(ActiveDivineAbility 神通, Vector3 中心, ICombatStats 攻击方,
                      LayerMask 敌人层, NpcInstance 单体目标, float 一次性存活)
    {
        this.神通 = 神通;
        this.中心 = 中心;
        this.攻击方 = 攻击方;
        this.敌人层 = 敌人层;
        this.单体目标 = 单体目标;

        float 时长 = 神通 != null ? 神通.持续时长 : 0f;
        float 首次 = 神通 != null ? Mathf.Max(0f, 神通.首次造成伤害时间) : 0f;

        // ---- 伤害窗口 ----
        float 窗口 = 时长 > 0f ? 时长 : Mathf.Max(0.1f, 一次性存活);
        伤害结束时间 = Time.time + 窗口;
        结束时间 = 伤害结束时间;

        // ---- 特效存活：至少播完它自己的自然总时长 ----
        // ★ 别让「伤害窗口」顺手决定特效的生死：焚天炎术窗口 3 秒、特效自然 5 秒，
        //   到 3 秒就 Destroy 的话火焰柱还没落完（用户 2026-09-29 报的）。
        if (存活按特效自动)
        {
            float 自然 = 特效摆放.量特效总时长(gameObject, 0f);   // 自己身上就是那份粒子系统
            if (自然 > 0.01f)
                结束时间 = Mathf.Max(结束时间, Time.time + 自然 * Mathf.Max(0.5f, 存活倍率));
        }

        下次结算时间 = Time.time + 首次;

        // 首次时间不早于**伤害窗口** → 一次伤害都打不出来，提前吼一声
        if (首次 > 0f && 下次结算时间 >= 伤害结束时间)
            Debug.LogWarning("[AreaSkillRunner] " + (神通 != null ? 神通.神通名称 : "?")
                + " 的「首次造成伤害时间」(" + 首次.ToString("0.##")
                + "s) 不早于它的伤害窗口 (" + (伤害结束时间 - Time.time).ToString("0.##")
                + "s)，这一次不会结算任何伤害 —— 调小首次时间，或调大持续时长。");

        // 首次时间为 0 就保持旧行为：施放瞬间立刻结算
        if (首次 <= 0f) 结算一次();
    }

    float 取间隔()
    {
        return 神通 != null ? Mathf.Max(0.05f, 神通.伤害间隔) : 1f;
    }

    void Update()
    {
        if (Time.time >= 结束时间) { Destroy(gameObject); return; }

        // 伤害窗口过了、但特效还在播（自然时长比窗口长）→ 只等销毁，不再结算
        if (Time.time >= 伤害结束时间) return;

        if (Time.time >= 下次结算时间)
        {
            结算一次();
            下次结算时间 = Time.time + 取间隔();
        }
    }

    /// <summary>对范围内所有敌人结算一次。返回本次的命中目标数。</summary>
    public int 结算一次()
    {
        if (神通 == null || 攻击方 == null) return 0;
        if(!环境已结算){
            环境已结算=true;
            // 焚天的环境破坏由每颗火球真实落地触发，持续伤害不另挖中心大坑。
            if(神通.神通id!="ability_fentian_yanshu")
                CombatImpactPipeline.接触(神通.神通id,中心,VoxelCombatDamage.AreaRadius(神通),source:this);
        }

        var spec = new AttackSpec(神通.伤害属性, 神通.攻击类别, false, 神通.伤害倍率);
        var batch = new CombatHitBatch();
        var context = new CombatHitContext(攻击方,spec,this,神通.神通id,中心,Vector3.down,已结算次数);

        // 范围为 0 → 退化成只打锁定目标
        if (神通.范围 <= 0f)
        {
            if (单体目标 == null || 单体目标.IsDead) return 0;
            var r1 = batch.命中(new NpcTarget(单体目标), context);
            已结算次数++;
            已结算总伤害 += r1.伤害;
            return r1.命中 ? 1 : 0;
        }

        var cols = Physics.OverlapSphere(中心, 神通.范围, 敌人层, QueryTriggerInteraction.Ignore);
        int 命中数 = 0;

        foreach (var col in cols)
        {
            if (col == null) continue;

            var npc = col.GetComponentInParent<NpcInstance>();
            if (npc == null || npc.IsDead) continue;
            // 「范围内的敌人」都打；但【锁定目标】无视好感度 —— 玩家既然锁了它
            // 又按了技能，就该对它生效（和飞剑"手动锁定不看好感度"的规则一致）
            if (!npc.是敌对目标 && npc != 单体目标) continue;
            var r = batch.命中(new NpcTarget(npc), context);
            if (r.状态 == CombatHitState.Duplicate) continue;
            已结算次数++;
            已结算总伤害 += r.伤害;
            if (r.命中) 命中数++;
        }

        if (命中数 > 0 && 打印日志)
            Debug.Log("[AreaSkillRunner] " + (神通 != null ? 神通.神通名称 : "?")
                      + " 命中 " + 命中数 + " 个目标，本次合计 " + 已结算总伤害.ToString("0.##"));

        return 命中数;
    }

    // ---- ASCII 别名 ----
    public float TotalDamage => 已结算总伤害;
    public int AppliedCount => 已结算次数;
    public int ApplyOnce() => 结算一次();
}
