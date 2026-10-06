using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>只生成法宝资产和原生模型图标，不改动任何场景或原始 FBX。</summary>
public static class ZhenyaohuAssetBuilder
{
    [MenuItem("Cultivation/Treasures/Build Zhenyaohu Assets")]
    public static void Build() {
        const string folder="Assets/resources/法宝/镇妖葫";
        Directory.CreateDirectory(folder);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/镇妖葫/镇妖葫.fbx");
        if(source==null) throw new System.InvalidOperationException("缺少镇妖葫 FBX");
        var model=new GameObject("镇妖葫模型");
        var original=(GameObject)PrefabUtility.InstantiatePrefab(source);
        original.transform.SetParent(model.transform,false);
        GameObject prefab;
        try {
            model.name="镇妖葫模型"; model.transform.position=Vector3.zero; model.transform.rotation=Quaternion.identity; model.transform.localScale=Vector3.one;
            var mouth=new GameObject("瓶口"); mouth.transform.SetParent(model.transform,false); mouth.transform.localPosition=new Vector3(0,.96f,0); mouth.transform.localRotation=Quaternion.Euler(-90,0,0);
            prefab=PrefabUtility.SaveAsPrefabAsset(model,folder+"/镇妖葫模型.prefab");
        } finally { Object.DestroyImmediate(model); }
        const string definitionFolder="Assets/Data/Generated/TreasureDefinition";
        Directory.CreateDirectory(definitionFolder);
        var path=definitionFolder+"/treasure_zhenyaohu.asset";
        if(AssetDatabase.LoadAssetAtPath<TreasureDefinition>(folder+"/镇妖葫.asset")!=null) AssetDatabase.MoveAsset(folder+"/镇妖葫.asset",path);
        var treasure=AssetDatabase.LoadAssetAtPath<TreasureDefinition>(path);
        if(treasure==null) { treasure=ScriptableObject.CreateInstance<TreasureDefinition>(); AssetDatabase.CreateAsset(treasure,path); }
        treasure.法宝id=UIPanelData.镇妖葫id; treasure.法宝名称="镇妖葫"; treasure.品阶=QualityTier.凡品;
        treasure.介绍="祭葫摄妖，凝其真灵为己所用。"; treasure.模型=prefab; treasure.模型资源路径="法宝/镇妖葫/镇妖葫模型";
        treasure.图标=RenderIcon(source,folder+"/镇妖葫图标.png");
        EditorUtility.SetDirty(treasure);
        var db=AssetDatabase.LoadAssetAtPath<PanelDatabase>("Assets/resources/面板/面板库.asset");
        if(db==null) throw new System.InvalidOperationException("缺少面板库");
        db.法宝.RemoveAll(t=>t!=null && t.法宝id==treasure.法宝id && t!=treasure);
        if(!db.法宝.Contains(treasure)) db.法宝.Add(treasure);
        EditorUtility.SetDirty(db); AssetDatabase.SaveAssets(); PanelDatabase.清缓存();
        Debug.Log("[镇妖葫] 模型、图标、定义和目录已生成；未保存或重建场景。");
    }
    static Sprite RenderIcon(GameObject source,string path) {
        GameObject stage=new GameObject("镇妖葫图标场地"); stage.transform.position=new Vector3(0,-8000,0);
        RenderTexture texture=null; Texture2D pixels=null; var old=RenderTexture.active;
        try {
            var model=Object.Instantiate(source,stage.transform); model.transform.localRotation=Quaternion.Euler(-8,20,-12)*source.transform.localRotation;
            Bounds bounds=new Bounds(); bool first=true;
            foreach(var r in model.GetComponentsInChildren<Renderer>()) { if(first) { bounds=r.bounds; first=false; } else bounds.Encapsulate(r.bounds); }
            var cameraGo=new GameObject("相机",typeof(Camera)); cameraGo.transform.SetParent(stage.transform,false);
            var camera=cameraGo.GetComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=bounds.extents.y*1.18f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.clear; camera.nearClipPlane=.01f; camera.farClipPlane=10;
            camera.transform.position=bounds.center+new Vector3(0,.12f,-2.5f); camera.transform.LookAt(bounds.center);
            foreach(var p in new[]{new Vector3(1,2,-2),new Vector3(-2,1,-1),new Vector3(1,2,2)}) {
                var light=new GameObject("补光",typeof(Light)); light.transform.SetParent(stage.transform,false); light.transform.position=bounds.center+p;
                var l=light.GetComponent<Light>(); l.type=LightType.Point; l.range=10; l.intensity=p.z<0?2:1; l.shadows=LightShadows.None;
            }
            texture=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32); texture.Create(); camera.targetTexture=texture; camera.Render();
            RenderTexture.active=texture; pixels=new Texture2D(512,512,TextureFormat.RGBA32,false); pixels.ReadPixels(new Rect(0,0,512,512),0,0); pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());
        } finally { RenderTexture.active=old; foreach(var c in stage.GetComponentsInChildren<Camera>()) c.targetTexture=null; if(pixels!=null) Object.DestroyImmediate(pixels); if(texture!=null) { texture.Release(); Object.DestroyImmediate(texture); } Object.DestroyImmediate(stage); }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
