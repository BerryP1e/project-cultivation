using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **怪物等级补正表的一档**（= 一个大境界）。
///
/// 「等级」在补正表里是**1~100 的连续等级**，每 10 级一个大境界：
/// 1~10 = 大境界 1、11~20 = 大境界 2 …… 所以「大境界序号 = ceil(等级 ÷ 10)」。
/// 共 10 档（100 级）。
/// </summary>
[System.Serializable]
public class NpcLevelTier
{
    [Tooltip("大境界序号，1 起。1 = 1~10 级、2 = 11~20 级 ……")]
    public int 大境界 = 1;

    [Tooltip("这一档的起点等级，= (大境界-1)×10 + 1")]
    public int 起点等级 = 1;

    [Tooltip("**倍数属性**在这一档内的总倍率（相对 1 级基准）。\n" +
             "气血 / 攻击 / 防御 / 灵力 / 气血回复 / 灵力回复 用这一列。")]
    public float 倍数 = 1f;

    [Tooltip("**百分比属性**在这一档内累计加的绝对值（例 0.25 = +25%）。\n" +
             "暴击 / 会心 / 闪避 / 各类加成减免 用这一列 —— 它们是 0~1 的比率，\n" +
             "乘一个巨大的倍率会算出几百倍的暴击，只能加法。")]
    public float 加值 = 0f;
}

/// <summary>
/// **怪物等级补正表**：从 CSV「NPC等级补正表」生成。
///
/// ## 它解决什么问题
///
/// 以前「同一个怪的强弱」是靠 <c>NpcVariantSplitter</c> 把 <c>NPC表.csv</c> 里
/// 每一行按蒙皮版本**逐个硬递推**出来的 —— 146 行妖魔其实只是 6 套数值模板，
/// 而且是**死的**：想加一种怪就得手工编一遍数值。
///
/// 现在改成：**NPC 表里只填「1 级基准值」，实际强度由等级补正表算**。
/// 于是：
///   · 加新怪 = 填一份 1 级基准值，任何等级都能刷
///   · 调强弱 = 改补正表的一条曲线，全场怪物跟着变
///   · 镇妖塔 1000 层 = 同一个怪在 100 个等级上的实例
///
/// ## 曲线形状（用户 2026-09-30 定）
///
/// 「筑基期肯定能碾压炼气期，起码能同时打 20~30 个炼气期」——
/// 所以**每跨一个大境界（10 级）整体 ×<see cref="每大境界倍率"/>**（默认 25）。
/// 这是**质变**，不是线性增长：到 100 级总倍率约 25^10 ≈ 9.5×10^13。
///
/// 大境界内部再按几何插值平滑过渡（`档内比^档内步`），
/// 所以 1 级和 10 级之间有平滑坡，11 级是踩上去的台阶。
/// </summary>
[CreateAssetMenu(fileName = "NpcLevelScale_", menuName = "修仙/NPC等级补正表", order = 8)]
public class NpcLevelScale : ScriptableObject
{
    [Header("曲线参数（生成器用；运行时只读 档位 列表）")]
    [Tooltip("每跨一个大境界（10 级）的整体倍率。用户定的「质变」幅度。\n" +
             "**和玩家的功法增益共用**（见 LevelCurve）—— 改这里，玩家和怪一起变。")]
    public float 每大境界倍率 = LevelCurve.每大境界倍率;

    [Tooltip("**档内因子的终点** = 本档第 10 级相对本档第 1 级的倍数。\n\n" +
             "运行时用 `档.倍数 ÷ 档内比^(级-档起点)`，所以：\n" +
             "  · 本档第 1 级  → 档.倍数 × 1（基准）\n" +
             "  · 本档第 10 级 → 档.倍数 ÷ 0.679 ≈ ×1.473\n\n" +
             "于是**跨档台阶 = 36.75 × 0.679 = ×17**（不是 25）——\n" +
             "因为档内那一段涨了 1.473 倍，台阶只要补上剩下的部分。\n" +
             "**实际观感是「每 10 级 ×17」**，档内再平滑涨 1.47 倍。\n" +
             "想要台阶正好 ×25 就把这里调成 1（档内完全平坦）。")]
    public float 档内比 = 0.679f;

    [Header("属性分档的权重（相对气血的增长快慢）")]
    [Tooltip("攻击 / 防御 / 灵力 相对于气血的曲线比")]
    public float 攻击比 = 1.65f;
    public float 防御比 = 1.62f;
    public float 灵力比 = 1.70f;

    [Header("百分比属性每档加值")]
    public float 暴击每档 = 0.05f;
    public float 会心每档 = 0.04f;
    public float 闪避每档 = 0.03f;
    public float 减免每档 = 0.02f;

