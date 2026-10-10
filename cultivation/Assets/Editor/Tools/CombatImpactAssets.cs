using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Creates defaults once. Existing tuned policies are never overwritten automatically.</summary>
public static class CombatImpactAssets
{
    public const string Path = "Assets/resources/Combat/CombatImpactCatalog.asset";
    [MenuItem("修仙/战斗/补齐战斗接触目录（不动场景）")]
    public static void Ensure()
    {
        var asset = AssetDatabase.LoadAssetAtPath<CombatImpactCatalog>(Path);
        if (!asset) { asset = ScriptableObject.CreateInstance<CombatImpactCatalog>(); AssetDatabase.CreateAsset(asset, Path); }
        var entries = new List<CombatImpactCatalog.Entry>(asset.规则);
        foreach (var entry in Defaults()) if (!entries.Exists(x => x != null && x.id == entry.id)) entries.Add(entry);
        asset.规则 = entries.ToArray(); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();
    }
    static CombatImpactCatalog.Entry[] Defaults()
    {
        return new[] {
            new CombatImpactCatalog.Entry { id="basic_taixu_sword_01", 生物命中特效id=CombatVfxPipeline.太虚剑命中 },
            new CombatImpactCatalog.Entry { id="basic_jiuba_01", 生物命中特效id=CombatVfxPipeline.火斩命中 },
            new CombatImpactCatalog.Entry { id="basic_lingxu_sword_01", 生物命中特效id=CombatVfxPipeline.火斩命中 },
            new CombatImpactCatalog.Entry { id="basic_remoteattack_01", 生物命中特效id=CombatVfxPipeline.太虚炼气命中, 地表特效id=CombatVfxPipeline.太虚炼气命中, 环境形状=CombatEnvironmentShape.Sphere },
            new CombatImpactCatalog.Entry { id="basic_thunder_01", 生物命中特效id=CombatVfxPipeline.雷命中, 环境形状=CombatEnvironmentShape.Sphere },
            new CombatImpactCatalog.Entry { id="basic_frostspike_01", 环境形状=CombatEnvironmentShape.Ellipsoid, 固定深度=true, 深度=.325f },
            new CombatImpactCatalog.Entry { id="frost_wave", 环境形状=CombatEnvironmentShape.Ellipsoid, 深度倍率=.25f },
            new CombatImpactCatalog.Entry { id="ability_fentian_yanshu", 落点引导半径倍率=1f/12f },
            new CombatImpactCatalog.Entry { id="meteor_ground", 地表特效id=CombatVfxPipeline.火球落地, 地表特效偏移=Vector3.up*.02f, 默认半径=1, 环境形状=CombatEnvironmentShape.Ellipsoid, 深度倍率=1f/3f },
            new CombatImpactCatalog.Entry { id="ability_bingbao_shu", 环境形状=CombatEnvironmentShape.Ellipsoid, 深度倍率=1f/8f },
            new CombatImpactCatalog.Entry { id="ability_shunlei_tianshan", 环境形状=CombatEnvironmentShape.Ellipsoid, 深度倍率=1f/8f },
            new CombatImpactCatalog.Entry { id="ability_leidong_qianshan", 环境形状=CombatEnvironmentShape.Sphere },
            new CombatImpactCatalog.Entry { id="ability_shuilong_pao", 默认半径=.65f, 环境形状=CombatEnvironmentShape.Sphere },
            new CombatImpactCatalog.Entry { id="ability_xiao_jianzhen", 环境形状=CombatEnvironmentShape.Sphere },
            new CombatImpactCatalog.Entry { id="ability_devileye", 默认半径=.5f, 环境形状=CombatEnvironmentShape.Sphere },
            new CombatImpactCatalog.Entry { id="treasure_qingshan_sword_ultimate", 环境形状=CombatEnvironmentShape.Ellipsoid, 半径倍率=1f/3f, 深度倍率=2f/3f },
        };
    }
}
