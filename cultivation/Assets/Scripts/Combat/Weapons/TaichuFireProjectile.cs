using UnityEngine;

/// <summary>一枚火弹一个命中批次；连续扫掠碰撞体，离手之后不再读取伴龙动画。</summary>
public sealed class TaichuFireProjectile : MonoBehaviour
{
    Transform owner;
    PlayerCombatStats stats;
    BasicTaichuDragon01 source;
    NpcInstance locked;
    Vector3 velocity;
    float radius, remaining, fade = -1;
    int segment;
    float multiplier;
    readonly CombatHitBatch batch = new CombatHitBatch();
    TaichuFireVisual visual;
    readonly Collider[] overlaps = new Collider[32];
    readonly RaycastHit[] hits = new RaycastHit[64];

    public void 初始化(BasicTaichuDragon01 ability, NpcInstance target, Vector3 point, float speed, float contactRadius, float range, int index, float damage)
    {
        source = ability; owner = ability.transform; stats = ability.GetComponent<PlayerCombatStats>(); locked = target;
        segment = index; multiplier = damage; radius = contactRadius; remaining = Mathf.Max(.5f, range);
        var direction = point - transform.position; if (direction.sqrMagnitude < .0001f) direction = owner.forward;
        velocity = direction.normalized * speed;
        transform.rotation = Quaternion.LookRotation(velocity.normalized);
        visual = GetComponent<TaichuFireVisual>();
        if (!visual) visual = gameObject.AddComponent<TaichuFireVisual>();
    }
    void Update()
    {
        if (fade >= 0) return;
        if (!owner || !source || !source.enabled || (owner.GetComponent<PlayerVitals>()?.IsDead ?? true)) { Finish(); return; }
        Vector3 start = transform.position, delta = velocity * Time.deltaTime;
        if (delta.magnitude > remaining) delta = delta.normalized * remaining;
        int count = Physics.OverlapSphereNonAlloc(start, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++) if (TryHit(overlaps[i], overlaps[i].ClosestPoint(start))) { Finish(); return; }
        count = Physics.SphereCastNonAlloc(start, radius, velocity.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
        // NonAlloc cast order is unspecified. Resolve the first blocking collider by distance.
        float nearest = float.MaxValue; int chosen = -1;
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].collider; if (!c || c.transform.IsChildOf(owner)) continue;
            var npc = c.GetComponentInParent<NpcInstance>();
            if (npc && (!npc.IsHostile && npc != locked || npc.IsDead)) continue;
            if (hits[i].distance < nearest) { nearest = hits[i].distance; chosen = i; }
        }
        if (chosen >= 0)
        {
            transform.position = hits[chosen].point;
            TryHit(hits[chosen].collider, hits[chosen].point); Finish(); return;
        }
        transform.position = start + delta; remaining -= delta.magnitude;
        if (remaining <= .001f) Finish();
    }
    bool TryHit(Collider collider, Vector3 point)
    {
        if (!collider || collider.transform.IsChildOf(owner)) return false;
        var npc = collider.GetComponentInParent<NpcInstance>();
        if (!npc || npc.IsDead || !npc.IsHostile && npc != locked) return false;
        var result = batch.命中(new NpcTarget(npc), new CombatHitContext(stats,
            new AttackSpec(DamageNature.特殊, AttackKind.普通攻击, false, multiplier), source,
            "taichu_fire", point, velocity.normalized, segment));
        if (result.状态 != CombatHitState.Duplicate) source.记录火弹命中();
        return true;
    }
    void Finish() { fade = 0; visual?.消散(); }
}
