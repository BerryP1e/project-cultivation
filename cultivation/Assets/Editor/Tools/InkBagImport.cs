using UnityEditor;
using UnityEngine;

public static class InkBagImport
{
    [MenuItem("修仙/UI/导入背包动态素材")]
    public static void Import()
    {
        const string root="Assets/resources/UI/InkUI/Bag/";
        foreach(var file in new[]{"bag-parcel-8f.png","fx-ink-flow-loop.png","bag-info-ink.png"}) {
            var path=root+file; var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null) continue;
            importer.textureType=TextureImporterType.Sprite; importer.alphaIsTransparency=true;
            importer.mipmapEnabled=false; importer.isReadable=false; importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=file.StartsWith("fx-") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            if(file=="bag-parcel-8f.png") {
                importer.spriteImportMode=SpriteImportMode.Multiple;
                importer.GetSourceTextureWidthAndHeight(out int width,out int height);
                var slices=new SpriteMetaData[8];
                for(int i=0;i<8;i++) slices[i]=new SpriteMetaData { name="parcel-"+i.ToString("00"),rect=new Rect(i%4*(width/4), (1-i/4)*(height/2),width/4,height/2),pivot=new Vector2(.5f,.5f),alignment=(int)SpriteAlignment.Center };
                importer.spritesheet=slices;
            } else importer.spriteImportMode=SpriteImportMode.Single;
            importer.spriteBorder=Vector4.zero;
            var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }
}
