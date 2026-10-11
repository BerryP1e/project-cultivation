using System;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>法则两张表的显式生成器；生成配置、九种宝箱和使用效果，不保存场景。</summary>
public static class LawFragmentAssets
{
    const string Folder = "Assets/resources/Laws";
    [MenuItem("修仙/调试/给予九境界法则宝箱（仅当前 Play 背包）")]
    public static void GiveTestChests()
    {
        if (!EditorApplication.isPlaying) return;
        var panel = UnityEngine.Object.FindObjectOfType<UIPanelData>(); var cfg = LawFragmentCatalog.取();
        if (panel && cfg) foreach (var realm in cfg.境界) panel.给物品(realm.宝箱, 2);
    }
    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!asset) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    [MenuItem("修仙/法则/导入法则表", false, 440)]
    public static void 构建()
    {
        Directory.CreateDirectory(Folder + "/Chests"); AssetDatabase.Refresh();
        var cfg = Asset<LawFragmentCatalog>(Folder + "/Catalog.asset");
        cfg.装备槽数 = 6;
        导入敏感数值(cfg);
        var names = File.ReadAllLines("Assets/Data/Tables/法则称呼表.csv").Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Split(',')).ToArray();
        cfg.称呼 = names.Select(r => new LawFragmentCatalog.NameRule { id = r[0], 称呼 = r[1], 主属性 = r.Skip(2).Where(x => x.Length > 0).Select(x => (AttributeType)Enum.Parse(typeof(AttributeType), x)).ToArray() }).ToArray();
        if (cfg.称呼.Length != 112 || cfg.称呼.Select(x => x.称呼).Distinct().Count() != 112) throw new InvalidDataException("法则称呼必须有 112 个独立名称");
        var rows = File.ReadAllLines("Assets/Data/Tables/法则境界表.csv").Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Split(',')).ToArray();
        if (rows.Length != 9) throw new InvalidDataException("法则必须有九个境界");
        cfg.境界 = new LawFragmentCatalog.RealmRule[9];
        for (int i = 0; i < 9; i++)
        {
            var row = rows[i]; string imagePath = Folder + "/Icons/" + row[10] + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed; importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            var effect = Asset<LawChestUseEffect>(Folder + "/Chests/Open_" + i + ".asset"); effect.境界 = i;
            effect.说明 = "开启后获得一枚" + row[0] + "期法则碎片。"; EditorUtility.SetDirty(effect);
            var item = Asset<ItemDefinition>(Folder + "/Chests/Chest_" + i + ".asset");
            item.物品id = "law_chest_" + i; item.物品名 = row[0] + "法则宝箱"; item.图标 = sprite;
            item.介绍 = effect.说明 + "\n主属性与副属性随机，开启后数值固定。";
            item.品阶 = (QualityTier)Mathf.Min(6, i); item.最大堆叠数量 = 99; item.可使用 = true; item.使用效果 = effect; EditorUtility.SetDirty(item);
            cfg.境界[i] = new LawFragmentCatalog.RealmRule { 境界名 = row[0], 后缀 = row[1], 主属性数 = int.Parse(row[2]), 副属性数 = int.Parse(row[3]),
                主增幅 = Range(row, 4), 副增幅 = Range(row, 6), 小增幅 = Range(row, 8), 敏感成长 = float.Parse(row[11],CultureInfo.InvariantCulture), 熔炼基础精粹 = long.Parse(row[12]), 洗练消耗精粹 = long.Parse(row[13]), 图标 = sprite, 宝箱 = item };
        }
        LawFragmentLayerAssets.配置(cfg);
        EditorUtility.SetDirty(cfg); AssetDatabase.SaveAssets(); QuestDatabaseBuilder.收集(false);
        Debug.Log("[法则] 已生成 112 称呼、九境界配置与宝箱；没有保存场景");
    }
    static Vector2 Range(string[] row, int index) => new Vector2(float.Parse(row[index], CultureInfo.InvariantCulture), float.Parse(row[index + 1], CultureInfo.InvariantCulture));
    [MenuItem("修仙/法则/仅导入数值平衡")]
    public static void 导入数值()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play");
        var cfg = Asset<LawFragmentCatalog>(Folder + "/Catalog.asset"); 导入敏感数值(cfg); EditorUtility.SetDirty(cfg); AssetDatabase.SaveAssets();
    }
    static void 导入敏感数值(LawFragmentCatalog cfg)
    {
        cfg.宝箱掉率 = .1f;
        cfg.高一境界概率 = .05f;
        cfg.高值稀有指数 = 2;
        var realms=File.ReadAllLines("Assets/Data/Tables/法则境界表.csv").Skip(1).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>x.Split(',')).ToArray();
        for(int i=0;i<cfg.境界.Length;i++)if(cfg.境界[i]!=null){cfg.境界[i].敏感成长=float.Parse(realms[i][11],CultureInfo.InvariantCulture);cfg.境界[i].熔炼基础精粹=long.Parse(realms[i][12]);cfg.境界[i].洗练消耗精粹=long.Parse(realms[i][13]);}
        cfg.敏感属性 = File.ReadAllLines("Assets/Data/Tables/法则敏感属性表.csv").Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => {
            var r = x.Split(','); return new LawFragmentCatalog.SensitiveRule { 属性 = (AttributeType)Enum.Parse(typeof(AttributeType), r[0]),
                登仙主增幅 = Range(r,1), 登仙副增幅 = Range(r,3), 登仙固定值 = Range(r,5),
                装备增幅上限 = float.Parse(r[7],CultureInfo.InvariantCulture), 装备固定上限 = float.Parse(r[8],CultureInfo.InvariantCulture),
                实际增量上限 = float.Parse(r[9],CultureInfo.InvariantCulture), 回复参照 = (AttributeType)Enum.Parse(typeof(AttributeType), r[10]), 回复占比上限 = float.Parse(r[11],CultureInfo.InvariantCulture) };
        }).ToArray();
    }
}
