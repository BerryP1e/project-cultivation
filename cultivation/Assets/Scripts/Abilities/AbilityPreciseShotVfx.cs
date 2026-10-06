using UnityEngine;

/// <summary>PreciseShot根节点正Z朝目标；法阵跟随发射点，弹道和拖尾使用世界空间。</summary>
public class AbilityPreciseShotVfx : MonoBehaviour
{
    public float 飞行速度 = 40f;
    ParticleSystem 子弹;
    Transform 命中面;
    Vector3 终点;
    ParticleSystem.Particle[] 粒子;

    void Awake()
    {
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var m = ps.main;
            if (!ps.name.Contains("Mark")) m.simulationSpace = ParticleSystemSimulationSpace.World;
            if (ps.name != "Effect_47_BulletEffects") continue;
            子弹 = ps;
            var 速 = ps.velocityOverLifetime; 速.enabled = false;
            粒子 = new ParticleSystem.Particle[m.maxParticles];
        }
        if (子弹 == null) return;
        var 平面 = new GameObject("PreciseShotTargetPlane");
        命中面 = 平面.transform; 命中面.SetParent(transform, false);
        var c = 子弹.collision; c.enabled = true; c.type = ParticleSystemCollisionType.Planes;
        c.SetPlane(0, 命中面); c.lifetimeLoss = 1f; c.bounce = 0f; c.dampen = 1f;
        c.radiusScale = .1f;
    }

    public void 对准(Vector3 起点, Vector3 目标点)
    {
        终点 = 目标点;
        Vector3 差 = 终点 - 起点;
        transform.position = 起点;
        if (差.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(差);
        if (命中面 != null)
        {
            命中面.position = 终点;
            命中面.rotation = Quaternion.FromToRotation(Vector3.up, -差.normalized);
        }
    }

    void LateUpdate()
    {
        if (子弹 == null) return;
        int n = 子弹.GetParticles(粒子);
        for (int i = 0; i < n; i++)
            粒子[i].velocity = (终点 - 粒子[i].position).normalized * 飞行速度;
        子弹.SetParticles(粒子, n);
    }
}
