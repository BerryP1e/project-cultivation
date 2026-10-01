using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **物品表 → 物品效果** 的导入步骤（用户 2026-09-27 要求：所有物品都必须从物品表出发）。
///
/// 背景：以前物品有两个来源 —— `物品表.csv`（材料/消耗品）和
/// `能力物品生成器`（每个功法/神通一件「秘籍/玉简/心得」+ 门派便服）。
/// 于是「便服」和「学习物品」在物品表里根本查不到。现在全部收进物品表。
///
/// 物品表新增三列（由本类消费，**不是** ItemDefinition 的字段）：
/// | 列 | 含义 |
/// |---|---|
/// | `可使用`     | 0/1，勾上 = 背包里多一个「使用」按钮 |
/// | `效果类型`   | `学功法` / `学主动神通` / `学被动神通` / `学外观` / `属性增益`，其余视为无效果 |
/// | `效果参数id` | 效果的目标 id（功法id / 神通id / 外观id …） |
/// | `效果说明`   | 面板上给玩家看的效果说明（可空，空则用物品自己的介绍） |
///
/// 效果资产统一生成在 `Assets/Data/Generated/能力物品/` 下，名字由「类型 + 参数id」决定，
/// 所以**反复跑导入器只会更新同一份资产**，不会越堆越多。
/// </summary>
public static class 物品效果导入
{
    public const string 效果目录 = "Assets/Data/Generated/能力物品";

    public static void 处理物品(ItemDefinition 物品, List<string> row, List<string> header, StringBuilder report)
    {
        if (物品 == null) return;

        string 类型 = 取列(row, header, "效果类型");
        物品.可使用 = 取列(row, header, "可使用") == "1" || 取列(row, header, "可使用").ToLower() == "true";

        // 没写效果类型 → 就是个不能用的物品（材料 / 提交物），把效果清掉
        if (string.IsNullOrWhiteSpace(类型))
        {
            物品.使用效果 = null;
            return;
        }

        string 参数id = 取列(row, header, "效果参数id");
        string 说明 = 取列(row, header, "效果说明");
        物品.可使用 = true;      // 有有效果就是能用的

        switch (类型.Trim())
        {
            case "学功法":
                {
                    var e = 取或建<学功法效果>(物品.物品id);
                    e.功法id = 参数id;
                    e.功法 = null;              // 让运行时按 id 反查（也可由下面的引用修复填上）
                    e.说明 = string.IsNullOrEmpty(说明) ? e.说明 : 说明;
                    EditorUtility.SetDirty(e);
                    物品.使用效果 = e;
                    break;
                }
            case "学主动神通":
                {
                    var e = 取或建<学主动神通效果>(物品.物品id);
                    e.神通id = 参数id;
                    e.神通 = null;
                    e.说明 = string.IsNullOrEmpty(说明) ? e.说明 : 说明;
                    EditorUtility.SetDirty(e);
                    物品.使用效果 = e;
                    break;
                }
            case "学被动神通":
                {
                    var e = 取或建<学被动神通效果>(物品.物品id);
                    e.神通id = 参数id;
                    e.神通 = null;
                    e.说明 = string.IsNullOrEmpty(说明) ? e.说明 : 说明;
                    EditorUtility.SetDirty(e);
                    物品.使用效果 = e;
                    break;
                }
            case "学外观":
                {
                    var e = 取或建<学外观效果>(物品.物品id);
                    e.外观id = 参数id;
                    e.外观 = null;
                    e.获得即装备 = true;
                    e.说明 = string.IsNullOrEmpty(说明) ? e.说明 : 说明;
                    EditorUtility.SetDirty(e);
                    物品.使用效果 = e;
                    break;
                }
            case "属性增益":
                {
                    var e = 取或建<属性增益效果>(物品.物品id);
                    e.说明 = string.IsNullOrEmpty(说明) ? e.说明 : 说明;
                    EditorUtility.SetDirty(e);
                    物品.使用效果 = e;
                    report.Append("  [提示] 「").Append(物品.物品名)
                          .Append("」是 属性增益 效果：加什么属性请在 Inspector 里补（表里只给了说明）\n");
                    break;
                }
            case "服丹":
                {
                    // 丹药的**数值不在这张表上** —— `效果参数id` 填丹方 id（`灵丹定义.id`），
                    // 运行时拿它去 灵丹库 反查（单一真相源）。表里只负责说"这是一味能吃的丹"。
                    var e = 取或建<服丹效果>(物品.物品id);
                    e.丹方id = 参数id;
                    e.说明 = string.IsNullOrEmpty(说明) ? e.说明 : 说明;
                    EditorUtility.SetDirty(e);
                    物品.使用效果 = e;
                    if (灵丹库.取(string.IsNullOrEmpty(参数id) ? 物品.物品id : 参数id) == null)
                        report.Append("  [警告] 「").Append(物品.物品名)
                              .Append("」标了 服丹，但 灵丹库 里没有 id = ")
                              .Append(string.IsNullOrEmpty(参数id) ? 物品.物品id : 参数id)
                              .Append(" 的丹方 —— 吃下去不会有任何效果\n");
                    break;
                }
            case "摆放物件":
            case "放置灵田":       // 旧写法，留个别名免得老表直接报"认不出"
                {
                    // 「灵田开拓令」/「练功木桩」用的效果：在背包里点使用 → 进洞府的摆放态。
                    // `效果参数id` = `摆放物库` 的 id（lingtian / muzhuang），
                    // 规则与尺寸都在 `摆放物定义` / `摆放校验` 里，表里只负责说"这玩意能摆"。
                    // 旧写法（效果类型=放置灵田）参数一般留空 ⇒ 兜底成 lingtian。
                    var e = 取或建<摆放物件效果>(物品.物品id);
                    e.摆放物id = string.IsNullOrEmpty(参数id)
                               ? (类型 == "放置灵田" ? "lingtian" : "")
                               : 参数id;
                    e.说明 = string.IsNullOrEmpty(说明) ? e.说明 : 说明;
                    EditorUtility.SetDirty(e);
                    物品.使用效果 = e;
                    if (摆放物库.取(e.摆放物id) == null)
                        report.Append("  [警告] 「").Append(物品.物品名)
                              .Append("」标了 摆放物件，但 摆放物库 里没有 id = ")
                              .Append(e.摆放物id).Append(" —— 用的时候会拒绝，摆不出任何东西\n");
                    break;
                }
            default:
                Debug.LogWarning("[物品效果导入] 认不出的效果类型「" + 类型 + "」，物品 = " + 物品.物品id);
                物品.使用效果 = null;
                break;
        }
    }

    static T 取或建<T>(string 物品id) where T : 物品使用效果
    {
        确保目录();
        // 资产名用「类型 + 物品id」，同一件物品反复导入只会更新同一份资产
        string 名 = typeof(T).Name + "_" + (string.IsNullOrEmpty(物品id) ? "未命名" : 物品id);
        string path = 效果目录 + "/" + 名 + ".asset";

        var e = AssetDatabase.LoadAssetAtPath<T>(path);
        if (e == null)
        {
            e = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(e, path);
        }
        return e;
    }

    static void 确保目录()
    {
        if (!Directory.Exists(效果目录))
        {
            Directory.CreateDirectory(效果目录);
            AssetDatabase.Refresh();
        }
    }

    static string 取列(List<string> row, List<string> header, string 列名)
    {
        if (header == null) return "";
        for (int i = 0; i < header.Count; i++)
            if (header[i] == 列名) return i < row.Count ? (row[i] ?? "").Trim() : "";
        return "";
    }
}
