using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **一种丹药的静态定义**。现在只服务"破境丹"，以后炼丹房（第二阶段）会长在这里。
///
/// 【为什么先做代码内置】和 <see cref="灵植库"/> 一个理由：
/// 炼丹表还没定，先把"破境丹"这一味跑通；以后要挪到
/// `Assets/Data/Tables/灵丹表.csv` 时**只改本类**，外面一行都不用动。
/// </summary>
[System.Serializable]
public class 灵丹定义
{
    [Tooltip("唯一 id，背包物品 id 也用它")]
    public string id = "";

    [Tooltip("显示名")]
    public string 名 = "";

    [Tooltip("**对应的大境界**。喂错境界的丹不生效 —— 免得拿一阶丹刷满 90 级")]
    public RealmTier 对应大境界 = RealmTier.未指定;

    [Tooltip("品阶（和 QualityTier 一致，仅用于显示与排序）")]
    public QualityTier 品阶 = QualityTier.凡品;

    [Tooltip("说明文本")]
    [TextArea(1, 3)] public string 说明 = "";

    // ============================================================ 丹方（用户 2026-10-01 需求）
    //
    // 需求原文：
    //   「炼丹需要对应材料以及消耗灵气，每种丹方都有成功概率以及 1-9 品的品质，
    //     品质越高成功率越低。炼丹通常需要一味主材和多味辅材。」
    //
    // ⇒ 只有 主材id 非空的才算"可炼的丹方"；破境丹也走同一套数据。

    [Header("丹方（留空 主材id = 不可炼制）")]
    [Tooltip("**主材**物品 id（一味）")]
    public string 主材id = "";

    [Tooltip("主材数量")]
    [Min(1)] public int 主材数量 = 1;

    [Tooltip("**辅材**：格式 `物品id:数量|物品id:数量`（多味）")]
    public string 辅材 = "";

    [Tooltip("炼丹消耗的**灵气**（走 PlayerVitals.当前灵气）")]
    [Min(0)] public int 灵气消耗 = 20;

    [Tooltip("产出数量下限")]
    [Min(1)] public int 产量下限 = 1;

    [Tooltip("产出数量上限")]
    [Min(1)] public int 产量上限 = 1;

    // ---- 1~9 品品质 ----
    //
    // 需求：「品质越高成功率越低」。
    // 所以每开一品，成功率乘一次 品阶衰减，灵气消耗乘一次 品阶加耗。
    // 这两条能保证"高品 = 更难 + 更贵"，且不用手填 9 组数。

    [Tooltip("**一品成功率**（0~1）。这是最高成功率，往上每品都更难")]
    [Range(0.05f, 1f)] public float 一品成功率 = 0.80f;

    [Tooltip("每往上开一品的**成功率衰减系数**。0.82 ⇒ 九品只有一品的 0.82^8 ≈ 0.20 倍")]
    [Range(0.3f, 1f)] public float 品阶衰减 = 0.82f;

    [Tooltip("每往上开一品的**灵气加耗系数**。1.35 ⇒ 九品要比一品多花 ~10 倍灵气")]
    [Range(1f, 3f)] public float 品阶加耗 = 1.35f;

    [Tooltip("能炼到的最高品（1~9）。低阶丹方炼不出高品")]
    [Range(1, 9)] public int 最高可炼品 = 9;

    /// <summary>是不是一个可炼制的丹方</summary>
    public bool 是丹方 => !string.IsNullOrEmpty(主材id) && !string.IsNullOrEmpty(id);

    /// <summary>第 N 品的成功率（1~9）</summary>
    public float 成功率(int 品)
    {
        int p = Mathf.Clamp(品, 1, Mathf.Clamp(最高可炼品, 1, 9));
        return Mathf.Clamp(一品成功率 * Mathf.Pow(品阶衰减, p - 1), 0.01f, 1f);
    }

    /// <summary>第 N 品的灵气消耗（1~9）</summary>
    public int 耗灵气(int 品)
    {
        int p = Mathf.Clamp(品, 1, Mathf.Clamp(最高可炼品, 1, 9));
        return Mathf.Max(1, Mathf.RoundToInt(灵气消耗 * Mathf.Pow(品阶加耗, p - 1)));
    }

    /// <summary>第 N 品的产量上限（高品同样数量，但"品质"体现在产出的丹本身）</summary>
    public int 掷产量() => Random.Range(产量下限, 产量上限 + 1);
}

/// <summary>
/// **灵丹库**（代码内置，只读）。
///
/// 目前只有 9 颗**破境丹** —— 每大境界一颗，用于**小境界突破**（1→2 … 9→10）。
///
/// ## 破境丹的作用（用户 2026-10-01 定）
///
/// 小境界突破**有概率**，失败要**重新刷一整个小境界的经验**。
/// 服下**对应大境界**的破境丹能把成功率从 `基础成功率` 抬到 `服丹成功率`
/// （两个值都在 <see cref="RealmDefinition"/> 上逐级配）。
///
/// > **大境界突破**（10→下一境界1）需要**专属丹药**，用户说"后续再说"，
/// > 所以这里**故意不做**大境界丹。缺了会明确报错，不会静默白送。
/// </summary>
public static class 灵丹库
{
    static List<灵丹定义> 缓存;

