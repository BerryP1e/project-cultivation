using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **塔相关三张表的导入器**：NPC等级补正表 / 刷怪组表 / 镇妖塔层表。
///
/// ## 为什么不能走 <see cref="DataTableImporter"/>
///
/// 那个导入器是**一行一个资产** + 反射按列名套字段。
/// 这三张表的形状不一样：
///
/// | 表 | 形状 | 结果 |
/// |---|---|---|
/// | NPC等级补正表 | 10 行 = **一张表的 10 个档位** | **一个**资产 |
/// | 刷怪组表 | 一行一个组，但成员是**变长的 id/数量列对** | 一行一个资产 + 嵌套列表 |
/// | 镇妖塔层表 | 一段一个区段（起止层 + 组 + 偏移） | 一行一个资产 + 嵌套列表 |
///
/// 所以单独一个导入器，并且**由 <see cref="DataTableImporter.ImportAll"/> 在末尾自动调用** ——
/// 用户只需要记住「修仙/从配置表生成资产」一个菜单。
///
/// ## 编码
///
/// 读的时候**剥 BOM**、写的时候**带 BOM**（项目铁律：`.csv` 必须 UTF-8 BOM）。
/// 注意 <c>new UTF8Encoding(true).GetString()</c> **不会**跳 BOM，
/// BOM 会变成字符串第一个字符 `\uFEFF`，把第一列表头读成「\uFEFF大境界」——
/// 这个坑在项目里踩过（NpcVariantSplitter 里专门处理过）。
/// </summary>
public static class TowerTablesImporter
{
    const string 表目录 = "Assets/Data/Tables";
    const string 生成根 = "Assets/Data/Generated";

    [MenuItem("修仙/镇妖塔/只导入塔三表（补正/刷怪组/层表）")]
    [MenuItem("Cultivation/Tower/Import Tower Tables")]
    public static void 导入全部()
    {
        var 报告 = new StringBuilder();
        导入等级补正表(报告);
        导入刷怪组表(报告);
        导入层表(报告);
        校验收录(报告);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[塔表导入] 完成：\n" + 报告);
    }

    // ============================================================ 收录校验（硬性要求）

    /// <summary>
    /// **刷怪组表的硬性校验**（用户 2026-10-01 立）：
    ///
    /// 1. **所有 demon 都必须被至少一个刷怪组用到** —— 「用上所有的 demon 是硬性规定」
    /// 2. 每一层都得配到组，且**相邻层的组不能相同**（不许连续几十层用同一个组）
    /// 3. 每层引用的组 id 必须真实存在
    ///
    /// 【为什么要有这个】第一版刷怪组表只写了 12 组、每组只用家族的 `_01` 版本，
    /// 结果 **146 个 demon 里有 124 个从来没被刷出来过**，而且**不报任何错**。
    /// 所以把"有没有漏"变成每次导入都会打印的检查项。
    /// </summary>
    static void 校验收录(StringBuilder 报告)
    {
        报告.AppendLine();
        报告.AppendLine("—— 收录校验 ——");

        // ---- 1) 所有 demon 都要被用到 ----
        var 用到的 = new HashSet<string>();
        foreach (var g in AssetDatabase.FindAssets("t:SpawnGroup"))
        {
            var 组 = AssetDatabase.LoadAssetAtPath<SpawnGroup>(AssetDatabase.GUIDToAssetPath(g));
            if (组?.成员 == null) continue;
            foreach (var m in 组.成员)
                if (m != null && !string.IsNullOrEmpty(m.npcId)) 用到的.Add(m.npcId.Trim());
        }

        var 妖魔全 = new List<string>();
        foreach (var g in AssetDatabase.FindAssets("t:NpcDefinition"))
        {
            var d = AssetDatabase.LoadAssetAtPath<NpcDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (d != null && d.类型 == NpcKind.妖魔) 妖魔全.Add(d.id);
        }

        var 漏掉 = new List<string>();
        foreach (var id in 妖魔全) if (!用到的.Contains(id)) 漏掉.Add(id);
        漏掉.Sort();

        if (妖魔全.Count == 0)
            报告.AppendLine("  demon 总数 0（NpcDefinition 还没生成？先跑 修仙/从配置表生成资产）");
        else if (漏掉.Count == 0)
            报告.AppendLine("  ✓ demon 收录：全部 " + 妖魔全.Count + " 个都被刷怪组用到");
        else
            报告.AppendLine("  ★ demon 收录：" + 漏掉.Count + " / " + 妖魔全.Count
                            + " 个**没被任何刷怪组用到**：" + string.Join("、", 漏掉.ConvertAll(x => x.ToString()).GetRange(0, Mathf.Min(12, 漏掉.Count)))
                            + (漏掉.Count > 12 ? " …" : ""));

        // ---- 2) 逐层检查：配到组 + 相邻不同 ----
        var 层表 = AssetDatabase.LoadAssetAtPath<TowerFloorTable>(
            "Assets/Data/Generated/TowerFloorTable/镇妖塔层表.asset");
        if (层表 == null) { 报告.AppendLine("  层表资产不存在，跳过逐层检查"); return; }

        int 缺段 = 0, 相邻同 = 0, 组不存在 = 0;
        var 组id集 = new HashSet<string>();
        foreach (var g in AssetDatabase.FindAssets("t:SpawnGroup"))
        {
            var 组 = AssetDatabase.LoadAssetAtPath<SpawnGroup>(AssetDatabase.GUIDToAssetPath(g));
            if (组 != null && !string.IsNullOrEmpty(组.组id)) 组id集.Add(组.组id);
        }
        string 上一层组 = null;
        for (int 层 = 1; 层 <= 层表.总层数; 层++)
        {
            var 组id = 层表.取刷怪组(层);
            if (string.IsNullOrEmpty(组id)) { 缺段++; continue; }
            if (!组id集.Contains(组id)) 组不存在++;
            if (上一层组 != null && 组id == 上一层组) 相邻同++;
            上一层组 = 组id;
        }
        报告.AppendLine("  " + (缺段 == 0 ? "✓" : "★") + " 层覆盖：" + 层表.总层数 + " 层，没配到组的 " + 缺段 + " 层");
        报告.AppendLine("  " + (相邻同 == 0 ? "✓" : "★") + " 相邻层同组：" + 相邻同 + " 处（用户要求不许连续几层用同一个组）");
        报告.AppendLine("  " + (组不存在 == 0 ? "✓" : "★") + " 层表引用了不存在的组：" + 组不存在 + " 处");
    }

