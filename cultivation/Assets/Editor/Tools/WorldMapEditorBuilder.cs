using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cultivation.WorldAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class WorldMapEditorBuilder
{
    public const string Root="Assets/WorldAuthoring";
    public const string ScenePath=Root+"/RuntimeMapEditor.unity";
    public const string PalettePath=Root+"/WorldPalette.asset";

    [MenuItem("修仙/场景工坊/打开地图编辑器")]
    public static void Open()
    {
        if(EditorApplication.isPlaying){Debug.LogWarning("先停止运行，再打开独立地图编辑器。");return;}
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))Build();
        EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("已打开独立地图编辑器。点击运行进入工坊；3C_Testbed 仍用于游戏测试。");
    }

    [MenuItem("修仙/场景工坊/生成或更新独立场景编辑器")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("请先停止运行，再生成编辑器场景。");
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Meshes");Directory.CreateDirectory(Root+"/PaletteIcons");AssetDatabase.Refresh();
        var palette=AssetDatabase.LoadAssetAtPath<WorldMapPalette>(PalettePath);
        if(!palette){palette=ScriptableObject.CreateInstance<WorldMapPalette>();AssetDatabase.CreateAsset(palette,PalettePath);}
        var sceneEntries=palette.entries.Where(x=>x.preservePivot||x.editorOnly).ToArray();
        WorldAuthoringResources.PlayerReference(palette);
        var entries=new List<WorldPaletteEntry>();
        for(int i=1;i<=11;i++)Add(entries,"Assets/Rocks/Prefabs/rock_"+i+".prefab","岩石", "岩石 "+i);
        foreach(var name in new[]{"GrassThin","GrassTall","GrassThick","Shrub"})Add(entries,"Assets/Stylized Grass Shader/Prefabs/"+name+".prefab","草花",name=="Shrub"?"灌木":name=="GrassTall"?"长草":name=="GrassThick"?"密草":"细草");
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Stylized Grass Shader/Prefabs/Flowers"}).Take(8))Add(entries,AssetDatabase.GUIDToAssetPath(guid),"草花",Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)).Replace("Groundcover_","").Replace("Flower_",""));
        Add(entries,"Assets/StylizedWater/Demo/DemoAssets/Prefabs/Bridge.prefab","桥道","木桥");
        foreach(var name in new[]{"SWS_DemoPier","SWS_LogRaft","SWS_WoodenCrate","SWS_Barrel"})Add(entries,"Assets/StylizedWater2/_Demo/DemoAssets/Prefabs/"+name+".prefab",name=="SWS_DemoPier"||name=="SWS_LogRaft"?"桥道":"建筑",name.Replace("SWS_",""));
        foreach(var path in Prefabs("Assets/Stylized Far East/Prefabs/Buildings"))Add(entries,path,"建筑",AssetLabel(path));
        foreach(var path in Prefabs("Assets/Stylized Far East/Prefabs/Nature"))
        {
            var name=Path.GetFileNameWithoutExtension(path);
            if(name.StartsWith("SM_tree_"))Add(entries,path,"树木",AssetLabel(path));
            else if(name.StartsWith("SM_cliff_"))Add(entries,path,"岩石",AssetLabel(path));
        }
        foreach(var path in Prefabs("Assets/Stylized Forest/Assets/Prefabs/Trees and bushes"))
            if(!path.Contains("Winter")&&!path.Contains("Autumn"))Add(entries,path,Path.GetFileName(path).StartsWith("Bush")?"草花":"树木",AssetLabel(path));
        foreach(var name in new[]{"SM_fence_bamboo","SM_fence_temple_preset","SM_lantern_paper_01","SM_marketstand_01","SM_platform_stairs_wooden","SM_road_preset_01","SM_bench_wooden","SM_cart_empty"})
            Add(entries,"Assets/Stylized Far East/Prefabs/Props/"+name+".prefab","摆件",AssetLabel(name));
        foreach(var path in WorldAuthoringResources.CompleteAssets())Add(entries,path,WorldAuthoringResources.Category(path),AssetLabel(path));
        foreach(var entry in sceneEntries)if(!entries.Any(x=>x.id==entry.id))entries.Add(entry);
        palette.entries=entries.ToArray();palette.boundaryRocks=new[]{0,1,2,3,4,5,6};
        WorldAuthoringResources.GroundLayers(palette);
        WorldAuthoringResources.Inventory(palette);
        palette.groundMaterial=Material("PaleGround","Cultivation/WorldAuthoring/Surface");palette.groundMaterial.SetColor("_BaseColor",new Color(.73f,.73f,.69f));palette.groundMaterial.SetColor("_GrassColor",new Color(.24f,.38f,.19f));palette.groundMaterial.SetFloat("_GrassBlend",1);palette.groundMaterial.SetFloat("_NoiseStrength",.025f);
        palette.groundMaterial.SetTexture("_BaseMap",Load<Texture2D>("Assets/StylizedWater2/_Demo/DemoAssets/Terrain/SWS_DemoSand.png"));
        palette.groundMaterial.SetTextureScale("_BaseMap",new Vector2(.90f,.90f));palette.groundMaterial.SetFloat("_TextureStrength",.58f);palette.groundMaterial.SetFloat("_TextureContrast",.55f);palette.groundMaterial.SetFloat("_TextureSaturation",.10f);palette.groundMaterial.SetFloat("_TextureExposure",1.85f);
        palette.groundMaterial.SetColor("_GrassColor",new Color(.29f,.35f,.25f));palette.groundMaterial.SetFloat("_BoundaryBlend",1);
        palette.groundMaterial.SetTexture("_GrassMap",Load<Texture2D>("Assets/Stylized Forest/Assets/Textures/Terrain Textures/Ground_1.jpg"));
        palette.groundMaterial.SetTextureScale("_GrassMap",new Vector2(.22f,.22f));palette.groundMaterial.SetColor("_GrassColor",new Color(.70f,.78f,.65f));palette.groundMaterial.SetFloat("_GrassMapExposure",1.7f);palette.groundMaterial.SetFloat("_GrassMapSaturation",.35f);
        palette.cliffMaterial=Material("CliffFoundation","Cultivation/WorldAuthoring/Surface");palette.cliffMaterial.SetColor("_BaseColor",new Color(.59f,.60f,.55f));palette.cliffMaterial.SetFloat("_TextureStrength",0);palette.cliffMaterial.SetFloat("_NoiseStrength",.10f);palette.cliffMaterial.SetFloat("_Cull",0);
        var grassSource=Load<Material>("Assets/Stylized Grass Shader/Materials/GrassTall.mat");
        palette.grassMesh=WorldGrassMeshBuilder.Build(Root+"/Meshes/SolidGrass.asset");
        palette.grassURP=Material("GrassURP",grassSource.shader.name);palette.grassURP.CopyPropertiesFromMaterial(grassSource);palette.grassURP.shaderKeywords=grassSource.shaderKeywords;
        palette.grassURP.SetTexture("_BaseMap",Texture2D.whiteTexture);palette.grassURP.SetFloat("_Cutoff",.1f);palette.grassURP.SetFloat("_AlphaToCoverage",0);palette.grassURP.SetFloat("_ColorMapStrength",0);
        palette.grassURP.SetColor("_BaseColor",new Color(.34f,.49f,.28f));palette.grassURP.SetColor("_HueVariation",new Color(.39f,.50f,.27f,.12f));palette.grassURP.SetFloat("_OcclusionStrength",.64f);
        palette.grassURP.SetFloat("_Translucency",.045f);palette.grassURP.SetFloat("_TranslucencyIndirect",.02f);palette.grassURP.SetFloat("_WindAmbientStrength",.10f);palette.grassURP.SetFloat("_WindGustStrength",.045f);palette.grassURP.SetFloat("_WindSpeed",.9f);palette.grassURP.SetFloat("_WindRandStrength",.25f);palette.grassURP.SetVector("_FadeParams",new Vector4(60,100,0,0));palette.grassURP.SetVector("_NormalParams",new Vector4(.25f,1,0,0));palette.grassURP.enableInstancing=true;
        palette.grassBuiltin=Material("GrassBuiltin","Cultivation/WorldAuthoring/Surface");palette.grassBuiltin.SetTexture("_BaseMap",Texture2D.whiteTexture);palette.grassBuiltin.SetColor("_BaseColor",new Color(.34f,.49f,.28f));palette.grassBuiltin.SetFloat("_Cutoff",0);palette.grassBuiltin.SetFloat("_Cull",2);palette.grassBuiltin.SetFloat("_WindStrength",.06f);palette.grassBuiltin.SetFloat("_NoiseStrength",0);palette.grassBuiltin.enableInstancing=true;
        palette.waterURP=Material("WaterURP","Universal Render Pipeline/FX/Stylized Water 2");palette.waterURP.CopyPropertiesFromMaterial(Load<Material>("Assets/StylizedWater2/Materials/StylizedWater2_Desktop.mat"));palette.waterURP.SetColor("_BaseColor",new Color(.04f,.26f,.29f,1));palette.waterURP.SetColor("_ShallowColor",new Color(.29f,.56f,.50f,.28f));palette.waterURP.SetFloat("_NormalStrength",.15f);palette.waterURP.SetFloat("_SunReflectionStrength",.8f);palette.waterURP.SetFloat("_FoamSize",.80f);palette.waterURP.SetFloat("_CausticsBrightness",.4f);palette.waterURP.SetFloat("_IntersectionLength",.45f);palette.waterURP.SetFloat("_WavesOn",0);palette.waterURP.DisableKeyword("_WAVES");
        palette.waterBuiltin=Material("WaterBuiltin","StylizedWater/Desktop");palette.waterBuiltin.CopyPropertiesFromMaterial(Load<Material>("Assets/StylizedWater/Materials/StylizedWater_Desktop.mat"));palette.waterBuiltin.SetFloat("_Wavesstrength",.015f);palette.waterBuiltin.SetColor("_WaterColor",new Color(.08f,.39f,.40f));palette.waterBuiltin.SetFloat("_Glossiness",.25f);palette.waterBuiltin.SetFloat("_Worldspacetiling",1);
        palette.waterURP.SetFloat("_DisableDepthTexture",0);palette.waterURP.DisableKeyword("_DISABLE_DEPTH_TEX");palette.waterURP.SetFloat("_EdgeFade",.52f);palette.waterURP.SetFloat("_Depth",1.8f);palette.waterURP.SetFloat("_IntersectionLength",.85f);palette.waterURP.SetFloat("_IntersectionStyle",2);palette.waterURP.DisableKeyword("_SHARP_INERSECTION");palette.waterURP.EnableKeyword("_SMOOTH_INTERSECTION");palette.waterURP.SetFloat("_IntersectionFalloff",.70f);
        // This version expresses shore fade as hundredths of a world unit.
        palette.waterURP.SetFloat("_ZWrite",0);palette.waterURP.SetFloat("_EdgeFade",45);palette.waterURP.SetFloat("_IntersectionLength",1.8f);palette.waterURP.SetColor("_IntersectionColor",new Color(.78f,.86f,.78f,.25f));
        palette.cloudMesh=null;palette.cloudMaterial=null;
        palette.fogNoise=Load<Texture2D>("Assets/Stylized Grass Shader/Materials/Textures/GustNoise.png");
        palette.fogVolumeNoise=FogNoise();
        palette.skybox=Material("Sky","Skybox/Procedural");palette.skybox.SetColor("_SkyTint",new Color(.60f,.72f,.81f));palette.skybox.SetColor("_GroundColor",new Color(.52f,.63f,.68f));palette.skybox.SetFloat("_Exposure",1.1f);palette.skybox.SetFloat("_AtmosphereThickness",.8f);
        palette.editorPipeline=Pipeline();
        palette.grading=Grading();
        EditorUtility.SetDirty(palette);AssetDatabase.SaveAssetIfDirty(palette);
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
        if(SceneManager.GetSceneByPath(ScenePath).isLoaded){Debug.Log("工坊素材已完整更新："+entries.Count+" 项，地表笔刷 "+palette.groundLayers.Length+" 项。已打开的工坊场景保留。");return;}
        var previous=SceneManager.GetActiveScene();bool replaceEmpty=string.IsNullOrEmpty(previous.path)&&previous.rootCount==0&&SceneManager.sceneCount==1;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,replaceEmpty?NewSceneMode.Single:NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);Environment(palette);
            var camera=new GameObject("WorldAuthoringCamera",typeof(Camera),typeof(AudioListener),typeof(WorldInkCamera)).GetComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=48;camera.farClipPlane=220;camera.nearClipPlane=.1f;camera.allowHDR=true;camera.depthTextureMode=DepthTextureMode.Depth;camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
            var root=new GameObject("WorldAuthoring");var scope=root.AddComponent<WorldEditorPipelineScope>();scope.palette=palette;var renderer=root.AddComponent<WorldMapRenderer>();var editor=root.AddComponent<WorldMapEditorController>();editor.palette=palette;editor.editorCamera=camera;editor.world=renderer;
            EditorSceneManager.SaveScene(scene,ScenePath);Debug.Log("独立场景编辑器已生成："+ScenePath+"，素材 "+entries.Count+" 项。打开后运行即可编辑；地图保存到 maps/source。");
        }
        finally {if(!replaceEmpty){SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}}
    }

    static UniversalRenderPipelineAsset Pipeline()
    {
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root+"/WorldRenderer.asset");
        if(!renderer)
        {
            renderer=ScriptableObject.CreateInstance<UniversalRendererData>();ResourceReloader.ReloadAllNullIn(renderer,UniversalRenderPipelineAsset.packagePath);AssetDatabase.CreateAsset(renderer,Root+"/WorldRenderer.asset");
            var feature=ScriptableObject.CreateInstance<WorldInkFeature>();feature.name="Environment ink contours";feature.maskShader=Shader.Find("Cultivation/OutlineMask");feature.compositeShader=Shader.Find("Cultivation/OutlineComposite");AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssetIfDirty(renderer);
        }
        if(!renderer.rendererFeatures.OfType<WorldMistFeature>().Any())
        {
            var mist=ScriptableObject.CreateInstance<WorldMistFeature>();mist.name="Shore mist volume";mist.shader=Shader.Find("Cultivation/WorldAuthoring/ShoreMist");AssetDatabase.AddObjectToAsset(mist,renderer);renderer.rendererFeatures.Add(mist);EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssetIfDirty(renderer);
        }
        foreach(var ink in renderer.rendererFeatures.OfType<WorldInkFeature>()){ink.maskShader=Shader.Find("Cultivation/WorldAuthoring/InkMask");EditorUtility.SetDirty(ink);AssetDatabase.SaveAssetIfDirty(ink);}
        renderer.copyDepthMode=CopyDepthMode.AfterOpaques;renderer.SetDirty();EditorUtility.SetDirty(renderer);AssetDatabase.SaveAssetIfDirty(renderer);
        var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/WorldURP.asset");
        if(!pipeline){pipeline=UniversalRenderPipelineAsset.Create(renderer);pipeline.supportsCameraDepthTexture=true;pipeline.supportsCameraOpaqueTexture=true;pipeline.msaaSampleCount=1;pipeline.supportsHDR=true;pipeline.shadowDistance=70;AssetDatabase.CreateAsset(pipeline,Root+"/WorldURP.asset");}
        return pipeline;
    }
    static VolumeProfile Grading()
    {
        var path=Root+"/WorldGrade.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
        if(!profile.TryGet<ColorAdjustments>(out var colour)){colour=profile.Add<ColorAdjustments>(true);AssetDatabase.AddObjectToAsset(colour,profile);}
        colour.postExposure.Override(.08f);colour.contrast.Override(10);colour.saturation.Override(6);colour.colorFilter.Override(new Color(.98f,1,1));
        if(!profile.TryGet<WhiteBalance>(out var white)){white=profile.Add<WhiteBalance>(true);AssetDatabase.AddObjectToAsset(white,profile);}white.temperature.Override(-3);white.tint.Override(-1);
        if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.Neutral);
        EditorUtility.SetDirty(profile);foreach(var component in profile.components){EditorUtility.SetDirty(component);AssetDatabase.SaveAssetIfDirty(component);}AssetDatabase.SaveAssetIfDirty(profile);return profile;
    }
    static Texture3D FogNoise()
    {
        var path=Root+"/Meshes/MistNoise.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture3D>(path);if(texture)return texture;
        const int size=32;var colours=new Color[size*size*size];
        for(int z=0;z<size;z++)for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float a=x*Mathf.PI*2/size,b=y*Mathf.PI*2/size,c=z*Mathf.PI*2/size;
            float n=Mathf.PerlinNoise(4+Mathf.Cos(a)*1.7f+Mathf.Cos(b),5+Mathf.Sin(c)*1.7f+Mathf.Sin(b));
            float m=Mathf.PerlinNoise(9+Mathf.Sin(a)*2.1f+Mathf.Sin(c),7+Mathf.Cos(b)*2.1f+Mathf.Cos(c));
            colours[(z*size+y)*size+x]=new Color(n*.65f+m*.35f,0,0,1);
        }
        texture=new Texture3D(size,size,size,TextureFormat.R8,false){name="Shore mist density",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};texture.SetPixels(colours);texture.Apply();AssetDatabase.CreateAsset(texture,path);return texture;
    }
    public static void Environment(WorldMapPalette palette)
    {
        RenderSettings.skybox=palette.skybox;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.40f,.48f,.57f);RenderSettings.ambientEquatorColor=new Color(.34f,.39f,.41f);RenderSettings.ambientGroundColor=new Color(.23f,.27f,.25f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.65f,.70f,.70f);RenderSettings.fogStartDistance=75;RenderSettings.fogEndDistance=180;
        var sun=new GameObject("WorldSun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(48,-28,0);sun.intensity=1.10f;sun.color=new Color(1,.97f,.90f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.82f;RenderSettings.sun=sun;
        if(palette.grading){var grade=new GameObject("WorldGrade").AddComponent<Volume>();grade.isGlobal=true;grade.sharedProfile=palette.grading;grade.priority=5;}
    }
    public static Material Material(string name,string shaderName)
    {
        var path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader=Shader.Find(shaderName);if(!shader)throw new InvalidOperationException("缺少 Shader："+shaderName);
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else if(material.shader!=shader)material.shader=shader;
        EditorUtility.SetDirty(material);return material;
    }
    static IEnumerable<string> Prefabs(string directory)
    {
        if(!AssetDatabase.IsValidFolder(directory))return Array.Empty<string>();
        return AssetDatabase.FindAssets("t:Prefab",new[]{directory}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!Path.GetFileName(p).Contains("(")&&!Path.GetFileNameWithoutExtension(p).EndsWith(" 1")).OrderBy(p=>p,StringComparer.Ordinal);
    }
    static string AssetLabel(string path)
    {
        var label=Path.GetFileNameWithoutExtension(path).Replace("SM_","");
        foreach(var pair in new[]{new[]{"building_residental","民居"},new[]{"tree_large_fareast","大型庭树"},new[]{"tree_fareast","庭树"},new[]{"tree_standard","乔木"},new[]{"Broadleaf","阔叶树"},new[]{"PineTree","松树"},new[]{"FirTree","冷杉"},new[]{"DryTree","枯树"},new[]{"Bush","灌木"},new[]{"cliff","山石"},new[]{"temple","寺庙"},new[]{"pagoda","宝塔"},new[]{"shrine","神社"},new[]{"watchtower","望楼"},new[]{"tower","塔楼"},new[]{"gatehouse","门楼"},new[]{"wall_city","城墙"},new[]{"votive","祈愿架"}})label=label.Replace(pair[0],pair[1]);
        return label.Replace('_',' ');
    }
    public static void Add(List<WorldPaletteEntry> entries,string path,string category,string label,bool refresh=false)
    {
        string id=AssetDatabase.AssetPathToGUID(path);int existing=entries.FindIndex(x=>x.id==id);if(string.IsNullOrEmpty(id)||(existing>=0&&!refresh))return;
        if(path.Contains("Stylized Far East")||path.Contains("Stylized Forest"))category=WorldAuthoringResources.Category(path);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab)return;
        var module=WorldWallModuleBuilder.Prepare(path,prefab);if(module)prefab=module.prefab;
        var renderers=prefab.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
        var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        var converted=new List<Material>();int slot=0;
        foreach(var renderer in renderers)foreach(var original in renderer.sharedMaterials)
        {
            if(!original){converted.Add(null);continue;}
            var mat=Material("Palette_"+id+"_"+slot++,"Cultivation/WorldAuthoring/Surface");
            ConfigureSurfaceMaterial(mat,original,category,path);
            converted.Add(mat);
        }
        var iconPath=Root+"/PaletteIcons/"+AssetDatabase.AssetPathToGUID(path)+"-v2.png";
        Texture2D icon=AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        if(!icon||refresh)
        {
            var mini=Thumbnail(prefab,converted.ToArray(),bounds);
            if(mini){File.WriteAllBytes(iconPath,mini.EncodeToPNG());UnityEngine.Object.DestroyImmediate(mini);AssetDatabase.ImportAsset(iconPath);icon=AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);}
        }
        float boundaryYaw=0,boundarySpan=Mathf.Max(bounds.size.x,bounds.size.z);if(category=="岩石")RockAxis(prefab,bounds,out boundaryYaw,out boundarySpan);
        var entry=new WorldPaletteEntry {id=id,label=label,category=category,sourcePack=WorldAuthoringResources.Pack(path),sourcePath=path,lockScale=category=="建筑"||module,module=module,prefab=prefab,thumbnail=icon,bounds=bounds,footprint=category=="树木"?Vector2.one*Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.18f,.45f,2):new Vector2(bounds.size.x,bounds.size.z),materials=converted.ToArray(),boundaryYaw=boundaryYaw,boundarySpan=boundarySpan,legacyScale=category=="桥道"&&path.Contains("StylizedWater")?Mathf.Clamp(6/Mathf.Max(bounds.size.x,bounds.size.z),.1f,2):1,defaultScale=WorldAuthoringResources.Scale(path,category,bounds)};
        if(existing>=0)entries[existing]=entry;else entries.Add(entry);
    }
    public static void ConfigureSurfaceMaterial(Material material,Material original,string category,string sourcePath)
    {
        if(!material||!original)return;
        if(category=="树木")material.enableInstancing=true;
        Texture texture=null;string property="_BaseMap";
        foreach(var candidate in new[]{"_Albedo","_BaseMap","_MainTex","_BaseColorMap"})if(original.HasProperty(candidate)&&original.GetTexture(candidate)){property=candidate;texture=original.GetTexture(candidate);break;}
        var tint=original.HasProperty("_AlbedoColor")?original.GetColor("_AlbedoColor"):original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.HasProperty("_Color")?original.GetColor("_Color"):Color.white;
        bool rock=category=="岩石",legacy=(category=="建筑"||category=="桥道")&&(sourcePath??"").StartsWith("Assets/Art/Environment/Imported/",StringComparison.Ordinal);
        material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",legacy?Color.Lerp(tint,Color.white,.35f):rock?new Color(.65f,.69f,.70f):tint);
        material.SetTextureScale("_BaseMap",texture?original.GetTextureScale(property)*(property=="_Albedo"&&original.HasProperty("_Tiling")?original.GetFloat("_Tiling"):1):Vector2.one);material.SetTextureOffset("_BaseMap",texture?original.GetTextureOffset(property):Vector2.zero);
        material.SetFloat("_TextureStrength",texture?(rock?.72f:1):0);material.SetFloat("_TextureContrast",legacy?.68f:rock?.52f:1);material.SetFloat("_TextureSaturation",legacy?.72f:rock?.24f:category=="树木"?.72f:1);material.SetFloat("_TextureExposure",legacy?1.3f:rock?1.5f:1);material.SetFloat("_DetailSoftness",legacy?1.35f:0);material.SetFloat("_NoiseStrength",0);material.SetFloat("_RockBlend",rock?1:0);
        string kind=(original.name+" "+original.shader.name+" "+original.GetTag("RenderType",false)).ToLowerInvariant();bool cutout=category=="草花"||kind.Contains("leav")||kind.Contains("leaf")||kind.Contains("cutout");
        material.SetFloat("_Cutoff",cutout?(original.HasProperty("_Cutoff")?Mathf.Max(.2f,original.GetFloat("_Cutoff")):.4f):0);material.SetFloat("_Cull",cutout?0:2);material.SetFloat("_WindStrength",category=="草花"?.04f:0);
    }
    static void RockAxis(GameObject prefab,Bounds bounds,out float yaw,out float span)
    {
        // Use the principal horizontal direction of the actual source mesh. The
        // bounding-box fallback also makes non-readable decorative meshes safe.
        var points=new List<Vector2>();
        foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>())
        {
            if(!filter.sharedMesh || !filter.sharedMesh.isReadable)continue;
            var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            foreach(var vertex in filter.sharedMesh.vertices){var p=matrix.MultiplyPoint3x4(vertex);points.Add(new Vector2(p.x,p.z));}
        }
        if(points.Count==0){yaw=bounds.size.x>bounds.size.z?-90:0;span=Mathf.Max(bounds.size.x,bounds.size.z);return;}
        var centre=Vector2.zero;foreach(var p in points)centre+=p;centre/=points.Count;
        float xx=0,xz=0,zz=0;foreach(var p in points){var d=p-centre;xx+=d.x*d.x;xz+=d.x*d.y;zz+=d.y*d.y;}
        float angle=.5f*Mathf.Atan2(2*xz,xx-zz);var axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
        float min=float.MaxValue,max=float.MinValue;foreach(var p in points){float projection=Vector2.Dot(p,axis);min=Mathf.Min(min,projection);max=Mathf.Max(max,projection);}
        yaw=-Mathf.Atan2(axis.x,axis.y)*Mathf.Rad2Deg;span=max-min;
    }
    public static void RefreshThumbnail(WorldPaletteEntry entry)
    {
        string path=Root+"/PaletteIcons/"+entry.id+"-v2.png";var image=Thumbnail(entry.prefab,entry.materials,entry.bounds);try{File.WriteAllBytes(path,image.EncodeToPNG());AssetDatabase.ImportAsset(path);entry.thumbnail=AssetDatabase.LoadAssetAtPath<Texture2D>(path);}finally{UnityEngine.Object.DestroyImmediate(image);}
    }
    static Texture2D Thumbnail(GameObject prefab,Material[] materials,Bounds bounds)
    {
        var preview=new PreviewRenderUtility();
        try
        {
            var model=UnityEngine.Object.Instantiate(prefab);preview.AddSingleGO(model);int slot=0;
            foreach(var r in model.GetComponentsInChildren<Renderer>(true)){var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++)if(slot<materials.Length)slots[i]=materials[slot++];r.sharedMaterials=slots;}
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.10f,.13f,.15f,1);preview.camera.orthographic=true;preview.camera.orthographicSize=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))*.70f;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=1000;
            var rotation=Quaternion.Euler(25,35,0);preview.camera.transform.position=bounds.center+rotation*new Vector3(0,0,-Mathf.Max(5,bounds.size.magnitude*2));preview.camera.transform.LookAt(bounds.center);
            preview.lights[0].intensity=1.1f;preview.lights[0].transform.rotation=Quaternion.Euler(40,30,0);preview.lights[1].intensity=.6f;preview.ambientColor=new Color(.4f,.45f,.5f);
            preview.BeginStaticPreview(new Rect(0,0,96,96));preview.Render(true);return preview.EndStaticPreview();
        }
        finally {preview.Cleanup();}
    }
    static T Load<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("缺少素材："+path);
}

