using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 设施交互（挂在玩家身上）。
///
/// 做三件事：
///   1. 每帧找【离玩家最近、且在交互范围内】的那台设施
///   2. 靠近时在物件头顶显示「右键打开XX」的提示
///   3. 右键 → 打开对应界面（UI 还没设计，先用空白幕布占位）
///
/// ============================================================
/// 和锁定 NPC 的右键不冲突
/// ============================================================
///   NpcTargeting 也用右键（鼠标右键锁定敌人）。
///   所以这里提供了 本次右键已被占用 这个静态标记：
///   站在设施旁边按右键时，NpcTargeting 会跳过这一帧的锁定，
///   免得"想开炼丹炉结果锁了个敌人"。
///   （NpcTargeting 那边已经加了一行检查）
///
/// ============================================================
/// 空白幕布
/// ============================================================
///   真正的 UI 还没做，所以这里临时生成一张半透明黑色全屏幕布 + 标题 + 关闭按钮。
///   等美术/布局定下来之后：
///     · 把做好的界面挂到 StationInteractable.界面预制体 上
///     · 这里会自动优先用那个预制体，不再用占位幕布
/// </summary>
[DisallowMultipleComponent]
public class StationInteractor : MonoBehaviour
{
    [Header("按键")]
    [Tooltip("交互键。★ 2026-09-28 起全项目统一 F（用户要求：建筑不再用右键，和 NPC 一样按 F）")]
    public KeyCode 交互键 = KeyCode.F;
    public KeyCode 关闭键 = KeyCode.Escape;

    [Header("字体")]
    [Tooltip("中文字体。留空中文会变成方块 —— 用 Cultivation/设施/安装交互 会自动填")]
    public Font 字体;

    [Header("占位幕布外观")]
    public Color 幕布底色 = new Color(0.05f, 0.06f, 0.09f, 0.92f);
    public Color 标题色 = new Color(0.92f, 0.90f, 0.82f);
    public Color 提示色 = new Color(1f, 0.95f, 0.6f, 1f);

    [Header("提示")]
    [Tooltip("靠近提示相对物件头顶再往上多少")]
    public float 提示抬高 = 0.6f;

    [Tooltip("★ 勾上（默认）：**非对话**设施（建筑 / 传送圈 / 炼丹炉…）的提示**挂在玩家身上** ——\n" +
             "水平 = 玩家位置 + 朝设施方向偏 `提示朝设施偏移`，高度 = 玩家 + `提示玩家抬高`。\n\n" +
             "**为什么（用户 2026-09-29）**：原来放在**物件包围盒顶部**（再按 `提示最高点` 封顶 3m），\n" +
             "宗门那些 12~19 米的楼会把这个 3 米高的提示**埋进楼体内部**，从外面完全看不见。\n" +
             "用户原话：「你生成的位置被建筑本身的模型盖住了 —— 以后这个互动提示生成在玩家的高度。」\n\n" +
             "挂玩家身上就永远不会被建筑挡住（相机本来就看着玩家），也不受楼高影响。\n" +
             "关掉它就退回老逻辑（物件顶部 + 提示最高点封顶）。")]
    public bool 提示挂玩家 = true;

    [Tooltip("挂玩家身上时，提示比**玩家脚底**高多少米（默认 2.1 ≈ 头顶稍上）")]
    public float 提示玩家抬高 = 2.1f;

    [Tooltip("挂玩家身上时，再朝「设施方向」水平偏出去多少米（0 = 就在玩家正上方）。\n" +
             "偏一点能让提示看起来是「朝着那台设施」的；**别给太大**，否则又可能钻进建筑里")]
    public float 提示朝设施偏移 = 0.5f;

    /// <summary>这一帧的右键是不是被设施吃掉了（给 NpcTargeting 用，兜底）</summary>
    public static bool 本次右键已被占用 { get; private set; }

    /// <summary>
    /// 当前有没有设施界面开着。
    /// 【暂停菜单】和【NPC 锁定】都要看它：
    ///   · 暂停菜单：界面开着时 ESC 应该是"关界面"，不该弹出暂停菜单
    ///   · NPC 锁定：界面开着时鼠标不该穿透到场景里去点建筑/锁敌人
    /// </summary>
    public static bool 有界面打开 { get; private set; }

