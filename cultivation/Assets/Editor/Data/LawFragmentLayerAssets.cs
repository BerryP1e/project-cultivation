using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>独立分层图集的稳定子图引用；只导入素材/配置，不重建数值或保存场景。</summary>
public static class LawFragmentLayerAssets
{
    [MenuItem("修仙/法则/导入分层动效素材")]
    public static void 导入()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止 Play");
        var cfg=Resources.Load<LawFragmentCatalog>("Laws/Catalog");配置(cfg);EditorUtility.SetDirty(cfg);AssetDatabase.SaveAssets();
    }
    public static void 配置(LawFragmentCatalog cfg)
    {
        var low=Import("cores-qi-spirit",3,2,new[]{"qi","foundation","core","soul","spirit"});
        var high=Import("cores-void-immortal",2,2,new[]{"void","union","mahayana","immortal"});
        var overlay=Import("ink-spirit",2,2,new[]{"ink-a","spirit-a","ink-b","spirit-b"});
        if(low.Length<5||high.Length<4||overlay.Length<4)return;
        var cores=low.Concat(high).ToArray();for(int i=0;i<9;i++)cfg.境界[i].晶体图标=cores[i];
        cfg.墨纹图层=new[]{overlay[0],overlay[2]};cfg.灵气图层=new[]{overlay[1],overlay[3]};
    }
    static Sprite[] Import(string id,int columns,int rows,string[] names)
    {
        string path="Assets/resources/Laws/Layers/"+id+".png";var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)return new Sprite[0];
        importer.GetSourceTextureWidthAndHeight(out int width,out int height);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.alphaIsTransparency=true;
        importer.mipmapEnabled=false;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.Compressed;importer.spritePixelsPerUnit=100;
        var rects=new SpriteMetaData[names.Length];
        for(int i=0;i<names.Length;i++)rects[i]=new SpriteMetaData{name=names[i],pivot=new Vector2(.5f,.5f),alignment=(int)SpriteAlignment.Center,rect=new Rect((i%columns)*width/(float)columns,(rows-1-i/columns)*height/(float)rows,width/(float)columns,height/(float)rows)};
#pragma warning disable 0618
        importer.spritesheet=rects;
#pragma warning restore 0618
        importer.SaveAndReimport();var sprites=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();return names.Select(n=>sprites.First(s=>s.name==n)).ToArray();
    }
}