[InitializeOnLoad]
public static class WorldMapExportBridge
{
    static string QueueDirectory=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../maps/export-requests"));
    static WorldMapExportBridge(){WorldMapEditorController.ExportRequested-=Queue;WorldMapEditorController.ExportRequested+=Queue;EditorApplication.playModeStateChanged+=Changed;}
    static void Queue(WorldMapData data,WorldMapPalette palette)
    {
        Directory.CreateDirectory(QueueDirectory);File.WriteAllText(Path.Combine(QueueDirectory,WorldMapStorage.SafeName(data.name)+".json"),JsonUtility.ToJson(data));
    }
    static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Process;}
    [MenuItem("修仙/场景工坊/导出已保存的地图请求")]
    public static void Process()
    {
        if(EditorApplication.isPlaying || !Directory.Exists(QueueDirectory))return;
        foreach(var path in Directory.GetFiles(QueueDirectory,"*.json"))
        {
            try{var data=JsonUtility.FromJson<WorldMapData>(File.ReadAllText(path));data.Validate();Export(data);File.Move(path,path+".completed."+DateTime.UtcNow.Ticks);}
            catch(Exception exception){Debug.LogError("地图导出失败："+exception.Message);}
        }
    }
    public static string Export(WorldMapData data)
    {
        data.Validate();string name=WorldMapStorage.SafeName(data.name);string directory=WorldMapEditorBuilder.Root+"/GeneratedMaps/"+name;Directory.CreateDirectory(directory);AssetDatabase.Refresh();
        var palette=AssetDatabase.LoadAssetAtPath<WorldMapPalette>(WorldMapEditorBuilder.PalettePath);
        var assetPath=directory+"/Map.asset";var asset=AssetDatabase.LoadAssetAtPath<WorldMapAsset>(assetPath);if(!asset){asset=ScriptableObject.CreateInstance<WorldMapAsset>();AssetDatabase.CreateAsset(asset,assetPath);}asset.palette=palette;asset.sourceJson=JsonUtility.ToJson(data);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);
        var previous=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);WorldMapEditorBuilder.Environment(palette);
            var root=new GameObject("MapEnvironment");root.AddComponent<WorldMapLoader>().map=asset;
            PrefabUtility.SaveAsPrefabAsset(root,directory+"/Environment.prefab");
            var camera=new GameObject("MapCamera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(22,28,-22);camera.transform.LookAt(Vector3.zero);camera.depthTextureMode=DepthTextureMode.Depth;
            var spawn=new GameObject("MapSpawn");spawn.transform.position=data.spawn;
            string scenePath=directory+"/"+name+".unity";EditorSceneManager.SaveScene(scene,scenePath);
            Debug.Log("地图已导出："+scenePath+"；Environment.prefab 可放入现有游戏场景。游戏角色与任务装配由游玩场景决定。");return scenePath;
        }
        finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
    }
}
