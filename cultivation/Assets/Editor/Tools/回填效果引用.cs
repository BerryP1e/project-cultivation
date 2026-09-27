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
    public static void 回填()
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
                if (学功.功法 == null && !string.IsNullOrEmpty(学功.功法id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学功.功法id, out v)) { 学功.功法 = v as GongFaDefinition; 改了 = 学功.功法 != null; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 功法 " + 学功.功法id); }
                }
            }
            // 学主动神通
            else if (效果 is 学主动神通效果 学主)
            {
                if (学主.神通 == null && !string.IsNullOrEmpty(学主.神通id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学主.神通id, out v)) { 学主.神通 = v as ActiveDivineAbility; 改了 = 学主.神通 != null; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 主动神通 " + 学主.神通id); }
                }
            }
            // 学被动神通
            else if (效果 is 学被动神通效果 学被)
            {
                if (学被.神通 == null && !string.IsNullOrEmpty(学被.神通id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学被.神通id, out v)) { 学被.神通 = v as PassiveDivineAbility; 改了 = 学被.神通 != null; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 被动神通 " + 学被.神通id); }
                }
            }
            // 学外观
            else if (效果 is 学外观效果 学外)
            {
                if (学外.外观 == null && !string.IsNullOrEmpty(学外.外观id))
                {
                    ScriptableObject v;
                    if (映射.TryGetValue(学外.外观id, out v)) { 学外.外观 = v as AppearanceDefinition; 改了 = 学外.外观 != null; }
                    else { 找不到++; 缺的.Add(物品.物品id + " → 外观 " + 学外.外观id); }
                }
            }

            if (改了) { EditorUtility.SetDirty(效果); 填了++; }
        }

        AssetDatabase.SaveAssets();
        var 报 = "[回填效果引用] 查过 " + 查过 + " 件、回填 " + 填了 + " 件、找不到 " + 找不到 + " 件";
        if (缺的.Count > 0) 报 += "\n  找不到对应资产的：\n    " + string.Join("\n    ", 缺的);
        Debug.Log(报);
        EditorUtility.DisplayDialog("回填效果引用", 报, "好");
    }
}
