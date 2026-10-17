using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Cultivation.WorldAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorldAuthoringResources
{
    public static readonly string[] CompleteRoots={"Assets/Stylized Far East","Assets/Stylized Forest/Assets","Assets/Stylized Grass Shader/Prefabs","Assets/StylizedWater2/_Demo/DemoAssets","Assets/StylizedWater/Demo/DemoAssets","Assets/_AsianArchitecture","Assets/WorldAuthoring/AsianAssemblies"};
    public static IEnumerable<string> CompleteAssets()
    {
        foreach(string root in CompleteRoots)
        {
            if(!AssetDatabase.IsValidFolder(root))continue;
            var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{root}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            foreach(var path in prefabs)if(LibraryItem(path))yield return path;
            var represented=new HashSet<string>(AssetDatabase.GetDependencies(prefabs,true));
            foreach(var path in AssetDatabase.FindAssets("",new[]{root}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".obj",StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x,StringComparer.Ordinal))
                if(!represented.Contains(path)&&LibraryItem(path)&&AssetDatabase.LoadAssetAtPath<GameObject>(path))yield return path;
        }
    }
    public static bool LibraryItem(string path)
    {
        if(!path.StartsWith("Assets/_AsianArchitecture/",StringComparison.Ordinal))return true;
        if(path.Contains("/_Rocks/"))return path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase);
        var name=Path.GetFileNameWithoutExtension(path);return name=="Building1"||name=="Building2"||name=="Building3";
    }
    public static string Pack(string path)=>path.Contains("_AsianArchitecture")||path.Contains("/AsianAssemblies/")?"亚洲建筑":path.Contains("Stylized Far East")?"Far East":path.Contains("Stylized Forest")?"森林":path.Contains("Stylized Grass")?"草花包":path.Contains("/Rocks/")?"岩石包":"水体包";
    public static string Category(string path)
    {
        string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        if(path.Contains("/AsianAssemblies/"))return name.Contains("bridge")?"桥道":"建筑";
        if(path.Contains("_AsianArchitecture/")&&(name.Contains("building")||name.Contains("house")||name.Contains("roof")||name.Contains("alcove")||name.Contains("column")||name.Contains("wall")||name.Contains("door")))return "建筑";
        if(path.Contains("/Buildings/"))return "建筑";
        if(name.Contains("wall_"))return "建筑";
        if(name.Contains("cloud"))return "天空";
        if(name.Contains("street")||name.Contains("stair")||name.Contains("bridge")||name.Contains("pier")||name.Contains("raft")||name.Contains("platform")||name.Contains("road"))return "桥道";
        if((name.Contains("tree")&&!name.Contains("treeslot"))||name.Contains("broadleaf"))return "树木";
        if(name.Contains("rock")||name.Contains("cliff"))return "岩石";
        if(name.Contains("grass")||name.Contains("flower")||name.Contains("bush")||name.Contains("shrub")||name.Contains("groundcover")||name.Contains("fern"))return "草花";
        if(name.Contains("stair")||name.Contains("bridge")||name.Contains("pier")||name.Contains("raft")||name.Contains("platform")||name.Contains("road"))return "桥道";
        return "摆件";
    }
    public static float Scale(string path,string category,Bounds bounds)
    {
        float height=Mathf.Max(.01f,bounds.size.y);string name=Path.GetFileNameWithoutExtension(path);
        if((path.Contains("_AsianArchitecture/")||path.Contains("/AsianAssemblies/"))&&(category=="建筑"||category=="桥道"))return 1.25f;
        if(category=="树木")
        {
            float target=name.Contains("Pine")?Mathf.Clamp(height*.42f,7,10.5f):name.Contains("Fir")?Mathf.Clamp(height*.9f,5.5f,9):name.Contains("Dry")?Mathf.Clamp(height*.65f,4.5f,7):name.Contains("large")?Mathf.Clamp(height*.95f,8,11):Mathf.Clamp(height*.64f,6,9);
            return Mathf.Clamp(target/height,.08f,3);
        }
        if(category=="岩石"&&path.Contains("Far East"))return Mathf.Clamp(10/height,.12f,1);
        if(category=="桥道"&&path.Contains("StylizedWater"))return Mathf.Clamp(6/Mathf.Max(bounds.size.x,bounds.size.z),.1f,2);
        if(category=="草花"&&height>1.3f)return 1.1f/height;
        return 1;
    }
    [MenuItem("修仙/场景工坊/更新素材库（保留地图）")]
    public static void RefreshLibrary()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止运行。");
        PrepareAsianAssemblies();
        var palette=AssetDatabase.LoadAssetAtPath<WorldMapPalette>(WorldMapEditorBuilder.PalettePath);if(!palette)throw new InvalidOperationException("请先创建场景工坊。");
        var entries=palette.entries.ToList();
        foreach(var path in CompleteAssets())WorldMapEditorBuilder.Add(entries,path,Category(path),Path.GetFileNameWithoutExtension(path));
        palette.entries=entries.ToArray();
        foreach(var entry in entries)
        {
            entry.libraryHidden=!LibraryItem(entry.sourcePath??"");
            if(entry.category!="建筑"&&entry.category!="桥道"&&!(entry.sourcePath??"").StartsWith("Assets/_AsianArchitecture/",StringComparison.Ordinal))continue;
            int slot=0;foreach(var renderer in entry.prefab.GetComponentsInChildren<Renderer>(true))foreach(var original in renderer.sharedMaterials){if(slot<entry.materials.Length&&!(renderer is ParticleSystemRenderer)&&!(renderer is TrailRenderer)&&!(renderer is LineRenderer)){var material=entry.materials[slot];WorldMapEditorBuilder.ConfigureSurfaceMaterial(material,original,entry.category,entry.sourcePath);if(material){EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);}}slot++;}
        }
        var boundaryIds=palette.boundaryRocks.Select(x=>palette.entries[x].id).ToArray();
        var used=new HashSet<string>();void Remember(WorldMapData data){if(data?.objects!=null)foreach(var item in data.objects)used.Add(item.paletteId);}
        foreach(var path in AssetDatabase.FindAssets("t:WorldMapAsset").Select(AssetDatabase.GUIDToAssetPath)){var map=AssetDatabase.LoadAssetAtPath<WorldMapAsset>(path);Remember(JsonUtility.FromJson<WorldMapData>(map.sourceJson));}
        foreach(var path in Directory.GetFiles(WorldMapStorage.DirectoryPath,"*.json",SearchOption.AllDirectories))Remember(JsonUtility.FromJson<WorldMapData>(File.ReadAllText(path)));
        int removed=entries.RemoveAll(x=>x.libraryHidden&&!used.Contains(x.id));palette.entries=entries.ToArray();palette.boundaryRocks=boundaryIds.Select(palette.Find).Where(x=>x>=0).ToArray();
        foreach(var entry in entries.Where(x=>!x.libraryHidden&&(x.category=="建筑"||x.category=="桥道"))){if(entry.IsBridge)entry.bridgeDeckHeight=MeasureBridgeDeck(entry);WorldMapEditorBuilder.RefreshThumbnail(entry);}
        EditorUtility.SetDirty(palette);AssetDatabase.SaveAssetIfDirty(palette);Inventory(palette);
        Debug.Log("可选素材 "+entries.Count(x=>!x.libraryHidden)+" 项；删除无引用零件条目 "+removed+" 项，已有地图引用的零件不再显示于可选库。");
    }
    static float MeasureBridgeDeck(WorldPaletteEntry entry)
    {
        var scene=EditorSceneManager.NewPreviewScene();var root=UnityEngine.Object.Instantiate(entry.prefab);SceneManager.MoveGameObjectToScene(root,scene);
        try{
            foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(script);
            var renderers=root.GetComponentsInChildren<Renderer>(true);var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh&&!filter.GetComponent<Collider>())filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            var colliders=root.GetComponentsInChildren<Collider>(true);foreach(var collider in colliders)collider.enabled=true;Physics.SyncTransforms();float deck=float.MaxValue;bool alongX=bounds.size.x>=bounds.size.z;
            foreach(float end in new[]{-.43f,.43f}){var p=bounds.center+(alongX?new Vector3(bounds.size.x*end,0,0):new Vector3(0,0,bounds.size.z*end));p.y=bounds.max.y+1;foreach(var collider in colliders)if(collider.Raycast(new Ray(p,Vector3.down),out var hit,bounds.size.y+3)&&hit.normal.y>.55f)deck=Mathf.Min(deck,hit.point.y);}
            return deck<float.MaxValue?(entry.preservePivot?deck:Mathf.Max(0,deck-bounds.min.y)):0;
        }finally{UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void PrepareAsianAssemblies()
    {
        const string directory="Assets/WorldAuthoring/AsianAssemblies",source="Assets/_AsianArchitecture/Scene.unity";if(!File.Exists(source))return;
        if(!AssetDatabase.IsValidFolder(directory))AssetDatabase.CreateFolder(WorldMapEditorBuilder.Root,"AsianAssemblies");
        var scene=EditorSceneManager.OpenScene(source,OpenSceneMode.Additive);var preview=EditorSceneManager.NewPreviewScene();
        try{
            var renderers=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
            void Save(string name,IEnumerable<MeshRenderer> parts)
            {
                var selected=parts.ToArray();if(selected.Length==0)return;var bounds=selected[0].bounds;foreach(var renderer in selected)bounds.Encapsulate(renderer.bounds);var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,preview);
                try{for(int i=0;i<selected.Length;i++){var renderer=selected[i];var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                    var go=new GameObject(renderer.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(root.transform,false);go.transform.SetPositionAndRotation(renderer.transform.position-origin,renderer.transform.rotation);go.transform.localScale=renderer.transform.lossyScale;
                    string meshPath=directory+"/"+name+"-"+i+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);var copy=UnityEngine.Object.Instantiate(filter.sharedMesh);copy.name=name+" "+i;if(mesh){EditorUtility.CopySerialized(copy,mesh);UnityEngine.Object.DestroyImmediate(copy);}else{mesh=copy;AssetDatabase.CreateAsset(mesh,meshPath);}EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;go.GetComponent<MeshCollider>().sharedMesh=mesh;
                }PrefabUtility.SaveAsPrefabAsset(root,directory+"/"+name+".prefab");}finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            Save("Building_Pavilion",renderers.Where(x=>x.name=="Lamp_080"));
            Save("Building_Gate",renderers.Where(x=>x.name.StartsWith("Fence_Gate",StringComparison.Ordinal)));
            var bridge=renderers.Where(x=>x.name.StartsWith("Bridge_",StringComparison.Ordinal)).ToArray();Save("Bridge_Long",bridge.Where(x=>x.transform.position.x>8));Save("Bridge_Short",bridge.Where(x=>x.transform.position.x<=8));
        }finally{EditorSceneManager.ClosePreviewScene(preview);EditorSceneManager.CloseScene(scene,true);}
    }
    public static void GroundLayers(WorldMapPalette palette)
    {
        var layers=new List<WorldGroundLayer>();
        Action<string,string> add=(path,label)=>{var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture)layers.Add(new WorldGroundLayer{id=AssetDatabase.AssetPathToGUID(path),label=label,texture=texture});};
        const string east="Assets/Stylized Far East/Materials/Textures/",forest="Assets/Stylized Forest/Assets/Textures/Terrain Textures/";
        add(east+"M_pavement_BaseColor2.png","青灰砖路");add(east+"M_pavement_02_BaseColor.png","旧石板路");add(east+"M_stone_bricks_BaseMap.png","石砖铺地");add(east+"M_dirt_BaseColor.png","裸土过渡");add(east+"M_DirtGrass_BaseMap.png","草土衔接");
        for(int i=1;i<=8;i++)add(forest+"Ground_"+i+".jpg",new[]{"森林草土","林地苔色","深色泥土","枯叶地表","岩土碎屑","浅灰石地","砂砾地面","林间地表"}[i-1]);
        for(int i=1;i<=3;i++)add(forest+"Snow_"+i+".jpg","积雪 "+i);add(forest+"Ice.jpg","冰面");add("Assets/StylizedWater2/_Demo/DemoAssets/Terrain/SWS_DemoSand.png","浅色沙土");
        palette.groundLayers=layers.ToArray();
        const int size=512;var array=new Texture2DArray(size,size,layers.Count,TextureFormat.RGBA32,true,false){name="GroundTextureLayers",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
        var previous=RenderTexture.active;var rt=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var readable=new Texture2D(size,size,TextureFormat.RGBA32,false,false);
        try
        {
            for(int layer=0;layer<layers.Count;layer++)
            {
                Graphics.Blit(layers[layer].texture,rt);RenderTexture.active=rt;readable.ReadPixels(new Rect(0,0,size,size),0,0);readable.Apply();var colours=readable.GetPixels();
                bool grass=layers[layer].label.Contains("草")||layers[layer].label.Contains("苔");
                for(int i=0;i<colours.Length;i++){float value=colours[i].grayscale;var colour=Color.Lerp(new Color(value,value,value),colours[i],grass?.55f:.78f);colours[i]=colour*(grass?.72f:.88f);colours[i].a=1;}
                array.SetPixels(colours,layer,0);
            }
            array.Apply(true,false);string path=WorldMapEditorBuilder.Root+"/Materials/GroundTextureLayers.asset";var existing=AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if(existing){EditorUtility.CopySerialized(array,existing);existing.Apply(false,false);UnityEngine.Object.DestroyImmediate(array);array=existing;}else AssetDatabase.CreateAsset(array,path);
            EditorUtility.SetDirty(array);AssetDatabase.SaveAssetIfDirty(array);palette.groundTextureArray=array;
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(readable);}
    }
    public static void PlayerReference(WorldMapPalette palette)
    {
        const string path="Assets/Scenes/3C_Testbed.scene";var existing=SceneManager.GetSceneByPath(path);bool opened=!existing.isLoaded;var source=opened?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):existing;
        var preview=EditorSceneManager.NewPreviewScene();GameObject clone=null;
        try
        {
            var player=source.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
            if(!player)throw new InvalidOperationException("正式测试场景缺少玩家参照");
            var visual=player.transform.Find("Player_Visual");if(!visual)throw new InvalidOperationException("玩家视觉节点缺失");
            clone=UnityEngine.Object.Instantiate(visual.gameObject);SceneManager.MoveGameObjectToScene(clone,preview);clone.name="ReferencePlayer";clone.transform.position=Vector3.zero;clone.transform.rotation=visual.localRotation;clone.transform.localScale=visual.lossyScale;
            var animator=clone.GetComponentInChildren<Animator>(true);var controller=animator?animator.runtimeAnimatorController:null;
            palette.referenceIdle=controller?controller.animationClips.Where(x=>x&&(x.name.ToLowerInvariant().Contains("idle")||x.name.Contains("待机"))&&!x.name.Contains("御风")&&!x.name.ToLowerInvariant().Contains("fly")).OrderBy(x=>x.name.Length).FirstOrDefault():null;
            foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(script);
            foreach(var collider in clone.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(var body in clone.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(body);
            foreach(var light in clone.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
            int slot=0;bool found=false;Bounds bounds=default;
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
                var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)
                {
                    var original=materials[i];var material=WorldMapEditorBuilder.Material("ReferencePlayer_"+slot++,"Cultivation/WorldAuthoring/Surface");
                    var texture=original&&original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):null;if(!texture&&original&&original.HasProperty("_MainTex"))texture=original.GetTexture("_MainTex");
                    material.SetTexture("_BaseMap",texture);material.SetFloat("_TextureStrength",texture?1:0);material.SetFloat("_NoiseStrength",0);material.SetFloat("_RockBlend",0);material.SetColor("_BaseColor",original&&original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original&&original.HasProperty("_Color")?original.GetColor("_Color"):Color.white);materials[i]=material;
                }
                renderer.sharedMaterials=materials;
            }
            if(animator){animator.runtimeAnimatorController=null;animator.applyRootMotion=false;}
            palette.playerHeight=found?bounds.size.y:2.6f;palette.referencePlayer=PrefabUtility.SaveAsPrefabAsset(clone,WorldMapEditorBuilder.Root+"/ReferencePlayer.prefab");
            Debug.Log("玩家参照保留正式视觉尺寸："+palette.playerHeight.ToString("F2")+"m，待机 "+(palette.referenceIdle?palette.referenceIdle.name:"无"));
        }
        finally{if(clone)UnityEngine.Object.DestroyImmediate(clone);EditorSceneManager.ClosePreviewScene(preview);if(opened)EditorSceneManager.CloseScene(source,true);}
    }
    public static void Inventory(WorldMapPalette palette)
    {
        string Quote(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
        var lines=new List<string>{"名称,分类,资源包,源资源,默认倍率,宽米,高米,深米"};
        foreach(var entry in palette.entries.Where(x=>!x.libraryHidden)){var size=entry.bounds.size*entry.defaultScale;lines.Add(string.Join(",",Quote(entry.label),Quote(entry.category),Quote(entry.sourcePack),Quote(string.IsNullOrEmpty(entry.sourcePath)?AssetDatabase.GetAssetPath(entry.prefab):entry.sourcePath),entry.defaultScale.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),size.x.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),size.y.ToString("F2",System.Globalization.CultureInfo.InvariantCulture),size.z.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)));}
        string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/reports"));Directory.CreateDirectory(directory);File.WriteAllLines(Path.Combine(directory,"工坊资源完整清单.csv"),lines,new System.Text.UTF8Encoding(true));
    }
}
