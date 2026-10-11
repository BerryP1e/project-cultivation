using System.Collections.Generic;
using UnityEngine;

/// <summary>复用太虚火弹粒子，包括需要外部补发的沿路粒子；终止后让现存粒子自然消散。</summary>
public sealed class TaichuFireVisual : MonoBehaviour
{
    ParticleSystem[] particles, along;
    Vector3 previous;
    float accumulated, fade = -1;
    public bool 已消散 => fade >= 0;
    void Awake()
    {
        var external = GetComponent<NpcProjectile>(); if (external) external.enabled = false;
        particles = GetComponentsInChildren<ParticleSystem>(true);
        var list = new List<ParticleSystem>();
        foreach (var ps in particles)
        {
            ps.Play(false);
            if (ps.emission.rateOverTime.constantMax <= .001f && ps.emission.burstCount == 0) { list.Add(ps); ps.Emit(1); }
        }
        along = list.ToArray(); previous = transform.position;
    }
    void LateUpdate()
    {
        if (fade >= 0)
        {
            fade += Time.deltaTime; bool alive = false;
            foreach (var ps in particles) if (ps && ps.IsAlive(false)) { alive = true; break; }
            if (!alive || fade > 3f) Destroy(gameObject);
            return;
        }
        accumulated += Vector3.Distance(previous, transform.position) * 8; previous = transform.position;
        int emit = Mathf.Min(8, Mathf.FloorToInt(accumulated)); accumulated -= emit;
        if (emit > 0) foreach (var ps in along) if (ps) ps.Emit(emit);
    }
    public void 消散()
    {
        if (fade >= 0) return;
        fade = 0; foreach (var ps in particles) if (ps) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }
}
