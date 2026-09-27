using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **角色面板的"目录"库** —— 运行时聚合资产，放在 `Assets/resources/面板/面板库.asset`。
///
/// 为什么要有这个（用户 2026-09-27 报的 bug）：
///   以前这些东西（神通全表 / 法宝 / 灵阵 / 坐骑 / 可获真灵 …）是**直接序列化在
///   `UIPanelData` 上**的，而 `UIPanelData` 在**每个场景里各有一份** ⇒
///   只有当初被"灌过表"的那个场景是对的，其他场景是空的。
///   实测：古古镇 神通全表=11、宗门=0 → 在宗门打开神通页一片空白。
///
/// 现在改成：**目录从这里读**（唯一源头），`UIPanelData` 只保留"玩家自己的数据"
/// （背包 / 已获得 / 已学 / 已停用 / 技能槽 / 战阵站位 …）。
/// 这样无论从哪个场景进，目录都一致。
///
/// ⚠️ 注意：**背包（`UIPanelData.物品`）不在这里** —— 它是玩家自己的数据，由存档负责。
///    同理 `已获得主动/被动神通`、`已学功法` 也不在这里。
/// </summary>
[CreateAssetMenu(fileName = "面板库", menuName = "修仙/面板库", order = 20)]
public class PanelDatabase : ScriptableObject
{
    [Tooltip("所有神通（主动 + 被动）。由「修仙/面板/收集面板目录」自动填充")]
    public List<DivineAbilityDefinition> 神通 = new List<DivineAbilityDefinition>();

    [Tooltip("所有法宝")]
    public List<TreasureDefinition> 法宝 = new List<TreasureDefinition>();

    [Tooltip("所有灵阵")]
    public List<SpiritArrayDefinition> 灵阵 = new List<SpiritArrayDefinition>();

    [Tooltip("所有坐骑（收集时会按门槛排序）")]
    public List<MountDefinition> 坐骑 = new List<MountDefinition>();

    [Tooltip("所有能当真灵的 NPC（有模型资源的妖魔/人类）")]
    public List<NpcDefinition> 真灵 = new List<NpcDefinition>();

    static PanelDatabase 缓存;

    /// <summary>取面板目录库（Resources/面板/面板库）。没有就返回 null，调用方要判空</summary>
    public static PanelDatabase 取()
    {
        if (缓存 == null) 缓存 = Resources.Load<PanelDatabase>("面板/面板库");
        return 缓存;
    }

    /// <summary>换了库（重新收集）之后调一下</summary>
    public static void 清缓存() => 缓存 = null;
}
