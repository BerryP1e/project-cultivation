using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 文件轮询式的编辑器控制通道 —— 用来替代挂掉的 MCP 桥。
///
/// 原理：本脚本带 [InitializeOnLoad]，Unity 一编译完就会加载它；
/// 之后每帧检查命令文件，有命令就执行，把结果写回结果文件。
/// 这样不需要任何外部进程/端口，只要 Unity 活着就能被驱动。
///
/// 命令文件  <仓库根>\.dsh\cmd.txt     每行一条命令
/// 结果文件  <仓库根>\.dsh\result.txt
///
/// 路径**不写死盘符** —— 家里电脑在 D:\project：cultivation，工作电脑在
/// E:\game project，写死会让另一台整个桥失效（轮询一个不存在的路径，
/// 表现就是「alive.txt 不更新、cmd.txt 没人消费」）。
/// 这里从 Application.dataPath 反推仓库根，两台机器都不用改。
///
/// 支持的命令：
///   refresh                 AssetDatabase.Refresh()
///   menu:<菜单路径>          执行菜单项，例如 menu:修仙/生成开始界面场景
///   console:clear           清空 Console
///   console:get[:N]         取最近 N 条日志
///   play:on / play:off      进出播放模式
///   save                    保存当前场景 + 资产
///   open:<场景路径>          打开场景
///   echo:<文本>             回显（测试通道是否活着）
///   shot[:路径]             把主相机渲染成 PNG
///   dump                    打印当前场景结构与 Build Settings
///   ui:open / ui:close / ui:tab:<索引>  运行时开关角色面板或切换页签（只用于截图验收，不保存场景）
///   screen:<宽>x<高>        运行时切换窗口分辨率（双分辨率验收）
/// </summary>
[InitializeOnLoad]
public static class DshBridge
{
    // Unity 工程在 <仓库根>\cultivation\，所以 dataPath = <仓库根>\cultivation\Assets，
    // 上推两级就是仓库根。
    static readonly string 仓库根 =
        Path.GetDirectoryName(Path.GetDirectoryName(Application.dataPath));

    static string 命令文件   => Path.Combine(仓库根, @".dsh\cmd.txt");
    static string 结果文件   => Path.Combine(仓库根, @".dsh\result.txt");
    static string 心跳文件   => Path.Combine(仓库根, @".dsh\alive.txt");
    static string 截图目录   => Path.Combine(仓库根, @"cultivation\screenshots");

    static double 上次心跳;
    static readonly List<string> 日志缓存 = new List<string>();
    static readonly List<string> 错误缓存 = new List<string>();