    /// <summary>
    /// 最近一次关闭界面的时刻（unscaledTime）。
    ///
    /// 【为什么需要】关界面的键和暂停菜单的键都是 ESC，而且两个脚本的 Update
    /// 执行顺序不定。如果 StationInteractor 先跑：它关掉界面、把 有界面打开 置 false，
    /// 紧接着 PauseMenuUI 跑，看到 有界面打开 == false 就顺手把暂停菜单弹出来了 ——
    /// 一次 ESC 干了三件事。
    /// 记一个时间戳，让暂停菜单知道"刚刚才关过界面"，这一下 ESC 不该再响应。
    /// 用 unscaledTime 是因为暂停菜单会把 timeScale 设成 0。
    /// </summary>
    public static float 上次关闭界面时间 { get; private set; } = -99f;

    /// <summary>当前打开着的界面（null = 没开）</summary>
    public GameObject 当前界面 { get; private set; }
    public StationInteractable 当前设施 { get; private set; }
    public bool 界面已打开 => 当前界面 != null;

    readonly List<StationInteractable> 已知设施 = new List<StationInteractable>();
    float 重扫计时;
    float 关闭冷却;      // 关界面后短暂屏蔽右键，防止同一串点击立刻又开一个
    StationInteractable 最近设施;
    Camera 主相机;

    // 占位幕布
    GameObject 幕布根;
    Text 幕布标题;
    Text 幕布副标题;

    // 头顶提示
    Text 提示文字;
    Text 提示键字;            // ★ 键帽上的字（F / 右键）—— 2026-09-26 合并建筑与 NPC 交互时加的
    GameObject 提示根;
    RectTransform 提示框;

    // ★ 2026-09-26 用户要求：NPC 的**名字单独放头顶**，而「F 对话」小框挪到 **NPC 身侧**（建筑保持原样）
    Text 名字文字;
    GameObject 名字根;

    [Tooltip("NPC 的「F 对话」小框离身体多远（米，沿屏幕右方向量）")]
    public float 身侧距离 = 0.55f;

    void Awake()
    {
        主相机 = Camera.main;
    }

    void Update()
    {
        // 静态标记只在一帧内有效，每帧开头先清掉
        本次右键已被占用 = false;
        if (关闭冷却 > 0f) 关闭冷却 -= Time.deltaTime;

        // 定期重扫（物件可能在运行时被生成/销毁）
        重扫计时 += Time.deltaTime;
        if (重扫计时 >= 1f)
        {
            重扫计时 = 0f;
            重扫();
        }

        if (界面已打开)
        {
            隐藏提示();
            // ★ 面板被**它自己**（或别处）收掉了 → 把"界面开着"的标记一起收回来。
            //
            //   踩过的坑（2026-10-02，镇妖塔清完怪的那个传送圈）：
            //   圈上的面板是 `Teleporter` 自己造的、**独立根对象**，
            //   塔在"10 秒后重刷本层"时会 `关面板()` 把面板 `SetActive(false)`。
            //   面板对象还在（不是 null），于是 `界面已打开` 一直为真 →
            //   玩家按 F / ESC 全被吞掉、提示也不显示，像卡住了一样。
            //   `activeInHierarchy`（连父级一起看）为假 = 这块面板已经不在画面上，
            //   就该当成"界面关了"。
            if (当前界面 == null || !当前界面.activeInHierarchy)
            {
                当前界面 = null;
                有界面打开 = false;
                当前设施 = null;
                return;
            }

            // 界面开着：只处理关闭，不做任何别的交互
            if (Input.GetKeyDown(关闭键) || Input.GetKeyDown(交互键)) { 关闭界面(); return; }

            // ★ 2026-09-28 用户要求：**走出交互范围就自动关**。
            //
            //   为什么放在这里、而不是靠触发区 OnTriggerExit：
            //   本组件本来就是**按距离**判定"在不在范围内"的（不用物理触发器），
            //   所以"离开"这件事在这里判最自然、也只有一处判定标准。
            //
            //   ⚠️ 别把这个逻辑下放到各自的界面里（传送面板、占位幕布…）——
            //      那样每个界面都要自己抄一遍距离判定，必然出现"有的界面关有的不关"。
            if (当前设施 != null && !在范围内(当前设施))
            {
                关闭界面();
                return;
            }
            return;
        }

        // ★ 对话框开着的时候：提示和交互都让开（对话框自己管开关，别再抢 F）
        //   同时把「有界面打开」这个静态标记同步上，右键锁敌 / ESC 协调器照旧能知道有界面
        if (DialogueUI.正在显示)
        {
            if (!有界面打开) 有界面打开 = true;
            隐藏提示();
            return;
        }
        // 对话框关掉了：把标记收回来（建筑界面还开着的话不动它）
        if (有界面打开 && 当前界面 == null) 有界面打开 = false;
        if(UiEscRegistry.SceneInputBlocked){隐藏提示();return;}

        // 刚关掉界面的一小段时间内不再响应，否则"关掉的那一下"会顺手又开一个
        if (关闭冷却 > 0f) { 隐藏提示(); return; }

        找最近设施();
        更新头顶提示();

        if (最近设施 != null && Input.GetKeyDown(最近设施.取按键(交互键)))
        {
            本次右键已被占用 = true;          // 告诉 NpcTargeting：这帧别锁 NPC
            打开界面(最近设施);
        }
    }

