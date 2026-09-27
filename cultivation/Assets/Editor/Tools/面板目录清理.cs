using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// **清掉场景里冗余的面板目录副本**（菜单：修仙/面板/清掉场景里冗余的目录副本）。
///
/// 背景（用户 2026-09-27）：
///   面板的「目录」（神通 / 法宝 / 灵阵 / 坐骑 / 可获真灵）原来是**序列化在每个场景的
///   `UIPanelData` 上**的 —— 于是只有被灌过表的场景是对的，其他场景全空
///   （实测：古古镇 神通=11、宗门=0 → 宗门里神通页一片空白）。
///
///   现在已经改成**运行时**从 `Assets/resources/面板/面板库.asset` 读
///   （见 `UIPanelData.从面板库灌目录()`）。也就是说**场景里那份副本已经是死数据**，
///   每次进游戏都会被覆盖掉。
///
/// 为什么还要清理：
///   · 5 个场景各存一份 11+3+5+9+233 个引用，白占体积、白增 diff 噪音；
///   · 留着容易让人以为"场景里那份才是真的"，再踩一次同样的坑。
///
/// ⚠️ **不影响玩法**：这些字段由 `从面板库灌目录()` 在运行时灌；
///    `面板库.asset` 不存在时会**明确报错**并保留场景里的旧值（所以清理是可逆的）。
/// </summary>
public static class 面板目录清理
{
    static readonly string[] 游戏场景 =
    {
        "Assets/Scenes/village.scene",
        "Assets/Scenes/Sect.scene",
        "Assets/Scenes/3C_Testbed.scene",
        "Assets/Scenes/Sect_Wilderness.scene",
        "Assets/Scenes/Demon-Suppressing Tower.scene",
    };

    [MenuItem("修仙/面板/清掉场景里冗余的目录副本", false, 610)]
    public static void 清理()
    {
        // 先确认运行时库在，否则清空后没人灌 → 面板会空
        var 库 = PanelDatabase.取();
        if (库 == null || 库.神通 == null || 库.神通.Count == 0)
        {
            EditorUtility.DisplayDialog("面板目录清理",
                "读不到 Assets/resources/面板/面板库.asset（或它是空的）。\n\n" +
                "先跑一次「修仙/面板/收集面板目录」再清理，\n" +
                "否则清空后运行时没人灌，面板会变空。", "好");
            return;
        }
        PanelDatabase.清缓存();

        var 报 = new System.Text.StringBuilder();
        int 总清 = 0;

        foreach (var 路径 in 游戏场景)
        {
            if (!System.IO.File.Exists(路径)) continue;
            var 场景 = EditorSceneManager.OpenScene(路径, OpenSceneMode.Single);
            var 面板s = Object.FindObjectsOfType<UIPanelData>();
            if (面板s.Length == 0) { 报.Append(System.IO.Path.GetFileNameWithoutExtension(路径)).Append("：没有 UIPanelData\n"); continue; }

            int 本场景 = 0;
            foreach (var p in 面板s)
            {
                int n = 数一下(p);
                if (n == 0) continue;
                p.神通 = new List<DivineAbilityDefinition>();
                p.法宝 = new List<TreasureDefinition>();
                p.灵阵 = new List<SpiritArrayDefinition>();
                p.坐骑 = new List<MountDefinition>();
                p.已获得真灵 = new List<NpcDefinition>();
                EditorUtility.SetDirty(p);
                本场景 += n;
            }
            if (本场景 > 0) { EditorSceneManager.MarkSceneDirty(场景); EditorSceneManager.SaveScene(场景); }
            总清 += 本场景;
            报.Append(System.IO.Path.GetFileNameWithoutExtension(路径)).Append("：清掉 ").Append(本场景).Append(" 个引用\n");
        }

        报.Append("\n共清掉 ").Append(总清).Append(" 个（运行时由 面板库 灌回，不影响玩法）");
        Debug.Log("[面板目录清理] " + 报.ToString().Replace("\n", " ｜ "));
        EditorUtility.DisplayDialog("面板目录清理", 报.ToString(), "好");
    }

    static int 数一下(UIPanelData p)
    {
        int n = 0;
        if (p.神通 != null) n += p.神通.Count;
        if (p.法宝 != null) n += p.法宝.Count;
        if (p.灵阵 != null) n += p.灵阵.Count;
        if (p.坐骑 != null) n += p.坐骑.Count;
        if (p.已获得真灵 != null) n += p.已获得真灵.Count;
        return n;
    }
}
