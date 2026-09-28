using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// **场景一致性体检 / 自动补齐**（菜单：修仙/体检/场景一致性）。
///
/// 为什么做这个（用户 2026-09-27 的原话）：
///   「每次搞点什么新的东西，一去其他的场景又不行，一看，哦又是什么东西没有同步过来」
///
/// 这一类 bug 的共同形状：**某个服务 / 组件 / 数据只在某一个场景里配了**，其他场景缺。
/// 实测踩过的（都是这个形状）：
///   · `演出锁` 只有古古镇和宗门有 → 个人洞府 / 宗门野外 / 镇妖塔 演过场时玩家能乱跑
///   · `任务管理器` 只有古古镇和宗门有 → 传送到另外三个场景，主线进度"没地方放"，回来就重放剧情
///   · `UIPanelData` 的"全表"只有古古镇填了 → 在宗门打开角色面板，神通页 / 背包页全是空的
///   · `CameraYawRotator` 只有古古镇和宗门有 → 调机位的工具在别的场景按【】没反应
///
/// 用法：
///   · **体检**：`修仙/体检/场景一致性（只报告）` —— 不改任何东西，只把差异列出来
///   · **补齐**：`修仙/体检/场景一致性（并补齐缺失）` —— 往缺的场景补上（会保存场景）
///
/// ⚠️ 加完新功能（新组件、新表、新服务）之后**跑一次体检**，就知道漏了哪个场景。
/// </summary>
public static class 场景一致性体检
{
    /// <summary>「每个游戏场景都应该有」的东西。找 = 在场景里怎么找它</summary>
    class 应备项
    {
        public string 名字;
        /// <summary>类型全名（Assembly-CSharp 里）</summary>
        public string 类型名;
        /// <summary>true = 必须挂在名叫 Player 的物体上（没有 Player 就按"场景整体"报缺）</summary>
        public bool 挂Player;
        /// <summary>缺失时怎么补：null = 只报告不补</summary>
        public System.Action<Scene, System.Type> 补齐;
        /// <summary>期望数量：-1 = 至少 1 个</summary>
        public int 期望 = 1;
    }

    static readonly string[] 游戏场景 = new string[]
    {
        // ⚠️ 用**真实文件名**的大小写：`village.scene` 是小写，
        //    而 Build Settings 里登记成 `Village.scene`（Windows 不区分所以一直没暴露）。
        //    这里按文件名写，比较场景名时再统一转小写，免得踩大小写的坑。
        "Assets/Scenes/village.scene",
        "Assets/Scenes/Sect.scene",
        "Assets/Scenes/3C_Testbed.scene",
        "Assets/Scenes/Sect_Wilderness.scene",
        "Assets/Scenes/Demon-Suppressing Tower.scene",
    };

    static List<应备项> 建清单()
    {
        var 单 = new List<应备项>();

        单.Add(new 应备项 {
            名字 = "任务管理器", 类型名 = "任务管理器",
            补齐 = (场景, t) => 场景内新建(场景, "任务管理器", t),
        });
        单.Add(new 应备项 {
            名字 = "UIPanelData", 类型名 = "UIPanelData",
            补齐 = (场景, t) => { /* 面板缺失要整体重建，风险大 → 只报告 */ },
        });
        单.Add(new 应备项 {
            名字 = "跨场景数据", 类型名 = "跨场景数据",
            补齐 = (场景, t) => { /* 同上，只报告 */ },
        });
        单.Add(new 应备项 {
            名字 = "演出锁", 类型名 = "演出锁", 挂Player = true,
            补齐 = (场景, t) => 给玩家挂(场景, t),
        });
        单.Add(new 应备项 {
            名字 = "CameraYawRotator", 类型名 = "CameraYawRotator", 挂Player = false,
            补齐 = (场景, t) => 场景内新建(场景, "CameraYawRotator", t),
        });
        return 单;
    }

    [MenuItem("修仙/体检/场景一致性（只报告）", false, 200)]
    static void 只报告() => 跑(false);

    [MenuItem("修仙/体检/场景一致性（并补齐缺失）", false, 201)]
    static void 补齐() => 跑(true);

