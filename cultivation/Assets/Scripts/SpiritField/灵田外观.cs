using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **灵田的程序化外观** —— 田块 + 灵植全都是代码生成的网格，不依赖任何美术资源。
///
/// ## 为什么不用现成模型
///
/// 需求里灵田和灵植被明确"还没有模型，需要自行生成替代品"。
/// 自己生成的好处：
/// · **不依赖美术资源**（不用等、也不会因为资源包风格不搭而变丑）
/// · **生长阶段可以直接改网格**（幼苗矮小、成株抽高、成熟开花），
///   换成固定模型的话就得准备 3 套模型
/// · 以后要换真模型时，只需替换本文件里的建造函数，**外面一行都不用改**
///
/// ## 视觉设计
///
/// · **田块**：一块略微鼓起的土床 + 每格的土垄小丘，深褐偏冷（配洞府后山）
/// · **灵植**：动漫风的"几片细长叶片" —— 叶片绕中心均匀铺开、向外倾斜，
///   越长大越高、越外张。成熟时叶片顶端结一颗**发光的小球**（好认）
/// · **成熟提示**：头顶一个永远朝向相机的「可收」小标
///
/// ## 性能
///
/// 所有格共用一份材质（按颜色分几档缓存），所以格子再多也不会爆 draw call。
/// 网格只在**阶段变化**时重建（<see cref="刷新"/> 里比对上次的阶段），
/// **不是每帧重建** —— 每帧重建网格会卡死。
/// </summary>
public static class 灵田外观
{
    /// <summary>每个灵田实例对应的一棵视觉树</summary>
    class 视觉
    {
        public GameObject 根;
        public GameObject[] 格根;
        public GameObject[] 植根;
        public TextMesh[] 标;
        public int[] 上次阶段;

        /// <summary>让「可收」标记永远面向相机</summary>
        public void 朝向相机()
        {
            var cam = Camera.main;
            if (cam == null) return;
            for (int i = 0; i < 标.Length; i++)
            {
                if (标[i] == null || !标[i].gameObject.activeSelf) continue;
                var t = 标[i].transform;
                t.rotation = Quaternion.LookRotation(t.position - cam.transform.position, cam.transform.up);
            }
        }
    }

    static readonly Dictionary<int, 视觉> 缓存 = new Dictionary<int, 视觉>();
    static readonly Dictionary<string, Material> 材质池 = new Dictionary<string, Material>();

    // ============================================================ 对外

    public static GameObject 建造(灵田 田, Transform 父)
    {
        var 根 = new GameObject("灵田外观");
        根.transform.SetParent(父 != null ? 父 : null, false);
        根.transform.position = 田.田位置;
        根.transform.rotation = Quaternion.Euler(0f, 田.朝向, 0f);

        var v = new 视觉
        {
            根 = 根,
            格根 = new GameObject[0],
            植根 = new GameObject[0],
            标 = new TextMesh[0],
            上次阶段 = new int[0],
        };
        缓存[田.GetInstanceID()] = v;
        return 根;
    }

    /// <summary>状态变了 —— 重建所有格的视觉</summary>
    public static void 通知刷新(灵田 田)
    {
        if (田 == null) return;
        视觉 v;
        if (!缓存.TryGetValue(田.GetInstanceID(), out v) || v == null || v.根 == null)
        {
            v = null;
        }
        刷新(田, v != null ? v.根 : null);
    }

    /// <summary>重建 / 增量刷新</summary>
    public static void 刷新(灵田 田, GameObject 根)
    {
        if (田 == null || 根 == null) return;

        视觉 v;
        if (!缓存.TryGetValue(田.GetInstanceID(), out v) || v == null || v.根 != 根)
        {
            v = new 视觉 { 根 = 根 };
            缓存[田.GetInstanceID()] = v;
        }

        根.transform.position = 田.田位置;
        根.transform.rotation = Quaternion.Euler(0f, 田.朝向, 0f);

        var 格 = 田.所有格;
        int n = 格 != null ? 格.Count : 0;

        // 格子数变了 → 重建整个田块
        if (v.格根 == null || v.格根.Length != n)
            重建田块(田, v, n);

        for (int i = 0; i < n; i++)
            刷新一格(田, v, i, 格[i]);
    }

