using System.Collections.Generic;
using UnityEngine;

/// <summary>施法节点→有限粒子弹道→扫掠接触→统一伤害/地表管线；目标相位和表现不充当伤害时钟。</summary>
[DefaultExecutionOrder(110)]
public sealed class FallingSpellRunner : MonoBehaviour
{
    ActiveSkillCaster owner;
    ActiveDivineAbility ability;
    NpcInstance target;
    PlayerAnimationController actions;
    AnimationClip clip;
    FallingSpellPattern pattern;
    GameObject visual;
    Vector3? aim;
    Vector3 center;
    bool released, ownsAction;
    int emitted;
    BasicRemoteAttack01 basic;
    float basicDamage,basicRadius;
    Transform OwnerTransform=>basic?basic.transform:owner?owner.transform:null;
    float HitRadius=>basic?basicRadius:pattern.命中半径>0?pattern.命中半径:ability.范围;
    const string BasicMeteor="ability_huoyan_yun_cast";
    float releaseTime, endTime;
    readonly Dictionary<uint, Vector3> previous = new Dictionary<uint, Vector3>();
    readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[64];
    readonly RaycastHit[] sweep = new RaycastHit[32];
    public int 接触次数 { get; private set; }
    public int 命中次数 { get; private set; }
    public float 累计伤害 { get; private set; }
    public Vector3 落点 => center;
    public bool 已释放 => released;
    public GameObject 当前特效 => visual;

