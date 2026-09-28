using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// **模型 3D 预览**（外观页右侧那块「模型3D展示」）。
///
/// 做法：把模型实例放到很远的地方（默认 Y -1000），用一台**只看得见它**的小相机渲染进 RenderTexture，
/// 再显示到面板里的 <see cref="画布"/>（RawImage）。这样不用动工程里的 Layer 设置，
/// 也不会把场景世界渲进去（相机 farClip 很短 + 纯色清屏）。
///
/// ### 旋转规则（用户 2026-09-27 明确要求）
/// **只能左右转，不能上下转** —— 拖动只取横向位移累加到 <see cref="当前偏航"/>，
/// 竖向位移直接丢掉；模型的欧拉角恒为 `(0, 当前偏航, 0)`，**俯仰永远是 0**，不可能翻过来。
///
/// 用法：
/// ```csharp
/// 预览.显示(外观定义);   // 换预览的模型（只换网格，和玩家身上那套换法一致）
/// ```
/// </summary>
[DisallowMultipleComponent]
public class 外观预览 : MonoBehaviour, IDragHandler, IPointerDownHandler
{
    [Header("显示")]
    [Tooltip("把预览画面显示到哪（面板里的 RawImage）。留空 = 自己找一个/自己建一个")]
    public RawImage 画布;

    [Tooltip("预览分辨率")]
    public Vector2 分辨率 = new Vector2(512f, 512f);

    [Header("旋转")]
    [Tooltip("水平拖动灵敏度（度 / 像素）")]
    public float 灵敏度 = 0.4f;

    [Tooltip("松手后是否慢慢自转到这个角度（关掉 = 完全不动）")]
    public bool 松手回正 = false;

    [Tooltip("初始朝向（度）")]
    public float 初始偏航 = 180f;

    [Tooltip("当前偏航（度）。**俯仰永远是 0** —— 用户要求只能左右转")]
    public float 当前偏航 = 180f;

    [Header("布光")]
    public Color 背景色 = new Color(0.62f, 0.62f, 0.62f, 1f);
    public float 相机距离 = 3.2f;
    public float 相机高度 = 1.2f;
    public float 灯光强度 = 1.1f;

    GameObject 预览根;
    Camera 相机;
    RenderTexture 贴图;
    Transform 模型根;
    Mesh 原始网格;
    Material[] 原始材质;
    SkinnedMeshRenderer 预览网格;
    bool 已初始化;
    AppearanceDefinition 上次展示;      // 重新启用时用来恢复画面

    public float 偏航 => 当前偏航;

    void Awake()
    {
        当前偏航 = 初始偏航;
        建立();
    }

    void OnDestroy() { 拆掉(); }

    /// <summary>
    /// ★ 面板一关就把预览台收掉。
    ///
    /// 预览台是个**独立根物体**（不在本组件底下），只在 `OnDestroy` 里销毁是不够的 ——
    /// 面板只是 `SetActive(false)`，组件不会被销毁，于是预览台会**一直活在场景里**。
    /// 配合下面那个"相机挂错地方"的老 bug，就会出现用户报的「莫名多出一个成男村民」。
    /// </summary>
    void OnDisable() { 拆掉(); }

    /// <summary>重新启用时把预览台搭回来，并恢复上次展示的那件外观（否则预览会一片空白）</summary>
    void OnEnable()
    {
        建立();
        if (上次展示 != null) 显示(上次展示);
    }

    void 拆掉()
    {
        // ★ 顺序很重要：先把相机的 targetTexture 摘掉，再 Release/Destroy 那张 RT。
        //   否则 Unity 报 “Releasing render texture that is set as Camera.targetTexture!”
        //   （上一轮 Play Mode 实测报出来的）
        if (相机 != null) 相机.targetTexture = null;
        if (画布 != null) 画布.texture = null;
        if (贴图 != null) { 贴图.Release(); Destroy(贴图); 贴图 = null; }
        if (预览根 != null) { Destroy(预览根); 预览根 = null; }

        // 状态全部复位，下次 OnEnable/显示() 时会重新搭一套
        相机 = null; 模型根 = null; 预览网格 = null;
        原始网格 = null; 原始材质 = null; 已初始化 = false;
    }

