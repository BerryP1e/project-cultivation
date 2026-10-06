using UnityEngine;

/// <summary>八九玄功真实刃尖轨迹。仅出手时发光；世界空间拖尾不跟着手掌回收。</summary>
[DisallowMultipleComponent]
public sealed class JiubaWeaponVfx : MonoBehaviour
{
    BasicJiuba01 attack;
    SkinnedMeshRenderer weapon;
    GameObject emitter;
    TrailRenderer edge, core;
    ParticleSystem sparks;
    Material material;
    Texture2D texture;
    Vector3 localTip;
    int attackNumber = -1;
    public bool 正在挥舞 { get; private set; }
    public Vector3 刃尖位置 => emitter != null ? emitter.transform.position : transform.position;

    public void 绑定(BasicJiuba01 owner)
    {
        if (attack == owner) return;
        停止(true); attack = owner; attackNumber = -1;
    }

    void Awake()
    {
        attack = GetComponent<BasicJiuba01>();
        texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[4096];
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float r = new Vector2((x + .5f) / 32f - 1, (y + .5f) / 32f - 1).magnitude;
            pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - r), 2));
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        material = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture };
    }

    void LateUpdate()
    {
        bool playing = attack != null && attack.isActiveAndEnabled && attack.出手动作中 &&
            attack.动画 != null && attack.动画.动作播放中;
        if (!playing) { 停止(false); return; }
        if (weapon == null || !weapon.gameObject.activeInHierarchy)
        {
            停止(true);
            foreach (var candidate in GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach (var name in attack.武器节点名.Split('|'))
                    if (string.Equals(candidate.name, name.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    { weapon = candidate; break; }
            if (weapon == null) return;
            Bind();
        }
        emitter.transform.position = weapon.transform.TransformPoint(localTip);
        if (attackNumber != attack.出手序号)
        {
            attackNumber = attack.出手序号;
            edge.Clear(); core.Clear();
        }
        float progress = attack.动画.动作进度;
        bool visible = progress >= .12f && progress <= .95f;
        edge.time = core.time = Mathf.Clamp(.20f / attack.攻速系数, .06f, .24f);
        edge.emitting = core.emitting = visible;
        if (visible && !正在挥舞) sparks.Play();
        else if (!visible && 正在挥舞) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        正在挥舞 = visible;
    }

    void Bind()
    {
        if (emitter != null) Destroy(emitter);
        // 只标定一次：较宽的一端是刀刃，另一端是柄。粒子不沿整根戟杆撒。
        var mesh = new Mesh();
        try
        {
            weapon.BakeMesh(mesh); mesh.RecalculateBounds();
            var b = mesh.bounds; var e = b.extents;
            int axis = e.x >= e.y && e.x >= e.z ? 0 : e.y >= e.z ? 1 : 2;
            float low = 0, high = 0;
            foreach (var v in mesh.vertices)
            {
                var d = v - b.center; float radial = 0;
                for (int i = 0; i < 3; i++) if (i != axis) radial += d[i] * d[i];
                if (d[axis] < -e[axis] * .55f) low = Mathf.Max(low, radial);
                if (d[axis] > e[axis] * .55f) high = Mathf.Max(high, radial);
            }
            localTip = b.center; localTip[axis] += (high >= low ? 1 : -1) * e[axis] * .96f;
        }
        finally { Destroy(mesh); }
        emitter = new GameObject("八九玄功_刃光粒子") { layer = weapon.gameObject.layer };
        // 独立于武器骨骼：挂载重建时，残余世界空间粒子可由本组件可靠清理。
        emitter.transform.SetParent(transform, true); emitter.transform.localScale = Vector3.one;
        emitter.transform.position = weapon.transform.TransformPoint(localTip);
        edge = MakeTrail("BladeGlow", .30f, new Color(1, .82f, .40f, .75f));
        core = MakeTrail("BladeCore", .065f, new Color(1, .98f, .87f, 1));
        var go = new GameObject("BladeSparks") { layer = weapon.gameObject.layer };
        go.transform.SetParent(emitter.transform, false);
        sparks = go.AddComponent<ParticleSystem>(); sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main; main.loop = true; main.playOnAwake = false; main.maxParticles = 128;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.16f, .32f);
        main.startSize = new ParticleSystem.MinMaxCurve(.035f, .075f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, 1.1f); main.gravityModifier = .12f;
        main.startColor = new Color(1, .9f, .6f);
        var emission = sparks.emission; emission.rateOverTime = 35; emission.rateOverDistance = 22;
        var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .035f;
        var color = sparks.colorOverLifetime; color.enabled = true;
        color.color = Fade(new Color(1, .88f, .48f, 1));
        var renderer = sparks.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 1.6f; renderer.velocityScale = .15f;
    }

    TrailRenderer MakeTrail(string name, float width, Color color)
    {
        var go = new GameObject(name) { layer = weapon.gameObject.layer };
        go.transform.SetParent(emitter.transform, false);
        var trail = go.AddComponent<TrailRenderer>(); trail.sharedMaterial = material;
        trail.widthMultiplier = width; trail.minVertexDistance = .015f; trail.emitting = false;
        trail.colorGradient = Fade(color); trail.numCapVertices = 3;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false; trail.textureMode = LineTextureMode.Stretch;
        return trail;
    }

    static Gradient Fade(Color color)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(color.a, 0), new GradientAlphaKey(0, 1) });
        return g;
    }

    public void 停止(bool clear)
    {
        正在挥舞 = false;
        if (edge != null) { edge.emitting = false; if (clear) edge.Clear(); }
        if (core != null) { core.emitting = false; if (clear) core.Clear(); }
        if (sparks != null) sparks.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        if (clear) { if (emitter != null) Destroy(emitter); weapon = null; emitter = null; }
    }

    void OnDisable() => 停止(true);
    void OnDestroy()
    {
        停止(true);
        if (material != null) Destroy(material);
        if (texture != null) Destroy(texture);
    }
}
