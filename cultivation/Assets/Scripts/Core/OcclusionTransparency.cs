using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 【通用装配 · 挂在主相机上】**遮挡与特效开关**：把「摄像机 ↔ 主角」连线挡住的
/// **建筑/场景**与**树冠**变透明，免得镜头被挡住看不见主角。
///
/// ============================================================
/// 两条通路（2026-09-28 补了树冠那条）
/// ============================================================
/// **① 碰撞体射线**（原有，管建筑/墙/石头）
///   从相机朝主角身上几个高度点打射线 → 命中的渲染器换透明。
///
/// **② 树冠按包围盒判**（新增，管树）
///   ⚠️ **树冠没有碰撞体**（实测：树干 153 个 CapsuleCollider，树冠 365 个子渲染体
///   **一个都没有**），所以射线**永远打不到树冠** —— 这是"树冠遮挡相机看不到主角"
///   一直没被解决的根本原因。
///   改成：把场景里所有树冠的包围盒和"相机→主角"的线段做**相交测试**，命中就透明。
///   每帧只对"在相机视锥里"的树做测试（先剔掉背后的），开销可忽略。
///
/// ============================================================
/// 材质怎么换（不能污染资产）
/// ============================================================
///   · **绝不改 `sharedMaterial` 指向的那个材质本身** —— 树冠材质是**多棵树共用**的，
///     改一处会让所有树一起透明。只改渲染器的 `sharedMaterials` **数组**，指向副本。
///   · 副本按**源材质**缓存 → 500 棵树只有几十个材质实例。
///   · 离场/禁用/销毁时**原样还回去**。具体见 <see cref="遮挡素材"/>。
///
/// ============================================================
/// 生效范围
/// ============================================================
/// 由 `Assets/resources/场景特效开关.asset` 的「建筑透明」一列决定
/// （不再靠"哪个场景挂了组件"，否则会被通用装配同步冲掉）。
/// </summary>
[DisallowMultipleComponent]
public class OcclusionTransparency : MonoBehaviour
{
    [Header("主角")]
    [Tooltip("主角根。留空自动找 PlayerVitals，再退化为名为 Player 的根")]
    public Transform 主角;

    [Header("建筑 / 场景遮挡（射线）")]
    [Tooltip("**淡到最透时保留的淡影**（0 = 彻底消失；0.22 = 还看得见一层淡影，不至于突然空一块）")]
    [Range(0.05f, 0.9f)] public float 透明度 = 0.22f;
    [Tooltip("朝主角身上打几条射线（覆盖身高，避免只挡到腿时看不见）")]
    public int 射线数 = 5;
    [Tooltip("参与透视的层（默认全部；UI 层会被自动排除）")]
    public LayerMask 层 = ~0;

    [Header("树冠遮挡（按包围盒判，因为树冠没有碰撞体）")]
    [Tooltip("树冠遮挡相机→主角连线时也变透明。用户 2026-09-28 要求")]
    public bool 树冠透明 = true;
    [Tooltip("树冠的淡影（比建筑更透，尽量别挡视线）")]
    [Range(0.0f, 0.9f)] public float 树冠透明度 = 0.12f;
    [Tooltip("树冠的判定用「包围盒和线段的距离」小于这个余量就算挡（米）")]
    public float 树冠余量 = 0.35f;

    [Header("水墨淡出（2026-10-03：不再是硬切，也不再是塑料半透明）")]
    [Tooltip("★ **一键退回**：关掉就用改造前那套（`Legacy Shaders/Transparent/Diffuse`）的观感，\n" +
             "但**淡入淡出仍然保留**（老材质只驱动 alpha）。想完全回到最早那个硬切版本，见文档 §7.3 的说明。")]
    public bool 用水墨淡出 = true;
    [Tooltip("淡出时长（秒）：实心 → 只剩剪影")]
    public float 淡出秒 = 0.30f;
    [Tooltip("回场时长（秒）：剪影 → 实心。⚠️ 比淡出略长，回场才不会「啪」地弹回来")]
    public float 回场秒 = 0.40f;
    [Tooltip("侵蚀强度：物体的形状像墨被水化开一样散掉。**默认 0 = 纯淡入淡出**\n" +
             "⚠️ 2026-10-03 用户看过一版侵蚀，明确说难看 ⇒ 默认关掉，想试再往上调")]
    [Range(0f, 1f)] public float 侵蚀 = 0f;
    [Tooltip("树冠的侵蚀。⚠️ 树冠是 billboard 卡片、量大（365 个子渲染体）—— 建议一直留 0")]
    [Range(0f, 1f)] public float 树冠侵蚀 = 0f;
    [Tooltip("边缘墨散：侵蚀边界压成墨色的宽度与强度（**只有在「侵蚀 > 0」时才起作用**）")]
    [Range(0.01f, 0.5f)] public float 墨边宽 = 0.16f;
    [Range(0f, 1f)] public float 墨边强度 = 0.25f;
    [Tooltip("淡出时往物体上叠的宣纸纸纹。**默认 0 = 不叠**\n" +
             "⚠️ 叠上去物体会变成一张纸板（用户 2026-10-03：「为啥在摄像头前盖了一层宣纸纹理，太丑了」）")]
    [Range(0f, 1f)] public float 纸纹 = 0f;