    // ============================================================ 等级补正表

    static void 导入等级补正表(StringBuilder 报告)
    {
        var 行 = 读表("NPC等级补正表", 报告, out var 表头);
        if (行 == null) return;

        var 资产 = 载入或新建<NpcLevelScale>("NpcLevelScale/NPC等级补正表.asset");

        // ★ 必须**先把档位铺满**，再按 CSV 覆盖。
        //
        // 【踩过的坑】原来这里写 `资产.重算档位(1)` 只铺 1 档，然后循环里用
        // `资产.取档(大境界)` 去拿目标档 —— 而 `取档` 是**夹取**的
        // （`Mathf.Clamp(大境界-1, 0, Count-1)`），10 行**全都拿到同一个对象**，
        // 于是每一行覆盖上一行，最后表里只剩 1 档、值 = CSV 最后一行的值。
        // 表现：档数=1、所有等级都返回最高档的倍率（1.22E+14），
        // **而且一点都不报错** —— 只有核对数值才发现。
        // 现在改成「按 CSV 的行数决定档数」，并且**新建对象**而不是复用 取档() 的结果。
        资产.重算档位(Mathf.Max(1, 行.Count));
        for (int i = 0; i < 行.Count; i++)
        {
            var r = 行[i];
            int 大境 = 取整(列(r, 表头, "大境界"), i + 1);
            if (大境 < 1 || 大境 > 资产.档位.Count)
            {
                报告.AppendLine("NPC等级补正表: ★第 " + (i + 2) + " 行的大境界 " + 大境 + " 越界，跳过");
                continue;
            }

            var 旧 = 资产.档位[大境 - 1];
            var 新 = new NpcLevelTier
            {
                大境界 = 大境,
                起点等级 = 取整(列(r, 表头, "起点等级"), NpcLevelScale.大境界到起点等级(大境)),
                倍数 = 取浮(列(r, 表头, "倍数"), 旧 != null ? 旧.倍数 : 1f),
                加值 = 取浮(列(r, 表头, "加值"), 旧 != null ? 旧.加值 : 0f),
            };
            资产.档位[大境 - 1] = 新;
        }
        EditorUtility.SetDirty(资产);
        报告.AppendLine("NPC等级补正表: " + 资产.档数 + " 档｜" + 资产.曲线摘要());
    }

    // ============================================================ 刷怪组表

