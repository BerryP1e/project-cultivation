using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>骤雨初歇派生资产和共享目录；不保存场景、不复制第三方资源整包。</summary>
public static class SpiralSwordAbilityBaker
{
    public const string Id = "ability_zhouyu_chuxie";
    public const string Output = "Assets/resources/Abilities/SpiralSwordRain";
    const string Art = "Assets/Art/VFX/Project/SpiralSwordRain";
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static Material SaveMaterial(Material material, string name)
    {
        material.name = name;
        string path = Art + "/" + name + ".mat"; var old = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (old) { EditorUtility.CopySerialized(material, old); EditorUtility.SetDirty(old); UnityEngine.Object.DestroyImmediate(material); return old; }
        AssetDatabase.CreateAsset(material, path); return material;
    }
    static Material Glow(string name, Texture texture)
    {
        var shader = Shader.Find("Particles/Standard Unlit");
        if (!shader) throw new InvalidOperationException("缺少内置粒子着色器");
        var material = new Material(shader) { name = name, mainTexture = texture, color = Color.white, renderQueue = 3100, enableInstancing = true };
        material.SetFloat("_Mode", 2); material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); material.SetInt("_DstBlend", (int)BlendMode.One);
        material.SetInt("_ZWrite", 0); material.EnableKeyword("_ALPHABLEND_ON");
        return SaveMaterial(material, name);
    }
    [MenuItem("修仙/神通/重建骤雨初歇（不动场景）")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式");
        Folder(Output); Folder(Art);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Weapons/Sword2/Sword2_TipForward.asset");
        if (!mesh) throw new InvalidOperationException("请先生成首批神通的校准剑网格");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/sword2/sword2.fbx").GetComponentInChildren<MeshRenderer>().sharedMaterial;
        var swordMaterial = Glow("SwordGlow", source.mainTexture);
        var sourceHit = AssetDatabase.LoadAssetAtPath<GameObject>(FirstWaveAbilitiesBaker.Output + "/SwordHit.prefab");
        var sparkSource = sourceHit.GetComponentsInChildren<ParticleSystem>().First(p => p.name == "SparksExpl");
        var sparkMaterial = Glow("StarGlow", sparkSource.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture);
        sparkMaterial.color = new Color(1.6f, 1.9f, 2.5f, 1); EditorUtility.SetDirty(sparkMaterial);
        var root = new GameObject("骤雨初歇");
        GameObject cast;
        try
        {
            var config = root.AddComponent<SpiralSwordPattern>();
            config.剑群 = Particle("剑群", root.transform, swordMaterial);
            var main = config.剑群.main; main.maxParticles = 385; main.startLifetime = 12; main.startSpeed = 0;
            var emission = config.剑群.emission; emission.enabled = false;
            var shape = config.剑群.shape; shape.enabled = false;
            var renderer = config.剑群.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh; renderer.alignment = ParticleSystemRenderSpace.World; renderer.enableGPUInstancing = true;
            config.星屑 = Particle("条带与锥面灵尘", root.transform, sparkMaterial);
            main = config.星屑.main; main.startLifetime = 12; main.startSpeed = 0; main.maxParticles = 1024;
            emission = config.星屑.emission; emission.enabled = false;
            shape = config.星屑.shape; shape.enabled = false;
            cast = PrefabUtility.SaveAsPrefabAsset(root, Output + "/SpiralSwordRain.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        var hit = new GameObject("骤雨剑锋命中"); GameObject impact;
        try
        {
            // Knife hit 的命中火花、爆闪、烟雾及冲击波；剑本体由执行体淡出，不重复插地模型。
            foreach (var ps in sourceHit.GetComponentsInChildren<ParticleSystem>().Where(p => p.name != "SwordHit"))
            {
                var clone = UnityEngine.Object.Instantiate(ps.gameObject, hit.transform, false); var effect = clone.GetComponent<ParticleSystem>(); effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = effect.main; main.loop = false; main.playOnAwake = true; main.duration = .5f; main.startDelay = 0;
                main.startLifetimeMultiplier = Mathf.Min(main.startLifetimeMultiplier, .4f); main.startColor = new Color(.6f, .8f, 1, .9f);
                var emission = effect.emission;
                var bursts = new ParticleSystem.Burst[emission.burstCount]; emission.GetBursts(bursts);
                for (int i = 0; i < bursts.Length; i++) { bursts[i].count = new ParticleSystem.MinMaxCurve(Mathf.Min(12, bursts[i].count.constantMax)); bursts[i].cycleCount = 1; }
                emission.SetBursts(bursts);
                if (ps.name == "SparksExpl")
                {
                    main.startSpeed = new ParticleSystem.MinMaxCurve(3, 7);
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, 8) });
                    clone.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMaterial;
                }
            }
            impact = PrefabUtility.SaveAsPrefabAsset(hit, Output + "/SwordContact.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(hit); }
        DataTableImporter.ImportTables(false, new[] { "主动神通表", "物品表" });
        var vfx = Resources.Load<CombatVfxCatalog>("Combat/CombatVfxCatalog");
        vfx.特效 = vfx.特效.Where(e => e.id != Id + "_cast" && e.id != Id + "_impact").Concat(new[] {
            new CombatVfxCatalog.Entry { id = Id + "_cast", 预制体 = cast, 最长存活 = 14, 关闭场景灯光 = true },
            new CombatVfxCatalog.Entry { id = Id + "_impact", 预制体 = impact, 最长存活 = .65f, 缩放 = .45f, 关闭场景灯光 = true } }).ToArray(); EditorUtility.SetDirty(vfx);
        var contacts = Resources.Load<CombatImpactCatalog>("Combat/CombatImpactCatalog");
        contacts.规则 = contacts.规则.Where(e => e.id != Id).Concat(new[] { new CombatImpactCatalog.Entry { id = Id,
            地表特效id = Id + "_impact", 环境形状 = CombatEnvironmentShape.None, 顿帧方式 = CombatHitStopMode.仅受击方 } }).ToArray(); EditorUtility.SetDirty(contacts);
        var icon = Resources.Load<Sprite>("UI/InkUI/AbilityArt/icon-ability-zhouyu_chuxie")??Resources.Load<Sprite>("UI/InkUI/Icons/icon-divine-ability");
        var skill = AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>("Assets/Data/Generated/ActiveDivineAbility/" + Id + ".asset"); skill.图标 = icon; EditorUtility.SetDirty(skill);
        var book = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_" + Id + ".asset"); book.图标 = icon; EditorUtility.SetDirty(book);
        QuestDatabaseBuilder.收集(false); 面板库收集器.收集(); AssetDatabase.SaveAssets();
        Debug.Log("[骤雨初歇] 288柄沿统一弧长均匀排布、范围直刺与共享战斗目录已生成；不保存场景");
    }
    static ParticleSystem Particle(string name, Transform parent, Material material)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = 12;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Shape;
        var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        return ps;
    }
}
