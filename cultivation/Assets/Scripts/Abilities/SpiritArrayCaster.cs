using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>灵阵序列、并行分支和穿阵生成物的唯一运行器。使用现有灵力与战斗伤害管线。</summary>
public sealed class SpiritArrayCaster : MonoBehaviour
{
    sealed class Cast { public bool stopped; public SpiritArrayDefinition definition; public Vector3? aim; }
    sealed class Payment { public float credit; }
    struct Cursor { public Vector3 point, direction; public float layoutSign; }
    sealed class Gate {
        public int id; public SpiritArrayNode node; public Vector3 point, direction;
        public float born, opens, expires; public bool completed; public SpiritArrayVisual visual;
    }
    sealed class Bolt {
        public Vector3 point, velocity; public float expires, damage = 1, turn;
        public int depth; public NpcInstance target; public SpiritArrayVisual visual;
        public HashSet<int> visited = new HashSet<int>();
    }
    readonly List<Gate> gates = new List<Gate>();
    readonly List<Bolt> bolts = new List<Bolt>();
    readonly RaycastHit[] hits = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[32];
    ActiveSkillCaster source;
    int nextGate, running;
    float nextHint;
    float treasureReadyAt;
    public float TreasureCooldown=>Mathf.Max(0,treasureReadyAt-Time.time);
    public void BeginTreasureCooldown(float seconds){treasureReadyAt=Time.time+seconds;}
    public int ActiveGates => gates.Count;
    public int ActiveProjectiles => bolts.Count;
    public int RunningSequences => running;
    public int AppliedModifiers { get; private set; }
    public int HitCount { get; private set; }
    public float SpentSpirit { get; private set; }
    public string LastStatus { get; private set; }

    public bool TryCast(ActiveSkillCaster owner, SpiritArrayDefinition definition, Vector3? aim, out string error)
    {
        error = null; source = owner;
        if (!source || !source.生命 || source.生命.IsDead || !source.面板数据) { error = "施法者不可用"; return false; }
        var program = SpiritArrayRules.Get(source.面板数据, definition)?.Copy();
        if (!SpiritArrayRules.Validate(program, out error)) return false;
        if (!definition.灵光材质) { error = "灵阵表现资源缺失，未消耗灵力"; return false; }
        if (running >= 4 || gates.Count >= 64 || bolts.Count >= 128) { error = "当前灵阵过多，请等上一轮结束"; return false; }
        var cursor = FrontCursor(aim);
        bool NeedsTarget(List<SpiritArrayNode> nodes){foreach(var n in nodes)if(n.操作==SpiritArrayOp.目标头顶||(n.子阵!=null&&NeedsTarget(n.子阵)))return true;return false;}
        if (NeedsTarget(program.序列) && !LockedTarget()) { error = "需要先锁定敌人"; return false; }
        if (!source.生命.够灵气(FirstCost(program.序列))) { error = "灵力不足，未开始布阵"; return false; }
        var cast = new Cast { definition = definition, aim = aim }; running++;
        StartCoroutine(Run(cast, program.序列, cursor));
        return true;
    }

