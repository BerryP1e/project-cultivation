using UnityEngine;
using UnityEngine.Rendering;

/// <summary>限量蒙皮快照残影与螺旋风丝。共享身体材质不变，快照Mesh循环复用。</summary>
[DefaultExecutionOrder(120)]
public sealed class FlightDodgeVfx : MonoBehaviour
{
    [Range(2, 8)] public int 残影上限 = 6;
    [Min(.02f)] public float 采样间隔 = .045f;
    [Min(.05f)] public float 残影寿命 = .26f;
    public Color 颜色 = new Color(.22f, .4f, .46f, .24f);
    sealed class Ghost
    {
        public GameObject root;
        public Mesh[] meshes;
        public MeshRenderer[] renderers;
        public float age;
        public bool active;
    }
    PlayerFlightDodge dodge;
    SkinnedMeshRenderer[] bodies;
    Ghost[] ghosts;
    Material material;
    MaterialPropertyBlock properties;
    GameObject effectRoot;
    ParticleSystem particles;
    readonly ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[96];
    LineRenderer[] threads;
    Texture2D softDot;
    float sampleTimer, emission, phase;
    int cursor;
    bool emitting;
    public int 可见残影数 { get { int n = 0; if (ghosts != null) foreach (var g in ghosts) if (g.active) n++; return n; } }

    public void 开始()
    {
        if (!dodge) dodge = GetComponent<PlayerFlightDodge>();
        if (!effectRoot) Build();
        emitting = effectRoot != null;
        sampleTimer = 0; emission = 0;
    }
    public void 停止() => emitting = false;