    static void 跑(bool 补)
    {
        var 清单 = 建清单();
        var 报 = new StringBuilder();
        报.Append("=== 场景一致性").Append(补 ? "（补齐模式）" : "（只报告）").Append(" ===\n\n");

        var 当前场景路径 = SceneManager.GetActiveScene().path;
        int 总缺 = 0, 总补 = 0;

        // 表格：行 = 项，列 = 场景
        var 表 = new Dictionary<string, Dictionary<string, int>>();
        var 场景短名 = new List<string>();

        foreach (var 路径 in 游戏场景)
        {
            if (!System.IO.File.Exists(路径)) { 报.Append("⚠ 场景文件不存在：").Append(路径).Append('\n'); continue; }
            var 场景 = EditorSceneManager.OpenScene(路径, OpenSceneMode.Single);
            var 短 = System.IO.Path.GetFileNameWithoutExtension(路径);
            场景短名.Add(短);
            var 行 = new Dictionary<string, int>();
            int 本场景补 = 0;      // ★ 必须是本场景的计数：用全局累加会导致前几个场景漏保存

            foreach (var 项 in 清单)
            {
                var t = System.Type.GetType(项.类型名 + ", Assembly-CSharp");
                if (t == null) { 行[项.名字] = -2; continue; }
                int n = 场景内数量(场景, t, 项.挂Player);
                行[项.名字] = n;
                if (n < 项.期望)
                {
                    总缺++;
                    if (补 && 项.补齐 != null)
                    {
                        项.补齐(场景, t);
                        本场景补++;
                        总补++;
                    }
                }
            }
            if (补 && 本场景补 > 0)
            {
                EditorSceneManager.MarkSceneDirty(场景);
                EditorSceneManager.SaveScene(场景);
            }
            表[短] = 行;
        }

        // 输出表格
        报.Append("项 / 场景");
        foreach (var s in 场景短名) 报.Append('\t').Append(s);
        报.Append('\n');
        foreach (var 项 in 清单)
        {
            报.Append(项.名字);
            var 值 = new List<int>();
            foreach (var s in 场景短名) { int v; 表[s].TryGetValue(项.名字, out v); 值.Add(v); 报.Append('\t').Append(v); }
            for (int i = 1; i < 值.Count; i++) if (值[i] != 值[0]) { 报.Append("\t★不一致"); break; }
            报.Append('\n');
        }

        报.Append("\n共 ").Append(总缺).Append(" 处缺失");
        if (补) 报.Append("，已补 ").Append(总补).Append(" 处（UIPanelData / 跨场景数据 缺了只能手工重建，不会自动补）");
        报.Append('\n');

        if (!string.IsNullOrEmpty(当前场景路径)) EditorSceneManager.OpenScene(当前场景路径, OpenSceneMode.Single);

        // ---- 传送路线体检（2026-09-28 加）----
        // 为什么单独做：传送落点缺失**不是"组件缺不缺"**，是"数据指向了一个不存在的物体"，
        // 上面那张"项 / 场景"表查不出来。实测就是因此漏了 7 条路线：
        // 玩家被放到场景默认点上，表现成"传送了但没落在落点"。
        报.Append('\n').Append(传送路线体检());

        Debug.Log(报.ToString());
        EditorUtility.DisplayDialog("场景一致性体检", 报.ToString(), "好");
    }

    /// <summary>
    /// 逐个检查每条传送路线的**落点物体在目标场景里存不存在**。
    ///
    /// 这一类 bug 的形状（2026-09-28 实测踩到）：
    ///   传送门的「落点」字段填了一个名字，而那个名字在目标场景里**根本没有对应物体**
    ///   → `找场景物体` 返回 null → 玩家留在**场景默认点**上，
    ///   表现成"传送过去了，但站的地方不对"（而不是报错，所以很难发现）。
    ///
    /// 另外：落点常常和它对应的传送门**同名**（`sect1 to 3c` 既是门也是落点）。
    /// 这是**允许的、而且是现在的设计** —— 落点就用目标场景自己那道门的点位。
    /// 因为传送已经改成"按 F 才开面板"（见 `Teleporter`），
    /// 落在门上不会自己弹面板，不会再出现老的"来回弹"。
    /// </summary>
    static string 传送路线体检()
    {
        var 报 = new StringBuilder();
        报.Append("--- 传送路线体检 ---\n");

        // 先把所有游戏场景里"能被按名字找到的物体"收集起来（含 inactive）
        var 物件 = new Dictionary<string, HashSet<string>>();   // 场景名(小写) → 物体名集合
        foreach (var 路径 in 游戏场景)
        {
            if (!System.IO.File.Exists(路径)) continue;
            var 场景 = EditorSceneManager.OpenScene(路径, OpenSceneMode.Single);
            var 集 = new HashSet<string>();
            foreach (var g in 场景.GetRootGameObjects())
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) 集.Add(t.name);
            物件[场景.name.ToLowerInvariant()] = 集;
        }

