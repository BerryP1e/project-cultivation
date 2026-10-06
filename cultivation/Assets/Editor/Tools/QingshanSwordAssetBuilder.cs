using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>迁移青山剑模型与目录，保留模型 GUID；不保存场景。</summary>
public static class QingshanSwordAssetBuilder
{
    [MenuItem("修仙/法宝/更新青山剑（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("请先停止 Play");
        const string folder="Assets/resources/法宝/青山剑";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        const string model=folder+"/青山剑模型.prefab";
        const string old="Assets/resources/Weapons/base_sword.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(model)==null) {
            var error=AssetDatabase.MoveAsset(old,model);
            if(!string.IsNullOrEmpty(error))throw new System.InvalidOperationException(error);
        }
        var prefab=PrefabUtility.LoadPrefabContents(model);
        try {prefab.name="青山剑模型";PrefabUtility.SaveAsPrefabAsset(prefab,model);}
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        // The table importer does not delete retired rows' assets itself.
        foreach(var path in new[]{
            "Assets/Data/Generated/GongFaDefinition/gongfa_qingyun_jianjue.asset",
            "Assets/Data/Generated/ItemDefinition/item_gongfa_qingyun_jianjue.asset",
            "Assets/Data/Generated/能力物品/学功法效果_item_gongfa_qingyun_jianjue.asset"})
            if(AssetDatabase.LoadMainAssetAtPath(path)!=null)AssetDatabase.DeleteAsset(path);
        安全导入配置表.ImportAssetsOnly("功法表","物品表","法宝表");
        var definition=AssetDatabase.LoadAssetAtPath<TreasureDefinition>("Assets/Data/Generated/TreasureDefinition/treasure_qingshan_sword.asset");
        definition.模型=AssetDatabase.LoadAssetAtPath<GameObject>(model);
        var display=Object.Instantiate(definition.模型);
        try {
            display.transform.rotation=Quaternion.AngleAxis(-40,Quaternion.Euler(12,35,0)*Vector3.forward)*Quaternion.Euler(225,0,0);
            SectExchangeAssets.RenderIcon(display,folder+"/青山剑图标.png");
        } finally {Object.DestroyImmediate(display);}
        TrimIcon(folder+"/青山剑图标.png");
        AssetDatabase.ImportAsset(folder+"/青山剑图标.png");
        var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/青山剑图标.png");
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();definition.图标=AssetDatabase.LoadAssetAtPath<Sprite>(folder+"/青山剑图标.png");
        EditorUtility.SetDirty(definition);
        foreach(var guid in AssetDatabase.FindAssets("t:获得法宝效果")) {
            var effect=AssetDatabase.LoadAssetAtPath<获得法宝效果>(AssetDatabase.GUIDToAssetPath(guid));
            effect.法宝=PanelDatabase.取()?.法宝.Find(t=>t!=null && t.法宝id==effect.法宝id);
            EditorUtility.SetDirty(effect);
        }
        SectExchangeAssets.Sync();AssetDatabase.SaveAssets();PanelDatabase.清缓存();
        Debug.Log("[青山剑] 法宝、模型、图标、获取道具与目录已更新，旧功法入口已移除；未保存场景");
    }
    // Trim transparent margins, keeping a square canvas and consistent padding for HUD / inventory.
    static void TrimIcon(string path)
    {
        if(!File.Exists(path))return;
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        Texture2D result=null;
        try {
            source.LoadImage(File.ReadAllBytes(path));
            var pixels=source.GetPixels32();int minX=source.width,minY=source.height,maxX=-1,maxY=-1;
            for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)if(pixels[y*source.width+x].a>12) {
                minX=Mathf.Min(minX,x);minY=Mathf.Min(minY,y);maxX=Mathf.Max(maxX,x);maxY=Mathf.Max(maxY,y);
            }
            if(maxX<minX)return;
            int w=maxX-minX+1,h=maxY-minY+1;
            int size=Mathf.CeilToInt(Mathf.Max(w,h)*1.12f);
            var output=new Color32[size*size];int left=(size-w)/2,bottom=(size-h)/2;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)output[(y+bottom)*size+x+left]=pixels[(y+minY)*source.width+x+minX];
            result=new Texture2D(size,size,TextureFormat.RGBA32,false);result.SetPixels32(output);result.Apply();
            File.WriteAllBytes(path,result.EncodeToPNG());
        } finally {Object.DestroyImmediate(source);if(result!=null)Object.DestroyImmediate(result);}
    }
}
