using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// **日常循环** —— 把"一天该做的事"串成一圈（第四阶段的地基）。
///
/// ## 一天里的四件事
///
/// | 项 | 怎么算完成 | 挂在哪 |
/// |---|---|---|
/// | 收灵草 | `灵田.收获` 事件 | 订阅（不轮询） |
/// | 炼丹 | `炼丹炉.炼制完成` 里 `结果.成功` | 订阅（不轮询） |
/// | 打坐修炼 | `时间管理器.今日次数 > 0` | 轮询（`今日次数` 每天自己归零） |
/// | 进镇妖塔 | 当前场景名 == <see cref="塔场景名"/> | 轮询 |
///
/// **前两件用订阅、后两件用轮询**不是随手写的：
/// · 收获 / 炼制本来就有事件，订阅最准（失败的一炉不算）；
/// · "打坐"和"进塔"没有对应的事件，但它们的状态**本来就在别处维护**
///   （`时间管理器.今日次数` 每天归零、场景名每帧都能问）—— 再引一层事件反而多一处会断的线。
///
/// ## 两条硬规矩
///
/// · **订阅要"补齐"**：`灵田.取()` / `炼丹炉.取()` 在切场景后会换成新实例，
///   而 `Update` 里的 <see cref="补齐订阅"/> 每帧比对，换了就重新订一次
///   （踩坑总库 B 段那类"Start 订阅 + OnDestroy 退订追不上重建"的坑，在这条路上绕开）。
/// · **跨天只认 `时间管理器.当前天数` 变没变**，不订阅 `过了一天` ——
///   那个事件只在时间推进时发，读档进来不补发；天数比对两条路都覆盖。
///
/// 清单本身**进存档**（天 + 一个位掩码），所以读档回到同一天时勾还在。
/// </summary>
public class 日常循环 : MonoBehaviour
{
    public enum 项 { 收灵草 = 0, 炼丹 = 1, 打坐 = 2, 入塔 = 3 }

    public static readonly 项[] 全部项 = { 项.收灵草, 项.炼丹, 项.打坐, 项.入塔 };

    static 日常循环 实例;
    public static 日常循环 取() => 实例;

    [Tooltip("视为「镇妖塔」的场景名。进过这个场景就算完成「入塔」。")]
    public string 塔场景名 = "Demon-Suppressing Tower";

    /// <summary>清单有任何变化（跨天、勾上一项）都会发。今日面板订阅它。</summary>
    public event System.Action 变化;

    readonly HashSet<项> 今日完成 = new HashSet<项>();
    int 记录天数 = int.MinValue;
    灵田 订阅的田;
    炼丹炉 订阅的炉;

    public bool 已完成(项 x) => 今日完成.Contains(x);
    public int 今日完成数 => 今日完成.Count;
    public int 今日总项数 => 全部项.Length;
    public bool 今日事毕 => 今日完成.Count >= 全部项.Length;
    public int 记录的那一天 => 记录天数;

    public static string 项名(项 x)
    {
        switch (x)
        {
            case 项.收灵草: return "收一次灵草";
            case 项.炼丹: return "炼一炉丹";
            case 项.打坐: return "打坐修炼一次";
            default: return "进一次镇妖塔";
        }
    }

    void Awake() { 实例 = this; }

    void OnDestroy()
    {
        if (实例 == this) 实例 = null;
        退订();
    }

    void Update()
    {
        var t = 时间管理器.取();
        if (t != null && t.当前天数 != 记录天数)
        {
            bool 首次 = 记录天数 == int.MinValue;
            记录天数 = t.当前天数;
            今日完成.Clear();
            if (!首次)
            {
                Debug.Log("[日常] 新的一天 —— " + t.纪年文本 + "，今日清单已刷新");
                ToastUI.提示("新的一天");
            }
            变化?.Invoke();
        }

        补齐订阅();
        轮询();
    }

    void 补齐订阅()
    {
        var 田 = 灵田.取();
        if (田 != 订阅的田)
        {
            if (订阅的田 != null) 订阅的田.收获 -= 收到灵草;
            订阅的田 = 田;
            if (订阅的田 != null) 订阅的田.收获 += 收到灵草;
        }

        var 炉 = 炼丹炉.取();
        if (炉 != 订阅的炉)
        {
            if (订阅的炉 != null) 订阅的炉.炼制完成 -= 炼完一炉;
            订阅的炉 = 炉;
            if (订阅的炉 != null) 订阅的炉.炼制完成 += 炼完一炉;
        }
    }

    void 退订()
    {
        if (订阅的田 != null) 订阅的田.收获 -= 收到灵草;
        if (订阅的炉 != null) 订阅的炉.炼制完成 -= 炼完一炉;
        订阅的田 = null;
        订阅的炉 = null;
    }

    void 收到灵草(string 产物, int 量) => 记下(项.收灵草);

    void 炼完一炉(炼丹结果 结果)
    {
        if (结果.成功) 记下(项.炼丹);
    }

    void 轮询()
    {
        var t = 时间管理器.取();
        if (t != null && t.今日次数 > 0) 记下(项.打坐);

        if (!string.IsNullOrEmpty(塔场景名) && SceneManager.GetActiveScene().name == 塔场景名) 记下(项.入塔);
    }

    void 记下(项 x)
    {
        if (!今日完成.Add(x)) return;

        Debug.Log("[日常] 完成「" + 项名(x) + "」→ " + 今日完成.Count + "/" + 全部项.Length);
        if (今日事毕)
        {
            var t = 时间管理器.取();
            ToastUI.提示("今日事毕 —— " + (t != null ? t.纪年文本 : ""));
        }
        变化?.Invoke();
    }

    // ============================================================ 存档

    public void 导出(out int 天, out int 掩码)
    {
        天 = 记录天数;
        掩码 = 0;
        foreach (var x in 今日完成) 掩码 |= 1 << (int)x;
    }

    public void 导入(int 天, int 掩码)
    {
        记录天数 = 天;
        今日完成.Clear();
        foreach (var x in 全部项)
            if ((掩码 & (1 << (int)x)) != 0) 今日完成.Add(x);
        变化?.Invoke();
    }
}
