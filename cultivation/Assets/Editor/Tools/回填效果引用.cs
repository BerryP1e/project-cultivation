using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// **回填「学习类」物品效果的资产引用**（菜单：修仙/修复/回填学习类物品的效果引用）。
///
/// 为什么必须回填：
///   学习类效果（<see cref="学功法效果"/> / <see cref="学主动神通效果"/> /
///   <see cref="学被动神通效果"/> / <see cref="学外观效果"/>）身上有一个
///   **直接引用**（功法 / 神通 / 外观）。物品表里只写了 id，导入后这个引用是空的，
///   代码会退到 <c>能力查找.按id&lt;T&gt;()</c> 去找 ——
///   而那个函数整段被 `#if UNITY_EDITOR` 包着、用的是 `AssetDatabase`，
///   **打包后直接返回 null** ⇒ 玩家点学习道具没有任何反应。
///   （实测：23 件物品里 12 个学习类的引用是 null。）
///
/// 顺带解决第二个问题：`AssetDatabase.LoadAssetAtPath` 加载的资产**不会被记进构建**，
/// 所以就算编辑器里能用，正式包里那些功法/神通资产也可能根本没打进去。
/// 回填成**直接引用**后，Unity 会自动把它们算进构建依赖 ✓
/// </summary>
public static class 回填效果引用
{
    [MenuItem("修仙/修复/回填学习类物品的效果引用", false, 300)]
    public static void 回填() => 回填(false);

    /// <summary>
    /// 回填主逻辑。<paramref name="静默"/> = true 时不弹对话框（供导入器自动调用）。
    ///
    /// ★ 导入器在 `ImportAll` 结尾会调它（见 DataTableImporter）。
    ///   这样**新建学习类道具时，引用会被自动填上**，
    ///   不用再靠人记得跑菜单 —— 也就不会再出现"打包后点学习道具没反应"。
    /// </summary>
    public static void 回填(bool 静默)
    {
        var 映射 = new Dictionary<string, ScriptableObject>();

        void 收<T>(System.Func<T, string> 取id) where T : ScriptableObject
        {
            foreach (var g in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var 路径 = AssetDatabase.GUIDToAssetPath(g);
                var o = AssetDatabase.LoadAssetAtPath<T>(路径);
                if (o == null) continue;
                var id = 取id(o);
                if (!string.IsNullOrEmpty(id) && !映射.ContainsKey(id)) 映射[id] = o;
            }
        }
        收<GongFaDefinition>(o => o.功法id);
        收<ActiveDivineAbility>(o => o.神通id);
        收<PassiveDivineAbility>(o => o.神通id);
        收<AppearanceDefinition>(o => o.id);

        int 查过 = 0, 填了 = 0, 找不到 = 0;
        var 缺的 = new List<string>();

        foreach (var g in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var 路径 = AssetDatabase.GUIDToAssetPath(g);
            var 物品 = AssetDatabase.LoadAssetAtPath<ItemDefinition>(路径);
            if (物品 == null || 物品.使用效果 == null) continue;
            查过++;

            var 效果 = 物品.使用效果;
            var t = 效果.GetType();
            bool 改了 = false;

            // 学功法
            if (效果 is 学功法效果 学功)
            {
                if (该填了(学功.功法, 学功.功法id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学功.功法id, out v) && v is GongFaDefinition)
                    { 学功.功法 = v as GongFaDefinition; 改了 = true; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 功法 " + 学功.功法id); }
                }
            }
            // 学主动神通
            else if (效果 is 学主动神通效果 学主)
            {
                if (该填了(学主.神通, 学主.神通id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学主.神通id, out v) && v is ActiveDivineAbility)
                    { 学主.神通 = v as ActiveDivineAbility; 改了 = true; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 主动神通 " + 学主.神通id); }
                }
            }
            // 学被动神通
            else if (效果 is 学被动神通效果 学被)
            {
                if (该填了(学被.神通, 学被.神通id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学被.神通id, out v) && v is PassiveDivineAbility)
                    { 学被.神通 = v as PassiveDivineAbility; 改了 = true; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 被动神通 " + 学被.神通id); }
                }
            }
            // 学外观
            else if (效果 is 学外观效果 学外)
            {
                if (该填了(学外.外观, 学外.外观id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学外.外观id, out v) && v is AppearanceDefinition)
                    { 学外.外观 = v as AppearanceDefinition; 改了 = true; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 外观 " + 学外.外观id); }
                }
            }

            if (改了) { EditorUtility.SetDirty(效果); 填了++; }
        }

        AssetDatabase.SaveAssets();
        var 报 = "[回填效果引用] 查过 " + 查过 + " 件、回填 " + 填了 + " 件、找不到 " + 找不到 + " 件";
        if (缺的.Count > 0) 报 += "\n  找不到对应资产的：\n    " + string.Join("\n    ", 缺的);
        if (找不到 > 0) Debug.LogError(报);      // 找不到 = 配置写错了，要显眼
        else Debug.Log(报);

        // ⚠️ 这里**不再弹模态框**（原来 `if (!静默) DisplayDialog(...)`）。
        //   信息上面那句日志已经全说了；而模态框会挡住主线程，
        //   自动化调用（AI 跑菜单）没人点它 → 看起来就是卡死。详见 踩坑 F8。
        //   `静默` 参数保留只是为了让老调用点不用改。
    }

    /// <summary>
    /// 这个直接引用**现在该不该（重新）填**：
    ///   · 为 null → 该填；
    ///   · 非 null，但**指向的资产 id 和表里写的 id 对不上** → 也该填（表里改了 id / 引用被写坏）。
    ///
    /// 【为什么加了后半条】2026-10-02 修的一处数据腐坏：原来只在 `引用 == null` 时补，
    /// 而那一版 `物品效果导入` 每次导入都先 `引用 = null`（"让运行时按 id 反查"）——
    /// 于是每次导入都是一次"清空 → 回填"的赛跑，中途还夹着好几趟 `SaveAssets()/Refresh()`。
    /// 实测「冰暴术」那件就卡在两次写盘之间：**内存里有引用、盘上还是 `fileID: 0`**，
    /// 而回填因为"已经不是 null"直接跳过了它（日志：回填 12 件，13 件里少的那一件就是它）。
    /// 现在两边都改了：`物品效果导入` 只在 id 变了时作废引用；本函数负责收口
    /// （补空 **以及** 纠正指错的），并且必须**核对类型**——原来用 `v as 类型` 失败会
    /// **静默写入 null**、日志里连一行都没有。
    /// </summary>
    static bool 该填了(ScriptableObject 引用, string 期望id)
    {
        if (string.IsNullOrEmpty(期望id)) return false;   // 表里没写 id：没什么可填的
        if (引用 == null) return true;

        string 实id = null;
        if (引用 is GongFaDefinition 功) 实id = 功.功法id;
        else if (引用 is DivineAbilityDefinition 神) 实id = 神.神通id;   // 主动 / 被动都继承它
        else if (引用 is AppearanceDefinition 外) 实id = 外.id;
        return 实id != 期望id;
    }
}
