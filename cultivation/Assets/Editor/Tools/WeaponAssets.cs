using System;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>武器表、获得物品、目录与原生模型图标一起重建，不写场景。</summary>
public static class WeaponAssets
{
    [MenuItem("修仙/武器/重建独立武器数据与图标（不动场景）")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先退出运行模式");
        foreach(var id in new[]{"gongfa_jiuba_xuangong","gongfa_taixu_jianjue","gongfa_lingxu_jianjue","gongfa_taichu_longqijue"}){
            AssetDatabase.DeleteAsset("Assets/Data/Generated/GongFaDefinition/"+id+".asset");
            AssetDatabase.DeleteAsset("Assets/Data/Generated/ItemDefinition/item_"+id+".asset");
        }
        DataTableImporter.ImportTables(false,new[]{"武器表","物品表","功法表"});面板库收集器.收集();
        const string folder="Assets/resources/UI/InkUI/Weapons";Directory.CreateDirectory(folder);
        foreach(var weapon in WeaponCatalog.全部){
            var prefab=Resources.Load<GameObject>(weapon.模型资源路径);if(!prefab)throw new InvalidOperationException("武器模型缺失："+weapon.武器id);
            var holder=new GameObject("WeaponThumbnailSource"){hideFlags=HideFlags.HideAndDontSave};
            try{
                var model=UnityEngine.Object.Instantiate(prefab,holder.transform,false);Bounds bounds=new Bounds();bool first=true;
                foreach(var r in model.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                var size=bounds.size;Vector3 axis=size.x>size.y&&size.x>size.z?Vector3.right:size.z>size.y?Vector3.forward:Vector3.up;
                var q=Quaternion.Euler(0,0,-28)*Quaternion.FromToRotation(axis,Vector3.up);model.transform.localRotation=q*model.transform.localRotation;model.transform.localPosition=-(q*bounds.center);
                string path=folder+"/"+weapon.武器id+".png";SectExchangeAssets.RenderIcon(holder,path);AssetDatabase.ImportAsset(path);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=512;importer.SaveAndReimport();weapon.图标=AssetDatabase.LoadAssetAtPath<Sprite>(path);EditorUtility.SetDirty(weapon);
            }finally{UnityEngine.Object.DestroyImmediate(holder);}
        }
        SectExchangeAssets.Sync();AssetDatabase.SaveAssets();
        Debug.Log("[独立武器] 已重建 "+WeaponCatalog.全部.Count+" 把武器、物品效果和图标；未保存场景");
    }
}
