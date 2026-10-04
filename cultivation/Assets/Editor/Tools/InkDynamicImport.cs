using UnityEditor;
using UnityEngine;

/// <summary>只导入新动态素材，避免重新导入旧包时覆盖人工微调的 border。</summary>
public static class InkDynamicImport
{
    [MenuItem("修仙/UI/导入动态水墨素材")]
    public static void 导入() {
        const string root="Assets/resources/UI/InkUI/Dynamic";
        int count=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{root})) {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null) continue;
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100; importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false; importer.isReadable=false; importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect; settings.spritePivot=new Vector2(.5f,.5f);
            settings.spriteAlignment=(int)SpriteAlignment.Center; importer.SetTextureSettings(settings);
            importer.spriteBorder=path.EndsWith("panel-ink-blob.png") ? new Vector4(256,256,256,256) : Vector4.zero;
            importer.SaveAndReimport(); count++;
        }
        Debug.Log("[InkDynamic] imported="+count+"; no scenes/data writes");
    }
}
