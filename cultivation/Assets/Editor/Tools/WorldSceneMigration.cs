using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Cultivation.WorldAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Maintained converter shared by the scene workshop and consistency checks.
// Gameplay objects stay in their original scenes, preserving serialized references and names.
public static class WorldSceneMigration
{
    public static readonly string[] Scenes={"3C_Testbed","Sect_Wilderness","Demon-Suppressing Tower"};
    public static readonly string[] Labels={"个人洞府","宗门野外","镇妖塔内"};
    const string Root="Assets/WorldAuthoring/Scenes";
    static readonly HashSet<string> FunctionTypes=new HashSet<string>{"StationInteractable","Teleporter","SpawnZone","SpawnPoint","任务触发区","仅编辑器可见","功德堂兑换","传法阁兑换"};
    static string Key(GameObject go)=>GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
    static bool Functional(GameObject go)=>go.GetComponents<MonoBehaviour>().Any(x=>x&&FunctionTypes.Contains(x.GetType().Name))&&!go.GetComponent<NpcInstance>();

    public static void ConvertAll()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止运行。");
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var prior=SceneManager.GetActiveScene();var palette=AssetDatabase.LoadAssetAtPath<WorldMapPalette>(WorldMapEditorBuilder.PalettePath);
        if(!palette)throw new InvalidOperationException("请先更新工坊素材。");
        string backup=Path.GetFullPath("../.local/backups/scene-migration-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(backup);
        foreach(var name in Scenes){File.Copy("Assets/Scenes/"+name+".scene",Path.Combine(backup,name+".scene"));File.Copy("Assets/Scenes/"+name+".scene.meta",Path.Combine(backup,name+".scene.meta"));}
        var report=new StringBuilder("# 五场景迁移\n\n原场景备份："+backup+"\n");
        try
        {
            for(int i=0;i<Scenes.Length;i++)
            {
                var path="Assets/Scenes/"+Scenes[i]+".scene";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try{SceneManager.SetActiveScene(scene);Convert(scene,palette,Labels[i],report);EditorSceneManager.SaveScene(scene);}
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            EditorUtility.SetDirty(palette);AssetDatabase.SaveAssetIfDirty(palette);WorldAuthoringResources.Inventory(palette);
            Directory.CreateDirectory("../docs/reports");File.WriteAllText("../docs/reports/五场景地图迁移.md",report.ToString());
        }
        finally{EditorUtility.ClearProgressBar();SceneManager.SetActiveScene(prior);}
        Debug.Log("五场景转换完成。地图已写入 maps/source，原场景备份："+backup);
    }

    static void Convert(Scene scene,WorldMapPalette palette,string label,StringBuilder report)
    {
        var roots=scene.GetRootGameObjects();if(roots.Any(x=>x.GetComponent<WorldMapLoader>())){report.AppendLine("\n- "+label+"：已迁移，保留当前地图，未重复转换。");return;}
        string directory=Root+"/"+scene.name;Directory.CreateDirectory(directory+"/Visuals");Directory.CreateDirectory(directory+"/Features");AssetDatabase.Refresh();
        var original=roots.SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();
        var functional=original.Where(t=>Functional(t.gameObject)&&!t.GetComponentsInParent<Transform>().Skip(1).Any(p=>Functional(p.gameObject))).Select(t=>t.gameObject).ToArray();
        var keys=original.ToDictionary(x=>x.gameObject,x=>Key(x.gameObject));
        var data=BuildGround(scene,label,original,out var grounds,out var waters);data.sourceScene=scene.name;
        var spawn=original.FirstOrDefault(x=>x.name=="SpawnPoint");var player=original.FirstOrDefault(x=>x.GetComponent<PlayerController>());data.spawn=spawn?spawn.position:player?player.position:Vector3.up;
        if(scene.name=="Sect_Wilderness"){data.spawn.y=data.Sample(data.height,data.spawn.x,data.spawn.z)+.15f;if(player)player.position=new Vector3(player.position.x,data.spawn.y,player.position.z);if(spawn)spawn.position=data.spawn;}
        var entries=palette.entries.ToList();var templates=palette.featureTemplates.Where(x=>x).ToList();var bindings=new List<WorldSceneBinding>();
        var treeChoices=GreenTrees(entries);
        var processed=new HashSet<GameObject>();int replacedTrees=0;
        Action<GameObject> Visual=null;
        Visual=go=>
        {
            if(!go.activeInHierarchy||processed.Contains(go)||grounds.Any(g=>go==g||go.transform.IsChildOf(g.transform))||waters.Contains(go)||go.name.StartsWith("better ground"))return;
            if(go.GetComponent<PlayerController>()||go.GetComponent<NpcInstance>()||go.GetComponent<Canvas>()||go.GetComponent<Camera>()||go.GetComponent<Light>()||go.GetComponent<AirWall>()||go.GetComponent<HomeBoundary>()||go.name=="空气墙"||go.name.StartsWith("Effect_"))return;
            if(Functional(go)&&!functional.Contains(go))return;
            var renderers=go.GetComponentsInChildren<Renderer>(true).Where(x=>x.enabled&&x.gameObject.activeInHierarchy).ToArray();if(renderers.Length==0){foreach(Transform child in go.transform)Visual(child.gameObject);return;}
            bool nature=IsTree(go),grass=IsGrass(go);
            if(grass){foreach(var renderer in renderers){var p=renderer.bounds.center;int x=Mathf.RoundToInt((p.x+data.Width*.5f)/data.cellSize),z=Mathf.RoundToInt((p.z+data.Width*.5f)/data.cellSize);if(x>0&&z>0&&x<data.resolution-1&&z<data.resolution-1&&!data.IsWet(p.x,p.z))data.grass[data.Index(x,z)]=1;}Hide(go);processed.Add(go);return;}
            bool standalone=PrefabUtility.IsAnyPrefabInstanceRoot(go)||go.GetComponent<Renderer>()||functional.Contains(go);
            if(!standalone){foreach(Transform child in go.transform)Visual(child.gameObject);return;}
            string instanceId=keys[go];WorldPaletteEntry entry;Vector3 position=go.transform.position,rotation=go.transform.eulerAngles,axisScale=go.transform.lossyScale;bool exact=true;float scale=1;
            if(nature&&treeChoices.Length>0)
            {
                entry=treeChoices[(int)(Math.Abs((long)Hash128.Compute(instanceId).GetHashCode())%treeChoices.Length)];var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float desired=Mathf.Clamp(bounds.size.y,5.5f,10.5f);scale=entry.defaultScale*Mathf.Clamp(desired/Mathf.Max(.1f,entry.bounds.size.y*entry.defaultScale),.75f,1.25f);exact=false;replacedTrees++;
                position.y=data.HasGround(position.x,position.z)?data.Sample(data.height,position.x,position.z)-.1f:bounds.min.y;
            }
            else
            {
                string prefab=Snapshot(go,directory+"/Visuals",false);
                WorldMapEditorBuilder.Add(entries,prefab,Building(go)?"建筑":IsRock(go)?"岩石":"摆件",go.name);
                entry=entries.First(x=>x.id==AssetDatabase.AssetPathToGUID(prefab));entry.preservePivot=true;entry.sourcePack="原场景 · "+label;entry.sourcePath=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);entry.defaultScale=1;entry.legacyScale=1;
                if(scene.name=="Sect_Wilderness"){float ground=OriginalGroundHeight(scene,position.x,position.z);position.y-=ground;}
            }
            var item=new MapObjectData{instanceId=instanceId,paletteId=entry.id,position=position,yaw=rotation.y,rotation=rotation,axisScale=axisScale,exactTransform=exact,scale=scale,sizingVersion=1,contactHeight=data.Sample(data.height,position.x,position.z)};
            if(functional.Contains(go))item.featureId=instanceId;data.objects.Add(item);processed.Add(go);Hide(go);
        };
        if(scene.name!="Demon-Suppressing Tower")foreach(var root in roots)Visual(root);
        foreach(var go in functional)
        {
            var id=keys[go];string path=Snapshot(go,directory+"/Features",true);var assetPath=directory+"/Features/"+Hash128.Compute(id)+".asset";var template=AssetDatabase.LoadAssetAtPath<WorldFeatureTemplate>(assetPath);
            if(!template){template=ScriptableObject.CreateInstance<WorldFeatureTemplate>();AssetDatabase.CreateAsset(template,assetPath);}template.id=id;template.label=FeatureLabel(go);template.kind=string.Join(" + ",go.GetComponents<MonoBehaviour>().Where(x=>x&&FunctionTypes.Contains(x.GetType().Name)).Select(x=>x.GetType().Name));template.sourceScene=scene.name;template.sourcePath=HierarchyPath(go.transform);template.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);template.references=References(go,keys,bindings).ToArray();EditorUtility.SetDirty(template);AssetDatabase.SaveAssetIfDirty(template);templates.Add(template);
            var feature=new WorldFeatureData{id=id,templateId=id,label=go.name,position=go.transform.position,rotation=go.transform.eulerAngles,scale=go.transform.lossyScale,enabled=go.activeSelf};var item=data.objects.Find(x=>x.instanceId==id);if(item!=null){feature.objectId=id;item.featureId=id;}
            if(scene.name=="Sect_Wilderness"&&item==null){feature.position.y=go.GetComponent<SpawnZone>()?data.Sample(data.height,feature.position.x,feature.position.z):feature.position.y-OriginalGroundHeight(scene,feature.position.x,feature.position.z);go.transform.position=feature.position;}
            data.features.Add(feature);bindings.Add(new WorldSceneBinding{key=id,target=go,managedFeature=true});
        }
        foreach(var go in grounds){if(go.GetComponent<HomeSurface>()){var boundary=go.GetComponentInChildren<HomeBoundary>(true);if(boundary)Object.DestroyImmediate(boundary.gameObject);}go.SetActive(false);}
        foreach(var go in waters)go.SetActive(false);
        if(scene.name=="Sect")foreach(var t in original.Where(x=>x.name.StartsWith("better ground")))t.gameObject.SetActive(false);
        palette.entries=entries.ToArray();palette.featureTemplates=templates.GroupBy(x=>x.id).Select(x=>x.Last()).ToArray();
        WorldFeatureRuntime.Synchronize(data);data.Validate();WorldMapStorage.Save(data);
        var mapPath=directory+"/Map.asset";var map=ScriptableObject.CreateInstance<WorldMapAsset>();map.palette=palette;map.sourceJson=JsonUtility.ToJson(data);AssetDatabase.CreateAsset(map,mapPath);
        var environment=new GameObject("WorldMap · "+label);SceneManager.MoveGameObjectToScene(environment,scene);var loader=environment.AddComponent<WorldMapLoader>();loader.map=map;loader.bindings=bindings.GroupBy(x=>x.key).Select(x=>x.First()).ToArray();loader.homeMap=scene.name=="3C_Testbed";
        // Runtime mesh generation does not serialize transient geometry into the scene.
        EditorUtility.SetDirty(map);AssetDatabase.SaveAssetIfDirty(map);
        report.AppendLine("\n- "+label+"："+data.resolution+"²，精度 "+data.cellSize+"m，外观 "+data.objects.Count+"，功能模块 "+data.features.Count+"，替换树 "+replacedTrees+"。地图：maps/source/"+WorldMapStorage.SafeName(data.name)+".worldmap.json");
    }

    static void Hide(GameObject go){foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.enabled=false;foreach(var c in go.GetComponentsInChildren<Collider>(true))if(!c.isTrigger)c.enabled=false;}
    static bool IsTree(GameObject go){string s=(go.name+" "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)).ToLowerInvariant();return s.Contains("/tree/")||s.Contains("_tree_")||s.Contains("shu1")||s.Contains("shu2");}
    static bool IsGrass(GameObject go){string s=(go.name+" "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)).ToLowerInvariant();return s.Contains("/grass/")||s.Contains("_grass_")||s.Contains("草丛")||s.Contains("草地块");}
    static bool IsRock(GameObject go)=>((go.name+" "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)).ToLowerInvariant().Contains("stone"));
    static bool Building(GameObject go){string s=(go.name+" "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)).ToLowerInvariant();return s.Contains("building")||s.Contains("/build/")||s.Contains("fangwu")||s.Contains("fangzi")||s.Contains("墙")||s.Contains("cultivation room");}
    static string FeatureLabel(GameObject go){var station=go.GetComponent<StationInteractable>();return station?station.标题:go.name;}
    static WorldPaletteEntry[] GreenTrees(IEnumerable<WorldPaletteEntry> entries)=>entries.Where(x=>x.category=="树木"&&x.sourcePath.Contains("Stylized Forest/Assets/Prefabs/")&&!x.sourcePath.Contains("Winter")&&!x.sourcePath.Contains("Autumn")&&(x.prefab.name.StartsWith("Broadleaf")||x.prefab.name.StartsWith("FirTree")||x.prefab.name.StartsWith("PineTree"))&&x.bounds.size.y*x.defaultScale>5).ToArray();
    static string HierarchyPath(Transform node)=>node.parent?HierarchyPath(node.parent)+"/"+node.name:node.name;

    static string Snapshot(GameObject source,string directory,bool function)
    {
        // Sharing is based on visual local hierarchy, so repeated rocks reuse one prefab.
        var fingerprint=new StringBuilder(function?Key(source):"visual");
        foreach(var t in source.GetComponentsInChildren<Transform>(true)){fingerprint.Append(t.name);if(t!=source.transform)fingerprint.Append(t.localPosition.ToString("F5")).Append(t.localRotation.ToString("F5")).Append(t.localScale.ToString("F5"));var filter=t.GetComponent<MeshFilter>();if(filter)fingerprint.Append(AssetDatabase.GetAssetPath(filter.sharedMesh)).Append(filter.sharedMesh?filter.sharedMesh.name:"");foreach(var r in t.GetComponents<Renderer>())foreach(var m in r.sharedMaterials)fingerprint.Append(AssetDatabase.GetAssetPath(m));}
        string path=directory+"/"+Hash128.Compute(fingerprint.ToString())+".prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path))return path;
        var preview=EditorSceneManager.NewPreviewScene();GameObject clone=null;
        try
        {
            clone=Object.Instantiate(source);SceneManager.MoveGameObjectToScene(clone,preview);clone.name=source.name;clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);clone.transform.localScale=Vector3.one;
            if(function)
            {
                foreach(var r in clone.GetComponentsInChildren<Renderer>(true))Object.DestroyImmediate(r);
                foreach(var p in clone.GetComponentsInChildren<ParticleSystem>(true))Object.DestroyImmediate(p);
                foreach(var c in clone.GetComponentsInChildren<Collider>(true))if(!c.isTrigger)Object.DestroyImmediate(c);
                foreach(var filter in clone.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(filter);
                clone.SetActive(false);
            }
            else
            {
                foreach(var script in clone.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
                foreach(var body in clone.GetComponentsInChildren<Rigidbody>(true))Object.DestroyImmediate(body);
                foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))renderer.enabled=true;
                foreach(var collider in clone.GetComponentsInChildren<Collider>(true))if(!collider.isTrigger)collider.enabled=true;else Object.DestroyImmediate(collider);
                BakeBoxColliders(clone);
            }
            PrefabUtility.SaveAsPrefabAsset(clone,path);return path;
        }
        finally{if(clone)Object.DestroyImmediate(clone);EditorSceneManager.ClosePreviewScene(preview);}
    }
    static IEnumerable<WorldFeatureReference> References(GameObject source,Dictionary<GameObject,string> keys,List<WorldSceneBinding> bindings)
    {
        foreach(var component in source.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(!component)continue;var serialized=new SerializedObject(component);var property=serialized.GetIterator();
            while(property.Next(true))
            {
                if(property.propertyType!=SerializedPropertyType.ObjectReference||property.propertyPath=="m_Script")continue;var target=property.objectReferenceValue;if(!target||EditorUtility.IsPersistent(target))continue;
                var targetGO=target as GameObject??(target as Component)?.gameObject;if(!targetGO||targetGO.transform.IsChildOf(source.transform))continue;
                // Target anchors remain direct references in existing scenes. Exported standalone maps
                // require an equivalent scene binding, reported by the shared consistency checker.
                string key=keys.TryGetValue(targetGO,out var value)?value:Key(targetGO);bindings.Add(new WorldSceneBinding{key=key,target=targetGO});
                string targetType=target.GetType().FullName;int targetIndex=target is Component tc?Array.IndexOf(targetGO.GetComponents(tc.GetType()),tc):0;
                yield return new WorldFeatureReference{ownerPath=AnimationUtility.CalculateTransformPath(component.transform,source.transform),ownerType=component.GetType().FullName,ownerIndex=Array.IndexOf(component.GetComponents(component.GetType()),component),propertyPath=property.propertyPath,targetKey=key,targetType=targetType,targetIndex=targetIndex,targetPath=""};
            }
        }
    }

    static Terrain SourceTerrain(Scene scene)=>scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Terrain>(true)).FirstOrDefault();
    static float OriginalGroundHeight(Scene scene,float x,float z){var terrain=SourceTerrain(scene);return terrain?terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y:0;}
    static WorldMapData BuildGround(Scene scene,string label,Transform[] transforms,out List<GameObject> grounds,out List<GameObject> waters)
    {
        bool home=scene.name=="3C_Testbed",sect=scene.name=="Sect",tower=scene.name=="Demon-Suppressing Tower",flat=scene.name=="Sect_Wilderness";
        grounds=new List<GameObject>();waters=new List<GameObject>();var terrain=SourceTerrain(scene);
        if(terrain)grounds.Add(terrain.gameObject);
        else if(home)grounds.AddRange(transforms.Where(x=>x.GetComponent<HomeSurface>()).Select(x=>x.gameObject));
        else if(tower)grounds.AddRange(transforms.Where(x=>x.name=="地面板").Select(x=>x.gameObject));
        else if(sect)
        {
            var rounded=transforms.FirstOrDefault(x=>x.name=="宗门圆角地面");if(rounded)grounds.Add(rounded.gameObject);
            else grounds.AddRange(transforms.Where(x=>x.name.StartsWith("better ground")).Select(x=>x.gameObject));
        }
        if(grounds.Count==0)throw new InvalidOperationException(label+"未找到可采样地面。");
        var colliders=grounds.SelectMany(x=>x.GetComponentsInChildren<Collider>(true)).Where(x=>!x.isTrigger&&!(x.GetComponentInParent<HomeBoundary>())).ToArray();
        int width=home?128:512;var data=WorldMapData.NewWorkspace(label,width);data.resolution=home?257:513;data.cellSize=home?.5f:1;
        int count=data.resolution*data.resolution;data.land=new float[count];data.height=new float[count];data.grass=new float[count];data.water=new float[count];data.waterHeight=new float[count];data.dryHeight=new float[count];data.surfaceIndex=new float[count];data.surfaceBlend=new float[count];data.floor=-8;data.cloudSea=!tower;data.rockBoundary=!tower;data.authoringVersion=3;data.grassDensity=.45f;
        float[,,] alpha=terrain?terrain.terrainData.GetAlphamaps(0,0,terrain.terrainData.alphamapWidth,terrain.terrainData.alphamapHeight):null;
        for(int z=0;z<data.resolution;z++)for(int x=0;x<data.resolution;x++)
        {
            int i=data.Index(x,z);float px=x*data.cellSize-data.Width*.5f,pz=z*data.cellSize-data.Width*.5f,height=float.MinValue;
            if(terrain)
            {
                var p=terrain.transform.position;var size=terrain.terrainData.size;float ux=(px-p.x)/size.x,uz=(pz-p.z)/size.z;
                float edge=Mathf.Min(px-p.x,p.x+size.x-px,pz-p.z,p.z+size.z-pz);if(edge<2)continue;
                // Round the formerly rectangular Terrain perimeter without altering interior placements.
                float qx=Mathf.Max(Mathf.Abs(px-p.x-size.x*.5f)-(size.x*.5f-8),0),qz=Mathf.Max(Mathf.Abs(pz-p.z-size.z*.5f)-(size.z*.5f-8),0);if(qx*qx+qz*qz>36)continue;
                height=flat?0:terrain.terrainData.GetInterpolatedHeight(ux,uz)+p.y;
                int ax=Mathf.Clamp(Mathf.RoundToInt(ux*(alpha.GetLength(1)-1)),0,alpha.GetLength(1)-1),az=Mathf.Clamp(Mathf.RoundToInt(uz*(alpha.GetLength(0)-1)),0,alpha.GetLength(0)-1);
                float green=0;for(int layer=0;layer<alpha.GetLength(2);layer++){string name=terrain.terrainData.terrainLayers[layer]?terrain.terrainData.terrainLayers[layer].name.ToLowerInvariant():"";if(name.Contains("grass")||name.Contains("草"))green+=alpha[az,ax,layer];}data.grass[i]=Mathf.Clamp01((green-.45f)*1.8f);
            }
            else
            {
                var ray=new Ray(new Vector3(px,90,pz),Vector3.down);foreach(var c in colliders)if(c.Raycast(ray,out var hit,300)&&hit.normal.y>.55f)height=Mathf.Max(height,hit.point.y);
            }
            if(height==float.MinValue)continue;data.land[i]=1;data.height[i]=data.dryHeight[i]=height;
        }
        if(sect){FillSectFoundation(data);data.bottomFogOnly=true;data.fogBaseHeight=-.7f;}
        foreach(var t in transforms)if(t.name=="河流"||t.name=="支流")if(t.GetComponent<MeshFilter>()){waters.Add(t.gameObject);RasterWater(t.GetComponent<MeshFilter>(),data,flat);}
        if(flat)data.SmoothRiverBanks();
        float minimum=data.height.Where((h,i)=>data.land[i]>.5f).DefaultIfEmpty(0).Min();data.floor=Mathf.Min(-8,minimum-5);for(int i=0;i<count;i++)if(data.land[i]<.5f)data.height[i]=data.dryHeight[i]=Mathf.Max(0,data.floor+.3f);
        return data;
    }
    static void RasterWater(MeshFilter filter,WorldMapData data,bool flat)
    {
        var mesh=filter.sharedMesh;if(!mesh)return;var snapshot=new Mesh();
        try
        {
            using(var source=MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var md=source[0];var positions=new Unity.Collections.NativeArray<Vector3>(md.vertexCount,Unity.Collections.Allocator.Temp);md.GetVertices(positions);var points=positions.ToArray();positions.Dispose();
                for(int i=0;i<points.Length;i++)points[i]=filter.transform.TransformPoint(points[i]);
                for(int sub=0;sub<md.subMeshCount;sub++)
                {
                    var desc=md.GetSubMesh(sub);if(desc.topology!=MeshTopology.Triangles)continue;var indices=new Unity.Collections.NativeArray<int>(desc.indexCount,Unity.Collections.Allocator.Temp);md.GetIndices(indices,sub);
                    for(int j=0;j+2<indices.Length;j+=3)
                    {
                        Vector3 a=points[indices[j]],b=points[indices[j+1]],c=points[indices[j+2]];float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<1e-6f)continue;
                        int x0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x,b.x,c.x)+data.Width*.5f)/data.cellSize),1,data.resolution-2),x1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x,b.x,c.x)+data.Width*.5f)/data.cellSize),1,data.resolution-2),z0=Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.z,b.z,c.z)+data.Width*.5f)/data.cellSize),1,data.resolution-2),z1=Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.z,b.z,c.z)+data.Width*.5f)/data.cellSize),1,data.resolution-2);
                        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){var p=data.Position(x,z);float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.z-c.z))/det,v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.z-c.z))/det;if(u<0||v<0||u+v>1)continue;int k=data.Index(x,z);if(data.land[k]<.5f)continue;float level=flat?-.18f:u*a.y+v*b.y+(1-u-v)*c.y;if(data.height[k]>level+.65f&&!flat)continue;data.water[k]=1;data.waterHeight[k]=level;data.height[k]=Mathf.Min(data.height[k],level-1.2f);data.grass[k]=0;}
                    }
                    indices.Dispose();
                }
            }
        }
        finally{Object.DestroyImmediate(snapshot);}
    }
    static void FillSectFoundation(WorldMapData data)
    {
        // Fill gaps between the original platforms into one low foundation. Raised areas keep
        // their sampled top heights; the original stair modules remain the vertical routes.
        for(int z=1;z<data.resolution-1;z++)
        {
            int first=-1,last=-1;for(int x=1;x<data.resolution-1;x++)if(data.land[data.Index(x,z)]>.5f){if(first<0)first=x;last=x;}
            if(first<0)continue;for(int x=first;x<=last;x++){int i=data.Index(x,z);if(data.land[i]<.5f){data.land[i]=1;data.height[i]=data.dryHeight[i]=-.7f;}}
        }
    }
    public static void UpdateSectFoundation()
    {
        var map=AssetDatabase.LoadAssetAtPath<WorldMapAsset>(Root+"/Sect/Map.asset");var data=JsonUtility.FromJson<WorldMapData>(map.sourceJson);FillSectFoundation(data);map.sourceJson=JsonUtility.ToJson(data);EditorUtility.SetDirty(map);AssetDatabase.SaveAssetIfDirty(map);WorldMapStorage.Save(data);
    }
    public static void UpdateModuleMarkers()
    {
        var palette=AssetDatabase.LoadAssetAtPath<WorldMapPalette>(WorldMapEditorBuilder.PalettePath);palette.builtinMistShader=Shader.Find("Cultivation/WorldAuthoring/ShoreMistBuiltin");palette.inkMaskShader=Shader.Find("Cultivation/WorldAuthoring/InkMask");palette.inkCompositeShader=Shader.Find("Cultivation/OutlineComposite");
        string path=Root+"/FunctionMarker.prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(!prefab){var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="功能挂点";marker.GetComponent<Renderer>().sharedMaterial=palette.cliffMaterial;PrefabUtility.SaveAsPrefabAsset(marker,path);Object.DestroyImmediate(marker);prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);}
        var entries=palette.entries.ToList();WorldMapEditorBuilder.Add(entries,path,"功能","功能挂点");var entry=entries.First(x=>x.id==AssetDatabase.AssetPathToGUID(path));entry.editorOnly=true;entry.preservePivot=true;entry.sourcePack="功能模块";palette.entries=entries.ToArray();
        var prior=SceneManager.GetActiveScene();
        foreach(var name in Scenes)
        {
            var scenePath="Assets/Scenes/"+name+".scene";var scene=SceneManager.GetSceneByPath(scenePath);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                var loader=scene.GetRootGameObjects().Select(x=>x.GetComponent<WorldMapLoader>()).First(x=>x);var data=JsonUtility.FromJson<WorldMapData>(loader.map.sourceJson);
                var trees=GreenTrees(palette.entries);foreach(var item in data.objects.Where(x=>!x.exactTransform)){int previous=palette.Find(item.paletteId);if(previous<0||palette.entries[previous].category!="树木"||trees.Length==0)continue;float height=palette.entries[previous].bounds.size.y*item.scale;var tree=trees[(int)(Math.Abs((long)Hash128.Compute(item.instanceId).GetHashCode())%trees.Length)];item.paletteId=tree.id;item.scale=height/Mathf.Max(.1f,tree.bounds.size.y);}
                foreach(var binding in loader.bindings)if(data.features.Exists(x=>x.id==binding.key))binding.managedFeature=true;
                foreach(var feature in data.features){var template=palette.Feature(feature.templateId);if(!template)continue;template.role=feature.label=="environment_Building_luoxiaguan_001_d"?WorldFeatureRole.MeritExchange:feature.label=="environment_Building_luoxiaguan_001_b"?WorldFeatureRole.TechniqueExchange:feature.label=="environment_Building_diaojiaolou_001_h"?WorldFeatureRole.Commission:WorldFeatureRole.Generic;EditorUtility.SetDirty(template);AssetDatabase.SaveAssetIfDirty(template);}
                foreach(var feature in data.features){var template=palette.Feature(feature.templateId);if(template&&template.kind.Contains("仅编辑器可见")&&!string.IsNullOrEmpty(feature.objectId)){var item=data.objects.Find(x=>x.instanceId==feature.objectId);if(item!=null&&palette.Find(item.paletteId)>=0)palette.entries[palette.Find(item.paletteId)].editorOnly=true;}}
                foreach(var feature in data.features)if(string.IsNullOrEmpty(feature.objectId)){feature.objectId=feature.id;data.objects.Add(new MapObjectData{instanceId=feature.id,paletteId=entry.id,featureId=feature.id,position=feature.position,rotation=feature.rotation,yaw=feature.rotation.y,exactTransform=true,axisScale=feature.scale,sizingVersion=1,scale=1});}
                if(name=="Sect"){var player=scene.GetRootGameObjects().FirstOrDefault(x=>x.GetComponent<PlayerController>());if(player)data.spawn=player.transform.position;}
                loader.map.sourceJson=JsonUtility.ToJson(data);EditorUtility.SetDirty(loader.map);AssetDatabase.SaveAssetIfDirty(loader.map);WorldMapStorage.Save(data);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        SceneManager.SetActiveScene(prior);EditorUtility.SetDirty(palette);AssetDatabase.SaveAssetIfDirty(palette);WorldAuthoringResources.Inventory(palette);
    }
    static void BakeBoxColliders(GameObject root)
    {
        string folder=Root+"/Collision";Directory.CreateDirectory(folder);
        foreach(var box in root.GetComponentsInChildren<BoxCollider>(true))
        {
            if(box.isTrigger)continue;string id=Hash128.Compute(box.center.ToString("F6")+box.size.ToString("F6")).ToString();string path=folder+"/Box_"+id+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);mesh=Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);Object.DestroyImmediate(primitive);mesh.name="Box_"+id;var vertices=mesh.vertices;for(int i=0;i<vertices.Length;i++)vertices[i]=box.center+Vector3.Scale(vertices[i],box.size);mesh.vertices=vertices;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            var collider=box.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.sharedMaterial=box.sharedMaterial;collider.contactOffset=box.contactOffset;collider.enabled=box.enabled;Object.DestroyImmediate(box);
        }
    }
    public static void RepairVisualColliders()
    {
        int count=0;foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.Contains("/Visuals/"))continue;var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset.GetComponentsInChildren<BoxCollider>(true).Length==0)continue;
            var contents=PrefabUtility.LoadPrefabContents(path);try{BakeBoxColliders(contents);PrefabUtility.SaveAsPrefabAsset(contents,path);count++;}finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        Debug.Log("已烘焙 "+count+" 个原场景外观预制体的盒形碰撞，支持镜像与非等比缩放。");
    }
    public static void RetireLegacyVisuals()
    {
        var prior=SceneManager.GetActiveScene();int trees=0,renderers=0;
        foreach(var name in Scenes.Where(x=>x!="Demon-Suppressing Tower"))
        {
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/"+name+".scene");bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene("Assets/Scenes/"+name+".scene",OpenSceneMode.Additive);
            try
            {
                var loader=scene.GetRootGameObjects().Select(x=>x.GetComponent<WorldMapLoader>()).First(x=>x);var data=JsonUtility.FromJson<WorldMapData>(loader.map.sourceJson);var lookup=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToDictionary(x=>Key(x.gameObject),x=>x.gameObject);
                var referenced=new HashSet<Object>();foreach(var root in scene.GetRootGameObjects())foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true)){if(!script)continue;var serialized=new SerializedObject(script);var property=serialized.GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue)referenced.Add(property.objectReferenceValue);}
                foreach(var item in data.objects)
                {
                    if(!lookup.TryGetValue(item.instanceId,out var go)||!go)continue;
                    if(IsTree(go)&&!loader.bindings.Any(x=>x.target&&(x.target==go||x.target.transform.IsChildOf(go.transform)))&&!referenced.Any(x=>x==go||(x is Component c&&c.transform.IsChildOf(go.transform))||(x is GameObject g&&g.transform.IsChildOf(go.transform)))){Object.DestroyImmediate(go);trees++;continue;}
                    if(PrefabUtility.IsPartOfPrefabInstance(go)){var outer=PrefabUtility.GetOutermostPrefabInstanceRoot(go);if(outer==go)PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);}
                    foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))if(!renderer.enabled&&!referenced.Contains(renderer)){Object.DestroyImmediate(renderer);renderers++;}
                    foreach(var collider in go.GetComponentsInChildren<Collider>(true))if(!collider.enabled&&!referenced.Contains(collider))Object.DestroyImmediate(collider);
                    foreach(var filter in go.GetComponentsInChildren<MeshFilter>(true))if(!filter.GetComponent<Renderer>()&&!referenced.Contains(filter))Object.DestroyImmediate(filter);
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        SceneManager.SetActiveScene(prior);Debug.Log("旧外观退役：删除 "+trees+" 个已替换树实例，移除 "+renderers+" 个停用渲染组件；保留功能、名称与引用锚点。");
    }
}
