using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 【通用装配 · 挂在主相机上】**遮挡透视**：把「摄像机 ↔ 主角」连线挡住的
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
    [Tooltip("遮挡时的透明度")]
    [Range(0.05f, 0.9f)] public float 透明度 = 0.22f;
    [Tooltip("朝主角身上打几条射线（覆盖身高，避免只挡到腿时看不见）")]
    public int 射线数 = 5;
    [Tooltip("参与透视的层（默认全部；UI 层会被自动排除）")]
    public LayerMask 层 = ~0;

    [Header("树冠遮挡（按包围盒判，因为树冠没有碰撞体）")]
    [Tooltip("树冠遮挡相机→主角连线时也变透明。用户 2026-09-28 要求")]
    public bool 树冠透明 = true;
    [Tooltip("树冠的透明度（比建筑更透，尽量别挡视线）")]
    [Range(0.0f, 0.9f)] public float 树冠透明度 = 0.12f;
    [Tooltip("树冠的判定用「包围盒和线段的距离」小于这个余量就算挡（米）")]
    public float 树冠余量 = 0.35f;

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

    readonly 遮挡素材.台账 _台账 = new 遮挡素材.台账();
    readonly Dictionary<Renderer, float> _保持到 = new Dictionary<Renderer, float>();
    /// <summary>本帧判定为遮挡的渲染器 → 它该用的透明度（建筑和树冠不一样）</summary>
    readonly Dictionary<Renderer, float> _本帧 = new Dictionary<Renderer, float>();
    readonly List<Renderer> _待恢复 = new List<Renderer>();

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
                + "」建筑透明 / 树冠透明 都没开 → 关闭遮挡透视组件", this);
            enabled = false;
        }
    }

    void LateUpdate()
    {
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

        // ---- 本帧命中的：换成透明，并续上保持时间 ----
        foreach (var kv in _本帧)
        {
            var r = kv.Key;
            if (r == null) continue;
            bool 新 = _台账.变透明(r, kv.Value);
            if (新 && 打印日志) Debug.Log("[遮挡透视] 变透明：" + r.name, r);
            _保持到[r] = Time.unscaledTime + Mathf.Max(0f, 保持);
        }

        // ---- 过期的：恢复原材质 ----
        _待恢复.Clear();
        foreach (var kv in _保持到)
            if (Time.unscaledTime > kv.Value) _待恢复.Add(kv.Key);
        foreach (var r in _待恢复)
        {
            if (打印日志 && r != null) Debug.Log("[遮挡透视] 恢复：" + r.name, r);
            _台账.还原(r);
            _保持到.Remove(r);
        }
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

        if (打印日志) Debug.Log("[遮挡透视] 树冠表重建：树 " + _树s.Count + " 棵");
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
}
