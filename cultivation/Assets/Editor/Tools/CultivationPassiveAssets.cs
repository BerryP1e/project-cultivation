using UnityEditor;
using UnityEngine;

/// <summary>本批七个被动、学习玉简及八张专属图标，资产导入不保存场景。</summary>
public static class CultivationPassiveAssets
{
    public static readonly string[] Keys = { "zicheng_xunhuan", "lingshi_weiya", "xingming_shuangxiu", "neihua_shijie", "shenwai_huashen", "lianxu_rudao", "kongjian_tengnuo" };
    [MenuItem("修仙/神通/更新循环与行游被动（不动场景）")]
    public static void Build()
    {
        安全导入配置表.ImportAssetsOnly("被动神通表", "物品表");
        BindIcons();
        面板库收集器.收集(); AssetDatabase.SaveAssets();
        Debug.Log("[循环与行游] 七个被动、玉简与图标更新完成，未修改场景或玩家数据");
    }
    public static void BindIcons()
    {
        foreach (var key in Keys) Bind(key, false);
        Bind("chedian_yeting", true);
    }
    static void Bind(string key, bool active)
    {
        var path = "Assets/resources/UI/InkUI/AbilityArt/icon-ability-" + key + (active ? "-v2" : "") + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer) throw new System.InvalidOperationException("缺少图标 " + path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
        importer.spritePixelsPerUnit = 100; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.maxTextureSize = 512; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var ability = AssetDatabase.LoadAssetAtPath<DivineAbilityDefinition>("Assets/Data/Generated/" + (active ? "ActiveDivineAbility" : "PassiveDivineAbility") + "/ability_" + key + ".asset");
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_" + (active ? "" : "passive_") + "ability_" + key + ".asset");
        if (!sprite || !ability || !item) throw new System.InvalidOperationException("图标/神通/玉简缺失 " + key);
        ability.图标 = item.图标 = sprite; EditorUtility.SetDirty(ability); EditorUtility.SetDirty(item);
    }
    [MenuItem("修仙/调试/给予循环与行游七门玉简（仅当前Play）")]
    static void Give()
    {
        if (!Application.isPlaying) { Debug.Log("请先进入 Play"); return; }
        var panel = Object.FindObjectOfType<UIPanelData>();
        foreach (var key in Keys) {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_passive_ability_" + key + ".asset");
            if (panel && item) panel.给物品(item, 1);
        }
    }
}
