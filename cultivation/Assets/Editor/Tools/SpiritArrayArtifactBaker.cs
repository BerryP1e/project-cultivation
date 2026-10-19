using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>按源网格分离浮世万象方块，保留材质/UV；成品独立保存，不覆盖导入 FBX。</summary>
public static class SpiritArrayArtifactBaker
{
    const string Folder="Assets/Art/Treasures/WorldsManifest";
    [MenuItem("工具/法宝/重建浮世万象")]
    public static void Bake()
    {
        EnsureFolder(Folder);
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/浮世万象/浮世万象.fbx");
        if(!source)throw new InvalidOperationException("缺少浮世万象源模型");
        // 展示页会旋转 prefab 根；把 FBX 的轴向补偿保留在子节点，避免展示时被覆盖。
        var root=new GameObject("浮世万象");UnityEngine.Object.Instantiate(source,root.transform,false);var pieces=new List<FloatingCubeCluster.Piece>();int faceCount=0;
        try{
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
                var mesh=filter.sharedMesh;var vs=mesh.vertices;var ts=mesh.triangles;var parents=Enumerable.Range(0,vs.Length).ToArray();
                int Find(int i){while(parents[i]!=i){parents[i]=parents[parents[i]];i=parents[i];}return i;}
                void Join(int a,int b){parents[Find(b)]=Find(a);}
                var same=new Dictionary<Vector3Int,int>();for(int i=0;i<vs.Length;i++){var p=vs[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));if(same.TryGetValue(key,out int other))Join(i,other);else same.Add(key,i);}
                for(int i=0;i<ts.Length;i+=3){Join(ts[i],ts[i+1]);Join(ts[i],ts[i+2]);}
                var groups=Enumerable.Range(0,vs.Length).GroupBy(Find).OrderByDescending(g=>g.Count()).Select(g=>g.ToArray()).ToArray();
                var anchors=groups.Where(g=>g.Length>=300).ToArray();if(anchors.Length<2)throw new InvalidOperationException("没有识别到独立外围方块，停止覆盖成品");
                var boxes=anchors.Select(g=>{var b=new Bounds(vs[g[0]],Vector3.zero);foreach(int i in g)b.Encapsulate(vs[i]);return b;}).ToArray();
                var owner=new int[vs.Length];
                foreach(var group in groups){int index=Array.FindIndex(anchors,g=>g==group);if(index<0){var centre=Vector3.zero;foreach(int i in group)centre+=vs[i]/group.Length;index=Enumerable.Range(0,boxes.Length).OrderBy(i=>boxes[i].SqrDistance(centre)).ThenBy(i=>(boxes[i].center-centre).sqrMagnitude).First();}foreach(int i in group)owner[i]=index;}
                var renderer=filter.GetComponent<MeshRenderer>();
                for(int part=0;part<anchors.Length;part++){
                    var used=Enumerable.Range(0,vs.Length).Where(i=>owner[i]==part).ToArray();var map=used.Select((index,i)=>new{index,i}).ToDictionary(x=>x.index,x=>x.i);var centre=boxes[part].center;
                    var split=new Mesh{name="Cube_"+part,indexFormat=mesh.indexFormat};split.vertices=used.Select(i=>vs[i]-centre).ToArray();split.uv=used.Select(i=>mesh.uv[i]).ToArray();split.normals=used.Select(i=>mesh.normals[i]).ToArray();if(mesh.tangents.Length==vs.Length)split.tangents=used.Select(i=>mesh.tangents[i]).ToArray();split.subMeshCount=mesh.subMeshCount;
                    for(int sub=0;sub<mesh.subMeshCount;sub++){var indices=mesh.GetTriangles(sub);var selected=new List<int>();for(int k=0;k<indices.Length;k+=3)if(owner[indices[k]]==part)for(int j=0;j<3;j++)selected.Add(map[indices[k+j]]);split.SetTriangles(selected,sub);faceCount+=selected.Count/3;}
                    split.RecalculateBounds();var path=Folder+"/Cube_"+part+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(split,saved);UnityEngine.Object.DestroyImmediate(split);}else{AssetDatabase.CreateAsset(split,path);saved=split;}
                    bool peripheral=part!=0&&Vector2.Distance(new Vector2(centre.x,centre.y),new Vector2(boxes[0].center.x,boxes[0].center.y))>.18f;
                    var go=new GameObject(part==0?"CentralCube":peripheral?"FloatingCube_"+part:"CentralDecoration_"+part);go.transform.SetParent(filter.transform,false);go.transform.localPosition=centre;go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                    if(peripheral){float nearest=Enumerable.Range(0,boxes.Length).Where(i=>i!=part).Select(i=>Vector3.Distance(boxes[i].center,centre)-boxes[i].extents.magnitude*.45f-boxes[part].extents.magnitude*.45f).Min();pieces.Add(new FloatingCubeCluster.Piece{transform=go.transform,position=centre,rotation=Quaternion.identity,amplitude=Mathf.Clamp(nearest*.08f,.003f,.012f),phase=part*2.39996f});}
                }
                UnityEngine.Object.DestroyImmediate(renderer);UnityEngine.Object.DestroyImmediate(filter);
            }
            root.AddComponent<FloatingCubeCluster>().pieces=pieces.ToArray();
            const string modelFolder="Assets/resources/法宝/浮世万象";EnsureFolder(modelFolder);
            var oldPath=Folder+"/浮世万象.prefab";var modelPath=modelFolder+"/浮世万象.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(oldPath)&&!AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)){string error=AssetDatabase.MoveAsset(oldPath,modelPath);if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);}
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,modelPath);
            var treasure=AssetDatabase.LoadAssetAtPath<TreasureDefinition>("Assets/Data/Generated/TreasureDefinition/treasure_spirit_array_talisman.asset");treasure.法宝名称="浮世万象";treasure.模型=prefab;treasure.模型资源路径="法宝/浮世万象/浮世万象";treasure.介绍="以万象为阵。装备后右键法宝图标打开编排面板，E 释放当前阵列；支持顺序/逆向打包与可调倾角的多重施法。";EditorUtility.SetDirty(treasure);
            var item=AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_spirit_array_talisman.asset");
            item.物品名="浮世万象认主符";item.介绍="使用后使法宝「浮世万象」认主。装备后右键编排灵阵，E 施放。";
            treasure.图标=item.图标=BakeIcon(prefab);EditorUtility.SetDirty(item);
            foreach(var definition in SpiritArrayRules.Templates){
                SpiritArrayRules.Migrate(definition.默认编排);
                if(definition.灵阵id=="array_program_5"&&definition.默认编排.名称=="反向三重"){
                    SpiritArrayNode Pack(SpiritArrayOp op,params SpiritArrayOp[] operations)=>new SpiritArrayNode{操作=op,子阵=operations.Select(SpiritArrayNode.Create).ToList()};
                    definition.默认编排.名称="正逆分流";definition.默认编排.序列=new List<SpiritArrayNode>{new SpiritArrayNode{操作=SpiritArrayOp.多重施法,外倾角=0,子阵=new List<SpiritArrayNode>{Pack(SpiritArrayOp.顺序打包,SpiritArrayOp.生成灵弹,SpiritArrayOp.追踪,SpiritArrayOp.加速),Pack(SpiritArrayOp.逆向打包,SpiritArrayOp.加速,SpiritArrayOp.强化,SpiritArrayOp.生成灵弹)}}};
                }
                definition.介绍="装备浮世万象后右键编排、E 施放。多重分支支持顺序或逆向打包；逆向只改变展开位置，不改变发射方向。";EditorUtility.SetDirty(definition);
            }
            BakeCenters();AssetDatabase.SaveAssets();Debug.Log("WORLDS_MANIFEST_BAKED floating="+pieces.Count+" faces="+faceCount);
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var slash=path.LastIndexOf('/');EnsureFolder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
    [MenuItem("工具/灵阵/重建独立中心图案库")]
    public static void BakeCenters()
    {
        const string folder="Assets/Art/SpiritArrays/Centers";EnsureFolder(folder);
        foreach(var op in new[]{SpiritArrayOp.生成灵弹,SpiritArrayOp.追踪,SpiritArrayOp.分裂,SpiritArrayOp.强化,SpiritArrayOp.加速,SpiritArrayOp.修正方向}){
            string name="Core_"+op,path=folder+"/"+name;var mesh=UnityEngine.Object.Instantiate(SpiritArrayGeometry.Core(op));mesh.name=name;var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path+".asset");if(saved){EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);}else{AssetDatabase.CreateAsset(mesh,path+".asset");saved=mesh;}
            var material=AssetDatabase.LoadAssetAtPath<Material>(path+".mat");if(!material){material=new Material(SpiritArrayRules.Templates[0].灵光材质);AssetDatabase.CreateAsset(material,path+".mat");}material.SetColor("_TintColor",SpiritArrayVisual.Tint(op));EditorUtility.SetDirty(material);
            var go=new GameObject(name);try{go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=material;PrefabUtility.SaveAsPrefabAsset(go,path+".prefab");}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        AssetDatabase.SaveAssets();
    }
    static Sprite BakeIcon(GameObject prefab)
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var instance=UnityEngine.Object.Instantiate(prefab);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance,scene);
        var cameraGo=new GameObject("ArtifactIconCamera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.orthographic=true;
        var lightGo=new GameObject("ArtifactIconLight");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo,scene);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.35f;light.transform.rotation=Quaternion.Euler(45,-30,0);
        var renderers=instance.GetComponentsInChildren<Renderer>();var box=renderers[0].bounds;foreach(var r in renderers)box.Encapsulate(r.bounds);camera.orthographicSize=box.extents.magnitude*.92f;camera.transform.position=box.center+new Vector3(1.6f,1.2f,-2.4f)*box.size.magnitude;camera.transform.LookAt(box.center);
        var target=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32);camera.targetTexture=target;var before=RenderTexture.active;var texture=new Texture2D(512,512,TextureFormat.RGBA32,false);const string path=Folder+"/浮世万象.png";
        try{camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{RenderTexture.active=before;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(texture);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
