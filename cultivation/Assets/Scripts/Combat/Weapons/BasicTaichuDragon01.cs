using System.Collections.Generic;
using UnityEngine;

/// <summary>太初龙栖诀：近处五式、远处冰晶/火弹/雷击三式；所有生物伤害由实际碰撞送入普攻管线。</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(150)]
public sealed class BasicTaichuDragon01 : MonoBehaviour
{
    public const string 方法id = "basic_taichu_dragon_01";
    public TaichuDragonDefinition 配置;
    public bool 自动出手 = true;
    [Min(0)] public float 基础冷却 = .1f;
    public int 当前式 { get; private set; } = -1;
    public int 下次式 => 下一式(下一段, 远程循环, 目标在近处());
    public float 下次范围 => RangeFor(下次式);
    public int 引导式(Vector3 point) => 下一式(下一段, 远程循环, 配置 && Vector3.Distance(transform.position, point) <= 配置.近战距离);
    public float 引导范围(Vector3 point) => RangeFor(引导式(point));
    float RangeFor(int stage) => 配置 ? (stage == 2 ? 配置.冰晶引导半径 : stage == 3 ? 配置.火弹判定半径 : stage == 4 ? 配置.雷击半径 : 配置.近战距离) : .6f;
    public bool 出手动作中 { get; private set; }
    public int 本次命中数 { get; private set; }
    public float 攻速系数 => stats ? Mathf.Max(.1f, stats.当前属性[AttributeType.AttackSpeed]) : 1f;
    public float 动作播放倍率 => 攻速系数 * (配置 ? 配置.动作速度倍率 : .6f);
    public float 冷却剩余 => Mathf.Max(0, nextTime - Time.time);
    public static int 下一式(int next, bool wasRanged, bool near) => near ? (wasRanged ? 0 : Mathf.Clamp(next, 0, 4)) : (next >= 2 && next <= 4 ? next : 2);

    PlayerAnimationController 动画;
    PlayerCombatStats stats;
    NpcTargeting targeting;
    PlayerVitals vitals;
    演出锁 cutscene;
    Animator player;
    TaichuDragonRig rig;
    float fadeTime, idleTime, nextTime, lastProgress;
    bool 远程循环, manual, lightning, normallyFinished;
    int 下一段;
    bool specialIdle;
    float stillTime, nextIdleCheck;
    Vector3 lastPosition;
    TaichuDragonDefinition.DragonMotion resting;
    public bool 特殊待机中 => specialIdle;
    public string 当前伴龙动作 => rig == null ? "" : rig.根.name;
    public Vector3 当前旅程偏移 => rig == null ? Vector3.zero : rig.旅程偏移;
    bool IdleUnavailable => (GetComponent<YufengFlight>()?.御风流程中 ?? false) || (GetComponent<MountRider>()?.骑乘中 ?? false) || (targeting && targeting.LockedNpc);
    bool Moving => (GetComponent<PlayerController>()?.CurrentSpeed ?? 0) > .1f || Vector3.ProjectOnPlane(transform.position - lastPosition, Vector3.up).sqrMagnitude > .000001f;
    AnimationClip playing;
    NpcInstance locked;
    Vector3 aim;
    bool[] released;
    CombatHitBatch meleeBatch;
    readonly List<Vector3> vertices = new List<Vector3>(2048);
    Vector3[] previous;
    int[] tailTriangles;
    bool havePrevious;
    readonly Collider[] contacts = new Collider[128];
    readonly HashSet<int> struck = new HashSet<int>();

