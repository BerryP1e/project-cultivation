using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **一块田地的外观** —— 土床 + 作物模型。
///
/// ## 素材来自哪里
///
/// 作物模型和土质贴图取自 GitHub 上的开源项目 `henjicc/yipiantian`
/// （一个 Godot 的江南水乡种田游戏），由 `tools/参考素材/` 那套脚本
/// 从 `.glb` 转成 Unity 能导入的 `.obj` + 贴图。来源逐条登记在
/// `docs/reference/外部素材来源.md`。
///
/// ## 三个长相（用户 2026-10-01 明确要求"模型要跟着状态变"）
///
/// | 状态 | 长相 |
/// |---|---|
/// | **待种植** | 起好垄的空畦，土色**偏干偏浅** —— 一眼看出"这块等着种" |
/// | **已种植** | 土色变深（浇过水的样子）+ 3 株作物模型，随生长阶段换 sprout / young / mature |
/// | **成熟** | 作物换成成熟模型，土床不变 |
///
/// 【为什么待种植和已种植的土色要不一样】用户报过一个 bug：
/// 「我开拓完后田的模型没有改变」。根因是上一版**建外观只在 `Start()` 里跑一次**
/// （按当时的"荒地/熟地"决定网格与材质），之后每帧的刷新**只管作物**，
/// 于是状态变了长相不变。这一版把"土床材质"也纳入 <see cref="刷新"/> 的判据，
/// 而且**刻意让两种状态长得不一样**，模型不动这种问题就能一眼看出来。
///
/// ## 性能上的两条硬规矩
///
/// · **只在"阶段变化"时重建作物模型**，不是每帧 —— 每帧 `Instantiate` 会卡死。
/// · 土床网格、材质**全局共享**（静态缓存），地块再多也不重复造。
/// </summary>
public static class 灵田地块外观
{
    /// <summary>一株作物摆几个模型。空着一根垄不好看，也显得收成少</summary>
    const int 每块几株 = 3;

    /// <summary>
    /// 模型原尺寸偏小（成熟才 ~0.3 米），要放大才看得清。
    /// 【为什么要放这么大】用户实测反馈「没有实现种植时和未种植时的模型区分」——
    /// 幼苗按 1.75 倍只有 0.14 米高，摆在 1.9 米的畦上就是几个小点，
    /// 一眼看不出来"种没种"。放大到 2.4 倍之后幼苗也有一掌高，种下去立刻能看见。
    /// </summary>
    const float 株缩放 = 2.4f;

    /// <summary>一块地的所有视觉部件（由 <see cref="建造"/> 建好，之后交给 <see cref="刷新"/>）</summary>
    public class 视觉
    {
        public GameObject 根;
        public MeshRenderer 土床;
        public Transform 植根;
        /// <summary>上一次的作物阶段，-1 = 还没刷过</summary>
        public int 上次阶段 = -1;
        /// <summary>上一次的"待种植"取值，用来判土床要不要换材质</summary>
        public int 上次待种 = -1;
    }

    // ============================================================ 建造

    /// <summary>给一块地造出完整外观</summary>
    public static 视觉 建造(灵田地块 块, Transform 父)
    {
        var 视 = new 视觉();
        视.根 = new GameObject("外观");
        视.根.transform.SetParent(父, false);

        var 床 = new GameObject("土床");
        床.transform.SetParent(视.根.transform, false);

        // ★ **土床故意不加碰撞体**（最后一个参数 false）。
        //   给田块加 MeshCollider 之后玩家会被它挡住：地块之间只有不到 1 米过道，
        //   而玩家胶囊直径 0.6 米、`stepOffset` 只 0.30 米 —— 实测玩家**卡在田里走不动**，
        //   也就走不到里面的地块按 F。土床只有 0.18 米高，不加碰撞体时走在上面只是
        //   **脚陷进去一点点**，第三人称俯视角基本看不出来；换来的是"整片田随便走"。
        挂网格体(床, 床网格(), 土材质(false), false);
        视.土床 = 床.GetComponent<MeshRenderer>();

        视.植根 = new GameObject("作物").transform;
        视.植根.SetParent(视.根.transform, false);

        return 视;
    }

    // ============================================================ 刷新