    // ================================================================
    // 找设施
    // ================================================================

    void 重扫()
    {
        已知设施.Clear();
        foreach (var s in FindObjectsOfType<StationInteractable>())
            if (s != null && s.isActiveAndEnabled) 已知设施.Add(s);
    }

    /// <summary>
    /// 玩家现在还在不在这台设施的交互范围内。
    ///
    /// **本项目"在不在范围内"只有这一个判定**（`找最近设施` 和"离开自动关界面"共用它），
    /// 免得两处各写一套阈值、出现"提示没了但界面还开着"这种半开状态。
    /// </summary>
    bool 在范围内(StationInteractable 设施)
    {
        if (设施 == null) return false;
        var 差 = 设施.transform.position - transform.position;
        float 水平 = new Vector2(差.x, 差.z).magnitude;
        return 设施.玩家在范围内(水平, 差.y);
    }

    void 找最近设施()
    {
        最近设施 = null;
        float 最近 = float.MaxValue;
        var 我 = transform.position;

        foreach (var s in 已知设施)
        {
            if (s == null) continue;
            // ★ 也要看"现在是不是还活着"：重扫是 **1 秒一次**，
            //   刚刚被隐藏的设施（例：镇妖塔重刷本层时收掉的通关传送点）
            //   在这一秒内还在缓存里，不挡掉的话按 F 还能把它那个已经收掉的面板再打开。
            if (!s.isActiveAndEnabled) continue;
            if (!在范围内(s)) continue;
            // ★ 用户 2026-09-26：同时进范围时取「**离根节点更近**」的那个（原来比的是水平距离）
            float 直距 = (s.transform.position - 我).magnitude;
            if (直距 < 最近) { 最近 = 直距; 最近设施 = s; }
        }
    }

    // ================================================================
    // 头顶提示
    // ================================================================

