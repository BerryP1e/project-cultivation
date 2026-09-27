using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **物品 / 背包的调试工具**（规整后：物品的唯一来源是 `物品表.csv`）。
///
/// 以前这里叫 `能力物品生成器`，会**自己造物品资产**（便服、各类学习物品），
/// 导致那些物品在物品表里查不到。用户 2026-09-27 要求「所有物品都从物品表出发」，
/// 所以生成职责已移交 `DataTableImporter`（配 `物品效果导入` 处理效果列），
/// 本文件只剩**场景侧的调试操作**。
///
/// 菜单：
///   · 修仙/调试/清空当前场景面板（新档口径）—— 把当前场景面板清成"什么都没有"
///   · 修仙/调试/用物品表填满当前场景背包 —— 测物品用，把物品表里所有物品各塞 1 个
/// </summary>
public static class 物品调试工具
{
    const string 物品目录 = "Assets/Data/Generated/ItemDefinition";

    [MenuItem("修仙/调试/清空当前场景面板（新档口径）", false, 400)]
    public static void 清空当前场景面板()
    {
        var 面板 = 取面板();
        if (面板 == null) return;

        面板.EnsureLists();
        int 功法 = 面板.已学功法 != null ? 面板.已学功法.Count : 0;
        int 主动 = 面板.已获得主动神通 != null ? 面板.已获得主动神通.Count : 0;
        int 被动 = 面板.已获得被动神通 != null ? 面板.已获得被动神通.Count : 0;
        int 物品 = 面板.物品 != null ? 面板.物品.Count : 0;

        面板.物品 = new List<ItemDefinition>();
        面板.法宝 = new List<TreasureDefinition>();
        面板.灵阵 = new List<SpiritArrayDefinition>();
        面板.坐骑 = new List<MountDefinition>();
        面板.当前坐骑 = null;
        面板.已获得真灵 = new List<NpcDefinition>();
        面板.已学功法 = new List<GongFaDefinition>();
        面板.已获得主动神通 = new List<ActiveDivineAbility>();
        面板.已获得被动神通 = new List<PassiveDivineAbility>();
        面板.当前功法 = null;
        面板.待装备神通 = null;
        面板.当前经验 = 0;
        for (int i = 0; i < 面板.主动技能.Count; i++) 面板.主动技能[i] = null;
        for (int i = 0; i < 面板.战阵站位.Count; i++) 面板.战阵站位[i] = null;
        // 用户 2026-09-26：所有被动一开始都是停用的（没获得的被动 = 停用状态）
        面板.已停用被动 = new List<PassiveDivineAbility>();
        foreach (var a in 面板.神通) if (a is PassiveDivineAbility p) 面板.已停用被动.Add(p);
        面板.RaiseChanged();

        标脏(面板);
        Debug.Log("[物品调试] 已把当前场景面板清空：原背包 " + 物品 + " 件、功法 " + 功法
            + "、主动 " + 主动 + "、被动 " + 被动 + " → 现在是「什么都没有」的新档状态（记得保存场景）");
    }

    [MenuItem("修仙/调试/用物品表填满当前场景背包", false, 401)]
    public static void 填满背包()
    {
        var 面板 = 取面板();
        if (面板 == null) return;

        var 物品 = new List<ItemDefinition>();
        foreach (var g in AssetDatabase.FindAssets("t:ItemDefinition", new[] { 物品目录 }))
        {
            var it = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (it != null) 物品.Add(it);
        }
        if (物品.Count == 0)
        {
            Debug.LogWarning("[物品调试] 物品目录里没有资产，先跑「修仙/从配置表生成资产」");
            return;
        }

        面板.EnsureLists();
        foreach (var it in 物品) 面板.给物品(it, 1);
        标脏(面板);
        Debug.Log("[物品调试] 已把物品表里的 " + 物品.Count + " 件物品各塞 1 个进当前场景背包（记得保存场景）");
    }

    static UIPanelData 取面板()
    {
        var 面板 = Object.FindObjectOfType<UIPanelData>();
        if (面板 == null) Debug.LogWarning("[物品调试] 当前场景里找不到 UIPanelData（角色面板数据）");
        return 面板;
    }

    static void 标脏(UIPanelData 面板)
    {
        EditorUtility.SetDirty(面板);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(面板.gameObject.scene);
    }
}