    static void 导入刷怪组表(StringBuilder 报告)
    {
        var 行 = 读表("刷怪组表", 报告, out var 表头);
        if (行 == null) return;

        // 「怪N」/「怪N数量」是**变长列对**，所以扫描表头找出所有序号
        var 槽位 = new List<int>();
        for (int n = 1; n <= 32; n++)
        {
            if (找列(表头, "怪" + n) >= 0) 槽位.Add(n);
        }
        if (槽位.Count == 0)
        {
            报告.AppendLine("刷怪组表: ★表头里没有「怪1」列，跳过");
            return;
        }

        int 建 = 0, 空 = 0;
        var 用过的id = new HashSet<string>();

        for (int i = 0; i < 行.Count; i++)
        {
            var r = 行[i];
            string id = 列(r, 表头, "组id");
            if (string.IsNullOrEmpty(id)) continue;

            var 资产 = 载入或新建<SpawnGroup>("SpawnGroup/" + 清理文件名(id) + ".asset");
            资产.组id = id;
            资产.名字 = 列(r, 表头, "名字");
            资产.权重 = Mathf.Max(1, 取整(列(r, 表头, "权重"), 1));
            资产.成员 = new List<SpawnGroupEntry>();

            foreach (var n in 槽位)
            {
                string npc = 列(r, 表头, "怪" + n);
                if (string.IsNullOrEmpty(npc)) continue;
                int 数量 = 取整(列(r, 表头, "怪" + n + "数量"), 1);
                if (数量 <= 0) continue;
                资产.成员.Add(new SpawnGroupEntry { npcId = npc, 数量 = 数量 });
            }

            EditorUtility.SetDirty(资产);
            用过的id.Add(id);
            if (资产.是空的) 空++; else 建++;
        }

        // 删掉 CSV 里已经删掉的组 —— 否则资产留着，塔还会刷到它
        int 删 = 清理孤儿<SpawnGroup>("SpawnGroup", 用过的id);
        报告.AppendLine("刷怪组表: " + 建 + " 组可用"
                        + (空 > 0 ? "，★" + 空 + " 组是空的" : "")
                        + (删 > 0 ? "，删掉 " + 删 + " 个已移除的组" : ""));
    }

    // ============================================================ 层表

    static void 导入层表(StringBuilder 报告)
    {
        var 行 = 读表("镇妖塔层表", 报告, out var 表头);
        if (行 == null) return;

        // 层表是「一张表 = 一个 TowerFloorTable」，区段是它的子列表。
        // 所以所有行写进**同一个**资产，用第一行之外的行做区段。
        var 资产 = 载入或新建<TowerFloorTable>("TowerFloorTable/镇妖塔层表.asset");
        资产.分段 = new List<TowerSegment>();

        int 顶 = 1;
        for (int i = 0; i < 行.Count; i++)
        {
            var r = 行[i];
            int 起 = 取整(列(r, 表头, "起始层"), -1);
            int 止 = 取整(列(r, 表头, "结束层"), -1);
            if (起 < 1 || 止 < 起)
            {
                报告.AppendLine("镇妖塔层表: ★第 " + (i + 2) + " 行起止层不合法（"
                                + 起 + "~" + 止 + "），跳过");
                continue;
            }
            资产.分段.Add(new TowerSegment
            {
                起始层 = 起,
                结束层 = 止,
                刷怪组 = 列(r, 表头, "刷怪组"),
                等级偏移 = 取整(列(r, 表头, "等级偏移"), 0),
                数量倍率 = 取浮(列(r, 表头, "数量倍率"), 1f),
            });
            顶 = Mathf.Max(顶, 止);
        }
        资产.总层数 = 顶;

        // 连续性检查：中间有洞的层会刷不出怪（取分段返回 null）
        var 洞 = 找空洞(资产);
        EditorUtility.SetDirty(资产);

        报告.AppendLine("镇妖塔层表: " + 资产.分段.Count + " 段，共 " + 资产.总层数 + " 层"
                        + "（每 " + 资产.每多少层一级 + " 层一级，1~" + 资产.总层数
                        + " 层的怪等级 " + 资产.取怪物等级(1) + "~" + 资产.取怪物等级(资产.总层数) + "）");
        if (洞 != null) 报告.AppendLine("  ★空洞：" + 洞);
    }

    /// <summary>找出没有被任何区段覆盖的楼层（有洞那些层会刷不出怪）</summary>
    static string 找空洞(TowerFloorTable 表)
    {
        var 缺 = new List<int>();
        for (int 层 = 1; 层 <= 表.总层数; 层++)
            if (表.取分段(层) == null)
            {
                缺.Add(层);
                if (缺.Count >= 8) break;
            }
        if (缺.Count == 0) return null;
        return string.Join("、", 缺.ConvertAll(v => v.ToString()))
               + (缺.Count >= 8 ? " …（只列前 8 个）" : "");
    }

    // ============================================================ 工具

