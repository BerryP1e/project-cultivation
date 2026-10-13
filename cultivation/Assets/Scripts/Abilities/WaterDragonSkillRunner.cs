using UnityEngine;

/// <summary>水龙炮：主角自然施法，龙55%攻击定格与出水同步，软消散。</summary>
[DefaultExecutionOrder(100)]
public sealed class WaterDragonSkillRunner : MonoBehaviour
{
    public const string 动作资源路径 = "技能动作/玄霄雷诀/A3";
    public const string 龙资源路径 = "Abilities/WaterDragonSummon";
    public const float 出手进度 = .43f;
    public const float 水柱粗细 = .05f;
    public const float 消散时长 = .65f;
    enum Phase { Summon, Active, Fade, Tail }
    Phase phase;
    ActiveSkillCaster caster;
    ActiveDivineAbility ability;
    NpcInstance target;
    PlayerAnimationController actions;
    AnimationClip motion;
    WaterDragonBreathVisual dragon;
    GameObject effect;
    ParticleSystem[] particles;
    ParticleSystemRenderer[] renderers;
    Color[] colors;
    MaterialPropertyBlock block;
    Vector3? manual;
    float end, nextDamage, fadeStart, started, preparationElapsed;
    bool owns, actionCompleted;
    public bool 已释放 { get; private set; }
    public bool 正在消散 => phase == Phase.Fade;
    public int 结算次数 { get; private set; }
    public GameObject 当前特效 => effect;
    public WaterDragonBreathVisual 当前龙 => dragon;
    public Vector3 炮口 => dragon ? dragon.炮口 : transform.position;

