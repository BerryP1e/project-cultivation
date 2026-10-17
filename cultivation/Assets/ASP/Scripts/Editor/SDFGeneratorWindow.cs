using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace ASP.Scripts.Editor
{
    public class SDFGeneratorWindow : EditorWindow
    {
        public enum SDFInvididualQuality
        {
            High = 2048,
            Mid = 1024,
            Low = 512,
        }

        public enum MergeedSDFQuality
        {
            High = 512,
            Mid = 256,
            Low = 128,
        }
    
        private void DrawHorizontalLine()
        {
            EditorGUILayout.Separator();
            // EditorGUILayout.Space();
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(2));
            r.height = 1;
            r.y += 1 / 2;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, Color.gray);
            EditorGUILayout.Space(10);
        }

        public int DistanceRange = 256;
        public string GroupName = "Group1";
        public SDFInvididualQuality SDFQuality = SDFInvididualQuality.High;
        public string GeneratePath = "Assets/Textures/SDFGeneratedResults/";
        public static readonly string projectPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6);
        [SerializeField] public Texture[] SourceTextures;
    
        public MergeedSDFQuality MergedQuality = MergeedSDFQuality.High;
        [SerializeField] public Texture[] SDFImagesToMerge;
    
        private RenderTexture m_renderTexture;

        // Add menu item
        [MenuItem("Tools/ASP/SDFGenerator")]
        static void Init()
        {
            EditorWindow window = EditorWindow.CreateInstance<SDFGeneratorWindow>();
            window.Show();
        }

        private void CreateGUI()
        {
            var serializedObject = new SerializedObject(this);
            VisualElement root = this.rootVisualElement;
            var distanceRangeView = new IntegerField("DistanceRange");
            distanceRangeView.SetValueWithoutNotify(256);
            distanceRangeView.RegisterCallback<ChangeEvent<int>>(e =>
            {
                DistanceRange = e.newValue;
            });

            var sdfGeneratorSection = new VisualElement();
            sdfGeneratorSection.style.paddingBottom = 5;
            sdfGeneratorSection.style.paddingTop = 5;
            sdfGeneratorSection.style.paddingLeft = 5;
            sdfGeneratorSection.style.paddingRight = 5;
            root.Add(sdfGeneratorSection);
        
            root.Add(new IMGUIContainer(DrawHorizontalLine));
        
            var sdfMergerSection = new VisualElement();
            sdfMergerSection.style.paddingBottom = 5;
            sdfMergerSection.style.paddingTop = 5;
            sdfMergerSection.style.paddingLeft = 5;
            sdfMergerSection.style.paddingRight = 5;
            root.Add(sdfMergerSection);

            var titleLabel = new Label("SDF Generator");
            titleLabel.style.alignSelf = Align.Center;
            titleLabel.style.fontSize = 20;
            titleLabel.style.marginTop = 10;
            titleLabel.style.marginBottom = 10;
        
            sdfGeneratorSection.Add(titleLabel);
        
            sdfGeneratorSection.Add(distanceRangeView);
        
            sdfGeneratorSection.Add(new IMGUIContainer(() =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("SDFQuality"));
                serializedObject.ApplyModifiedProperties();
            }));
        
            var groupNameView = new TextField("GroupName");
            groupNameView.SetValueWithoutNotify("Group1");
            groupNameView.RegisterCallback<ChangeEvent<string>>(e =>
            {
                GroupName = e.newValue;
            });
            sdfGeneratorSection.Add(groupNameView);
        
            var targetPathView = new TextField("GeneratePath");
            targetPathView.SetValueWithoutNotify(GeneratePath);
            targetPathView.RegisterCallback<ChangeEvent<string>>(e =>
            {
                GeneratePath = e.newValue;
            });
            sdfGeneratorSection.Add(targetPathView);
        
            sdfGeneratorSection.Add(new IMGUIContainer(() =>
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("SourceTextures"), true);
                serializedObject.ApplyModifiedProperties();
            }));
        
            var createSDFsButton = new Button();
            createSDFsButton.text = "Generate SDF Images";
            createSDFsButton.clicked += () => {
                GenerateSDF(false);
                serializedObject.ApplyModifiedProperties();
            };
            sdfGeneratorSection.Add(createSDFsButton);
        
            var mergeLabel = new Label("SDFs Merger");
            mergeLabel.style.alignSelf = Align.Center;
            mergeLabel.style.fontSize = 20;
            mergeLabel.style.marginTop = 10;
            mergeLabel.style.marginBottom = 10;
        
            sdfMergerSection.Add(mergeLabel);
        
        
            sdfMergerSection.Add(new IMGUIContainer(() =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("MergedQuality"));
                serializedObject.ApplyModifiedProperties();
            }));
        
            var mergeArray = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                EditorGUILayout.PropertyField(serializedObject.FindProperty("SDFImagesToMerge"), true);
                serializedObject.ApplyModifiedProperties();
            });
            sdfMergerSection.Add(mergeArray);
        
            var mergeSDFImagesButton = new Button();
            mergeSDFImagesButton.text = "Merge SDF Images";
            mergeSDFImagesButton.clicked += () => {
                MergeSDF();
                serializedObject.ApplyModifiedProperties();
                mergeArray.MarkDirtyRepaint();
            };
            sdfMergerSection.Add(mergeSDFImagesButton);
        
            var createSDFAndMergeButton = new Button();
            createSDFAndMergeButton.text = "Create SDFs & Merge";
            createSDFAndMergeButton.clicked += () => {
                GenerateSDF(true);
                foreach (var view in root.Query<IMGUIContainer>().ToList())
                {
                    view.MarkDirtyRepaint();
                }
                serializedObject.ApplyModifiedProperties();
            };
            sdfGeneratorSection.Add(createSDFAndMergeButton);
        }

        public void GenerateSDF(bool mergeAfterFinish)
        {
            
            if (!Directory.Exists(projectPath + GeneratePath))
            {
                Directory.CreateDirectory(projectPath + GeneratePath);
            }

            if (SourceTextures == null)
            {
                return;
            }

            var SDFShader = Shader.Find("Hidden/ImgToSDF");
            if (SDFShader == null)
            {
                return;
            }

            if (m_renderTexture != null && m_renderTexture.IsCreated())
                m_renderTexture.Release();

            SDFImagesToMerge = new Texture[SourceTextures.Length];
            if (mergeAfterFinish && SourceTextures.Length < 2)
            {
                mergeAfterFinish = false;
            }

            for (int i = 0; i < SourceTextures.Length; i++)
            {
                m_renderTexture = new RenderTexture((int)SDFQuality, (int)SDFQuality, 0, RenderTextureFormat.R16);

                var mat = new Material(SDFShader);
                mat.hideFlags = HideFlags.DontSave;
                mat.SetFloat("_range", DistanceRange);

                var tempRT =
                    RenderTexture.GetTemporary(new RenderTextureDescriptor((int)SDFQuality, (int)SDFQuality,
                        RenderTextureFormat.R16));

                Graphics.Blit(SourceTextures[i], tempRT);
                Graphics.Blit(tempRT, m_renderTexture, mat);
                RenderTexture.ReleaseTemporary(tempRT);


                Texture2D tex = new Texture2D(m_renderTexture.width, m_renderTexture.height, TextureFormat.R16, false);
                tex.ReadPixels(new Rect(0, 0, m_renderTexture.width, m_renderTexture.height), 0, 0);
                tex.Apply();

                File.WriteAllBytes(projectPath + GeneratePath + GroupName + "_" + i + ".png", tex.EncodeToPNG());
                var unityPath = GeneratePath + GroupName + "_" + i + ".png";
                AssetDatabase.ImportAsset(unityPath);
                var textureImporter = AssetImporter.GetAtPath(unityPath) as TextureImporter;
                textureImporter.wrapMode = TextureWrapMode.Clamp;
                textureImporter.sRGBTexture = false;
                textureImporter.isReadable = true;
                textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                textureImporter.mipmapEnabled = false;

                var platformTextureSettings = textureImporter.GetDefaultPlatformTextureSettings();
                platformTextureSettings.format = TextureImporterFormat.R16;
                platformTextureSettings.textureCompression = TextureImporterCompression.Uncompressed;
                textureImporter.SetPlatformTextureSettings(platformTextureSettings);
                textureImporter.SaveAndReimport();

                if (RenderTexture.active == m_renderTexture)
                {
                    RenderTexture.active = null;
                }

                if (m_renderTexture != null && m_renderTexture.IsCreated())
                    m_renderTexture.Release();


                var generatedSDF = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
                if (!mergeAfterFinish && i == SourceTextures.Length - 1)
                {
                    SDFImagesToMerge[i] = generatedSDF;
                    Selection.activeObject = generatedSDF;
                    EditorGUIUtility.PingObject(generatedSDF);
                }
                else
                {
                    SDFImagesToMerge[i] = generatedSDF;
                }
            }

            if (mergeAfterFinish && SourceTextures.Length >= 2)
            {
                MergeSDF();
            }
        }

        public void MergeSDF()
        {
            if (SDFImagesToMerge == null || SDFImagesToMerge.Length < 2)
            {
                Debug.Log("Net enough textures ready to merge");
                return;
            }

            var mergeShader = Shader.Find("Hidden/SDFMerger");
            var mat = new Material(mergeShader);
            mat.hideFlags = HideFlags.DontSave;
            mat.SetInt("_imgNum", SDFImagesToMerge.Length);
            for (int i = 0; i < SDFImagesToMerge.Length; i++)
            {
                mat.SetTexture("_MainTex" + i, SDFImagesToMerge[i]);
            }

            if (m_renderTexture != null && m_renderTexture.IsCreated())
                m_renderTexture.Release();


            m_renderTexture = new RenderTexture((int)MergedQuality, (int)MergedQuality, 0, RenderTextureFormat.R16);
            var tempRT =
                RenderTexture.GetTemporary(new RenderTextureDescriptor((int)MergedQuality, (int)MergedQuality,
                    RenderTextureFormat.R16));

            Graphics.Blit(tempRT, m_renderTexture, mat);
            RenderTexture.ReleaseTemporary(tempRT);

            Texture2D tex = new Texture2D(m_renderTexture.width, m_renderTexture.height, TextureFormat.R16, false);
            tex.ReadPixels(new Rect(0, 0, m_renderTexture.width, m_renderTexture.height), 0, 0);
            tex.Apply();

            File.WriteAllBytes(projectPath + GeneratePath + GroupName + "_merged.png", tex.EncodeToPNG());
            var unityPath = GeneratePath + GroupName + "_merged.png";
            AssetDatabase.ImportAsset(unityPath);
            var textureImporter = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            textureImporter.wrapMode = TextureWrapMode.Repeat;
            textureImporter.sRGBTexture = false;
            textureImporter.isReadable = true;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            textureImporter.mipmapEnabled = false;

            var platformTextureSettings = textureImporter.GetDefaultPlatformTextureSettings();
            platformTextureSettings.format = TextureImporterFormat.R16;
            platformTextureSettings.textureCompression = TextureImporterCompression.Uncompressed;
            textureImporter.SetPlatformTextureSettings(platformTextureSettings);
            textureImporter.SaveAndReimport();
            if (RenderTexture.active == m_renderTexture)
            {
                RenderTexture.active = null;
            }

            if (m_renderTexture != null && m_renderTexture.IsCreated())
                m_renderTexture.Release();

            var generatedSDF = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
            Selection.activeObject = generatedSDF;
            EditorGUIUtility.PingObject(generatedSDF);
        }

    }
}