    /// <summary>每帧调一次，让「可收」标记面向相机</summary>
    public static void 每帧(灵田 田)
    {
        if (田 == null) return;
        视觉 v;
        if (缓存.TryGetValue(田.GetInstanceID(), out v) && v != null) v.朝向相机();
    }

    // ============================================================ 田块

    static void 重建田块(灵田 田, 视觉 v, int n)
    {
        // 清掉旧的
        foreach (var g in v.格根) if (g != null) Object.Destroy(g);
        foreach (var g in v.植根) if (g != null) Object.Destroy(g);
        foreach (var t in v.标) if (t != null) Object.Destroy(t.gameObject);

        v.格根 = new GameObject[n];
        v.植根 = new GameObject[n];
        v.标 = new TextMesh[n];
        v.上次阶段 = new int[n];
        for (int i = 0; i < n; i++) v.上次阶段[i] = -1;

        if (n == 0) return;

        int 每行 = Mathf.Max(1, 田.每行格数);
        int 行数 = Mathf.CeilToInt(n / (float)每行);
        float 距 = Mathf.Max(0.3f, 田.格间距) * 1.25f;

        // ---- 土床：把整片田包住的一块扁平土台 ----
        float 宽 = 每行 * 距 + 0.5f;
        float 深 = 行数 * 距 + 0.5f;
        var 床 = new GameObject("土床");
        床.transform.SetParent(v.根.transform, false);
        床.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        建网格体(床, 建土床网格(宽, 深), 取材质(new Color(0.26f, 0.20f, 0.15f)));

        for (int i = 0; i < n; i++)
        {
            int 列 = i % 每行, 行 = i / 每行;
            float x = (列 - (每行 - 1) * 0.5f) * 距;
            float z = -(行 - (行数 - 1) * 0.5f) * 距;

            var 格根 = new GameObject("格" + (i + 1));
            格根.transform.SetParent(v.根.transform, false);
            格根.transform.localPosition = new Vector3(x, 0f, z);
            v.格根[i] = 格根;

            // 土垄小丘：让每格看得出是一块地
            建网格体(格根, 建土垄网格(距 * 0.78f), 取材质(new Color(0.32f, 0.24f, 0.18f)));

            var 植根 = new GameObject("植");
            植根.transform.SetParent(格根.transform, false);
            v.植根[i] = 植根;

            var 标 = 建标记(格根.transform, "可收");
            v.标[i] = 标;
            标.gameObject.SetActive(false);
        }
    }

    static void 刷新一格(灵田 田, 视觉 v, int i, 灵田格 g)
    {
        if (g == null || v.植根 == null || i >= v.植根.Length) return;
        int 阶段 = g.阶段;

        // ★ 只在**阶段变化**时重建网格。每帧重建会卡死
        if (v.上次阶段[i] == 阶段) { 更新标记(v, i, g); return; }
        v.上次阶段[i] = 阶段;

        var 根 = v.植根[i];
        for (int c = 根.transform.childCount - 1; c >= 0; c--)
            Object.Destroy(根.transform.GetChild(c).gameObject);

        if (阶段 == 0) { 更新标记(v, i, g); return; }

        var d = g.植;
        if (d == null) { 更新标记(v, i, g); return; }

        // 幼苗矮小、成株抽高、成熟再高一点
        float t = g.进度;
        float 高 = Mathf.Lerp(d.株高 * 0.45f, d.株高 * 1.5f, Mathf.Clamp01(t));
        if (阶段 == 3) 高 *= 1.12f;

        建网格体(根, 建灵植网格(d, 高, 阶段), 取材质(d.主色));
        if (d.会开花 && 阶段 >= 2)
            建网格体(根, 建花网格(d, 高), 取材质(d.花色));

        更新标记(v, i, g);
    }