    [Header("开关（运行时由 场景特效开关.asset 覆盖）")]
    [Tooltip("建筑/墙/石头遮挡时变透明。由场景特效开关表的「建筑透明」列决定")]
    public bool 建筑遮挡启用 = true;

    [Header("通用")]
    [Tooltip("主角脚底到头顶的高度（米）")]
    public float 身高 = 1.8f;
    [Tooltip("拿开遮挡后保持多久再恢复（秒），防止边缘来回闪")]
    public float 保持 = 0.12f;
    [Tooltip("打印调试日志")]
    public bool 打印日志 = false;

    // ⚠️ 这几个容器**不能写成 readonly + 只在字段初始化器里 new**：
    //    本组件是**存进场景**的（相机上那份是序列化过的），实测从场景反序列化出来的实例
    //    这几个非序列化容器会是 **null** ⇒ LateUpdate 第一行就 NRE、每帧刷屏（踩过，2026-10-03）。
    //    所以统一走 确保容器() 兜底。
    遮挡素材.台账 _台账;
    Dictionary<Renderer, float> _保持到;
    /// <summary>本帧判定为遮挡的渲染器（值是"它该用的透明度"的历史遗留，现在只当命中标记）</summary>
    Dictionary<Renderer, float> _本帧;
    List<Renderer> _待恢复;
    /// <summary>每帧推进用的快照（不能直接遍历字典：推进过程中可能把渲染器还回去）</summary>
    List<Renderer> _在管快照;

    /// <summary>反序列化兜底：容器为 null 就地补上（见上面那条注释）</summary>
    void 确保容器()
    {
        if (_台账 == null) _台账 = new 遮挡素材.台账();
        if (_保持到 == null) _保持到 = new Dictionary<Renderer, float>();
        if (_本帧 == null) _本帧 = new Dictionary<Renderer, float>();
        if (_待恢复 == null) _待恢复 = new List<Renderer>();
        if (_在管快照 == null) _在管快照 = new List<Renderer>();
    }

    // 树冠登记表：一棵树一行（按"树根"找，把子渲染体都收进来）
    class 树
    {
        public Renderer[] 渲染s;
        public Bounds 盒;
    }
    readonly List<树> _树s = new List<树>();
    float _重建树表到;

