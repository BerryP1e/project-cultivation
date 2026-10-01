using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **特效资源清单工具** —— 导入或整理特效资产包之后跑它，把整个 `Assets/resources/特效` 一把生成一张可读的表。
///
/// 为什么需要：特效包里动辄几百个 prefab、命名还是英文缩写，光看目录根本不知道哪个能当子弹、
/// 哪个是命中爆炸、哪个是循环拖尾。这个工具把关键信息抠出来（路径 / 粒子数 / 尺寸 / 特征），
/// 写成一份 markdown 供人和 AI 查。
///
/// **输出只有一份**：`docs/guides/特效资源清单.md`。
/// （历史：以前是"选中一个目录生成一份"，于是散成 飞弹/法术/战斗法术/传送/命中 五份，
/// 体积虚高又难查；2026 本轮合并成一份，并按"共同目录前缀"省掉每条路径里重复的头。）
///
/// 菜单：修仙 / 资源整理 / 生成特效资源清单（全部）
/// </summary>
public static class NpcEffectInventory
{
    /// <summary>特效总根（分类目录都在它下面）</summary>
    const string 特效根 = "Assets/resources/特效";

    /// <summary>分类的展示顺序；不在这张表里的目录按名字排在后面</summary>
    static readonly string[] 已知分类 = { "飞弹", "法术", "战斗法术", "传送", "命中" };

    [MenuItem("修仙/资源整理/生成特效资源清单（全部）")]
    public static void 生成全部菜单() => 生成全部();

    // ============================================================ 主流程

