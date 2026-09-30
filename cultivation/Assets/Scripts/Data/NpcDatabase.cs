using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **运行时「NPC库」**：全部 <see cref="NpcDefinition"/> 的聚合资产，
/// 放 `Assets/resources/NPC数据/NPC库.asset`（由 <c>TowerDatabaseCollector</c> 收集）。
///
/// 刷怪时「组里写的是 npcId」→ 这里按 id 查到定义 → 再由
/// <see cref="NpcDefinition.模型资源路径"/> 加载 prefab。
///
/// 【为什么需要它】生成的 <see cref="NpcDefinition"/> 在 `Assets/Data/Generated/NpcDefinition/`，
/// **不在 Resources 目录下**，运行时 `Resources.LoadAll&lt;NpcDefinition&gt;("")` 返回 **0 个**（实测）。
/// 所以必须有一份只装引用的聚合资产 —— 同任务库 / 对话库 / 面板库的做法。
///
/// > 单独一个文件是**必须的**：Unity 要求 `ScriptableObject` 的类名与文件名一致，
/// > 否则报「No script asset for NpcDatabase」，资产建不出来也加载不了。
/// </summary>
[CreateAssetMenu(fileName = "NPC库", menuName = "修仙/NPC库（运行时聚合）", order = 12)]
public class NpcDatabase : ScriptableObject
{
    [Tooltip("全部 NPC 定义（NPC表.csv 生成的全部）")]
    public List<NpcDefinition> 全部 = new List<NpcDefinition>();

    Dictionary<string, NpcDefinition> 索引;

    /// <summary>按 id 取定义（找不到返回 null）</summary>
    public NpcDefinition 按id(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (索引 == null) 建索引();
        NpcDefinition r;
        return 索引.TryGetValue(id, out r) ? r : null;
    }

    void 建索引()
    {
        索引 = new Dictionary<string, NpcDefinition>();
        if (全部 == null) return;
        foreach (var d in 全部)
        {
            if (d == null || string.IsNullOrEmpty(d.id)) continue;
            if (索引.ContainsKey(d.id))
            {
                Debug.LogWarning("[NPC库] 有两个定义的 id 都是「" + d.id + "」，用前者");
                continue;
            }
            索引[d.id] = d;
        }
    }

    /// <summary>库里的定义数</summary>
    public int 数量 => 全部 != null ? 全部.Count : 0;
}