    [Header("档位（1~10 大境界，由生成器写入）")]
    public List<NpcLevelTier> 档位 = new List<NpcLevelTier>();

    [Header("安全上限")]
    [Tooltip("补正后的气血/攻击等数值上限。100 级会算到 1e14 量级，\n" +
             "float 放得下但 UI 会很难看，而且没必要真的显示这么大的数。\n" +
             "0 = 不封顶")]
    public float 数值上限 = 0f;

    // ============================================================ 查询

    /// <summary>第一大境界的起点等级（= 1）</summary>
    public const int 首档起点 = 1;

    /// <summary>总共多少个大境界（= 档位数）</summary>
    public int 档数 => 档位 != null ? 档位.Count : 0;

    /// <summary>等级 → 大境界序号（1 起）。等级 1~10 → 1、11~20 → 2 ……</summary>
    public static int 等级到大境界(int 等级)
    {
        if (等级 <= 0) return 1;
        return (等级 - 1) / 10 + 1;
    }

    /// <summary>大境界序号 → 它的起点等级</summary>
    public static int 大境界到起点等级(int 大境界)
    {
        return Mathf.Max(1, (大境界 - 1) * 10 + 1);
    }

    /// <summary>取某一档（越界就夹到两端）</summary>
    public NpcLevelTier 取档(int 大境界)
    {
        if (档位 == null || 档位.Count == 0) return null;
        int i = Mathf.Clamp(大境界 - 1, 0, 档位.Count - 1);
        return 档位[i];
    }

    /// <summary>
    /// **等级 → 相对 1 级基准的倍率**（倍数属性 / 百分比属性各一个）。
    ///
    /// 【与谁共用】曲线本身来自 <see cref="LevelCurve"/> —— **和玩家的功法增益是同一条**。
    /// 这是刻意的：怪物和玩家必须同步增长，否则后期一定崩
    /// （老版本的病根就是「怪物几何、玩家线性」）。
    ///
    /// ## 两条路
    ///
    /// · **优先用 <see cref="档位"/> 表**（CSV「NPC等级补正表」填的，策划可手改，
    ///   而且档内可以做几何插值，比纯公式更有手感）
    /// · 档位表是空的（还没导入 CSV）→ **退化成 <see cref="LevelCurve"/> 纯公式**
    ///
    /// 两条路在 1 级都给 ×1、在每个大境界起点都给 ×25^(档-1)，所以互相兼容。
    ///
    /// ## 等级允许**小数**
    ///
    /// 镇妖塔「同一级内逐层递进」会把补正等级喂成 1.25 / 1.8 这种值
    /// （见 `TowerFloorTable.取怪物补正等级`）。
    /// 档内因子本来就是连续的几何插值（`档内比^k`），所以小数 `k` 天然落在
    /// 正确的位置上，**不需要任何特殊处理** ✓
    /// </summary>
    public void 取倍率(float 等级, out float 倍数, out float 加值)
    {
        int 大境 = 等级到大境界(Mathf.RoundToInt(等级));
        var 档 = 取档(大境);

        if (档 == null)
        {
            // 退化：纯公式（还没有补正表 CSV 时也能跑）
            倍数 = LevelCurve.倍率(等级);
            加值 = 0f;
            return;
        }

        // 档内插值：k = 0..9（本档起点 = 0），**允许小数**
        //
        //   `倍数 = 档.倍数 / 档内比^k`
        //
        //   本档第 1 级（k=0）= 档.倍数（基准，1 级恰好 ×1 ✓）
        //   本档第 10 级（k=9）= 档.倍数 / 0.679 = 档.倍数 × 1.473
        //
        //   而 CSV 里每一档的值本身就是**再乘过 25 / 0.679 的**，
        //   于是档内涨满之后跨到下一档正好是 25 倍：
        //       档N第10级 = V(N)·1.473
        //       档N+1第1级 = V(N)·36.75   →  比值 = 36.75 × 0.679 = **25** ✓
        //
        //   【踩过的坑·连错三次，每次都只错一个常数因子，而且不报任何错】
        //     1) 用 `档内比^k` 乘：1 级 = ×1，但 10→11 跳 ×54
        //     2) 用 `档内比^(9-k)`：1 级 = ×0.679，台阶 ×36.75
        //     3) `档内比` 同时当「档内终点」和「跨档台阶」用 → 台阶永远不是 25
        //   三条断言可以一次抓住全部三种错（见 TowerBuilder.曲线自检）：
        //     ① 取倍率(1) == 1
        //     ② 取倍率(11) / 取倍率(10) == 每大境界倍率（**注意是 10→11，不是 11/1**）
        //     ③ 曲线单调递增
        float k = Mathf.Clamp(等级 - 大境界到起点等级(大境), 0f, 9f);
        float 档内因子 = Mathf.Pow(Mathf.Max(0.0001f, 档内比), k);

        倍数 = 档.倍数 / 档内因子;
        加值 = 档.加值 / 档内因子;

        if (数值上限 > 0f) 倍数 = Mathf.Min(倍数, 数值上限);
    }

