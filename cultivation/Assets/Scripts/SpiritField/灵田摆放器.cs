using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **灵田摆放器** —— 用户要求的那套"鼠标变成一块半透明的灵田、随便找个地方摆下来"。
///
/// 需求原文（2026-10-01）：
/// > 「在背包中找到灵田开拓令，假如是在洞府中使用的灵田开拓令（**不在洞府场景无法使用开拓令**），
/// >   **取消面板**，鼠标会变成一块待摆放的**半透明的灵田**，
/// >   可以在整个洞府中有地面的有空间的地方任意的摆放。
/// >   摆放中的灵田可以**旋转 8 个朝向**，摆下来后**它以及它周围的一部分空间会被标记为不可摆放东西**。」
///
/// 还有吸附（同一次对话里补的）：
/// > 「当你在摆放灵田时，如果有**两个边相隔比较近**的、又是**同一个朝向**的同种物品，可以**自动吸附对齐**。」
///
/// ## 操作
///
/// | 键 | 作用 |
/// |---|---|
/// | 移动鼠标 | 预览跟着在地面上滑（**自动吸附**到附近同朝向的田块旁边） |
/// | **R** | 旋转一档（8 档，45°） |
/// | **左键** | 放下（<u>开新地</u>时才扣开拓令；<u>挪动</u>不扣） |
/// | **右键 / ESC** | 取消（开新地会把开拓令**还回去**） |
///
/// ## 为什么"取消要把道具还回去"
///
/// 道具是在**背包里点「使用」那一刻**就被框架扣掉的（`物品使用器.使用()` 的流程：
/// 效果返回 true → 扣一件）。而"进入摆放态"和"真的摆下去"是两件事 ——
/// 玩家完全可能进去看一眼再取消。所以 <see cref="取消"/> 里必须**把开拓令还回去**，
/// 否则点一下使用就白亏一张。
///
/// ## "取消面板"是什么意思
///
/// 用户要的是**别弹配置面板**，直接进摆放态。所以这里**没有**面板，
/// 只有屏幕底部一条**极简的按键提示**（不点它、不挡操作），
/// 以及一行"为什么这里不能放"的原因 —— 不然用户看着红色的预览完全不知道为什么。
/// </summary>
[DisallowMultipleComponent]
public class 灵田摆放器 : MonoBehaviour
{
    // ============================================================ 单例

    static 灵田摆放器 实例;
    public static 灵田摆放器 取()
    {
        if (实例 != null) return 实例;
        实例 = FindObjectOfType<灵田摆放器>();
        if (实例 != null) return 实例;
        var go = new GameObject("灵田摆放器");
        DontDestroyOnLoad(go);
        实例 = go.AddComponent<灵田摆放器>();
        return 实例;
    }

    /// <summary>现在是不是在摆放态（别的地方要判断：摆放时不要响应别的交互）</summary>
    public static bool 正在摆放 => 实例 != null && 实例.摆放中;

    enum 干嘛 { 无, 开新地, 挪动 }

    干嘛 当前;
    int 挪动哪块 = -1;
    int 朝向档;
    GameObject 预览;
    /// <summary>进摆放态时开拓令**是不是已经被扣了** —— 决定取消时要不要还回去</summary>
    bool 道具已扣过;
    string 当前原因 = "";
    bool 当前可以;

    Canvas 提示画布;
    Text 提示文字;

    bool 摆放中 => 当前 != 干嘛.无;

    // ============================================================ 进入 / 退出