    static void 更新标记(视觉 v, int i, 灵田格 g)
    {
        if (v.标 == null || i >= v.标.Length || v.标[i] == null) return;
        bool 显示 = g != null && g.已成熟;
        if (v.标[i].gameObject.activeSelf != 显示) v.标[i].gameObject.SetActive(显示);
    }

    static TextMesh 建标记(Transform 父, string 文本)
    {
        var go = new GameObject("标记");
        go.transform.SetParent(父, false);
        go.transform.localPosition = new Vector3(0f, 1.05f, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.text = 文本;
        tm.fontSize = 42;
        tm.characterSize = 0.022f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(1f, 0.93f, 0.42f);

        // 字体：和项目 HUD 用同一套（SimHei），找不到就退回默认（会显示成方块，但不崩）
        var f = 取中文字体();
        if (f != null)
        {
            tm.font = f;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = f.material;
        }
        return tm;
    }

    static Font 取中文字体()
    {
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
        if (f != null) return f;
#endif
        foreach (var x in Resources.FindObjectsOfTypeAll<Font>())
            if (x != null && x.name.ToLowerInvariant().Contains("simhei")) return x;
        return null;
    }

    // ============================================================ 网格生成

    /// <summary>整片田的土床：一块略微鼓起、带斜边的扁平台</summary>
    static Mesh 建土床网格(float 宽, float 深)
    {
        float hw = 宽 * 0.5f, hd = 深 * 0.5f;
        float 台高 = 0.30f, 边收 = 0.30f;

        var 顶 = new[]
        {
            new Vector3(-hw + 边收, 台高, -hd + 边收),
            new Vector3( hw - 边收, 台高, -hd + 边收),
            new Vector3( hw - 边收, 台高,  hd - 边收),
            new Vector3(-hw + 边收, 台高,  hd - 边收),
        };
        var 底 = new[]
        {
            new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd),
            new Vector3(hw, 0f,  hd),  new Vector3(-hw, 0f,  hd),
        };

        var 顶点 = new List<Vector3>();
        var 三角 = new List<int>();
        // 顶面
        顶点.AddRange(顶);
        三角.AddRange(new[] { 0, 2, 1, 0, 3, 2 });
        // 四面斜边（每条边一个四边形）
        int[] 序 = { 0, 1, 2, 3 };
        for (int i = 0; i < 4; i++)
        {
            int a = 序[i], b = 序[(i + 1) % 4];
            int 基 = 顶点.Count;
            顶点.Add(顶[a]); 顶点.Add(顶[b]); 顶点.Add(底[b]); 顶点.Add(底[a]);
            三角.AddRange(new[] { 基, 基 + 2, 基 + 1, 基, 基 + 3, 基 + 2 });
        }
        // 底面
        int 底基 = 顶点.Count;
        顶点.AddRange(底);
        三角.AddRange(new[] { 底基, 底基 + 1, 底基 + 2, 底基, 底基 + 2, 底基 + 3 });

        return 收尾(顶点, 三角);
    }

