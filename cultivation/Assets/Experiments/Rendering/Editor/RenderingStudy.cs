using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Cultivation.RenderingStudy
{
    /// <summary>Creates cloned study assets, then renders in a temporary additive scene.</summary>
    public static class RenderingStudy
    {
        public const string Root = "Assets/Experiments/Rendering";
        public const string ScenePath = Root + "/MeadowStudy.unity";
        public const string PipelinePath = Root + "/StudyURP.asset";

        [MenuItem("Tools/Cultivation/画面试验/生成草水云试验场")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先结束 Play 模式。");
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.ImportAsset(Root);
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Root + "/StudyRenderer.asset");
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                AssetDatabase.CreateAsset(renderer, Root + "/StudyRenderer.asset");
                var outline = ScriptableObject.CreateInstance<StudyInkOutlineFeature>();
                outline.name = "Existing ink outline (study adapter)";
                outline.maskShader = Shader.Find("Cultivation/OutlineMask");
                outline.compositeShader = Shader.Find("Cultivation/OutlineComposite");
                AssetDatabase.AddObjectToAsset(outline, renderer);
                renderer.rendererFeatures.Add(outline);
                EditorUtility.SetDirty(renderer);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.supportsCameraDepthTexture = true;
                pipeline.supportsCameraOpaqueTexture = true;
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 1;
                pipeline.shadowDistance = 60;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var sky = NewMaterial("StudySky", "Skybox/Procedural");
                sky.SetColor("_SkyTint", new Color(.55f, .69f, .80f));
                sky.SetColor("_GroundColor", new Color(.52f, .64f, .65f));
                sky.SetFloat("_AtmosphereThickness", .65f);
                sky.SetFloat("_Exposure", 1.15f);
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1.25f;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(.71f, .79f, .79f);
                RenderSettings.fogStartDistance = 38;
                RenderSettings.fogEndDistance = 115;
                var sun = new GameObject("Study sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.rotation = Quaternion.Euler(43, -32, 0);
                sun.color = new Color(1, .93f, .80f);
                sun.intensity = 1.15f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = .75f;
                sun.cullingMask = 1 << 30;
                RenderSettings.sun = sun;
                DynamicGI.UpdateEnvironment();

                var ground = NewMaterial("MeadowGround", "Universal Render Pipeline/Lit");
                ground.SetColor("_BaseColor", new Color(.38f, .47f, .22f));
                ground.SetFloat("_Smoothness", .08f);
                Primitive("Low frequency meadow ground", PrimitiveType.Cylinder, new Vector3(0,-.1f,0), new Vector3(20,.35f,18), ground);
                var shore = NewMaterial("Shore", "Universal Render Pipeline/Lit");
                shore.SetColor("_BaseColor", new Color(.65f,.59f,.38f));
                Primitive("Sand shelf", PrimitiveType.Cylinder, new Vector3(0,-.45f,0), new Vector3(22,.3f,20), shore);
                var grassSource = Load<GameObject>("Assets/Stylized Grass Shader/Prefabs/GrassTall.prefab");
                var grass = CloneMaterial("MeadowGrass", grassSource.GetComponentInChildren<Renderer>().sharedMaterial);
                grass.SetColor("_BaseColor", new Color(.43f,.64f,.24f));
                grass.SetColor("_HueVariation", new Color(.80f,.75f,.32f,.10f));
                grass.SetFloat("_OcclusionStrength", .16f);
                grass.SetFloat("_VertexDarkening", .07f);
                grass.SetFloat("_WindAmbientStrength", .18f);
                grass.SetVector("_FadeParams", new Vector4(30,65,1,0));
                grass.enableInstancing = true;
                var rng = new System.Random(20261014);
                var plants = new GameObject("Real grass meshes (seed 20261014)");
                for (int i = 0; i < 340; i++)
                {
                    float x = (float)rng.NextDouble()*18-9;
                    float z = (float)rng.NextDouble()*17-8.5f;
                    if (x*x/100+z*z/81 > .88f || Mathf.Abs(x-z*.16f) < .8f || (new Vector2(x+2,z+1)).magnitude < 1.3f) continue;
                    var plant = UnityEngine.Object.Instantiate(grassSource, plants.transform);
                    plant.transform.position = new Vector3(x,.25f,z);
                    plant.transform.rotation = Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                    plant.transform.localScale = Vector3.one*(.75f+(float)rng.NextDouble()*.35f);
                    foreach(var r in plant.GetComponentsInChildren<Renderer>()) r.sharedMaterial = grass;
                }
                var waterSource = Load<GameObject>("Assets/StylizedWater2/Prefabs/StylizedWater_Toon.prefab");
                var water = UnityEngine.Object.Instantiate(waterSource);
                water.name = "New water: depth + opaque texture";
                water.transform.position = new Vector3(0,-.55f,0);
                var waterMat = CloneMaterial("StudyWater", water.GetComponent<Renderer>().sharedMaterial);
                waterMat.SetColor("_BaseColor", new Color(.04f,.38f,.44f,1));
                waterMat.SetColor("_ShallowColor", new Color(.29f,.68f,.62f,.2f));
                waterMat.SetFloat("_NormalStrength", .18f);
                waterMat.SetFloat("_SunReflectionStrength", .6f);
                waterMat.SetFloat("_FoamSize", .70f);
                waterMat.SetColor("_FoamColor",new Color(.83f,.94f,.90f,.14f));
                waterMat.SetFloat("_CausticsBrightness", .35f);
                waterMat.SetFloat("_IntersectionLength", .65f);
                water.GetComponent<Renderer>().sharedMaterial = waterMat;

                var cloudPaths = AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/CloudPack Smooth/Prefabs/Style 01"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
                if (cloudPaths.Length == 0) throw new InvalidOperationException("未找到云预制体。");
                for (int i=0;i<7;i++)
                {
                    var cloud = UnityEngine.Object.Instantiate(Load<GameObject>(cloudPaths[i%cloudPaths.Length]));
                    cloud.name = "Distance cloud " + i;
                    cloud.transform.position = new Vector3(-32+i*11,12+i%2*5,38+i%3*9);
                    cloud.transform.localScale = Vector3.one*(.55f+i%3*.12f);
                }
                var avatar = UnityEngine.Object.Instantiate(Load<GameObject>("Assets/resources/外观/修仙者.prefab"));
                avatar.name = "Existing avatar + ASP cel shading";
                avatar.transform.position = new Vector3(-2,.25f,-1);
                avatar.transform.rotation = Quaternion.Euler(0,150,0);
                avatar.AddComponent<RenderingStudySubject>();
                foreach(var behaviour in avatar.GetComponentsInChildren<MonoBehaviour>())
                    if (!(behaviour is RenderingStudySubject)) UnityEngine.Object.DestroyImmediate(behaviour);
                var originalMaterials = avatar.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct().ToArray();
                var ramp = AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Materials/InkCelRamp.asset");
                if(!ramp)
                {
                    ramp = new Texture2D(256,2,TextureFormat.RGB24,false){name="InkCelRamp",wrapMode=TextureWrapMode.Clamp};
                    for(int x=0;x<256;x++)
                    {
                        float t=x/255f;
                        var colour=t<.32f ? new Color(.33f,.39f,.45f) : t<.62f ? new Color(.64f,.68f,.70f) : new Color(1,.98f,.92f);
                        ramp.SetPixel(x,0,colour);ramp.SetPixel(x,1,colour);
                    }
                    ramp.Apply();AssetDatabase.CreateAsset(ramp,Root+"/Materials/InkCelRamp.asset");
                }
                foreach(var r in avatar.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = r.sharedMaterials.Select(original =>
                    {
                        if(!original) return original;
                        var mat = NewMaterial("Avatar_"+Array.IndexOf(originalMaterials,original), "ASP/Character");
                        if(original.HasProperty("_MainTex")) mat.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
                        if(original.HasProperty("_Color")) mat.SetColor("_BaseColor",original.GetColor("_Color"));
                        mat.SetFloat("_style",1);
                        mat.SetTexture("_RampMap",ramp);
                        mat.EnableKeyword("_STYLE_CELSHADING");
                        mat.SetFloat("_ShadowColorMode",1);
                        mat.SetColor("_ReceivedShadowColor",new Color(.48f,.53f,.59f));
                        return mat;
                    }).ToArray();
                var camera = new GameObject("RenderingStudyCamera").AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(15,10,-18);
                camera.transform.LookAt(new Vector3(0,1.5f,2));
                camera.fieldOfView = 48;
                camera.farClipPlane = 180;
                camera.cullingMask = 1<<30;
                camera.allowHDR = true;
                camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
                camera.GetUniversalAdditionalCameraData().requiresColorTexture = true;
                foreach(var root in scene.GetRootGameObjects()) foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=30;
                // Loading a saved .unity file as sub-assets can deserialize scene objects off-thread.
                // Save only the study materials and renderer assets that we actually edited.
                foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
                    AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
                AssetDatabase.SaveAssetIfDirty(renderer);
                AssetDatabase.SaveAssetIfDirty(pipeline);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("Rendering study created: "+ScenePath+". Open Tools/Cultivation/画面试验/预览 URP 草水云 to render.");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing study input: " + path);
        static Material NewMaterial(string name, string shader)
        {
            var path = Root+"/Materials/"+name+".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) {mat = new Material(Shader.Find(shader) ?? throw new InvalidOperationException(shader)); AssetDatabase.CreateAsset(mat,path);}
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static Material CloneMaterial(string name, Material original)
        {
            var mat = NewMaterial(name, original.shader.name);
            mat.CopyPropertiesFromMaterial(original);
            return mat;
        }
        static void Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
        }
    }

    public sealed class RenderingStudyWindow : EditorWindow
    {
        RenderTexture preview;
        Scene study, previous;
        RenderPipelineAsset previousGraphics, previousQuality;
        RenderPipelineGlobalSettings previousGlobal;
        byte[] graphicsFile, qualityFile;
        string graphicsPath, qualityPath;
        int waitFrames;
        bool active;
        bool finishing;
        bool previousLinear, previousTemperature;
        int previousAA;
        Light[] otherLights;
        bool[] lightStates;
        string status = "预览会暂时使用试验管线，完成后自动恢复项目设置。";
        public bool showOutline = true;
        public bool characterCloseup;
        public string outputFile = "meadow-urp.png";

        [MenuItem("Tools/Cultivation/画面试验/预览 URP 草水云")]
        public static void Open() => GetWindow<RenderingStudyWindow>("草水云试验");

        public void Capture()
        {
            if(active || EditorApplication.isPlaying) return;
            if(!File.Exists(RenderingStudy.ScenePath)) RenderingStudy.Build();
            previous=SceneManager.GetActiveScene();
            previousGraphics=GraphicsSettings.defaultRenderPipeline;
            previousQuality=QualitySettings.renderPipeline;
            previousLinear=GraphicsSettings.lightsUseLinearIntensity;
            previousTemperature=GraphicsSettings.lightsUseColorTemperature;
            previousAA=QualitySettings.antiAliasing;
            previousGlobal=GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
            graphicsPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../ProjectSettings/GraphicsSettings.asset"));
            qualityPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../ProjectSettings/QualitySettings.asset"));
            graphicsFile=File.ReadAllBytes(graphicsPath);
            qualityFile=File.ReadAllBytes(qualityPath);
            otherLights=UnityEngine.Object.FindObjectsOfType<Light>();
            lightStates=otherLights.Select(l=>l.enabled).ToArray();
            active=true;
            try
            {
                study=EditorSceneManager.OpenScene(RenderingStudy.ScenePath,OpenSceneMode.Additive);
                SceneManager.SetActiveScene(study);
                foreach(var light in otherLights) light.enabled=false;
                var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderingStudy.PipelinePath);
                GraphicsSettings.defaultRenderPipeline=asset;
                QualitySettings.renderPipeline=asset;
                waitFrames=8;
                EditorApplication.update+=Tick;
                AssemblyReloadEvents.beforeAssemblyReload+=Restore;
                status="渲染试验场…";
            }
            catch {Restore();throw;}
        }

        void Tick()
        {
            if(--waitFrames>0){EditorApplication.QueuePlayerLoopUpdate(); return;}
            try
            {
                var camera=study.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();
                camera.name=showOutline ? "RenderingStudyCamera" : "RenderingStudyCameraNoInk";
                if(characterCloseup)
                {
                    camera.transform.position=new Vector3(-5.2f,2.7f,-6);
                    camera.transform.LookAt(new Vector3(-2,1.2f,-1));
                    camera.fieldOfView=35;
                }
                if(preview) preview.Release();
                if(preview) DestroyImmediate(preview);
                preview=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32);
                preview.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination=preview });
                var before=RenderTexture.active;
                RenderTexture.active=preview;
                var pixels=new Texture2D(preview.width,preview.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,preview.width,preview.height),0,0);
                pixels.Apply();
                RenderTexture.active=before;
                Directory.CreateDirectory("../.local/diagnostics");
                File.WriteAllBytes(Path.Combine("../.local/diagnostics",Path.GetFileName(outputFile)),pixels.EncodeToPNG());
                DestroyImmediate(pixels);
                status="已渲染；正在恢复原管线及光照设置…";
            }
            catch(Exception e){status=e.ToString();Debug.LogException(e);}
            finally{Restore();Repaint();}
        }

        void Restore()
        {
            EditorApplication.update-=Tick;
            AssemblyReloadEvents.beforeAssemblyReload-=Restore;
            if(!active)return;
            GraphicsSettings.defaultRenderPipeline=previousGraphics;
            QualitySettings.renderPipeline=previousQuality;
            if(previousGlobal) GraphicsSettings.RegisterRenderPipelineSettings<UniversalRenderPipeline>(previousGlobal);
            else GraphicsSettings.UnregisterRenderPipelineSettings<UniversalRenderPipeline>();
            for(int i=0;i<otherLights.Length;i++)if(otherLights[i])otherLights[i].enabled=lightStates[i];
            if(previous.IsValid() && previous.isLoaded)SceneManager.SetActiveScene(previous);
            if(study.IsValid() && study.isLoaded)EditorSceneManager.CloseScene(study,true);
            // Pipeline creation itself changes global light flags and quality AA. Wait for disposal
            // before the final restoration, or URP can overwrite them again later in this frame.
            if(!finishing){finishing=true;waitFrames=4;EditorApplication.update+=FinishRestore;}
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void FinishRestore()
        {
            if(--waitFrames>0){EditorApplication.QueuePlayerLoopUpdate();return;}
            FinishRestoreImmediately();
            status="已渲染并恢复原管线、光照和抗锯齿。截图：.local/diagnostics/"+outputFile;
            Repaint();
        }

        void FinishRestoreImmediately()
        {
            EditorApplication.update-=FinishRestore;
            if(!active)return;
            GraphicsSettings.lightsUseLinearIntensity=previousLinear;
            GraphicsSettings.lightsUseColorTemperature=previousTemperature;
            QualitySettings.antiAliasing=previousAA;
            if(previousGlobal)GraphicsSettings.RegisterRenderPipelineSettings<UniversalRenderPipeline>(previousGlobal);
            else GraphicsSettings.UnregisterRenderPipelineSettings<UniversalRenderPipeline>();
            // Tuanjie can serialize ProjectSettings during a pipeline change. Restore disk too.
            File.WriteAllBytes(graphicsPath,graphicsFile);
            File.WriteAllBytes(qualityPath,qualityFile);
            active=false;
            finishing=false;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
            using(new EditorGUI.DisabledScope(active || EditorApplication.isPlaying))
            {
                showOutline=EditorGUILayout.Toggle("保留原描边",showOutline);
                characterCloseup=EditorGUILayout.Toggle("角色材质近景",characterCloseup);
                if(GUILayout.Button("渲染草、水、云与原有描边"))Capture();
            }
            var rect=GUILayoutUtility.GetRect(64,10000,64,10000);
            if(preview)GUI.DrawTexture(rect,preview,ScaleMode.ScaleToFit,false);
        }
        void OnDisable(){Restore();FinishRestoreImmediately();if(preview){preview.Release();DestroyImmediate(preview);}}
    }
}