    /// <summary>扫描 `Assets/resources/特效` 下所有分类目录，写成一份合并清单。</summary>
    public static void 生成全部()
    {
        if (!Directory.Exists(特效根))
        {
            Debug.LogError("[资源清单] 找不到目录：" + 特效根);
            return;
        }

        var 分类名 = new List<string>();
        foreach (var n in 已知分类)
            if (Directory.Exists(特效根 + "/" + n)) 分类名.Add(n);
        foreach (var d in Directory.GetDirectories(特效根))
        {
            string n = Path.GetFileName(d);
            if (!分类名.Contains(n)) 分类名.Add(n);
        }
        if (分类名.Count == 0) { Debug.LogWarning("[资源清单] " + 特效根 + " 下没有子目录"); return; }

        var 分组 = new List<清单组>();
        int 总数 = 0;
        foreach (var n in 分类名)
        {
            var 条目 = new List<条目>();
            foreach (var f in Directory.GetFiles(特效根 + "/" + n, "*.prefab", SearchOption.AllDirectories))
                条目.Add(分析(f.Replace('\\', '/')));
            if (条目.Count == 0) continue;

            // 按路径排序（路径最后一段就是资产名，所以等价于按"子目录 + 名字"排）
            条目.Sort((a, b) => string.CompareOrdinal(a.资源路径, b.资源路径));
            分组.Add(new 清单组 { 名 = n, 前缀 = 共同目录前缀(条目), 条目 = 条目 });
            总数 += 条目.Count;
        }

        var 文本 = new StringBuilder();
        文本.AppendLine("# 特效资源清单");
        文本.AppendLine();
        文本.AppendLine("> **管什么**：`Assets/resources/特效/` 下**全部特效 prefab** 的路径、规模与特征，按分类分节，供挑特效 / 填路径用。");
        文本.AppendLine("> **不管什么**：目录约定、怎么加载、导入新包怎么整理 → [特效系统](特效系统.md)；某个特效怎么配到怪身上 → [飞弹与子弹](飞弹与子弹.md)。");
        文本.AppendLine("> **本文件怎么查**：先看「汇总」定位分类，再进对应小节按名字搜；每节开头有「统一前缀」，表里 `路径` = 前缀 + 表中值。");
        文本.AppendLine("> **来源**：由 `NpcEffectInventory` **自动生成**（菜单 **修仙 / 资源整理 / 生成特效资源清单（全部）**）。**改完资源重跑菜单，不要手改本文件。**");
        文本.AppendLine();
        文本.AppendLine("扫描根目录：`" + 特效根 + "`　共 **" + 总数 + "** 个 prefab。");
        文本.AppendLine(">");
        文本.AppendLine("> **路径怎么读**：每个小节开头写了该节的「统一前缀」，表里 `路径` = **前缀 + 表中值**，");
        文本.AppendLine("> 拼起来就是能直接填进 `NpcAttackConfig.子弹特效路径` / `NpcAttackConfig.命中特效路径` 的 Resources 路径");
        文本.AppendLine("> （相对 `Assets/resources`、**不带扩展名**）。**每行路径的最后一段就是资产名。**");
        文本.AppendLine(">");
        文本.AppendLine("> **列义**");
        文本.AppendLine("> - `粒子` = 粒子系统个数（含子物体）");
        文本.AppendLine("> - `外径` = 散布半径 × 2 + 最大粒子尺寸（粗估，用来判断「要不要缩放」，不是精确包围盒）");
        文本.AppendLine("> - `特征` = **建议档位** + 标记。档位：`1` 飞行道具（适合当子弹）· `2` 单次爆发（适合当命中 / 爆炸）·");
        文本.AppendLine(">   `3` 循环效果（拖尾 / 常驻）· `8` 其他 · `9` 空壳（没粒子也没脚本）· `0` 读不出来。");
        文本.AppendLine(">   标记：`循环` = 有粒子勾了 loop，`动` = 有粒子带速度或挂了脚本（脚本名最多列 3 个，更多就写 `多脚本`）");
        文本.AppendLine(">");
        文本.AppendLine("> ⚠️ **`特征` 里的建议档位是按「粒子数 / 循环 / 会不会自己移动」猜的**：");
        文本.AppendLine("> 对 `特效/飞弹` 这种「一个主题 = 一份单体飞弹」的包准；");
        文本.AppendLine("> 对 `特效/法术`（一套法术 = 主 prefab + 一堆 Parts/Base 子件）**不准**");
        文本.AppendLine("> （实测 312 个里 171 个被误判成「飞行道具」）。看那个包时只看路径和外径。");
        文本.AppendLine(">");
        文本.AppendLine("> 目录约定、怎么加载、怎么导入新包 → [特效系统](特效系统.md)。");
        文本.AppendLine();
        文本.AppendLine("---");
        文本.AppendLine();

        // ---- 汇总 ----
        文本.AppendLine("## 汇总");
        文本.AppendLine();
        文本.AppendLine("| 分类目录 | prefab | 建议档位分布 |");
        文本.AppendLine("|---|---|---|");
        foreach (var g in 分组)
            文本.AppendLine("| `" + 特效根 + "/" + g.名 + "` | " + g.条目.Count + " | " + 档位分布(g.条目) + " |");
        文本.AppendLine();
        文本.AppendLine("---");
        文本.AppendLine();

        // ---- 每类明细 ----
        foreach (var g in 分组)
        {
            文本.AppendLine("## `" + g.名 + "`（" + g.条目.Count + " 个）");
            文本.AppendLine();
            文本.AppendLine("> 统一前缀：`" + g.前缀 + "`");
            文本.AppendLine();
            文本.AppendLine("| 路径 | 粒子 | 外径 | 特征 |");
            文本.AppendLine("|---|---|---|---|");
            foreach (var e in g.条目)
            {
                string 尾 = e.资源路径.Length > g.前缀.Length ? e.资源路径.Substring(g.前缀.Length) : e.资源路径;
                文本.AppendLine("| `" + 尾 + "` | " + e.粒子数
                    + " | " + e.规模.ToString("0.#")
                    + " | " + 特征(e) + " |");
            }
            文本.AppendLine();
            if (g.名 != 分组[分组.Count - 1].名) { 文本.AppendLine("---"); 文本.AppendLine(); }
        }

        string 根 = Directory.GetParent(Application.dataPath)?.Parent?.FullName ?? Application.dataPath;
        string 文档目录 = Path.Combine(根, "docs", "guides");
        if (!Directory.Exists(文档目录)) Directory.CreateDirectory(文档目录);
        string 输出 = Path.Combine(文档目录, "特效资源清单.md");
        File.WriteAllText(输出, 文本.ToString(), new UTF8Encoding(true));

        Debug.Log("[资源清单] " + 分组.Count + " 个分类 / " + 总数 + " 个 prefab →\n  " + 输出
            + "\n  " + 汇总一行(分组));
        AssetDatabase.Refresh();
    }

    // ============================================================ 输出小工具

    class 清单组
    {
        public string 名;
        public string 前缀;             // 该分类所有路径的共同目录前缀（拼接用）
        public List<条目> 条目;
    }

    static string 档位分布(List<条目> 全部)
    {
        var 计数 = new SortedDictionary<string, int>();
        foreach (var e in 全部)
        {
            string k = e.建议.Substring(0, 1);
            计数[k] = 计数.ContainsKey(k) ? 计数[k] + 1 : 1;
        }
        var sb = new StringBuilder();
        foreach (var kv in 计数)
        {
            if (sb.Length > 0) sb.Append(" · ");
            sb.Append("`" + kv.Key + "` × " + kv.Value);
        }
        return sb.ToString();
    }

