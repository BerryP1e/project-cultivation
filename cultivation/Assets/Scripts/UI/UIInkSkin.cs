using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【U I 水墨换皮 · 运行时】把已有 uGUI 节点按名字换成 `Assets/resources/UI/InkUI/` 素材。
///
/// ## 为什么是"运行时按名字换"而不是改生成器 / 改场景
///
/// 1. 角色面板是 `CharacterPanelBuilder` **在编辑器里生成**、然后**序列化进 5 个游玩场景**的
///    （见 `docs/architecture/UI现状原理图.md §3`）。重新生成 + 保存场景会把运行时 UI 列表一起重排，
///    历史上已经因此写脏过场景 **+19787 行**（踩坑 A8）。⇒ **不碰场景文件**是本工程的硬规矩。
/// 2. 所以这一版走"**运行时补**"：由 `场景自举.补HUD层()` 顺手挂到 `CharacterUI` 上（和 `功德堂兑换`
///    一样的路子）。等素材与映射都定稿了，再把同样的映射**折进生成器**（那时只跑一次生成器 + 逐个场景目视验收）。
///
/// ## 它只做三件事
///
/// · **换图**：按节点名给 `Image.sprite` 换素材，并把 `Image.type` / `pixelsPerUnitMultiplier` 配对
///   （素材是 **2× 交付**、PPU=100 ⇒ 九宫格要 `pixelsPerUnitMultiplier = 2` 才按 1× 视觉尺寸渲染）。
/// · **换按钮**：`Button` 改成 `SpriteSwap`，按名字判**三档材质**（朱砂=仪式/危险、玉绿=主行动、素纸=次级）。
/// · **布局与状态**：调用 UI神通页重排 调整运行时布局，底色复位为白、文字改墨色；数据与开关仍由原控制器负责。
///
/// ## 用法
///
/// 由 `场景自举` 自动补到 `CharacterUI` 上，不需要手动挂。
/// 调试回退通过编辑器 inkqa:before 后重新进 Play；禁用组件不会还原已经换过的 Sprite。
/// </summary>
[DisallowMultipleComponent]
public class UIInkSkin : MonoBehaviour
{
    [Tooltip("总开关。关掉 = 什么也不换（排查「画面变了是不是它干的」用）")]
    public bool 启用 = true;

    [Header("分组开关（想只换一部分时用）")]
    public bool 换面板底 = true;
    public bool 换页签 = true;
    public bool 换列表行 = true;
    public bool 换按钮 = true;
    public bool 换进度条 = true;
    public bool 换滚动条 = true;

    [Tooltip("要换皮的根。留空 = 本物体（`CharacterUI`）")]
    public Transform 根;

    [Tooltip("打印每个节点换了什么（排查用）")]
    public bool 打印日志 = false;

    [Tooltip("把面板里的淡色文字改成**墨色**（纸底上白字看不清）并去掉黑描边")]
    public bool 换字色 = true;

    [Tooltip("按原型图**重排神通页**（大幕布 / 左导航立板 / 六槽环+编号 / 卡片网格 / 通高详情）")]
    public bool 重排神通页 = true;

    /// <summary>创角面板的墨色（= 规格色板 #303D37）</summary>
    static readonly Color 墨色 = new Color(0.188f, 0.239f, 0.216f, 1f);

    // ---- 素材根（相对 Resources；`Assets/resources` 是全工程既有的 Resources 目录）----
    const string 资源根 = "UI/InkUI";

    /// <summary>2× 交付 ⇒ 九宫格按 1× 渲染要这个倍率</summary>
    const float 九宫倍率 = 2f;
    /// <summary>按钮包是 1× 交付（520×157），建议显示高 48~64 ⇒ 倍率 2.6（见 BUTTON-KIT.md）</summary>
    const float 按钮倍率 = 2.6f;


    CharacterPanelUI _角色面板;

    void Start()
    {
        // 页签切换发生在运行时，不能只在 Start 时把「神通」设成选中态。
        // 订阅现有控制器的事件即可保持原有开关逻辑，仍然不改场景数据。
        _角色面板 = GetComponent<CharacterPanelUI>();
        if (_角色面板 != null) _角色面板.TabChanged += 页签切换后刷新;
        刷新();
    }