    /// <summary>一格上的土垄小丘</summary>
    static Mesh 建土垄网格(float 直径)
    {
        float r = 直径 * 0.5f, 高 = 0.16f;
        int 段 = 10;
        var 顶点 = new List<Vector3>();
        var 三角 = new List<int>();

        顶点.Add(new Vector3(0f, 高, 0f));                     // 顶心
        for (int i = 0; i < 段; i++)
        {
            float a = i / (float)段 * Mathf.PI * 2f;
            顶点.Add(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
        }
        for (int i = 0; i < 段; i++)
        {
            int c = 1 + i, nx = 1 + (i + 1) % 段;
            三角.AddRange(new[] { 0, c, nx });
        }
        return 收尾(顶点, 三角);
    }

    /// <summary>
    /// 一株灵植：绕中心均匀铺开的几片细长叶片。
    /// 阶段越高叶片越多、越长、越外张。
    /// </summary>
    static Mesh 建灵植网格(灵植定义 d, float 高, int 阶段)
    {
        int 叶片数 = 阶段 >= 3 ? 7 : 阶段 == 2 ? 6 : 4;
        float 外张 = 阶段 >= 2 ? 0.55f : 0.30f;      // 叶片倾斜程度
        float 半宽 = Mathf.Max(0.05f, 高 * 0.22f);

        var 顶点 = new List<Vector3>();
        var 三角 = new List<int>();

        for (int i = 0; i < 叶片数; i++)
        {
            float a = i / (float)叶片数 * Mathf.PI * 2f + d.株高 * 7f;   // 用株高打散相位，免得每株长得一样
            float 倾斜 = 外张 * (0.7f + 0.6f * ((i * 37 % 10) / 10f));

            var 外 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var 侧 = new Vector3(-外.z, 0f, 外.x);

            // 叶片：根部窄、中部最宽、梢部收成尖
            var 根 = Vector3.zero;
            var 中 = 外 * (高 * 0.35f * 倾斜) + Vector3.up * (高 * 0.55f);
            var 梢 = 外 * (高 * 0.62f * 倾斜) + Vector3.up * 高;

            int b = 顶点.Count;
            顶点.Add(根 - 侧 * (半宽 * 0.35f));
            顶点.Add(根 + 侧 * (半宽 * 0.35f));
            顶点.Add(中 - 侧 * 半宽);
            顶点.Add(中 + 侧 * 半宽);
            顶点.Add(梢);

            三角.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });   // 下半段
            三角.AddRange(new[] { b + 2, b + 4, b + 3 });                    // 收尖
        }
        return 收尾(顶点, 三角);
    }

    /// <summary>成熟后结的小花：顶端一圈朝上的小三角面</summary>
    static Mesh 建花网格(灵植定义 d, float 高)
    {
        var 顶点 = new List<Vector3>();
        var 三角 = new List<int>();
        int 瓣 = 5;
        float r = Mathf.Max(0.05f, 高 * 0.16f);

        for (int i = 0; i < 瓣; i++)
        {
            float a = i / (float)瓣 * Mathf.PI * 2f;
            var 外 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var 侧 = new Vector3(-外.z, 0f, 外.x);
            float y = 高 * 0.92f;
            int b = 顶点.Count;
            顶点.Add(外 * (r * 0.2f) + Vector3.up * y);
            顶点.Add(外 * r + 侧 * (r * 0.45f) + Vector3.up * (y + r * 0.25f));
            顶点.Add(外 * r - 侧 * (r * 0.45f) + Vector3.up * (y + r * 0.25f));
            三角.AddRange(new[] { b, b + 1, b + 2 });
        }
        return 收尾(顶点, 三角);
    }

    static Mesh 收尾(List<Vector3> 顶点, List<int> 三角)
    {
        var m = new Mesh();
        m.SetVertices(顶点);
        m.SetTriangles(三角, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ============================================================ 材质 / 网格体

    static void 建网格体(GameObject 父, Mesh 网格, Material 材质)
    {
        var go = new GameObject("mesh");
        go.transform.SetParent(父.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = 网格;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = 材质;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    /// <summary>
    /// 按颜色缓存材质，全部走 Standard 哑光 —— 
    /// 和「全局材质收敛」定的基准一致（金属 0 / 光泽 0），这样灵田不会在
    /// 统一调色之后显得突兀。
    /// </summary>
    static Material 取材质(Color c)
    {
        string k = $"{c.r:F3}_{c.g:F3}_{c.b:F3}";
        Material m;
        if (材质池.TryGetValue(k, out m) && m != null) return m;

        var sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
        m = new Material(sh) { name = "灵田_" + k, hideFlags = HideFlags.HideAndDontSave };
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
        材质池[k] = m;
        return m;
    }
}
