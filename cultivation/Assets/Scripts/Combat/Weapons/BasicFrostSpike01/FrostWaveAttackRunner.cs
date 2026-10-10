using System.Collections.Generic;
using UnityEngine;

/// <summary>沧澜第三式的一道冰波；固定起点和方向，向前扫过敌人，每人仅结算一次特殊普攻。</summary>
public sealed class FrostWaveAttackRunner : MonoBehaviour
{
    PlayerCombatStats attacker;
    NpcInstance locked;
    AttackSpec spec;
    LayerMask layers;
    Vector3 origin, direction;
    float length, width, duration, born, scanned, voxelScanned;
    readonly CombatHitBatch hit = new CombatHitBatch();
    public int 命中数 { get; private set; }
    public System.Action<NpcInstance> 命中回调;

    public void 初始化(PlayerCombatStats attacker, Vector3 origin, Vector3 direction,
        float length, float width, float duration, LayerMask layers, AttackSpec spec,
        NpcInstance locked, string path)
    {
        this.attacker = attacker;
        this.origin = origin;
        this.direction = direction;
        this.length = Mathf.Max(.1f, length);
        this.width = Mathf.Max(.1f, width);
        this.duration = Mathf.Max(.05f, duration);
        this.layers = layers;
        this.spec = spec;
        this.locked = locked;
        born = Time.time;
        var prefab = Resources.Load<GameObject>(path);
        // The native wave's +X axis is 11.5 m long and centred on its root.
        var rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(0, -90, 0);
        特效摆放.生成(path, origin + direction * this.length * .5f, rotation.eulerAngles, this.length / 11.5f,
            对齐到锚点: false, 存活秒: 特效摆放.量特效总时长(prefab, 3) * 1.05f, 名: "沧澜寒渊录_A3_冰波");
        扫描(0, 0);
    }

    void Update()
    {
        if (!attacker || (attacker.GetComponent<PlayerVitals>()?.IsDead ?? false))
        {
            Destroy(gameObject);
            return;
        }
        float end = Mathf.Min(length, length * (Time.time - born) / duration);
        if (end > scanned)
        {
            扫描(scanned, end);
            scanned = end;
        }
        if (scanned >= length) Destroy(gameObject);
    }

    void 扫描(float start, float end)
    {
        // Capsule covers the entire newly traversed segment, including at low frame rates.
        foreach (var c in Physics.OverlapCapsule(origin + direction * start, origin + direction * end,
            width * .5f, layers, QueryTriggerInteraction.Ignore))
        {
            var npc = c.GetComponentInParent<NpcInstance>();
            if (!npc || npc.IsDead || (!npc.是敌对目标 && npc != locked)) continue;
            var result = hit.命中(new NpcTarget(npc),new CombatHitContext(attacker,spec,this,"frost_wave",c.ClosestPoint(origin+direction*end),direction));
            if (result.命中)
            {
                命中数++;
                命中回调?.Invoke(npc);
            }
        }
        // Environment follows the original shallow path cuts, independent of target hits.
        float radius = Mathf.Max(.35f, width * .5f), step = Mathf.Max(.3f, radius);
        while (voxelScanned <= end)
        {
            CombatImpactPipeline.接触("frost_wave",origin + direction * voxelScanned,radius,source:this);
            voxelScanned += step;
        }
    }
}