    static DshBridge()
    {
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            {
                错误缓存.Add("[" + type + "] " + msg + "\n" + stack);
                if (错误缓存.Count > 100) 错误缓存.RemoveAt(0);
            }
            日志缓存.Add("[" + type + "] " + msg + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : ""));
            if (日志缓存.Count > 400) 日志缓存.RemoveRange(0, 200);
        };
        EditorApplication.update += 轮询;
        Debug.Log("[DshBridge] 文件控制通道已启动，仓库根 = " + 仓库根);
    }

    static void 轮询()
    {
        // 心跳，方便外部确认 Unity 还活着
        if (EditorApplication.timeSinceStartup - 上次心跳 > 2.0)
        {
            上次心跳 = EditorApplication.timeSinceStartup;
            try { File.WriteAllText(心跳文件, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); } catch { }
        }

        if (!File.Exists(命令文件)) return;

        string 内容;
        try { 内容 = File.ReadAllText(命令文件); }
        catch { return; }

        // 读完立刻删掉，避免重复执行
        try { File.Delete(命令文件); } catch { }

        var 输出 = new StringBuilder();
        foreach (var 原始行 in 内容.Split('\n'))
        {
            var 行 = 原始行.Trim('\r', ' ', '\t');
            if (行.Length == 0) continue;
            try { 执行(行, 输出); }
            catch (Exception e) { 输出.Append("!! ").Append(行).Append(" 异常: ").Append(e.Message).Append('\n'); }
        }

        try { File.WriteAllText(结果文件, 输出.ToString()); } catch { }
    }

    static void 执行(string 行, StringBuilder 输出)
    {
        int 冒号 = 行.IndexOf(':');
        string 命令 = 冒号 < 0 ? 行 : 行.Substring(0, 冒号);
        string 参数 = 冒号 < 0 ? "" : 行.Substring(冒号 + 1);

        switch (命令)
        {
            case "echo":
                输出.Append("OK echo: ").Append(参数).Append('\n');
                break;

            case "refresh":
                AssetDatabase.Refresh();
                输出.Append("OK refresh\n");
                break;

            case "menu":
                {
                    bool ok = EditorApplication.ExecuteMenuItem(参数);
                    输出.Append(ok ? "OK menu: " : "FAIL menu（没这个菜单项？）: ").Append(参数).Append('\n');
                }
                break;

            case "console":
                if (参数 == "clear") { 日志缓存.Clear(); 错误缓存.Clear(); 输出.Append("OK console cleared\n"); }
                else if (参数 == "errors")
                {
                    输出.Append("Errors=").Append(错误缓存.Count).Append('\n');
                    foreach (var error in 错误缓存) 输出.Append(error).Append('\n');
                }
                else
                {
                    int n = 30;
                    int c2 = 参数.IndexOf(':');
                    if (c2 >= 0) int.TryParse(参数.Substring(c2 + 1), out n);
                    int 起 = Mathf.Max(0, 日志缓存.Count - n);
                    输出.Append("=== console 最近 ").Append(日志缓存.Count - 起).Append(" 条 ===\n");
                    for (int i = 起; i < 日志缓存.Count; i++) 输出.Append(日志缓存[i]).Append('\n');
                }
                break;

            case "play":
                EditorApplication.isPlaying = 参数 == "on";
                输出.Append("OK play=").Append(参数).Append('\n');
                break;

            case "save":
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
                AssetDatabase.SaveAssets();
                输出.Append("OK save\n");
                break;

            case "open":
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(参数);
                输出.Append("OK open: ").Append(参数).Append('\n');
                break;

            case "shot":
                {
                    // 把当前场景的主相机渲染成 PNG，用于视觉确认
                    var cam = Camera.main;
                    if (cam == null) { 输出.Append("FAIL shot: 没有 Main Camera\n"); break; }
                    int w = 1280, h = 720;

                    // 截图是要看场景/角色，不是看 UI —— 把 Canvas 整个临时关掉。
                    // （之前试图把 Overlay 切成 ScreenSpaceCamera，结果 UI 反而盖满整张图。）
                    var 关掉的Canvas = new System.Collections.Generic.List<GameObject>();
                    foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                        if (c.gameObject.activeSelf) { c.gameObject.SetActive(false); 关掉的Canvas.Add(c.gameObject); }
                    var rt = new RenderTexture(w, h, 24);
                    var 旧target = cam.targetTexture;
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    cam.targetTexture = 旧target;
                    RenderTexture.active = null;
                    var png = tex.EncodeToPNG();
                    string outPath = 参数.Length > 0 ? 参数 : Path.Combine(截图目录, "dsh_shot.png");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
                    System.IO.File.WriteAllBytes(outPath, png);
                    foreach (var g in 关掉的Canvas) g.SetActive(true);
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(tex);
                    输出.Append("OK shot: ").Append(outPath).Append('\n');
                }
                break;

            case "bird":
                {
                    // 鸟瞰截图：临时把主相机抬高俯视，拍完还原。用于看村庄整体布局。
                    var cam = Camera.main;
                    if (cam == null) { 输出.Append("FAIL bird: 没有 Main Camera\n"); break; }
                    var 旧pos = cam.transform.position;
                    var 旧rot = cam.transform.rotation;
                    var 旧fov = cam.fieldOfView;

                    // 参数: 高度[,半径]  默认 高度55 半径62
                    float 高 = 55f, 半径 = 62f;
                    if (!string.IsNullOrEmpty(参数))
                    {
                        var parts = 参数.Split(',');
                        if (parts.Length > 0) float.TryParse(parts[0], out 高);
                        if (parts.Length > 1) float.TryParse(parts[1], out 半径);
                    }

                    // 鸟瞰是要看地形，不是看 UI —— 把 Canvas 整个临时关掉，
                    // 否则角色面板之类的会盖满屏幕（第一次拍就是这样）。
                    var 关掉的Canvas = new System.Collections.Generic.List<GameObject>();
                    foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                        if (c.gameObject.activeSelf) { c.gameObject.SetActive(false); 关掉的Canvas.Add(c.gameObject); }

                    var look = new Vector3(0f, 0f, 0f);
                    cam.transform.position = new Vector3(半径, 高, -半径);
                    cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position, Vector3.up);
                    cam.fieldOfView = 55f;

                    int w = 1400, h = 900;
                    var rt = new RenderTexture(w, h, 24);
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                    cam.targetTexture = null; RenderTexture.active = null;
                    string op = Path.Combine(截图目录, "bird.png");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(op));
                    System.IO.File.WriteAllBytes(op, tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);

                    cam.transform.position = 旧pos; cam.transform.rotation = 旧rot; cam.fieldOfView = 旧fov;
                    foreach (var g in 关掉的Canvas) g.SetActive(true);
                    输出.Append("OK bird: ").Append(op).Append('\n');
                }
                break;

            case "shot2":
                {
                    // 用 Unity 自己的截图路径（ScreenCapture）作对照，
                    // 验证我那个 Camera.Render() 版本是不是渲染得偏暗。
                    string outPath = 参数.Length > 0 ? 参数 : Path.Combine(截图目录, "dsh_shot2.png");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
                    ScreenCapture.CaptureScreenshot(outPath);
                    输出.Append("OK shot2 (ScreenCapture): ").Append(outPath)
                        .Append("   isPlaying=").Append(EditorApplication.isPlaying).Append('\n');
                }
                break;

            case "dump":
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    输出.Append("=== 场景 ").Append(scene.name).Append(" ===\n");
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        输出.Append("* ").Append(root.name).Append("  [").Append(root.GetComponents<Component>().Length).Append(" comps]\n");
                        foreach (Transform c in root.transform)
                        {
                            输出.Append("    - ").Append(c.name)
                              .Append("  pos=").Append(c.position.ToString("F2"));
                            var rs = c.GetComponentsInChildren<Renderer>();
                            if (rs.Length > 0)
                            {
                                var b = rs[0].bounds;
                                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                                输出.Append("  size=").Append(b.size.ToString("F1"))
                                  .Append("  y范围=").Append(b.min.y.ToString("F2")).Append("~").Append(b.max.y.ToString("F2"));
                            }
                            输出.Append('\n');
                            if (c.childCount > 0 && c.childCount <= 6)
                                foreach (Transform gc in c) 输出.Append("        .").Append(gc.name)
                                    .Append(" pos=").Append(gc.position.ToString("F1")).Append('\n');
                        }
                    }
                    输出.Append("Build Settings:\n");
                    foreach (var s in EditorBuildSettings.scenes)
                        输出.Append("   ").Append(s.enabled ? "[x] " : "[ ] ").Append(s.path).Append('\n');
                }
                break;

            case "quest":
                {
                    // 查主线的真实运行时状态：进度字符串 + 各任务当前阶段。
                    // 「导入进度没接上」和「导出的串是空的」表现一样，必须直接看串。
                    输出.Append("=== 任务管理器 ===\n");
                    Type 类型 = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("任务管理器", false);
                        if (t != null) { 类型 = t; break; }
                    }
                    输出.Append("类型 = ").Append(类型 != null ? 类型.FullName : "找不到").Append('\n');
                    if (类型 == null) break;

                    var 实例属性 = 类型.GetProperty("实例",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var 实例 = 实例属性?.GetValue(null);
                    输出.Append("实例 = ").Append(实例 != null ? "存在" : "null（场景里没有任务管理器）").Append('\n');
                    if (实例 == null) break;

                    var m导 = 类型.GetMethod("导出进度");
                    var 串 = m导?.Invoke(实例, null) as string;
                    输出.Append("导出进度() = [").Append(串 ?? "(null)").Append("]\n");
                    输出.Append("串长度 = ").Append(串?.Length ?? -1).Append('\n');

                    var f当前 = 类型.GetField("当前阶段",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var d当前 = f当前?.GetValue(实例) as System.Collections.IDictionary;
                    输出.Append("当前阶段 条目数 = ").Append(d当前?.Count ?? -1).Append('\n');
                    if (d当前 != null)
                        foreach (System.Collections.DictionaryEntry e in d当前)
                            输出.Append("   ").Append(e.Key).Append(" : ").Append(e.Value).Append('\n');

                    var f完成 = 类型.GetField("已完成任务",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var s完成 = f完成?.GetValue(实例) as System.Collections.IEnumerable;
                    输出.Append("已完成任务 = ");
                    if (s完成 != null) foreach (var x in s完成) 输出.Append(x).Append("  ");
                    输出.Append('\n');

                    var f已执 = 类型.GetField("已执行动作",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var s已执 = f已执?.GetValue(null) as System.Collections.IEnumerable;
                    输出.Append("已执行动作 = ");
                    if (s已执 != null) foreach (var x in s已执) 输出.Append(x).Append("  ");
                    输出.Append('\n');
                }
                break;

            case "stages":
                {
                    // 读任务定义资产（只读，不改任何状态）：确认 CSV 改完重导后真的生效。
                    // 参数可给任务id，默认 q_main_004。
                    string 任务id = string.IsNullOrEmpty(参数) ? "q_main_004" : 参数;
                    Type 库类 = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("QuestDatabase", false);
                        if (t != null) { 库类 = t; break; }
                    }
                    if (库类 == null) { 输出.Append("找不到 QuestDatabase\n"); break; }

                    var m取 = 库类.GetMethod("取", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var m任务 = 库类.GetMethod("取任务");
                    if (m取 == null || m任务 == null) { 输出.Append("取/取任务 方法找不到\n"); break; }
                    var 库 = m取.Invoke(null, null);
                    if (库 == null) { 输出.Append("QuestDatabase.取() 返回 null\n"); break; }

                    var 列表 = m任务.Invoke(库, new object[] { 任务id }) as System.Collections.IEnumerable;
                    if (列表 == null) { 输出.Append("取任务 返回 null\n"); break; }

                    输出.Append("=== ").Append(任务id).Append(" 的阶段（资产实读） ===\n");
                    foreach (var q in 列表)
                    {
                        if (q == null) continue;
                        var ty = q.GetType();
                        object 取(string n) => ty.GetField(n)?.GetValue(q) ?? ty.GetProperty(n)?.GetValue(q);
                        输出.Append("  阶段 ").Append(取("阶段")).Append("  ").Append(取("阶段名"))
                            .Append("  条件=").Append(取("条件"))
                            .Append("  动作=").Append(取("动作"))
                            .Append("  目标=").Append(取("动作目标npcId"))
                            .Append("  参数=").Append(取("动作参数"))
                            .Append("  坐标=").Append(取("坐标"))
                            .Append("  速度=").Append(取("动作速度"))
                            .Append('\n');
                    }
                }
                break;

            case "curtain":
                {
                    // 查运行时黑幕字幕的状态：字体是不是中文字体、文本有没有内容、幕/字是否可见。
                    // 症状「黑幕起了但一个字都没有」多半是字体回落到了
                    // LegacyRuntime.ttf（内置字体，不含中文）。
                    Type 类型 = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("黑幕字幕", false);
                        if (t != null) { 类型 = t; break; }
                    }
                    输出.Append("黑幕字幕 类型 = ").Append(类型 != null ? "找到" : "找不到").Append('\n');
                    if (类型 == null) break;

                    var 实例属性 = 类型.GetProperty("实例",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var 实例 = 实例属性?.GetValue(null);
                    输出.Append("实例 = ").Append(实例 != null ? "存在" : "null（还没建过黑幕）").Append('\n');
                    if (实例 == null) break;

                    var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var f文本 = 类型.GetField("文本", F);
                    var f幕 = 类型.GetField("幕", F);
                    var f画布 = 类型.GetField("画布", F);
                    var f字体 = 类型.GetField("字体", F);
                    var f全句 = 类型.GetField("全句", F);
                    var f色 = 类型.GetField("字色", F);

                    var 字体 = f字体?.GetValue(实例) as Font;
                    输出.Append("字体字段 = ").Append(字体 != null ? 字体.name : "(空)").Append('\n');
                    输出.Append("字色 = ").Append(f色?.GetValue(实例)).Append('\n');
                    输出.Append("全句 = [").Append(f全句?.GetValue(实例)).Append("]\n");

                    var 文本 = f文本?.GetValue(实例) as UnityEngine.UI.Text;
                    if (文本 != null)
                    {
                        输出.Append("文本.text = [").Append(文本.text).Append("]\n");
                        输出.Append("文本.font = ").Append(文本.font != null ? 文本.font.name : "(null)").Append('\n');
                        输出.Append("文本.color = ").Append(文本.color).Append('\n');
                        输出.Append("文本.enabled = ").Append(文本.enabled)
                            .Append("  go.activeInHierarchy = ").Append(文本.gameObject.activeInHierarchy).Append('\n');
                        输出.Append("文本 尺寸 = ").Append(文本.rectTransform.rect.size).Append('\n');
                    }
                    else 输出.Append("文本 组件取不到\n");

                    var 幕 = f幕?.GetValue(实例) as UnityEngine.UI.Image;
                    输出.Append("幕 = ").Append(幕 != null ? (幕.gameObject.activeSelf ? "显示中" : "隐藏") : "(null)").Append('\n');
                    var 画布 = f画布?.GetValue(实例) as Canvas;
                    输出.Append("画布.enabled = ").Append(画布 != null ? 画布.enabled.ToString() : "(null)").Append('\n');
                }
                break;

            case "ui":
                {
                    // 施工验收用的运行时小入口：只调用现有 CharacterPanelUI，
                    // 不改场景、不写存档，避免依赖窗口焦点发送按键。
                    var panel = UnityEngine.Object.FindObjectOfType<CharacterPanelUI>();
                    if (panel == null) { 输出.Append("FAIL ui: 找不到 CharacterPanelUI\n"); break; }

                    if (参数 == "open")
                    {
                        panel.SetOpen(true);
                        输出.Append("OK ui: character panel open\n");
                        break;
                    }
                    if (参数 == "close")
                    {
                        panel.SetOpen(false);
                        输出.Append("OK ui: character panel close\n");
                        break;
                    }

                    const string tabPrefix = "tab:";
                    if (参数.StartsWith(tabPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(参数.Substring(tabPrefix.Length), out var index))
                        {
                            输出.Append("FAIL ui: tab index invalid\n");
                            break;
                        }
                        panel.ShowTabByIndex(index);
                        输出.Append("OK ui: character tab ").Append(index).Append('\n');
                        break;
                    }

                    输出.Append("FAIL ui: unsupported parameter ").Append(参数).Append('\n');
                }
                break;

            case "inkqa":
                输出.Append(InkUIQa.Panel(参数)).Append('\n');
                break;

            case "screen":
                {
                    var parts = 参数.ToLowerInvariant().Split('x');
                    if (parts.Length != 2 || !int.TryParse(parts[0], out var width)
                        || !int.TryParse(parts[1], out var height) || width < 640 || height < 360)
                    {
                        输出.Append("FAIL screen: expected widthxheight\n");
                        break;
                    }
                    输出.Append("OK screen: ").Append(InkUIQa.SetSize(width, height)).Append('\n');
                }
                break;

            default:
                输出.Append("?? 未知命令: ").Append(行).Append('\n');
                break;
        }
    }

    // ---- 供菜单手动确认通道可用 ----
    [MenuItem("修仙/测试控制通道")]
    [MenuItem("Cultivation/Test Dsh Bridge")]
    public static void 测试()
    {
        Debug.Log("[DshBridge] 控制通道正常，心跳文件 " + 心跳文件);
    }
}
