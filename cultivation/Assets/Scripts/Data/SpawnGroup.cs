using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **刷怪组里的一个条目**：刷哪种怪、刷几只。
/// 对应 CSV「刷怪组表」的一列对（`<npcId>` 与 `<npcId>_数量`）。
/// </summary>
[System.Serializable]
public class SpawnGroupEntry
{
    [Tooltip("NPC 定义 id（NPC表.csv 的 id）")]
    public string npcId = "";

    [Tooltip("这一组里刷几只")]
    public int 数量 = 1;
}

/// <summary>
/// **刷怪组**：一组一起出现的怪。
/// 对应 CSV「刷怪组表」的一行 —— 镇妖塔每层刷一个组。
///
/// ## 为什么要有「组」这一层
///
/// 用户要求「每次怪物都以表格中的刷怪组类别组团出现」——
/// 而不是随机挑怪。这样每层的**构成**是可设计的（例：2 只前排 + 3 只远程），
/// 也方便调难度（改数量、改种类，不用碰代码）。
///
/// ## 怎么加一组
///
/// 在 <c>Assets/Data/Tables/刷怪组表.csv</c> 里加一行：
/// <code>
/// 组id,名字,权重,id1,数量1,id2,数量2,...
/// </code>
/// 列数不固定：导入器按 `<列名>` / `<列名>_数量` 成对读。
/// </summary>
[CreateAssetMenu(fileName = "SpawnGroup_", menuName = "修仙/刷怪组", order = 9)]
public class SpawnGroup : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("组 id，全局唯一")]
    public string 组id = "";

    [Tooltip("显示名（调试 / 塔层提示用）")]
    public string 名字 = "";

    [Tooltip("随机抽取时的权重（镇妖塔按层查表时不用它）")]
    public int 权重 = 1;

    [Header("成员（由 CSV 生成）")]
    public List<SpawnGroupEntry> 成员 = new List<SpawnGroupEntry>();

    /// <summary>这一组共几只</summary>
    public int 总数
    {
        get
        {
            int n = 0;
            if (成员 != null) for (int i = 0; i < 成员.Count; i++) n += 成员[i].数量;
            return n;
        }
    }

    /// <summary>组里的怪种类数（不算 0 只的条目）</summary>
    public int 种类数
    {
        get
        {
            int n = 0;
            if (成员 != null) for (int i = 0; i < 成员.Count; i++) if (成员[i].数量 > 0) n++;
            return n;
        }
    }

    /// <summary>是不是空组（没有可刷的怪）</summary>
    public bool 是空的 => 总数 <= 0;

    /// <summary>把组内容摊成一个 id 列表（每只一个元素），刷怪时按它逐个生成</summary>
    public List<string> 展开()
    {
        var r = new List<string>();
        if (成员 == null) return r;
        for (int i = 0; i < 成员.Count; i++)
        {
            var m = 成员[i];
            if (m == null || string.IsNullOrEmpty(m.npcId) || m.数量 <= 0) continue;
            for (int k = 0; k < m.数量; k++) r.Add(m.npcId);
        }
        return r;
    }

    /// <summary>可读摘要，例：「寒狼×3 + 花妖×2」</summary>
    public string 摘要()
    {
        if (成员 == null || 成员.Count == 0) return "（空）";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 成员.Count; i++)
        {
            var m = 成员[i];
            if (m == null || m.数量 <= 0) continue;
            if (sb.Length > 0) sb.Append(" + ");
            sb.Append(m.npcId).Append('×').Append(m.数量);
        }
        return sb.Length == 0 ? "（空）" : sb.ToString();
    }
}

/// <summary>
/// **刷怪组的运行时取用口。** 按 id 查，带缓存。
/// 和 <see cref="NpcLevelScale库"/> 一样走「塔库」聚合资产
/// （生成的资产在 `Assets/Data/Generated/`，不在 Resources 下，`Resources.LoadAll` 拿不到）。
/// </summary>
public static class SpawnGroup库
{
    static Dictionary<string, SpawnGroup> 索引;
    static readonly HashSet<string> 警告过 = new HashSet<string>();

    static Dictionary<string, SpawnGroup> 表()
    {
        if (索引 != null) return 索引;

        索引 = new Dictionary<string, SpawnGroup>();
        var 库 = TowerDatabase库.取();
        if (库 != null && 库.刷怪组 != null)
        {
            foreach (var g in 库.刷怪组)
            {
                if (g == null || string.IsNullOrEmpty(g.组id)) continue;
                if (索引.ContainsKey(g.组id))
                {
                    Debug.LogWarning("[刷怪组] 有两个组的 组id 都是「" + g.组id + "」，用前者");
                    continue;
                }
                索引[g.组id] = g;
            }
        }
        return 索引;
    }

    /// <summary>按组 id 取（找不到返回 null 并只警告一次）</summary>
    public static SpawnGroup 按id(string 组id)
    {
        if (string.IsNullOrEmpty(组id)) return null;
        SpawnGroup g;
        if (表().TryGetValue(组id, out g)) return g;

        if (警告过.Add(组id))
            Debug.LogWarning("[刷怪组] 找不到组「" + 组id
                             + "」。跑一次「修仙/从配置表生成资产」，并确认刷怪组表里有这个 组id");
        return null;
    }

    /// <summary>全部组（调试面板列用）</summary>
    public static List<SpawnGroup> 全部()
    {
        var r = new List<SpawnGroup>(表().Values);
        r.Sort((a, b) => string.CompareOrdinal(a.组id, b.组id));
        return r;
    }

    /// <summary>清缓存（重生成资产 / 重进 Play 时用）</summary>
    public static void 清缓存() { 索引 = null; 警告过.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置静态() { 清缓存(); }
}
