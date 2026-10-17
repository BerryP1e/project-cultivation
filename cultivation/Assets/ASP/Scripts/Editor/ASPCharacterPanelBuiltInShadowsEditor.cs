using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ASP;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEditor.UIElements;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace ASP.Scripts.Editor
{
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        public class RefreshColorEvent : EventBase<RefreshColorEvent>
        {
            public bool State;

            public static RefreshColorEvent GetPooled(bool state)
            {
                var evt = RefreshColorEvent.GetPooled();
                evt.State = state;
                return evt;
            }
        }

        private RefreshColorEvent m_refreshButtonColorEvent;

        partial void DrawBuiltInShadowCastInfo(VisualElement root, ASPCharacterPanel characterPanel)
        {
            var builtInShadowFoldoutGroup = new Foldout();
            
            var builtInCastItemContainer = new VisualElement();
            builtInCastItemContainer.style.justifyContent = Justify.SpaceBetween;
            builtInCastItemContainer.style.alignSelf = Align.Stretch;
            builtInCastItemContainer.style.alignItems = Align.Auto;
            builtInCastItemContainer.style.flexDirection = FlexDirection.Row;
            builtInShadowFoldoutGroup.Q<Foldout>().text = "Built-In Shadow Casting Behaviours";
            builtInShadowFoldoutGroup.Q<Foldout>().style.fontSize = 15;
            builtInShadowFoldoutGroup.Q<Foldout>().value = false;
              
            var builtInCastRenderer = new Label("Renderer");
            builtInCastRenderer.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            var buildInCastButtonLabel = new Label("Cast Shadow");
            buildInCastButtonLabel.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            builtInCastItemContainer.Add(builtInCastRenderer);
            builtInCastItemContainer.Add(buildInCastButtonLabel);
            builtInShadowFoldoutGroup.Add(builtInCastItemContainer);
            var rendererList = new List<Renderer>();
            foreach (var renderer in characterPanel.GetComponentsInChildren<Renderer>())
            {
                if(renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name.Contains("Hidden"))
                    continue;
                rendererList.Add(renderer);
                var state = renderer.shadowCastingMode == ShadowCastingMode.On;
                var item = new VisualElement();
                item.style.justifyContent = Justify.SpaceBetween;
                item.style.alignSelf = Align.Stretch;
                item.style.alignItems = Align.Auto;
                item.style.flexDirection = FlexDirection.Row;
                
                var nameField = new ObjectField();
                nameField.style.width = new StyleLength(new Length(45, LengthUnit.Percent));
                nameField.value = renderer;
                item.Add(nameField);
                nameField.SetEnabled(false);
                
                var button = new Button();
                button.style.width = new StyleLength(new Length(45, LengthUnit.Percent));
                item.Add(button);
                item.Q<Button>().style.backgroundColor =
                    state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                item.Q<Button>().RegisterCallback<RefreshColorEvent>(e =>
                {
                    state = renderer.shadowCastingMode == ShadowCastingMode.On;
                    item.Q<Button>().style.backgroundColor =
                        state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                    item.Q<Button>().text = renderer.shadowCastingMode.ToString();
                });
                item.Q<Button>().RegisterCallback<ClickEvent>(e =>
                {
                    if (renderer.shadowCastingMode != ShadowCastingMode.On)
                    {
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                    }
                    else
                    {
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    state = renderer.shadowCastingMode == ShadowCastingMode.On;
                    item.Q<Button>().style.backgroundColor =
                        state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                    item.Q<Button>().text = renderer.shadowCastingMode.ToString();
                });
                item.Q<Button>().text = renderer.shadowCastingMode.ToString();
                builtInShadowFoldoutGroup.Q<Foldout>().Add(item);
            }
            
            var toggleAllContainer = new VisualElement();
            toggleAllContainer.style.justifyContent = Justify.FlexEnd;
            toggleAllContainer.style.alignSelf = Align.Stretch;
            toggleAllContainer.style.alignItems = Align.FlexEnd;
            toggleAllContainer.style.marginTop = 10;
            toggleAllContainer.style.flexDirection = FlexDirection.Row;

            var toggleAllBehaviour = new EnumField(ShadowCastingMode.On);
            toggleAllBehaviour.style.width = new StyleLength(new Length(15, LengthUnit.Percent));
            
            var toggleCastOnShadowAllButton = new Button();
            toggleCastOnShadowAllButton.text = "Apply All";
            toggleCastOnShadowAllButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.8f));
            toggleCastOnShadowAllButton.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            toggleCastOnShadowAllButton.RegisterCallback<ClickEvent>(e =>
            {
                foreach (var renderer in characterPanel.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name.Contains("Hidden"))
                        continue;
                    renderer.shadowCastingMode = (ShadowCastingMode)toggleAllBehaviour.value;
                }
                foreach (var button in builtInShadowFoldoutGroup.Query<Button>().ToList())
                {
                    var evt = RefreshColorEvent.GetPooled();
                    evt.target = button;
                    button.SendEvent(evt);
                }
            });
            toggleAllContainer.Add(toggleAllBehaviour);
            toggleAllContainer.Add(toggleCastOnShadowAllButton);
            builtInShadowFoldoutGroup.Add(toggleAllContainer);
            
            root.Add(builtInShadowFoldoutGroup);
        }

        partial void DrawBuiltInShadowReceivedInfo(VisualElement root, ASPCharacterPanel characterPanel)
        {
              var builtInShadowReceivedFoldoutGroup = new Foldout();
            
            var builtInReceiveItemContainer = new VisualElement();
            builtInReceiveItemContainer.style.justifyContent = Justify.SpaceBetween;
            builtInReceiveItemContainer.style.alignSelf = Align.Stretch;
            builtInReceiveItemContainer.style.alignItems = Align.Auto;
            builtInReceiveItemContainer.style.flexDirection = FlexDirection.Row;
            builtInShadowReceivedFoldoutGroup.Add(builtInReceiveItemContainer);
            builtInShadowReceivedFoldoutGroup.Q<Foldout>().text = "Built-In Shadow Receive Behaviours";
            builtInShadowReceivedFoldoutGroup.Q<Foldout>().style.fontSize = 15;
            builtInShadowReceivedFoldoutGroup.Q<Foldout>().value = false;
            
            var builtInReceivedRendererLabel = new Label("Renderer/Material");
            builtInReceivedRendererLabel.style.width = new StyleLength(new Length(45, LengthUnit.Percent));
            var buildInReceivedButtonLabel = new Label("Shadow Received");
            buildInReceivedButtonLabel.style.width = new StyleLength(new Length(45, LengthUnit.Percent));
            builtInReceiveItemContainer.Add(builtInReceivedRendererLabel);
            builtInReceiveItemContainer.Add(buildInReceivedButtonLabel);

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
                    var state = mat.GetFloat("_ReceiveShadows") > 0;
                    var item = new VisualElement();
                    item.style.justifyContent = Justify.SpaceBetween;
                    item.style.alignSelf = Align.Stretch;
                    item.style.alignItems = Align.Auto;
                    item.style.flexDirection = FlexDirection.Row;
                    
                    var parentRendererField = new ObjectField();
                    parentRendererField.style.width = new StyleLength(new Length(20, LengthUnit.Percent));
                    parentRendererField.value = renderer;
                    parentRendererField.SetEnabled(false);
                    item.Add(parentRendererField);
                    
                    var nameField = new ObjectField();
                    nameField.style.width = new StyleLength(new Length(20, LengthUnit.Percent));
                    nameField.value = mat;
                    nameField.SetEnabled(false);
                    item.Add(nameField);
                
                    var button = new Button();
                    button.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
                    item.Add(button);
                    
                    item.Q<Button>().style.backgroundColor =
                        state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                    
                    item.Q<Button>().RegisterCallback<ClickEvent>(e =>
                    {
                        var newValue = !(mat.GetFloat("_ReceiveShadows") > 0);
                        SetPropertyFloat(mat, "_ReceiveShadows", newValue);
                        SetKeyword(mat, "_RECEIVE_SHADOWS_OFF", !newValue);
                        
                        state = mat.GetFloat("_ReceiveShadows") > 0;
                        item.Q<Button>().style.backgroundColor =
                            state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                        item.Q<Button>().text = state ? "Received" : "Not Received";
                    });
                    
                    item.Q<Button>().RegisterCallback<RefreshColorEvent>(e =>
                    {
                        var newValue = e.State;
                        SetPropertyFloat(mat, "_ReceiveShadows", newValue);
                        SetKeyword(mat, "_RECEIVE_SHADOWS_OFF", !newValue);
                        
                        state = mat.GetFloat("_ReceiveShadows") > 0;
                        item.Q<Button>().style.backgroundColor =
                            state ? new Color(0.2f, 0.6f, 0.2f) : Color.grey;
                        item.Q<Button>().text = state ? "Received" : "Not Received";
                    });
                    item.Q<Button>().text = state ? "Received" : "Not Received";
                    builtInShadowReceivedFoldoutGroup.Q<Foldout>().Add(item);
                }
            }
            
            var applyAllContainer = new VisualElement();
            applyAllContainer.style.justifyContent = Justify.FlexEnd;
            applyAllContainer.style.alignSelf = Align.Stretch;
            applyAllContainer.style.alignItems = Align.FlexEnd;
            applyAllContainer.style.marginTop = 10;
            applyAllContainer.style.flexDirection = FlexDirection.Row;

            var applyAllBehaviour = new PopupField<string>("", new List<string>(){"Received", "Not Received"}, 0);
            applyAllBehaviour.style.width = new StyleLength(new Length(20, LengthUnit.Percent));
            
            var applyAllButton = new Button();
            applyAllButton.text = "Apply All";
            applyAllButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.8f));
            applyAllButton.style.width = new StyleLength(new Length(30, LengthUnit.Percent));
            applyAllButton.RegisterCallback<ClickEvent>(e =>
            {
                foreach (var button in builtInShadowReceivedFoldoutGroup.Query<Button>().ToList())
                {
                    if(button == applyAllButton)
                        continue;
                    var evt = RefreshColorEvent.GetPooled(applyAllBehaviour.index == 0);
                    evt.target = button;
                    button.SendEvent(evt);
                }
            });
            applyAllContainer.Add(applyAllBehaviour);
            applyAllContainer.Add(applyAllButton);
            builtInShadowReceivedFoldoutGroup.Add(applyAllContainer);
            
            root.Add(builtInShadowReceivedFoldoutGroup);
        }
    }
}