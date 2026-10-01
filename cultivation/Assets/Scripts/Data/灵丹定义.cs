using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **丹药吃下去干什么**。
///
/// 【为什么要分开】`破境` 是一种"吃了提升破境成功率"的丹，
/// 一阶基础丹（聚气 / 清障 / 愈伤）是**直接生效**的消耗品，
/// 回春丹那类"临时属性增益"的效果**不由这里管**（它挂在物品自己的使用效果上）。
/// 三者的消费入口完全不同，所以用显式枚举区分，不要靠"有没有主材"之类间接信号猜。
/// </summary>
public enum 灵丹作用
{
    无,       // 不由 `服丹效果` 处理：效果写在物品自己的「使用效果」上（例：回春丹 = 属性增益）
    破境,     // 服下 → 提升「当前破境成功率」（见 PlayerCultivation.破境加成）
    加修为,   // 聚气丹：直接加总灵气（"丹药辅助修炼、快速提升修为"）
    回气血,   // 愈伤丹（瞬回，和回春丹的"缓回增益"是两回事）
    回灵力,   // 清障丹（疏通灵力淤塞）
}

/// <summary>
/// **一种丹药的静态定义**。既是"丹方"，也是"这颗丹本身的资料"。
///
/// 【为什么代码内置】和 <see cref="灵植库"/> 一个理由：炼丹表还没定，先把这一套跑通；
/// 以后要挪到 `Assets/Data/Tables/灵丹表.csv` 时**只改本类**，外面一行都不用动。
/// </summary>
[System.Serializable]
public class 灵丹定义
{
    [Tooltip("唯一 id")]
    public string id = "";

    [Tooltip("显示名")]
    public string 名 = "";

    /// <summary>
    /// **丹药的品（1~9）** —— 丹药自己的等级，表示**珍贵程度**，**和境界对应**。
    ///
    /// 用户 2026-10-01 明确定义：
    /// > 「筑基丹是 2 品丹药，邪神究极无敌狂霸破灭神丹是 9 品丹药，丹药的品阶只是说明
    /// >   丹药珍贵程度的东西，对应境界」
    ///
    /// ⇒ 所以它是**这颗丹的固有属性**，**不是"每炉选几品"**：
    /// 炼气破境丹 = 1 品、筑基 = 2 品 …… 登仙 = 9 品；一阶基础丹（聚气/清障/愈伤/回春）= 1 品。
    /// **品越高 = 越稀有 = 越难炼**（见 <see cref="成功率"/>），这是"不同丹之间"的差别。
    ///
    /// > 【曾经的错误做法·别再走回去】原来这里有一组 `最高可炼品` + `品阶衰减` + `品阶加耗`，
    /// > 让玩家**同一炉里选 1~9 品**，选高品则成功率下降、耗气上升，而**产出的物品 id 完全一样**
    /// > —— 品根本没被记下来，"选品"这个动作对结果没有任何实际意义。
    /// </summary>
    [Range(1, 9)] public int 品 = 1;

    [Tooltip("**对应的大境界**。喂错境界的丹不生效 —— 免得拿一阶丹刷满 90 级")]
    public RealmTier 对应大境界 = RealmTier.未指定;

    [Tooltip("品阶（`QualityTier`，物品表那一列的通用品质档，只用于显示与排序）")]
    public QualityTier 品阶 = QualityTier.凡品;

    [Tooltip("说明文本")]
    [TextArea(1, 3)] public string 说明 = "";

    // ============================================================ 丹方
    //
    // 需求原文：
    //   「炼丹需要对应材料以及消耗灵气，每种丹方都有成功概率以及 1-9 品的品质，
    //     品质越高成功率越低。炼丹通常需要一味主材和多味辅材。」
    //
    // ⇒ 只有 主材id 非空的才算"可炼的丹方"。

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

    [Tooltip("**成丹率**（0~1）。品越高的丹这个值越低（越难炼）")]
    [Range(0.05f, 1f)] public float 成功率 = 0.80f;

    // ---- 丹药作用（一阶基础丹方 / 破境丹）----

    [Header("丹药作用")]
    [Tooltip("这味丹吃下去干什么。默认「无」= 效果写在物品自己的使用效果上（最安全）")]
    public 灵丹作用 作用 = 灵丹作用.无;

