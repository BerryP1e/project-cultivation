using UnityEngine;

/// <summary>百分比被动结算、真实灵力恢复粒子及轮盘威压入口。</summary>
[DisallowMultipleComponent]
public sealed class CultivationPassiveAbilities : MonoBehaviour
{
    public const string CycleId = "ability_zicheng_xunhuan";
    public const string PressureId = "ability_lingshi_weiya";
    public const string CloneId = "ability_shenwai_huashen";
    public const string SpaceId = "ability_kongjian_tengnuo";
    PlayerVitals vitals;
    UIPanelData data;
    ParticleSystem motes;
    readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[48];
    readonly Vector3[] starts = new Vector3[48];
    readonly float[] ages = new float[48];
    int count;
    float recoveredAt = -100, budget;
    static bool Enabled(UIPanelData panel, PassiveDivineAbility passive)
        => panel && passive && passive.结算方式 != PassiveSkillKind.绝对护罩 && panel.已获得被动(passive)
        && (panel.已停用被动 == null || !panel.已停用被动.Contains(passive));

    public static PassiveDivineAbility Find(UIPanelData panel, string id, bool enabledOnly = true)
    {
        if (!panel || panel.神通 == null) return null;
        foreach (var entry in panel.神通)
            if (entry is PassiveDivineAbility passive && passive.神通id == id
                && (!enabledOnly || Enabled(panel, passive))) return passive;
        return null;
    }

    public static void 汇总属性(UIPanelData panel, AttributeSet values, AttributeSet percent)
    {
        if (!panel || panel.神通 == null) return;
        foreach (var entry in panel.神通)
            if (entry is PassiveDivineAbility passive && Enabled(panel, passive))
            {
                values.Add(passive.增益);
                percent[AttributeType.HealthRegen] += Mathf.Max(0, passive.气血回复增幅);
                percent[AttributeType.SpiritRegen] += Mathf.Max(0, passive.灵力回复增幅);
                percent[AttributeType.Sense] += Mathf.Max(0, passive.神识增幅);
            }
    }

    void OnEnable()
    {
        vitals = GetComponent<PlayerVitals>();
        data = GetComponent<PlayerAbilityLoader>()?.面板数据 ?? GetComponent<UIPanelData>() ?? FindObjectOfType<UIPanelData>();
        if (vitals) vitals.灵气恢复 += Recovered;
    }
    void Recovered(float amount)
    {
        if (amount <= 0 || !Find(data, CycleId)) return;
        recoveredAt = Time.time;
        budget = Mathf.Min(6, budget + Mathf.Clamp(amount * .5f, .1f, 4));
    }
    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0) return;
        bool enabled = vitals && !vitals.IsDead && Find(data, CycleId);
        if (!enabled) { budget = 0; recoveredAt = -100; }
        if (enabled && Time.time - recoveredAt < .18f) budget = Mathf.Min(6, budget + dt * 9);
        while (budget >= 1 && count < particles.Length)
        {
            EnsureParticles(); budget--;
            float angle = Random.value * Mathf.PI * 2;
            starts[count] = new Vector3(Mathf.Cos(angle), Random.Range(.3f, 1.8f), Mathf.Sin(angle)) * Random.Range(1.1f, 1.5f);
            ages[count] = 0; count++;
        }
        for (int i = count - 1; i >= 0; i--)
        {
            ages[i] += dt;
            float u = ages[i] / .85f;
            if (u >= 1) { count--; starts[i] = starts[count]; ages[i] = ages[count]; continue; }
        }
        for (int i = 0; i < count; i++)
        {
            float u = ages[i] / .85f;
            Vector3 start = starts[i];
            var twist = Quaternion.Euler(0, u * 38, 0) * start;
            particles[i] = new ParticleSystem.Particle {
                position = Vector3.Lerp(twist, Vector3.up * 1.05f, u * u),
                startSize = Mathf.Lerp(.065f, .015f, u), startLifetime = 1, remainingLifetime = 1,
                startColor = new Color(.34f, .88f, .92f, Mathf.Sin(Mathf.PI * u) * .8f)
            };
        }
        if (motes) motes.SetParticles(particles, count);
    }
    void EnsureParticles()
    {
        if (motes) return;
        var root = new GameObject("自成循环_灵气入体"); root.transform.SetParent(transform, false);
        motes = root.AddComponent<ParticleSystem>();
        motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = motes.main; main.playOnAwake = false; main.maxParticles = particles.Length;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = motes.emission; emission.enabled = false;
        var renderer = motes.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = NpcDissolveEffect.取材质();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    public bool CanPressure(out string reason)
    {
        reason = "";
        if (!isActiveAndEnabled || !Find(data, PressureId)) { reason = "尚未启用灵势威压"; return false; }
        var targeting = GetComponent<NpcTargeting>(); var target = targeting ? targeting.LockedNpc : null;
        if (!target || target.IsDead) { reason = "请先锁定目标"; return false; }
        if (!targeting.IsInSenseRange(target)) { reason = "超出神识范围"; return false; }
        float targetLevel = target.已等级补正 ? target.当前补正等级 : target.定义 ? target.定义.境界 : 1;
        int level = GetComponent<PlayerCultivation>()?.等级 ?? 1;
        if (targetLevel >= level) { reason = "目标境界须低于自身"; return false; }
        return true;
    }
    public bool Pressure()
    {
        if (!CanPressure(out var reason)) { data?.ShowHint(reason); return false; }
        var target = GetComponent<NpcTargeting>().LockedNpc;
        var effect = target.GetComponent<NpcWindPressure>() ?? target.gameObject.AddComponent<NpcWindPressure>();
        effect.Begin(this, Mathf.Max(.1f, Find(data, PressureId).压制持续时间));
        return true;
    }
    public bool PressureEnabled => isActiveAndEnabled && vitals && !vitals.IsDead && Find(data, PressureId);
    void OnDisable()
    {
        if (vitals) vitals.灵气恢复 -= Recovered;
        if (motes) Destroy(motes.gameObject);
        motes = null; count = 0; budget = 0; recoveredAt = -100;
    }
}
