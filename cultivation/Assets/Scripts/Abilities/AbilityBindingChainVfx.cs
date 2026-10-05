using UnityEngine;

/// <summary>保留源锁链网格和材质，从外围锚点向身体收拢，成形后持续保持。</summary>
public class AbilityBindingChainVfx : MonoBehaviour
{
    public float 生长时长 = .8f;
    public bool 已成形 { get; private set; }
    ParticleSystem 锁链;
    ParticleSystem.Particle[] 粒子;
    Vector3[] 起点, 终点;
    Transform[] 链段;
    float 开始;
    Material 方块材质, 锁链材质;

    void Start()
    {
        锁链 = GetComponentInChildren<ParticleSystem>();
        if (锁链 == null) return;
        // 先采样原素材的球形分布，仅改生长位置、长度和生命周期。
        锁链.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        锁链.Simulate(.05f, false, true);
        粒子 = new ParticleSystem.Particle[锁链.main.maxParticles];
        int 数量 = 锁链.GetParticles(粒子);
        if (数量 == 0) { 锁链.Emit(25); 数量 = 锁链.GetParticles(粒子); }
        System.Array.Resize(ref 粒子, 数量);
        起点 = new Vector3[数量]; 终点 = new Vector3[数量];
        链段 = new Transform[数量];
        var 主 = 锁链.main; 主.loop = false;
        var 发射 = 锁链.emission; 发射.enabled = false;
        var 大小 = 锁链.sizeOverLifetime; 大小.enabled = false;
        var 颜色 = 锁链.colorOverLifetime; 颜色.enabled = false;
        var 旋转 = 锁链.rotationOverLifetime; 旋转.enabled = false;
        var 速 = 锁链.velocityOverLifetime; 速.enabled = false;
        锁链.Pause(false);
        var 渲染 = 锁链.GetComponent<ParticleSystemRenderer>();
        渲染.alignment = ParticleSystemRenderSpace.Local;
        锁链材质 = new Material(渲染.sharedMaterial);
        锁链材质.DisableKeyword("IS_UNITY_PARTICLE_INSTANCING_ENABLED");
        // 源素材为巨型锁链铺80个链环；缩到人物周围仍用80会挤成红线。
        var 贴图倍率 = 锁链材质.mainTextureScale; 贴图倍率.x = 10f;
        锁链材质.mainTextureScale = 贴图倍率;
        渲染.sharedMaterial = 锁链材质;
        // 使用同一个源网格直接控制两端，避免粒子渲染器对长条网格的尺寸归一化。
        渲染.enabled = false;
        方块材质 = new Material(Shader.Find("Standard"));
        方块材质.color = new Color(.12f, .025f, .035f);
        方块材质.EnableKeyword("_EMISSION");
        方块材质.SetColor("_EmissionColor", new Color(.8f, .06f, .025f));
        for (int i = 0; i < 数量; i++)
        {
            // 均匀覆盖四面八方，保留随机旋转，避免整束锁链来自地面。
            Vector3 方向 = 粒子[i].position.normalized;
            if (方向.sqrMagnitude < .1f) 方向 = Random.onUnitSphere;
            起点[i] = 方向 * Random.Range(11f, 15f);
            终点[i] = 方向 * 2f;
            粒子[i].rotation3D = (Quaternion.FromToRotation(Vector3.up, -方向)
                * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up)).eulerAngles;
            粒子[i].velocity = Vector3.zero;
            粒子[i].startLifetime = 粒子[i].remainingLifetime = 100000f;
            粒子[i].startColor = 主.startColor.Evaluate(Random.value);
            var 段 = new GameObject("ChainSegment_" + i, typeof(MeshFilter), typeof(MeshRenderer));
            段.transform.SetParent(锁链.transform, false); 段.layer = gameObject.layer;
            段.GetComponent<MeshFilter>().sharedMesh = 渲染.mesh;
            var 段渲染 = 段.GetComponent<MeshRenderer>(); 段渲染.sharedMaterial = 锁链材质;
            段渲染.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; 段渲染.receiveShadows = false;
            段.transform.localRotation = Quaternion.Euler(粒子[i].rotation3D);
            链段[i] = 段.transform;
            var 块 = GameObject.CreatePrimitive(PrimitiveType.Cube); 块.name = "ChainOrigin_" + i;
            块.transform.SetParent(锁链.transform, false);
            块.transform.localPosition = 起点[i]; 块.transform.localRotation = Random.rotation;
            块.transform.localScale = Vector3.one * .85f; 块.layer = gameObject.layer;
            Destroy(块.GetComponent<Collider>());
            var r = 块.GetComponent<MeshRenderer>(); r.sharedMaterial = 方块材质;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        开始 = Time.time;
        更新形状(0f);
    }

    void LateUpdate()
    {
        if (锁链 == null || 粒子 == null || 已成形) return;
        float t = Mathf.Clamp01((Time.time - 开始) / Mathf.Max(.01f, 生长时长));
        更新形状(Mathf.SmoothStep(0f, 1f, t));
        已成形 = t >= 1f;
    }

    void 更新形状(float t)
    {
        for (int i = 0; i < 粒子.Length; i++)
        {
            Vector3 尖端 = Vector3.Lerp(起点[i], 终点[i], t);
            粒子[i].position = (起点[i] + 尖端) * .5f;
            // LongSlide 网格沿Y长20，移动中心让外端始终钉在出生点。
            粒子[i].startSize3D = new Vector3(.45f, Mathf.Max(.001f, Vector3.Distance(起点[i], 尖端) / 20f), .5f);
            链段[i].localPosition = 粒子[i].position;
            链段[i].localScale = 粒子[i].startSize3D;
        }
        锁链.SetParticles(粒子, 粒子.Length);
    }

    void OnDestroy()
    {
        if (方块材质 != null) Destroy(方块材质);
        if (锁链材质 != null) Destroy(锁链材质);
    }
}