    void OnDestroy()
    {
        if (_角色面板 != null) _角色面板.TabChanged -= 页签切换后刷新;
    }

    void 页签切换后刷新(CharacterTab _)
    {
        var r = 根 != null ? 根 : transform;
        刷新页签(r);
    }

    /// <summary>跑一遍换皮（幂等：换过的节点会再设一次同样的值）</summary>
    public void 刷新()
    {
        if (!启用 || !InkUITheme.Enabled) return;
        var r = 根 != null ? 根 : transform;
        int 面板 = 0, 页签 = 0, 行 = 0, 按钮 = 0, 进度 = 0, 滚动 = 0;

        foreach (var img in r.GetComponentsInChildren<Image>(true))
        {
            if (img == null) continue;
            string n = img.gameObject.name;

            // ---- 主动技能栏：六槽**环形整图**（中央透明，正好让六个槽位露出来）----
            if (n == "ActiveSkillBar")
            {
                if (套普通(img, "SkillsPage/skills/active-ring", true)) 面板++;
                continue;
            }
            // ---- 六个主动槽位（`Slot_0`…`Slot_5`）----
            if (n.StartsWith("Slot_"))
            {
                if (套普通(img, "SkillsPage/skills/slot-empty", true)) 行++;
                continue;
            }
            // 槽位里的 Icon 由 UIActiveSkillSlot.Bind 按数据写入：
            // 空槽必须保持 sprite=null，否则它会盖住真正的异形槽底；有内容时
            // 再由数据侧 DisplayIcon 覆盖。这里故意不把空槽框塞进 Icon 层。
            // ---- 页根 / 内容容器：把它们自己的深色底**关掉**，让父级那张纸透上来 ----
            //   （生成器给每个 `Page_*` 都铺了一层深色底，不关的话整块内容区还是黑的，纸白只在边角）
            if (n == "Content" || n.StartsWith("Page_"))
            {
                if (img.sprite == null && img.color.a > 0.01f)
                {
                    var c = img.color; c.a = 0f; img.color = c;      // 只清底，不动它的布局
                    面板++;
                }
                continue;
            }
            // ---- 页签（`Tab_背包`/`Tab_神通`…）----
            if (换页签 && n.StartsWith("Tab_"))
            {
                if (套九宫(img, "SkillsPage/navigation/tab-normal", true)) 页签++;
                continue;
            }
            // ---- 列表行（34 高行 / 外观行 / 丹方行）----
            if (换列表行 && (n == "Row" || n.StartsWith("行_") || n.StartsWith("丹方") || n.StartsWith("背包行")))
            {
                if (套九宫(img, "SkillsPage/skills/row-normal")) 行++;
                continue;
            }
            // ---- 进度条 ----
            if (换进度条)
            {
                if (n == "Track" || n == "进度底" || n.EndsWith("条底"))
                {
                    if (套九宫(img, "SkillsPage/status/bar-track")) 进度++;
                    continue;
                }
                if (n == "Fill" || n.EndsWith("进度填充"))
                {
                    if (套普通(img, "SkillsPage/status/bar-fill")) 进度++;
                    continue;
                }
            }
            // ---- 滚动条 ----
            if (换滚动条)
            {
                var sb = img.GetComponent<Scrollbar>();
                if (sb != null)
                {
                    // Scrollbar 自己的 Image = 轨道；它子物体名叫 Handle 的 = 滑块
                    if (sb.handleRect != null && sb.handleRect.GetComponent<Image>() == img)
                    {
                        if (套九宫(img, "SkillsPage/status/scroll-thumb")) 滚动++;
                    }
                    else if (套九宫(img, "SkillsPage/status/scroll-track")) 滚动++;
                    continue;
                }
            }
            // ---- 面板底 ----
            if (换面板底)
            {
                if (n == "Window")
                {
                    if (套九宫(img, "SkillsPage/common/panel-sheet", true)) 面板++;
                    continue;
                }
                if (n == "Sidebar")
                {
                    if (套九宫(img, "SkillsPage/navigation/nav-rail", true)) 面板++;
                    continue;
                }
                if (是内板名(n))
                {
                    // 神通页的 `Info` 在规格里是**右侧通高详情板**（不是普通内板）
                    bool 是神通详情 = (n == "Info" && 在页下(img.transform, "Page_神通"));
                    string 路 = 是神通详情 ? "SkillsPage/skills/panel-detail" : "SkillsPage/common/panel-inner";
                    if (套九宫(img, 路, true)) 面板++;
                    continue;
                }
            }
        }

        // ---- 按钮：换 SpriteSwap + 三档材质（放在 Image 之后，避免被上面的 continue 抢先）----
        if (换按钮)
            foreach (var b in r.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "Dim") continue;
                if (b == null) continue;
                if (套按钮(b)) 按钮++;
            }


