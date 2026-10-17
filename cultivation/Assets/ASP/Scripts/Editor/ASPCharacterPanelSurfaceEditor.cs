using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ASP.Scripts.Editor
{
    
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        partial void DrawASPSurfaceInfo(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject)
        {
            var foldout = new Foldout();
            foldout.Q<Foldout>().text = "Surfaces";
            foldout.Q<Foldout>().style.fontSize = 15;
            foldout.Q<Foldout>().value = true;
            root.Add(foldout);
            
            /*var toggleMaterialInstanceOnPlay = new Toggle("Create Material Instance On Play");
            toggleMaterialInstanceOnPlay.tooltip =
                "Toggle this if you will instantiate multiple asp character via prefab or copy object on scene";
            var createMaterialInstanceOnPlayProperty = serializedObject.FindProperty("CreateMaterialInstanceOnPlay");
            toggleMaterialInstanceOnPlay.value = createMaterialInstanceOnPlayProperty.boolValue;
            toggleMaterialInstanceOnPlay.RegisterCallback<ChangeEvent<bool>>(e =>
            {
                createMaterialInstanceOnPlayProperty.boolValue =
                    e.newValue;
                serializedObject.ApplyModifiedProperties();
                
            });
            foldout.Add(toggleMaterialInstanceOnPlay);
            
            var createMaterialInstanceNow = new Button();
            createMaterialInstanceNow.text = "Create Material Instances Now";
            createMaterialInstanceNow.RegisterCallback<ClickEvent>(e =>
            {
               characterPanel.CreateMaterialInstance();

            });
            foldout.Add(createMaterialInstanceNow);*/
        
            var categoryContainer = new VisualElement();
            categoryContainer.style.justifyContent = Justify.SpaceBetween;
            categoryContainer.style.alignSelf = Align.Stretch;
            categoryContainer.style.alignItems = Align.Auto;
            categoryContainer.style.flexDirection = FlexDirection.Row;

        
            var categoryMatLabel = new Label("Material");
            categoryContainer.Add(categoryMatLabel);
        
            var categoryTypeLabel = new Label("Type");
            categoryContainer.Add(categoryTypeLabel);
        
            var categoryAlphaClipLabel = new Label("AlphaClip");
            categoryContainer.Add(categoryAlphaClipLabel);
        
            var categoryRenderFaceLabel = new Label("RenderFace");
            categoryContainer.Add(categoryRenderFaceLabel);
        
            /*var categoryFovAdjustLabel = new Label("FOV Adjustment");
            categoryContainer.Add(categoryFovAdjustLabel);*/
        
        
            var totalWidthPercent = 100.0f;
            var perChildWidthPercent = totalWidthPercent / categoryContainer.childCount;
            foreach (var child in categoryContainer.Children())
            {
                child.Q<Label>().style.whiteSpace = WhiteSpace.Normal;
                child.Q<Label>().style.textOverflow = TextOverflow.Clip;
                child.style.width = new StyleLength(new Length(perChildWidthPercent, LengthUnit.Percent));
            }
        
            foldout.Add(categoryContainer);
            var materialList = new List<Material>();
        
            foreach (var renderer in characterPanel.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if(mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")) || mat.shader.name.Contains("Hidden"))
                    {
                        continue;
                    }
                    if(materialList.Contains(mat))
                        continue;
                    materialList.Add(mat);
                    var item = new VisualElement();
                    item.style.justifyContent = Justify.SpaceBetween;
                    item.style.alignSelf = Align.Stretch;
                    item.style.alignItems = Align.Auto;
                    item.style.flexDirection = FlexDirection.Row;
                
                    var nameField = new ObjectField();
                    nameField.style.width = new StyleLength(new Length(perChildWidthPercent, LengthUnit.Percent));
                    nameField.value = mat;
                    nameField.SetEnabled(true);
                    item.Add(nameField);

                    var isTransparent = mat.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
                    var surfaceTypeLabel = new Label((isTransparent ? "Transparent" : "Opaque"));
                    surfaceTypeLabel.style.width = new StyleLength(new Length(perChildWidthPercent, LengthUnit.Percent));
                    item.Add(surfaceTypeLabel);

                    var isAlphaClip = mat.IsKeywordEnabled("_ALPHATEST_ON") ? "On" : "Off";
                    var alphaClipLabel = new Label(isAlphaClip);
                    alphaClipLabel.style.width = new StyleLength(new Length(perChildWidthPercent, LengthUnit.Percent));
                    alphaClipLabel.style.color = mat.IsKeywordEnabled("_ALPHATEST_ON") ? Color.green : alphaClipLabel.style.backgroundColor;
                    item.Add(alphaClipLabel);
                
                    var renderFaceStr = "";
                    switch ((int)mat.GetFloat("_Cull"))
                    {
                        case 0:
                            renderFaceStr = "Both";
                            break;
                        case 1:
                            renderFaceStr = "Back";
                            break;
                        default:
                            renderFaceStr = "Front";
                            break;
                    }
                    var renderFaceLabel = new Label(renderFaceStr);
                    renderFaceLabel.style.width = new StyleLength(new Length(perChildWidthPercent, LengthUnit.Percent));
                    item.Add(renderFaceLabel);
                
                    /*var fovAdjustLabel = new Slider(0, 1);
                    fovAdjustLabel.value = mat.GetFloat("_FOVShiftX");
                    fovAdjustLabel.showInputField = true;
                    fovAdjustLabel.name = "INDIVIDUAL_FOV_SLIDER";
                    fovAdjustLabel.style.width = new StyleLength(new Length(perChildWidthPercent, LengthUnit.Percent));
                    fovAdjustLabel.RegisterCallback<ChangeEvent<float>>(e =>
                    {
                        mat.SetFloat("_FOVShiftX", e.newValue);
                    });
                    item.Add(fovAdjustLabel);*/
                    foldout.Add(item);
                }
            }
        
            var categoryDitheringLabel = new Label("Dithering Factor");
            categoryDitheringLabel.style.marginTop = 5;
            foldout.Add(categoryDitheringLabel);
        
            var ditheringFactorSlider = new Slider(0, 1);
            ditheringFactorSlider.showInputField = true;
            if (materialList.Count > 0)
            {
                ditheringFactorSlider.value = materialList.First().GetFloat("_Dithering");
            }

            ditheringFactorSlider.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            ditheringFactorSlider.RegisterCallback<ChangeEvent<float>>(e =>
            {
                characterPanel.SetDitheringValueToAllMaterials(e.newValue);
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                }
            });
            foldout.Add(ditheringFactorSlider);
        
            var categoryDitheringSizeLabel = new Label("Dithering Size");
            categoryDitheringSizeLabel.style.marginTop = 5;
            foldout.Add(categoryDitheringSizeLabel);
        
            var ditheringSizeSlider = new SliderInt(1, 20);
            ditheringSizeSlider.showInputField = true;
            if (materialList.Count > 0)
            {
                ditheringSizeSlider.value = (int)materialList.First().GetFloat("_DitherTexelSize");
            }
            ditheringSizeSlider.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            ditheringSizeSlider.RegisterCallback<ChangeEvent<int>>(e =>
            {
                characterPanel.SetDitheringSizeValueToAllMaterials((float)e.newValue);
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                }
            });
            foldout.Add(ditheringSizeSlider);
        
            var fovAdjustAllLabel = new Label("Set FOV Adjust On All");
            fovAdjustAllLabel.style.marginTop = 25;
            foldout.Add(fovAdjustAllLabel);
        
            var fovAdjustAllSlider = new Slider(0, 1);
            if (materialList.Count > 0)
            {
                fovAdjustAllSlider.value = materialList.First().GetFloat("_FOVShiftX");
            }
            fovAdjustAllSlider.showInputField = true;
            fovAdjustAllSlider.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            fovAdjustAllSlider.RegisterCallback<ChangeEvent<float>>(e =>
            {
                characterPanel.SetFOVAdjustValueToAllMaterials(e.newValue);
                foreach (var children in foldout.Query<Slider>("INDIVIDUAL_FOV_SLIDER").ToList())
                {
                    children.value = (e.newValue);
                }
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                }
            });
            foldout.Add(fovAdjustAllSlider);

            var characterCenterInfo = new Label();
            characterCenterInfo.style.width = new StyleLength(new Length( 100, LengthUnit.Percent));
            characterCenterInfo.text = "Character World Position Center : "+ (characterPanel.transform.position + characterPanel.CenterPositionOffset);
            foldout.Add(characterCenterInfo);
            
            var characterCenterOffset = serializedObject.FindProperty("CenterPositionOffset");
            var characterCenterOffsetField = new Vector3Field("Center Position Offset");
            characterCenterOffsetField.SetValueWithoutNotify(characterPanel.CenterPositionOffset);
            characterCenterOffsetField.style.width = new StyleLength(new Length( 100, LengthUnit.Percent));
            characterCenterOffsetField.style.marginLeft = -0.5f;
            characterCenterOffsetField.RegisterCallback<ChangeEvent<Vector3>>(e =>
            {
                characterCenterOffset.vector3Value =
                    e.newValue;
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
                characterCenterInfo.text = "Character World Position Center : "+ (characterPanel.transform.position + characterPanel.CenterPositionOffset);
            });
            foldout.Add(characterCenterOffsetField);

            var DebugLabel = new Label("====Debug Options===");
            DebugLabel.style.marginTop = 25;
            DebugLabel.style.alignSelf = Align.Center;
            foldout.Add(DebugLabel);
        
            var toggleDebugGI = new Toggle("Toggle Debug GI Color");
            var anyDebugGIEnable = false;
            foreach (var renderer in characterPanel.GetComponentsInChildren<Renderer>())
            {
                Material[] mats = Application.isPlaying ? renderer.materials : renderer.sharedMaterials;
                foreach (var mat in mats)
                {
                    if (mat == null || mat.shader == null)
                        continue;
                    if (!mat.shader.name.Equals("ASP/Character"))
                    {
                        continue;
                    }

                    if (mat.GetFloat("_DebugGI") > 0)
                    {
                        anyDebugGIEnable = true;
                        break;
                    }
                }
            }

            toggleDebugGI.value = anyDebugGIEnable;
            toggleDebugGI.RegisterCallback<ChangeEvent<bool>>(e =>
            {
                characterPanel.SetDebugGIFlagToAllMaterials(e.newValue ? 1.0f : 0.0f);
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    UnityEditor.SceneView.RepaintAll();
                }
            });
            foldout.Add(toggleDebugGI);
        }
    
    
    }
}