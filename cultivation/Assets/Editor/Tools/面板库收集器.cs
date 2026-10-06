using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **收集「面板目录」到运行时聚合库**（菜单：修仙/面板/收集面板目录）。
///
/// 生成 `Assets/resources/面板/面板库.asset`，供 `PanelDatabase.取()` 在运行时读。
///
/// 为什么要有这一步：
///   面板的"目录"（神通 / 法宝 / 灵阵 / 坐骑 / 可获真灵）原来是**序列化在每个场景的
///   `UIPanelData` 上**的 —— 于是只有被灌过表的场景是对的，其他场景全空
///   （实测：古古镇 神通全表=11、宗门=0 → 宗门里神通页一片空白）。
///   现在统一到一个 Resources 聚合资产，**哪个场景进都一样**。
///
/// 只要**改了配置表并重新生成资产**，就跑一次这个菜单。
/// </summary>
public static class 面板库收集器
{
    const string 目录 = "Assets/resources/面板";
    const string 资产路径 = 目录 + "/面板库.asset";

    [MenuItem("修仙/面板/收集面板目录", false, 600)]
    public static void 收集()
    {
        if (!Directory.Exists(目录))
        {
            Directory.CreateDirectory(目录);
            AssetDatabase.Refresh();
        }

        var 库 = AssetDatabase.LoadAssetAtPath<PanelDatabase>(资产路径);
        bool 新建 = 库 == null;
        if (新建)
        {
            库 = ScriptableObject.CreateInstance<PanelDatabase>();
            AssetDatabase.CreateAsset(库, 资产路径);
        }

        // ---- 功法目录：所有场景与读档共用，不写玩家的已学列表 ----
        库.功法 = new List<GongFaDefinition>();
        foreach (var g in AssetDatabase.FindAssets("t:GongFaDefinition", new[] { "Assets/Data/Generated/GongFaDefinition" }))
            库.功法.Add(AssetDatabase.LoadAssetAtPath<GongFaDefinition>(AssetDatabase.GUIDToAssetPath(g)));
        库.功法.RemoveAll(x => x == null);
        库.功法.Sort((a, b) => string.CompareOrdinal(a.功法id, b.功法id));

        // ---- 神通：主动 + 被动 ----
        库.神通 = new List<DivineAbilityDefinition>();
        foreach (var g in AssetDatabase.FindAssets("t:ActiveDivineAbility"))
            库.神通.Add(AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>(AssetDatabase.GUIDToAssetPath(g)));
        foreach (var g in AssetDatabase.FindAssets("t:PassiveDivineAbility"))
            库.神通.Add(AssetDatabase.LoadAssetAtPath<PassiveDivineAbility>(AssetDatabase.GUIDToAssetPath(g)));
        库.神通.RemoveAll(x => x == null);
        var 主动id = new HashSet<string>();
        foreach (var a in 库.神通) if (a is ActiveDivineAbility) 主动id.Add(a.神通id);
        // 四御由被动改为主动，旧资产仍留给旧场景引用迁移，目录只展示主动版本。
        库.神通.RemoveAll(x => x is PassiveDivineAbility && 主动id.Contains(x.神通id));

        // ---- 法宝 / 灵阵 ----
        var 法宝 = new List<TreasureDefinition>();
        foreach (var g in AssetDatabase.FindAssets("t:TreasureDefinition"))
            法宝.Add(AssetDatabase.LoadAssetAtPath<TreasureDefinition>(AssetDatabase.GUIDToAssetPath(g)));
        if (库.法宝 == null) 库.法宝 = new List<TreasureDefinition>();
        库.法宝 = 法宝.FindAll(x => x != null);

        var 灵阵 = new List<SpiritArrayDefinition>();
        foreach (var g in AssetDatabase.FindAssets("t:SpiritArrayDefinition"))
            灵阵.Add(AssetDatabase.LoadAssetAtPath<SpiritArrayDefinition>(AssetDatabase.GUIDToAssetPath(g)));
        库.灵阵 = 灵阵.FindAll(x => x != null);

        // ---- 坐骑（用运行时同一个比较器排序，避免两处各排一套）----
        var 坐骑 = new List<MountDefinition>();
        foreach (var g in AssetDatabase.FindAssets("t:MountDefinition"))
            坐骑.Add(AssetDatabase.LoadAssetAtPath<MountDefinition>(AssetDatabase.GUIDToAssetPath(g)));
        坐骑.RemoveAll(x => x == null || x.坐骑id == "mount_julong_01");
        坐骑.Sort(UIPanelData.比坐骑);
        库.坐骑 = 坐骑;

        // ---- 可获真灵：所有**有模型资源**的 NPC（和原来 RewirePanelData 的口径一致）----
        var 真灵 = new List<NpcDefinition>();
        foreach (var g in AssetDatabase.FindAssets("t:NpcDefinition"))
        {
            var n = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (n == null) continue;
            if (string.IsNullOrEmpty(n.模型资源路径)) continue;   // 没 prefab 的不可能上场
            真灵.Add(n);
        }
        真灵.Sort(UIPanelData.比真灵);
        库.真灵 = 真灵;

        EditorUtility.SetDirty(库);
        AssetDatabase.SaveAssets();
        PanelDatabase.清缓存();

        Debug.Log("[面板库] 已收集：神通 " + 库.神通.Count + "、法宝 " + 库.法宝.Count
            + "、灵阵 " + 库.灵阵.Count + "、坐骑 " + 库.坐骑.Count + "、真灵 " + 库.真灵.Count
            + (新建 ? "（新建）" : "（更新）")
            + "\n  写到 " + 资产路径);

        // ⚠️ 这里**故意不弹 `EditorUtility.DisplayDialog`**。
        //   模态框会**挡住编辑器主线程**，而自动化调用（AI 跑这个菜单）**没人去点它** ——
        //   表现就是"命令一直不返回、看起来卡住了"，白等很久。上面的 Debug.Log 已经把所有信息都说了。
        //   用户 2026-09-29 明确要求：跑完别弹需要点确认的窗。详见 踩坑 F8。
    }
}
