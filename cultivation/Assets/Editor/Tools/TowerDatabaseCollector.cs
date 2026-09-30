using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **收集运行时聚合库**：塔库（`Assets/resources/塔/塔库.asset`）+ NPC库（`Assets/resources/NPC数据/NPC库.asset`）。
///
/// 菜单：**修仙/镇妖塔/收集塔库与NPC库**
///
/// 由 <see cref="DataTableImporter.ImportAll"/> 在末尾**自动调用**，
/// 所以日常只要跑「修仙/从配置表生成资产」就够了。
///
/// ## 为什么必须有这一步（实测记录）
///
/// 生成的资产在 `Assets/Data/Generated/`，**不在任何 Resources 目录下**：
///
/// <code>
/// Resources.LoadAll&lt;NpcDefinition&gt;("")     = 0    ← 实测
/// Resources.LoadAll&lt;NpcLevelScale&gt;("")     = 0
/// Resources.LoadAll&lt;SpawnGroup&gt;("")         = 0
/// Resources.LoadAll&lt;TowerFloorTable&gt;("")    = 0
/// </code>
///
/// 所以运行时**不可能**靠 `Resources.LoadAll` 拿到它们，必须有一份装引用的聚合资产。
/// 项目里任务库 / 对话库 / 面板库都是这个做法。
///
/// > ⚠️ 这里**故意不弹 `EditorUtility.DisplayDialog`** —— 模态框会挡住编辑器主线程，
/// > 而自动化调用（AI 跑这个菜单）没人去点它，表现就是"命令卡住不返回"。见 踩坑 F8。
/// </summary>
public static class TowerDatabaseCollector
{
    const string 生成根 = "Assets/Data/Generated";
    const string 塔库目录 = "Assets/resources/塔";
    const string 塔库路径 = 塔库目录 + "/塔库.asset";
    const string Npc库目录 = "Assets/resources/NPC数据";
    const string Npc库路径 = Npc库目录 + "/NPC库.asset";

    [MenuItem("修仙/镇妖塔/收集塔库与NPC库", false, 620)]
    public static void 收集()
    {
        确保目录(塔库目录);
        确保目录(Npc库目录);

        var 塔 = 载入或新建<TowerDatabase>(塔库路径);
        var npc = 载入或新建<NpcDatabase>(Npc库路径);

        // ---- 等级补正表（全局唯一一张）----
        塔.等级补正表 = 唯一<NpcLevelScale>("NpcLevelScale");
        if (塔.等级补正表 == null)
            Debug.LogWarning("[塔库] 没找到 NpcLevelScale —— 先跑「修仙/从配置表生成资产」的塔表导入步骤");

        // ---- 层表（全局唯一一张）----
        塔.层表 = 唯一<TowerFloorTable>("TowerFloorTable");

        // ---- 刷怪组（全部）----
        塔.刷怪组 = 全部<SpawnGroup>("SpawnGroup");
        塔.刷怪组.Sort((a, b) => string.CompareOrdinal(a.组id, b.组id));

        // ---- NPC 定义（全部，按 id 排好，方便肉眼比对）----
        npc.全部 = 全部<NpcDefinition>("NpcDefinition");
        npc.全部.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        EditorUtility.SetDirty(塔);
        EditorUtility.SetDirty(npc);
        AssetDatabase.SaveAssets();

        // 运行时缓存必须清掉，否则编辑器里改了库、Play 里读的还是旧的
        TowerDatabase库.清缓存();
        NpcLevelScale库.清缓存();
        SpawnGroup库.清缓存();
        TowerFloorTable库.清缓存();
        NpcDatabase库.清缓存();
        NpcPrefabs.清索引();

        var 报告 = "[塔库] 已收集：\n"
                   + "  塔库  " + 塔库路径 + "\n"
                   + "    等级补正表：" + (塔.等级补正表 != null ? 塔.等级补正表.曲线摘要() : "★缺") + "\n"
                   + "    层表：" + (塔.层表 != null
                        ? 塔.层表.有效总层数 + " 层 / " + 塔.层表.分段.Count + " 段"
                        : "★缺") + "\n"
                   + "    刷怪组：" + 塔.刷怪组.Count + " 组\n"
                   + "  NPC库 " + Npc库路径 + "\n"
                   + "    NPC 定义：" + npc.全部.Count + " 个";
        Debug.Log(报告);
    }

    // ============================================================ 工具

    static T 唯一<T>(string 子目录) where T : ScriptableObject
    {
        var 全部项 = 全部<T>(子目录);
        if (全部项.Count == 0) return null;
        if (全部项.Count > 1)
            Debug.LogWarning("[塔库] " + typeof(T).Name + " 有 " + 全部项.Count
                             + " 个资产（应该只有一个），用第一个：" + 全部项[0].name);
        return 全部项[0];
    }

    static List<T> 全部<T>(string 子目录) where T : ScriptableObject
    {
        var r = new List<T>();
        string 目录 = 生成根 + "/" + 子目录;
        if (!AssetDatabase.IsValidFolder(目录)) return r;

        foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { 目录 }))
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (a != null) r.Add(a);
        }
        return r;
    }

    static T 载入或新建<T>(string 路径) where T : ScriptableObject
    {
        var so = AssetDatabase.LoadAssetAtPath<T>(路径);
        if (so != null) return so;
        so = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(so, 路径);
        return so;
    }

    static void 确保目录(string 路径)
    {
        if (AssetDatabase.IsValidFolder(路径)) return;
        var 段 = 路径.Split('/');
        string 当前 = 段[0];
        for (int i = 1; i < 段.Length; i++)
        {
            string 下 = 当前 + "/" + 段[i];
            if (!AssetDatabase.IsValidFolder(下)) AssetDatabase.CreateFolder(当前, 段[i]);
            当前 = 下;
        }
    }
}
