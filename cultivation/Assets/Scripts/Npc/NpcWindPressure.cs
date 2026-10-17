using UnityEngine;

/// <summary>威压阻止 AI 决策及待发攻击；独立移动锁不干扰其他禁锢来源。</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(250)]
public sealed class NpcWindPressure : MonoBehaviour
{
    CultivationPassiveAbilities owner;
    NpcInstance npc;
    NpcMovementLock movementLock;
    readonly LineRenderer[] rings = new LineRenderer[5];
    readonly Vector3[] points = new Vector3[65];
    Material material;
    float expires, started, height, radius;
    Vector3 anchor;
    public bool 生效中 => enabled && owner && owner.PressureEnabled && npc && !npc.IsDead && Time.time < expires;
    public void Begin(CultivationPassiveAbilities caster, float duration)
    {
        owner = caster; npc = GetComponent<NpcInstance>(); anchor = transform.position;
        started = Time.time; expires = Time.time + duration; enabled = true;
        movementLock = GetComponent<NpcMovementLock>() ?? gameObject.AddComponent<NpcMovementLock>();
        movementLock.加锁(this);
        GetComponent<NpcAiBase>()?.ForceState(NpcAiState.待机);
        var bounds = AbilityVfxUtility.身体边界(transform);
        height = Mathf.Max(2, bounds.size.y + 1); radius = Mathf.Max(.55f, Mathf.Max(bounds.size.x, bounds.size.z) * .6f);
        if (!material) material = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
        for (int i = 0; i < rings.Length; i++)
        {
            if (!rings[i]) {
                var root = new GameObject("灵势威压_下压风环_" + i); root.transform.SetParent(transform, false);
                rings[i] = root.AddComponent<LineRenderer>(); rings[i].sharedMaterial = material;
                rings[i].positionCount = points.Length; rings[i].useWorldSpace = true; rings[i].widthMultiplier = .035f;
                rings[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            rings[i].enabled = true;
        }
    }
    void LateUpdate()
    {
        if (!生效中) { enabled = false; return; }
        transform.position = anchor;
        float fade = Mathf.Min(Mathf.Clamp01((Time.time - started) / .18f), Mathf.Clamp01((expires - Time.time) / .35f));
        for (int i = 0; i < rings.Length; i++)
        {
            float u = Mathf.Repeat((Time.time - started) * .8f + i / (float)rings.Length, 1);
            float r = radius * Mathf.Lerp(.25f, 1.3f, u);
            float y = Mathf.Lerp(height, .08f, u);
            for (int j = 0; j < points.Length; j++) {
                float a = j / (float)(points.Length - 1) * Mathf.PI * 2;
                float wav = Mathf.Sin(a * 3 + Time.time * 4) * .025f;
                points[j] = anchor + new Vector3(Mathf.Cos(a) * (r + wav), y, Mathf.Sin(a) * (r + wav));
            }
            rings[i].SetPositions(points);
            rings[i].startColor = rings[i].endColor = new Color(.12f, .55f, 1, Mathf.Sin(u * Mathf.PI) * .55f * fade);
        }
    }
    void OnDisable()
    {
        if (movementLock) movementLock.解锁(this);
        foreach (var ring in rings) if (ring) ring.enabled = false;
    }
    void OnDestroy() { if (material) Destroy(material); }
}