    /// <summary>读一张表 → 数据行（不含表头）。失败返回 null</summary>
    static List<List<string>> 读表(string 表名, StringBuilder 报告, out List<string> 表头)
    {
        表头 = null;
        string 路径 = 表目录 + "/" + 表名 + ".csv";
        if (!File.Exists(路径)) { 报告.AppendLine(表名 + ": 文件不存在，跳过"); return null; }

        var 全行 = ParseCsv(剥BOM(File.ReadAllText(路径, Encoding.UTF8)));
        if (全行.Count < 2) { 报告.AppendLine(表名 + ": 没有数据行，跳过"); return null; }

        表头 = 全行[0];
        var 数据 = new List<List<string>>();
        for (int i = 1; i < 全行.Count; i++)
        {
            var r = 全行[i];
            if (r.Count == 0 || string.IsNullOrWhiteSpace(取(r, 0))) continue;
            数据.Add(r);
        }
        return 数据;
    }

    static string 剥BOM(string s)
    {
        if (!string.IsNullOrEmpty(s) && s[0] == '\uFEFF') return s.Substring(1);
        return s;
    }

    /// <summary>表头里找列（剥掉 BOM 和空白）</summary>
    static int 找列(List<string> 表头, string 名)
    {
        if (表头 == null) return -1;
        for (int i = 0; i < 表头.Count; i++)
            if (剥BOM(表头[i]).Trim() == 名) return i;
        return -1;
    }

    static string 列(List<string> 行, List<string> 表头, string 名)
    {
        int i = 找列(表头, 名);
        if (i < 0) return "";
        return 取(行, i).Trim();
    }

    static string 取(List<string> 行, int i)
        => 行 != null && i >= 0 && i < 行.Count ? 行[i] : "";

    static int 取整(string s, int 默认)
    {
        int v;
        return int.TryParse(s, out v) ? v : 默认;
    }

    static float 取浮(string s, float 默认)
    {
        float v;
        return float.TryParse(s, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out v) ? v : 默认;
    }

    static T 载入或新建<T>(string 相对路径) where T : ScriptableObject
    {
        string 路径 = 生成根 + "/" + 相对路径;
        确保目录(Path.GetDirectoryName(路径).Replace('\\', '/'));
        var so = AssetDatabase.LoadAssetAtPath<T>(路径);
        if (so == null)
        {
            so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, 路径);
        }
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

    /// <summary>删掉不在 <paramref name="保留"/> 里的资产（CSV 里删了行，资产要跟着走）</summary>
    static int 清理孤儿<T>(string 子目录, HashSet<string> 保留) where T : ScriptableObject
    {
        string 目录 = 生成根 + "/" + 子目录;
        if (!AssetDatabase.IsValidFolder(目录)) return 0;

        int 删 = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { 目录 }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<T>(p);
            if (so == null) continue;

            // 用反射取 id 字段（这几个类型都有「组id」/「区段id」）
            string id = null;
            var t = typeof(T);
            var f = t.GetField("组id") ?? t.GetField("区段id");
            if (f != null) id = f.GetValue(so) as string;
            if (string.IsNullOrEmpty(id)) continue;
            if (保留.Contains(id)) continue;

            AssetDatabase.DeleteAsset(p);
            删++;
        }
        return 删;
    }

    static string 清理文件名(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
            sb.Append(Path.GetInvalidFileNameChars().Length > 0 && System.Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        return sb.ToString();
    }

    // ============================================================ CSV 解析

    /// <summary>带引号支持的 CSV 解析（和 DataTableImporter 同一套行为）</summary>
    static List<List<string>> ParseCsv(string 文本)
    {
        var 结果 = new List<List<string>>();
        var 行 = new List<string>();
        var 字段 = new StringBuilder();
        bool 在引号里 = false;

        for (int i = 0; i < 文本.Length; i++)
        {
            char c = 文本[i];
            if (在引号里)
            {
                if (c == '"')
                {
                    if (i + 1 < 文本.Length && 文本[i + 1] == '"') { 字段.Append('"'); i++; }
                    else 在引号里 = false;
                }
                else 字段.Append(c);
            }
            else
            {
                if (c == '"') 在引号里 = true;
                else if (c == ',') { 行.Add(字段.ToString()); 字段.Length = 0; }
                else if (c == '\n')
                {
                    行.Add(字段.ToString()); 字段.Length = 0;
                    结果.Add(行); 行 = new List<string>();
                }
                else if (c == '\r') { /* 忽略，等 \n */ }
                else 字段.Append(c);
            }
        }
        if (字段.Length > 0 || 行.Count > 0) { 行.Add(字段.ToString()); 结果.Add(行); }
        return 结果;
    }
}
