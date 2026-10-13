using UnityEditor;
using UnityEngine;

/// <summary>只导入六门新神通的专属图片并同步神通/玉简；不重建特效或场景。</summary>
public static class NewAbilityIconAssets
{
    public static readonly string[] Keys={"jianyu_luo","leiyun_strike","huoyan_yun","jianyu_luohua","huoyan_zhui","zhouyu_chuxie"};
    [MenuItem("修仙/UI/导入新神通专属图标（不动场景）")]
    public static void Import()
    {
        foreach(var key in Keys) {
            var path="Assets/resources/UI/InkUI/AbilityArt/icon-ability-"+key+".png";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new System.InvalidOperationException("图标缺失："+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit=100;importer.spriteBorder=Vector4.zero;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.sRGBTexture=true;importer.npotScale=TextureImporterNPOTScale.None;
            importer.maxTextureSize=512;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var ability=AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>("Assets/Data/Generated/ActiveDivineAbility/ability_"+key+".asset");
            var book=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_ability_"+key+".asset");
            if(!ability||!book||!sprite)throw new System.InvalidOperationException("神通/玉简/图片引用缺失："+key);
            ability.图标=sprite;book.图标=sprite;EditorUtility.SetDirty(ability);EditorUtility.SetDirty(book);
        }
        AssetDatabase.SaveAssets();Debug.Log("[新神通图标] 六门神通及玉简已同步，场景和特效未改动");
    }
}