        int 坏 = 0, 总 = 0;
        foreach (var 路径 in 游戏场景)
        {
            if (!System.IO.File.Exists(路径)) continue;
            var 场景 = EditorSceneManager.OpenScene(路径, OpenSceneMode.Single);
            foreach (var g in 场景.GetRootGameObjects())
                foreach (var tp in g.GetComponentsInChildren<Teleporter>(true))
                {
                    if (tp.选项 == null) continue;
                    foreach (var o in tp.选项)
                    {
                        if (o == null || o.暂未开放) continue;
                        总++;
                        string 目标 = string.IsNullOrEmpty(o.场景) ? 场景.name : o.场景;
                        // ⚠️ 两边都转小写再比：Build Settings 里登记的是 `Village`，
                        //    而场景对象 / 文件名是小写 `village` —— 不归一化会误报"落点不存在"。
                        string 键 = 目标.ToLowerInvariant();
                        bool 场景在 = 物件.ContainsKey(键);
                        bool 落点在 = 场景在 && 物件[键].Contains(o.落点);
                        if (落点在) continue;

                        坏++;
                        报.Append("  ✗ ").Append(场景.name).Append(" / ").Append(tp.gameObject.name)
                           .Append(" 「").Append(o.名称).Append("」 → ").Append(目标)
                           .Append(" 的落点 [").Append(o.落点).Append("] ");
                        报.Append(场景在 ? "**目标场景里没有这个物体** → 玩家会落在场景默认点上\n"
                                          : "**目标场景不在体检清单里**（检查拼写 / Build Settings）\n");
                    }
                }
        }

        报.Append(坏 == 0 ? "  共 " + 总 + " 条传送路线，落点全部可解析 ✓\n"
                          : "  共 " + 总 + " 条路线，" + 坏 + " 条落点解析不了 ✗\n");
        return 报.ToString();
    }

    static int 场景内数量(Scene 场景, System.Type t, bool 只算玩家身上)
    {
        int n = 0;
        foreach (var o in Object.FindObjectsOfType(t, true))
        {
            var c = o as Component;
            if (c == null || c.gameObject.scene != 场景) continue;
            if (只算玩家身上 && !是玩家(c.gameObject)) continue;
            n++;
        }
        return n;
    }

    static bool 是玩家(GameObject go)
    {
        if (go.name == "Player") return true;
        var v = go.GetComponent("PlayerVitals");
        return v != null;
    }

    static void 场景内新建(Scene 场景, string 名, System.Type t)
    {
        var go = new GameObject(名);
        EditorSceneManager.MoveGameObjectToScene(go, 场景);
        go.AddComponent(t);
        Debug.Log("[场景体检] " + 场景.name + "：补上「" + 名 + "」");
    }

    static void 给玩家挂(Scene 场景, System.Type t)
    {
        foreach (var o in Object.FindObjectsOfType(System.Type.GetType("PlayerVitals, Assembly-CSharp"), true))
        {
            var c = o as Component;
            if (c == null || c.gameObject.scene != 场景) continue;
            if (c.gameObject.GetComponent(t) == null)
            {
                c.gameObject.AddComponent(t);
                Debug.Log("[场景体检] " + 场景.name + "：给玩家补上「" + t.Name + "」");
            }
            return;
        }
        Debug.LogWarning("[场景体检] " + 场景.name + "：找不到玩家，无法补「" + t.Name + "」");
    }
}
