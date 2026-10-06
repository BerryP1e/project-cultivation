using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **运行时「塔库」+「NPC库」的聚合资产**。
///
/// ## 为什么必须有这一层
///
/// `DataTableImporter` 生成到 `Assets/Data/Generated/&lt;类型名&gt;/`，
/// **那个路径不在任何 Resources 文件夹下**，所以运行时 `Resources.Load` 拿不到。
/// 这一点项目里已经踩过并形成了固定做法（任务库 / 对话库 / 面板库）：
/// 在 `Assets/resources/` 下放一份**只装引用的聚合资产**，运行时只读它。
///
/// 本来我想让塔的三个运行时库直接 `Resources.LoadAll&lt;T&gt;`，
/// 实测 **返回 0 个**（见下面的实测记录），所以老老实实走聚合资产。
///
/// <code>
/// 实测（2026-09-30，Unity 编辑器）：
///   Resources.LoadAll&lt;NpcDefinition&gt;("")  = 0
///   Resources.LoadAll&lt;GongFaDefinition&gt;("") = 0
///   Resources.LoadAll&lt;RealmDefinition&gt;("")  = 0
/// </code>
/// </summary>
[CreateAssetMenu(fileName = "塔库", menuName = "修仙/塔库（运行时聚合）", order = 11)]
public class TowerDatabase : ScriptableObject
{
    [Tooltip("怪物等级补正表（全局唯一一张）")]
    public NpcLevelScale 等级补正表;

    [Tooltip("镇妖塔层表（全局唯一一张）")]
    public TowerFloorTable 层表;

    [Tooltip("全部刷怪组")]
    public List<SpawnGroup> 刷怪组 = new List<SpawnGroup>();
}
