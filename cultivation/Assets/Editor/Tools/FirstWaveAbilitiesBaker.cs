using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>首批五门天降神通的派生资产；保留源特效/FBX和共享材质，不写场景。</summary>
public static class FirstWaveAbilitiesBaker
{
    public const string Output = "Assets/resources/Abilities/FallingSpells";
    const string Source = "Assets/Art/effectReference/Hovl Studio/AOE Magic spells Vol.1/Prefabs/";
    const string Art = "Assets/Art/Weapons/Sword2";
    const string Clips = "Assets/resources/技能动作/神通施法";
    public static readonly string[] Ids = { "ability_jianyu_luo", "ability_leiyun_strike", "ability_huoyan_yun", "ability_jianyu_luohua", "ability_huoyan_zhui" };
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    [MenuItem("修仙/神通/重建首批天降神通（不动场景）")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出运行模式");
        Folder(Output); Folder(Art); Folder(Clips);
        var sword = SwordMesh();
        var material = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/sword2/sword2.fbx").GetComponentInChildren<MeshRenderer>().sharedMaterial;
        var hit = Clone("Knife hit");
        try
        {
            Clean(hit); var ps = hit.GetComponent<ParticleSystem>(); var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = sword; renderer.sharedMaterial = material; renderer.alignment = ParticleSystemRenderSpace.World;
            var main = ps.main; main.startSize3D = false; main.startSize = 1; main.startSpeed = 0; main.startRotation3D = true;
            main.startRotationX = Mathf.PI / 2; main.startRotationY = main.startRotationZ = 0;
            var shape = ps.shape; shape.enabled = false;
            StableSword(ps); // 源贴片的自转/伸缩不能沿用到插地实体剑。
            foreach (var child in hit.GetComponentsInChildren<ParticleSystem>(true)) { var m = child.main; m.playOnAwake = true; }
            Save(hit, "SwordHit");
        }
        finally { UnityEngine.Object.DestroyImmediate(hit); }
        Pattern("Knives", "SwordRain", 18, .075f, .4f, .85f, 1, sword, material);
        Pattern("Lightning strike", "ThunderFall", 24, .06f, .38f, 1.2f, 1, null, null);
        Pattern("Meteor", "MeteorFall", 1, 0, .42f, 0, 1.25f, null, null);
        Pattern("Magic attack", "MagicSwordRain", 16, .1f, .4f, 1.1f, 1.15f, sword, material);
        Pattern("Meteor shower", "MeteorRain", 14, .14f, .45f, 2.2f, 1.5f, null, null);
        Clip("atk_energy02_inplace", "能量02_前半施法", .5f);
        Clip("atk_energy03_inplace", "能量03_施法", 1);
        Clip("atk_energy05_inplace", "能量05_施法", 1);
        DataTableImporter.ImportTables(false, new[] { "主动神通表", "物品表" });
        ImpactRules();
        VfxRules();
        // 神通与玉简共用各自的专属成品图标；重建特效不能覆盖为通用玉简。
        var fallbackIcon = Resources.Load<Sprite>("UI/InkUI/Icons/icon-divine-ability");
        foreach (string id in Ids)
        {
            var icon=Resources.Load<Sprite>("UI/InkUI/AbilityArt/icon-"+id.Replace("ability_","ability-"))??fallbackIcon;
            var skill = AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>("Assets/Data/Generated/ActiveDivineAbility/" + id + ".asset");
            skill.图标 = icon; EditorUtility.SetDirty(skill);
            var book = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_" + id + ".asset");
            book.图标 = icon; EditorUtility.SetDirty(book);
        }
        QuestDatabaseBuilder.收集(false);
        // 面板目录必须重新聚合，运行时获得/装备新神通无需保存任何正式场景。
        面板库收集器.收集();
        AssetDatabase.SaveAssets();
        Debug.Log("[天降神通] 五门定义、玉简、特效与三段施法动作已生成；原特效及场景未修改");
    }
    static GameObject Clone(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Source + name + ".prefab");
        if (!prefab) throw new InvalidOperationException("缺少源特效 " + name);
        var root = UnityEngine.Object.Instantiate(prefab); root.name = name; return root;
    }
    static GameObject Save(GameObject root, string name)
    {
        return PrefabUtility.SaveAsPrefabAsset(root, Output + "/" + name + ".prefab");
    }
    static GameObject ImpactPrefab(GameObject source, string name)
    {
        if (!source) return null;
        var clone = UnityEngine.Object.Instantiate(source);
        try { Clean(clone); foreach (var ps in clone.GetComponentsInChildren<ParticleSystem>(true)) { var m = ps.main; m.playOnAwake = true; } return Save(clone, name); }
        finally { UnityEngine.Object.DestroyImmediate(clone); }
    }
    static void Clean(GameObject root)
    {
        foreach (var script in root.GetComponentsInChildren<ParticleCollisionInstance>(true)) UnityEngine.Object.DestroyImmediate(script);
        foreach (var sound in root.GetComponentsInChildren<EGA_EffectSound>(true)) sound.Repeating = false;
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var collision = ps.collision; collision.sendCollisionMessages = false; // 保留火花/烟雾的原生碰撞表现。
        }
        foreach (var light in root.GetComponentsInChildren<Light>(true)) { light.intensity *= .25f; light.range = Mathf.Min(2.5f, light.range); }
    }
    static void StableSword(ParticleSystem ps)
    {
        // 只固定剑的几何；子节点的火花、烟雾、冲击波仍按原曲线播放。
        var main = ps.main; main.gravityModifier = 0;
        var velocity = ps.velocityOverLifetime; velocity.enabled = false;
        var force = ps.forceOverLifetime; force.enabled = false;
        var noise = ps.noise; noise.enabled = false;
        var inherit = ps.inheritVelocity; inherit.enabled = false;
        var rotation = ps.rotationOverLifetime; rotation.enabled = false;
        var rotationSpeed = ps.rotationBySpeed; rotationSpeed.enabled = false;
        var size = ps.sizeOverLifetime; size.enabled = false;
        var sizeSpeed = ps.sizeBySpeed; sizeSpeed.enabled = false;
    }
    static void Pattern(string source, string name, int count, float interval, float release, float radius, float impactScale, Mesh sword, Material swordMaterial)
    {
        var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source + source + ".prefab");
        var collision = original.GetComponentsInChildren<ParticleCollisionInstance>(true).FirstOrDefault();
        var originalHit = collision && collision.EffectsOnCollision.Length > 0 ? collision.EffectsOnCollision[0] : null;
        var root = new GameObject(name); var visual = Clone(source); visual.transform.SetParent(root.transform, false);
        try
        {
            Clean(visual);
            if (source == "Magic attack") Silence(visual);
            var config = root.AddComponent<FallingSpellPattern>(); config.数量 = count; config.间隔 = interval; config.出手进度 = release;
            config.命中半径 = radius; config.命中特效缩放 = impactScale;
            config.飞行粒子 = visual.GetComponent<ParticleSystem>();
            if (source == "Knives") { visual.transform.localPosition = Vector3.zero; config.命中特效 = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/SwordHit.prefab"); }
            else if (source == "Magic attack")
            {
                config.召唤阵 = config.飞行粒子;
                config.飞行粒子 = visual.GetComponentsInChildren<ParticleSystem>(true).First(p => p.name == "Projectiles");
                var sub = config.召唤阵.subEmitters;
                for (int i = sub.subEmittersCount - 1; i >= 0; i--) if (sub.GetSubEmitterSystem(i) == config.飞行粒子) sub.RemoveSubEmitter(i);
                config.命中特效 = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/SwordHit.prefab"); config.额外命中特效 = ImpactPrefab(originalHit, "MagicHit");
                var silent = UnityEngine.Object.Instantiate(config.额外命中特效);
                try { Silence(silent); config.额外命中特效 = Save(silent, "MagicHit"); }
                finally { UnityEngine.Object.DestroyImmediate(silent); }
                config.飞行速度 = 42; config.起点偏移 = Vector3.up * 8;
            }
            else if (source == "Lightning strike")
            {
                foreach (var t in visual.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Start")).ToArray()) UnityEngine.Object.DestroyImmediate(t.gameObject);
                config.飞行速度 = original.GetComponent<ParticleSystem>().main.startSpeed.constantMax;
                config.接触后留痕时间 = .25f;
                config.起点偏移 = Vector3.up * 7; config.命中特效 = ImpactPrefab(originalHit, "ThunderHit");
                // 保留瞬闪本体，只移除蓄力。源素材的第3秒雷击现在直接在释放节点进入。
                foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main; main.startDelay = 0;
                    var emission = ps.emission;
                    var bursts = new ParticleSystem.Burst[emission.burstCount]; emission.GetBursts(bursts);
                    for (int i = 0; i < bursts.Length; i++) { bursts[i].time = Mathf.Max(0, bursts[i].time - 2); bursts[i].cycleCount = 1; }
                    emission.SetBursts(bursts);
                    bool linked = false;
                    var links = config.飞行粒子.subEmitters;
                    for (int j = 0; j < links.subEmittersCount; j++) if (links.GetSubEmitterSystem(j) == ps) linked = true;
                    if (ps != config.飞行粒子 && ps.transform.parent == visual.transform && !linked)
                    { main.playOnAwake = true; emission.rateOverTime = 0; }
                    if (ps.name == "Lightning")
                    {
                        // 天空闪光与每道实际落雷一起发射，不能全部集中在技能中心。
                        config.起点闪光 = ps; main.simulationSpace = ParticleSystemSimulationSpace.World;
                        main.playOnAwake = false; emission.enabled = false;
                    }
                }
            }
            else if (source == "Meteor")
            {
                config.起点偏移 = new Vector3(0, 7, -4); config.飞行速度 = 24; config.弹体半径 = .35f;
                config.命中特效 = ImpactPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source + "Meteor hit.prefab"), "MeteorHit");
                // 飞行体只保留原生出生子发射器；地表爆炸改为真实接触时生成。
                var keep = new HashSet<Transform> { visual.transform };
                var sub = config.飞行粒子.subEmitters;
                for (int i = 0; i < sub.subEmittersCount; i++) keep.Add(sub.GetSubEmitterSystem(i).transform);
                foreach (var t in visual.transform.Cast<Transform>().ToArray()) if (!keep.Contains(t)) UnityEngine.Object.DestroyImmediate(t.gameObject);
            }
            else
            {
                config.命中特效 = ImpactPrefab(originalHit, "MeteorRainHit"); config.弹体半径 = .22f; config.飞行速度 = 30;
                // 原 Meteor shower 只有线迹；复用同包陨石弹体，保留原有火焰、烟与拖尾。
                var renderer = config.飞行粒子.GetComponent<ParticleSystemRenderer>();
                var trailMaterial = renderer.trailMaterial;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>(Source + "Meteor.prefab").GetComponent<ParticleSystemRenderer>().sharedMaterial;
                renderer.trailMaterial = trailMaterial;
                var rainMain = config.飞行粒子.main; rainMain.startSize = .85f;
            }
            var projectile = config.飞行粒子;
            var pc = projectile.collision; pc.enabled = false; // 实体命中由全段扫掠处理。
            var projectileSub = projectile.subEmitters;
            for (int i = 0; i < projectileSub.subEmittersCount; i++)
                if (projectileSub.GetSubEmitterType(i) == ParticleSystemSubEmitterType.Collision) projectileSub.SetSubEmitterType(i, ParticleSystemSubEmitterType.Manual);
            var pm = projectile.main; pm.simulationSpace = ParticleSystemSimulationSpace.World; pm.startSpeed = 0;
            pm.startLifetime = 2; pm.maxParticles = 64; pm.startDelay = 0; pm.gravityModifier = 0;
            var pe = projectile.emission; pe.enabled = false;
            var pv = projectile.velocityOverLifetime; pv.enabled = false;
            var force = projectile.forceOverLifetime; force.enabled = false;
            var noise = projectile.noise; noise.enabled = false;
            if (source == "Lightning strike")
            {
                // 保留原生细微折线及速度；接触后轨迹短暂淡出，避免杀粒子时整条雷光瞬间消失。
                noise.enabled = true;
                // Trail lifetime 是粒子总寿命的比例[0,1]；按源模拟倍率换算为真实秒数。
                var trails = projectile.trails; trails.dieWithParticles = false;
                trails.lifetime = config.接触后留痕时间 * pm.simulationSpeed / pm.startLifetime.constantMax;
                var branches = visual.GetComponentsInChildren<ParticleSystem>(true).First(p => p.name == "SubLight");
                var branchTrails = branches.trails; branchTrails.dieWithParticles = false;
            }
            if (config.召唤阵)
            {
                var gm = config.召唤阵.main; gm.simulationSpace = ParticleSystemSimulationSpace.World; gm.startDelay = 0;
                var ge = config.召唤阵.emission; ge.enabled = false;
            }
            if (sword)
            {
                var renderer = projectile.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = sword; renderer.sharedMaterial = swordMaterial; renderer.alignment = ParticleSystemRenderSpace.World;
                pm.startSize3D = false; pm.startSize = source == "Magic attack" ? 1.15f : 1; pm.startRotation3D = true;
                StableSword(projectile);
            }
            Save(root, name);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static Mesh SwordMesh()
    {
        var obj = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TripoModels/sword2/sword2.fbx"));
        try
        {
            var filter = obj.GetComponentInChildren<MeshFilter>(); var source = filter.sharedMesh;
            var vertices = source.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray();
            var mean = vertices.Aggregate(Vector3.zero, (sum, p) => sum + p) / vertices.Length;
            var axis = Vector3.forward;
            for (int step = 0; step < 32; step++)
            {
                var next = Vector3.zero;
                foreach (var p in vertices) { var d = p - mean; next += d * Vector3.Dot(d, axis); }
                axis = next.normalized;
            }
            if (axis.z < 0) axis = -axis; // 已实物预览：源模型+Z端是剑尖，-Z端为剑柄。
            var tip = vertices.OrderByDescending(p => Vector3.Dot(p, axis)).First();
            float length = vertices.Max(p => Vector3.Dot(p, axis)) - vertices.Min(p => Vector3.Dot(p, axis));
            var rotate = Quaternion.FromToRotation(axis, Vector3.forward);
            var mesh = UnityEngine.Object.Instantiate(source); mesh.name = "Sword2_TipForward";
            mesh.vertices = vertices.Select(p => rotate * (p - tip) * (2.2f / length)).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string path = Art + "/Sword2_TipForward.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old) { EditorUtility.CopySerialized(mesh, old); UnityEngine.Object.DestroyImmediate(mesh); return old; }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        finally { UnityEngine.Object.DestroyImmediate(obj); }
    }
    static void Clip(string name, string output, float fraction)
    {
        string path = "Assets/resources/Animation Library/Magical-Knight_Set/Animation/Humanoid/inplace/" + name + ".fbx";
        var source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var clip = UnityEngine.Object.Instantiate(source); clip.name = output;
        float end = source.length * fraction;
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            var trimmed = new AnimationCurve(curve.keys.Where(k => k.time < end).ToArray()); trimmed.AddKey(end, curve.Evaluate(end));
            if (binding.type == typeof(Animator) && (binding.propertyName == "RootT.x" || binding.propertyName == "RootT.z")) trimmed = AnimationCurve.Constant(0, end, 0);
            AnimationUtility.SetEditorCurve(clip, binding, trimmed);
        }
        HandMotionRetargeting.Apply(clip, HandMotionRetargeting.AvatarFor(source));
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = settings.loopBlend = false;
        settings.startTime = 0; settings.stopTime = end; settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings); AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>()); clip.EnsureQuaternionContinuity();
        string outputPath = Clips + "/" + output + ".anim"; var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
        if (old) { EditorUtility.CopySerialized(clip, old); EditorUtility.SetDirty(old); UnityEngine.Object.DestroyImmediate(clip); }
        else AssetDatabase.CreateAsset(clip, outputPath);
    }
    static void ImpactRules()
    {
        var catalog = Resources.Load<CombatImpactCatalog>("Combat/CombatImpactCatalog");
        var entries = catalog.规则.Where(e => !Ids.Contains(e.id)).ToList();
        float[] radii = { .45f, .3f, .45f, .5f, .35f }, depths = { .12f, .06f, .25f, .15f, .12f };
        for (int i = 0; i < Ids.Length; i++) entries.Add(new CombatImpactCatalog.Entry { id = Ids[i], 环境形状 = CombatEnvironmentShape.Ellipsoid,
            地表特效id = Ids[i] + "_impact", 半径倍率 = radii[i], 固定深度 = true, 深度 = depths[i], 顿帧方式 = CombatHitStopMode.仅受击方, 落点引导半径倍率 = 1 });
        catalog.规则 = entries.ToArray(); EditorUtility.SetDirty(catalog);
    }
    static void VfxRules()
    {
        var catalog = Resources.Load<CombatVfxCatalog>("Combat/CombatVfxCatalog");
        var entries = catalog.特效.Where(e => !Ids.Any(id => e.id == id + "_cast" || e.id == id + "_impact")).ToList();
        string[] names = { "SwordRain", "ThunderFall", "MeteorFall", "MagicSwordRain", "MeteorRain" };
        for (int i = 0; i < Ids.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output + "/" + names[i] + ".prefab");
            var pattern = prefab.GetComponent<FallingSpellPattern>();
            var impact = pattern.命中特效;
            if (pattern.额外命中特效)
            {
                var combined = new GameObject("MagicSwordHit");
                try
                {
                    UnityEngine.Object.Instantiate(impact, combined.transform, false);
                    UnityEngine.Object.Instantiate(pattern.额外命中特效, combined.transform, false);
                    Silence(combined);
                    impact = Save(combined, "MagicSwordHit");
                }
                finally { UnityEngine.Object.DestroyImmediate(combined); }
            }
            entries.Add(new CombatVfxCatalog.Entry { id = Ids[i] + "_cast", 预制体 = prefab, 最长存活 = 12, 灯光最大半径 = 2.5f });
            entries.Add(new CombatVfxCatalog.Entry { id = Ids[i] + "_impact", 预制体 = impact, 缩放 = pattern.命中特效缩放,
                最长存活 = Mathf.Clamp(特效摆放.量特效总时长(impact, 2) + .25f, .5f, 5), 灯光最大半径 = 2.5f });
        }
        catalog.特效 = entries.ToArray(); EditorUtility.SetDirty(catalog);
    }
    static void Silence(GameObject root)
    {
        foreach (var sound in root.GetComponentsInChildren<EGA_EffectSound>(true)) UnityEngine.Object.DestroyImmediate(sound);
        foreach (var audio in root.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
    }
}
