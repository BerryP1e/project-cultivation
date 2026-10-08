using UnityEngine;

/// <summary>落剑沿发射点前方移动，按父级缩放换算；触地特效归属剑阵，不依赖演示场景。</summary>
public class AbilityFallingSword : MonoBehaviour
{
    public float 速度 = 40f;
    public GameObject 触地特效;
    float 地面高度, 出生时间, 消散开始;
    public bool 已落地 { get; private set; }
    public bool 正在消散 { get; private set; }
    void Start()
    {
        var 阵 = GetComponentInParent<DirectedAbilityRunner>();
        地面高度 = 阵 != null ? 阵.transform.position.y : transform.root.position.y;
        出生时间 = Time.time;
    }
    void Update()
    {
        if (正在消散)
        {
            float alpha = 1f - Mathf.Clamp01((Time.time - 消散开始) / .6f);
            foreach (var 系统 in GetComponentsInChildren<ParticleSystem>())
            {
                var 粒子 = new ParticleSystem.Particle[系统.particleCount];
                int n = 系统.GetParticles(粒子);
                for (int i = 0; i < n; i++) { Color c = 粒子[i].startColor; c.a = alpha; 粒子[i].startColor = c; }
                系统.SetParticles(粒子, n);
            }
            return;
        }
        if (已落地) return;
        Vector3 前 = transform.position;
        transform.localPosition += transform.localRotation * Vector3.forward * (速度 * Time.deltaTime);
        Vector3 后 = transform.position;
        bool hitSurface=VoxelCombatDamage.Ray(前,后,out var surface);
        if (hitSurface || !VoxelCombatDamage.Active && 后.y <= 地面高度 && 前.y > 地面高度)
        {
            float t = Mathf.InverseLerp(后.y, 前.y, 地面高度);
            Vector3 命中 = hitSurface?surface.point:Vector3.Lerp(后, 前, t);
            transform.position = 命中;
            已落地 = true;
            VoxelCombatDamage.Sphere(命中,Mathf.Clamp(transform.lossyScale.x*.7f,.45f,1.5f));
            foreach (var 粒子 in GetComponentsInChildren<ParticleSystem>(true))
            {
                if (粒子.name.Contains("SwordBody") || 粒子.name.Contains("SwordHead"))
                {
                    // 原剑身五秒后自行淡出；停在成形阶段，寿命交给剑阵管理。
                    粒子.Simulate(.5f, false, true);
                    粒子.Pause(false);
                }
                else 粒子.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        else if (Time.time - 出生时间 > 2f) Destroy(gameObject);
    }

    public void 消散()
    {
        if (正在消散) return;
        正在消散 = true;
        消散开始 = Time.time;
        // 脱离即将销毁的剑阵，只留下短暂的收尾，不再参与伤害。
        transform.SetParent(null, true);
        foreach (var 粒子 in GetComponentsInChildren<ParticleSystem>(true)) 粒子.Pause(false);
        if (触地特效 != null)
        {
            var 特效 = Instantiate(触地特效, transform.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            特效.transform.localScale = transform.lossyScale;
            foreach (var 节点 in 特效.GetComponentsInChildren<Transform>(true)) 节点.gameObject.layer = gameObject.layer;
            // 每把剑采用独立方向；取消源素材定向的速度，细粒子向球形外围散开。
            foreach (var 粒子 in 特效.GetComponentsInChildren<ParticleSystem>(true))
            {
                粒子.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var 主 = 粒子.main; 主.loop = false;
                if (粒子.GetComponent<ParticleSystemRenderer>().renderMode == ParticleSystemRenderMode.Billboard)
                {
                    var 形 = 粒子.shape; 形.enabled = true; 形.shapeType = ParticleSystemShapeType.Sphere; 形.radius = .25f;
                    var 速 = 粒子.velocityOverLifetime; 速.enabled = false;
                    主.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
                }
                粒子.Play(false);
            }
            Destroy(特效, 1.2f);
        }
        Destroy(gameObject, .6f);
    }
}
