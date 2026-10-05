using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// **安全导入配置表**：跑 `DataTableImporter.ImportAll()`，但**不留下它顺手改写的场景**。
///
/// ## 为什么需要它（2026-10-03 用户报的真问题）
///
/// 导入器会**保存当前打开的场景**（这是它的老毛病，见踩坑 A8）。工程里长期的做法是：
/// 跑完导入器 → `git checkout` 把写脏的场景还原。
/// **这个做法会误伤**：如果那段时间里**用户或 AI 在场景里做了实质改动**（例如 2026-10-03 用户
/// 把几个 `better ground` 的碰撞地板缩小、让它贴合可见网格、不再卡住），
/// 一次 `git checkout` 就**把这些改动一起冲掉了** —— 用户看到的现象就是
/// 「重新生成资产时这个组件被重置」。
///
/// ## 做法：**字节级还原**，而不是"还原到上一次提交"
///
/// 跑导入器**之前**记下每个打开场景文件的**原始字节**；导入完成后**把字节原样写回**。
/// ⇒ 导入对场景而言变成**零副作用**；而**你保存过的改动本来就写在那份字节里**，所以**不会被冲掉** ✓
///
/// ## 用法
///
/// 菜单：**`修仙 / 工具 / 安全导入配置表（不动场景）`**
/// （想连带看场景被写成什么样，加 `导出被改动的场景` 参数：会另存到 `Temp/导入器改写的场景/` 供比对）
///
/// ⚠️ 前提：**先把 Unity 里未保存的场景改动存盘**（Ctrl+S）。
/// 本工具只能保住"**盘上**"的字节 —— 内存里没存过的改动，它救不了（会按盘上内容还原）。
/// </summary>
public static class 安全导入配置表
{
    /// <summary>只更新指定表的资产，不回填也不保存任何场景，保留内存中尚未保存的场景内容。</summary>
    public static void ImportAssetsOnly(params string[] 表名)
    {
        DataTableImporter.ImportTables(false, 表名);
        面板库收集器.收集();
    }
    [MenuItem("修仙/工具/安全导入配置表（不动场景）", false, 950)]
    [MenuItem("Cultivation/Safe Import Data Tables", false, 950)]
    public static void 导入()
    {
        // 先警告：有未保存的场景就先存（本工具只保住盘上的字节）
        int 脏场景 = 0;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (s.isDirty) 脏场景++;
        }
        if (脏场景 > 0)
        {
            bool 继续 = EditorUtility.DisplayDialog("安全导入配置表",
                "有 " + 脏场景 + " 个场景还没存盘。\n\n" +
                "本工具会**按盘上的字节**还原，所以**内存里没存的改动不会丢在内存里、但也不会被写进盘**。\n" +
                "建议先 Ctrl+S 存盘再跑。要继续吗？", "先存盘再说", "继续（我确认）");
            if (继续) return;                    // 第一个按钮 = 先去存盘
        }

        // 1) 记下所有"已打开的场景"的原始字节
        var 备份 = new Dictionary<string, byte[]>();
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var s = EditorSceneManager.GetSceneAt(i);
            if (string.IsNullOrEmpty(s.path) || !File.Exists(s.path)) continue;
            备份[s.path] = File.ReadAllBytes(s.path);
        }
        Debug.Log("[安全导入] 记下 " + 备份.Count + " 个已打开场景的原始字节");

        // 2) 跑导入器（它会顺手保存当前场景）
        DataTableImporter.ImportAll();

        // 3) 把场景字节原样写回（导入器造成的那部分一并抹掉；用户已保存的改动本来就在这份字节里）
        int 还原 = 0, 变过 = 0;
        foreach (var kv in 备份)
        {
            if (!File.Exists(kv.Key)) continue;
            var 现在 = File.ReadAllBytes(kv.Key);
            if (现在.Length == kv.Value.Length)
            {
                bool 一样 = true;
                for (int i = 0; i < 现在.Length; i++) if (现在[i] != kv.Value[i]) { 一样 = false; break; }
                if (一样) continue;
            }
            变过++;
            // 想留一份"导入器把它写成什么样"用于比对（默认不留）
            if (System.Environment.GetEnvironmentVariable("DSH_导出被改写场景") == "1")
            {
                string 目录 = Path.Combine("Temp", "导入器改写的场景");
                Directory.CreateDirectory(目录);
                File.WriteAllBytes(Path.Combine(目录, Path.GetFileName(kv.Key)), 现在);
            }
            File.WriteAllBytes(kv.Key, kv.Value);
            还原++;
        }

        if (还原 > 0)
        {
            AssetDatabase.Refresh();
            Debug.Log("[安全导入] ✔ 已把 " + 还原 + " 个场景还原成导入前的字节（导入器本来改写了它们）。"
                      + "**你保存过的场景改动不受影响** —— 因为还原的就是「含你那笔改动」的那份字节。");
        }
        else
        {
            Debug.Log("[安全导入] 导入器这次没有改写任何已打开的场景（无需还原）");
        }

        Debug.Log("[安全导入] 完成。提示：**以后不要再对场景目录做整目录 `git checkout`** —— "
                  + "那会把用户/AI 保存过的实质改动一起冲掉（这正是「组件被重置」的来源）。");
    }

    // ---- ASCII 别名 ----
    public static void Run() { 导入(); }
}
