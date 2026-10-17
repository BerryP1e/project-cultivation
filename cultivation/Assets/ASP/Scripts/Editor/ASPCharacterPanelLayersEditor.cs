using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace ASP.Scripts.Editor
{
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        private int MaskToLayer(LayerMask mask)
        {
            var bitmask = mask.value;

            UnityEngine.Assertions.Assert.IsFalse((bitmask & (bitmask - 1)) != 0,
                "MaskToLayer() was passed an invalid mask containing multiple layers.");

            int result = bitmask > 0 ? 0 : 31;
            while (bitmask > 1)
            {
                bitmask = bitmask >> 1;
                result++;
            }

            return result;
        }

        private void GetRendererFeatureLayerAndMaskString(string[] renderingLayerMaskNames, int layer,
            int renderingLayerMask, out string layerValueString, out string renderingLayerMaskString)
        {
            layerValueString = "missing";
            renderingLayerMaskString = "unexpected value, check renderer feature";
            if (m_rendererFeatures.Where(e => e.GetType() == typeof(ASPDepthOffsetShadowFeature)).Count() > 0)
            {
                var depthOffsetFeature = m_rendererFeatures.Find(e => e.GetType() == typeof(ASPDepthOffsetShadowFeature));
                layerValueString = LayerMask.LayerToName(layer);

                int maskCount = (int)Mathf.Log(renderingLayerMask, 2);
                if (renderingLayerMaskNames.Length < maskCount && maskCount <= 32)
                {
                    var newRenderingLayerMaskNames = new string[maskCount];
                    for (int i = 0; i < maskCount; ++i)
                    {
                        newRenderingLayerMaskNames[i] = i < renderingLayerMaskNames.Length
                            ? renderingLayerMaskNames[i]
                            : $"Unused Layer {i}";
                    }

                    renderingLayerMaskNames = newRenderingLayerMaskNames;

                    // EditorGUILayout.HelpBox($"One or more of the Rendering Layers is not defined in the Universal Global Settings asset.", MessageType.Warning);
                }

                var renderingLayerMaskField = new MaskField(renderingLayerMaskNames.ToList(), maskCount);
                if (renderingLayerMaskField.showMixedValue)
                {
                    renderingLayerMaskString = "Mixed....";
                }
                else if (maskCount >= 0 && maskCount < renderingLayerMaskNames.Length)
                {
                    renderingLayerMaskString = renderingLayerMaskNames[maskCount];
                }
            }
        }

        partial void DrawCharacterPanelLayerInfo(VisualElement root, ASPCharacterPanel characterPanel)
        {
            var rendererLayerMaskFoldoutGroup = new Foldout();
            rendererLayerMaskFoldoutGroup.Q<Foldout>().text = "Renderer Layer Information";
            rendererLayerMaskFoldoutGroup.Q<Foldout>().style.fontSize = 15;
            rendererLayerMaskFoldoutGroup.Q<Foldout>().value = false;

            var rendererFeatureHintLabel = new VisualElement();
            rendererFeatureHintLabel.style.justifyContent = Justify.SpaceBetween;
            rendererFeatureHintLabel.style.alignSelf = Align.Stretch;
            rendererFeatureHintLabel.style.alignItems = Align.Auto;
            rendererFeatureHintLabel.style.flexDirection = FlexDirection.Row;
            rendererFeatureHintLabel.style.marginTop = 10;
            rendererFeatureHintLabel.style.marginTop = 5;
            var rendererFeatureTitleHintLabel = new Label("Feature");
            var rendererFeatureLayerHintLabel = new Label("Layer");
            var rendererFeatureRenderingLayerMaskHintLabel = new Label("Rendering Layer Mask");
            rendererFeatureTitleHintLabel.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            rendererFeatureLayerHintLabel.style.width = new StyleLength(new Length(25, LengthUnit.Percent));
            rendererFeatureRenderingLayerMaskHintLabel.style.width = new StyleLength(new Length(35, LengthUnit.Percent));


            rendererFeatureTitleHintLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rendererFeatureLayerHintLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rendererFeatureRenderingLayerMaskHintLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            rendererFeatureRenderingLayerMaskHintLabel.style.whiteSpace = WhiteSpace.Normal;

            rendererFeatureHintLabel.Add(rendererFeatureTitleHintLabel);
            rendererFeatureHintLabel.Add(rendererFeatureLayerHintLabel);
            rendererFeatureHintLabel.Add(rendererFeatureRenderingLayerMaskHintLabel);

            rendererLayerMaskFoldoutGroup.Add(rendererFeatureHintLabel);

            var renderPipelineAsset = GraphicsSettings.currentRenderPipeline;
            var renderingLayerMaskNames = renderPipelineAsset.renderingLayerMaskNames;

            // -- Depth Offset Feature Start --

            var depthOffsetFeatureContainer = new VisualElement();
            depthOffsetFeatureContainer.style.justifyContent = Justify.SpaceBetween;
            depthOffsetFeatureContainer.style.alignSelf = Align.Stretch;
            depthOffsetFeatureContainer.style.alignItems = Align.Auto;
            depthOffsetFeatureContainer.style.flexDirection = FlexDirection.Row;
        
            var layerValueString = "Missing";
            var renderingLayerMaskString = "Unexpected Value, Check renderer feature";
            var hasDepthOffsetShadowFeature = 
                m_rendererFeatures.Where(e => e.GetType() == typeof(ASPDepthOffsetShadowFeature)).Count() > 0;
            if (hasDepthOffsetShadowFeature)
            {
                var feature =
                    m_rendererFeatures.Find(e => e.GetType() == typeof(ASPDepthOffsetShadowFeature)) as
                        ASPDepthOffsetShadowFeature;
                GetRendererFeatureLayerAndMaskString(renderingLayerMaskNames, feature.m_layer,
                    (int)feature.m_renderingLayerMask, out layerValueString, out renderingLayerMaskString);
            }

            if (hasDepthOffsetShadowFeature)
            {
                var depthOffsetExpectedLayerPrefix = new Label("Depth Offset Shadow");
                depthOffsetExpectedLayerPrefix.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
                depthOffsetExpectedLayerPrefix.style.overflow = new StyleEnum<Overflow>(Overflow.Hidden);
                var depthOffsetExpectedLayer = new Label(layerValueString);
                depthOffsetExpectedLayer.style.width = new StyleLength(new Length(25, LengthUnit.Percent));

                var depthOffsetExpectedRenderingLayerMask = new Label(renderingLayerMaskString);
                depthOffsetExpectedRenderingLayerMask.style.width = new StyleLength(new Length(35, LengthUnit.Percent));
                depthOffsetFeatureContainer.Add(depthOffsetExpectedLayerPrefix);
                depthOffsetFeatureContainer.Add(depthOffsetExpectedLayer);
                depthOffsetFeatureContainer.Add(depthOffsetExpectedRenderingLayerMask);
                rendererLayerMaskFoldoutGroup.Add(depthOffsetFeatureContainer);   
            }

            // -- Depth Offset Feature End --

            // -- Mesh Outline Feature Start --

            var meshOutlineFeatureContainer = new VisualElement();
            meshOutlineFeatureContainer.style.justifyContent = Justify.SpaceBetween;
            meshOutlineFeatureContainer.style.alignSelf = Align.Stretch;
            meshOutlineFeatureContainer.style.alignItems = Align.Auto;
            meshOutlineFeatureContainer.style.flexDirection = FlexDirection.Row;
            var hasMeshOutlineFeature =
                m_rendererFeatures.Where(e => e.GetType() == typeof(ASPMeshOutlineRendererFeature)).Count() > 0;
            if (hasMeshOutlineFeature)
            {
                var feature =
                    m_rendererFeatures.Find(e => e.GetType() == typeof(ASPMeshOutlineRendererFeature)) as
                        ASPMeshOutlineRendererFeature;
                GetRendererFeatureLayerAndMaskString(renderingLayerMaskNames, feature.m_layer,
                    (int)feature.m_renderingLayerMask, out layerValueString, out renderingLayerMaskString);
            }

            if (hasMeshOutlineFeature)
            {
                var meshOutlineFeatureName = new Label("Mesh Outline Feature");
                meshOutlineFeatureName.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
                meshOutlineFeatureName.style.overflow = new StyleEnum<Overflow>(Overflow.Hidden);
                var meshOutlineFeatureLayerLabel = new Label(layerValueString);
                meshOutlineFeatureLayerLabel.style.width = new StyleLength(new Length(25, LengthUnit.Percent));

                var meshOutlineFeatureRenderingLayerMaskLabel = new Label(renderingLayerMaskString);
                meshOutlineFeatureRenderingLayerMaskLabel.style.width = new StyleLength(new Length(35, LengthUnit.Percent));
                meshOutlineFeatureContainer.Add(meshOutlineFeatureName);
                meshOutlineFeatureContainer.Add(meshOutlineFeatureLayerLabel);
                meshOutlineFeatureContainer.Add(meshOutlineFeatureRenderingLayerMaskLabel);
                rendererLayerMaskFoldoutGroup.Add(meshOutlineFeatureContainer);
            }
        
            // -- Mesh Outline Feature End --

            rendererLayerMaskFoldoutGroup.Add(new IMGUIContainer(DrawHorizontalLine));

            var categoryHintLabel = new VisualElement();
            categoryHintLabel.style.justifyContent = Justify.SpaceBetween;
            categoryHintLabel.style.alignSelf = Align.Stretch;
            categoryHintLabel.style.alignItems = Align.Auto;
            categoryHintLabel.style.flexDirection = FlexDirection.Row;
            categoryHintLabel.style.marginTop = 10;
            var layerMaskRendererLabel = new Label("Renderer");
            var layerMaskValueLabel = new Label("Layer");
            var renderingLayerMaskLabel = new Label("Rendering Layer Mask");
            layerMaskRendererLabel.text = "Renderer";
            layerMaskRendererLabel.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            layerMaskRendererLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            layerMaskRendererLabel.style.whiteSpace = WhiteSpace.Normal;
       
            layerMaskValueLabel.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            layerMaskValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            layerMaskValueLabel.style.whiteSpace = WhiteSpace.Normal;
        
            renderingLayerMaskLabel.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            renderingLayerMaskLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            renderingLayerMaskLabel.style.whiteSpace = WhiteSpace.Normal;
        
            categoryHintLabel.Add(layerMaskRendererLabel);
            categoryHintLabel.Add(layerMaskValueLabel);
            categoryHintLabel.Add(renderingLayerMaskLabel);
            rendererLayerMaskFoldoutGroup.Add(categoryHintLabel);

            foreach (var renderer in characterPanel.GetComponentsInChildren<Renderer>())
            {
                var layers = Enumerable.Range(0, 31).Select(index => LayerMask.LayerToName(index))
                    .Where(l => !string.IsNullOrEmpty(l)).ToList();
                if (!layers.Contains(LayerMask.LayerToName(renderer.gameObject.layer)))
                {
                    continue;
                }

                var item = new VisualElement();
                item.style.justifyContent = Justify.SpaceBetween;
                item.style.alignSelf = Align.Stretch;
                item.style.alignItems = Align.Auto;
                item.style.flexDirection = FlexDirection.Row;

                var nameField = new ObjectField();
                nameField.style.width = new StyleLength(new Length(20, LengthUnit.Percent));
                nameField.value = renderer;
                nameField.SetEnabled(true);
                item.Add(nameField);

                var layerMaskField = new PopupField<string>(layers, LayerMask.LayerToName(renderer.gameObject.layer));
                layerMaskField.style.width = new StyleLength(new Length(35, LengthUnit.Percent));
                layerMaskField.RegisterCallback<ChangeEvent<string>>(e =>
                {
                    renderer.gameObject.layer = LayerMask.NameToLayer(layerMaskField.value);
                });
                item.Add(layerMaskField);
                int maskCount = (int)Mathf.Log((int)renderer.renderingLayerMask, 2) + 1;
                if (renderingLayerMaskNames.Length < maskCount && maskCount <= 32)
                {
                    var newRenderingLayerMaskNames = new string[maskCount];
                    for (int i = 0; i < maskCount; ++i)
                    {
                        newRenderingLayerMaskNames[i] = i < renderingLayerMaskNames.Length
                            ? renderingLayerMaskNames[i]
                            : $"Unused Layer {i}";
                    }

                    renderingLayerMaskNames = newRenderingLayerMaskNames;
                }

                var renderingLayerMaskField =
                    new MaskField(renderingLayerMaskNames.ToList(), (int)renderer.renderingLayerMask);
                renderingLayerMaskField.style.width = new StyleLength(new Length(35, LengthUnit.Percent));
                renderingLayerMaskField.RegisterCallback<ChangeEvent<int>>(e =>
                {
                    renderer.renderingLayerMask = (uint)e.newValue;
                });
                item.Add(renderingLayerMaskField);

                rendererLayerMaskFoldoutGroup.Add(item);
            }

            root.Add(rendererLayerMaskFoldoutGroup);
        }
    }
}