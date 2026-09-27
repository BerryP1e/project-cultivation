using UnityEngine;

/// <summary>
/// **使用一件物品时的上下文**：谁在用、背包数据在哪、用的是哪一件。
/// 效果脚本只认这个结构，不直接去找玩家/面板，方便以后从别的地方（任务奖励、NPC 给予）复用。
/// </summary>
public struct 物品使用请求
{
    public UIPanelData 面板;
    public GameObject 玩家;
    public ItemDefinition 物品;

    public string 物品名 => 物品 != null ? 物品.DisplayName : "";
}

/// <summary>
/// **物品使用效果**基类。
///
/// 设定（用户 2026-09-26）：物品只分「**能使用的**」和「**不能使用的**（材料、提交物）」。
/// 能使用的物品 = 在背包页多出一个「使用」按钮 + **挂一个专门的效果脚本** —— 就是本类。
///
/// 为什么是 ScriptableObject 而不是 MonoBehaviour：
///   · 效果是**数据**（学会哪门功法、加多少百分比、持续多久），做成资产就能在 Inspector 里配、能复用；
///   · 一件物品一个效果资产，互不干扰，也不用为了每种效果写一个预制体。
///
/// 加新效果 = 新写一个子类（`[CreateAssetMenu]`）+ 建一个资产 + 挂到物品上，**不用改 UI、不用改物品表结构**。
/// </summary>
public abstract class 物品使用效果 : ScriptableObject
{
    [TextArea(2, 4)]
    [Tooltip("效果说明，面板里给玩家看的（留空就用物品自己的介绍）")]
    public string 说明 = "";

    /// <summary>现在能不能用。例：已经学会的功法就不能再用一次</summary>
    public virtual bool 能使用(物品使用请求 请求) => true;

    /// <summary>不能用的原因（飘字/日志用）</summary>
    public virtual string 不能用原因(物品使用请求 请求) => "";

    /// <summary>真正使用。返回 true = **消耗掉一个**；返回 false = 什么都不做（物品留着）</summary>
    public abstract bool 使用(物品使用请求 请求);
}

/// <summary>
/// **按 id 找能力资产**（功法 / 主动神通 / 被动神通 / 外观）。
///
/// 为什么需要它：物品表现在是**唯一的物品来源**，而 CSV 里只能填字符串（id），
/// 没法引用资产。所以「学功法 / 学主动神通 / 学被动神通 / 学外观」这几个效果
/// 都支持只填 id，运行时按 id 反查成资产。
///
/// 查法：按 id 在 `Assets/Data/Generated/&lt;类型名&gt;/` 里扫一遍。
/// 用 AssetDatabase 只在编辑器下可用；打包后那条分支不编译返回 null，
/// 所以**正式打包前要保证效果资产上的引用已经填好**（可以在编辑器里跑一次
/// `修仙/调试/回填物品效果引用`，或直接进 Inspector 看有没有漏）。
/// </summary>
public static class 能力查找
{
    /// <summary>按 id 找一个 ScriptableObject（限定类型 T）</summary>
    public static T 按id<T>(string id) where T : ScriptableObject
    {
        if (string.IsNullOrEmpty(id)) return null;
#if UNITY_EDITOR
        string 目录 = "Assets/Data/Generated/" + typeof(T).Name;
        // 先只在这个类型自己的目录里找
        var guids = UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { 目录 });
        foreach (var g in guids)
        {
            var o = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (o != null && 取id(o) == id) return o;
        }
        // 退一步：全工程扫（资产被挪到别处也能找到）
        foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var o = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (o != null && 取id(o) == id) return o;
        }
#endif
        return null;
    }

    static string 取id(ScriptableObject o)
    {
        var g = o as GongFaDefinition; if (g != null) return g.功法id;
        var a = o as ActiveDivineAbility; if (a != null) return a.神通id;
        var p = o as PassiveDivineAbility; if (p != null) return p.神通id;
        var ap = o as AppearanceDefinition; if (ap != null) return ap.id;
        return o.name;
    }
}