    [Tooltip("作用数值：加修为 = 总灵气点数；回气血 / 回灵力 = 回复点数（破境 / 无 不用）")]
    [Min(0)] public int 作用数值 = 0;

    /// <summary>是不是一个可炼制的丹方</summary>
    public bool 是丹方 => !string.IsNullOrEmpty(主材id) && !string.IsNullOrEmpty(id);

    /// <summary>产出数量</summary>
    public int 掷产量() => Random.Range(产量下限, 产量上限 + 1);
}

/// <summary>
/// **灵丹库**（代码内置，只读）。
///
/// 三类：
///   · **9 颗破境丹** —— 每大境界一颗，**品 = 1~9**（对应境界）。**可以吃**，
///     服下把「当前破境成功率」顶到 `RealmDefinition.服丹成功率`。
///   · **4 味一阶基础丹** —— 聚气丹（加修为）/ 清障丹（回灵力）/ 愈伤丹（回气血）/ 回春丹（属性增益）。
///     品 = 1，只用**一阶下品灵田就能种**的灵草。
///   · **大境界突破的专属丹药** —— 用户说"后续再说"，所以**故意不做**，缺了会明确报错。
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
            // ============================================================ 一阶基础丹（品 = 1）
            new 灵丹定义 { id = "item_dan_juqi", 名 = "聚气丹", 品 = 1, 作用 = 灵丹作用.加修为, 作用数值 = 25,
                主材id = "item_shenglingcao", 主材数量 = 2, 辅材 = "item_ningluhua:1",
                灵气消耗 = 8, 成功率 = 0.90f,
                品阶 = QualityTier.凡品, 说明 = "聚敛草木灵气，服下直入丹田。一阶最常用的修炼辅丹。" },
            new 灵丹定义 { id = "item_dan_qingzhang", 名 = "清障丹", 品 = 1, 作用 = 灵丹作用.回灵力, 作用数值 = 30,
                主材id = "item_ningluhua", 主材数量 = 2, 辅材 = "item_shenglingcao:1",
                灵气消耗 = 8, 成功率 = 0.90f,
                品阶 = QualityTier.凡品, 说明 = "花露通脉，扫去灵力淤塞。服下迅速回灵。" },
            new 灵丹定义 { id = "item_dan_yushang", 名 = "愈伤丹", 品 = 1, 作用 = 灵丹作用.回气血, 作用数值 = 80,
                主材id = "item_shenglingcao", 主材数量 = 1, 辅材 = "item_ningluhua:2",
                灵气消耗 = 8, 成功率 = 0.90f,
                品阶 = QualityTier.凡品, 说明 = "生肌止血的入门伤药，**瞬回**气血。和回春丹（缓回增益）并存。" },
            new 灵丹定义 { id = "item_huichundan", 名 = "回春丹", 品 = 1, 作用 = 灵丹作用.无,
                主材id = "item_huichuncao", 主材数量 = 2, 辅材 = "item_ningluhua:1",
                灵气消耗 = 10, 成功率 = 0.88f,
                品阶 = QualityTier.凡品,
                说明 = "温养气血的常备丹。**效果写在物品的使用效果上**（属性增益：气血回复加成，持续一段时间）。" },

            // ============================================================ 破境丹（品 = 1~9，对应 9 大境界）
            new 灵丹定义 { id = "item_dan_pojing_lianqi", 名 = "炼气破境丹", 品 = 1, 对应大境界 = RealmTier.炼气, 作用 = 灵丹作用.破境,
                主材id = "item_shenglingcao", 主材数量 = 3, 辅材 = "item_ningluhua:2", 灵气消耗 = 20, 成功率 = 0.85f,
                品阶 = QualityTier.凡品, 说明 = "助炼气期修士冲开一层小境界。炼气期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_zhuji", 名 = "筑基破境丹", 品 = 2, 对应大境界 = RealmTier.筑基, 作用 = 灵丹作用.破境,
                主材id = "item_ningluhua", 主材数量 = 3, 辅材 = "item_qingxinlian:2", 灵气消耗 = 45, 成功率 = 0.80f,
                品阶 = QualityTier.凡品, 说明 = "助筑基期修士稳固根基、冲开小境界。筑基期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_jindan", 名 = "金丹破境丹", 品 = 3, 对应大境界 = RealmTier.金丹, 作用 = 灵丹作用.破境,
                主材id = "item_qingxinlian", 主材数量 = 3, 辅材 = "item_chiyanzhi:2", 灵气消耗 = 90, 成功率 = 0.75f,
                品阶 = QualityTier.黄品, 说明 = "金丹期破境之用，药力醇厚。金丹期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_yuanying", 名 = "元婴破境丹", 品 = 4, 对应大境界 = RealmTier.元婴, 作用 = 灵丹作用.破境,
                品阶 = QualityTier.玄品, 主材id = "item_chiyanzhi", 主材数量 = 4, 辅材 = "item_qingxinlian:3", 灵气消耗 = 160, 成功率 = 0.70f, 说明 = "温养元婴、冲击瓶颈。元婴期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_huashen", 名 = "化神破境丹", 品 = 5, 对应大境界 = RealmTier.化神, 作用 = 灵丹作用.破境,
                品阶 = QualityTier.玄品, 主材id = "item_chiyanzhi", 主材数量 = 5, 辅材 = "item_qingxinlian:4", 灵气消耗 = 280, 成功率 = 0.66f, 说明 = "凝练神识、破开关隘。化神期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_lianxu", 名 = "炼虚破境丹", 品 = 6, 对应大境界 = RealmTier.炼虚, 作用 = 灵丹作用.破境,
                品阶 = QualityTier.地品, 主材id = "item_chiyanzhi", 主材数量 = 6, 辅材 = "item_qingxinlian:5", 灵气消耗 = 480, 成功率 = 0.62f, 说明 = "虚空练形，破境之资。炼虚期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_heti", 名 = "合体破境丹", 品 = 7, 对应大境界 = RealmTier.合体, 作用 = 灵丹作用.破境,
                品阶 = QualityTier.地品, 主材id = "item_chiyanzhi", 主材数量 = 7, 辅材 = "item_qingxinlian:6", 灵气消耗 = 800, 成功率 = 0.58f, 说明 = "天人合一之机，破境之钥。合体期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_dacheng", 名 = "大乘破境丹", 品 = 8, 对应大境界 = RealmTier.大乘, 作用 = 灵丹作用.破境,
                品阶 = QualityTier.天品, 主材id = "item_chiyanzhi", 主材数量 = 8, 辅材 = "item_qingxinlian:7", 灵气消耗 = 1300, 成功率 = 0.54f, 说明 = "大乘之资，世间罕有。大乘期专用。" },
            new 灵丹定义 { id = "item_dan_pojing_dengxian", 名 = "登仙破境丹", 品 = 9, 对应大境界 = RealmTier.登仙, 作用 = 灵丹作用.破境,
                品阶 = QualityTier.仙品, 主材id = "item_chiyanzhi", 主材数量 = 9, 辅材 = "item_qingxinlian:8", 灵气消耗 = 2200, 成功率 = 0.50f, 说明 = "传说中可窥仙门一隙。登仙期专用。" },
        };
    }

    public static 灵丹定义 取(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        确保();
        foreach (var d in 缓存) if (d.id == id) return d;
        return null;
    }

    /// <summary>取某个大境界对应的破境丹。没有就返回 null（大境界专属丹故意没做）</summary>
    public static 灵丹定义 取破境丹(RealmTier 大境界)
    {
        确保();
        foreach (var d in 缓存) if (d.作用 == 灵丹作用.破境 && d.对应大境界 == 大境界) return d;
        return null;
    }
}

/// <summary>
/// **破境结果**。给 UI 显示用 —— 玩家必须知道"为什么失败"。
/// </summary>
public struct 破境结果
{
    /// <summary>整件事成不成立（灵气够不够、有没有满级）</summary>
    public bool 受理;
    /// <summary>受理的话，突破成功了吗</summary>
    public bool 成功;
    /// <summary>这次用的成功率（0~1）。显示给玩家看，让概率是透明的</summary>
    public float 成功率;
    /// <summary>这次有没有嗑丹加成（影响展示文案）</summary>
    public bool 服了丹;
    /// <summary>给玩家看的话</summary>
    public string 文本;

    public static 破境结果 拒绝(string 原因)
        => new 破境结果 { 受理 = false, 成功 = false, 成功率 = 0f, 文本 = 原因 };
}
