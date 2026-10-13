using System.Collections.Generic;
using UnityEngine;

/// <summary>只管理布阵与曲线弹道；成本、学习、伤害、顿帧、接触表现都走现有管线。</summary>
[DefaultExecutionOrder(110)]
public sealed class SpiralSwordSkillRunner : MonoBehaviour
{
    sealed class Sword
    {
        public Vector3 position, start, end, velocity;
        public Quaternion rotation;
        public SpiralSwordCurves.SwordFlight flight;
        public float distance01, revealTime, scheduledAge, launchTime, launchOpacity, duration, previousU, contactTime;
        public bool launched, contact, planted, finished;
    }
    ActiveSkillCaster owner;
    ActiveDivineAbility ability;
    NpcInstance target;
    Collider targetCollider;
    PlayerAnimationController actions;
    AnimationClip clip;
    AnimationClip volleyClip;
    SpiralSwordPattern pattern;
    GameObject visual;
    Vector3 cachedAim;
    Vector3? manualAim;
    bool ownsAction, volleyActionStarted, volleyReleased;
    float born, launchDelay;
    int active, launched, contacted, finished;
    Sword[] swords;
    SpiralSwordCurves.ArcLengthRibbon ribbon;
    int[] order;
    ParticleSystem.Particle[] particles;
    readonly ParticleSystem.Particle[] dust = new ParticleSystem.Particle[1024];
    readonly RaycastHit[] sweep = new RaycastHit[32];
    readonly Collider[] overlaps = new Collider[64];
    public int 发射数 => launched;
    public int 接触次数 => contacted;
    public int 命中次数 { get; private set; }
    public int 最大在飞数 { get; private set; }
    public GameObject 当前特效 => visual;
    public Vector3 落点 => cachedAim;
    public string 阶段 => !visual ? "起手" : Time.time - born < pattern.布阵时长 ? "布阵"
        : !volleyReleased ? "蓄势" : launched < swords.Length ? "出剑" : "回收";
    float VolleyStart => pattern.布阵时长 + pattern.单剑显现时长 + pattern.蓄势时长;
    float NextLaunch => swords[order[launched]].scheduledAge + launchDelay;