    void Build()
    {
        var shader = Resources.Load<Shader>("VFX/FlightDodgeGhost");
        if (!shader) return;
        bodies = GetComponentsInChildren<SkinnedMeshRenderer>();
        material = new Material(shader) { name = "御风闪避_残影_Runtime" };
        properties = new MaterialPropertyBlock();
        effectRoot = new GameObject("FlightDodgeVfx_Runtime"); // 世界空间快照不随角色移动。
        ghosts = new Ghost[Mathf.Clamp(残影上限, 2, 8)];
        for (int i = 0; i < ghosts.Length; i++)
        {
            var g = ghosts[i] = new Ghost { root = new GameObject("DodgeGhost_" + i), meshes = new Mesh[bodies.Length], renderers = new MeshRenderer[bodies.Length] };
            g.root.transform.SetParent(effectRoot.transform, false);
            for (int j = 0; j < bodies.Length; j++)
            {
                var obj = new GameObject("Body_" + j); obj.transform.SetParent(g.root.transform, false); obj.layer = bodies[j].gameObject.layer;
                g.meshes[j] = new Mesh { name = "DodgeSnapshot" }; g.meshes[j].MarkDynamic();
                obj.AddComponent<MeshFilter>().sharedMesh = g.meshes[j];
                var renderer = g.renderers[j] = obj.AddComponent<MeshRenderer>();
                int count = bodies[j].sharedMesh ? bodies[j].sharedMesh.subMeshCount : 1;
                var mats = new Material[count]; for (int k = 0; k < count; k++) mats[k] = material;
                renderer.sharedMaterials = mats; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            g.root.SetActive(false);
        }
        softDot = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "DodgeSoftDot", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float r = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
            pixels[y * 32 + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Max(0, 1 - r), 2));
        }
        softDot.SetPixels(pixels); softDot.Apply(false, true);
        var pgo = new GameObject("DodgeSpiralParticles"); pgo.transform.SetParent(effectRoot.transform, false);
        particles = pgo.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 96; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = .3f; main.startSpeed = 0; main.startSize = .065f; main.startColor = new Color(.6f, .95f, 1f, .8f);
        var emit = particles.emission; emit.enabled = false;
        var shape = particles.shape; shape.enabled = false;
        var overLife = particles.colorOverLifetime; overLife.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); overLife.color = gradient;
        var pr = particles.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = material; pr.shadowCastingMode = ShadowCastingMode.Off;
        properties.Clear(); properties.SetTexture("_MainTex", softDot); properties.SetColor("_Color", new Color(.5f, .9f, 1f, 1)); pr.SetPropertyBlock(properties);
        particles.Play();
        threads = new LineRenderer[3];
        for (int i = 0; i < threads.Length; i++)
        {
            var go = new GameObject("DodgeWindThread_" + i); go.transform.SetParent(effectRoot.transform, false);
            var line = threads[i] = go.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.useWorldSpace = true; line.positionCount = 22; line.widthMultiplier = .024f;
            line.widthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
            line.startColor = new Color(.6f, .93f, 1, 0); line.endColor = new Color(.6f, .93f, 1, .48f);
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.enabled = false;
        }
    }

    void LateUpdate()
    {
        if (!effectRoot) return;
        float dt = CombatHitStop.正在顿帧(transform) ? 0 : Time.deltaTime;
        bool active = emitting && dodge && dodge.闪避中;
        if (active && dt > 0)
        {
            sampleTimer -= dt;
            if (sampleTimer <= 0) { Snapshot(); sampleTimer = Mathf.Max(.02f, 采样间隔); }
            emission += dt * 100; phase += dt * 19;
            int count = Mathf.Min(16, Mathf.FloorToInt(emission)); emission -= count;
            for (int i = 0; i < count; i++)
            {
                float angle = phase + i * 2.4f;
                var ep = new ParticleSystem.EmitParams { position = transform.position + new Vector3(Mathf.Cos(angle) * .48f, .2f + (i % 5) * .17f, Mathf.Sin(angle) * .48f), velocity = new Vector3(-Mathf.Sin(angle), .15f, Mathf.Cos(angle)) * 1.7f };
                particles.Emit(ep, 1);
            }
        }
        for (int i = 0; i < threads.Length; i++)
        {
            var line = threads[i]; line.enabled = active;
            if (!active) continue;
            for (int j = 0; j < 22; j++)
            {
                float u = j / 21f, angle = phase + i * 2.094f - u * 2.7f;
                line.SetPosition(j, transform.position + new Vector3(Mathf.Cos(angle) * .5f, .18f + u * .8f, Mathf.Sin(angle) * .5f) - dodge.方向 * (1 - u) * .45f);
            }
        }
        int alive = particles.GetParticles(particleBuffer);
        for (int i = 0; i < alive; i++) particleBuffer[i].velocity = Quaternion.AngleAxis(dt * 240, Vector3.up) * particleBuffer[i].velocity;
        particles.SetParticles(particleBuffer, alive);
        foreach (var g in ghosts)
        {
            if (!g.active) continue;
            g.age += dt;
            if (g.age >= 残影寿命) { g.active = false; g.root.SetActive(false); continue; }
            var tint = 颜色; tint.a *= Mathf.Pow(1 - g.age / Mathf.Max(.05f, 残影寿命), 1.5f);
            foreach (var renderer in g.renderers)
            {
                renderer.GetPropertyBlock(properties); properties.SetColor("_Color", tint); renderer.SetPropertyBlock(properties);
            }
        }
    }

    void Snapshot()
    {
        var g = ghosts[cursor++ % ghosts.Length];
        g.age = 0; g.active = true; g.root.SetActive(true);
        for (int j = 0; j < bodies.Length; j++)
        {
            var body = bodies[j]; var renderer = g.renderers[j];
            if (!body || !body.enabled || !body.gameObject.activeInHierarchy || !body.sharedMesh) { renderer.enabled = false; continue; }
            renderer.enabled = true;
            body.BakeMesh(g.meshes[j]);
            renderer.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
            // BakeMesh已经把当前蒙皮骨架的世界缩放写进顶点，再乘lossyScale会放大1.8倍。
            renderer.transform.localScale = Vector3.one;
            properties.Clear(); properties.SetTexture("_MainTex", body.sharedMaterial && body.sharedMaterial.mainTexture ? body.sharedMaterial.mainTexture : Texture2D.whiteTexture);
            properties.SetColor("_Color", 颜色); renderer.SetPropertyBlock(properties);
        }
    }

    void OnDisable()
    {
        emitting = false;
        if (ghosts != null) foreach (var g in ghosts) { g.active = false; g.root.SetActive(false); }
        if (particles) particles.Clear();
        if (threads != null) foreach (var line in threads) line.enabled = false;
    }
    void OnDestroy()
    {
        if (ghosts != null) foreach (var g in ghosts) foreach (var mesh in g.meshes) if (mesh) Destroy(mesh);
        if (effectRoot) Destroy(effectRoot);
        if (material) Destroy(material);
        if (softDot) Destroy(softDot);
    }
}