    void 更新头顶提示()
    {
        bool 要显示 = 最近设施 != null && 最近设施.显示靠近提示;
        if (!要显示) { 隐藏提示(); return; }

        确保提示存在();
        if (提示根 == null) return;

        提示根.SetActive(true);
        Vector3 提示世界点;
        if (提示键字 != null) 提示键字.text = StationInteractable.按键名(最近设施.取按键(交互键));

        var 位 = 最近设施.transform.position;
        float 顶 = 算提示高度(最近设施);

        if (主相机 == null) 主相机 = Camera.main;

        // ★ 用户要的排版（2026-09-26）：
        //   NPC → **头顶只有名字**，另外在**身侧**放一个「F 对话」小框；
        //   建筑 → 保持老样子，头顶一个「F 炼丹」。
        bool 是对话 = 最近设施.类型 == StationInteractable.StationKind.对话;

        if (是对话)
        {
            确保名字存在();
            if (名字根 != null)
            {
                名字根.SetActive(true);
                if (名字文字 != null) 名字文字.text = 最近设施.标题;
                名字根.transform.position = 位 + Vector3.up * 顶;
                if (主相机 != null)
                    名字根.transform.rotation = Quaternion.LookRotation(
                        名字根.transform.position - 主相机.transform.position, 主相机.transform.up);
            }

            // 小框里写动作名（"对话"），不再写 NPC 名字 —— 名字已经在头顶了
            提示文字.text = StationInteractable.默认名(最近设施.类型);

            // 身侧位置：沿「屏幕右方向」在世界里横着挪一点，高度取肩/胸之间
            Vector3 侧 = 主相机 != null ? 主相机.transform.right : Vector3.right;
            侧.y = 0f;
            if (侧.sqrMagnitude < 0.0001f) 侧 = Vector3.right;
            侧.Normalize();
            float 肩高 = Mathf.Clamp(顶 * 0.62f, 1.2f, 2.2f);
            提示世界点 = 位 + 侧 * 身侧距离 + Vector3.up * 肩高;
        }
        else
        {
            if (名字根 != null && 名字根.activeSelf) 名字根.SetActive(false);
            提示文字.text = 最近设施.标题;

            if (提示挂玩家)
            {
                // ★ 用户 2026-09-29：**挂在玩家身上**，不再挂物件顶部 ——
                //   原来放在物件包围盒顶（再封顶 3m），宗门那些 15 米的楼会把提示埋进楼体里。
                //   水平 = 玩家 + 朝设施方向偏一点；高度 = 玩家的高度。
                var 朝 = 位 - transform.position;
                朝.y = 0f;
                if (朝.sqrMagnitude > 0.0001f) 朝.Normalize(); else 朝 = Vector3.zero;
                提示世界点 = transform.position
                                          + 朝 * Mathf.Max(0f, 提示朝设施偏移)
                                          + Vector3.up * 提示玩家抬高;
            }
            else
            {
                提示世界点 = 位 + Vector3.up * 顶;
            }
        }

        提示框.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Clamp(提示文字.preferredWidth + 112f, 180f, 420f));
        // 仍跟随原世界锚点，但在屏幕叠加层绘制，避免场景后处理冲淡字色。
        if (主相机 == null) { 隐藏提示(); return; }
        var screen = 主相机.WorldToScreenPoint(提示世界点);
        if (screen.z <= .05f) { 隐藏提示(); return; }
        float margin = Screen.height * 屏幕边距比例;
        float scale = 提示根.GetComponent<Canvas>().scaleFactor;
        screen.x = Mathf.Clamp(screen.x, margin, Mathf.Max(margin, Screen.width - margin - 提示框.rect.width * scale));
        screen.y = Mathf.Clamp(screen.y, margin + 提示框.rect.height * scale * .5f, Screen.height - margin - 提示框.rect.height * scale * .5f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)提示根.transform,screen,null,out var point);
        提示框.anchoredPosition = point;
        if (名字根 != null && 名字根.activeSelf) 夹进屏幕(名字根.transform);

        // 根据距离淡一点，远了不明显
        float 近 = Vector2.Distance(new Vector2(位.x, 位.z), new Vector2(transform.position.x, transform.position.z));
        float a = Mathf.Clamp01(1.4f - 近 / Mathf.Max(0.1f, 最近设施.交互距离));
        提示文字.color = new Color(.98f, .96f, .90f, Mathf.Clamp(a + .25f,.85f,1f));
        if (名字文字 != null) 名字文字.color = new Color(名字色.r, 名字色.g, 名字色.b, Mathf.Clamp01(a + 0.35f));
    }

    void LateUpdate()
    {
        // 对话、传送或其他界面也可能在本帧由外部脚本打开。
        if(UiEscRegistry.SceneInputBlocked)隐藏提示();
    }

    void 隐藏提示()
    {
        if (提示根 != null && 提示根.activeSelf) 提示根.SetActive(false);
        if (名字根 != null && 名字根.activeSelf) 名字根.SetActive(false);
    }

    /// <summary>
    /// 提示该摆在物件上方多高。
    ///
    /// ★ 2026-09-28 用户报「建筑比较高的话提示就看不到了」：
    ///   原来是**直接取渲染体包围盒的顶部**，而宗门那些楼有 12~19 米高
    ///   （实测：炼丹阁顶 y=18.70、宗门大殿顶 y=26.89）→ 提示被摆到十几米高空，
    ///   相机是俯视角、视野里根本没有那个高度 ✗。
    ///
    ///   现在**封顶**：`StationInteractable.提示最高点`（默认 3 米，0 = 不限）。
    ///   矮物件（NPC、炼丹炉、传送圈）本来就在 3 米以内，行为不变；
    ///   高楼则统一把提示压在 3 米高处。
    ///
    ///   ⚠️ 别改成"取物件中心和顶部之间" —— 15 米高的楼取一半也有 7.5 米，照样在画面外。
    ///      封顶是这里唯一稳的做法；另外 `更新头顶提示` 末尾还有一道屏幕内收兜底。
    /// </summary>
    float 算提示高度(StationInteractable 设施)
    {
        float 顶 = 1.8f + 设施.提示抬高;
        var 渲染器 = 设施.GetComponentsInChildren<Renderer>();
        if (渲染器.Length > 0)
        {
            var b = 渲染器[0].bounds;
            for (int i = 1; i < 渲染器.Length; i++) b.Encapsulate(渲染器[i].bounds);
            顶 = (b.max.y - 设施.transform.position.y) + 设施.提示抬高;
        }

        if (设施.提示最高点 > 0.01f) 顶 = Mathf.Min(顶, 设施.提示最高点);
        return Mathf.Max(0.4f, 顶);
    }

    /// <summary>把世界空间的一个提示拉回相机安全视野内（世界空间 Canvas，所以用 WorldToScreenPoint 再转回去）</summary>
    void 夹进屏幕(Transform 谁)
    {
        if (谁 == null || 主相机 == null) return;

        var 屏幕 = 主相机.WorldToScreenPoint(谁.position);
        if (屏幕.z <= 0.05f) return;                     // 在相机背后，别乱拉

        float 边 = Screen.height * 屏幕边距比例;
        float x = Mathf.Clamp(屏幕.x, 边, Screen.width - 边);
        float y = Mathf.Clamp(屏幕.y, 边, Screen.height - 边);
        if (Mathf.Approximately(x, 屏幕.x) && Mathf.Approximately(y, 屏幕.y)) return;

        var 新 = 主相机.ScreenToWorldPoint(new Vector3(x, y, 屏幕.z));
        谁.position = new Vector3(新.x, 新.y, 谁.position.z);
    }

    [Tooltip("★ 提示的屏幕安全边距（占屏幕高度的比例）。摆到画面外时会被拉回这个范围")]
    [Range(0.02f, 0.3f)] public float 屏幕边距比例 = 0.08f;

    // 头顶名字：不带框，白字 + 深色描边（亮背景上也看得清）
    static readonly Color 名字色 = new Color(1f, 0.98f, 0.92f, 1f);

    void 确保名字存在()
    {
        if (名字根 != null) return;

        名字根 = new GameObject("StationName", typeof(Canvas));
        var canvas = 名字根.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 199;               // 比「F 对话」小框(200)低一档
        var rt = 名字根.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 64f);
        rt.localScale = Vector3.one * 0.011f;

        var go = new GameObject("名字", typeof(RectTransform));
        go.transform.SetParent(名字根.transform, false);
        名字文字 = go.AddComponent<Text>();
        名字文字.font = 取字体();
        名字文字.fontSize = 40;
        名字文字.fontStyle = FontStyle.Bold;
        名字文字.alignment = TextAnchor.MiddleCenter;
        名字文字.color = 名字色;
        名字文字.raycastTarget = false;
        名字文字.horizontalOverflow = HorizontalWrapMode.Overflow;
        名字文字.verticalOverflow = VerticalWrapMode.Overflow;
        拉伸(go.GetComponent<RectTransform>());

        var 描 = go.AddComponent<Outline>();
        描.effectColor = new Color(0f, 0f, 0f, 0.85f);
        描.effectDistance = new Vector2(2f, -2f);
    }

    void 确保提示存在()
    {
        if (提示根 != null) return;

        提示根 = new GameObject("StationHint", typeof(Canvas));
        var canvas = 提示根.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1550;
        var scaler = 提示根.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.matchWidthOrHeight = .5f;
        提示框 = UIBuildUtils.CreateRect("提示框",提示根.transform);
        提示框.anchorMin = 提示框.anchorMax = new Vector2(.5f,.5f);
        提示框.pivot = new Vector2(0,.5f);
        提示框.sizeDelta = new Vector2(310,72);
        var 底图 = UIBuildUtils.CreateImage("底",提示框,new Color(.025f,.035f,.035f,.96f));
        拉伸(底图.rectTransform);
        底图.gameObject.AddComponent<UIInkDialogueBackdrop>();
        var 帽 = UIBuildUtils.CreateRect("键",提示框);
        帽.anchorMin = 帽.anchorMax = new Vector2(0,.5f);
        帽.pivot = new Vector2(0,.5f);
        帽.anchoredPosition = new Vector2(28,0);
        帽.sizeDelta = new Vector2(40,40);
        var k = new GameObject("键字", typeof(RectTransform));
        k.transform.SetParent(帽, false);
        提示键字 = k.AddComponent<Text>();
        提示键字.font = 取字体();
        提示键字.fontSize = 26;
        提示键字.fontStyle = FontStyle.Bold;
        提示键字.alignment = TextAnchor.MiddleCenter;
        提示键字.color = new Color(.96f,.88f,.68f);
        提示键字.raycastTarget = false;
        拉伸(k.GetComponent<RectTransform>());

        // ---- 动作文字：键帽右边 ----
        var t = new GameObject("Text", typeof(RectTransform));
        t.transform.SetParent(提示框, false);
        提示文字 = t.AddComponent<Text>();
        提示文字.font = 取字体();
        提示文字.fontSize = 24;
        提示文字.alignment = TextAnchor.MiddleLeft;
        提示文字.color = new Color(.98f,.96f,.90f);
        提示文字.raycastTarget = false;
        var trt = t.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(80f, 0f); trt.offsetMax = new Vector2(-28f, 0f);
    }

    static void 拉伸(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// <summary>圆角方块 sprite（带 9 宫格 border，缩放不走形）。程序生成一次、所有提示共用</summary>
    static Sprite 圆角精灵;
    static Sprite 取圆角()
    {
        if (圆角精灵 != null) return 圆角精灵;
        const int N = 32; const float r = 10f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (N - r)));
                float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (N - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
        tex.Apply();
        圆角精灵 = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(r + 2f, r + 2f, r + 2f, r + 2f));
        return 圆角精灵;
    }

    // ================================================================
    // 打开 / 关闭界面
    // ================================================================

    public void 打开界面(StationInteractable 设施)
    {
        if (设施 == null || 界面已打开) return;
        隐藏提示();

        // ★ 对话类设施**不走幕布、也不走界面预制体**，交给 NPC 自己的对话模块。
        //   原来这里会给对话类也造一块占位幕布（sortingOrder=2500），而对话框只有 900，
        //   结果幕布永远盖在对话框上面 —— 用户实测就是"有的村民点开是一块空白幕布，
        //   有的村民是幕布和对话叠在一起"。而且 NpcDialogue 也在同一帧响应 F，等于两个系统抢一个键。
        // ★ 传送圈：**面板由 Teleporter 自己画**（它要按选项数量动态生成按钮，
        //   占位幕布满足不了）。所以这里只负责"按 F 这一下"和"提示"。
        //   2026-09-28 用户要求：传送从"走上去自动弹面板"改成和 NPC / 建筑一样按 F。
        if (设施.类型 == StationInteractable.StationKind.传送)
        {
            var 传送 = 设施.GetComponent<Teleporter>();
            if (传送 == null) 传送 = 设施.GetComponentInParent<Teleporter>();
            if (传送 == null)
            {
                Debug.LogWarning($"[StationInteractor] 【{设施.标题}】标了「传送」但物件上没有 Teleporter 组件", 设施);
                return;
            }

            当前设施 = 设施;
            传送.开面板();
            // ★ 一定要把面板根交给本组件：`界面已打开` 是 `当前界面 != null` 算出来的，
            //   而 `Update` 里那行自愈 `if (有界面打开 && 当前界面 == null) 有界面打开 = false;`
            //   会在下一帧把"界面开着"清掉 → ESC 就再也关不掉了（2026-09-28 实测踩到）。
            当前界面 = 传送.面板根;
            有界面打开 = true;
            本次右键已被占用 = true;
            Debug.Log($"[StationInteractor] 打开【{设施.标题}】传送面板", 设施);
            return;
        }

        // ★ 功德堂兑换：和灵田地块一个路子 —— 面板由它自己画（行数取决于上架几件）。
        //   判据是"这台设施上有没有 功德堂兑换 组件"，而不是某个 StationKind：
        //   功德堂在场景里是当「建筑」配的（类型=修炼），加一种 StationKind 反而要动枚举 + 场景数据。
        var 兑换 = 设施.GetComponent<功德堂兑换>();
        if (兑换 == null) 兑换 = 设施.GetComponentInParent<功德堂兑换>();
        if (兑换 != null)
        {
            当前设施 = 设施;
            兑换.开面板();
            当前界面 = 兑换.面板根;
            有界面打开 = true;
            本次右键已被占用 = true;
            Debug.Log($"[StationInteractor] 打开【{设施.标题}】功德堂兑换", 设施);
            return;
        }

        // ★ 灵田地块：**面板由地块自己画**（每块地一个、要带自己的上下文，
        //   占位幕布和共用预制体都表达不了"是哪一块地"）。
        //   和下面传送圈一个路子：这里只负责"按 F 这一下"和把面板根记下来。
        if (设施.类型 == StationInteractable.StationKind.灵田地块)
        {
            var 块 = 设施.GetComponent<灵田地块>();
            if (块 == null) 块 = 设施.GetComponentInParent<灵田地块>();
            if (块 == null)
            {
                Debug.LogWarning($"[StationInteractor] 【{设施.标题}】标了「灵田地块」但物件上没有 灵田地块 组件", 设施);
                return;
            }

            当前设施 = 设施;
            块.开面板();
            // ★ 必须把面板根交给本组件：`界面已打开` 是 `当前界面 != null` 算出来的，
            //   而 Update 里那行自愈 `if (有界面打开 && 当前界面 == null) 有界面打开 = false;`
            //   会在下一帧把"界面开着"清掉 → ESC 就再也关不掉了（2026-09-28 传送面板踩过）。
            当前界面 = 块.面板根;
            有界面打开 = true;
            本次右键已被占用 = true;
            Debug.Log($"[StationInteractor] 打开【{设施.标题}】", 设施);
            return;
        }

        if (设施.类型 == StationInteractable.StationKind.对话)
        {
            var 对话 = 设施.GetComponent<NpcDialogue>();
            if (对话 == null) 对话 = 设施.GetComponentInParent<NpcDialogue>();
            if (对话 == null) 对话 = 设施.gameObject.AddComponent<NpcDialogue>();   // 老场景实例漏挂时兜一下

            本次右键已被占用 = true;
            Debug.Log($"[StationInteractor] 【{设施.标题}】交给对话模块打开", 设施);
            对话.打开对话();
            return;
        }

        当前设施 = 设施;

        // 有做好的界面就用它，没有就用占位幕布
        if (设施.界面预制体 != null)
        {
            当前界面 = Instantiate(设施.界面预制体);
            当前界面.name = "StationUI_" + 设施.类型;
        }
        else
        {
            当前界面 = 建占位幕布(设施);
        }

        有界面打开 = true;
        本次右键已被占用 = true;      // 开界面这一帧不要再被别处当成"锁敌人"

        Debug.Log($"[StationInteractor] 打开【{设施.标题}】界面" +
                  (设施.界面预制体 == null ? "（UI 尚未设计，用空白幕布占位）" : ""), 设施);
    }

    public void 关闭界面()
    {
        有界面打开 = false;
        上次关闭界面时间 = Time.unscaledTime;      // 让暂停菜单知道"刚关过"，这一下 ESC 别再响
        UiEscRegistry.记录关闭();                    // 统一走协调器，和角色面板同一个记录

        // ★ 传送面板不是本组件造的（是 Teleporter 自己画的），所以**不能 Destroy**，
        //   只能让它自己收起来 —— 否则下次再按 F 时 Teleporter 手里的 面板 引用已经销毁了。
        if (当前设施 != null && 当前设施.类型 == StationInteractable.StationKind.传送)
        {
            var 传送 = 当前设施.GetComponent<Teleporter>();
            if (传送 == null) 传送 = 当前设施.GetComponentInParent<Teleporter>();
            if (传送 != null) 传送.关面板();
            当前界面 = null;
            关闭冷却 = 0.25f;
            Debug.Log($"[StationInteractor] 关闭【{当前设施.标题}】传送面板", 当前设施);
            当前设施 = null;
            return;
        }

        // ★ 功德堂兑换的面板同样是它自己造的 —— 只能让它自己收
        if (当前设施 != null)
        {
            var 兑换 = 当前设施.GetComponent<功德堂兑换>();
            if (兑换 == null) 兑换 = 当前设施.GetComponentInParent<功德堂兑换>();
            if (兑换 != null)
            {
                兑换.关面板();
                当前界面 = null;
                关闭冷却 = 0.25f;
                当前设施 = null;
                return;
            }
        }

        // ★ 灵田地块的面板也是它自己造的（见 打开界面），所以同样只能让它自己收
        if (当前设施 != null && 当前设施.类型 == StationInteractable.StationKind.灵田地块)
        {
            var 块 = 当前设施.GetComponent<灵田地块>();
            if (块 == null) 块 = 当前设施.GetComponentInParent<灵田地块>();
            if (块 != null) 块.关面板();
            当前界面 = null;
            关闭冷却 = 0.25f;
            Debug.Log($"[StationInteractor] 关闭【{当前设施.标题}】", 当前设施);
            当前设施 = null;
            return;
        }

        if (当前界面 != null) Destroy(当前界面);
        当前界面 = null;
        关闭冷却 = 0.25f;            // 关掉之后短暂不响应右键，免得同一串点击又开一个
        if (当前设施 != null) Debug.Log($"[StationInteractor] 关闭【{当前设施.标题}】界面", 当前设施);
        当前设施 = null;
    }

    /// <summary>
    /// 临时占位幕布：全屏半透明黑 + 标题 + 一句说明 + 关闭按钮。
    /// 【这整块等真 UI 做好后会被替换掉】
    /// </summary>
    GameObject 建占位幕布(StationInteractable 设施)
    {
        幕布根 = new GameObject("StationUIPlaceholder", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = 幕布根.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2500;                  // 低于暂停菜单(3000)，高于其它 UI

        var scaler = 幕布根.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.transform.SetParent(幕布根.transform, false);
        }

        // 幕布
        var 底 = new GameObject("幕布", typeof(Image));
        底.transform.SetParent(幕布根.transform, false);
        var img = 底.GetComponent<Image>();
        img.color = 幕布底色;
        img.raycastTarget = true;                    // 挡住底下的点击
        拉满(img.rectTransform);

        // 标题
        var 标 = new GameObject("标题", typeof(Text));
        标.transform.SetParent(幕布根.transform, false);
        幕布标题 = 标.GetComponent<Text>();
        幕布标题.text = 设施.标题;
        幕布标题.font = 取字体();
        幕布标题.fontSize = 72;
        幕布标题.color = 标题色;
        幕布标题.alignment = TextAnchor.UpperCenter;
        幕布标题.raycastTarget = false;
        var 标rt = 标.GetComponent<RectTransform>();
        标rt.anchorMin = 标rt.anchorMax = new Vector2(0.5f, 1f);
        标rt.pivot = new Vector2(0.5f, 1f);
        标rt.anchoredPosition = new Vector2(0f, -120f);
        标rt.sizeDelta = new Vector2(900f, 110f);

        // 副标题（说明这是占位）
        var 副 = new GameObject("副标题", typeof(Text));
        副.transform.SetParent(幕布根.transform, false);
        幕布副标题 = 副.GetComponent<Text>();
        幕布副标题.text = "（界面尚未设计，这里是空白幕布占位）\n右键 或 ESC 关闭";
        幕布副标题.font = 取字体();
        幕布副标题.fontSize = 30;
        幕布副标题.color = new Color(标题色.r, 标题色.g, 标题色.b, 0.7f);
        幕布副标题.alignment = TextAnchor.MiddleCenter;
        幕布副标题.raycastTarget = false;
        var 副rt = 副.GetComponent<RectTransform>();
        副rt.anchorMin = 副rt.anchorMax = new Vector2(0.5f, 0.5f);
        副rt.pivot = new Vector2(0.5f, 0.5f);
        副rt.anchoredPosition = Vector2.zero;
        副rt.sizeDelta = new Vector2(900f, 140f);

        // 关闭按钮
        建按钮(幕布根.transform, "关闭", new Vector2(0f, 140f));

        return 幕布根;
    }

    void 建按钮(Transform 父, string 文案, Vector2 位置)
    {
        var go = new GameObject("按钮_" + 文案, typeof(Image), typeof(Button));
        go.transform.SetParent(父, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        img.raycastTarget = true;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = 位置;
        rt.sizeDelta = new Vector2(220f, 56f);

        var 文 = new GameObject("文字", typeof(RectTransform));
        文.transform.SetParent(go.transform, false);
        // 【不要 AddComponent<Text>()】—— 上面 if 里已经挂了 Text 时再加会报
        // "Can't add 'Text' because a 'Text' is already added"，
        // 而且返回的引用是 null，紧接着就 NullReferenceException。
        var t = 文.AddComponent<Text>();
        t.text = 文案;
        t.font = 取字体();
        t.fontSize = 30;
        t.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        拉满(t.rectTransform);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(关闭界面);
    }

    static void 拉满(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Font 取字体()
    {
        return 字体 != null ? 字体 : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // ---- ASCII 别名 ----
    public static bool RightClickConsumed => 本次右键已被占用;
    public bool IsOpen => 界面已打开;
    public StationInteractable CurrentStation => 当前设施;
}