    /// <summary>按当前状态刷新外观。**作物与土床都刷**，状态一变长相就变</summary>
    public static void 刷新(视觉 视, 灵田地块状态 b)
    {
        if (视 == null || 视.根 == null || b == null) return;

        // ---- 土床：待种植 / 已种植 连**网格带材质**一起换 ----
        //
        // 【为什么必须连网格一起换】用户实测反馈「没有实现种植时和未种植时的模型区分」：
        //   原来两种状态只差一点点颜色（1.18 的暖色染色 vs 纯白），远看根本分不出来，
        //   而且那个染色实际上把贴图烧成了**亮橙色**，不像土。
        //   现在改成**两个网格**：待种植是**翻好的深沟**（垄高 0.14，一眼是"刚整好的地"），
        //   种下去之后换成正常的浅垄（0.08）+ 深色湿土 + 三株作物 ⇒ 轮廓和颜色同时变。
        int 待种 = b.待种植 ? 1 : 0;
        if (待种 != 视.上次待种)
        {
            视.上次待种 = 待种;
            if (视.土床 != null)
            {
                视.土床.sharedMaterial = 土材质(b.待种植);
                var mf = 视.土床.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = 床网格(b.待种植);
            }
        }

        // ---- 作物：只在阶段变化时重建 ----
        int 阶段 = b.阶段;
        if (阶段 != 视.上次阶段)
        {
            视.上次阶段 = 阶段;
            清空(视.植根);
            if (阶段 >= 1) 摆作物(b, 视.植根);
        }
    }

    static void 清空(Transform 根)
    {
        for (int i = 根.childCount - 1; i >= 0; i--) Object.Destroy(根.GetChild(i).gameObject);
    }

    // ============================================================ 作物摆放

    /// <summary>垄顶的三个位置（本地坐标），作物就种在这三处</summary>
    static readonly Vector3[] 垄位 =
    {
        new Vector3(-灵田规格.床边长 / (灵田规格.垄数 + 1), 灵田规格.垄顶, 0f),
        new Vector3(0f, 灵田规格.垄顶, 0f),
        new Vector3(灵田规格.床边长 / (灵田规格.垄数 + 1), 灵田规格.垄顶, 0f),
    };

    static void 摆作物(灵田地块状态 b, Transform 植根)
    {
        string 阶段名 = b.阶段 == 1 ? "sprout" : (b.阶段 == 2 ? "young" : "mature");
        var 预 = 载模型(b.作物id, 阶段名);
        if (预 == null) return;

        // 用"作物id + 阶段"当随机种子：外观要**稳定**（每次进场景长得一样），
        // 不能用 Random —— 那样每次切场景草的位置都会跳。
        var 随 = new System.Random(b.作物id.GetHashCode() * 31 + b.阶段);

        for (int i = 0; i < 每块几株 && i < 垄位.Length; i++)
        {
            var go = Object.Instantiate(预, 植根);
            go.name = "株" + (i + 1);
            float 抖 = (float)(随.NextDouble() - 0.5) * 0.16f;
            go.transform.localPosition = 垄位[i] + new Vector3(抖, 0f, 抖);
            go.transform.localRotation = Quaternion.Euler(0f, (float)随.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * 株缩放 * (0.9f + (float)随.NextDouble() * 0.2f);

            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
        }
    }

    static GameObject 载模型(string 灵植id, string 阶段名)
    {
        if (string.IsNullOrEmpty(灵植id)) return null;
        string 路径 = "灵田/" + 灵植id + "/" + 灵植id + "_" + 阶段名;

        var 预 = Resources.Load<GameObject>(路径);
        if (预 != null && 预.GetComponentInChildren<MeshFilter>() != null) return 预;

        // 【为什么还要 LoadAll 兜一道】项目里踩过：同目录有**同名 FBX** 时
        // `Resources.Load<GameObject>` 会挑到那个 FBX（见 NpcPrefabs 的注释）。
        foreach (var g in Resources.LoadAll<GameObject>("灵田/" + 灵植id))
            if (g != null && g.name == 灵植id + "_" + 阶段名 && g.GetComponentInChildren<MeshFilter>() != null)
                return g;

        Debug.LogWarning("[灵田] 找不到作物模型「" + 路径 + "」（.obj 是否在 Assets/resources/灵田/ 下？）");
        return null;
    }

    // ============================================================ 材质

    static Material 土材质缓存_待种, 土材质缓存_已种, 幽灵材质缓存_可, 幽灵材质缓存_不可;

    /// <summary>
    /// 土床材质。**待种植 = 干土（略亮的土黄）**，已种植 = 深色湿土。
    /// ⚠️ 染色是**中性**的（R≈G≈B）：上一版用了 `(1.18, 1.10, 0.92)` 这种偏暖的染色，
    /// 乘到土质贴图上直接把畦烧成了**亮橙色**，不像土 —— 别再用偏色染色，
    /// 要区分就靠"亮度 + 网格"这两样（见 <see cref="刷新"/>）。
    /// </summary>
    static Material 土材质(bool 待种植)
    {
        if (待种植) return 土材质缓存_待种 != null ? 土材质缓存_待种
            : (土材质缓存_待种 = 造土材质(new Color(1.14f, 1.12f, 1.06f), "灵田待种植土"));
        return 土材质缓存_已种 != null ? 土材质缓存_已种
            : (土材质缓存_已种 = 造土材质(new Color(0.86f, 0.84f, 0.80f), "灵田土床"));
    }

    static Material 造土材质(Color 染色, string 名)
    {
        var m = new Material(Shader.Find("Standard"));
        m.name = 名;
        var 图 = Resources.Load<Texture2D>("灵田/_土质/loam_512");
        if (图 != null)
        {
            m.mainTexture = 图;
            m.mainTextureScale = new Vector2(2.5f, 2.5f);
        }
        else Debug.LogWarning("[灵田] 找不到土质贴图 Assets/resources/灵田/_土质/loam_512.jpg —— 土床会用纯色");
        m.color = 染色;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0.05f);
        return m;
    }