    public static bool 资源就绪(ActiveDivineAbility definition)
    {
        if (!definition) return false;
        var prefab = Resources.Load<GameObject>(definition.特效资源路径);
        var p = prefab ? prefab.GetComponent<SpiralSwordPattern>() : null;
        return p && p.剑群 && p.星屑 && Resources.Load<AnimationClip>("技能动作/" + p.齐射动作)
            && Resources.Load<AnimationClip>("技能动作/" + definition.施法动作)
            && CombatVfxPipeline.配置?.查找(definition.神通id + "_cast")?.预制体;
    }
    public static bool 尝试开始(ActiveSkillCaster caster, ActiveDivineAbility definition, NpcInstance locked, Vector3? point)
    {
        if (!caster || !caster.动画 || !资源就绪(definition)) return false;
        var host = new GameObject("SpiralSword_" + definition.神通id);
        var runner = host.AddComponent<SpiralSwordSkillRunner>();
        runner.owner = caster; runner.ability = definition; runner.target = locked; runner.manualAim = point;
        runner.targetCollider = locked ? locked.GetComponentInChildren<Collider>() : null;
        runner.cachedAim = point ?? (locked ? locked.transform.position + Vector3.up * .8f : caster.transform.position);
        runner.actions = caster.动画; runner.clip = Resources.Load<AnimationClip>("技能动作/" + definition.施法动作);
        if (!runner.actions.播动作(runner.clip) || !runner.actions.占用施法(runner)) { Destroy(host); return false; }
        runner.ownsAction = true;
        return true;
    }
    bool OwnerReady => owner && owner.isActiveAndEnabled && owner.生命 && !owner.生命.IsDead;
    Vector3 Aim()
    {
        if (manualAim.HasValue) return manualAim.Value;
        if (target && !target.IsDead) cachedAim = targetCollider ? targetCollider.bounds.center : target.transform.position + Vector3.up * .8f;
        return cachedAim;
    }
    void Begin()
    {
        cachedAim = Aim();
        visual = CombatVfxPipeline.播放(ability.神通id + "_cast", owner.transform.position, Vector3.up);
        if (!visual) { Destroy(gameObject); return; }
        visual.transform.SetParent(transform, true);
        pattern = visual.GetComponent<SpiralSwordPattern>();
        volleyClip = Resources.Load<AnimationClip>("技能动作/" + pattern.齐射动作);
        born = Time.time;
        int count = Mathf.Clamp(pattern.剑数, 96, 384);
        swords = new Sword[count]; order = new int[count]; particles = new ParticleSystem.Particle[count + 1];
        int seed = Random.Range(1, int.MaxValue);
        var random = new System.Random(seed);
        ribbon = new SpiralSwordCurves.ArcLengthRibbon(SpiralSwordCurves.Ribbon.Create(seed, pattern.圈数), 0);
        // 整条剑幕共用一个弧长基准，不把不同长度的层分别归一后叠成簇。
        for (int i = 0; i < count; i++)
        {
            float distance = (i + .5f) / count;
            swords[i] = new Sword { distance01 = distance, revealTime = distance * pattern.布阵时长 };
            order[i] = i;
        }
        // Fisher–Yates只洗发射顺序，布阵本身依然沿弧长均匀排布。
        for (int i = count - 1; i > 0; i--) { int j = random.Next(i + 1); int swap = order[i]; order[i] = order[j]; order[j] = swap; }
        float schedule = 0;
        for (int i = 0; i < count; i++)
        {
            swords[order[i]].scheduledAge = VolleyStart + schedule;
            float interval = 1 / pattern.出剑速率(schedule);
            schedule += 1 / pattern.出剑速率(schedule + interval * .5f);
        }
        pattern.剑群.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); pattern.剑群.Play(false);
        if (pattern.星屑) pattern.星屑.Play(false);
    }
    void Update()
    {
        if (!OwnerReady) { Destroy(gameObject); return; }
        if (Time.deltaTime <= 0) return;
        if (!visual)
        {
            if (!actions || !actions.动作播放中 || actions.当前动作 != clip) { Destroy(gameObject); return; }
            var config = Resources.Load<GameObject>(ability.特效资源路径).GetComponent<SpiralSwordPattern>();
            if (actions.动作进度 < config.出手进度) return;
            Begin(); if (!visual) return;
        }
        if (ownsAction && volleyActionStarted && (!actions || actions.当前动作 != volleyClip || !actions.动作播放中)) ReleaseAction();
        float age = Time.time - born;
        if (launched == 0) visual.transform.position = owner.transform.position;
        if (!volleyActionStarted && age >= VolleyStart)
        {
            ReleaseAction();
            if (!actions || !actions.播动作(volleyClip) || !actions.占用施法(this)) { Destroy(gameObject); return; }
            ownsAction = true;
            volleyActionStarted = true;
        }
        if (!volleyReleased)
        {
            if (volleyActionStarted && (!actions || actions.当前动作 != volleyClip || !actions.动作播放中))
            { Destroy(gameObject); return; }
            float progress = volleyActionStarted ? actions.动作进度 : 0;
            float speed = actions && actions.animator ? Mathf.Max(.01f, actions.animator.speed) : 1;
            float remaining = Mathf.Max(0, pattern.第二段出剑进度 - progress) * volleyClip.length / speed;
            // 等待时同步预转向时序；到下蹲节点才开启整套逐剑时钟，不积压成齐射。
            launchDelay = Mathf.Max(0, age - VolleyStart) + remaining;
            if (volleyActionStarted && progress >= pattern.第二段出剑进度)
            { volleyReleased = true; launchDelay = age - VolleyStart; }
        }
        Vector3 center = Aim();
        var bounds = new Bounds(visual.transform.TransformPoint(ribbon.Evaluate(0)), Vector3.zero);
        for (int sample = 1; sample <= 32; sample++)
            bounds.Encapsulate(visual.transform.TransformPoint(ribbon.Evaluate(sample / 32f)));
        Vector3 bandOrigin = bounds.center;
        var frame = SpiralSwordCurves.SwordFlight.Create(bandOrigin, center, center, bandOrigin, Vector3.zero, 0, 1);
        // 固定大小的条带从底部顺序显现，未出阵剑沿弧长流动；曲线本身不渲染。
        for (int i = 0; i < swords.Length; i++) if (!swords[i].launched)
        {
            var sword = swords[i]; var arc = ribbon;
            float distance = FlowDistance(sword, age);
            sword.position = visual.transform.TransformPoint(arc.Evaluate(distance));
            sword.velocity = visual.transform.TransformDirection(arc.Tangent(distance)) * pattern.条带流速;
            sword.rotation = visual.transform.rotation * arc.SwordRotation(distance);
            // 每柄有独立的连续时间戳，先从平铺姿态柔和转向，再进入飞行倾角。
            float turn = Mathf.SmoothStep(0, 1, (age - (sword.scheduledAge + launchDelay - pattern.出阵转向时长)) / pattern.出阵转向时长);
            var direction = AreaPoint(center, i) - sword.position;
            sword.rotation = Quaternion.Slerp(sword.rotation, AimRotation(direction, frame.Up, frame.Side), turn);
        }
        // 预算满时整体顺延，避免积压的发射时间在下一帧一起喷出。
        int budget = Mathf.Clamp(pattern.最大同时飞剑, 8, 96);
        if (volleyReleased && launched < swords.Length && active >= budget && age > NextLaunch) launchDelay += age - NextLaunch;
        while (volleyReleased && launched < swords.Length && age >= NextLaunch && active < budget)
        {
            float scheduledAge = NextLaunch;
            int id = order[launched++]; var sword = swords[id];
            var arc = ribbon; float distance = FlowDistance(sword, scheduledAge);
            sword.launched = true;
            sword.start = sword.position = visual.transform.TransformPoint(arc.Evaluate(distance));
            sword.launchOpacity = RibbonOpacity(distance);
            sword.end = GroundPoint(AreaPoint(center, id));
            float turns = .14f + (id % 5) * .035f;
            float speed = pattern.飞行速度 * (1.05f + .25f * (id % 11) / 10);
            sword.velocity = (sword.end - sword.start).normalized * (speed * .8f);
            float length = Vector3.Distance(sword.start, sword.end) + Vector3.Distance(sword.start, bandOrigin) * Mathf.PI * turns * .5f;
            sword.duration = Mathf.Clamp(length / speed + .03f, .35f, 1.3f);
            sword.flight = SpiralSwordCurves.SwordFlight.Create(bandOrigin, center, sword.end, sword.start, sword.velocity, turns, sword.duration);
            // 高速逐剑可能同帧更新，但各剑按自己的时间戳推进，不是同一时刻齐射。
            sword.launchTime = born + scheduledAge;
            active++; 最大在飞数 = Mathf.Max(最大在飞数, active);
        }
        int visible = 0;
        for (int i = 0; i < swords.Length; i++)
        {
            var sword = swords[i]; if (sword.finished) continue;
            float behindHead = age - sword.revealTime;
            float alpha = behindHead < 0 ? 0 : Mathf.Lerp(.08f, 1, Mathf.SmoothStep(0, 1, behindHead / pattern.单剑显现时长));
            if (!sword.launched)
            {
                // 开放条带末端淡出、底端淡入，缓流越过端点时不产生可见瞬移。
                float distance = FlowDistance(sword, age), seam = .45f / ribbon.Length;
                alpha *= RibbonOpacity(distance) * Mathf.SmoothStep(0, 1, distance / seam) * Mathf.SmoothStep(0, 1, (1 - distance) / seam);
            }
            if (alpha <= 0) continue;
            if (sword.launched)
            {
                alpha *= Mathf.Lerp(sword.launchOpacity, 1, Mathf.SmoothStep(0, 1, (Time.time - sword.launchTime) / .08f));
                if (!sword.contact) Advance(sword, i);
                if (sword.contact)
                {
                    float hold = sword.planted ? pattern.插地停留时长 : 0;
                    float fade = sword.planted ? pattern.插地淡出时长 : .16f;
                    alpha *= 1 - Mathf.Clamp01((Time.time - sword.contactTime - hold) / fade);
                    if (alpha <= 0) { sword.finished = true; finished++; continue; }
                }
                if (!sword.contact)
                {
                    float u = Mathf.Clamp01((Time.time - sword.launchTime) / sword.duration);
                    // 始终沿实际飞行方向；末段切线冻结，不再朝敌人中心额外扭转。
                    sword.rotation = AimRotation(sword.flight.Velocity(u), sword.flight.Up, sword.flight.Side);
                }
            }
            particles[visible++] = new ParticleSystem.Particle { position = sword.position, velocity = Vector3.zero,
                startLifetime = 12, remainingLifetime = 12, startSize = pattern.剑缩放, rotation3D = sword.rotation.eulerAngles,
                startColor = new Color(.75f, .9f, 1, alpha), randomSeed = (uint)(i + 1) };
        }
        // 唯一的引导剑沿完整条带滑移，身后按弧长位置留下幻影并逐渐凝实。
        float leaderAlpha = 1 - Mathf.Clamp01((age - pattern.布阵时长) / .18f);
        if (leaderAlpha > 0)
        {
            float distance = Mathf.Clamp01(age / pattern.布阵时长);
            var arc = ribbon; var rotation = visual.transform.rotation * arc.SwordRotation(distance);
            particles[visible++] = new ParticleSystem.Particle { position = visual.transform.TransformPoint(arc.Evaluate(distance)),
                velocity = Vector3.zero, startLifetime = 12, remainingLifetime = 12, startSize = pattern.剑缩放 * 1.15f,
                rotation3D = rotation.eulerAngles, startColor = new Color(1, 1, 1, leaderAlpha), randomSeed = 65535 };
        }
        pattern.剑群.SetParticles(particles, visible);
        UpdateDust(age);
        if (finished == swords.Length) Destroy(gameObject);
        else if (age > 13) Destroy(gameObject); // 极端低帧率/配置错误也不遗留场景资源。
    }
    float FlowDistance(Sword sword, float age) => Mathf.Repeat(sword.distance01 + Mathf.Max(0, age - sword.revealTime) * pattern.条带流速 / ribbon.Length, 1);
    float RibbonOpacity(float distance)
    {
        float edge = Mathf.Min(distance, 1 - distance) / Mathf.Max(.001f, pattern.端部虚化比例);
        return pattern.剑幕透明度 * Mathf.Lerp(.08f, 1, Mathf.SmoothStep(0, 1, edge));
    }
    Vector3 AreaPoint(Vector3 center, int id) => center + SpiralSwordCurves.AreaOffset(id, swords.Length, ability.范围);
    Vector3 GroundPoint(Vector3 point)
    {
        int count = Physics.RaycastNonAlloc(point + Vector3.up * 12, Vector3.down, sweep, 60, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity; Vector3 ground = point;
        ground.y -= manualAim.HasValue ? 0 : .8f;
        for (int i = 0; i < count; i++)
        {
            var hit = sweep[i]; var collider = hit.collider;
            if (!collider || collider.transform.IsChildOf(owner.transform) || collider.GetComponentInParent<NpcInstance>()
                || hit.normal.y < .35f || hit.distance >= nearest) continue;
            nearest = hit.distance; ground = hit.point;
        }
        return ground - Vector3.up * .08f;
    }
    static float Noise01(int key)
    {
        uint value = (uint)(key + 1); value ^= value >> 16; value *= 0x7feb352d; value ^= value >> 15;
        value *= 0x846ca68b; value ^= value >> 16; return (value & 0xffff) / 65535f;
    }
    float RibbonPresence(Sword sword) => !sword.launched ? 1 : 1 - Mathf.Clamp01((Time.time - sword.launchTime) / .24f);
    void UpdateDust(float age)
    {
        if (!pattern.星屑) return;
        int count = 0, spacing = Mathf.Clamp(pattern.星光剑间隔, 2, 16);
        for (int i = 0; i < swords.Length && count < 768; i++)
        {
            if (i % spacing != 0) continue;
            var sword = swords[i]; int nextIndex = Mathf.Min(swords.Length - 1, i + 1);
            float presence = (RibbonPresence(sword) + RibbonPresence(swords[nextIndex])) * .5f;
            if (presence <= 0) continue;
            float alpha = Mathf.SmoothStep(0, 1, (age - sword.revealTime) / pattern.单剑显现时长) * presence;
            if (alpha <= 0) continue;
            float distance = FlowDistance(sword, age), seam = .45f / ribbon.Length;
            alpha *= RibbonOpacity(distance) * Mathf.SmoothStep(0, 1, distance / seam) * Mathf.SmoothStep(0, 1, (1 - distance) / seam);
            var rotation = ribbon.SwordRotation(distance);
            float edge = (i / spacing) % 2 == 0 ? .03f : -1;
            var local = ribbon.Evaluate(distance) + rotation * Vector3.forward * (pattern.剑缩放 * 2.2f * edge)
                + rotation * Vector3.up * (.025f * Mathf.Sin(age * 1.8f + i));
            float twinkle = .45f + .55f * Mathf.Pow(.5f + .5f * Mathf.Sin(age * 3 + i * 1.7f), 2);
            dust[count++] = Mote(visual.transform.TransformPoint(local), (.035f + .025f * Noise01(i + 503)), alpha * twinkle * .4f, i);
        }
        // 每柄飞剑最多一颗离散微光；插地后不保留尾部光点。
        for (int i = 0; i < swords.Length && count < dust.Length; i++)
        {
            var sword = swords[i]; if (!sword.launched || sword.contact || sword.finished) continue;
            float u = Mathf.Max(0, sword.previousU - .035f);
            dust[count++] = Mote(sword.flight.Evaluate(u), .045f, .5f, 2048 + i);
        }
        pattern.星屑.SetParticles(dust, count);
    }
    static ParticleSystem.Particle Mote(Vector3 position, float size, float alpha, int seed) => new ParticleSystem.Particle {
        position = position, velocity = Vector3.zero, startLifetime = 12, remainingLifetime = 12,
        startSize = size, startColor = new Color(.5f, .8f, 1, alpha), randomSeed = (uint)(seed + 1) };
    static Quaternion AimRotation(Vector3 direction, Vector3 up, Vector3 side)
    {
        if (direction.sqrMagnitude < .000001f) return Quaternion.identity;
        direction.Normalize(); return Quaternion.LookRotation(direction, Mathf.Abs(Vector3.Dot(direction, up)) > .95f ? side : up);
    }
    void Advance(Sword sword, int id)
    {
        float u = Mathf.Clamp01((Time.time - sword.launchTime) / sword.duration);
        int steps = Mathf.Clamp(Mathf.CeilToInt((u - sword.previousU) * sword.duration / .016f), 1, 8);
        float oldU = sword.previousU;
        for (int step = 1; step <= steps; step++)
        {
            float t = Mathf.Lerp(oldU, u, (float)step / steps);
            Vector3 to = sword.flight.Evaluate(t), from = sword.position, delta = to - from;
            float nearest = float.PositiveInfinity; Vector3 point = to; bool hit = false, ground = false;
            if (delta.sqrMagnitude > .000001f)
            {
                int count = Physics.SphereCastNonAlloc(from, pattern.弹体半径, delta.normalized, sweep, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < count; j++)
                {
                    var collider = sweep[j].collider;
                    if (!collider || collider.transform.IsChildOf(owner.transform) || sweep[j].distance >= nearest) continue;
                    nearest = sweep[j].distance; point = sweep[j].point; hit = true;
                    ground = sweep[j].normal.y > .35f && !collider.GetComponentInParent<NpcInstance>();
                }
            }
            // 无碰撞薄地面也只在抵达端点时结算，不在发射时预扣伤害。
            if (hit || t >= 1)
            {
                sword.planted = ground || !hit;
                sword.position = sword.planted ? point - Vector3.up * .08f : point;
                sword.rotation = AimRotation(sword.flight.Velocity(t), sword.flight.Up, sword.flight.Side);
                sword.previousU = t; sword.contact = true; sword.contactTime = Time.time; active--;
                Contact(id, point, delta); return;
            }
            sword.position = to; sword.previousU = t;
        }
    }
    void Contact(int id, Vector3 point, Vector3 direction)
    {
        contacted++;
        CombatImpactPipeline.接触(ability.神通id, point, pattern.命中半径, source: owner, direction: direction);
        var batch = new CombatHitBatch();
        var spec = new AttackSpec(ability.伤害属性, ability.攻击类别, false, ability.伤害倍率);
        int count = Physics.OverlapSphereNonAlloc(point, pattern.命中半径, overlaps, owner.敌人层, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var npc = overlaps[i] ? overlaps[i].GetComponentInParent<NpcInstance>() : null;
            if (!npc || npc.IsDead || (!npc.是敌对目标 && npc != target)) continue;
            var result = batch.命中(new NpcTarget(npc), new CombatHitContext(owner.战斗属性, spec, owner, ability.神通id, point, direction, id + 1));
            if (result.状态 != CombatHitState.Duplicate) 命中次数++;
        }
    }
    void ReleaseAction() { if (ownsAction && actions) actions.释放施法(this); ownsAction = false; }
    void OnDestroy()
    {
        if (ownsAction && actions && (actions.当前动作 == clip || actions.当前动作 == volleyClip)) actions.停止动作();
        ReleaseAction();
    }
}
