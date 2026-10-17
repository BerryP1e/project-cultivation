using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Color = UnityEngine.Color;
using FontStyle = UnityEngine.FontStyle;

// Creates a custom Label on the inspector for all the scripts named ScriptName
// Make sure you have a ScriptName script in your
// project, else this will not work.
namespace ASP.Scripts.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ASP.ASPCharacterPanel))]
    public partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {

        private bool m_showSmoothNormalOptions = true;
        private UnityEngine.Object m_bakeNoramlDestination;

        private struct FeatureDisplayModel
        {
            public Type type;
            public String DisplayName;
            public bool IsActive;
        }

        private Dictionary<FeatureDisplayModel, VisualElement> m_guiEventElements =
            new Dictionary<FeatureDisplayModel, VisualElement>();

        List<ScriptableRendererFeature> m_rendererFeatures = new List<ScriptableRendererFeature>();
        partial void DrawCharacterPanelLayerInfo(VisualElement root, ASPCharacterPanel characterPanel);
        partial void DrawBuiltInShadowCastInfo(VisualElement root, ASPCharacterPanel characterPanel);
        partial void DrawBuiltInShadowReceivedInfo(VisualElement root, ASPCharacterPanel characterPanel);
        partial void DrawASPExtraShadowReceivedInfo(VisualElement root, ASPCharacterPanel characterPanel);
        partial void DrawSimpleShadowSetupEditor(VisualElement root, ASPCharacterPanel characterPanel);
        
        partial void DrawMeshOutlineParam(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject);
        partial void DrawLightingDirectionOverrideProperty(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject);
        partial void DrawMainLightingPropertyOverrideProperty(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject);
        partial void DrawASPSurfaceInfo(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject);

        List<ScriptableRendererFeature> FetchRendererFeatures()
        {
            var renderer = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset).GetRenderer(0);
            var property =
                typeof(ScriptableRenderer).GetProperty("rendererFeatures",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            return property.GetValue(renderer) as List<ScriptableRendererFeature>;
        }

        private void SetKeyword(Material material, string keyword, bool state)
        {
            if (state)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        private void SetPropertyFloat(Material material, string propName, bool state)
        {
            if (state)
            {
                material.SetFloat(propName, 1f);
            }
            else
            {
                material.SetFloat(propName, 0f);
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            var characterPanel = (ASP.ASPCharacterPanel)target;
            var container = new VisualElement();
            m_rendererFeatures = FetchRendererFeatures();
            m_guiEventElements.Clear();
            container.Add(new IMGUIContainer(DrawHorizontalLine));
            if (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset != null)
            {
                var rendererTitleLabel = new Label("ASP Renderer Feature Check List");
                rendererTitleLabel.style.fontSize = 20;
                rendererTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                rendererTitleLabel.style.marginBottom = 10;
                rendererTitleLabel.style.whiteSpace = WhiteSpace.Normal;
                var requiredLabel = new Label("---Required Passes---");
                requiredLabel.style.alignSelf = Align.Center;
                container.Add(rendererTitleLabel);
                container.Add(requiredLabel);
                container.Add(DrawRendererFeatureCheckerElement(
                    m_rendererFeatures, typeof(ASPMaterialPassFeature), "ASP Material Pass"));
                container.Add(DrawRendererFeatureCheckerElement(
                    m_rendererFeatures, typeof(ASPShadowMapFeature), "ASP ShadowMap"));
                container.Add(DrawRendererFeatureCheckerElement(
                    m_rendererFeatures, typeof(ASPDepthOffsetShadowFeature), "ASP Depth-Offset Shadow"));
                var optionalLabel = new Label("---Optional Passes---");
                optionalLabel.style.alignSelf = Align.Center;
                container.Add(optionalLabel);
                container.Add(DrawRendererFeatureCheckerElement(
                    m_rendererFeatures, typeof(ASPMeshOutlineRendererFeature), "ASP Mesh Outline"));
            }
        
            container.Add(new IMGUIContainer(() =>
            {
                var isAllRequiredFeaturesReady = true;
                foreach (var pair in m_guiEventElements)
                {
                    if (!pair.Key.IsActive)
                    {
                        isAllRequiredFeaturesReady = false;
                    }
                }

                if (!isAllRequiredFeaturesReady)
                {
                    EditorGUILayout.HelpBox("some passes are not setup to run.", MessageType.Info);
                    //GUILayout.Space(-32);
                
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Open URP Data", GUILayout.Width(130)))
                        {
                            var urpAsset = (GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
                            PropertyInfo propertyInfo = typeof(UniversalRenderPipelineAsset).GetProperty("scriptableRendererData", BindingFlags.NonPublic | BindingFlags.Instance);
                            if (propertyInfo != null)
                            {
                                // Get the value of m_RendererDataList
                                ScriptableRendererData rendererData = (ScriptableRendererData)propertyInfo.GetValue(urpAsset);
                                Selection.activeObject = rendererData;
                                GUIUtility.ExitGUI();
                            }
                        }

                        GUILayout.Space(8);
                    }
                    GUILayout.Space(11);
                }
            }));

            container.Add(new IMGUIContainer(DrawHorizontalLine));
        
            var shadowInfoContainer = new VisualElement();
            shadowInfoContainer.style.alignItems = Align.Stretch;
            var shadowInfoTitle = new Label("Shadow Behaviours");
            shadowInfoTitle.style.fontSize = 20;
            shadowInfoTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            shadowInfoTitle.style.marginBottom = 5;
            shadowInfoContainer.Add(shadowInfoTitle);

            DrawCharacterPanelLayerInfo(shadowInfoContainer, characterPanel);

            shadowInfoContainer.Add(new Label());

            DrawBuiltInShadowCastInfo(shadowInfoContainer, characterPanel);

            shadowInfoContainer.Add(new Label());

            DrawBuiltInShadowReceivedInfo(shadowInfoContainer, characterPanel);

            shadowInfoContainer.Add(new Label());

            DrawASPExtraShadowReceivedInfo(shadowInfoContainer, characterPanel);
            
            shadowInfoContainer.Add(new Label());

            DrawSimpleShadowSetupEditor(shadowInfoContainer, characterPanel);

            container.Add(shadowInfoContainer);

            container.Add(new IMGUIContainer(DrawHorizontalLine));

            var lightDirectionOverride = new VisualElement();
            lightDirectionOverride.style.alignItems = Align.Stretch;
            var lightDirectionOverrideTitle = new Label("Main Light Direction Param");
            lightDirectionOverrideTitle.style.fontSize = 20;
            lightDirectionOverrideTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            lightDirectionOverride.Add(lightDirectionOverrideTitle);

            DrawLightingDirectionOverrideProperty(lightDirectionOverride, characterPanel, serializedObject);
            container.Add(lightDirectionOverride);
            container.Add(new IMGUIContainer(DrawHorizontalLine));
            
            var mainLightPropertyOverride = new VisualElement();
            mainLightPropertyOverride.style.alignItems = Align.Stretch;
            var mainLightPropertyOverrideTitle = new Label("Main Light Property Override");
            mainLightPropertyOverrideTitle.style.fontSize = 20;
            mainLightPropertyOverrideTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            mainLightPropertyOverride.Add(mainLightPropertyOverrideTitle);

            DrawMainLightingPropertyOverrideProperty(mainLightPropertyOverride, characterPanel, serializedObject);
            container.Add(mainLightPropertyOverride);
            container.Add(new IMGUIContainer(DrawHorizontalLine));

            var meshOutlineInfoContainer = new VisualElement();
            meshOutlineInfoContainer.style.alignItems = Align.Stretch;
            var meshOutlineInfoTitle = new Label("MeshOutline Param");
            meshOutlineInfoTitle.style.fontSize = 20;
            meshOutlineInfoTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            meshOutlineInfoContainer.Add(meshOutlineInfoTitle);

            DrawMeshOutlineParam(meshOutlineInfoContainer, characterPanel, serializedObject);

            container.Add(meshOutlineInfoContainer);
        
            container.Add(new IMGUIContainer(DrawHorizontalLine));
        
            var surfaceOptionContainer = new VisualElement();
            var surfaceOptionTitle = new Label("Surface Options");
            surfaceOptionContainer.style.alignItems = Align.Stretch;
            surfaceOptionTitle.style.fontSize = 20;
            surfaceOptionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            surfaceOptionContainer.Add(surfaceOptionTitle);

            DrawASPSurfaceInfo(surfaceOptionContainer, characterPanel, serializedObject);

            container.Add(surfaceOptionContainer);

            container.RegisterCallback<MouseEnterEvent>(e =>
            {
                m_rendererFeatures = FetchRendererFeatures();
                foreach (var pair in m_guiEventElements)
                {
                    UpdateRendererFeatureCheckerElement(m_rendererFeatures, pair.Key.type, pair.Key.DisplayName,
                        pair.Value);
                }
            });
        
            container.Add(new IMGUIContainer(OnInspectorGUI));
            return container;
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

        private VisualElement DrawRendererFeatureCheckerElement(List<ScriptableRendererFeature> features, Type type,
            string displayName)
        {
            var container = new VisualElement();
            //var rendererFeatureElementAsset = Resources.Load<VisualTreeAsset>("ASPRendererFeatureElement");
            //var rendererFeatureElement = rendererFeatureElementAsset.Instantiate();
            container.style.flexDirection = FlexDirection.Row;
            container.style.justifyContent = Justify.SpaceBetween;
            container.style.alignSelf = Align.Stretch;
            container.style.alignItems = Align.Auto;

            var isExist = features.Any(e => e.GetType() == type);
            var isActive = false;
            if (isExist)
            {
                isActive = features.Find(e => e.GetType() == type).isActive;
            }

            Label featureTitleLabel = new Label("Title");
            featureTitleLabel.name = "Title";
            featureTitleLabel.text = displayName;

            Label featureStatusIcon = new Label("   ");
            featureStatusIcon.name = "StatusIcon";
            featureStatusIcon.style.width = 20;
            featureStatusIcon.style.justifyContent = Justify.FlexEnd;
            featureStatusIcon.style.alignItems = Align.Auto;
            featureStatusIcon.style.alignSelf = Align.Auto;
            featureStatusIcon.style.backgroundImage =
                new StyleBackground(EditorGUIUtility.TrIconContent("console.warnicon").image as Texture2D);

            Label featureStatusLabel = new Label("Status");
            featureStatusLabel.name = "Status";

            var fontColor = isExist ? (isActive ? Color.green : Color.yellow) : Color.red;
            featureStatusLabel.text = isExist ? (isActive ? "Active" : "Not Active") : "Missing";
            featureStatusLabel.style.color = fontColor;
            featureStatusLabel.style.justifyContent = Justify.FlexEnd;
            featureStatusLabel.style.paddingLeft = 9 * featureStatusLabel.text.Length;
            featureStatusLabel.style.unityBackgroundScaleMode = new StyleEnum<ScaleMode>(ScaleMode.ScaleToFit);

            var iconImage = isExist
                ? EditorGUIUtility.TrIconContent("P4_CheckOutRemote@2x").image
                : EditorGUIUtility.TrIconContent("console.erroricon.inactive.sml@2x").image;
            if (isExist)
            {
                iconImage = isActive
                    ? EditorGUIUtility.TrIconContent("P4_CheckOutRemote@2x").image
                    : EditorGUIUtility.TrIconContent("console.warnicon@2x").image;
            }

            featureStatusLabel.style.backgroundImage = new StyleBackground(iconImage as Texture2D);

            container.Add(featureTitleLabel);
            container.Add(featureStatusLabel);
        
            m_guiEventElements.Add(new FeatureDisplayModel { type = type, DisplayName = displayName, IsActive = isActive}, container);
            return container;
        }

        private void UpdateRendererFeatureCheckerElement(List<ScriptableRendererFeature> features, Type type,
            string displayName,
            VisualElement rendererFeatureElement)
        {
            var isExist = features.Any(e => e.GetType() == type);
            var isActive = false;
            if (isExist)
            {
                isActive = features.Find(e => e.GetType() == type).isActive;
            }

            Label featureTitleLabel = rendererFeatureElement.Q<Label>("Title");
            featureTitleLabel.text = displayName;

            Label featureStatusLabel = rendererFeatureElement.Q<Label>("Status");
            var fontColor = isExist ? (isActive ? Color.green : Color.yellow) : Color.red;
            featureStatusLabel.text = isExist ? (isActive ? "Active" : "Not Active") : "Missing";
            featureStatusLabel.style.paddingLeft = 9 * featureStatusLabel.text.Length;
            featureStatusLabel.style.color = fontColor;

            var iconImage = isExist
                ? EditorGUIUtility.TrIconContent("P4_CheckOutRemote@2x").image
                : EditorGUIUtility.TrIconContent("console.erroricon.inactive.sml@2x").image;
            if (isExist)
            {
                iconImage = isActive
                    ? EditorGUIUtility.TrIconContent("P4_CheckOutRemote@2x").image
                    : EditorGUIUtility.TrIconContent("console.warnicon@2x").image;
            }

            featureStatusLabel.style.backgroundImage = new StyleBackground(iconImage as Texture2D);
        }

        private void DrawBakeSmoothNormalFieldIMGUI(ASPCharacterPanel characterPanel)
        {
            m_showSmoothNormalOptions = EditorGUILayout.Foldout(m_showSmoothNormalOptions, "Bake Smooth normal field");
            if (m_showSmoothNormalOptions)
            {
                EditorGUILayout.BeginHorizontal();
                m_bakeNoramlDestination =
                    EditorGUILayout.ObjectField("Smooth Target", m_bakeNoramlDestination, typeof(GameObject), false);
                if (GUILayout.Button("Quick Find"))
                {
                    Mesh targetMesh = null;
                    var regularMesh = characterPanel.GetComponentInChildren<MeshFilter>();
                    if (regularMesh)
                    {
                        targetMesh = regularMesh.sharedMesh;
                    }
                
                    var skinnedMesh = characterPanel.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (skinnedMesh)
                    {
                        targetMesh = skinnedMesh.sharedMesh;
                    }

                    if (targetMesh != null)
                    {
                        m_bakeNoramlDestination = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(targetMesh));
                        //Debug.Log(AssetDatabase.GetAssetPath(targetMesh));
                    }

                }
                EditorGUILayout.EndHorizontal();
         
                GUILayout.Space(5);

                if (GUILayout.Button("bake smooth normal into uv4(5th channel)"))
                {
                    if (m_bakeNoramlDestination != null)
                    {
                        //  SmoothNormalTool.PerformSmoothNormalBaking(bakeNoramlSource as GameObject, bakeNoramlDestination as GameObject);
                        var assetPath = AssetDatabase.GetAssetPath(m_bakeNoramlDestination);
                        string src = assetPath;
                        string smoothedModelDst = Path.GetDirectoryName(src) + "/" + m_bakeNoramlDestination.name + "@smoothRef" +
                                                  Path.GetExtension(src);
                        var smoothObject = AssetDatabase.LoadAssetAtPath<GameObject>(smoothedModelDst);
                        if (smoothObject != null)
                        {
                            // AssetDatabase.DeleteAsset(smoothedModelDst);
                        }

                        if (!File.Exists(Application.dataPath + "/" + smoothedModelDst.Substring(7)))
                        {
                            AssetDatabase.CopyAsset(src, smoothedModelDst);
                            AssetDatabase.ImportAsset(smoothedModelDst);
                            var smoothAssetImporter = AssetImporter.GetAtPath(smoothedModelDst);
                            smoothAssetImporter.userData = "ASPSmoothModel_SmoothRef";
                            smoothAssetImporter.SaveAndReimport();
                        }
                    
                        AssetImporter assetImporter = AssetImporter.GetAtPath(assetPath);
                        assetImporter.userData = "ASPSmoothModel_BakeTarget";
                        assetImporter.SaveAndReimport();
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            DrawHorizontalLine();
            var characterPanel = (ASP.ASPCharacterPanel)target;
            if(!Application.isPlaying)
                characterPanel.SetupMaterialID();
        }
    }
}