    void Awake() => Resolve();
    void Resolve()
    {
        if (!配置) 配置 = Resources.Load<TaichuDragonDefinition>("Weapons/TaichuDragon/Definition");
        if (!动画) 动画 = GetComponent<PlayerAnimationController>();
        if (!stats) stats = GetComponent<PlayerCombatStats>();
        if (!targeting) targeting = GetComponent<NpcTargeting>();
        if (!vitals) vitals = GetComponent<PlayerVitals>();
        if (!cutscene) cutscene = GetComponent<演出锁>();
        if (动画) player = 动画.animator;
    }
    void OnEnable() { Resolve(); 下一段 = 0; 远程循环 = false; stillTime = 0; nextIdleCheck = 0; lastPosition = transform.position; if (动画) 动画.动作播完 += OnMotionFinished; }
    void OnMotionFinished() { if (出手动作中) normallyFinished = true; }
    void OnDisable()
    {
        if (动画) 动画.动作播完 -= OnMotionFinished;
        if ((出手动作中 || specialIdle) && 动画 && 动画.当前动作 == playing) 动画.停止动作();
        出手动作中 = specialIdle = false; resting = null; rig?.Dispose(); rig = null;
    }
    bool InputAllowed => enabled && 配置 && player && 动画 && vitals && !vitals.IsDead
        && !UiEscRegistry.SceneInputBlocked && !(cutscene && cutscene.正在锁) && !动画.施法占用中 && Time.timeScale > 0;
    bool 目标在近处() => targeting && targeting.LockedNpc && !targeting.LockedNpc.IsDead
        && Vector3.Distance(transform.position, targeting.LockedNpc.transform.position) <= 配置.近战距离;
    public bool 手动出手(Vector3 point)
    {
        if (targeting && targeting.LockedNpc && !targeting.LockedNpc.IsDead) return false;
        return StartAttack(true, point);
    }
    public bool 出手() => StartAttack(false, default);
    bool StartAttack(bool byMouse, Vector3 point)
    {
        Resolve(); if (CombatHitStop.正在顿帧(transform) || !InputAllowed || 出手动作中 || 动画.动作播放中 || 冷却剩余 > 0) return false;
        var target = targeting ? targeting.LockedNpc : null;
        if (!byMouse && (!target || target.IsDead)) return false;
        bool near = byMouse ? Vector3.Distance(transform.position, point) <= 配置.近战距离 : 目标在近处();
        int stage = 下一式(下一段, 远程循环, near);
        if (stage >= 2 && Vector3.Distance(transform.position, byMouse ? point : target.transform.position) > PlayerManualAim.SenseRange(gameObject)) return false;
        if (配置.五式 == null || 配置.五式.Length != 5) return false;
        var motion = 配置.五式[stage];
        if (motion == null || !motion.玩家动作 || !motion.龙动作 || !motion.龙预制体) return false;
        GetComponent<PlayerController>()?.对准攻击方向((byMouse ? point : target.transform.position) - transform.position);
        StopIdle();
        if (!动画.播动作(motion.玩家动作, 动作播放倍率)) return false;
        当前式 = stage; manual = byMouse; locked = byMouse ? null : target; aim = byMouse ? point : target.transform.position;
        playing = motion.玩家动作; 远程循环 = !near; 下一段 = stage == 4 ? (near ? 0 : 2) : stage + 1;
        released = new bool[配置.冰刺.Length]; lightning = false; havePrevious = false; lastProgress = 0; normallyFinished = false;
        meleeBatch = new CombatHitBatch(); struck.Clear(); 本次命中数 = 0;
        出手动作中 = true; SwitchRig(motion);
        if (stage == 2) rig.设置目标旅程(aim);
        return true;
    }
    void Update()
    {
        Resolve();
        if (!配置 || !player) return;
        if (vitals && vitals.IsDead) { rig?.Dispose(); rig = null; 出手动作中 = false; StopIdle(); return; }
        if(CombatHitStop.正在顿帧(transform))return;
        if (specialIdle && (Moving || IdleUnavailable || !InputAllowed || 动画.当前动作 != playing)) StopIdle();
        // Current clip cleared at normal completion is different from replacement by a spell.
        if (出手动作中 && 动画.当前动作 != playing && !normallyFinished) EndAttack(false);
        if (!出手动作中 && InputAllowed && !PlayerManualAim.AltHeld && (自动出手 || Input.GetKey(KeyCode.F) || Input.GetMouseButton(1))) 出手();
    }
    void LateUpdate()
    {
        if(CombatHitStop.正在顿帧(transform))return;
        if (!配置 || !player || !vitals || vitals.IsDead) return;
        if (rig == null && 配置.待机 != null) { SwitchRig(配置.待机); resting = 配置.待机; }
        if (rig == null) return;
        rig.推进过渡(Time.deltaTime);
        float p;
        if (出手动作中)
        {
            bool done = normallyFinished;
            p = done ? 1f : 动画.动作进度;
            if (!done && 动画.当前动作 != playing) { EndAttack(false); return; }
            rig.采样(p);
            if (当前式 < 2) SampleDragonContact(p);
            else if (当前式 == 2) SampleDragonContact(p);
            else if (当前式 == 4 && !lightning && p >= 配置.雷击进度) { lightning = true; StrikeLightning(); }
            else if (当前式 == 3) ReleaseFire(p);
            lastProgress = p;
            if (done || p >= .9999f) EndAttack(true);
        }
        else
        {
            bool moving = Moving;
            if (!specialIdle)
            {
                var desired = moving && 配置.移动 != null ? 配置.移动 : 配置.待机;
                if (resting != desired) { SwitchRig(desired); resting = desired; }
                bool canIdle = !moving && InputAllowed && !动画.动作播放中 && !(targeting && targeting.LockedNpc)
                    && !IdleUnavailable;
                stillTime = canIdle ? stillTime + Time.deltaTime : 0;
                if (canIdle && stillTime >= 配置.特殊待机等待 && Time.time >= nextIdleCheck)
                {
                    nextIdleCheck = Time.time + 配置.特殊待机检查间隔;
                    if (Random.value < 配置.特殊待机概率 && 配置.特殊待机 != null && 动画.播可打断待机(配置.特殊待机.玩家动作, 配置.动作速度倍率))
                    { specialIdle = true; playing = 配置.特殊待机.玩家动作; SwitchRig(配置.特殊待机); }
                }
            }
            if (specialIdle)
            {
                if (动画.当前动作 != playing) StopIdle();
                else rig.采样(动画.动作进度);
            }
            if (!specialIdle) { idleTime += Time.deltaTime * 配置.动作速度倍率; rig.采样(Mathf.Repeat(idleTime / rig.长度, 1f)); }
        }
        lastPosition = transform.position;
        fadeTime += Time.deltaTime; rig.透明度(Mathf.Clamp01(fadeTime / .15f));
    }
    void StopIdle()
    {
        if (!specialIdle) return;
        specialIdle = false; stillTime = 0; nextIdleCheck = Time.time + 配置.特殊待机等待;
        if (动画 && 动画.可打断待机中 && 动画.当前动作 == playing) 动画.停止动作();
        resting = null;
    }
    void EndAttack(bool complete)
    {
        出手动作中 = false; nextTime = Time.time + 基础冷却 / 动作播放倍率;
        if (!complete) { 下一段 = 0; 远程循环 = false; }
        SwitchRig(配置.待机); resting = 配置.待机; stillTime = 0;
    }
    void SwitchRig(TaichuDragonDefinition.DragonMotion motion)
    {
        if (motion == null || !motion.龙预制体 || !motion.龙动作) return;
        var old = rig;
        rig = new TaichuDragonRig(motion, player, 配置, old);
        old?.根.SetActive(false); old?.Dispose();
        tailTriangles = null;
        fadeTime = old == null ? 0 : .15f; idleTime = 0;
    }
    float Multiplier => 配置.伤害倍率 != null && 当前式 < 配置.伤害倍率.Length ? 配置.伤害倍率[当前式] : 1f;
    bool IsTarget(Collider c, out NpcInstance npc)
    {
        npc = c ? c.GetComponentInParent<NpcInstance>() : null;
        return npc && !npc.IsDead && (npc.IsHostile || npc == locked) && !npc.transform.IsChildOf(transform);
    }
    void SampleDragonContact(float progress)
    {
        var motion = 配置.五式[当前式]; if (!rig.龙网格) return;
        if (progress < motion.判定开始 || lastProgress > motion.判定结束) { havePrevious = false; return; }
        // The swept interval must stay inside the authored contact window, even after a slow frame.
        if (!havePrevious)
        {
            rig.采样(Mathf.Clamp(lastProgress, motion.判定开始, motion.判定结束));
            if (当前式 == 2) rig.取冰晶世界顶点(vertices); else rig.取世界顶点(vertices);
            if (previous == null || previous.Length != vertices.Count) previous = new Vector3[vertices.Count];
            vertices.CopyTo(previous); havePrevious = true;
        }
        float start = Mathf.Clamp(lastProgress, motion.判定开始, motion.判定结束);
        float end = Mathf.Min(progress, motion.判定结束);
        // A1折返甩尾的弧线不能只连两帧端点，否则掉帧会跳过向前伸出的部分。
        int samples = 当前式 == 0 ? Mathf.Max(1, Mathf.CeilToInt((end - start) / .015f)) : 1;
        for (int sample = 1; sample <= samples; sample++)
        {
            rig.采样(Mathf.Lerp(start, end, (float)sample / samples));
            SampleContactPose();
        }
        rig.采样(progress);
    }
    void SampleContactPose()
    {
        if (当前式 == 2) rig.取冰晶世界顶点(vertices); else rig.取世界顶点(vertices);
        if (vertices.Count == 0) return;
        if (previous == null || previous.Length != vertices.Count) previous = new Vector3[vertices.Count];
        var bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 0; i < vertices.Count; i++)
        {
            bounds.Encapsulate(vertices[i]);
            if (havePrevious) bounds.Encapsulate(previous[i]);
        }
        bool downward = 当前式 == 0 && 配置.甩尾向下覆盖;
        float tolerance = 当前式 == 0 ? 配置.甩尾接触容差 : 配置.接触容差;
        if (downward) bounds.Encapsulate(new Vector3(bounds.center.x, transform.position.y, bounds.center.z));
        bounds.Expand(tolerance * 2);
        int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, contacts, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int n = 0; n < count; n++)
        {
            var c = contacts[n]; if (!IsTarget(c, out var npc) || struck.Contains(npc.GetInstanceID())) continue;
            float contactHeight = c.bounds.center.y;
            bool contacted = false;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 current = vertices[i], prior = previous[i];
                if (downward)
                {
                    // 仅沿动画真实扫过的水平位置向下延展，不扩大成玩家周围的固定圆。
                    current.y = Mathf.Min(current.y, Mathf.Max(transform.position.y, contactHeight));
                    prior.y = Mathf.Min(prior.y, Mathf.Max(transform.position.y, contactHeight));
                }
                var point = c.ClosestPoint(current); bool hit = (point - current).sqrMagnitude <= tolerance * tolerance;
                Vector3 delta = current - prior;
                if (!hit && havePrevious && delta.sqrMagnitude > .000001f && c.Raycast(new Ray(prior, delta.normalized), out var h, delta.magnitude)) { hit = true; point = h.point; }
                if (!hit) continue;
                struck.Add(npc.GetInstanceID());
                meleeBatch.命中(new NpcTarget(npc), new CombatHitContext(stats,
                    new AttackSpec(当前式 == 2 ? DamageNature.特殊 : DamageNature.物理, AttackKind.普通攻击, false, Multiplier), this, 当前式 == 2 ? "taichu_crystal" : "taichu_melee", point,
                    havePrevious && delta.sqrMagnitude > .000001f ? delta.normalized : transform.forward, 当前式));
                本次命中数++; contacted = true; break;
            }
            // 只测表面顶点会漏掉处在龙身体投影内部的小目标；三角形补齐实心攻击包络。
            if (downward && !contacted && TailFootprintContact(c, tolerance, out var footprintPoint))
            {
                struck.Add(npc.GetInstanceID());
                meleeBatch.命中(new NpcTarget(npc), new CombatHitContext(stats,
                    new AttackSpec(DamageNature.物理, AttackKind.普通攻击, false, Multiplier), this,
                    "taichu_melee", footprintPoint, transform.forward, 当前式));
                本次命中数++;
            }
        }
        for (int i = 0; i < vertices.Count; i++) previous[i] = vertices[i]; havePrevious = true;
    }
    bool TailFootprintContact(Collider collider, float tolerance, out Vector3 point)
    {
        if (tailTriangles == null) tailTriangles = rig.龙网格.sharedMesh.triangles;
        var targetBounds = collider.bounds; var center = targetBounds.center; var p = new Vector2(center.x, center.z);
        for (int i = 0; i < tailTriangles.Length; i += 3)
        {
            var a = vertices[tailTriangles[i]]; var b = vertices[tailTriangles[i + 1]]; var c = vertices[tailTriangles[i + 2]];
            if (Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < targetBounds.min.y - tolerance
                || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < targetBounds.min.x - tolerance
                || Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > targetBounds.max.x + tolerance
                || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < targetBounds.min.z - tolerance
                || Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > targetBounds.max.z + tolerance) continue;
            var projected = ClosestOnTriangle(p, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z));
            var probe = new Vector3(projected.x, Mathf.Max(transform.position.y, center.y), projected.y);
            point = collider.ClosestPoint(probe);
            if ((point - probe).sqrMagnitude <= tolerance * tolerance) return true;
        }
        point = default; return false;
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static Vector2 ClosestOnTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        if (Mathf.Abs(Cross(b - a, c - a)) > .000001f)
        {
            float ab = Cross(b - a, p - a), bc = Cross(c - b, p - b), ca = Cross(a - c, p - c);
            if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return p;
        }
        var x = ClosestOnSegment(p, a, b); var y = ClosestOnSegment(p, b, c); var z = ClosestOnSegment(p, c, a);
        return (p - x).sqrMagnitude < (p - y).sqrMagnitude ? ((p - x).sqrMagnitude < (p - z).sqrMagnitude ? x : z)
            : ((p - y).sqrMagnitude < (p - z).sqrMagnitude ? y : z);
    }
    static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
    { var d = b - a; return a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.000001f, d.sqrMagnitude)); }
    void StrikeLightning()
    {
        // Keep the cast's locked root/aim point; collision volume handles any target within it.
        var point = !manual && locked && !locked.IsDead ? locked.transform.position : aim;
        CombatVfxPipeline.播放(CombatVfxPipeline.雷四式[0], point, Vector3.down);
        var batch = new CombatHitBatch();
        int count = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * .1f, point + Vector3.up * 2.5f, 配置.雷击半径, contacts, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) if (IsTarget(contacts[i], out var npc))
        {
            var result = batch.命中(new NpcTarget(npc), new CombatHitContext(stats,
                new AttackSpec(DamageNature.特殊, AttackKind.普通攻击, false, Multiplier), this, "taichu_thunder", contacts[i].ClosestPoint(point + Vector3.up), Vector3.down, 4));
            if (result.状态 != CombatHitState.Duplicate) 本次命中数++;
        }
    }
    void ReleaseFire(float progress)
    {
        for (int i = 0; i < 配置.冰刺.Length; i++)
        {
            if (released[i] || progress < 配置.冰刺[i].释放进度) continue;
            var release = 配置.冰刺[i];
            rig.采样(release.释放进度);
            var node = rig.冰刺节点(release.节点路径);
            var visual = rig.释放火弹(release.节点路径);
            if (node && visual)
            {
                visual.name = "太初火弹_" + (i + 1);
                var point = !manual && locked && !locked.IsDead ? new NpcTarget(locked).判定点 : aim;
                visual.gameObject.AddComponent<TaichuFireProjectile>().初始化(this, locked, point, 配置.火弹速度, 配置.火弹判定半径,
                    PlayerManualAim.SenseRange(gameObject) + Vector3.Distance(node.position, transform.position), i, Multiplier);
            }
            released[i] = true; rig.采样(progress);
        }
    }
    public void 记录火弹命中() => 本次命中数++;
}
