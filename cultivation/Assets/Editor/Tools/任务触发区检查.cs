using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text;
using System.Collections.Generic;

/// <summary>
/// **任务触发区一览 / 体检**。
///
/// 场景里会越摆越多触发区，每个实例靠**自己的组件字段**说明它管哪个任务，
/// 实例名只是给人看的标签。这个工具把所有触发区按名字列成一张表，并检查常见错误：
///   · 三个触发字段全空 —— 摆了但什么都不做
///   · 引用了任务表里不存在的任务 id —— 永远触发不了（写错名字最常见）
///   · 同一场景里名字重复、或同一个任务被两个触发区接取 —— 分不清是哪个
///   · 半径 <= 0
///
/// 菜单：修仙/主线/触发区一览（当前场景）/ （全部场景）
/// </summary>
public static class 任务触发区检查
{
    [MenuItem("修仙/主线/触发区一览（当前场景）")]
    static void 当前场景() { 跑(false); }

    [MenuItem("修仙/主线/触发区一览（全部场景）")]
    static void 全部场景() { 跑(true); }

    static System.Type 类型()
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(x => x.Name == "\u4efb\u52a1\u89e6\u53d1\u533a");
    }

    /// <summary>任务表里所有存在的 任务id</summary>
    static HashSet<string> 已知任务()
    {
        var set = new HashSet<string>();
        foreach (var g in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var q = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (q != null && !string.IsNullOrEmpty(q.任务id)) set.Add(q.任务id);
        }
        return set;
    }

    static string S(SerializedObject so, string n)
    {
        var p = so.FindProperty(n);
        if (p == null) return "?";
        if (p.propertyType == SerializedPropertyType.Float) return p.floatValue.ToString("0.##");
        if (p.propertyType == SerializedPropertyType.Boolean) return p.boolValue ? "1" : "0";
        return p.stringValue;
    }

    static void 跑(bool 全部)
    {
        var ty = 类型();
        if (ty == null) { Debug.LogError("[触发区检查] 找不到 任务触发区 类型，先让 Unity 编译一次"); return; }

        var 场景 = 全部
            ? EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray()
            : new[] { UnityEngine.SceneManagement.SceneManager.GetActiveScene().path };
        if (全部) EditorSceneManager.SaveOpenScenes();

        var 已知 = 已知任务();
        var sb = new StringBuilder();
        int 总数 = 0, 问题数 = 0;

        foreach (var sp in 场景)
        {
            if (string.IsNullOrEmpty(sp) || !System.IO.File.Exists(sp)) continue;
            var sc = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
            var 全部区 = sc.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t.GetComponent(ty) != null).OrderBy(t => t.name).ToList();
            if (全部区.Count == 0) continue;

            sb.AppendLine("=== " + sp + "  触发区 " + 全部区.Count + " 个");
            sb.AppendLine("    名字 | 位置 | 半径 | 接取任务 | 推进阶段 | 加标记 | 需要标记 | 需要任务");
            var 名字 = new HashSet<string>();
            var 接取过 = new Dictionary<string, string>();

            foreach (var t in 全部区)
            {
                总数++;
                var so = new SerializedObject(t.GetComponent(ty));
                string 半径 = S(so, "\u534a\u5f84");
                string 接取 = S(so, "\u89e6\u53d1\u4efb\u52a1id");
                string 推进 = S(so, "\u5b8c\u6210\u9636\u6bb5\u4efb\u52a1id");
                string 加标 = S(so, "\u52a0\u6807\u8bb0");
                string 需标 = S(so, "\u9700\u8981\u6807\u8bb0");
                string 需任 = S(so, "\u9700\u8981\u4efb\u52a1id");

                sb.AppendLine("    " + t.name + " | " + t.position.ToString("F1") + " | " + 半径 + " | "
                    + (接取 == "" ? "-" : 接取) + " | " + (推进 == "" ? "-" : 推进) + " | "
                    + (加标 == "" ? "-" : 加标) + " | " + (需标 == "" ? "-" : 需标) + " | " + (需任 == "" ? "-" : 需任));

                System.Action<string> 问题 = m => { 问题数++; sb.AppendLine("      ⚠ " + t.name + "：" + m); };

                if (接取 == "" && 推进 == "" && 加标 == "")
                    问题("三个触发字段全空 —— 摆了但什么都不做");
                if (接取 != "" && !已知.Contains(接取)) 问题("接取任务「" + 接取 + "」在任务表里不存在");
                if (推进 != "" && !已知.Contains(推进)) 问题("推进任务「" + 推进 + "」在任务表里不存在");
                if (需任 != "" && !已知.Contains(需任)) 问题("需要任务「" + 需任 + "」在任务表里不存在");
                if (!float.TryParse(半径, out var r) || r <= 0f) 问题("半径不是正数");
                if (!名字.Add(t.name)) 问题("同场景内名字重复，分不清是哪一个");
                if (接取 != "")
                {
                    if (接取过.ContainsKey(接取)) 问题("接取任务「" + 接取 + "」和另一个触发区「" + 接取过[接取] + "」重复");
                    else 接取过[接取] = t.name;
                }
            }
        }

        if (总数 == 0) sb.AppendLine("没有找到任何任务触发区");
        else sb.AppendLine("合计 " + 总数 + " 个触发区，发现 " + 问题数 + " 个问题");
        Debug.Log("[触发区检查]\n" + sb.ToString());
    }
}
