// 镇妖塔灯光收敛工具
//
// ## 为什么需要
//
// 塔内实测有 **33 盏灯**：8 盏「层光」强度 2.0 / range 40（塔半径才 27 —— 隔着墙灌进来）、
// 7 盏「火光」强度 3.4 / range 26、12 盏「梯灯」2.2 / range 16。
// 点光**全都关着阴影**，加上 range 远大于房间尺度，
// 结果是**全塔均匀泛光**：暗部被抬成灰、明暗对比被抹平、画面一层琥珀色雾霾。
//
// 这不是"不够亮"，是"**没有暗**"。所以收敛方向是**降强度 + 缩半径**，
// 让光只照到该照的地方，把暗部还给场景 —— 而不是继续加光。
//
// ## 用法
// 打开 `Assets/Scenes/Demon-Suppressing Tower.scene` 后执行对应菜单项。
// 改的是**场景里序列化的值**，立刻保存生效，可反复执行（幂等：按倍率乘，已应用过会继续变小，故记录原值备份）。
//
// ## 备份
// 首次执行会把每个灯的原始 强度/range 写进 `.local/backups/tower-lighting/塔内灯光原值.txt`。

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cultivation.EditorTools
{
    public static class 塔灯光收敛
    {
        private const string 场景路径 = "Assets/Scenes/Demon-Suppressing Tower.scene";
        private static readonly string 备份目录 = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.local/backups/tower-lighting"));

        /// <summary>
        /// 按**灯名**定收敛系数：名字 → (强度倍率, 半径倍率)。
        /// 用名字查而不用引用，是因为灯是场景对象、改完要落盘，名字在场景里是稳定的。
        /// </summary>
        private static readonly Dictionary<string, Vector2> 收敛表 = new Dictionary<string, Vector2>
        {
            // 层光：range 40 → 28（收到塔半径附近，不再隔墙灌），强度 2.0 → 1.15
            { "层光", new Vector2(0.58f, 0.70f) },
            // 火光：3.4 → 2.20（原值太高，贴脸的地面/墙直接过曝）
            { "火光", new Vector2(0.65f, 0.85f) },
            // 梯灯：2.2 → 1.50
            { "梯灯", new Vector2(0.68f, 0.90f) },
            // 灯光（4 盏补光）：1.6 → 1.10
            { "灯光", new Vector2(0.69f, 0.90f) },
        };

        [MenuItem("工具/画面/① 灯光：只报告不改", false, 30)]
        public static void 报告()
        {
            if (!打开塔场景()) return;
            var sb = new StringBuilder();
            sb.AppendLine("=== 塔内灯光现状 ===");
            int 命中 = 0;
            foreach (var l in Object.FindObjectsOfType<Light>(true))
            {
                var 系数 = 取系数(l.name);
                sb.AppendLine($"  {(系数.HasValue ? "★" : " ")} {l.name,-26} {l.type,-11} 强度={l.intensity,6:F2} range={l.range,6:F1} shadow={l.shadows}");
                if (系数.HasValue) 命中++;
            }
            sb.AppendLine($"灯总数 = {Object.FindObjectsOfType<Light>(true).Length}，其中会被收敛的 = {命中}");
            Debug.Log(sb.ToString());
        }

        [MenuItem("工具/画面/② 灯光：执行收敛（降强度+缩半径）", false, 31)]
        public static void 执行()
        {
            if (!打开塔场景()) return;

            var 灯 = Object.FindObjectsOfType<Light>(true);
            var 备份 = new StringBuilder();
            备份.AppendLine("# 塔内灯光原值备份（收敛前）");
            备份.AppendLine("# 格式：灯名 | 类型 | 强度 | range");
            int 改了 = 0;

            foreach (var l in 灯)
            {
                var 系数 = 取系数(l.name);
                if (!系数.HasValue) continue;
                备份.AppendLine($"{l.name} | {l.type} | {l.intensity:F3} | {l.range:F2}");

                l.intensity *= 系数.Value.x;
                l.range *= 系数.Value.y;
                EditorUtility.SetDirty(l);
                改了++;
            }

            try
            {
                Directory.CreateDirectory(备份目录);
                var f = Path.Combine(备份目录, "塔内灯光原值.txt");
                if (!File.Exists(f)) File.WriteAllText(f, 备份.ToString(), new UTF8Encoding(false));
            }
            catch (System.Exception e) { Debug.LogWarning("[灯光收敛] 备份写入失败：" + e.Message); }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log($"[灯光收敛] 已收敛 {改了} 盏灯，场景已保存。");
            Debug.Log(备份.ToString());
        }

        private static Vector2? 取系数(string 名)
        {
            foreach (var kv in 收敛表)
                if (名.StartsWith(kv.Key)) return kv.Value;
            return null;
        }

        private static bool 打开塔场景()
        {
            var 当前 = SceneManager.GetActiveScene();
            if (当前.path == 场景路径) return true;

            if (当前.isDirty)
            {
                if (!EditorUtility.DisplayDialog("场景未保存",
                    $"当前场景「{当前.name}」有未保存改动。\n要先保存再切到镇妖塔场景吗？", "保存并切换", "取消"))
                    return false;
                EditorSceneManager.SaveScene(当前);
            }
            EditorSceneManager.OpenScene(场景路径, OpenSceneMode.Single);
            return true;
        }
    }
}