        // ---- 字色：纸底上原来的淡色字（金/白）会看不清 ⇒ 统一成墨色，并去掉黑描边 ----
        int 字 = 0;
        if (换字色)
            foreach (var t in r.GetComponentsInChildren<Text>(true))
            {
                if (t == null) continue;
                // 只改"偏亮"的字（深色字本来就能读，别乱动）
                if (t.GetComponentInParent<Button>(true) == null && t.color.maxColorComponent > 0.55f) { t.color = 墨色; 字++; }
                var o = t.GetComponent<Outline>();
                if (o != null && o.enabled) o.enabled = false;
            }

        Debug.Log("[水墨换皮] " + gameObject.name + " 完成：面板底 " + 面板 + " / 页签 " + 页签 + " / 列表行 " + 行
                  + " / 按钮 " + 按钮 + " / 进度条 " + 进度 + " / 滚动条 " + 滚动 + " / 改字色 " + 字, this);

        // ---- 换完图之后，再按原型图**重排神通页**（幕布/左导航/六槽环+编号/卡片网格/通高详情）----
        if (重排神通页) UI神通页重排.应用(r);
        foreach (var button in r.GetComponentsInChildren<Button>(true))
            if (!button.name.StartsWith("Tab_") && button.name != "Row" && button.GetComponent<UIActiveSkillSlot>() == null && button.GetComponent<UISpiritSlot>() == null)
                InkUITheme.Button(button);
        刷新页签(r);
    }

    /// <summary>内板（内容分区底板）—— 名字来自 `CharacterPanelBuilder` 的分区命名</summary>
    static readonly HashSet<string> 内板 = new HashSet<string>
    {
        "BagGrid", "Info", "ItemDesc", "AttrList", "GongFaShow", "RealmShow",
        "ActiveSkillBar", "PassiveList", "KnownList", "MountShow", "MountList",
        "AppearanceShow", "AppearanceList", "FormationGrid", "SpiritList", "OwnedList", "ArrayList", "GridArea",
    };
    static bool 是内板名(string n) => 内板.Contains(n);

    /// <summary>某个节点的祖先里有没有叫这个名字的（用来区分"同一个 `Info` 在哪个页里"）</summary>
    static bool 在页下(Transform t, string 页名)
    {
        for (var p = t; p != null; p = p.parent) if (p.name == 页名) return true;
        return false;
    }

    /// <summary>九宫格图：Sprite + Sliced + 倍率；`重置颜色` 用于底板/页签（生成器给它们设了深色，会把纸乘成灰）</summary>
    bool 套九宫(Image img, string 相对路径, bool 重置颜色 = false)
    {
        var sp = 取图(相对路径);
        if (sp == null) return false;
        img.sprite = sp;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 九宫倍率;
        if (重置颜色) img.color = Color.white;      // ★ 不重置的话：纸白 × 0.13 灰 = 还是黑的
        if (打印日志) Debug.Log("[水墨换皮] " + img.gameObject.name + " ← " + 相对路径, img);
        return true;
    }

    /// <summary>普通图（异形整图 / 填充段）：Sprite + Simple（填充段保持它原有的 Filled）</summary>
    bool 套普通(Image img, string 相对路径, bool 重置颜色 = false)
    {
        var sp = 取图(相对路径);
        if (sp == null) return false;
        img.sprite = sp;
        if (img.type != Image.Type.Filled) img.type = Image.Type.Simple;
        if (重置颜色) img.color = Color.white;
        if (打印日志) Debug.Log("[水墨换皮] " + img.gameObject.name + " ← " + 相对路径, img);
        return true;
    }

    /// <summary>按钮三档：按名字判"这是仪式动作 / 主行动 / 次级动作"</summary>
    bool 套按钮(Button b)
    {
        string n = b.gameObject.name;
        if (b.GetComponent<UIActiveSkillSlot>() != null || b.GetComponent<UISpiritSlot>() != null) return false;

        // 页签与列表行各自拥有三态素材，不能被通用按钮包覆盖。
        if (n.StartsWith("Tab_")) return 套页签按钮(b);
        if (n == "Row" || n.StartsWith("行_") || n.StartsWith("丹方") || n.StartsWith("背包行"))
            return 套列表按钮(b);

        string 档 =
            (n.Contains("破境") || n.Contains("转修") || n.Contains("重生") || n.Contains("开炉")
             || n.Contains("开炼") || n.Contains("死亡") || n.Contains("卸下")) ? "cinnabar"
            : (n.Contains("装备") || n.Contains("修炼") || n.Contains("闭关") || n.Contains("突破")
               || n.Contains("启用") || n.Contains("确认") || n.Contains("兑换") || n.Contains("使用")
               || n.Contains("收获") || n.Contains("播种") || n.Contains("一键")) ? "jade"
            : "ivory";

        var 常态 = 取图("Buttons/" + 档 + "-normal");
        if (常态 == null) return false;

        var img = b.targetGraphic as Image;
        if (img == null) img = b.GetComponent<Image>();
        if (img == null) return false;

        img.sprite = 常态;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 按钮倍率;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;

        var st = b.spriteState;
        st.highlightedSprite = 取图("Buttons/" + 档 + "-hover");
        st.selectedSprite = st.highlightedSprite;
        st.pressedSprite = 取图("Buttons/" + 档 + "-pressed");
        st.disabledSprite = 取图("Buttons/" + 档 + "-disabled");
        b.spriteState = st;

        if (打印日志) Debug.Log("[水墨换皮] 按钮 " + n + " → " + 档, b);
        return true;
    }

    bool 套页签按钮(Button b)
    {
        var img = b.targetGraphic as Image;
        if (img == null) img = b.GetComponent<Image>();
        if (img == null) return false;

        var normal = 取图("SkillsPage/navigation/tab-normal");
        var hover = 取图("SkillsPage/navigation/tab-hover");
        var active = 取图("SkillsPage/navigation/tab-active");
        if (normal == null) return false;

        img.sprite = normal;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 九宫倍率;
        img.color = Color.white;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        var state = b.spriteState;
        state.highlightedSprite = hover;
        state.pressedSprite = active;
        state.selectedSprite = active;
        state.disabledSprite = normal;
        b.spriteState = state;
        return true;
    }

    bool 套列表按钮(Button b)
    {
        var img = b.targetGraphic as Image;
        if (img == null) img = b.GetComponent<Image>();
        if (img == null) return false;

        var normal = 取图("SkillsPage/skills/row-normal");
        var hover = 取图("SkillsPage/skills/row-hover");
        var selected = 取图("SkillsPage/skills/row-selected");
        if (normal == null) return false;

        img.sprite = normal;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 九宫倍率;
        img.color = Color.white;
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        var state = b.spriteState;
        state.highlightedSprite = hover;
        state.pressedSprite = selected;
        state.selectedSprite = selected;
        state.disabledSprite = normal;
        b.spriteState = state;
        return true;
    }

    void 刷新页签(Transform r)
    {
        if (!换页签 || r == null || !InkUITheme.Enabled) return;
        string 当前 = "";
        if (_角色面板 != null) 当前 = _角色面板.CurrentTab.ToString();
        if (string.IsNullOrEmpty(当前))
        {
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Page_") && t.gameObject.activeSelf) { 当前 = t.name.Substring(5); break; }
        }

        foreach (var t in r.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Tab_")) continue;
            var img = t.GetComponent<Image>();
            if (img == null) continue;
            bool selected = t.name.Substring(4) == 当前;
            // 独立素纸牌只用于当前项；其余项直接露出暗墨立板。
            // BigPieces 导航大件烘有固定选中项，不能拿它代替动态八页签。
            var sp = 取图("Skeleton/nav-tab-inactive");
            if (sp == null) continue;
            img.sprite = sp;
            img.overrideSprite = null;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 九宫倍率;
            img.color = selected ? Color.white : Color.clear;
            var tabRect = t as RectTransform;
            if (tabRect != null) tabRect.sizeDelta = new Vector2(selected ? 20 : -16, 70);
            var tabButton = t.GetComponent<Button>();
            if (tabButton != null)
            {
                var state = tabButton.spriteState;
                state.selectedSprite = sp;
                state.highlightedSprite = sp;
                state.pressedSprite = sp;
                state.disabledSprite = sp;
                tabButton.spriteState = state;
            }
            var text = t.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.color = selected ? 墨色 : new Color(.93f, .90f, .82f);
                text.fontSize = 24;
                text.alignment = TextAnchor.MiddleLeft;
                text.rectTransform.offsetMin = new Vector2(64f, 0f);
                text.rectTransform.offsetMax = new Vector2(-8f, 0f);
            }

            // 左侧父导航的图标也是独立素材；没有图标子节点时运行时补一个，
            // 不把它烘在立板或页签底图里，方便后续替换/禁用。
            var icon = t.Find("InkIcon");
            if (icon == null)
            {
                icon = new GameObject("InkIcon", typeof(RectTransform)).transform;
                icon.SetParent(t, false);
                var rt = icon as RectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(12f, 0f);
                rt.sizeDelta = new Vector2(44f, 44f);
                var iconImage = icon.gameObject.AddComponent<Image>();
                iconImage.raycastTarget = false;
            }
            var iconImg = icon.GetComponent<Image>();
            var iconRect = icon as RectTransform;
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconRect.sizeDelta = new Vector2(44f, 44f);
            if (iconImg != null)
            {
                var iconPath = 页签图标(t.name.Substring(4));
                var iconSprite = 取图(iconPath);
                if (iconSprite != null)
                {
                    iconImg.sprite = iconSprite;
                    iconImg.type = Image.Type.Simple;
                    iconImg.preserveAspect = true;
                    iconImg.color = Color.white;
                }
            }
        }
    }

    static string 页签图标(string name)
    {
        switch (name)
        {
            case "背包": return "Icons/icon-inventory";
            case "境界": return "Icons/icon-realm";
            case "神通": return "Icons/icon-divine-ability";
            case "法宝": return "Icons/icon-treasure";
            case "灵阵": return "Icons/icon-spirit-array";
            case "战阵": return "Icons/icon-formation";
            case "坐骑": return "Icons/icon-mount";
            case "外观": return "Icons/icon-appearance";
            default: return "";
        }
    }

    static readonly Dictionary<string, Sprite> 缓存 = new Dictionary<string, Sprite>();

    static Sprite 取图(string 相对路径)
    {
        Sprite sp;
        if (缓存.TryGetValue(相对路径, out sp)) return sp;
        sp = InkUITheme.Load(相对路径);
        if (sp == null && !缓存.ContainsKey("__警告_" + 相对路径))
        {
            缓存["__警告_" + 相对路径] = null;
            Debug.LogWarning("[水墨换皮] Resources 里找不到素材「" + 资源根 + "/" + 相对路径 + "」"
                             + " —— 检查 UI 素材是不是被放在了 `Assets/resources/` 下（UIResources 不是 Resources）");
        }
        缓存[相对路径] = sp;
        return sp;
    }

    // ---- ASCII 别名（外部工具按名反射时用）----
    public void Refresh() => 刷新();
    public bool Enabled { get => 启用; set => 启用 = value; }
}