    /// <summary>整数等级的便捷重载（绝大多数调用方用这个）</summary>
    public void 取倍率(int 等级, out float 倍数, out float 加值)
        => 取倍率((float)等级, out 倍数, out 加值);

    /// <summary>
    /// **把一份 1 级基准属性补正到指定等级。**
    ///
    /// 这是整个补正系统的唯一入口 —— <c>NpcInstance.应用等级补正()</c> 用它。
    /// 不改任何资产，返回一份新的 <see cref="AttributeSet"/>。
    /// </summary>
    public AttributeSet 应用到(AttributeSet 基准, float 等级)
    {
        var 结果 = new AttributeSet();
        if (基准 == null) return 结果;
        结果.CopyFrom(基准);

        取倍率(等级, out float 倍数, out float 加值);
        if (Mathf.Approximately(倍数, 1f) && Mathf.Approximately(加值, 0f)) return 结果;

        for (int i = 0; i < AttributeUtil.Count; i++)
        {
            var t = (AttributeType)i;
            float v = 基准[t];
            if (Mathf.Approximately(v, 0f)) continue;      // 基准是 0 的属性不补正（免得 0 × 巨大倍率 还是 0，但语义更清楚）

            结果[t] = 属于倍数组(t)
                ? 封顶(v * 倍数 * 取组修正(t))
                : v + 加值;
        }

        // 百分比属性加值后夹到合理范围 —— 补正表的加值本来就不会越界，
        // 但用户手改 CSV 时会，这里兜一层免得出现「暴击 500%」
        foreach (var t in 百分比组)
        {
            if (t == AttributeType.AttackSpeed) continue;   // 攻速可以 >1，是有意义的
            结果[t] = Mathf.Min(结果[t], 0.95f);
        }

        return 结果;
    }

    float 封顶(float v) => 数值上限 > 0f ? Mathf.Min(v, 数值上限) : v;

    /// <summary>攻击/防御/灵力 相对气血的曲线修正（气血走 1.0）</summary>
    float 取组修正(AttributeType t)
    {
        switch (t)
        {
            case AttributeType.Attack: return 攻击比;
            case AttributeType.Defense: return 防御比;
            case AttributeType.MaxSpirit: return 灵力比;
            default: return 1f;
        }
    }

    // ============================================================ 属性分组

    /// <summary>
    /// **倍数属性组** —— 随等级乘倍率增长。
    ///
    /// 规则：**绝对数值量纲**的属性（血/攻/防/灵力及其回复）才乘倍率。
    /// </summary>
    public static readonly AttributeType[] 倍数组 =
    {
        AttributeType.MaxHealth,
        AttributeType.Attack,
        AttributeType.Defense,
        AttributeType.MaxSpirit,
        AttributeType.HealthRegen,
        AttributeType.SpiritRegen,
    };

    /// <summary>
    /// **百分比组** —— 随等级加绝对值。
    ///
    /// 规则：**0~1 比率量纲**的属性只能加法。乘 25×10 次会算出「暴击 2500000%」这种东西。
    /// </summary>
    public static readonly AttributeType[] 百分比组 =
    {
        AttributeType.CritChance,
        AttributeType.CritResist,
        AttributeType.Insight,
        AttributeType.InsightResist,
        AttributeType.TruePower,
        AttributeType.TrueResist,
        AttributeType.Dodge,
        AttributeType.IgnoreDodge,
        AttributeType.CooldownReduction,
        AttributeType.BasicAttackBonus,
        AttributeType.BasicAttackReduction,
        AttributeType.ActiveSpellBonus,
        AttributeType.ActiveSpellReduction,
        AttributeType.PassiveSpellBonus,
        AttributeType.PassiveSpellReduction,
    };

    /// <summary>该属性是否走倍率（否则走加值）</summary>
    public static bool 属于倍数组(AttributeType t)
    {
        for (int i = 0; i < 倍数组.Length; i++) if (倍数组[i] == t) return true;
        return false;
    }

