using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>生成游戏版神通特效与法环；只写资产，不回填、保存或重建场景。</summary>
public static class NewAbilityAssets
{
    const string 输出 = "Assets/resources/Abilities";
    const string 来源 = "Assets/resources/特效/法术/AllEffects/";
    static readonly Dictionary<string, GameObject> 已处理 = new Dictionary<string, GameObject>();

    [MenuItem("修仙/神通/生成水炮锁链剑阵与护罩资产")]
    public static void Build()
    {
        Directory.CreateDirectory(输出 + "/Parts");
        Directory.CreateDirectory(输出 + "/Materials");
        AssetDatabase.Refresh();
        已处理.Clear();
        创建("WaterDragon", "EffectsSet_1(NotScriptBased)/Effects/Effect_28_PurifierBeam/Effect_28_PurifierBeam.prefab");
        生成邪眼射击();
        创建("BindingChain", "EffectsSet_1(NotScriptBased)/Effects/Effect_48_CriticalTumor/Effect_48_CriticalTumor.prefab", x =>
        {
            // 主特效的球形分布锁链先细后展开；BondageChain 是另一套地面束缚，不能替代它。
            for (int i = x.transform.childCount - 1; i >= 0; i--)
                if (x.transform.GetChild(i).name != "Effect_48_Chain") UnityEngine.Object.DestroyImmediate(x.transform.GetChild(i).gameObject);
            var 链 = x.GetComponentInChildren<ParticleSystem>();
            var 主 = 链.main; float 原尺寸 = 主.startSize.constantMax;
            // 小体型上链环容易退化成细线，只加宽 LongSlide 的X轴，保留长度与展开曲线。
            主.startSize3D = true; 主.startSizeX = 原尺寸 * 2.4f; 主.startSizeY = 原尺寸; 主.startSizeZ = 原尺寸;
        });
        创建("DevilEye", "EffectsSet_1(NotScriptBased)/Effects/Effect_32_DevilEye/Effect_32_DevilEye.prefab", x =>
        {
            var 十字 = x.transform.Find("Effect_32_ObjectSphere");
            if (十字 != null) UnityEngine.Object.DestroyImmediate(十字.gameObject);
            // 分层实测：Sphere_2 / Sphere_3 是交叉的白色竖横闪光，不能当实体瞳孔保留。
            foreach (var t in x.GetComponentsInChildren<Transform>(true))
                if (t.name == "Effect_32_Sphere_2" || t.name == "Effect_32_Sphere_3") UnityEngine.Object.DestroyImmediate(t.gameObject);
            foreach (var r in x.GetComponentsInChildren<ParticleSystemRenderer>(true))
                if (r.renderMode == ParticleSystemRenderMode.Mesh) r.alignment = ParticleSystemRenderSpace.Local;
        });
        创建("SmallSwordArray", "EffectsSet_2(ScriptBased)/Effects/Effect_34_SwordDance/Effect_34_SwordDance.prefab");
        string[] 名 = { "PhysicalShield", "SpecialShield", "DualShield", "AbsoluteShield" };
        string[] 源名 = { "Effect_09_HoloShield(IncludeHit)", "Effect_09_HolyShield(IncludeHit)", "Effect_09_InfernoShield(IncludeHit)", "Effect_09_GloryShield" };
        for (int i = 0; i < 名.Length; i++)
            创建(名[i], "EffectsSet_2(ScriptBased)/Effects/Effect_09_GloryShield/" + 源名[i] + ".prefab", x =>
            {
                for (int j = x.transform.childCount - 1; j >= 0; j--)
                    if (x.transform.GetChild(j).name.Contains("MultipleShot")) UnityEngine.Object.DestroyImmediate(x.transform.GetChild(j).gameObject);
            });
        生成法环();
        生成瞳孔();
        导入图标();
        安全导入配置表.ImportAssetsOnly("主动神通表", "被动神通表", "物品表");
        设置图标();
        AssetDatabase.SaveAssets();
        Debug.Log("[神通资产] 八种特效、法环、图标与学习物品已生成；没有修改场景");
    }

    [MenuItem("修仙/神通/仅更新邪眼射击特效")]
    public static void 生成邪眼射击()
    {
        Directory.CreateDirectory(输出 + "/Parts");
        Directory.CreateDirectory(输出 + "/Materials");
        已处理.Clear();
        创建("DevilEyeLaser", "EffectsSet_1(NotScriptBased)/Effects/Effect_47_PreciseShot/Effect_47_PreciseShot.prefab", x =>
        {
            foreach (var t in x.GetComponentsInChildren<Transform>(true))
                if (t.name == "Effect_47_BulletEffects") t.gameObject.SetActive(true);
            // 所有碰撞面由运行时放在目标处，不能引用演示预制体的远处平面。
            foreach (var p in x.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = p.main; m.loop = false;
                var c = p.collision; c.enabled = false;
                for (int i = 0; i < 6; i++) c.SetPlane(i, null);
            }
        });
        AssetDatabase.SaveAssets();
    }

