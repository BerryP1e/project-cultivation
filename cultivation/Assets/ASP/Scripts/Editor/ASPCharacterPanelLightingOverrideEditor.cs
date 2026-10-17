using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ASP.Scripts.Editor
{
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        partial void DrawLightingDirectionOverrideProperty(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject)
        {
            var container = new VisualElement();
            container.style.marginTop = 5;
            container.style.justifyContent = Justify.FlexStart;
            container.style.alignItems = Align.FlexStart;
            container.style.flexDirection = FlexDirection.Row;

            var lightDirectionOverrideModeProperty = serializedObject.FindProperty("_overrideMode");

            var currentLightOverrideMethodLabel =
                new Label("Current Light Direction Override Method : " + (ASPCharacterPanel.OverrideMode)lightDirectionOverrideModeProperty.enumValueIndex);
            currentLightOverrideMethodLabel.name = "LightOverrideMethodLabel";
            currentLightOverrideMethodLabel.style.marginTop = 5;
            root.Add(currentLightOverrideMethodLabel);
        
            var lightDirectionOverrideField =
                new EnumField((ASPCharacterPanel.OverrideMode)lightDirectionOverrideModeProperty.enumValueIndex);
            lightDirectionOverrideField.style.width = new StyleLength(new Length( 80, LengthUnit.Percent));
            lightDirectionOverrideField.style.marginTop = 5;
            var applyMethodButton = new Button();
            applyMethodButton.text = "Apply";
            applyMethodButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.8f));
            applyMethodButton.style.width= new StyleLength(new Length( 20, LengthUnit.Percent));
            applyMethodButton.RegisterCallback<ClickEvent>(e =>
            {
                lightDirectionOverrideModeProperty.enumValueIndex =
                    (int)(ASPCharacterPanel.OverrideMode)lightDirectionOverrideField.value;

                serializedObject.ApplyModifiedProperties();
                root.Q<Label>("LightOverrideMethodLabel").text = "Current Light Direction Override Method : " +
                                                                 (ASPCharacterPanel.OverrideMode)lightDirectionOverrideModeProperty.enumValueIndex;
                characterPanel.UpdateLightDirectionOverrideParam();
            });

            container.Add(lightDirectionOverrideField);
            container.Add(applyMethodButton);
            root.Add(container);
        
            var lightDirectionEulerProperty = serializedObject.FindProperty("_overrideLightAngle");
            var lightDirectionEulerPicker = new Vector3Field("Override Light Euler Angle");
            lightDirectionEulerPicker.style.marginTop = 10;
            lightDirectionEulerPicker.value = lightDirectionEulerProperty.vector3Value;
            lightDirectionEulerPicker.RegisterCallback<ChangeEvent<Vector3>>(e =>
            {
                lightDirectionEulerProperty.vector3Value = e.newValue;
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
            });
            root.Add(lightDirectionEulerPicker);
            
            var headBoneTransformProperty = serializedObject.FindProperty("HeadBoneTransform");
            var headBonePropertyField = new PropertyField(headBoneTransformProperty);
            headBonePropertyField.tooltip =
                "Bind Head bone for accurate face shadow map calculation, otherwise the face shadow using character's object direction.";
            headBonePropertyField.style.marginTop = 10;
            headBonePropertyField.RegisterCallback<ChangeEvent<Transform>>(e =>
            {
                serializedObject.ApplyModifiedProperties();
                characterPanel.UpdateLightDirectionOverrideParam();
            });
            root.Add(headBonePropertyField);
        }
        
         partial void DrawMainLightingPropertyOverrideProperty(VisualElement root, ASPCharacterPanel characterPanel,
            SerializedObject serializedObject)
        {
            var container = new VisualElement();
            container.style.marginTop = 5;
            container.style.justifyContent = Justify.FlexStart;
            container.style.alignItems = Align.FlexStart;
            container.style.flexDirection = FlexDirection.Row;

            var lightColorOverrideModeProperty = serializedObject.FindProperty("_mainLightColorOverride");
            var lightColorOverrideField = new PropertyField(lightColorOverrideModeProperty, "Override Color & Intensity");
            lightColorOverrideField.style.width = new StyleLength(new Length( 80, LengthUnit.Percent));
            lightColorOverrideField.style.marginTop = 5;
            container.Add(lightColorOverrideField);
            root.Add(container);
            
            lightColorOverrideField.RegisterCallback<ChangeEvent<bool>>(e =>
            {
                serializedObject.ApplyModifiedProperties();
            });
            
            var lightColorOverrideValueProperty = serializedObject.FindProperty("_mainLightColorOverrideValue");
            var lightColorOverrideValuePropertyField = new PropertyField(lightColorOverrideValueProperty, "Color");
            lightColorOverrideValuePropertyField.style.width = new StyleLength(new Length( 80, LengthUnit.Percent));
            lightColorOverrideValuePropertyField.style.marginTop = 5;
            root.Add(lightColorOverrideValuePropertyField);
            
            lightColorOverrideValuePropertyField.RegisterCallback<ChangeEvent<Color>>(e =>
            {
                serializedObject.ApplyModifiedProperties();
            });
            
            var lightStrengthOverrideValueProperty = serializedObject.FindProperty("_mainLightStrengthOverrideValue");
            var lightStrengthOverrideValuePropertyField = new PropertyField(lightStrengthOverrideValueProperty, "Intensity");
            lightStrengthOverrideValuePropertyField.style.width = new StyleLength(new Length( 80, LengthUnit.Percent));
            lightStrengthOverrideValuePropertyField.style.marginTop = 5;
            root.Add(lightStrengthOverrideValuePropertyField);
            
            lightStrengthOverrideValuePropertyField.RegisterCallback<ChangeEvent<float>>(e =>
            {
                serializedObject.ApplyModifiedProperties();
            });
        }
    }
}