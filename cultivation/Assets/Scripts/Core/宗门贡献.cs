using UnityEngine;

/// <summary>
/// **宗门贡献** —— 宗门里的通用货币（换灵田开拓令 / 升阶令 / 练功木桩这些"没有别处来源"的东西）。
///
/// ## 为什么是一个静态类，不是单例组件
///
/// 和 <see cref="对话标记"/>、`TowerProgress` 同一类：**跨场景要活着的纯数据**。
/// 挂在物件上就得操心"哪个场景有、谁先创建、切场景会不会丢"；
/// 静态 + 进存档反而是最省事也最不容易错的做法（本工程既有做法）。
///
/// ## 谁会改它
///
/// · 打塔：<see cref="TowerController"/> 每清空一波给一点（塔是"长线刷贡献"的正路）
/// · 任务：任务表 `奖励贡献` 列（阶段完成时发）
/// · 兑换：<see cref="功德堂兑换"/> 扣
///
/// ⚠️ 任何"加"都要走 <see cref="加"/>，它会广播 <see cref="变化"/> ——
/// 界面靠它实时刷新（不然玩家兑换完看到的还是旧数字，本工程在别处踩过同款坑）。
/// </summary>
public static class 宗门贡献
{
    /// <summary>当前贡献</summary>
    public static int 当前 { get; private set; }

    /// <summary>贡献变了（参数：新值）。界面订阅它刷新</summary>
    public static event System.Action<int> 变化;

    /// <summary>加贡献（原因只用于日志）</summary>
    public static void 加(int 点数, string 原因 = null)
    {
        if (点数 == 0) return;
        当前 = Mathf.Max(0, 当前 + 点数);
        if (!string.IsNullOrEmpty(原因))
            Debug.Log("[宗门贡献] +" + 点数 + "（" + 原因 + "）→ " + 当前);
        变化?.Invoke(当前);
    }

    /// <summary>够不够</summary>
    public static bool 够(int 点数) => 当前 >= 点数;

    /// <summary>扣贡献；不够返回 false（**不扣**）</summary>
    public static bool 扣(int 点数, string 原因 = null)
    {
        if (!够(点数)) return false;
        当前 -= 点数;
        if (!string.IsNullOrEmpty(原因))
            Debug.Log("[宗门贡献] -" + 点数 + "（" + 原因 + "）→ " + 当前);
        变化?.Invoke(当前);
        return true;
    }

    /// <summary>读档用（不广播日志，避免读档刷屏）</summary>
    public static void 从存档设置(int 值)
    {
        当前 = Mathf.Max(0, 值);
        变化?.Invoke(当前);
    }

    /// <summary>不重启 Unity 就串局 —— 每局开始清空（和别的静态状态一个规矩）</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置() { 当前 = 0; 变化 = null; }

    // ---- ASCII 别名 ----
    public static int Current => 当前;
    public static void Add(int points, string reason = null) => 加(points, reason);
    public static bool Spend(int points, string reason = null) => 扣(points, reason);
}
