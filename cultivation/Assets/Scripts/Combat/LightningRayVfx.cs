using UnityEngine;

/// <summary>Original lightning-ray particles, fitted from an impact point to a lightning source.</summary>
public static class LightningRayVfx
{
    public const string Path = "CombatVFX/CombatMagic/lightning-fx/lightning-ray";

    public static GameObject Spawn(Vector3 source, Vector3 impact, float scale, float width,
        string name, bool beamOnly = false, Color? tint = null)
    {
        var prefab = Resources.Load<GameObject>(Path);
        if (!prefab) return null;
        var axis = source - impact;
        float length = Mathf.Max(.1f, axis.magnitude);
        if (axis.sqrMagnitude < .0001f) axis = Vector3.up;
        var up = Mathf.Abs(Vector3.Dot(axis.normalized, Vector3.up)) > .98f ? Vector3.forward : Vector3.up;
        var rotation = Quaternion.LookRotation(axis.normalized, up);
        var go = Object.Instantiate(prefab, impact, rotation);
        go.name = name;
        scale = Mathf.Max(.05f, scale);
        go.transform.localScale = Vector3.one * scale;
        float lifetime = 特效摆放.量特效总时长(prefab, 4f) * 1.05f;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            bool beam = ps.name == "lightning-ray" || ps.name == "lightning-glow";
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (beamOnly && !beam)
            {
                // Keep children: the original prefab nests its ray under the ground effects.
                var emission = ps.emission;
                emission.enabled = false;
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (renderer) renderer.enabled = false;
                continue;
            }

            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.loop = false;
            if (tint.HasValue)
            {
                var color = main.startColor;
                color.colorMin = Recolor(color.colorMin, tint.Value);
                color.colorMax = Recolor(color.colorMax, tint.Value);
                main.startColor = color;
                var overLife = ps.colorOverLifetime;
                if (overLife.enabled)
                {
                    var gradient = overLife.color;
                    if (gradient.gradient != null)
                    {
                        var colors = gradient.gradient.colorKeys;
                        for (int i = 0; i < colors.Length; i++) colors[i].color = Recolor(colors[i].color, tint.Value);
                        gradient.gradient = new Gradient { colorKeys = colors, alphaKeys = gradient.gradient.alphaKeys };
                        overLife.color = gradient;
                    }
                }
            }

            if (!beam) continue;
            // Native Stretch quads extend along +Z by startSize * lengthScale.
            // Adjust only their width and length; preserve native speed, lifetime and renderer mode.
            main.startSizeMultiplier *= Mathf.Max(.05f, width);
            float worldSize = Mathf.Max(.01f, main.startSize.constantMax * scale);
            renderer.lengthScale = length / worldSize;
            renderer.velocityScale = 0f;
            ps.transform.position = impact;
        }
        Object.Destroy(go, lifetime);
        return go;
    }

    static Color Recolor(Color original, Color tint)
    {
        float brightness = Mathf.Max(original.r, original.g, original.b);
        return new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, original.a);
    }
}