    // ============================================================ 摆放时的半透明预览
    //
    // 用户要求：「鼠标会变成一块待摆放的**半透明**的灵田」。
    // 用同一个土床网格 + 一个半透明材质（Standard 的 Fade 模式），
    // 绿=能放、红=不能放，一眼就知道。

    /// <summary>造一块半透明的"幽灵灵田"（摆放预览用）。返回的物件由调用方负责销毁</summary>
    public static GameObject 造幽灵(Transform 父)
    {
        var go = new GameObject("灵田预览");
        go.transform.SetParent(父, false);
        挂网格体(go, 床网格(), 幽灵材质(true), false);

        // 再叠一层线框式的"边框"，让边界比纯半透明更清楚：4 根细长条
        var 边 = new GameObject("边界");
        边.transform.SetParent(go.transform, false);
        float h = 灵田规格.床边长 * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            var 条 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(条.GetComponent<Collider>());
            条.name = "边" + i;
            条.transform.SetParent(边.transform, false);
            bool 沿X = i < 2;
            条.transform.localScale = 沿X ? new Vector3(灵田规格.床边长, 0.03f, 0.05f)
                                          : new Vector3(0.05f, 0.03f, 灵田规格.床边长);
            float 符号 = (i % 2 == 0) ? 1f : -1f;
            条.transform.localPosition = 沿X ? new Vector3(0f, 0.01f, 符号 * h)
                                            : new Vector3(符号 * h, 0.01f, 0f);
            条.GetComponent<MeshRenderer>().sharedMaterial = 幽灵材质(true);
        }
        return go;
    }

    /// <summary>给预览上色：能放=绿，不能放=红</summary>
    public static void 幽灵上色(GameObject 幽灵, bool 可以)
    {
        if (幽灵 == null) return;
        var m = 幽灵材质(可以);
        foreach (var r in 幽灵.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = m;
    }

    static Material 幽灵材质(bool 可以)
    {
        if (可以 && 幽灵材质缓存_可 != null) return 幽灵材质缓存_可;
        if (!可以 && 幽灵材质缓存_不可 != null) return 幽灵材质缓存_不可;

        var m = new Material(Shader.Find("Standard"));
        m.name = 可以 ? "灵田预览_可" : "灵田预览_不可";
        var 图 = Resources.Load<Texture2D>("灵田/_土质/loam_512");
        if (图 != null) { m.mainTexture = 图; m.mainTextureScale = new Vector2(2.5f, 2.5f); }
        m.color = 可以 ? new Color(0.45f, 0.95f, 0.45f, 0.45f) : new Color(1f, 0.35f, 0.30f, 0.45f);

        // Standard 的透明：必须切到 Fade 渲染模式，否则 alpha 不生效（还是一块不透明的板）
        m.SetFloat("_Mode", 2f);
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0.1f);

        if (可以) 幽灵材质缓存_可 = m; else 幽灵材质缓存_不可 = m;
        return m;
    }

    // ============================================================ 网格

    /// <summary>
    /// 土床网格。**待种植和已种植是两个不同的网格**（垄深不一样），见 <see cref="刷新"/> 的说明。
    /// </summary>
    public static Mesh 床网格(bool 待种植)
    {
        if (待种植)
            return 待种床缓存 != null ? 待种床缓存 : (待种床缓存 = 造畦网格(0.14f));
        return 已种床缓存 != null ? 已种床缓存 : (已种床缓存 = 造畦网格(灵田规格.垄高));
    }

    static Mesh 待种床缓存, 已种床缓存;

    /// <summary>摆放预览用的床网格（用正常那款就行）</summary>
    public static Mesh 床网格() => 床网格(false);

    /// <summary>
    /// 造一块**畦**：床面按余弦起垄（<see cref="灵田规格.垄数"/> 条）+ 四周斜裙边落到地面。
    /// </summary>
    static Mesh 造畦网格(float 垄高)
    {
        const int 横 = 24;      // 沿宽度采样几段
        const int 纵 = 6;       // 沿深度采样几段
        float 宽 = 灵田规格.床边长, 深 = 灵田规格.床边长;
        float hw = 宽 * 0.5f, hd = 深 * 0.5f;

        var 顶点 = new List<Vector3>();
        var uv = new List<Vector2>();
        var 三角 = new List<int>();

        System.Func<float, float> 面高 = x =>
        {
            float t = (x / 宽 + 0.5f) * 灵田规格.垄数 - 0.5f;      // 峰落在 t 的整数处
            float c = Mathf.Cos(2f * Mathf.PI * t);
            return 灵田规格.床顶 + 垄高 * Mathf.Clamp01(c * 0.5f + 0.5f);
        };

        // ---- 床面 ----
        for (int j = 0; j <= 纵; j++)
        {
            float z = -hd + 深 * j / 纵;
            for (int i = 0; i <= 横; i++)
            {
                float x = -hw + 宽 * i / 横;
                顶点.Add(new Vector3(x, 面高(x), z));
                uv.Add(new Vector2(i / (float)横, j / (float)纵));
            }
        }
        int 每行 = 横 + 1;
        for (int j = 0; j < 纵; j++)
            for (int i = 0; i < 横; i++)
            {
                int a = j * 每行 + i, b = a + 1, c = a + 每行, d = c + 1;
                三角.Add(a); 三角.Add(c); 三角.Add(b);
                三角.Add(b); 三角.Add(c); 三角.Add(d);
            }

        // ---- 四周裙边 ----
        //
        // ⚠️ 【坑】裙边的 UV **不能**照抄床面边缘那两个 UV：那样 v 恒等于 0 或 1，
        //    等于"只取贴图的一行"再竖着拉满整个侧面 —— 实测侧面会变成一片竖向条纹，
        //    看起来像木板而不是土。侧面要按**自己的长宽**铺 UV。
        const float 贴图密度 = 2.5f;      // 和材质的 mainTextureScale 对齐
        void 裙(Vector3 上1, Vector3 上2, float u1, float u2)
        {
            int 基 = 顶点.Count;
            float v顶 = 灵田规格.床顶 * 贴图密度;
            顶点.Add(上1); 顶点.Add(上2);
            顶点.Add(new Vector3(上1.x, 0f, 上1.z));
            顶点.Add(new Vector3(上2.x, 0f, 上2.z));
            uv.Add(new Vector2(u1, v顶)); uv.Add(new Vector2(u2, v顶));
            uv.Add(new Vector2(u1, 0f));  uv.Add(new Vector2(u2, 0f));
            三角.Add(基); 三角.Add(基 + 1); 三角.Add(基 + 2);
            三角.Add(基 + 1); 三角.Add(基 + 3); 三角.Add(基 + 2);
        }
        for (int i = 0; i < 横; i++)
        {
            float x1 = -hw + 宽 * i / 横, x2 = -hw + 宽 * (i + 1) / 横;
            float u1 = i / (float)横 * 宽 * 贴图密度, u2 = (i + 1) / (float)横 * 宽 * 贴图密度;
            裙(new Vector3(x1, 面高(x1), -hd), new Vector3(x2, 面高(x2), -hd), u1, u2);
            裙(new Vector3(x2, 面高(x2), hd), new Vector3(x1, 面高(x1), hd), u2, u1);
        }
        for (int j = 0; j < 纵; j++)
        {
            float z1 = -hd + 深 * j / 纵, z2 = -hd + 深 * (j + 1) / 纵;
            float u1 = j / (float)纵 * 深 * 贴图密度, u2 = (j + 1) / (float)纵 * 深 * 贴图密度;
            裙(new Vector3(-hw, 面高(-hw), z2), new Vector3(-hw, 面高(-hw), z1), u2, u1);
            裙(new Vector3(hw, 面高(hw), z1), new Vector3(hw, 面高(hw), z2), u1, u2);
        }

        var m = new Mesh { name = "灵田畦" };
        m.SetVertices(顶点);
        m.SetUVs(0, uv);
        m.SetTriangles(三角, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    static void 挂网格体(GameObject 挂, Mesh 网格, Material 材质, bool 加碰撞)
    {
        挂.AddComponent<MeshFilter>().sharedMesh = 网格;
        var mr = 挂.AddComponent<MeshRenderer>();
        mr.sharedMaterial = 材质;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = true;
        if (加碰撞)
        {
            var mc = 挂.AddComponent<MeshCollider>();
            mc.sharedMesh = 网格;
        }
    }
}