    IEnumerator Run(Cast cast, List<SpiritArrayNode> nodes, Cursor cursor)
    {
        try { yield return Sequence(cast, nodes, cursor, new Payment()); }
        finally { running = Mathf.Max(0, running - 1); }
    }
    public static float FirstCost(List<SpiritArrayNode> nodes, int index = 0)
    {
        if (nodes == null || index >= nodes.Count) return 0;
        var n = nodes[index];
        if (n.操作 == SpiritArrayOp.身前布阵 || n.操作 == SpiritArrayOp.目标头顶) return FirstCost(nodes, index + 1);
        if (SpiritArrayRules.Pack(n.操作)) return FirstCost(n.子阵);
        if (n.操作 == SpiritArrayOp.多重施法 || n.操作==SpiritArrayOp.反向多重) { float sum = 0; foreach (var child in n.子阵) sum += FirstCost(new List<SpiritArrayNode> { child }); return sum; }
        return SpiritArrayRules.Gate(n.操作) ? 0 : SpiritArrayRules.Cost(n.操作);
    }
    bool Pay(float amount, Payment payment = null)
    {
        float prepaid = payment == null ? 0 : Mathf.Min(amount, payment.credit);
        if (!source || !source.生命 || source.生命.IsDead || !source.生命.扣灵气(amount - prepaid)) return false;
        if (payment != null) payment.credit -= prepaid;
        SpentSpirit += amount - prepaid;
        return true;
    }
    void Hint(string message)
    {
        LastStatus = message;
        if (Time.unscaledTime < nextHint) return;
        nextHint = Time.unscaledTime + .8f; source?.面板数据?.ShowHint(message);
    }
    NpcInstance LockedTarget()
    {
        var target = source && source.目标管理器 ? source.目标管理器.LockedNpc : null;
        return target && !target.IsDead && target.IsHostile ? target : null;
    }
    bool Alive(Cast cast) => !cast.stopped && source && source.生命 && !source.生命.IsDead;
    Cursor FrontCursor(Vector3? aim,float distance=1.1f)
    {
        var origin=transform.position+Vector3.up*1.1f;
        var locked=LockedTarget();
        var destination=aim ?? (locked ? locked.transform.position+Vector3.up*NpcTarget.判定高度 : origin+transform.forward);
        var direction=destination-origin;
        if(direction.sqrMagnitude<.001f)direction=transform.forward;
        direction.Normalize();
        return new Cursor{point=origin+direction*distance,direction=direction,layoutSign=1};
    }
    IEnumerator Sequence(Cast cast, List<SpiritArrayNode> nodes, Cursor cursor, Payment payment)
    {
        for (int i = 0; i < nodes.Count && Alive(cast); i++) {
            var n = nodes[i];
            if (n.操作 == SpiritArrayOp.身前布阵) {
                float sign=cursor.layoutSign;cursor=FrontCursor(cast.aim,SpiritArrayRules.OriginDistance(n));cursor.layoutSign=sign;cursor.point+=cursor.direction*sign*SpiritArrayRules.Step(n);continue;
            }
            if (n.操作 == SpiritArrayOp.目标头顶) {
                var locked = LockedTarget();
                if (!locked) { Hint("锁定已失效，后续灵阵停止"); cast.stopped = true; yield break; }
                cursor.point = locked.transform.position + Vector3.up * SpiritArrayRules.OriginDistance(n); cursor.direction = Vector3.down;cursor.point+=cursor.direction*cursor.layoutSign*SpiritArrayRules.Step(n);continue;
            }
            if (SpiritArrayRules.Pack(n.操作)) {
                var packed=cursor;packed.layoutSign=cursor.layoutSign*(n.操作==SpiritArrayOp.逆向打包?-1:1);
                packed.point+=packed.direction*packed.layoutSign*SpiritArrayRules.Step(n);
                yield return Sequence(cast, n.子阵, packed, payment);
                cursor.point += cursor.direction * cursor.layoutSign * (SpiritArrayRules.Step(n)+Extent(n.子阵)); continue;
            }
            if (n.操作 == SpiritArrayOp.多重施法 || n.操作 == SpiritArrayOp.反向多重) {
                if (!Alive(cast)) yield break;
                float cost = 0; foreach (var child in n.子阵) cost += FirstCost(new List<SpiritArrayNode> { child });
                if(gates.Count+1+n.子阵.Count>64){Hint("多重施法阵面容量不足，未扣灵力");cast.stopped=true;yield break;}
                if (!Pay(cost, payment)) { Hint("多重施法灵力不足，未展开分支"); cast.stopped = true; yield break; }
                var multi=CreateGate(cast,n,cursor);multi.visual.Flash(true);
                multi.expires=float.PositiveInfinity; // 等待各分支展开，不能按普通阵面寿命提前消失。
                var branches = new List<Coroutine>();
                for (int branch = 0; branch < n.子阵.Count; branch++) {
                    var rotation = Quaternion.LookRotation(cursor.direction, Mathf.Abs(cursor.direction.y) > .98f ? Vector3.forward : Vector3.up);
                    float angle = branch * 360f / n.子阵.Count;
                    var azimuth=rotation * Quaternion.AngleAxis(angle, Vector3.forward);
                    var fan = (azimuth * Quaternion.AngleAxis(n.外倾角, Vector3.up) * Vector3.forward).normalized;
                    var radial = azimuth * Vector3.right * (n.子阵.Count>1?Mathf.Max(1.4f,n.子阵.Count*.36f):0);
                    var branchNodes = new List<SpiritArrayNode> { n.子阵[branch] };
                    bool reverse=n.子阵[branch].操作==SpiritArrayOp.逆向打包;
                    branches.Add(StartCoroutine(Sequence(cast, branchNodes,
                        new Cursor { point = cursor.point + radial + fan * SpiritArrayRules.Step(n)*(reverse?-1:1), direction = fan, layoutSign=1 },
                        new Payment { credit = FirstCost(branchNodes) })));
                }
                foreach (var branch in branches) yield return branch;
                Complete(multi);
                cursor.point += cursor.direction * cursor.layoutSign * (SpiritArrayRules.Step(n)+BranchExtent(n.子阵)); continue;
            }
            if (gates.Count >= 64) { Hint("灵阵数量已达上限"); cast.stopped = true; yield break; }
            float startup = SpiritArrayRules.Gate(n.操作) ? 0 : SpiritArrayRules.Cost(n.操作);
            if (!Pay(startup, payment)) { Hint("灵力不足，后续灵阵停止"); cast.stopped = true; yield break; }
            var gate = CreateGate(cast,n,cursor);
            yield return new WaitForSeconds(SpiritArrayRules.前摇);
            if (!Alive(cast)) yield break;
            if (n.操作 == SpiritArrayOp.生成灵弹) StartCoroutine(Emit(cast, gate));
            cursor.point += cursor.direction * cursor.layoutSign * SpiritArrayRules.Step(n);
        }
    }
    Gate CreateGate(Cast cast,SpiritArrayNode n,Cursor cursor)
    {
        var gate=new Gate{id=++nextGate,node=n,point=cursor.point,direction=cursor.direction,born=Time.time,opens=Time.time+SpiritArrayRules.前摇,expires=Time.time+SpiritArrayRules.前摇+SpiritArrayRules.存活};
        gate.visual=SpiritArrayVisual.Array(cast.definition,gate.point,gate.direction,n.操作);gates.Add(gate);return gate;
    }
    static void Complete(Gate gate)
    {
        if(gate.completed)return;
        gate.completed=true;
        gate.expires=Time.time+SpiritArrayRules.功能完成消散;
    }
    static float Extent(List<SpiritArrayNode> nodes)
    {
        float span=0;foreach(var n in nodes){if(SpiritArrayRules.Pack(n.操作))span+=SpiritArrayRules.Step(n)+Extent(n.子阵);else if(n.操作==SpiritArrayOp.多重施法||n.操作==SpiritArrayOp.反向多重)span+=SpiritArrayRules.Step(n)+BranchExtent(n.子阵);else if(n.操作==SpiritArrayOp.生成灵弹||SpiritArrayRules.Gate(n.操作))span+=SpiritArrayRules.Step(n);}return span;
    }
    static float BranchExtent(List<SpiritArrayNode> nodes){float length=0;foreach(var n in nodes)length=Mathf.Max(length,Extent(new List<SpiritArrayNode>{n}));return length;}
    IEnumerator Emit(Cast cast, Gate gate)
    {
        yield return new WaitForSeconds(gate.node.延迟);
        if (!source || !source.生命 || source.生命.IsDead || !gate.visual.Exists) yield break;
        gate.visual.Flash(true);
        Spawn(new Bolt { point = gate.point, velocity = gate.direction * (gate.node.反向 ? -SpiritArrayRules.灵弹初速 : SpiritArrayRules.灵弹初速),
            damage = Mathf.Clamp(gate.node.参数, .1f, 8), expires = Time.time + 6 }, cast.definition);
        Complete(gate);
    }
    void Spawn(Bolt bolt, SpiritArrayDefinition definition)
    {
        if (bolts.Count >= 128) { Hint("灵弹数量已达上限"); return; }
        bolt.visual = SpiritArrayVisual.Projectile(definition, bolt.point); bolts.Add(bolt);
    }
    void Update()
    {
        if (!source) return;
        if (!source.生命 || source.生命.IsDead) { Clear(); return; }
        for (int i = gates.Count - 1; i >= 0; i--) {
            var g = gates[i];
            if (Time.time >= g.expires) { g.visual.Dispose(); gates.RemoveAt(i); }
            else g.visual.Animate(Time.time - g.born, Mathf.Clamp01((Time.time - g.born) / SpiritArrayRules.前摇), Mathf.SmoothStep(0,1,Mathf.Clamp01((g.expires - Time.time) / SpiritArrayRules.功能完成消散)));
        }
        // 新分裂弹从下一帧开始更新，避免一次 Update 内再次越过同一组阵。
        int original = bolts.Count;
        for (int i = original - 1; i >= 0; i--) {
            var b = bolts[i];
            if (Time.time >= b.expires || !Move(b, Mathf.Min(Time.deltaTime, .1f))) { b.visual.Dispose(); bolts.RemoveAt(i); }
            else b.visual.Move(b.point, b.velocity);
        }
    }
    bool Move(Bolt b, float remaining)
    {
        if (b.turn > 0 && b.target && !b.target.IsDead) {
            var desired = b.target.transform.position + Vector3.up * NpcTarget.判定高度 - b.point;
            b.velocity = Vector3.RotateTowards(b.velocity, desired.normalized * b.velocity.magnitude, b.turn * Mathf.Deg2Rad * remaining, 0);
        }
        for (int step = 0; step < 16 && remaining > .00001f; step++) {
            var delta = b.velocity * remaining; float fraction = 1; Gate crossed = null; Collider collider = null;
            int count = Physics.SphereCastNonAlloc(b.point, .10f, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < count; h++) {
                var c = hits[h].collider; if (!Blocks(c)) continue;
                float f = hits[h].distance / Mathf.Max(.0001f, delta.magnitude);
                if (f <= fraction) { fraction = f; collider = c; }
            }
            // 包含刚生成时重叠的敌人；不靠渲染或触发器回调来结算。
            count = Physics.OverlapSphereNonAlloc(b.point, .10f, overlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int h = 0; h < count; h++) if (Blocks(overlaps[h])) { fraction = 0; collider = overlaps[h]; break; }
            foreach (var gate in gates) {
                if (!SpiritArrayRules.Gate(gate.node.操作) || Time.time < gate.opens || b.visited.Contains(gate.id)) continue;
                float from = Vector3.Dot(b.point - gate.point, gate.direction), along = Vector3.Dot(delta, gate.direction);
                if (Mathf.Abs(along) < .000001f) continue;
                float f = -from / along;
                float radius=SpiritArrayRules.Radius(gate.node.操作);
                if (f < -.00001f || f > fraction || (b.point + delta * f - gate.point).sqrMagnitude > radius * radius) continue;
                fraction = Mathf.Max(0, f); crossed = gate;
            }
            b.point += delta * fraction; remaining *= 1 - fraction;
            if (crossed != null) {
                b.visited.Add(crossed.id); Modify(b, crossed); continue;
            }
            if (collider) { Hit(b, collider); return false; }
            return true;
        }
        return true;
    }
    bool Blocks(Collider c)
    {
        if (!c || c.transform.IsChildOf(transform)) return false;
        var npc = c.GetComponentInParent<NpcInstance>();
        return !npc || !npc.IsDead && npc.IsHostile;
    }
    void Hit(Bolt b, Collider collider)
    {
        var npc = collider.GetComponentInParent<NpcInstance>();
        if (!npc || npc.IsDead || !npc.IsHostile) return;
        var context = new CombatHitContext(source.战斗属性,
            new AttackSpec(DamageNature.特殊, AttackKind.主动神通, false, b.damage), this, "spirit_array", b.point, b.velocity.normalized);
        CombatDamagePipeline.命中(new NpcTarget(npc), context); HitCount++;
    }
    void Modify(Bolt b, Gate gate)
    {
        var n = gate.node; var locked = LockedTarget();
        if (n.操作 == SpiritArrayOp.追踪 && !locked) { Hint("追踪阵没有有效锁定，未扣灵力"); return; }
        int copies = Mathf.Clamp(Mathf.RoundToInt(n.参数), 2, 5);
        if (n.操作 == SpiritArrayOp.分裂 && (b.depth >= 4 || bolts.Count + copies - 1 > 128)) { Hint("分裂达到上限，未扣灵力"); return; }
        if (!Pay(SpiritArrayRules.Cost(n.操作))) { Hint("穿阵灵力不足，本次未附加效果"); gate.visual.Flash(false); return; }
        AppliedModifiers++; gate.visual.Flash(true);
        Complete(gate);
        switch (n.操作) {
            case SpiritArrayOp.追踪: b.target = locked; b.turn = Mathf.Clamp(n.参数, 1, 360); break;
            case SpiritArrayOp.强化: b.damage = Mathf.Clamp(b.damage * n.参数, .1f, 8); break;
            case SpiritArrayOp.加速: b.velocity = b.velocity.normalized * Mathf.Clamp(b.velocity.magnitude * n.参数, SpiritArrayRules.灵弹速度倍率, 60*SpiritArrayRules.灵弹速度倍率); break;
            case SpiritArrayOp.修正方向: b.velocity = gate.direction * b.velocity.magnitude; break;
            case SpiritArrayOp.分裂:
                var velocity = b.velocity; b.depth++;
                var axis = Mathf.Abs(velocity.normalized.y) > .98f ? Vector3.forward : Vector3.up;
                for (int j = 1; j < copies; j++) {
                    var child = new Bolt { point = b.point, velocity = Quaternion.AngleAxis((j - (copies - 1) * .5f) * n.外倾角, axis) * velocity,
                        damage = b.damage, depth = b.depth, expires = b.expires, turn = b.turn, target = b.target, visited = new HashSet<int>(b.visited) };
                    Spawn(child, gate.visual.Definition);
                }
                b.velocity = Quaternion.AngleAxis(-(copies - 1) * .5f * n.外倾角, axis) * velocity; break;
        }
    }
    void Clear()
    {
        StopAllCoroutines(); running = 0;
        foreach (var g in gates) g.visual.Dispose(); foreach (var b in bolts) b.visual.Dispose(); gates.Clear(); bolts.Clear();
    }
    void OnDisable() => Clear();
}
