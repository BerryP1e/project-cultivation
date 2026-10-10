using System;
using UnityEngine;

public readonly struct CombatEnvironmentContact
{
    public readonly string 来源id;
    public readonly object 来源;
    public readonly Vector3 接触点;
    public readonly float 半径, 深度;
    public readonly CombatEnvironmentShape 形状;
    public CombatEnvironmentContact(string id, object source, Vector3 point, float radius, float depth, CombatEnvironmentShape shape)
    { 来源id = id; 来源 = source; 接触点 = point; 半径 = radius; 深度 = depth; 形状 = shape; }
}

/// <summary>Consumes actual contacts; no added biological damage, no dependence on visual lifetime.</summary>
public static class CombatImpactPipeline
{
    static CombatImpactCatalog catalog;
    public static event Action<CombatEnvironmentContact> 环境接触;
    public static CombatImpactCatalog 配置 => catalog ? catalog : (catalog = Resources.Load<CombatImpactCatalog>("Combat/CombatImpactCatalog"));
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { catalog = null; 环境接触 = null; }
    public static CombatImpactCatalog.Entry 规则(string id) => 配置 ? 配置.查找(id) : null;

    public static void 生物命中(CombatHitEvent hit)
    {
        var context = hit.上下文;
        if (!context.播放命中表现 || !hit.结果.有效接触) return;
        var profile = 规则(context.来源id);
        if (profile != null && !string.IsNullOrEmpty(profile.生物命中特效id))
            CombatVfxPipeline.命中(profile.生物命中特效id, hit.结果, context.接触点, context.来向, context.表现缩放);
    }

    public static void 接触(string id, Vector3 point, float basis = -1f, float visualScale = 1f,
        bool playEffect = true, object source = null, Vector3 direction = default)
    {
        var profile = 规则(id);
        if (profile == null) { Debug.LogWarning("[战斗接触] 缺少配置：" + id); return; }
        float size = basis >= 0f ? basis : profile.默认半径;
        float radius = profile.半径(size), depth = profile.破坏深度(size);
        if (profile.环境形状 == CombatEnvironmentShape.Sphere && radius > 0f) VoxelCombatDamage.Sphere(point, radius);
        else if (profile.环境形状 == CombatEnvironmentShape.Ellipsoid && radius > 0f && depth > 0f) VoxelCombatDamage.Ellipsoid(point, radius, depth);
        if (playEffect && !string.IsNullOrEmpty(profile.地表特效id))
            CombatVfxPipeline.播放(profile.地表特效id, point + profile.地表特效偏移,
                direction.sqrMagnitude > .00001f ? direction : Vector3.down, visualScale);
        var contact = new CombatEnvironmentContact(id, source, point, radius, depth, profile.环境形状);
        if (环境接触 == null) return;
        foreach (Action<CombatEnvironmentContact> callback in 环境接触.GetInvocationList())
            try { callback(contact); } catch (Exception error) { Debug.LogException(error); }
    }
}
