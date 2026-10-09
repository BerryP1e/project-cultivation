using UnityEditor;
using UnityEngine;

/// <summary>Debug access to finalized manuals; no source-model or rebaking dependency.</summary>
public static class FinalizedGongfaTools
{
    [MenuItem("修仙/调试/给予灵虚剑决秘籍（仅当前 Play 背包）")]
    public static void GiveLingxu() => Give("item_gongfa_lingxu_jianjue");

    [MenuItem("修仙/调试/给予太虚剑决秘籍（仅当前 Play 背包）")]
    public static void GiveTaixu() => Give("item_gongfa_taixu_jianjue");

    static void Give(string id)
    {
        if (!EditorApplication.isPlaying) return;
        var panel = Object.FindObjectOfType<UIPanelData>();
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/" + id + ".asset");
        if (panel && item && panel.物品数量(item) == 0) panel.给物品(item, 1);
    }
}
