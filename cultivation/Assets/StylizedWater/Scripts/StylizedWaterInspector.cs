// Compatibility editor for the legacy Built-in water shader on Unity 2022.
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(StylizedWater))]
public class StylizedWaterInspector : Editor
{
    MaterialEditor materialEditor;
    Material editedMaterial;

    void OnDisable()
    {
        if (materialEditor) DestroyImmediate(materialEditor);
    }

    public override void OnInspectorGUI()
    {
        var water = (StylizedWater)target;
        var renderer = water.GetComponent<MeshRenderer>();
        var material = renderer ? renderer.sharedMaterial : null;
        EditorGUILayout.HelpBox("此旧版水使用已烘焙贴图。Unity 2022 已移除内置 Substance 生成器；颜色、水波、岸边泡沫及贴图可在下方直接编辑。", MessageType.Info);
        if (!material || !material.shader || !material.shader.name.StartsWith("StylizedWater/"))
        {
            EditorGUILayout.HelpBox("请给 MeshRenderer 分配 StylizedWater/ 水材质。", MessageType.Warning);
            return;
        }
        if (editedMaterial != material || !materialEditor)
        {
            if (materialEditor) DestroyImmediate(materialEditor);
            editedMaterial = material;
            materialEditor = (MaterialEditor)CreateEditor(material);
        }
        EditorGUILayout.LabelField(material.shader.name, EditorStyles.boldLabel);
        var properties = MaterialEditor.GetMaterialProperties(new Object[] { material });
        EditorGUI.BeginChangeCheck();
        foreach (var property in properties)
            if ((property.flags & MaterialProperty.PropFlags.HideInInspector) == 0)
                materialEditor.ShaderProperty(property, property.displayName);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(water, "Update water parameters");
            water.getProperties();
            EditorUtility.SetDirty(water);
        }
    }
}
#endif