    void Awake()
    {
        确保容器();
        // ★ 生效范围交给「场景特效开关表」决定（而不是"哪个场景挂了组件"）——
        //   这样组件可以跟着通用装配铺到所有场景，不会再被同步冲掉、也不会到处生效。
        //
        // ★★ 2026-09-28：表里是**两个独立开关**（建筑透明 / 树冠透明）。
        //    用户明确要求「我只是要树冠，不要房屋」——
        //    所以**不能再把两者绑在一起**（原来一个「建筑透明」同时管建筑和树冠）。
        var e = 场景特效开关.取(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        建筑遮挡启用 = e != null && e.建筑透明;
        树冠透明 = e != null && e.树冠透明;

        if (!建筑遮挡启用 && !树冠透明)
        {
            Debug.Log("[场景特效开关] 「" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                + "」建筑透明 / 树冠透明 都没开 → 关闭遮挡与特效开关组件", this);
            enabled = false;
        }
    }

    void LateUpdate()
    {
        确保容器();
        var cam = GetComponent<Camera>();
        if (cam == null || !cam.isActiveAndEnabled) return;

        if (主角 == null)
        {
            var 命 = FindObjectOfType<PlayerVitals>();
            if (命 != null) 主角 = 命.transform;
            else { var g = GameObject.Find("Player"); if (g != null) 主角 = g.transform; }
            if (主角 == null) return;
        }

        // 主角身上几个高度点（供射线和树冠测试共用）
        int n = Mathf.Max(1, 射线数);
        var 脚 = 主角.position;
        var 采样点 = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            float f = n <= 1 ? 0.55f : i / (float)(n - 1);
            采样点[i] = 脚 + Vector3.up * (身高 * Mathf.Lerp(0.08f, 0.95f, f));
        }

        _本帧.Clear();
        if (建筑遮挡启用) 收碰撞体射线(cam, 采样点);
        if (树冠透明) 收树冠(cam, 采样点);

        float now = Time.unscaledTime;

        // ---- 本帧命中的：换上水墨材质，并续上保持时间 ----
        foreach (var kv in _本帧)
        {
            var r = kv.Key;
            if (r == null) continue;
            bool 新 = _台账.换成水墨(r, 用水墨淡出);
            if (新 && 打印日志) Debug.Log("[遮挡与特效开关] 换成水墨：" + r.name, r);
            _保持到[r] = now + Mathf.Max(0f, 保持);
        }

        // ---- 每帧推进所有在管的渲染器（淡出 / 回场都是连续的，不再是硬切）----
        //   参数按"是建筑还是树冠"现算：树冠更透、侵蚀更轻（片状资产 + 量大）
        _在管快照.Clear();
        foreach (var r in _台账.名单) if (r != null) _在管快照.Add(r);

        _待恢复.Clear();
        foreach (var r in _在管快照)
        {
            float 保持到;
            bool 遮挡 = _本帧.ContainsKey(r) || (_保持到.TryGetValue(r, out 保持到) && now <= 保持到);
            float f = _台账.推进(r, 遮挡, 取参数(r));
            if (!遮挡 && f <= 0.0001f) _待恢复.Add(r);      // 已经完全回来了 → 把原材质还回去
        }

        foreach (var r in _待恢复)
        {
            if (打印日志 && r != null) Debug.Log("[遮挡与特效开关] 还原：" + r.name, r);
            _台账.还原(r);
            _保持到.Remove(r);
        }
    }

    /// <summary>按"建筑还是树冠"组一套水墨参数</summary>
    遮挡素材.水墨参数 取参数(Renderer r)
    {
        bool 树 = 遮挡素材.是树冠(r);
        var p = 树 ? 遮挡素材.水墨参数.树冠默认() : 遮挡素材.水墨参数.建筑默认();
        p.剪影 = 树 ? 树冠透明度 : 透明度;
        p.侵蚀 = 树 ? 树冠侵蚀 : 侵蚀;
        p.墨边宽 = 墨边宽;
        p.墨边强度 = 树 ? 墨边强度 * 0.55f : 墨边强度;
        p.纸纹 = 纸纹;
        p.淡出秒 = 淡出秒;
        p.回场秒 = 回场秒;
        return p;
    }

    // ---------------------------------------------------------------- 通路 ①：碰撞体射线

    void 收碰撞体射线(Camera cam, Vector3[] 采样点)
    {
        int 掩码 = 层.value;
        foreach (var 点 in 采样点)
        {
            var 向 = 点 - cam.transform.position;
            float 距 = 向.magnitude;
            if (距 < 0.01f) continue;

            var 命中s = Physics.RaycastAll(cam.transform.position, 向 / 距, 距, 掩码, QueryTriggerInteraction.Ignore);
            foreach (var h in 命中s)
            {
                // ★ 碰撞体在根、网格在子物体上的情况很常见（环境 FBX 就是：根挂 BoxCollider、
                //   子物体各带一个网格）。只找 GetComponentInParent 会漏掉它们 ✗
                var 渲染s = h.collider.GetComponentsInChildren<Renderer>();
                if (渲染s.Length == 0)
                {
                    var 父 = h.collider.GetComponentInParent<Renderer>();
                    if (父 != null) 渲染s = new[] { 父 };
                }
                foreach (var r in 渲染s) 收(r, 透明度);
            }
        }
    }

    void 收(Renderer r, float 透明)
    {
        if (r == null) return;
        if (r.transform.IsChildOf(主角)) return;          // 主角自己不淡
        if (r is ParticleSystemRenderer) return;
        if (!_本帧.ContainsKey(r)) _本帧[r] = 透明;
    }

    // ---------------------------------------------------------------- 通路 ②：树冠包围盒