    static GameObject 创建(string 名, string 相对路径, Action<GameObject> 筛选 = null)
    {
        var 原 = AssetDatabase.LoadAssetAtPath<GameObject>(来源 + 相对路径);
        if (原 == null) throw new FileNotFoundException("缺少指定特效", 来源 + 相对路径);
        return 净化(原, 输出 + "/" + 名 + ".prefab", 筛选);
    }
    static GameObject 净化(GameObject 原, string 路径, Action<GameObject> 筛选 = null)
    {
        string 键 = 路径;
        if (已处理.TryGetValue(键, out var 有)) return 有;
        var 临时 = UnityEngine.Object.Instantiate(原);
        try
        {
            临时.name = Path.GetFileNameWithoutExtension(路径);
            临时.transform.localPosition = Vector3.zero;
            临时.transform.localRotation = Quaternion.identity;
            临时.transform.localScale = Vector3.one;
            筛选?.Invoke(临时);
            foreach (var 发射 in 临时.GetComponentsInChildren<MultipleObjectsMake>(true))
            {
                var 替换 = 发射.gameObject.AddComponent<AbilityVfxEmitter>();
                替换.开始延迟 = 发射.m_startDelay; 替换.生成间隔 = 发射.m_makeDelay;
                替换.生成次数 = 发射.m_makeCount; 替换.随机位置 = 发射.m_randomPos;
                替换.随机角度 = 发射.m_randomRot; 替换.随机缩放 = 发射.m_randomScale;
                var 资源 = new List<GameObject>();
                foreach (var 子 in 发射.m_makeObjs)
                {
                    if (子 == null) continue;
                    string 子路径 = 输出 + "/Parts/" + 子.name + "_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(子)).Substring(0, 8) + ".prefab";
                    资源.Add(净化(子, 子路径));
                }
                替换.预制体 = 资源.ToArray();
            }
            foreach (var 换材质 in 临时.GetComponentsInChildren<NewMaterialChange>(true))
            {
                var 渲染 = 换材质.GetComponent<Renderer>();
                if (渲染 == null || 换材质.m_inputMaterial == null) continue;
                string 材质路径 = 输出 + "/Materials/" + 换材质.m_inputMaterial.name + "_"
                    + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(换材质.m_inputMaterial)).Substring(0, 8) + ".mat";
                var 材质 = AssetDatabase.LoadAssetAtPath<Material>(材质路径);
                if (材质 == null) { 材质 = new Material(换材质.m_inputMaterial); AssetDatabase.CreateAsset(材质, 材质路径); }
                if (材质.HasProperty("_MaskCutOut")) 材质.SetFloat("_MaskCutOut", 1f);
                渲染.sharedMaterial = 材质;
            }
            foreach (var 噪声 in 临时.GetComponentsInChildren<ScaleFactorApplyToMaterial>(true))
            {
                var 渲染 = 噪声.GetComponent<Renderer>();
                if (渲染 == null || 渲染.sharedMaterial == null || !渲染.sharedMaterial.HasProperty("_NoiseScale")) continue;
                var 原材质 = 渲染.sharedMaterial;
                string 材质路径 = 输出 + "/Materials/" + 原材质.name + "_PlayerScale.mat";
                var 材质 = AssetDatabase.LoadAssetAtPath<Material>(材质路径);
                if (材质 == null) { 材质 = new Material(原材质); AssetDatabase.CreateAsset(材质, 材质路径); }
                // 原演示脚本对小于 0.5 的缩放采用 0.25 噪声倍率，保留这条素材标定规则。
                材质.SetFloat("_NoiseScale", 原材质.GetFloat("_NoiseScale") * .25f);
                EditorUtility.SetDirty(材质); 渲染.sharedMaterial = 材质;
            }
            foreach (var 移动 in 临时.GetComponentsInChildren<ObjectMoveDestroy>(true))
            {
                var 落剑 = 移动.gameObject.AddComponent<AbilityFallingSword>();
                落剑.速度 = 移动.MoveSpeed;
                if (移动.m_hitObject != null)
                {
                    var 命中 = 移动.m_hitObject.gameObject;
                    string 命中路径 = 输出 + "/Parts/" + 命中.name + "_" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(命中)).Substring(0, 8) + ".prefab";
                    落剑.触地特效 = 净化(命中, 命中路径);
                }
            }
            foreach (var 脚本 in 临时.GetComponentsInChildren<MonoBehaviour>(true))
                if (脚本 != null && !(脚本 is AbilityVfxEmitter) && !(脚本 is AbilityFallingSword)) UnityEngine.Object.DestroyImmediate(脚本);
            foreach (var ps in 临时.GetComponentsInChildren<ParticleSystem>(true))
            { var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy; m.simulationSpace = ParticleSystemSimulationSpace.Local; }
            foreach (var 碰撞 in 临时.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(碰撞);
            已处理[键] = PrefabUtility.SaveAsPrefabAsset(临时, 路径);
            return 已处理[键];
        }
        finally { UnityEngine.Object.DestroyImmediate(临时); }
    }

    public static void 生成瞳孔()
    {
        var 根 = new GameObject("DevilEyePupil");
        try
        {
            根.AddComponent<DevilEyePupil>();
            var 琥珀 = 材质("DevilEyeAmber", new Color(.65f, .16f, .015f), new Color(.75f, .2f, .01f));
            var 瞳 = 材质("DevilEyeBlack", new Color(.008f, .005f, .012f), Color.black);
            var 光 = 材质("DevilEyeHighlight", new Color(1f, .85f, .5f), new Color(1f, .55f, .15f));
            球("Cornea", Vector3.zero, new Vector3(.32f, .38f, .22f), 琥珀);
            球("Pupil", Vector3.zero, new Vector3(.055f, .25f, .245f), 瞳);
            球("GlintBack", new Vector3(-.055f, .065f, -.11f), Vector3.one * .025f, 光);
            球("GlintFront", new Vector3(.055f, .065f, .11f), Vector3.one * .025f, 光);
            PrefabUtility.SaveAsPrefabAsset(根, 输出 + "/DevilEyePupil.prefab");
            void 球(string 名, Vector3 位, Vector3 大小, Material mat)
            {
                var 球体 = GameObject.CreatePrimitive(PrimitiveType.Sphere); 球体.name = 名;
                球体.transform.SetParent(根.transform, false); 球体.transform.localPosition = 位; 球体.transform.localScale = 大小;
                UnityEngine.Object.DestroyImmediate(球体.GetComponent<Collider>());
                var r = 球体.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(根); }
    }
    static Material 材质(string 名, Color 颜色, Color 发光)
    {
        string 路径 = 输出 + "/Materials/" + 名 + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(路径);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, 路径); }
        m.color = 颜色; m.SetFloat("_Glossiness", .75f); m.SetColor("_EmissionColor", 发光); m.EnableKeyword("_EMISSION");
        m.SetFloat("_Mode", 3f); m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 1); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHABLEND_ON"); m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        // 粒子为叠加混合，原先会盖亮竖瞳；实体眼球在粒子之后绘制，仍使用正常深度测试。
        m.renderQueue = 名 == "DevilEyeBlack" ? 3110 : 名 == "DevilEyeHighlight" ? 3120 : 3100;
        EditorUtility.SetDirty(m); return m;
    }

    public static void 生成法环()
    {
        var 资源 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/NPC/Human/YangJian/YangJian_02.prefab");
        var 人 = UnityEngine.Object.Instantiate(资源);
        GameObject 环 = null;
        try
        {
            人.transform.position = Vector3.zero; 人.transform.rotation = Quaternion.identity; 人.transform.localScale = Vector3.one;
            SkinnedMeshRenderer 网格 = null;
            foreach (var r in 人.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "YangJian_02_FaHuan") 网格 = r;
            if (网格 == null) throw new InvalidOperationException("没有找到 YangJian_02_FaHuan");
            var 烘焙 = new Mesh { name = "YangJian02Halo" };
            网格.BakeMesh(烘焙);
            var 顶点 = 烘焙.vertices;
            var 矩阵 = 人.transform.worldToLocalMatrix * 网格.transform.localToWorldMatrix;
            var 边界 = new Bounds(矩阵.MultiplyPoint3x4(顶点[0]), Vector3.zero);
            for (int i = 0; i < 顶点.Length; i++) { 顶点[i] = 矩阵.MultiplyPoint3x4(顶点[i]); 边界.Encapsulate(顶点[i]); }
            // 保持原杨戬法环的姿态，中心归零后便于把邪眼放进空缺。
            for (int i = 0; i < 顶点.Length; i++) 顶点[i] -= 边界.center;
            烘焙.vertices = 顶点; 烘焙.RecalculateBounds(); 烘焙.RecalculateNormals();
            // 原模型仅有正面；玩家背后会从反面观察。补实体背面，保持同一贴图和原始姿态。
            var 法线 = 烘焙.normals; var uv = 烘焙.uv; var 三角形 = 烘焙.triangles;
            var 双面顶点 = new Vector3[顶点.Length * 2]; var 双面法线 = new Vector3[法线.Length * 2];
            var 双面UV = new Vector2[uv.Length * 2]; var 双面三角形 = new int[三角形.Length * 2];
            for (int i = 0; i < 顶点.Length; i++)
            { 双面顶点[i] = 双面顶点[i + 顶点.Length] = 顶点[i]; 双面法线[i] = 法线[i]; 双面法线[i + 顶点.Length] = -法线[i]; }
            for (int i = 0; i < uv.Length; i++) 双面UV[i] = 双面UV[i + uv.Length] = uv[i];
            Array.Copy(三角形, 双面三角形, 三角形.Length);
            for (int i = 0; i < 三角形.Length; i += 3)
            {
                双面三角形[三角形.Length + i] = 三角形[i] + 顶点.Length;
                双面三角形[三角形.Length + i + 1] = 三角形[i + 2] + 顶点.Length;
                双面三角形[三角形.Length + i + 2] = 三角形[i + 1] + 顶点.Length;
            }
            烘焙.Clear(); 烘焙.vertices = 双面顶点; 烘焙.normals = 双面法线; 烘焙.uv = 双面UV;
            烘焙.triangles = 双面三角形; 烘焙.RecalculateBounds();
            string 路径 = 输出 + "/YangJian02Halo.asset";
            var 已有 = AssetDatabase.LoadAssetAtPath<Mesh>(路径);
            if (已有 == null) AssetDatabase.CreateAsset(烘焙, 路径);
            else
            {
                已有.name = "YangJian02Halo";
                已有.Clear(); 已有.vertices = 烘焙.vertices; 已有.normals = 烘焙.normals;
                已有.uv = 烘焙.uv; 已有.triangles = 烘焙.triangles; 已有.RecalculateBounds();
                EditorUtility.SetDirty(已有);
                UnityEngine.Object.DestroyImmediate(烘焙); 烘焙 = 已有;
            }
            环 = new GameObject("DevilEyeHalo");
            环.AddComponent<MeshFilter>().sharedMesh = 烘焙;
            环.AddComponent<MeshRenderer>().sharedMaterials = 网格.sharedMaterials;
            PrefabUtility.SaveAsPrefabAsset(环, 输出 + "/DevilEyeHalo.prefab");
            Debug.Log("[法环] 独立网格 " + 顶点.Length + " 顶点，原杨戬姿态中心=" + 边界.center + " 尺寸=" + 边界.size);
        }
        finally { if (环 != null) UnityEngine.Object.DestroyImmediate(环); UnityEngine.Object.DestroyImmediate(人); }
    }

    static readonly string[] 编号 = { "ability_shuilong_pao", "ability_jingu_suolian", "ability_xiao_jianzhen", "ability_xieyan",
        "ability_huzhao_wuli", "ability_huzhao_teshu", "ability_huzhao_shuangchong", "ability_huzhao_juedui" };
    static readonly string[] 图标名 = { "water-dragon", "binding-chain", "small-sword-array", "devil-eye", "physical-shield", "special-shield", "dual-shield", "absolute-shield" };
    static void 导入图标()
    {
        foreach (string 名 in 图标名)
        {
            string 路径 = "Assets/resources/UI/InkUI/Abilities/" + 名 + ".png";
            var 导入 = (TextureImporter)AssetImporter.GetAtPath(路径);
            导入.textureType = TextureImporterType.Sprite; 导入.spriteImportMode = SpriteImportMode.Single;
            导入.alphaIsTransparency = true; 导入.mipmapEnabled = false; 导入.maxTextureSize = 512;
            导入.textureCompression = TextureImporterCompression.Uncompressed; 导入.spritePixelsPerUnit = 100f;
            导入.SaveAndReimport();
        }
    }
    static void 设置图标()
    {
        for (int i = 0; i < 编号.Length; i++)
        {
            var 图标 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/resources/UI/InkUI/Abilities/" + 图标名[i] + ".png");
            string 类型 = i < 3 ? "ActiveDivineAbility" : "PassiveDivineAbility";
            var 技能 = AssetDatabase.LoadAssetAtPath<DivineAbilityDefinition>("Assets/Data/Generated/" + 类型 + "/" + 编号[i] + ".asset");
            if (技能 == null) throw new InvalidOperationException("缺少神通资产 " + 编号[i]);
            技能.图标 = 图标; EditorUtility.SetDirty(技能);
            var 物品 = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_" + 编号[i] + ".asset");
            if (物品 != null) { 物品.图标 = 图标; EditorUtility.SetDirty(物品); }
        }
    }

    sealed class PrefabEditScope : IDisposable
    {
        readonly string 路径;
        public GameObject Root;
        public PrefabEditScope(string p) { 路径 = p; Root = PrefabUtility.LoadPrefabContents(p); }
        public void Save() { PrefabUtility.SaveAsPrefabAsset(Root, 路径); }
        public void Dispose() { PrefabUtility.UnloadPrefabContents(Root); }
    }
}
