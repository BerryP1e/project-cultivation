// 设施界面接线：给「炼丹 / 炼器 / 布阵」这些**已经有 StationInteractable 但界面预制体为空**的
// 设施挂上对应界面预制体。
//
// ## 背景（2026-10-01）
//
// `Sect.scene` 里那几栋可互动建筑**本来就是炼丹阁 / 炼器阁 / 布阵点**：
//
//   environment_Building_qingsangcheng_001_o   → 炼丹
//   environment_building_minju_003_a           → 炼器
//   environment_building_qinghuacifangwu_02_a  → 布阵
//
// 它们**已经挂了 `StationInteractable`**，但 `界面预制体` 是**空的** ——
// 所以按 F 只会弹一块空白幕布（`StationInteractor` 的占位分支："UI 尚未设计"）。
//
// 对照：`3C_Testbed` 的 `cultivation room`（修炼）**挂了 `CultivationUI` 预制体**，所以它能用。
//
// ## 这个工具做什么
//
// 1. 生成 `Assets/Prefabs/炼丹界面.prefab`（结构照抄 `CultivationUI.prefab`：
//    根 + 组件 + 字体 = SimHei）
// 2. 把它挂到所有「炼丹」类的 `StationInteractable.界面预制体` 上
//
// 炼器复用同一个界面 —— 需求说「炼器 和炼丹基本一样，只是把丹药改成了法宝，
// 不过现在暂时还没有法宝，先不管」。所以炼器先不开界面，避免给玩家一个空壳。

using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cultivation.EditorTools
{
    public static class 设施界面接线
    {
        const string 预制体目录 = "Assets/Prefabs";
        const string 炼丹预制体 = 预制体目录 + "/炼丹界面.prefab";
        const string 字体路径 = "Assets/Fonts/SimHei.ttf";

        [MenuItem("工具/主线/④ 生成炼丹界面预制体 + 挂到炼丹阁", false, 63)]
        public static void 接线()
        {
            var 报告 = new StringBuilder();
            确保预制体(报告);

            var 预制 = AssetDatabase.LoadAssetAtPath<GameObject>(炼丹预制体);
            if (预制 == null) { Debug.LogError("[设施界面接线] 预制体没生成成功"); return; }

            // 所有打开着的场景都扫一遍
            int 挂上 = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var 场景 = SceneManager.GetSceneAt(i);
                if (!场景.isLoaded) continue;
                foreach (var go in 场景.GetRootGameObjects())
                    foreach (var si in go.GetComponentsInChildren<StationInteractable>(true))
                    {
                        if (si == null) continue;
                        if (si.类型 != StationInteractable.StationKind.炼丹) continue;
                        if (si.界面预制体 == 预制) continue;
                        si.界面预制体 = 预制;
                        EditorUtility.SetDirty(si);
                        挂上++;
                        报告.AppendLine($"  挂上：{si.gameObject.name}（{场景.name}）→ 炼丹界面");
                    }
                EditorSceneManager.MarkSceneDirty(场景);
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[设施界面接线] 完成：挂了 {挂上} 处\n{报告}");
        }

        [MenuItem("工具/主线/⑤ 检查设施界面挂了没", false, 64)]
        public static void 检查()
        {
            var sb = new StringBuilder("[设施界面接线] 检查\n");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var 场景 = SceneManager.GetSceneAt(i);
                if (!场景.isLoaded) continue;
                sb.AppendLine("=== " + 场景.name + " ===");
                foreach (var go in 场景.GetRootGameObjects())
                    foreach (var si in go.GetComponentsInChildren<StationInteractable>(true))
                    {
                        if (si == null) continue;
                        string 界 = si.界面预制体 == null ? "❌ 空（按 F 只会弹空白幕布）" : "✓ " + si.界面预制体.name;
                        sb.AppendLine($"  {si.类型,-6} {si.gameObject.name,-44} {界}");
                    }
            }
            Debug.Log(sb.ToString());
        }

        // ============================================================ 预制体

        static void 确保预制体(StringBuilder 报告)
        {
            if (!AssetDatabase.IsValidFolder(预制体目录))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            if (AssetDatabase.LoadAssetAtPath<GameObject>(炼丹预制体) != null)
            {
                报告.AppendLine("  炼丹界面.prefab 已存在，跳过生成");
                return;
            }

            // 结构照抄 CultivationUI.prefab：根 + 组件 + 字体，Canvas 全在 Awake 里自搭
            var 根 = new GameObject("炼丹界面");
            var 组件 = 根.AddComponent<炼丹界面>();

            var 字体 = AssetDatabase.LoadAssetAtPath<Font>(字体路径);
            if (字体 != null) 组件.字体 = 字体;
            else 报告.AppendLine("  ⚠ 没找到 " + 字体路径 + "，中文会变方块");

            var 预制 = PrefabUtility.SaveAsPrefabAsset(根, 炼丹预制体);
            Object.DestroyImmediate(根);
            AssetDatabase.Refresh();
            报告.AppendLine("  已生成 " + 炼丹预制体 + "（组件=" + (预制 != null && 预制.GetComponent<炼丹界面>() != null) + "）");
        }
    }
}
