using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **灵田品阶**。1 起，数字越大越好。
/// 决定「能种什么」（灵植有品阶门槛）和「产量多少」。
/// </summary>
public enum 灵田品阶
{
    一阶下品 = 1,
    一阶中品 = 2,
    一阶上品 = 3,
    二阶下品 = 4,
    二阶中品 = 5,
    二阶上品 = 6,
    三阶下品 = 7,
    三阶中品 = 8,
    三阶上品 = 9,
}

/// <summary>灵植的品阶（决定它需要什么品阶的灵田）</summary>
public enum 灵植品阶
{
    凡品 = 1,
    下品 = 2,
    中品 = 3,
    上品 = 4,
    极品 = 5,
}

public static class 灵田品阶说明
{
    public static string 中文名(灵田品阶 p) => p.ToString();
    public static string 中文名(灵植品阶 p) => p.ToString();

    /// <summary>灵田品阶 → 产量倍率。品阶越高，一次收得越多</summary>
    public static float 产量倍率(灵田品阶 田)
    {
        // 每上一阶 +35%，且用小步进避免整数溢出感
        int 级 = Mathf.Max(1, (int)田);
        return 1f + (级 - 1) * 0.35f;
    }

    /// <summary>灵田品阶 → 生长速度倍率（越高越快）</summary>
    public static float 生长倍率(灵田品阶 田)
    {
        int 级 = Mathf.Max(1, (int)田);
        return 1f + (级 - 1) * 0.12f;
    }
}

/// <summary>
/// **一种灵植的静态定义**。用代码内置一份，不依赖配置表 —— 
/// 后续要挪到 CSV 表里时，只要把 <see cref="灵植库.全部"/> 换成读表即可。
/// </summary>
[System.Serializable]
public class 灵植定义
{
    [Tooltip("唯一 id，存档里存的就是它")]
    public string id = "";

    [Tooltip("显示名")]
    public string 名 = "";

    [Tooltip("品阶。灵田品阶必须 >= 它才能种")]
    public 灵植品阶 品阶 = 灵植品阶.凡品;

    [Tooltip("从种下到成熟需要多少**游戏日**")]
    [Min(0.01f)] public float 成熟天数 = 3f;

    [Tooltip("收获数量下限")]
    [Min(1)] public int 产量下限 = 1;

    [Tooltip("收获数量上限（含）")]
    [Min(1)] public int 产量上限 = 3;

    [Tooltip("它长成后产出的物品 id（进背包用）")]
    public string 产物id = "";

    [Tooltip("种下它需要消耗的**种子物品 id**。空 = 不需要种子")]
    public string 种子id = "";

    [Tooltip("外观：茎的高度（米）。越高的草越显眼")]
    public float 株高 = 0.45f;

    [Tooltip("外观：主色")]
    public Color 主色 = new Color(0.36f, 0.72f, 0.34f);

    [Tooltip("外观：成熟时会不会开花")]
    public bool 会开花 = false;

    [Tooltip("外观：花的颜色")]
    public Color 花色 = new Color(0.95f, 0.86f, 0.45f);

    [Tooltip("说明文本（UI 提示用）")]
    [TextArea(1, 3)] public string 说明 = "";
}

/// <summary>
/// **灵植库**。代码内置，单例式只读。
///
/// 以后要挪到 `Assets/Data/Tables/灵植表.csv` 时，
/// 只需要改这里——`灵田` 和 UI 都只通过本类取数据。
/// </summary>
public static class 灵植库
{
    static List<灵植定义> 缓存;

    public static IReadOnlyList<灵植定义> 全部
    {
        get { 确保(); return 缓存; }
    }

