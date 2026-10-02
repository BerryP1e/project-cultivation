using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **炼丹界面（丹房）** —— 按用户 2026-10-02 给的设计图重做。
///
/// ## 布局（照用户那张图）
///
/// ```
/// ┌──────────────────────────────────────────────────────────────┐
/// │                          丹房                    灵气 150/150 │
/// │  ┌────────────┐  ┌───────────────────────┐  ┌─────────────┐ │
/// │  │  成品预览   │  │ 各种信息的提供          │  │ 获得的丹方   │ │
/// │  │（有丹方时   │  │ 【材料】…               │  │ （只列学会的）│ │
/// │  │  显示信息） │  │ 【1 品丹】成功率…        │  │ [聚气丹]     │ │
/// │  └────────────┘  │ 说明…                  │  │ [清障丹]     │ │
/// │  ┌──────┐┌──────┐└───────────────────────┘  │  ……         │ │
/// │  │主材   ││辅材   │ …… ×4                    └─────────────┘ │
/// │  └──────┘└──────┘                        ┌───────────────┐  │
/// │                                           │    开  炼 ！   │  │
/// │            提示行                          └───────────────┘  │
/// │                        [ 关　闭（ESC / 再按 F） ]              │
/// └──────────────────────────────────────────────────────────────┘
/// ```
///
/// ## 两条硬口径（用户 2026-10-02）
///
/// · **丹方要先"获得"才会列出来**：来源两种 —— 任务里大师兄给的（阶段24 打 `丹方_<id>` 标记）、
///   以及**只要炼出来过就算学会**（`炼丹炉.炼制()` 成功时自己补标记）。没学会的**不显示**。
/// · **材料够的丹方亮、不够的暗**（列表与"开炼"按钮同一套判据：`炼丹炉.能炼`）。
///
/// ## 踩过的坑（照抄 `灵田界面` 的正确结构）
///
/// 1. **幕布就是面板本体，只 `SetActive` 它** —— 别把面板挂在幕布下却丢掉幕布引用，
///    否则关不掉、全屏遮罩一直吃点击（踩坑 H25）。
/// 2. **`UIBuildUtils.CreateImage` 默认 `raycastTarget = false`**，按钮底图必须显式开，
///    否则点不动且没有任何报错。
/// </summary>
[DisallowMultipleComponent]
public class 炼丹界面 : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("**默认关（None）**。\n\n" +
             "【为什么】本界面挂在设施的 `StationInteractable.界面预制体` 上、由 `StationInteractor`\n" +
             "实例化出来 = 就是要显示，关界面直接 Destroy。自己再管热键会和它抢按键。")]
    public KeyCode 开关按键 = KeyCode.None;

    [Header("字体")]
    public Font 字体;

    [Header("配色")]
    public Color 幕布色 = new Color(0f, 0f, 0f, 0.72f);
    public Color 面板色 = new Color(0.15f, 0.14f, 0.17f, 0.97f);
    public Color 板色 = new Color(0.20f, 0.19f, 0.22f, 1f);
    public Color 标题色 = new Color(0.97f, 0.93f, 0.80f, 1f);
    public Color 正文色 = new Color(0.88f, 0.87f, 0.86f, 1f);
    public Color 灰字色 = new Color(0.62f, 0.61f, 0.64f, 1f);
    public Color 强调色 = new Color(0.55f, 0.85f, 0.75f, 1f);
    public Color 按钮色 = new Color(0.72f, 0.58f, 0.30f, 1f);
    public Color 按钮暗色 = new Color(0.30f, 0.29f, 0.32f, 1f);
    public Color 按钮字色 = new Color(0.10f, 0.09f, 0.08f, 1f);
    public Color 开炼色 = new Color(0.84f, 0.17f, 0.14f, 1f);      // 图里那个大红色
    public Color 警告色 = new Color(1f, 0.66f, 0.40f, 1f);

    Canvas 画布;
    RectTransform 面板;                 // ★ 幕布本体（只 SetActive 它）
    Text 标题文本, 灵气文本, 信息文本, 预览名, 提示文本;
    RectTransform 丹方内容, 预览方块;
    Button 开炼按钮;
    RectTransform[] 主材格, 辅材格;
    Text[] 主材格字, 辅材格字;
    readonly List<Button> 丹方按钮 = new List<Button>();
    readonly List<Text> 丹方文字 = new List<Text>();
    readonly List<Image> 丹方底 = new List<Image>();

    string 选中丹方 = "";
    float 提示到期 = -1f;
    float 下次刷新;

    public bool 已打开 => 面板 != null && 面板.gameObject.activeSelf;

    void Awake()
    {
        if (字体 == null) 取默认字体();
        搭界面();
        // ★【不要在这里 关闭()】StationInteractor 是 Instantiate 出来用的，关界面直接 Destroy。
        打开();
    }

    void Start()
    {
        var 炉 = 炼丹炉.取();
        if (炉 != null) 炉.炼制完成 += 处理炼制完成;
        刷新();
    }

    void OnDestroy()
    {
        var 炉 = FindObjectOfType<炼丹炉>();
        if (炉 != null) 炉.炼制完成 -= 处理炼制完成;
    }

    void Update()
    {
        if (开关按键 != KeyCode.None && Input.GetKeyDown(开关按键)) 切换();
        if (提示到期 > 0f && Time.unscaledTime >= 提示到期)
        {
            提示到期 = -1f;
            if (提示文本 != null) 提示文本.text = "";
        }
        // 材料数量会被背包改动，隔一会儿刷一次（面板只在打开时刷新，开销可以忽略）
        if (已打开 && Time.unscaledTime >= 下次刷新)
        {
            下次刷新 = Time.unscaledTime + 0.35f;
            刷新();
        }
    }

    // ============================================================ 开关

    public void 切换() { if (已打开) 关闭(); else 打开(); }

    public void 打开()
    {
        if (面板 == null) return;
        面板.gameObject.SetActive(true);
        刷新();
        Debug.Log("[炼丹界面] 已打开");
    }

    public void 关闭()
    {
        if (面板 == null) return;
        面板.gameObject.SetActive(false);
    }

    // ============================================================ 操作

    void 点丹方(string id) { 选中丹方 = id; 刷新(); }

    void 点开炼()
    {
        var 炉 = 炼丹炉.取();
        if (炉 == null) return;
        var r = 炉.炼制(选中丹方);
        处理炼制完成(r);
        刷新();
    }

    void 处理炼制完成(炼丹结果 r)
    {
        if (提示文本 == null) return;
        提示文本.text = r.文本;
        提示文本.color = r.成功 ? 强调色 : (r.受理 ? 警告色 : 正文色);
        提示到期 = Time.unscaledTime + 5f;
    }

    // ============================================================ 刷新

    void 刷新()
    {
        var 炉 = 炼丹炉.取();
        if (炉 == null || 面板 == null || !面板.gameObject.activeSelf) return;

        var 命 = FindObjectOfType<PlayerVitals>();
        if (灵气文本 != null)
        {
            string 灵 = 命 != null ? $"{命.当前灵气:F0}/{命.灵气上限:F0}" : "—";
            string 加 = 炉.成功率加成 > 0.001f ? $"　丹术 +{炉.成功率加成:P0}" : "";
            string 减 = 炉.耗气减免 > 0.001f ? $"　减耗 {炉.耗气减免:P0}" : "";
            灵气文本.text = $"灵气 {灵}{加}{减}";
        }

        // ★ 只列**已学会**的丹方（没学会的不显示 —— 用户口径）
        var 丹方们 = 炉.已学会的丹方();

        if (丹方们.Count == 0)
        {
            选中丹方 = "";
            刷新丹方列表(炉);
            清空详情();
            return;
        }

        bool 还在 = false;
        foreach (var d in 丹方们) if (d.id == 选中丹方) 还在 = true;
        if (!还在) 选中丹方 = 丹方们[0].id;

        刷新丹方列表(炉);
        刷新详情(炉);
    }

    void 清空详情()
    {
        if (信息文本 != null) 信息文本.text = "还没有学会任何丹方。\n\n（丹方来自：师父 / 大师兄给的，或自己炼成功过一次。）";
        if (预览名 != null) 预览名.text = "—";
        foreach (var t in 主材格字) if (t != null) t.text = "—";
        foreach (var t in 辅材格字) if (t != null) t.text = "—";
        foreach (var g in 主材格) if (g != null) g.GetComponent<Image>().color = 按钮暗色;
        foreach (var g in 辅材格) if (g != null) g.GetComponent<Image>().color = 按钮暗色;
        if (开炼按钮 != null)
        {
            开炼按钮.interactable = false;
            开炼按钮.GetComponent<Image>().color = 按钮暗色;
        }
    }

    void 刷新丹方列表(炼丹炉 炉)
    {
        var 丹方们 = 炉.已学会的丹方();
        if (丹方按钮.Count != 丹方们.Count) 重建丹方(丹方们.Count);
        for (int i = 0; i < 丹方按钮.Count && i < 丹方们.Count; i++)
        {
            var d = 丹方们[i];
            string 原因;
            bool 够 = 炉.能炼(d, out 原因);
            bool 选 = d.id == 选中丹方;

            丹方文字[i].text = 够 ? d.名 : d.名 + "（缺料）";
            // 选中最亮、材料够的亮、不够的暗（用户：材料足够就会亮起，不然就是暗下去的）
            丹方底[i].color = 选 ? 按钮色 : (够 ? new Color(0.42f, 0.38f, 0.30f, 1f) : new Color(0.24f, 0.23f, 0.26f, 1f));
            丹方文字[i].color = 选 ? 按钮字色 : (够 ? 正文色 : 灰字色);
        }
    }

    void 刷新详情(炼丹炉 炉)
    {
        var 丹方 = 炉.取丹方(选中丹方);
        if (丹方 == null) { 清空详情(); return; }

        var 板 = FindObjectOfType<UIPanelData>();
        var 材料 = 炉.全部材料(丹方);
        var sb = new StringBuilder();
        sb.AppendLine("【材料】");
        for (int i = 0; i < 材料.Count; i++)
        {
            var kv = 材料[i];
            var 定义 = 取物品(kv.Key);
            int 有 = 定义 != null && 板 != null ? 板.物品数量(定义) : 0;
            string 名 = 定义 != null ? 定义.物品名 : kv.Key;
            sb.AppendLine($"　{名} ×{kv.Value}　(有 {有}){(有 >= kv.Value ? "" : "　✗")}");
        }
        sb.AppendLine();
        sb.AppendLine($"【{丹方.品} 品丹】成功率 {炉.实际成功率(丹方):P0}　耗灵气 {炉.实际耗气(丹方)}");
        sb.AppendLine(丹方.说明);
        if (信息文本 != null) 信息文本.text = sb.ToString();

        // ---- 成品预览 ----
        if (预览名 != null) 预览名.text = 丹方.名 + "\n" + 丹方.品 + " 品";
        if (预览方块 != null)
            预览方块.GetComponent<Image>().color = new Color(0.78f, 0.70f, 0.42f, 1f);

        // ---- 材料格：第 0 格主材，其余辅材（最多 4 格）----
        刷一格(主材格[0], 主材格字[0], 材料.Count > 0 ? 材料[0] : default(KeyValuePair<string, int>), 材料.Count > 0, 板);
        for (int i = 0; i < 辅材格.Length; i++)
        {
            int 序 = i + 1;
            刷一格(辅材格[i], 辅材格字[i], 序 < 材料.Count ? 材料[序] : default(KeyValuePair<string, int>), 序 < 材料.Count, 板);
        }

        // ---- 开炼按钮 ----
        string 不行;
        bool 可以 = 炉.能炼(丹方, out 不行);
        if (开炼按钮 != null)
        {
            开炼按钮.interactable = 可以;
            开炼按钮.GetComponent<Image>().color = 可以 ? 开炼色 : 按钮暗色;
        }
    }

    void 刷一格(RectTransform 格, Text 字, KeyValuePair<string, int> 材料, bool 有这一格, UIPanelData 板)
    {
        if (格 == null || 字 == null) return;
        if (!有这一格)
        {
            字.text = "—";
            字.color = 灰字色;
            格.GetComponent<Image>().color = new Color(0.20f, 0.19f, 0.22f, 1f);
            return;
        }
        var 定义 = 取物品(材料.Key);
        int 有 = 定义 != null && 板 != null ? 板.物品数量(定义) : 0;
        string 名 = 定义 != null ? 定义.物品名 : 材料.Key;
        bool 够 = 有 >= 材料.Value;
        字.text = 名 + "\n×" + 材料.Value + "\n(有 " + 有 + ")";
        字.color = 够 ? 正文色 : 警告色;
        格.GetComponent<Image>().color = 够 ? new Color(0.30f, 0.36f, 0.30f, 1f) : new Color(0.40f, 0.24f, 0.22f, 1f);
    }

    // ============================================================ 搭界面

    void 重建丹方(int n)
    {
        foreach (var b in 丹方按钮) if (b != null) Destroy(b.gameObject);
        丹方按钮.Clear(); 丹方文字.Clear(); 丹方底.Clear();
        if (丹方内容 == null) return;
        for (int i = 0; i < n; i++)
        {
            string id = 炼丹炉.取().已学会的丹方()[i].id;
            var b = UIBuildUtils.CreateButton("丹方" + i, 丹方内容, 字体, "", 20);
            var 图 = b.GetComponent<Image>();
            图.raycastTarget = true;                       // ★ 必须显式开
            var lbl = b.GetComponentInChildren<Text>();
            lbl.alignment = TextAnchor.MiddleLeft;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 46f; le.minHeight = 46f;
            b.onClick.AddListener(() => 点丹方(id));
            丹方按钮.Add(b); 丹方文字.Add(lbl); 丹方底.Add(图);
        }
    }

    /// <summary>按左上角定位（框内绝对布局：图里每个块的位置都是定好的）</summary>
    static RectTransform 放(RectTransform 父, string 名, float x, float y, float w, float h)
    {
        var rt = UIBuildUtils.CreateRect(名, 父);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -y);
        return rt;
    }

    static Image 放板(RectTransform 父, string 名, float x, float y, float w, float h, Color 色)
    {
        var rt = 放(父, 名, x, y, w, h);
        var 图 = UIBuildUtils.CreateImage(名 + "底", rt, 色);
        UIBuildUtils.Stretch(图.rectTransform);
        return 图;
    }

    void 搭界面()
    {
        if (画布 != null) return;

        var 根 = new GameObject("AlchemyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        画布.sortingOrder = 2650;          // 高于 HUD(≤1520)，低于暂停菜单(3000)

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        // ★★ 幕布 = 面板本体，**只 SetActive 它**
        var 幕 = UIBuildUtils.CreateImage("炼丹幕布", 根.transform, 幕布色);
        幕.raycastTarget = true;
        UIBuildUtils.Stretch(幕.rectTransform);
        面板 = 幕.rectTransform;

        // 内容框（居中，1560×880）
        var 框图 = UIBuildUtils.CreateImage("内容框", 面板, 面板色);
        框图.raycastTarget = true;
        var 框 = 框图.rectTransform;
        框.anchorMin = new Vector2(0.5f, 0.5f);
        框.anchorMax = new Vector2(0.5f, 0.5f);
        框.pivot = new Vector2(0.5f, 0.5f);
        框.sizeDelta = new Vector2(1560f, 880f);
        框.anchoredPosition = Vector2.zero;

        // ---- 标题「丹房」+ 右上灵气 ----
        标题文本 = UIBuildUtils.CreateText("标题", 放(框, "标题位", 0f, 16f, 1560f, 56f), 字体, "丹房", 44, TextAnchor.MiddleCenter, 标题色);
        UIBuildUtils.Stretch(标题文本.rectTransform);
        灵气文本 = UIBuildUtils.CreateText("灵气", 放(框, "灵气位", 1100f, 26f, 420f, 40f), 字体, "灵气 -", 20, TextAnchor.MiddleRight, 正文色);
        UIBuildUtils.Stretch(灵气文本.rectTransform);

        // ---- 左：成品预览 ----
        var 预览板 = 放板(框, "成品预览", 40f, 100f, 360f, 260f, 板色);
        var 方块图 = 放格(预览板.rectTransform, "预览方块", 20f, 20f, 320f, 150f);
        方块图.color = new Color(0.35f, 0.33f, 0.36f, 1f);
        预览方块 = 方块图.rectTransform;
        预览名 = UIBuildUtils.CreateText("预览名", 放(预览板.rectTransform, "预览名位", 20f, 178f, 320f, 66f), 字体, "—", 22, TextAnchor.UpperCenter, 标题色);
        UIBuildUtils.Stretch(预览名.rectTransform);

        // ---- 中：各种信息的提供 ----
        var 信息板 = 放板(框, "信息板", 420f, 100f, 700f, 260f, 板色);
        var 信息标题 = UIBuildUtils.CreateText("信息标题", 放(信息板.rectTransform, "信息标题位", 18f, 12f, 664f, 30f), 字体, "各种信息的提供", 20, TextAnchor.MiddleLeft, 灰字色);
        UIBuildUtils.Stretch(信息标题.rectTransform);
        信息文本 = UIBuildUtils.CreateText("信息", 放(信息板.rectTransform, "信息位", 18f, 46f, 664f, 200f), 字体, "", 19, TextAnchor.UpperLeft, 正文色);
        UIBuildUtils.Stretch(信息文本.rectTransform);

        // ---- 右上：获得的丹方（滚动列表）----
        var 丹方板 = 放板(框, "丹方板", 1140f, 100f, 380f, 460f, 板色);
        var 丹方标题 = UIBuildUtils.CreateText("丹方标题", 放(丹方板.rectTransform, "丹方标题位", 16f, 12f, 348f, 32f), 字体, "获得的丹方", 22, TextAnchor.MiddleLeft, 标题色);
        UIBuildUtils.Stretch(丹方标题.rectTransform);
        var 丹方提示 = UIBuildUtils.CreateText("丹方提示", 放(丹方板.rectTransform, "丹方提示位", 16f, 48f, 348f, 52f), 字体,
            "（只要炼出来过就能学会，没学会的不显示；材料够就亮，不够就暗）", 15, TextAnchor.UpperLeft, 灰字色);
        UIBuildUtils.Stretch(丹方提示.rectTransform);

        var 视口 = 放(丹方板.rectTransform, "视口", 16f, 106f, 348f, 338f);
        视口.gameObject.AddComponent<RectMask2D>();
        丹方内容 = UIBuildUtils.CreateRect("丹方内容", 视口);
        丹方内容.anchorMin = new Vector2(0f, 1f);
        丹方内容.anchorMax = new Vector2(1f, 1f);
        丹方内容.pivot = new Vector2(0.5f, 1f);
        丹方内容.sizeDelta = new Vector2(0f, 0f);
        丹方内容.anchoredPosition = Vector2.zero;
        var 竖 = UIBuildUtils.AddVerticalLayout(丹方内容, 6f, new RectOffset(0, 0, 0, 0));
        竖.childAlignment = TextAnchor.UpperCenter;
        竖.childControlWidth = true;
        // ★ 必须 true：否则布局组不认按钮上的 `LayoutElement.preferredHeight`，
        //   每行会用它自己的默认高（100），4 条就撑出可视区、第 4 条被裁掉（实测）
        竖.childControlHeight = true;
        竖.childForceExpandWidth = true; 竖.childForceExpandHeight = false;
        var 自适应 = 丹方内容.gameObject.AddComponent<ContentSizeFitter>();
        自适应.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var 滚 = 丹方板.gameObject.AddComponent<ScrollRect>();
        滚.viewport = 视口; 滚.content = 丹方内容;
        滚.horizontal = false; 滚.vertical = true;
        滚.movementType = ScrollRect.MovementType.Clamped;
        滚.scrollSensitivity = 30f;

        // ---- 下：主材 1 格 + 辅材 4 格（**格子自己带 Image**，刷新时要直接改它的颜色）----
        主材格 = new RectTransform[1];
        主材格字 = new Text[1];
        主材格[0] = 放格(框, "主材格", 40f, 384f, 200f, 132f).rectTransform;
        主材格字[0] = 格内字(主材格[0], "主材");
        辅材格 = new RectTransform[4];
        辅材格字 = new Text[4];
        for (int i = 0; i < 4; i++)
        {
            辅材格[i] = 放格(框, "辅材格" + i, 260f + i * 216f, 384f, 200f, 132f).rectTransform;
            辅材格字[i] = 格内字(辅材格[i], "辅材");
        }

        // ---- 右下：大红「开炼！」----
        开炼按钮 = UIBuildUtils.CreateButton("开炼", 放(框, "开炼位", 1140f, 580f, 380f, 170f), 字体, "开炼！", 46);
        UIBuildUtils.Stretch(开炼按钮.GetComponent<RectTransform>());
        var 炼图 = 开炼按钮.GetComponent<Image>();
        炼图.raycastTarget = true;
        炼图.color = 开炼色;
        开炼按钮.GetComponentInChildren<Text>().color = new Color(1f, 0.97f, 0.94f, 1f);
        开炼按钮.onClick.AddListener(点开炼);
        // 标签要覆盖整块按钮（CreateButton 的 Label 是左上角对齐的，这里改成居中大字）
        var 炼字 = 开炼按钮.GetComponentInChildren<Text>();
        炼字.alignment = TextAnchor.MiddleCenter;
        炼字.fontSize = 46;

        // ---- 提示行 + 关闭 ----
        提示文本 = UIBuildUtils.CreateText("提示", 放(框, "提示位", 40f, 748f, 1080f, 40f), 字体, "", 19, TextAnchor.MiddleLeft, 正文色);
        UIBuildUtils.Stretch(提示文本.rectTransform);

        string 关字 = 开关按键 == KeyCode.None ? "关　闭（ESC / 再按 F）" : "关闭（" + 开关按键 + "）";
        var 关 = UIBuildUtils.CreateButton("关闭", 放(框, "关闭位", 40f, 800f, 1560f, 48f), 字体, 关字, 20);
        UIBuildUtils.Stretch(关.GetComponent<RectTransform>());
        var 关图 = 关.GetComponent<Image>();
        关图.raycastTarget = true;
        关图.color = 按钮暗色;
        关.GetComponentInChildren<Text>().color = 正文色;
        关.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
        关.onClick.AddListener(关闭);
    }

    /// <summary>材料格：**Image 挂在格子自己身上**（刷新时要直接改它的底色）</summary>
    static Image 放格(RectTransform 父, string 名, float x, float y, float w, float h)
    {
        var rt = 放(父, 名, x, y, w, h);
        var 图 = rt.gameObject.AddComponent<Image>();
        图.color = new Color(0.20f, 0.19f, 0.22f, 1f);
        图.raycastTarget = true;
        return 图;
    }

    /// <summary>材料格里的两行字：左上小标题（主材 / 辅材）+ 中间的名称与数量</summary>
    Text 格内字(RectTransform 格, string 小标题)
    {
        var 标题 = UIBuildUtils.CreateText("格标题", 放(格, "格标题位", 10f, 6f, 180f, 24f), 字体, 小标题, 15, TextAnchor.UpperLeft, 灰字色);
        UIBuildUtils.Stretch(标题.rectTransform);
        var 字 = UIBuildUtils.CreateText("格字", 放(格, "格字位", 10f, 30f, 180f, 96f), 字体, "—", 19, TextAnchor.MiddleCenter, 正文色);
        UIBuildUtils.Stretch(字.rectTransform);
        return 字;
    }

    static ItemDefinition 取物品(string id)
    {
        var 库 = QuestDatabase.取();
        return 库 != null ? 库.找物品(id) : null;
    }

    void 取默认字体()
    {
#if UNITY_EDITOR
        字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
#endif
        if (字体 == null)
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                if (f != null && f.name.ToLowerInvariant().Contains("simhei")) { 字体 = f; return; }
    }

    // ---- ASCII 别名 ----
    public void Open() => 打开();
    public void Close() => 关闭();
    public void Toggle() => 切换();
}