    /// <summary>
    /// 从背包里用「灵田开拓令」→ 进摆放态开一块新地。
    /// 返回 false = 现在不能摆（不在洞府等），调用方据此拒绝使用道具。
    /// </summary>
    /// <param name="道具已扣">
    /// 道具是不是**已经被扣掉了**。正常从背包走 `物品使用器` 时会扣（默认 true），
    /// 取消时就要还回去；如果是谁直接调进来调试（没扣），传 false，
    /// 否则取消一次就白送一张开拓令。
    /// </param>
    public static bool 开始开新地(out string 原因, bool 道具已扣 = true)
    {
        原因 = "";
        var 田 = 灵田.取();
        if (田 == null) { 原因 = "找不到灵田系统"; return false; }
        if (!田.当前场景可摆放)
        {
            原因 = "灵田只能在个人洞府里开垦";
            return false;
        }

        var 自 = 取();
        自.当前 = 干嘛.开新地;
        自.挪动哪块 = -1;
        自.道具已扣过 = 道具已扣;
        自.建预览();
        自.收起角色面板();
        Debug.Log("[灵田] 进入摆放态（开新地）：R 旋转 / 左键放下 / 右键取消");
        return true;
    }

    /// <summary>把第 i 块地拿起来重新摆（用户选的是"能挪位置，但不退道具也不多花"）</summary>
    public static bool 开始挪动(int 编号, out string 原因)
    {
        原因 = "";
        var 田 = 灵田.取();
        if (田 == null || 田.状态(编号) == null) { 原因 = "没有这块地"; return false; }

        var 自 = 取();
        自.当前 = 干嘛.挪动;
        自.挪动哪块 = 编号;
        自.道具已扣过 = false;             // 挪动本来就不花道具
        自.朝向档 = 田.状态(编号).朝向档;
        自.建预览();
        自.收起角色面板();
        Debug.Log("[灵田] 进入摆放态（挪动第 " + (编号 + 1) + " 块地）");
        return true;
    }

    /// <summary>
    /// **把角色/背包面板收起来** —— 用户说的"**取消面板**，鼠标会变成一块待摆放的半透明的灵田"。
    ///
    /// 【为什么必须做两件事】背包面板（`CharacterPanelUI`）有两个属性会让摆放没法进行：
    ///   · `pauseGameWhenOpen = true` —— 面板开着时**游戏是暂停的**（timeScale = 0）；
    ///   · 它是全屏面板，会**盖住整个画面**，那块半透明预览根本看不见。
    /// 所以进摆放态第一件事就是 `SetOpen(false)`（它内部会 `RestoreTimeScale()`）。
    /// </summary>
    void 收起角色面板()
    {
        var 面板 = FindObjectOfType<CharacterPanelUI>();
        if (面板 == null || !面板.IsOpen) return;
        面板.SetOpen(false, true);
        Debug.Log("[灵田] 已收起角色面板，开始摆放");
    }

    /// <summary>取消摆放。**只有真的扣过开拓令**才会退回去</summary>
    public void 取消()
    {
        if (!摆放中) return;
        bool 要退 = 当前 == 干嘛.开新地 && 道具已扣过;
        收尾();
        if (要退)
        {
            灵田.给道具(灵田.开拓令id, 1);
            Debug.Log("[灵田] 取消摆放，开拓令已退回背包");
        }
    }

    void 收尾()
    {
        当前 = 干嘛.无;
        挪动哪块 = -1;
        道具已扣过 = false;
        if (预览 != null) Destroy(预览);
        预览 = null;
        if (提示画布 != null) 提示画布.gameObject.SetActive(false);
    }

    // ============================================================ 每帧

