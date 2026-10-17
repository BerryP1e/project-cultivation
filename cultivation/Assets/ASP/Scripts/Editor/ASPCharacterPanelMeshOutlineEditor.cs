using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Slider = UnityEngine.UIElements.Slider;

namespace ASP.Scripts.Editor
{
    partial class ASPCharacterPanelEditor : UnityEditor.Editor
    {
        private void UpdateMaterialOutlineProperty(ASPCharacterPanel characterPanel)
        {
            foreach (var renderer in characterPanel.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if(mat == null || mat.shader == null)
                        continue;
                    if (!(mat.shader.name.Equals("ASP/Character") || mat.shader.name.Equals("ASP/Eye")))
                    {
                        continue;
                    }
                    SetKeyword(mat, "_USE_BAKED_NORAML",
                        characterPanel.CurrentBackFaceOutlineMethod == ASPCharacterPanel.BackFaceOutlineMethod.FROM_UV4);
                    mat.SetColor("_OutlineColor", characterPanel.BackfaceOutlineColor);
                    mat.SetFloat("_ScaleAsScreenSpaceOutline", characterPanel.ScaleWidthAsScreenSpaceOutline);
                    mat.SetFloat("_OutlineWidth", characterPanel.BackFaceOutlineWidth);
                    mat.SetVector("_OutlineDistancFade", characterPanel.BackFaceOutlineFadeStartEnd);
                }
            }
        }

