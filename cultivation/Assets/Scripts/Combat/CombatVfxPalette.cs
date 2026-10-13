using System;
using UnityEngine;

/// <summary>表现目录可选的材质/灯光调色，原生资源不改；动态子效果继承相同配置。</summary>
[CreateAssetMenu(menuName = "修仙/战斗特效调色")]
public sealed class CombatVfxPalette : ScriptableObject
{
    [Serializable] public sealed class Replacement { public Material source, target; }
    public Replacement[] materials = Array.Empty<Replacement>();
    public Color lightColor = Color.white;
    public void Apply(GameObject root)
    {
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            var values = renderer.sharedMaterials; bool changed = false;
            for (int i = 0; i < values.Length; i++) foreach (var pair in materials)
                if (values[i] && values[i] == pair.source) { values[i] = pair.target; changed = true; break; }
            if (changed) renderer.sharedMaterials = values;
        }
        foreach (var light in root.GetComponentsInChildren<Light>(true)) light.color = lightColor;
    }
}