    /// <summary>
    /// **恒定组** —— 补正表不动它们。
    ///
    /// 移动速度 / 攻速 / 暴击伤害 / 会心伤害 / 治疗加成：
    /// 这些是「手感」参数，跟着等级涨会让怪跑得比玩家快、或者一出手就秒人，
    /// 属于**质变之外的失控**，所以保持表里的基准值。
    /// </summary>
    public static bool 属于恒定组(AttributeType t)
    {
        return t == AttributeType.MoveSpeed
            || t == AttributeType.AttackSpeed
            || t == AttributeType.CritDamage
            || t == AttributeType.InsightDamage
            || t == AttributeType.TrueDamage
            || t == AttributeType.HealingBonus;
    }

    /// <summary>
    /// 按公式重算全部档位（生成器调用；CSV 没给值时兜底）。
    ///
    /// 档位值必须是**预除过档内因子终点**的：
    /// <c>V(N) = 每大境界倍率^(N-1) / 档内比^9</c>
    ///
    /// 推导：档 N 第 10 级 = V(N)/档内比^9，档 N+1 第 1 级 = V(N+1)，
    /// 要两者之比 == 每大境界倍率，就得到上面这个式子。
    /// 漏掉 `/档内比^9` 会让台阶变成 `倍率 × 档内比^9`（实测 ×17 而不是 ×25）。
    /// </summary>
    public void 重算档位(int 档数 = LevelCurve.档数)
    {
        档位 = new List<NpcLevelTier>();
        float 每档 = Mathf.Max(1.0001f, 每大境界倍率);
        float 档内终点 = Mathf.Pow(Mathf.Max(0.0001f, 档内比), 9);

        for (int 大境 = 1; 大境 <= 档数; 大境++)
        {
            // 大境界 1 = 1 级基准（×1）；大境界 N 的台阶值是 倍率^(N-1)，
            // 但存进表里要预除档内终点
            float 台阶值 = Mathf.Pow(每档, 大境 - 1);
            档位.Add(new NpcLevelTier
            {
                大境界 = 大境,
                起点等级 = 大境界到起点等级(大境),
                倍数 = 台阶值 / 档内终点,
                加值 = 每档加值(大境) / 档内终点,
            });
        }
    }

    /// <summary>百分比属性在大境界 N 上累计的加值</summary>
    float 每档加值(int 大境)
    {
        // 第一档不加（1 级就是基准），且加值本身也要有上限感：
        // 用「每档固定步长」而不是几何 —— 百分比属性几何增长会失控
        float 步 = Mathf.Max(暴击每档, Mathf.Max(会心每档, Mathf.Max(闪避每档, 减免每档)));
        return 步 * (大境 - 1);
    }

    /// <summary>把曲线信息拼成一行摘要（日志 / 文档用）</summary>
    public string 曲线摘要()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("每大境界×").Append(每大境界倍率.ToString("0.##"));
        if (档位 != null && 档位.Count > 0)
        {
            sb.Append("｜1级×").Append(档位[0].倍数.ToString("0.##"));
            var 末 = 档位[档位.Count - 1];
            sb.Append("｜").Append(末.起点等级).Append("级×").Append(末.倍数.ToString("0.###E+0"));
        }
        return sb.ToString();
    }
}

/// <summary>
/// **等级补正表的运行时取用口。** 全局只有一张表，缓存起来免得每只怪都去取一遍。
///
/// 【为什么走"塔库"而不是 `Resources.LoadAll&lt;NpcLevelScale&gt;`】
/// 生成的资产在 `Assets/Data/Generated/`，**那个路径不在 Resources 下**，
/// 实测 `Resources.LoadAll` 返回 0 个。所以和任务库 / 对话库 / 面板库一样，
/// 走 `Assets/resources/塔/塔库.asset` 这份**只装引用的聚合资产**。
/// </summary>
public static class NpcLevelScale库
{
    static NpcLevelScale 缓存;

    /// <summary>取等级补正表（找不到返回 null 并只警告一次）</summary>
    public static NpcLevelScale 取()
    {
        if (缓存 != null) return 缓存;

        var 库 = TowerDatabase库.取();
        if (库 != null && 库.等级补正表 != null)
        {
            缓存 = 库.等级补正表;
            return 缓存;
        }

        if (!已警告)
        {
            已警告 = true;
            Debug.LogWarning("[等级补正] 塔库里没有「等级补正表」。"
                             + "跑一次「修仙/从配置表生成资产」（会顺便收集塔库）");
        }
        return null;
    }

    static bool 已警告;

    /// <summary>清缓存（重生成资产 / 重新进 Play 时用）</summary>
    public static void 清缓存() { 缓存 = null; 已警告 = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置静态() { 清缓存(); }
}
