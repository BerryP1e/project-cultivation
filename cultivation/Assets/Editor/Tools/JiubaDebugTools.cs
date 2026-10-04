using UnityEditor;
using UnityEngine;

/// <summary>Give a test copy without modifying serialized scenes or learning the art directly.</summary>
public static class JiubaDebugTools
{
    [MenuItem("修仙/调试/给予八九玄功秘籍（仅当前 Play 背包）")]
    public static void GiveManual()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[八九玄功] 请先进入 Play；此操作不修改场景背包。");
            return;
        }
        var panel = Object.FindObjectOfType<UIPanelData>();
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(
            "Assets/Data/Generated/ItemDefinition/item_gongfa_jiuba_xuangong.asset");
        if (panel == null || item == null || !(item.使用效果 is 学功法效果 effect) || effect.取功法() == null)
        {
            Debug.LogError("[八九玄功] 面板或秘籍学习效果缺失，检查物品表与生成资产。");
            return;
        }
        if (panel.物品数量(item) == 0) panel.给物品(item, 1);
        Debug.Log("[八九玄功] 当前测试背包已有一份秘籍，使用后学会功法；不自动学习、不保存场景。", item);
    }
}
