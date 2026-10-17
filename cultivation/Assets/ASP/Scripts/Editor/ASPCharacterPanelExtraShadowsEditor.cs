using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ASP.Scripts.Editor
{
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        partial void DrawASPExtraShadowReceivedInfo(VisualElement root, ASPCharacterPanel characterPanel)
        {
            var aspShadowReceivedFoldoutGroup = new Foldout();
            var aspShadowReceivedTitleContainer = new VisualElement();
            aspShadowReceivedTitleContainer.style.justifyContent = Justify.SpaceBetween;
            aspShadowReceivedTitleContainer.style.alignSelf = Align.Stretch;
            aspShadowReceivedTitleContainer.style.alignItems = Align.Auto;
            aspShadowReceivedTitleContainer.style.flexDirection = FlexDirection.Row;
            aspShadowReceivedFoldoutGroup.Add(aspShadowReceivedTitleContainer);
            aspShadowReceivedFoldoutGroup.Q<Foldout>().text = "ASP Extra Shadow Behaviours";
            aspShadowReceivedFoldoutGroup.Q<Foldout>().style.fontSize = 15;
            aspShadowReceivedFoldoutGroup.Q<Foldout>().value = false;
        
            var aspShadowMaterialTabLabel = new Label("Material");
            var aspShadowReceivedCharacterShadowLabel = new Label("Character-Only ShadowMap");
            var aspShadowReceivedOffsetShadowLabel = new Label("Depth Offset Shadow");
        
            aspShadowReceivedOffsetShadowLabel.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
            aspShadowReceivedCharacterShadowLabel.style.whiteSpace = new StyleEnum<WhiteSpace>(WhiteSpace.Normal);
            aspShadowMaterialTabLabel.style.width = new StyleLength(new Length(20, LengthUnit.Percent));
            aspShadowReceivedCharacterShadowLabel.style.width = new StyleLength(new Length(35, LengthUnit.Percent));
            aspShadowReceivedOffsetShadowLabel.style.width = new StyleLength(new Length(35, LengthUnit.Percent));
        
            aspShadowReceivedTitleContainer.Add(aspShadowMaterialTabLabel);
            aspShadowReceivedTitleContainer.Add(aspShadowReceivedCharacterShadowLabel);
            aspShadowReceivedTitleContainer.Add(aspShadowReceivedOffsetShadowLabel);
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
                    var state = mat.GetFloat("_ReceiveASPShadow") > 0 && mat.IsKeywordEnabled("_RECEIVE_ASP_SHADOW");
                    var item = new VisualElement();
                    item.style.justifyContent = Justify.SpaceBetween;
                    item.style.alignSelf = Align.Stretch;
                    item.style.alignItems = Align.Auto;
                    item.style.flexDirection = FlexDirection.Row;
                
                    var nameField = new ObjectField();
                    nameField.style.width = new StyleLength(new Length(20, LengthUnit.Percent));
                    nameField.value = mat;
                    nameField.SetEnabled(true);
                    item.Add(nameField);
                
                    var shadowMapButton = new Button();
                    shadowMapButton.name = "ShadowMap";
                    shadowMapButton.style.width = new StyleLength(new Length(36, LengthUnit.Percent));
                    item.Add(shadowMapButton);
                
                    shadowMapButton.style.backgroundColor =
                        state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                
                    shadowMapButton.RegisterCallback<RefreshColorEvent>(e =>
                    {
                        var newValue = e.State;
                        SetPropertyFloat(mat, "_ReceiveASPShadow", newValue);
                        SetKeyword(mat, "_RECEIVE_ASP_SHADOW", newValue);
                    
                        state = mat.GetFloat("_ReceiveASPShadow") > 0;
                        shadowMapButton.style.backgroundColor =
                            state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                        shadowMapButton.text = state ? "Received" : "Not Received";
                    });
                
                    shadowMapButton.RegisterCallback<ClickEvent>(e =>
                    {
                        var newValue = !(mat.GetFloat("_ReceiveASPShadow") > 0);
                        SetPropertyFloat(mat, "_ReceiveASPShadow", newValue);
                        SetKeyword(mat, "_RECEIVE_ASP_SHADOW", newValue);
                    
                        state = mat.GetFloat("_ReceiveASPShadow") > 0;
                        shadowMapButton.style.backgroundColor =
                            state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                        shadowMapButton.text = state ? "Received" : "Not Received";
                    });
                    shadowMapButton.text = state ? "Received" : "Not Received";
                
                    var offsetShadowButton = new Button();
                    offsetShadowButton.name = "OffsetShadow";
                    offsetShadowButton.style.width = new StyleLength(new Length(36, LengthUnit.Percent));
                    item.Add(offsetShadowButton);
                
                    state = mat.GetFloat("_ReceiveOffsetedDepthMap") > 0 && mat.IsKeywordEnabled("_RECEIVE_OFFSETED_SHADOW_ON");
                    offsetShadowButton.style.backgroundColor =
                        state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;

                    offsetShadowButton.RegisterCallback<RefreshColorEvent>(e =>
                    {
                        var newValue = e.State;
                        SetPropertyFloat(mat, "_ReceiveOffsetedDepthMap", newValue);
                        SetKeyword(mat, "_RECEIVE_OFFSETED_SHADOW_ON", newValue);
                    
                        state = mat.GetFloat("_ReceiveOffsetedDepthMap") > 0;
                        offsetShadowButton.style.backgroundColor =
                            state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                        offsetShadowButton.text = state ? "Received" : "Not Received";
                    });
                
                    offsetShadowButton.RegisterCallback<ClickEvent>(e =>
                    {
                        var newValue = !(mat.GetFloat("_ReceiveOffsetedDepthMap") > 0);
                        SetPropertyFloat(mat, "_ReceiveOffsetedDepthMap", newValue);
                        SetKeyword(mat, "_RECEIVE_OFFSETED_SHADOW_ON", newValue);
                    
                        state = mat.GetFloat("_ReceiveOffsetedDepthMap") > 0;
                        offsetShadowButton.style.backgroundColor =
                            state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                        offsetShadowButton.text = state ? "Received" : "Not Received";
                    });
                    offsetShadowButton.text = state ? "Received" : "Not Received";
                
                    aspShadowReceivedFoldoutGroup.Add(item);
                }
            }
        
            var applyAllContainer = new VisualElement();
            applyAllContainer.style.justifyContent = Justify.FlexEnd;
            applyAllContainer.style.alignSelf = Align.Stretch;
            applyAllContainer.style.alignItems = Align.FlexEnd;
            applyAllContainer.style.marginTop = 10;
            applyAllContainer.style.flexDirection = FlexDirection.Row;

            var applyAllShadowMapBehaviour = new PopupField<string>("", new List<string>(){"Received", "Not Received"}, 0);
            applyAllShadowMapBehaviour.style.width = new StyleLength(new Length(18, LengthUnit.Percent));
        
            var applyAllShadowMapButton = new Button();
            applyAllShadowMapButton.text = "Apply All";
            applyAllShadowMapButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.8f));
            applyAllShadowMapButton.style.width = new StyleLength(new Length(18, LengthUnit.Percent));
            applyAllShadowMapButton.RegisterCallback<ClickEvent>(e =>
            {
                foreach (var button in aspShadowReceivedFoldoutGroup.Query<Button>("ShadowMap").ToList())
                {
                    if(button == applyAllShadowMapButton)
                        continue;
                    var evt = RefreshColorEvent.GetPooled(applyAllShadowMapBehaviour.index == 0);
                    evt.target = button;
                    button.SendEvent(evt);
                }
            });
            applyAllContainer.Add(applyAllShadowMapBehaviour);
            applyAllContainer.Add(applyAllShadowMapButton);
        
            var applyAllOffsetShadowBehaviour = new PopupField<string>("", new List<string>(){"Received", "Not Received"}, 0);
            applyAllOffsetShadowBehaviour.style.width = new StyleLength(new Length(18, LengthUnit.Percent));
            applyAllOffsetShadowBehaviour.style.marginLeft = new StyleLength(new Length(3, LengthUnit.Percent));
            var applyAllOffsetShadowButton = new Button();
            applyAllOffsetShadowButton.text = "Apply All";
            applyAllOffsetShadowButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.8f));
            applyAllOffsetShadowButton.style.width = new StyleLength(new Length(18, LengthUnit.Percent));
            applyAllOffsetShadowButton.RegisterCallback<ClickEvent>(e =>
            {
                foreach (var button in aspShadowReceivedFoldoutGroup.Query<Button>("OffsetShadow").ToList())
                {
                    if(button == applyAllOffsetShadowButton)
                        continue;
                    var evt = RefreshColorEvent.GetPooled(applyAllOffsetShadowBehaviour.index == 0);
                    evt.target = button;
                    button.SendEvent(evt);
                }
            });
            applyAllContainer.Add(applyAllOffsetShadowBehaviour);
            applyAllContainer.Add(applyAllOffsetShadowButton);
            aspShadowReceivedFoldoutGroup.Add(applyAllContainer);
        
            root.Add(aspShadowReceivedFoldoutGroup);
        }
    }
}