    /// <summary>搭一套"隔离"的预览环境</summary>
    void 建立()
    {
        if (已初始化) return;
        已初始化 = true;

        预览根 = new GameObject("外观预览台");
        预览根.transform.position = new Vector3(0f, -1000f, 0f);

        // 相机：只看这一小块，纯色清屏
        //
        // ★★ 相机必须是预览根的**子物件**，**绝对不能** `预览根.AddComponent<Camera>()`！
        //    因为那样 `相机.transform` 就是 `预览根.transform`，下面那句
        //    `相机.transform.localPosition = …` 等于把**预览根自己**从 -1000 拽回
        //    (0, 1.2, -3.2) —— 正好是玩家身边。预览用的模型于是站进了真实世界：
        //    它会顶着名牌、被 NpcAiHuman 带着到处走、还能被右键锁定和对话。
        //    （用户 2026-09-28 报的「莫名多出一个成男村民」就是这个。）
        相机 = new GameObject("预览相机").AddComponent<Camera>();
        相机.transform.SetParent(预览根.transform, false);
        相机.clearFlags = CameraClearFlags.SolidColor;
        相机.backgroundColor = 背景色;
        相机.fieldOfView = 30f;
        相机.nearClipPlane = 0.1f;
        相机.farClipPlane = 12f;
        相机.transform.localPosition = new Vector3(0f, 相机高度, -相机距离);
        相机.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);   // 略俯视的机位（模型本身不俯仰）

        // 灯：主光 + 补光，够看清就行
        var 灯 = new GameObject("主光").AddComponent<Light>();
        灯.transform.SetParent(预览根.transform, false);
        灯.type = LightType.Directional;
        灯.intensity = 灯光强度;
        灯.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
        var 补 = new GameObject("补光").AddComponent<Light>();
        补.transform.SetParent(预览根.transform, false);
        补.type = LightType.Directional;
        补.intensity = 0.45f;
        补.transform.rotation = Quaternion.Euler(-20f, -40f, 0f);

        // RenderTexture
        int w = Mathf.Max(64, Mathf.RoundToInt(分辨率.x));
        int h = Mathf.Max(64, Mathf.RoundToInt(分辨率.y));
        贴图 = new RenderTexture(w, h, 16) { name = "外观预览RT" };
        相机.targetTexture = 贴图;