    static void 确保()
    {
        if (缓存 != null) return;
        缓存 = new List<灵植定义>
        {
            new 灵植定义
            {
                id = "lingcao_shengling", 名 = "生灵草", 品阶 = 灵植品阶.凡品,
                成熟天数 = 3f, 产量下限 = 2, 产量上限 = 5,
                产物id = "item_shenglingcao", 种子id = "item_seed_shengling", 株高 = 0.5f,
                主色 = new Color(0.42f, 0.80f, 0.40f),
                说明 = "最基础的聚气灵草。灵田初开就能种，无需照看。",
            },
            new 灵植定义
            {
                id = "lingcao_ninglu", 名 = "凝露花", 品阶 = 灵植品阶.凡品,
                成熟天数 = 3f, 产量下限 = 1, 产量上限 = 3,
                产物id = "item_ningluhua", 种子id = "item_seed_ninglu", 株高 = 0.42f,
                主色 = new Color(0.46f, 0.74f, 0.52f), 会开花 = true,
                花色 = new Color(0.78f, 0.90f, 0.98f),
                说明 = "花心凝露，是清障丹的主材。",
            },
            new 灵植定义
            {
                id = "lingcao_qingxin", 名 = "清心莲", 品阶 = 灵植品阶.下品,
                成熟天数 = 5f, 产量下限 = 1, 产量上限 = 3,
                产物id = "item_qingxinlian", 种子id = "item_seed_qingxin", 株高 = 0.55f,
                主色 = new Color(0.38f, 0.70f, 0.66f), 会开花 = true,
                花色 = new Color(0.98f, 0.94f, 0.72f),
                说明 = "需要一阶中品灵田。静心凝神，愈伤丹的辅材。",
            },
            new 灵植定义
            {
                id = "lingcao_chiyan", 名 = "赤焰芝", 品阶 = 灵植品阶.中品,
                成熟天数 = 7f, 产量下限 = 1, 产量上限 = 2,
                产物id = "item_chiyanzhi", 种子id = "", 株高 = 0.38f,
                主色 = new Color(0.82f, 0.34f, 0.22f), 会开花 = false,
                说明 = "需要一阶上品灵田。火性浓烈，炼器淬火的上好辅料。",
            },
        };
    }

    public static 灵植定义 取(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        确保();
        foreach (var d in 缓存) if (d.id == id) return d;
        return null;
    }

    /// <summary>这块田能不能种这种灵植</summary>
    public static bool 可种(灵植定义 植, 灵田品阶 田)
        => 植 != null && (int)田 >= (int)植.品阶;

    /// <summary>随机产量（用定义里的上下限 × 灵田品阶倍率）</summary>
    public static int 掷产量(灵植定义 植, 灵田品阶 田)
    {
        if (植 == null) return 0;
        int 基础 = Random.Range(植.产量下限, 植.产量上限 + 1);
        return Mathf.Max(1, Mathf.RoundToInt(基础 * 灵田品阶说明.产量倍率(田)));
    }
}

/// <summary>
/// **一个灵田格子**的运行时状态。
/// 只存"种了什么 + 长了多久"，成熟与否是算出来的（<see cref="已成熟"/>）——
/// 这样改「成熟天数」或灵田品阶时，已种下的东西也会跟着变，不用逐个刷数据。
/// </summary>
[System.Serializable]
public class 灵田格
{
    /// <summary>种的灵植 id。空 = 这个格子是空的</summary>
    public string 种子id = "";

    /// <summary>已经生长了多少**游戏日**（按灵田生长倍率算过的等效值）</summary>
    public float 已生长天数 = 0f;

    /// <summary>这一茬收获过了吗（收完要清空格子）</summary>
    public bool 已收获 = false;

    public bool 是空的 => string.IsNullOrEmpty(种子id);

    public 灵植定义 植 => 灵植库.取(种子id);

    /// <summary>是否已经成熟可收。**成熟天数是灵植自己的，不乘灵田倍率** ——
    /// 灵田品阶影响的是"长得快不快"，在累计生长量时就已经乘进去了</summary>
    public bool 已成熟
    {
        get
        {
            var d = 植;
            return d != null && !已收获 && 已生长天数 >= d.成熟天数;
        }
    }

    /// <summary>生长进度 0~1（给 UI 画进度条）</summary>
    public float 进度
    {
        get
        {
            var d = 植;
            if (d == null || d.成熟天数 <= 0f) return 0f;
            return Mathf.Clamp01(已生长天数 / d.成熟天数);
        }
    }

    /// <summary>生长阶段 0=空 1=幼苗 2=成长 3=成熟</summary>
    public int 阶段
    {
        get
        {
            if (是空的) return 0;
            if (已成熟) return 3;
            return 进度 < 0.5f ? 1 : 2;
        }
    }

    public void 清空()
    {
        种子id = "";
        已生长天数 = 0f;
        已收获 = false;
    }

    // ---- 存档 ----
    public string 导出() => 是空的 ? "-" : $"{种子id}|{已生长天数:F3}|{(已收获 ? 1 : 0)}";

    public static 灵田格 导入(string s)
    {
        var g = new 灵田格();
        if (string.IsNullOrEmpty(s) || s == "-") return g;
        var 段 = s.Split('|');
        if (段.Length < 2) return g;
        g.种子id = 段[0];
        float v;
        if (float.TryParse(段[1], out v)) g.已生长天数 = v;
        if (段.Length >= 3) g.已收获 = 段[2] == "1";
        // 种子的定义没了（改过表）→ 当作空格子，免得留一个永远长不出来的僵尸
        if (灵植库.取(g.种子id) == null) g.清空();
        return g;
    }
}