    static string 汇总一行(List<清单组> 分组)
    {
        var sb = new StringBuilder();
        foreach (var g in 分组)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(g.名 + " " + g.条目.Count);
        }
        return sb.ToString();
    }

    /// <summary>算出一批路径的"共同目录前缀"（切在 `/` 上），用来在表里省掉每条都重复的那一截。</summary>
    static string 共同目录前缀(List<条目> 全部)
    {
        string p = 全部[0].资源路径;
        foreach (var e in 全部)
        {
            int i = 0;
            while (i < p.Length && i < e.资源路径.Length && p[i] == e.资源路径[i]) i++;
            p = p.Substring(0, i);
            if (p.Length == 0) break;
        }
        int s = p.LastIndexOf('/');
        return s < 0 ? "" : p.Substring(0, s + 1);
    }

    /// <summary>`1 循环 动 脚本名` 这种一行摘要</summary>
    static string 特征(条目 e)
    {
        var parts = new List<string> { e.建议.Substring(0, 1) };
        if (e.循环) parts.Add("循环");
        if (e.会移动) parts.Add("动");

        var 名 = new List<string>();
        foreach (var s in e.脚本.Split('/'))
            if (s.Length > 0 && !名.Contains(s)) 名.Add(s);
        if (名.Count > 3) parts.Add("多脚本");
        else parts.AddRange(名);

        return string.Join(" ", parts);
    }

    // ============================================================ 分析一个 prefab

    class 条目
    {
        public string 资源路径;      // 可直接填进 子弹特效路径
        public string 建议;
        public int 粒子数;
        public bool 循环;
        public bool 会移动;
        public string 脚本 = "";
        public float 规模;           // 粗估外径
    }

    static 条目 分析(string assetPath)
    {
        var e = new 条目 { 资源路径 = 转Resources路径(assetPath) };

        var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (go == null) { e.建议 = "0 读不出来"; return e; }

        var 粒子们 = go.GetComponentsInChildren<ParticleSystem>(true);
        e.粒子数 = 粒子们.Length;

        bool 会移动 = false;
        var 脚本名 = new List<string>();

        // 粒子自己会不会飞
        foreach (var ps in 粒子们)
        {
            var main = ps.main;
            if (main.loop) e.循环 = true;

            var vel = ps.velocityOverLifetime;
            if (vel.enabled)
            {
                bool 有速度 = Mathf.Abs(vel.x.constantMax) > 0.01f
                           || Mathf.Abs(vel.y.constantMax) > 0.01f
                           || Mathf.Abs(vel.z.constantMax) > 0.01f;
                if (有速度) 会移动 = true;
            }
        }

        // 有没有脚本在推它（很多子弹包用脚本让特效飞）
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) 脚本名.Add(mb.GetType().Name);
        if (脚本名.Count > 0) 会移动 = true;
        e.脚本 = string.Join("/", 脚本名);

        e.会移动 = 会移动;

        // 规模：**别用 Renderer.bounds** —— prefab 没实例化进场景时它是退化的（量出来是 0）。
        // 对粒子特效改从「粒子大小 + 散布半径」估，这才是「要不要缩放」真正要看的东西。
        float 最大粒子 = 0f, 最大散布 = 0f;
        foreach (var ps in 粒子们)
        {
            最大粒子 = Mathf.Max(最大粒子, ps.main.startSize.constantMax);

            var shape = ps.shape;
            if (!shape.enabled) continue;
            switch (shape.shapeType)
            {
                case ParticleSystemShapeType.Sphere:
                case ParticleSystemShapeType.Hemisphere:
                case ParticleSystemShapeType.Circle:
                case ParticleSystemShapeType.Cone:
                case ParticleSystemShapeType.ConeShell:
                    最大散布 = Mathf.Max(最大散布, shape.radius);
                    break;
                case ParticleSystemShapeType.Box:
                    最大散布 = Mathf.Max(最大散布, shape.scale.x * 0.5f, shape.scale.z * 0.5f);
                    break;
            }
        }
        e.规模 = 最大散布 * 2f + 最大粒子;      // 粗估外径，够判断量级

        e.建议 = 判建议(e);
        return e;
    }

    /// <summary>给个「适合当什么」的建议，方便挑</summary>
    static string 判建议(条目 e)
    {
        if (e.粒子数 == 0 && e.脚本.Length == 0) return "9 空壳（没粒子也没脚本）";
        if (e.循环) return "3 循环效果（拖尾 / 常驻）";
        if (e.会移动) return "1 飞行道具（适合当子弹）";
        if (e.粒子数 > 0) return "2 单次爆发（适合当命中 / 爆炸）";
        return "8 其他";
    }

    /// <summary>`Assets/resources/xxx/yyy.prefab` → `xxx/yyy`（Resources.Load 用的写法）</summary>
    static string 转Resources路径(string assetPath)
    {
        const string 前缀 = "Assets/resources/";
        string p = assetPath.Replace('\\', '/');
        if (p.StartsWith(前缀, System.StringComparison.OrdinalIgnoreCase)) p = p.Substring(前缀.Length);
        else if (p.StartsWith("Assets/Resources/", System.StringComparison.OrdinalIgnoreCase)) p = p.Substring("Assets/Resources/".Length);
        return p.Substring(0, p.Length - ".prefab".Length);
    }

    // ---- ASCII 别名 ----
    public static void BuildInventory() => 生成全部();
}