    public static bool 资源就绪(ActiveDivineAbility definition)
    {
        if (!definition) return false;
        var prefab = Resources.Load<GameObject>(definition.特效资源路径);
        var config = prefab ? prefab.GetComponent<FallingSpellPattern>() : null;
        return config && config.飞行粒子 && Resources.Load<AnimationClip>("技能动作/" + definition.施法动作)
            && CombatVfxPipeline.配置 && CombatVfxPipeline.配置.查找(definition.神通id + "_cast")?.预制体;
    }
    public static bool 尝试开始(ActiveSkillCaster caster, ActiveDivineAbility definition, NpcInstance locked, Vector3? point)
    {
        if (!资源就绪(definition) || !caster || !caster.动画) return false;
        var host = new GameObject("FallingSpell_" + definition.神通id);
        var runner = host.AddComponent<FallingSpellRunner>();
        runner.owner = caster; runner.ability = definition; runner.target = locked; runner.aim = point;
        runner.center = point ?? (locked ? locked.transform.position : caster.transform.position);
        runner.actions = caster.动画;
        runner.clip = Resources.Load<AnimationClip>("技能动作/" + definition.施法动作);
        if (!runner.actions.播动作(runner.clip) || !runner.actions.占用施法(runner))
        { Destroy(host); return false; }
        runner.ownsAction = true;
        return true;
    }
    public static bool 普攻陨石资源就绪()
    {var entry=CombatVfxPipeline.配置?.查找(BasicMeteor);var config=entry?.预制体?entry.预制体.GetComponent<FallingSpellPattern>():null;return config&&config.飞行粒子;}
    public static bool 尝试普攻陨石(BasicRemoteAttack01 source,NpcInstance locked,Vector3 point,float damage,float radius)
    {
        if(!source||!普攻陨石资源就绪())return false;
        var host=new GameObject("Taixu_A3_Meteor");var runner=host.AddComponent<FallingSpellRunner>();
        runner.basic=source;runner.target=locked;runner.basicDamage=damage;runner.basicRadius=Mathf.Max(.1f,radius);runner.center=point;
        runner.ReleaseVisual(BasicMeteor);return true;
    }
    void ReleaseVisual(string id)
    {
        transform.position=center;
        visual=CombatVfxPipeline.播放(id,center,Vector3.down);
        visual.transform.SetParent(transform,true);pattern=visual.GetComponent<FallingSpellPattern>();
        foreach(var ps in visual.GetComponentsInChildren<ParticleSystem>(true))ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach(var ps in visual.GetComponentsInChildren<ParticleSystem>(true))if(ps.main.playOnAwake)ps.Play(false);
        pattern.飞行粒子.Play(false);
        if(pattern.起点闪光)pattern.起点闪光.Play(false);
        if(pattern.召唤阵)pattern.召唤阵.Play(false);
        released=true;releaseTime=Time.time;
        endTime=releaseTime+(pattern.数量-1)*pattern.间隔+pattern.起点偏移.magnitude/pattern.飞行速度+6;
    }
    bool OwnerReady => basic?basic.isActiveAndEnabled&&!(basic.GetComponent<PlayerVitals>()?.IsDead??true):owner&&owner.isActiveAndEnabled&&owner.生命&&!owner.生命.IsDead;
    void Update()
    {
        if (!OwnerReady) { Destroy(gameObject); return; }
        if (!released)
        {
            if (!actions || !actions.动作播放中 || actions.当前动作 != clip)
            { Destroy(gameObject); return; }
            var config = Resources.Load<GameObject>(ability.特效资源路径).GetComponent<FallingSpellPattern>();
            if (actions.动作进度 < config.出手进度) return;
            center = aim ?? (target && !target.IsDead ? target.transform.position : center);
            // 地面落点：鼠标和移动目标取释放节点的当前位置，后续可以躲出弹道。
            if (Physics.Raycast(center + Vector3.up * 2, Vector3.down, out var floor, 5, ~0, QueryTriggerInteraction.Ignore)
                && !floor.collider.GetComponentInParent<NpcInstance>() && !floor.collider.GetComponentInParent<PlayerVitals>()) center.y = floor.point.y;
            ReleaseVisual(ability.神通id+"_cast");
        }
        if (ownsAction && (!actions || actions.当前动作 != clip || !actions.动作播放中)) ReleaseAction();
        float elapsed = Time.time - releaseTime;
        while (emitted < Mathf.Clamp(pattern.数量, 1, 48) && elapsed + .00001f >= emitted * pattern.间隔) Emit();
        if (Time.time >= endTime) Destroy(gameObject);
    }
    void Emit()
    {
        float hitRadius=HitRadius;
        float spread=basic?0:Mathf.Max(0,ability.范围-hitRadius);
        // 第一发在目标中心；其余采用等面积黄金角分布，覆盖引导圈且不会随机全落空。
        float radius = emitted == 0 ? 0 : spread * Mathf.Sqrt((float)emitted / Mathf.Max(1, pattern.数量 - 1));
        float angle = emitted * 2.399963f;
        var end = center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
        var start = end + pattern.起点偏移;
        var velocity = -pattern.起点偏移.normalized * pattern.飞行速度;
        uint seed = (uint)(++emitted);
        var emit = new ParticleSystem.EmitParams { position = start, velocity = velocity, randomSeed = seed,
            rotation3D = Quaternion.LookRotation(velocity, Vector3.forward).eulerAngles, applyShapeToPosition = false };
        pattern.飞行粒子.Emit(emit, 1); previous[seed] = start;
        if (pattern.起点闪光)
            pattern.起点闪光.Emit(new ParticleSystem.EmitParams { position = start, velocity = Vector3.zero, applyShapeToPosition = false }, 3);
        if (pattern.召唤阵)
        {
            emit.position = end + Vector3.up * pattern.起点偏移.y; emit.velocity = Vector3.zero; emit.rotation3D = new Vector3(90, 0, 0);
            pattern.召唤阵.Emit(emit, 1);
        }
    }
    void LateUpdate()
    {
        if (!released || !pattern || !OwnerReady || Time.deltaTime <= 0) return;
        var ps = pattern.飞行粒子; int count = ps.GetParticles(particles);
        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            var p = particles[i];
            if (!previous.TryGetValue(p.randomSeed, out var from)) continue;
            Vector3 delta = p.position - from;
            bool contact = false; Vector3 point = p.position;
            float nearest = float.PositiveInfinity;
            if (delta.sqrMagnitude > .000001f)
            {
                int hits = Physics.SphereCastNonAlloc(from, pattern.弹体半径, delta.normalized, sweep, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < hits; j++)
                {
                    var collider = sweep[j].collider;
                    if (!collider || collider.transform.IsChildOf(OwnerTransform) || sweep[j].distance >= nearest) continue;
                    nearest = sweep[j].distance; point = sweep[j].point; contact = true;
                }
            }
            // 薄地面/无碰撞展示地面仍以实际弹道穿过落点平面时接触，不能等到技能结束。
            if (!contact && from.y >= center.y && p.position.y <= center.y)
            { point = Vector3.Lerp(from, p.position, (from.y - center.y) / Mathf.Max(.00001f, from.y - p.position.y)); contact = true; }
            if (contact)
            {
                Impact(point, p.velocity, (int)p.randomSeed);
                p.position = point;
                var sub = ps.subEmitters;
                for (int j = 0; j < sub.subEmittersCount; j++) if (sub.GetSubEmitterType(j) == ParticleSystemSubEmitterType.Manual) ps.TriggerSubEmitter(j, ref p);
                // 雷光保留接触后的静止端点供原生拖尾淡出；移出判定表后不能再次造成伤害。
                p.velocity = Vector3.zero;
                p.remainingLifetime = Mathf.Min(p.remainingLifetime, pattern.接触后留痕时间 * ps.main.simulationSpeed);
                particles[i] = p; previous.Remove(p.randomSeed); changed = true;
            }
            else previous[p.randomSeed] = p.position;
        }
        if (changed) ps.SetParticles(particles, count);
    }
    void Impact(Vector3 point, Vector3 direction, int phase)
    {
        接触次数++;
        float radius=HitRadius;
        var surface = point;
        if (basic||ability.命中特效贴地)
        {
            surface.y = center.y;
            float nearest = float.PositiveInfinity;
            int hits = Physics.RaycastNonAlloc(point + Vector3.up, Vector3.down, sweep, 12, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
                if (sweep[i].collider && !sweep[i].collider.GetComponentInParent<NpcInstance>() && !sweep[i].collider.GetComponentInParent<PlayerVitals>() && sweep[i].distance < nearest)
                { nearest = sweep[i].distance; surface = sweep[i].point; }
        }
        CombatImpactPipeline.接触(basic?"ability_huoyan_yun":ability.神通id,surface,radius,source:basic?(object)basic:owner);
        var spec=basic?new AttackSpec(DamageNature.特殊,AttackKind.普通攻击,false,basicDamage):new AttackSpec(ability.伤害属性,ability.攻击类别,false,ability.伤害倍率);
        var batch = new CombatHitBatch();
        foreach (var col in Physics.OverlapSphere(point, Mathf.Max(.05f, radius),basic?~0:owner.敌人层.value,QueryTriggerInteraction.Ignore))
        {
            var npc = col.GetComponentInParent<NpcInstance>();
            if (!npc || npc.IsDead || (!npc.是敌对目标 && npc != target)) continue;
            var result=batch.命中(new NpcTarget(npc),new CombatHitContext(basic?basic.玩家战斗属性:owner.战斗属性,spec,basic?(object)basic:owner,basic?basic.方法id:ability.神通id,point,direction,basic?2:phase,playHitVfx:!basic));
            if (result.状态 == CombatHitState.Duplicate) continue;
            if(basic)basic.记录结果(new NpcTarget(npc),result);
            命中次数++; 累计伤害 += result.伤害;
        }
    }
    void ReleaseAction() { if (ownsAction && actions) actions.释放施法(this); ownsAction = false; }
    void OnDestroy()
    {
        if (ownsAction && actions && actions.当前动作 == clip) actions.停止动作();
        ReleaseAction();
    }
}