    void 收树冠(Camera cam, Vector3[] 采样点)
    {
        if (Time.unscaledTime > _重建树表到 || _树s.Count == 0) 重建树表();

        // 只在相机视锥里的树才做相交测试（把背后的、屏幕外的剔掉）
        var 面 = GeometryUtility.CalculateFrustumPlanes(cam);

        foreach (var 树 in _树s)
        {
            if (树 == null || 树.渲染s == null || 树.渲染s.Length == 0) continue;

            bool 有活的 = false;
            foreach (var r in 树.渲染s) if (r != null && r.enabled && r.gameObject.activeInHierarchy) { 有活的 = true; break; }
            if (!有活的) continue;

            if (!GeometryUtility.TestPlanesAABB(面, 树.盒)) continue;

            // 任一采样点的连线穿进这棵树的包围盒 → 就算挡
            foreach (var 点 in 采样点)
            {
                if (!线段近包围盒(cam.transform.position, 点, 树.盒, 树冠余量)) continue;
                foreach (var r in 树.渲染s)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    收(r, 树冠透明度);
                }
                break;
            }
        }
    }

    /// <summary>重建树冠表：按"树根"（名字以 environment_Tree 开头、且父级不是）收集</summary>
    void 重建树表()
    {
        _树s.Clear();
        _重建树表到 = Time.unscaledTime + 5f;      // 5 秒一次，够应付运行时新生成的树

        var 根s = new List<Transform>();
        foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
        {
            if (r == null || r is ParticleSystemRenderer) continue;
            var go = r.gameObject;
            if (!go.scene.IsValid()) continue;                      // 排除资产/prefab
            if (!遮挡素材.是树冠(r)) continue;

            // 往上找到最外层的 environment_Tree 根
            var 根 = r.transform;
            while (根.parent != null && 根.parent.name.StartsWith("environment_Tree", System.StringComparison.OrdinalIgnoreCase))
                根 = 根.parent;
            if (!根s.Contains(根)) 根s.Add(根);
        }

        foreach (var 根 in 根s)
        {
            var rs = 根.GetComponentsInChildren<Renderer>(true);
            var 留 = new List<Renderer>();
            foreach (var r in rs) if (r != null && !(r is ParticleSystemRenderer)) 留.Add(r);
            if (留.Count == 0) continue;

            var 盒 = 留[0].bounds;
            for (int i = 1; i < 留.Count; i++) 盒.Encapsulate(留[i].bounds);
            _树s.Add(new 树 { 渲染s = 留.ToArray(), 盒 = 盒 });
        }

        if (打印日志) Debug.Log("[遮挡与特效开关] 树冠表重建：树 " + _树s.Count + " 棵");
    }

    /// <summary>线段（a→b）离包围盒最近距离是否小于 余量</summary>
    static bool 线段近包围盒(Vector3 a, Vector3 b, Bounds 盒, float 余量)
    {
        // 包围盒往外扩 余量，再做"线段 vs AABB"的 slab 相交测试
        var min = 盒.min - Vector3.one * 余量;
        var max = 盒.max + Vector3.one * 余量;

        var d = b - a;
        float t0 = 0f, t1 = 1f;
        for (int i = 0; i < 3; i++)
        {
            float 起 = a[i], 方 = d[i], lo = min[i], hi = max[i];
            if (Mathf.Abs(方) < 1e-6f)
            {
                if (起 < lo || 起 > hi) return false;             // 平行且在板外
                continue;
            }
            float ta = (lo - 起) / 方, tb = (hi - 起) / 方;
            if (ta > tb) { var tmp = ta; ta = tb; tb = tmp; }
            if (ta > t0) t0 = ta;
            if (tb < t1) t1 = tb;
            if (t0 > t1) return false;
        }
        return true;
    }

    void OnDisable() { _台账.全还原(); _保持到.Clear(); _本帧.Clear(); _待恢复.Clear(); }
    void OnDestroy() { _台账.全还原(); }

    // ---------------------------------------------------------------- 只读诊断（自动化验证用）
    //
    // 本组件是**由 场景自举 补到相机上**的，参数不写进场景 ⇒ 验证脚本需要能读到运行时状态，
    // 否则"淡出到底有没有在推进"只能靠肉眼看。两个只读入口都不改任何东西。

    /// <summary>当前被水墨淡出管着的渲染器数量（0 = 这一帧没有任何东西在淡）。读不到返回 -1</summary>
    public int 水墨数量
    {
        get { try { return _台账.数量; } catch (System.Exception) { return -1; } }
    }

    /// <summary>随便挑一个在管的渲染器，返回它的 `_Fade`（没有 = -1，读异常 = -2）。自动化验证用</summary>
    public float 取一个进度()
    {
        try
        {
            foreach (var r in _台账.名单) return _台账.取进度(r);
        }
        catch (System.Exception) { return -2f; }
        return -1f;
    }
}