    public static bool 资源就绪()
    {
        var prefab = Resources.Load<GameObject>(龙资源路径);
        var visual = prefab ? prefab.GetComponent<WaterDragonBreathVisual>() : null;
        return visual && visual.配置有效 && Resources.Load<AnimationClip>(动作资源路径);
    }
    public void 初始化(ActiveSkillCaster player, ActiveDivineAbility definition, NpcInstance locked, Vector3? point = null)
    {
        caster = player; ability = definition; target = locked; manual = point; actions = player.动画;
        motion = Resources.Load<AnimationClip>(动作资源路径);
        if (actions && motion && actions.播动作(motion, 1f)) owns = actions.占用施法(this);
        if (!owns) { Destroy(gameObject); return; }
        actions.动作播完 += ActionCompleted;
        started = Time.time; phase = Phase.Summon;
        var prefab = Resources.Load<GameObject>(龙资源路径);
        var instance = prefab ? Instantiate(prefab, transform) : null;
        dragon = instance ? instance.GetComponent<WaterDragonBreathVisual>() : null;
        if (!dragon || !dragon.初始化())
        { if (instance) Destroy(instance); dragon = null; Finish(); }
    }
    void ActionCompleted() { actionCompleted = true; }
    void Update()
    {
        if (phase == Phase.Fade)
        {
            float alpha = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.time - fadeStart) / 消散时长));
            Fade(alpha);
            if (alpha <= 0 && effect) { Destroy(effect); effect = null; }
            if (dragon && dragon.已消融) { Destroy(dragon.gameObject); dragon = null; }
            if (alpha <= 0 && !dragon) phase = Phase.Tail;
            return;
        }
        if (phase == Phase.Tail)
        {
            if (!actions || !actions.动作播放中 || actions.当前动作 != motion || Time.time - fadeStart > 3f)
            { Release(); Destroy(gameObject); }
            return;
        }
        if (!ValidTarget() || !actions || (!actionCompleted && (actions.当前动作 != motion || !actions.动作播放中)))
        { Finish(); return; }
        if (phase == Phase.Summon)
        {
            if (!dragon || actionCompleted || Time.time - started > 4f) Finish();
            return;
        }
        if (Time.time >= end) { Finish(); return; }
        if (Time.time < nextDamage || !caster.战斗属性) return;
        nextDamage = Time.time + Mathf.Max(.05f, ability.伤害间隔);
        if (target)
        {
            CombatDamagePipeline.命中(target, new CombatHitContext(caster.战斗属性, new AttackSpec(ability.伤害属性, ability.攻击类别, false, ability.伤害倍率), caster, ability.神通id, AbilityVfxUtility.命中点(target.transform), target.transform.position - 炮口, 结算次数));
            结算次数++;
        }
        else if (manual.HasValue) CombatImpactPipeline.接触(ability.神通id, manual.Value, source: caster);
    }
    void LateUpdate()
    {
        if (phase == Phase.Fade)
        {
            if (dragon) dragon.推进(Time.deltaTime, dragon.transform.position, dragon.transform.rotation);
            return;
        }
        if (!caster) return;
        if (dragon)
        {
            Vector3 anchor = caster.transform.position;
            var animator = actions ? actions.animator : null;
            var hips = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (hips) anchor = hips.position - Vector3.up * .7f;
            anchor += caster.transform.right * 1.25f - caster.transform.forward * 1.2f;
            Vector3 forward = Vector3.ProjectOnPlane(Destination() - anchor, Vector3.up);
            // 放大的龙嘴前伸更远：近目标时将龙整体后移，保持炮口处于目标前方。
            float distance = forward.magnitude;
            if (distance < dragon.吐息前伸距离 + .35f)
                anchor -= (distance > .001f ? forward / distance : caster.transform.forward) * (dragon.吐息前伸距离 + .35f - distance);
            Quaternion rotation = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : caster.transform.rotation;
            if (phase == Phase.Summon)
            {
                float progress = actions ? actions.动作进度 : 0;
                float desired = dragon.吐息预备时长 * Mathf.Clamp01(progress / 出手进度);
                dragon.推进(Time.deltaTime, anchor, rotation, Mathf.Max(0, desired - preparationElapsed));
                preparationElapsed = desired;
                if (progress >= 出手进度 && dragon.正在吐息) StartBeam();
            }
            else dragon.推进(Time.deltaTime, anchor, rotation);
        }
        if (phase == Phase.Active && effect && dragon)
            AbilityVfxUtility.对准光束(effect, dragon.炮口, Destination(), 水柱粗细);
    }
    Vector3 Destination() => manual ?? (target ? AbilityVfxUtility.命中点(target.transform) : (caster ? caster.transform.position + caster.transform.forward * 5f : transform.position));
    bool ValidTarget()
    {
        if (!caster || !caster.isActiveAndEnabled || !caster.生命 || caster.生命.IsDead) return false;
        if (manual.HasValue) return true;
        return target && !target.IsDead && (ability.范围 <= 0 || Vector3.Distance(caster.transform.position, target.transform.position) <= ability.范围);
    }
    void StartBeam()
    {
        phase = Phase.Active; 已释放 = true;
        effect = AbilityVfxUtility.生成(ability.特效资源路径, transform, 1);
        end = Time.time + Mathf.Max(.1f, ability.持续时长);
        nextDamage = Time.time + Mathf.Max(0, ability.首次造成伤害时间);
        if (!effect) { Finish(); return; }
        particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
        colors = new Color[renderers.Length]; block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            var material = renderers[i].sharedMaterial;
            colors[i] = material && material.HasProperty("_TintColor") ? material.GetColor("_TintColor") : Color.white;
        }
        AbilityVfxUtility.对准光束(effect, 炮口, Destination(), 水柱粗细);
    }
    void Finish()
    {
        if (phase == Phase.Fade || phase == Phase.Tail) return;
        fadeStart = Time.time; phase = Phase.Fade;
        if (dragon) dragon.结束();
        if (particles != null) foreach (var p in particles) if (p) p.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        if (!effect && !dragon) { Release(); Destroy(gameObject); return; }
        // 收招时冻结最后一帧水柱方向与龙的位置，避免尾散扫向其他单位。
        if (!ValidTarget()) Release();
    }
    void Fade(float alpha)
    {
        if (!effect) return;
        for (int i = 0; i < renderers.Length; i++) if (renderers[i])
        {
            renderers[i].GetPropertyBlock(block); block.SetColor("_TintColor", colors[i] * alpha);
            renderers[i].SetPropertyBlock(block); block.Clear();
        }
        var scale = effect.transform.localScale; scale.x = scale.y = 水柱粗细 * Mathf.Lerp(.12f, 1, alpha); effect.transform.localScale = scale;
    }
    void Release()
    {
        if (!actions) return;
        actions.动作播完 -= ActionCompleted;
        if (owns) actions.释放施法(this); owns = false;
    }
    void OnDisable()
    {
        Release();
        if (effect) Destroy(effect);
        if (dragon) Destroy(dragon.gameObject);
    }
}