    public static IReadOnlyList<灵丹定义> 全部
    {
        get { 确保(); return 缓存; }
    }

    static void 确保()
    {
        if (缓存 != null) return;
        缓存 = new List<灵丹定义>
        {
            new 灵丹定义 { id = "item_dan_pojing_lianqi", 名 = "炼气破境丹", 对应大境界 = RealmTier.炼气, 主材id = "item_shenglingcao", 主材数量 = 3, 辅材 = "item_ningluhua:2", 灵气消耗 = 20, 一品成功率 = 0.85f, 最高可炼品 = 9,
                品阶 = QualityTier.凡品, 说明 = "助炼气期修士冲开一层小境界。炼气期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_zhuji", 名 = "筑基破境丹", 对应大境界 = RealmTier.筑基, 主材id = "item_ningluhua", 主材数量 = 3, 辅材 = "item_qingxinlian:2", 灵气消耗 = 45, 一品成功率 = 0.80f, 最高可炼品 = 9,
                品阶 = QualityTier.凡品, 说明 = "助筑基期修士稳固根基、冲开小境界。筑基期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_jindan", 名 = "金丹破境丹", 对应大境界 = RealmTier.金丹, 主材id = "item_qingxinlian", 主材数量 = 3, 辅材 = "item_chiyanzhi:2", 灵气消耗 = 90, 一品成功率 = 0.75f, 最高可炼品 = 9,
                品阶 = QualityTier.黄品, 说明 = "金丹期破境之用，药力醇厚。金丹期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_yuanying", 名 = "元婴破境丹", 对应大境界 = RealmTier.元婴,
                品阶 = QualityTier.玄品, 主材id = "item_chiyanzhi", 主材数量 = 4, 辅材 = "item_qingxinlian:3", 灵气消耗 = 160, 一品成功率 = 0.70f, 最高可炼品 = 9, 说明 = "温养元婴、冲击瓶颈。元婴期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_huashen", 名 = "化神破境丹", 对应大境界 = RealmTier.化神,
                品阶 = QualityTier.玄品, 主材id = "item_chiyanzhi", 主材数量 = 5, 辅材 = "item_qingxinlian:4", 灵气消耗 = 280, 一品成功率 = 0.66f, 最高可炼品 = 9, 说明 = "凝练神识、破开关隘。化神期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_lianxu", 名 = "炼虚破境丹", 对应大境界 = RealmTier.炼虚,
                品阶 = QualityTier.地品, 主材id = "item_chiyanzhi", 主材数量 = 6, 辅材 = "item_qingxinlian:5", 灵气消耗 = 480, 一品成功率 = 0.62f, 最高可炼品 = 9, 说明 = "虚空练形，破境之资。炼虚期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_heti", 名 = "合体破境丹", 对应大境界 = RealmTier.合体,
                品阶 = QualityTier.地品, 主材id = "item_chiyanzhi", 主材数量 = 7, 辅材 = "item_qingxinlian:6", 灵气消耗 = 800, 一品成功率 = 0.58f, 最高可炼品 = 9, 说明 = "天人合一之机，破境之钥。合体期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_dacheng", 名 = "大乘破境丹", 对应大境界 = RealmTier.大乘,
                品阶 = QualityTier.天品, 主材id = "item_chiyanzhi", 主材数量 = 8, 辅材 = "item_qingxinlian:7", 灵气消耗 = 1300, 一品成功率 = 0.54f, 最高可炼品 = 9, 说明 = "大乘之资，世间罕有。大乘期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_dengxian", 名 = "登仙破境丹", 对应大境界 = RealmTier.登仙,
                品阶 = QualityTier.仙品, 主材id = "item_chiyanzhi", 主材数量 = 9, 辅材 = "item_qingxinlian:8", 灵气消耗 = 2200, 一品成功率 = 0.50f, 最高可炼品 = 9, 说明 = "传说中可窥仙门一隙。登仙期专用。" },
        };
    }

    public static 灵丹定义 取(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        确保();
        foreach (var d in 缓存) if (d.id == id) return d;
        return null;
    }

    /// <summary>取某个大境界对应的破境丹。没有就返回 null（大境界丹故意没做）</summary>
    public static 灵丹定义 取破境丹(RealmTier 大境界)
    {
        确保();
        foreach (var d in 缓存) if (d.对应大境界 == 大境界) return d;
        return null;
    }
}

/// <summary>
/// **破境结果**。给 UI 显示用 —— 玩家必须知道"为什么失败"。
/// </summary>
public struct 破境结果
{
    /// <summary>整件事成不成立（灵气够不够、物品对不对、有没有满级）</summary>
    public bool 受理;
    /// <summary>受理的话，突破成功了吗</summary>
    public bool 成功;
    /// <summary>这次用的成功率（0~1）。显示给玩家看，让概率是透明的</summary>
    public float 成功率;
    /// <summary>有没有嗑丹（影响展示文案）</summary>
    public bool 服了丹;
    /// <summary>给玩家看的话</summary>
    public string 文本;

    public static 破境结果 拒绝(string 原因)
        => new 破境结果 { 受理 = false, 成功 = false, 成功率 = 0f, 文本 = 原因 };
}