        partial void DrawMeshOutlineParam(VisualElement root, ASPCharacterPanel characterPanel, SerializedObject serializedObject)
        {
            var hasFeature = m_rendererFeatures.Where(e => e.GetType() == typeof(ASPMeshOutlineRendererFeature)).Count() > 0;
            if (!hasFeature)
            {
                root.Add(new IMGUIContainer(() =>
                {
                    EditorGUILayout.HelpBox(
                        $"The mesh outline feature is not included the URP asset.",
                        MessageType.Warning);
                }));
                return;
            }
        
            var foldout = new Foldout();
 
            foldout.text = "Expand MeshOutline Params";
            foldout.RegisterCallback<ChangeEvent<bool>>((e) =>
            {
                if (e.newValue == true)
                {
                    foldout.text = "Collapse MeshOutline Params";
                }
                else
                {
                    foldout.text = "Expand MeshOutline Params";
                }
            });
            //  foldout.style.justifyContent = Justify.FlexStart;
            //   foldout.style.alignItems = Align.FlexStart;
            //   foldout.style.flexDirection = FlexDirection.Row;
            root.Add(foldout);

            var currentOutlineExtrudeMethodLabel =
                new Label("Current Extrude Method : " + characterPanel.CurrentBackFaceOutlineMethod);
            currentOutlineExtrudeMethodLabel.name = "OutlineExtrudeMethod";
            currentOutlineExtrudeMethodLabel.style.marginTop = 5;
            foldout.Add(currentOutlineExtrudeMethodLabel);
        
            var container = new VisualElement();
            container.style.marginTop = 5;
            container.style.justifyContent = Justify.FlexStart;
            container.style.alignItems = Align.FlexStart;
            container.style.flexDirection = FlexDirection.Row;

            var outlineMethodProperty = serializedObject.FindProperty("CurrentBackFaceOutlineMethod");
            var outlineExtrudeMethod = new EnumField((ASPCharacterPanel.BackFaceOutlineMethod)outlineMethodProperty.enumValueIndex);
            outlineExtrudeMethod.style.width = new StyleLength(new Length( 80, LengthUnit.Percent));
            var applyMethodButton = new Button();
            applyMethodButton.text = "Apply";
            applyMethodButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.8f));
            applyMethodButton.style.width= new StyleLength(new Length( 20, LengthUnit.Percent));
            applyMethodButton.RegisterCallback<ClickEvent>(e =>
            {
                outlineMethodProperty.enumValueIndex =
                    (int)(ASPCharacterPanel.BackFaceOutlineMethod)outlineExtrudeMethod.value;
            
                serializedObject.ApplyModifiedProperties();
                foldout.Q<Label>("OutlineExtrudeMethod").text ="Current Extude Method : " +
                                                               characterPanel.CurrentBackFaceOutlineMethod;
                UpdateMaterialOutlineProperty(characterPanel);
            });
            container.Add(outlineExtrudeMethod);
            container.Add(applyMethodButton);
            foldout.Add(container);

            var outlineColorProperty = serializedObject.FindProperty("BackfaceOutlineColor");
            var outlineColorPicker = new ColorField("Mesh Outline Color");
            outlineColorPicker.value = outlineColorProperty.colorValue;
            outlineColorPicker.style.marginTop = 10;
            outlineColorPicker.RegisterCallback<ChangeEvent<Color>>(e =>
            {
                outlineColorProperty.colorValue = e.newValue;
                serializedObject.ApplyModifiedProperties();
                UpdateMaterialOutlineProperty(characterPanel);
            });

            var outlineWidthProperty = serializedObject.FindProperty("BackFaceOutlineWidth");
            var outlineWidthPicker = new Slider("Mesh Outline Width", 0, 30);
            outlineWidthPicker.showInputField = true;
            outlineWidthPicker.style.marginTop = 10;
            outlineWidthPicker.value = outlineWidthProperty.floatValue;
            outlineWidthPicker.RegisterCallback<ChangeEvent<float>>(e =>
            {
                outlineWidthPicker.value = e.newValue;
                outlineWidthProperty.floatValue = e.newValue;
                serializedObject.ApplyModifiedProperties();
                UpdateMaterialOutlineProperty(characterPanel);
            });
        
        
            var fadeOutStartEndProperty = serializedObject.FindProperty("BackFaceOutlineFadeStartEnd");
            var fadeOutStartEndPicker = new Vector2Field("Fade Out Start End Distance");
            fadeOutStartEndPicker.value = fadeOutStartEndProperty.vector2Value;
            fadeOutStartEndPicker.style.marginTop = 10;
            fadeOutStartEndPicker.RegisterCallback<ChangeEvent<Vector2>>(e =>
            {
                fadeOutStartEndProperty.vector2Value = e.newValue;
                new SerializedObject(characterPanel).ApplyModifiedProperties();
                serializedObject.ApplyModifiedProperties();
                UpdateMaterialOutlineProperty(characterPanel);
            });
            
            var scaleWidthAsScreenSpaceOutline = serializedObject.FindProperty("ScaleWidthAsScreenSpaceOutline");
            var scaleWidthAsScreenSpaceOutlineToggle = new Toggle("Make mesh-based outline scale behave as screen-space outline");
            scaleWidthAsScreenSpaceOutlineToggle.value = scaleWidthAsScreenSpaceOutline.floatValue > 0;
            scaleWidthAsScreenSpaceOutlineToggle.style.marginTop = 10;
            scaleWidthAsScreenSpaceOutlineToggle.RegisterCallback<ChangeEvent<bool>>(e =>
            {
                scaleWidthAsScreenSpaceOutline.floatValue = e.newValue ? 1.0f : 0.0f;
                new SerializedObject(characterPanel).ApplyModifiedProperties();
                serializedObject.ApplyModifiedProperties();
                UpdateMaterialOutlineProperty(characterPanel);
            });
            
            foldout.Add(outlineColorPicker);
            foldout.Add(outlineWidthPicker);
            foldout.Add(fadeOutStartEndPicker);
            foldout.Add(scaleWidthAsScreenSpaceOutlineToggle);
            foldout.Add(new IMGUIContainer(()=>DrawHorizontalLine()));
            foldout.Add(new IMGUIContainer(()=>DrawBakeSmoothNormalFieldIMGUI(characterPanel)));
        }
    }
}