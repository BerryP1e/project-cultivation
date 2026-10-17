using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
namespace ASP.Scripts.Editor
{
    [CustomPropertyDrawer(typeof(RenderingLayerMaskAttribute))]
    public class RenderingLayerMaskDrawer : PropertyDrawer
    {
        public static void DrawRenderingLayerMask(SerializedProperty property)
        {
            int renderingLayer = property.intValue;
            RenderPipelineAsset renderPipelineAsset = null;
#if UNITY_6000_0_OR_NEWER
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                renderPipelineAsset = GraphicsSettings.defaultRenderPipeline;
            }
#else
            if (GraphicsSettings.renderPipelineAsset != null)
            {
                renderPipelineAsset = GraphicsSettings.renderPipelineAsset;
            }
#endif
            else if (QualitySettings.renderPipeline != null)
            {
                renderPipelineAsset = QualitySettings.renderPipeline;
            }
            else
            {
                return;
            }

            string[] renderingLayerMaskNames = renderPipelineAsset.renderingLayerMaskNames;
            int maskCount = (int)Mathf.Log(renderingLayer, 2) + 1;
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

                EditorGUILayout.HelpBox(
                    $"One or more of the Rendering Layers is not defined in the Universal Global Settings asset.",
                    MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            renderingLayer = EditorGUILayout.MaskField("RenderingLayerMask", renderingLayer, renderingLayerMaskNames);
            if (EditorGUI.EndChangeCheck())
            {
#if UNITY_2022_1_OR_NEWER
                property.uintValue = (uint)renderingLayer;
#else
            property.intValue = (int)renderingLayer;
#endif
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
      
            DrawRenderingLayerMask(property);
        }
    }
}