        if (画布 == null) 画布 = 自己找画布();
        if (画布 != null) 画布.texture = 贴图;
    }

    RawImage 自己找画布()
    {
        var 父 = transform;
        while (父 != null)
        {
            var r = 父.GetComponentInChildren<RawImage>(true);
            if (r != null) return r;
            父 = 父.parent;
        }
        return null;
    }

    /// <summary>预览某一件外观（只换网格 + 材质，和玩家身上那套换法一致）</summary>
    public void 显示(AppearanceDefinition 外观)
    {
        建立();
        if (外观 == null) return;
        上次展示 = 外观;

        var 源 = 外观.取模型();
        var 源网格 = 源 != null ? 源.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
        if (源网格 == null) { Debug.LogWarning("[外观预览] 「" + 外观.DisplayName + "」没有蒙皮网格", 外观); return; }

        if (模型根 == null)
        {
            var 实例 = Instantiate(源, 预览根.transform);
            实例.name = "预览模型";
            实例.transform.localPosition = Vector3.zero;
            实例.transform.localRotation = Quaternion.Euler(0f, 当前偏航, 0f);
            实例.transform.localScale = Vector3.one;
            模型根 = 实例.transform;
            预览网格 = 实例.GetComponentInChildren<SkinnedMeshRenderer>(true);
            // 预览用的模型只留渲染：碰撞 + **所有会"干活"的 NPC 脚本**都去掉
            去掉会干活的组件(实例);
            原始网格 = 预览网格 != null ? 预览网格.sharedMesh : null;
            原始材质 = 预览网格 != null ? 预览网格.sharedMaterials : null;
        }
        else if (预览网格 != null)
        {
            预览网格.sharedMesh = 源网格.sharedMesh;
            预览网格.sharedMaterials = 源网格.sharedMaterials;
        }

        应用偏航();
    }

    /// <summary>
    /// 预览模型**绝不能是"活 NPC"**。
    ///
    /// 外观资产取出来的模型是**完整的 NPC 预制体**（`村民·成男` / `npc_cunmin_chengnan`），
    /// 身上带着 `NpcInstance` / `NpcIndicator`（头顶名牌）/ `NpcDialogue` /
    /// `StationInteractable` / `NpcAiHuman`（自己会走路）。
    /// 不摘掉的话，这个"预览"就是一个真 NPC：顶名牌、满地走、能被右键锁定、
    /// 任务系统也可能把它当成真的 `npc_cunmin_chengnan`（用户报的 bug 见踩坑 B23/B33）。
    ///
    /// ⚠️ 用 `DestroyImmediate` 而不是 `Destroy`，并且**依赖方先删、`NpcInstance` 最后删**：
    /// `NpcIndicator` 之类是 `[RequireComponent(typeof(NpcInstance))]`，
    /// 而 `Destroy` 要延迟到帧末才真的删掉，依赖还在时 Unity 会直接拒绝：
    /// `Can't remove NpcInstance (Script) because NpcIndicator (Script) depends on it`
    /// （实测报过这条 Error）。
    /// `NpcAnimator` 要留着，否则预览里的人会僵住不动。
    /// </summary>
    static void 去掉会干活的组件(GameObject go)
    {
        var 依赖方 = new System.Collections.Generic.List<MonoBehaviour>();
        MonoBehaviour 本体 = null;
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c == null) continue;
            var n = c.GetType().Name;
            if (n == "NpcAnimator") continue;                 // ← 留着播待机动作
            if (n == "NpcInstance") { 本体 = c; continue; }
            if (n == "NpcIndicator" || n == "NpcDialogue"
                || n == "StationInteractable" || n.StartsWith("NpcAi"))
                依赖方.Add(c);
        }

        // 先全部停掉：各自的 OnDisable 清理逻辑跑的时候，别的组件都还活着
        foreach (var c in 依赖方) c.enabled = false;
        if (本体 != null) 本体.enabled = false;

        foreach (var c in 依赖方) DestroyImmediate(c);
        if (本体 != null) DestroyImmediate(本体);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
    }

    /// <summary>拖动：**只用横向位移**（竖向一律忽略 —— 用户要求不能上下转）</summary>
    public void OnDrag(PointerEventData e)
    {
        当前偏航 += e.delta.x * 灵敏度;
        应用偏航();
    }

    public void OnPointerDown(PointerEventData e) { /* 只是为了让 OnDrag 生效 */ }

    void LateUpdate()
    {
        if (模型根 == null) return;
        if (松手回正 && Mathf.Abs(Mathf.DeltaAngle(当前偏航, 初始偏航)) > 0.05f)
        {
            当前偏航 = Mathf.MoveTowardsAngle(当前偏航, 初始偏航, 40f * Time.unscaledDeltaTime);
            应用偏航();
        }
    }

    void 应用偏航()
    {
        if (模型根 == null) return;
        // ★ 欧拉角第二项恒为 0：模型永远不俯仰，翻不过来
        模型根.localRotation = Quaternion.Euler(0f, 当前偏航, 0f);
    }

    // ---- ASCII 别名 ----
    public float Yaw => 当前偏航;
    public void Show(AppearanceDefinition a) => 显示(a);
}