    void Update()
    {
        if (!摆放中) return;

        var 田 = 灵田.取();
        if (田 == null) { 收尾(); return; }

        // 场景切走了（比如读档/传送）→ 直接收摊，别留一个孤儿预览
        if (!田.当前场景可摆放) { 取消(); return; }

        // ---- 旋转：8 档 ----
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E))
            朝向档 = (朝向档 + 1) % 灵田规格.朝向档数;
        if (Input.GetKeyDown(KeyCode.Q))
            朝向档 = (朝向档 + 灵田规格.朝向档数 - 1) % 灵田规格.朝向档数;

        // ---- 取消 ----
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { 取消(); return; }

        // ---- 鼠标 → 地面 ----
        var 相 = Camera.main;
        if (相 == null) return;
        var 射 = 相.ScreenPointToRay(Input.mousePosition);
        RaycastHit 地;
        if (!Physics.Raycast(射, out 地, 200f, ~0, QueryTriggerInteraction.Ignore)
            || 地.collider is CharacterController)
        {
            当前可以 = false;
            当前原因 = "把鼠标移到地面上";
            摆预览(预览 != null ? 预览.transform.position : transform.position);
            刷提示();
            return;
        }

        var 位 = 地.point;

        // ---- 吸附：附近有**同朝向**的田块时，把位置吸到"边对齐"处 ----
        var 吸到 = 吸一下(田, new Vector2(位.x, 位.z), out bool 吸了);
        if (吸了)
        {
            位.x = 吸到.x;
            位.z = 吸到.y;
            // 吸过去之后地面高度可能不一样，按新的 XZ 重新探一次地
            RaycastHit 地2;
            if (Physics.Raycast(new Vector3(位.x, 地.point.y + 3f, 位.z), Vector3.down, out 地2, 8f,
                                ~0, QueryTriggerInteraction.Ignore))
                位.y = 地2.point.y;
        }

        // ---- 合法吗 ----
        当前可以 = 田.位置可用(位, 朝向档, out 当前原因, 挪动哪块);
        摆预览(位);
        刷提示();
    }

    /// <summary>找最近的吸附点。只有**同朝向**的田块才参与（用户要求）</summary>
    Vector2 吸一下(灵田 田, Vector2 想放, out bool 吸了)
    {
        吸了 = false;
        Vector2 最好 = 想放;
        float 最近 = 灵田规格.吸附距离;

        var 候选 = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i < 田.总块数; i++)
        {
            if (i == 挪动哪块) continue;
            var b = 田.状态(i);
            if (b == null || b.朝向档 != 朝向档) continue;      // ★ 只吸附同朝向

            var 已 = new Vector2(b.位置.x, b.位置.z);
            灵田规格.吸附候选(已, b.朝向档, 想放, 候选);
            foreach (var c in 候选)
            {
                float d = Vector2.Distance(c, 想放);
                if (d < 最近) { 最近 = d; 最好 = c; 吸了 = true; }
            }
        }
        return 最好;
    }

    /// <summary>造出那块半透明的"幽灵灵田"（已经有一个就复用）</summary>
    void 建预览()
    {
        if (预览 == null)
        {
            // 【不挂成任何东西的子物体】挂了就会跟着父物体的缩放/旋转走；
            // 而且它是**当前场景**里的临时物件，切场景时自然销毁 —— 摆放态本来也会被取消。
            预览 = 灵田地块外观.造幽灵(null);
        }
        预览.SetActive(true);
        灵田地块外观.幽灵上色(预览, 当前可以);
    }

    void 摆预览(Vector3 位)
    {
        if (预览 == null) return;
        预览.transform.position = 位;
        预览.transform.rotation = Quaternion.Euler(0f, 灵田规格.档转角度(朝向档), 0f);
        灵田地块外观.幽灵上色(预览, 当前可以);
    }

    // ============================================================ 放下

    void LateUpdate()
    {
        if (!摆放中) return;
        if (!Input.GetMouseButtonDown(0)) return;

        // 鼠标压在 UI 上时不当成"放下"（比如玩家去点了背包）
        if (UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        试着放下();
    }

    /// <summary>
    /// 把预览**落在它现在的位置**上。返回 true = 放成功（摆放态已收尾、预览已销毁）。
    ///
    /// 【为什么单独抽出来】鼠标点击那条路（`LateUpdate` 里的 `GetMouseButtonDown`）
    /// 在自动化里按不出来，所以"放下之后幽灵到底有没有被销毁"这件事一直没被验证过 ——
    /// 而用户恰好反馈了「放下去之后怎么还是透明的」。抽成公开方法之后，
    /// 那条路除了"按下左键"这一行，其余全都测得到。
    /// </summary>
    public bool 试着放下(Vector3? 指定位置 = null)
    {
        if (!摆放中) return false;

        // 【为什么要能指定位置】鼠标点击那条路（LateUpdate 里的 GetMouseButtonDown）在自动化里
        // 按不出来，而 Update 每帧又**用鼠标射线重算预览位置** —— 从外面把预览挪过去再放下
        // 是没用的（下一帧就被覆盖回去）。传位置就能直接验证
        // 「放下之后幽灵到底有没有被销毁」，那正是用户反馈「放下去之后还是透明的」那条路。
        // 以后要做"点按钮确认摆放"之类的入口也能复用它。
        bool 用指定 = 指定位置.HasValue;
        if (!用指定 && 预览 == null) return false;

        var 位 = 用指定 ? 指定位置.Value : 预览.transform.position;

        if (!用指定 && !当前可以)
        {
            Debug.LogWarning("[灵田] 放不下：" + 当前原因);
            return false;
        }

        var 田 = 灵田.取();
        if (田 == null) return false;

        if (当前 == 干嘛.开新地)
        {
            if (!田.位置可用(位, 朝向档, out string 原因))
            {
                Debug.LogWarning("[灵田] 放不下：" + 原因);
                return false;
            }
            田.放置(位, 朝向档);
            收尾();                       // 道具在使用那一刻已经扣过了
            return true;
        }

        if (当前 == 干嘛.挪动)
        {
            if (!田.挪动(挪动哪块, 位, 朝向档)) return false;
            收尾();
            return true;
        }
        return false;
    }

    // ============================================================ 底部提示（唯一的 UI）

    void 建提示()
    {
        if (提示画布 != null) return;

        var go = new GameObject("摆放提示", typeof(Canvas), typeof(CanvasScaler));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1900;                 // 高于 HUD，低于各种面板
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;

        var 底 = new GameObject("底", typeof(RectTransform));
        底.transform.SetParent(go.transform, false);
        var 图 = 底.AddComponent<Image>();
        图.color = new Color(0.06f, 0.07f, 0.09f, 0.86f);
        图.raycastTarget = false;
        var rt = 图.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(900f, 76f);

        var t = new GameObject("字", typeof(RectTransform));
        t.transform.SetParent(底.transform, false);
        提示文字 = t.AddComponent<Text>();
        提示文字.font = 灵田地块牌_取字体();
        提示文字.fontSize = 22;
        提示文字.alignment = TextAnchor.MiddleCenter;
        提示文字.color = new Color(0.95f, 0.93f, 0.86f);
        提示文字.raycastTarget = false;
        提示文字.horizontalOverflow = HorizontalWrapMode.Overflow;
        var trt = 提示文字.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10f, 0f); trt.offsetMax = new Vector2(-10f, 0f);

        提示画布 = c;
        go.SetActive(false);
    }

    void 刷提示()
    {
        建提示();
        if (提示画布 == null) return;
        if (!提示画布.gameObject.activeSelf) 提示画布.gameObject.SetActive(true);

        string 第一行 = 当前 == 干嘛.挪动
            ? "挪动第 " + (挪动哪块 + 1) + " 块地"
            : "开垦新灵田";
        提示文字.text = 第一行 + "　[R] 旋转（第 " + (朝向档 + 1) + "/8 朝向）　[左键] 放下　[右键/ESC] 取消\n"
                      + (当前可以 ? "<color=#8CE08C>这里可以放</color>" : "<color=#FF8080>放不了：" + 当前原因 + "</color>");
        提示文字.supportRichText = true;
    }

    /// <summary>借用地块牌的字体加载（同一个 SimHei，别在这里再抄一遍）</summary>
    static Font 灵田地块牌_取字体()
    {
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
        if (f != null) return f;
#endif
        foreach (var x in Resources.FindObjectsOfTypeAll<Font>())
            if (x != null && x.name.ToLowerInvariant().Contains("simhei")) return x;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
