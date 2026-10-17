using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ASP.Scripts.Editor
{
    [CustomPropertyDrawer(typeof(SingleLayerMaskAttribute))]
    public class SingleLayerMaskDrawer : PropertyDrawer
    {
        public void DrawSingleLayerMask(SerializedProperty property)
        {
            var layers = Enumerable.Range(0, 31).Select(index => LayerMask.LayerToName(index))
                .Where(l => !string.IsNullOrEmpty(l)).ToList();
        
            var index = layers.IndexOf(LayerMask.LayerToName(property.intValue));

            EditorGUI.BeginChangeCheck();
            index = EditorGUILayout.Popup("Game Object Layer", index, layers.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = LayerMask.NameToLayer(layers[index]);
            }
        }


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DrawSingleLayerMask(property);
        }
    }
}