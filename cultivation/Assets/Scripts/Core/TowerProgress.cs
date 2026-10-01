using UnityEngine;

/// <summary>
/// **镇妖塔进度**（爬到第几层）的运行时载体 + 落盘。
///
/// ## 为什么单独一层
///
/// 用户要求「镇妖塔的等级应该是可以被记录的」——层数要跨场景、跨读档都还在。
/// 但层数**不属于玩家角色**（不是属性、不是背包），所以：
///
/// · **内存里**用 static 保存 → 切场景（<c>LoadSceneMode.Single</c> 会销毁一切）后还在
/// · **落盘**走 <see cref="SaveData"/> 的 <c>镇妖塔当前层</c> / <c>镇妖塔最高层</c>
///
/// ## 必须 static 的理由
///
/// 见 `docs/ai/踩坑总库.md` §八：
/// **实例状态活不过切场景**。`主线开场.演过` 当初写成实例字段，
/// 结果从宗门回古古镇整个开场重演一遍。层数同理 —— 写成实例字段就每次进塔都归零。
/// </summary>
public static class TowerProgress
{
    /// <summary>当前所在层（1 起）</summary>
    public static int 当前层 { get; private set; } = 1;

    /// <summary>历史最高层（1 起）。给排行榜 / 「你已爬到第 N 层」用</summary>
    public static int 最高层 { get; private set; } = 1;

    /// <summary>进度变化（参数：当前层、最高层）</summary>
    public static event System.Action<int, int> 变化;

    /// <summary>记一笔（不落盘）</summary>
    public static void 记录(int 当前, int 最高)
    {
        当前层 = Mathf.Max(1, 当前);
        最高层 = Mathf.Max(最高层, Mathf.Max(1, 最高), 当前层);
        变化?.Invoke(当前层, 最高层);
    }

    /// <summary>从存档读进来</summary>
    public static void 从存档读(SaveData 数据)
    {
        if (数据 == null) return;
        当前层 = Mathf.Max(1, 数据.镇妖塔当前层);
        最高层 = Mathf.Max(当前层, Mathf.Max(1, 数据.镇妖塔最高层));
        变化?.Invoke(当前层, 最高层);
    }

    /// <summary>写进存档对象（<see cref="SaveSystem.从角色采集"/> 时调）</summary>
    public static void 写进存档(SaveData 数据)
    {
        if (数据 == null) return;
        数据.镇妖塔当前层 = 当前层;
        数据.镇妖塔最高层 = 最高层;
    }

    /// <summary>新档：回到第 1 层</summary>
    public static void 重置()
    {
        当前层 = 1;
        最高层 = 1;
        变化?.Invoke(当前层, 最高层);
    }

    /// <summary>
    /// **把进度落盘到当前存档。**
    ///
    /// 用「读出来 → 只改层数 → 写回去」而不是 `从角色采集`：
    /// 塔里存档时玩家可能正在死亡流程里（气血 0），
    /// 全量采集会把「0 血」写进档，读档起来就是个死人。
    /// </summary>
    public static bool 落盘(int 槽位 = 0)
    {
        var 数据 = SaveSystem.读档(槽位);
        if (数据 == null) return false;         // 还没开过档 —— 不凭空造一个

        写进存档(数据);
        return SaveSystem.存档(槽位, 数据);
    }

    /// <summary>重新进 Play 时归零（静态字段不会自己清）</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置静态()
    {
        当前层 = 1;
        最高层 = 1;
        变化 = null;
    }
}
