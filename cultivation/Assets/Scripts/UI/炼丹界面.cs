using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **炼丹界面** —— 选丹方 → 选品 → 炼丹。
///
/// ## 怎么打开
///
/// 默认 **<see cref="开关按键"/> = F5**。
///
/// ## 布局
///
/// ```
/// ┌────────────────────────────────────────────┐
/// │ 丹房 · 炼丹         灵气 150/150  加成 +10% │
/// ├──────────────┬─────────────────────────────┤
/// │ 丹方          │ 炼气破境丹                  │
/// │ [炼气破境丹]  │ 主材 生灵草 ×3（有 12）      │
/// │ [筑基破境丹]  │ 辅材 凝露花 ×2（有 5）       │
/// │              │ 品  [1][2][3][4][5]...       │
/// │              │ 成功率 85%   耗气 20         │
/// │              │        [ 炼  制 ]            │
/// ├──────────────┴─────────────────────────────┤
/// │ 提示行                                      │
/// └────────────────────────────────────────────┘
/// ```
///
/// ## ⚠️ 踩过的坑（照抄 `灵田界面` 的正确结构，别再犯）
///
/// 1. **幕布当面板的父物体，只 SetActive 幕布** ——
///    别把面板挂在幕布下却把幕布引用丢掉，那样关面板关不掉幕布，
///    **全屏遮罩会一直吃掉所有点击**（灵田界面就踩过，见 踩坑 H25）。
/// 2. **`UIBuildUtils.CreateImage` 默认 `raycastTarget = false`**，
///    按钮底图必须显式开，否则**点不动而且没有任何报错**。
/// </summary>
[DisallowMultipleComponent]
public class 炼丹界面 : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("**默认关（None）**。\n\n" +
             "【为什么】本界面是挂在设施的 `StationInteractable.界面预制体` 上、\n" +
             "由 `StationInteractor` **实例化出来 = 就是要显示**，关界面时直接 Destroy。\n" +
             "自己再管一套热键会和它抢按键（和 `CultivationUI` 同一个口径）。\n" +
             "只有把它当独立 HUD 用时才设一个键。")]
    public KeyCode 开关按键 = KeyCode.None;

    [Header("字体")]
    public Font 字体;

    [Header("配色")]
    public Color 幕布色 = new Color(0f, 0f, 0f, 0.72f);
    public Color 面板色 = new Color(0.15f, 0.14f, 0.17f, 0.97f);
    public Color 标题色 = new Color(0.97f, 0.93f, 0.80f, 1f);
    public Color 正文色 = new Color(0.88f, 0.87f, 0.86f, 1f);
    public Color 强调色 = new Color(0.55f, 0.85f, 0.75f, 1f);
    public Color 按钮色 = new Color(0.72f, 0.58f, 0.30f, 1f);
    public Color 按钮暗色 = new Color(0.30f, 0.29f, 0.32f, 1f);
    public Color 按钮字色 = new Color(0.10f, 0.09f, 0.08f, 1f);
    public Color 警告色 = new Color(1f, 0.66f, 0.40f, 1f);

    Canvas 画布;
    RectTransform 面板;        // ★ 幕布本体（只 SetActive 它）
    Text 标题文本, 灵气文本, 丹方详情, 提示文本;
    RectTransform 丹方排, 品排;
    Button 炼制按钮;

    readonly List<Button> 丹方按钮 = new List<Button>();
    readonly List<Text> 丹方文字 = new List<Text>();
    readonly List<Button> 品按钮 = new List<Button>();
    readonly List<Text> 品文字 = new List<Text>();

    string 选中丹方 = "";
    int 选中品 = 1;
    float 提示到期 = -1f;

    public bool 已打开 => 面板 != null && 面板.gameObject.activeSelf;

    void Awake()
    {
        if (字体 == null) 取默认字体();
        搭界面();
        // ★【不要在这里 关闭()】StationInteractor 是把本预制体 Instantiate 出来用的，
        //   关界面时直接 Destroy —— "被实例化出来 = 就是要显示"。
        //   在 Awake 里关掉会导致按 F 交互后一片空白（CultivationUI 里记过同样的坑）。
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
    }

    // ============================================================ 开关

    public void 切换() { if (已打开) 关闭(); else 打开(); }

    public void 打开()
    {
        if (面板 == null) return;
        面板.gameObject.SetActive(true);
        刷新();
        Debug.Log("[炼丹界面] 已打开（" + 开关按键 + " 关闭）");
    }

    public void 关闭()
    {
        if (面板 == null) return;
        面板.gameObject.SetActive(false);
    }

    // ============================================================ 操作

    void 点丹方(string id)
    {
        选中丹方 = id;
        选中品 = 1;
        刷新();
    }

    void 点品(int 品)
    {
        选中品 = 品;
        刷新();
    }

    void 点炼制()
    {
        var 炉 = 炼丹炉.取();
        var r = 炉.炼制(选中丹方, 选中品);
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

        var 丹方们 = 炉.全部丹方();
        if (丹方们.Count == 0)
        {
            if (丹方详情 != null) 丹方详情.text = "还没有任何可炼的丹方。";
            return;
        }
        if (string.IsNullOrEmpty(选中丹方) || 炉.取丹方(选中丹方) == null)
            选中丹方 = 丹方们[0].id;

        刷新丹方列表(炉, 丹方们);
        刷新详情(炉);
    }

    void 刷新丹方列表(炼丹炉 炉, List<灵丹定义> 丹方们)
    {
        if (丹方按钮.Count != 丹方们.Count) 重建丹方(丹方们.Count);
        for (int i = 0; i < 丹方们.Count; i++)
        {
            bool 选 = 丹方们[i].id == 选中丹方;
            丹方按钮[i].GetComponent<Image>().color = 选 ? 按钮色 : new Color(0.34f, 0.32f, 0.30f, 1f);
            丹方文字[i].color = 选 ? 按钮字色 : 正文色;
            丹方文字[i].text = 丹方们[i].名;
        }
    }

    void 刷新详情(炼丹炉 炉)
    {
        var 丹方 = 炉.取丹方(选中丹方);
        if (丹方 == null || 丹方详情 == null) return;

        var 板 = FindObjectOfType<UIPanelData>();
        var sb = new StringBuilder();
        sb.AppendLine(丹方.名);
        sb.AppendLine();
        sb.AppendLine("【材料】");
        foreach (var kv in 炉.全部材料(丹方))
        {
            var 定义 = 取物品(kv.Key);
            int 有 = 定义 != null && 板 != null ? 板.物品数量(定义) : 0;
            string 名 = 定义 != null ? 定义.物品名 : kv.Key;
            sb.AppendLine($"　{名} ×{kv.Value}　(有 {有}){(有 >= kv.Value ? "" : "　✗")}");
        }
        sb.AppendLine();
        int 品 = Mathf.Clamp(选中品, 1, Mathf.Clamp(丹方.最高可炼品, 1, 9));
        float 概 = 炉.实际成功率(丹方, 品);
        int 耗 = 炉.实际耗气(丹方, 品);
        sb.AppendLine($"【{品} 品】成功率 {概:P0}　耗灵气 {耗}");
        sb.AppendLine(丹方.说明);
        丹方详情.text = sb.ToString();

        // 品按钮：1 ~ 最高可炼品
        刷新品按钮(炉, 丹方);

        // 炼制按钮可用性
        string 原因;
        bool 可以 = 炉.能炼(丹方, 品, out 原因);
        if (炼制按钮 != null)
        {
            炼制按钮.interactable = 可以;
            炼制按钮.GetComponent<Image>().color = 可以 ? 按钮色 : 按钮暗色;
            var lbl = 炼制按钮.GetComponentInChildren<Text>();
            if (lbl != null) lbl.text = 可以 ? "炼　制" : "材料不足";
        }
        if (!可以 && 提示文本 != null && string.IsNullOrEmpty(提示文本.text))
            提示文本.text = 原因;
    }

    void 刷新品按钮(炼丹炉 炉, 灵丹定义 丹方)
    {
        int 最高 = Mathf.Clamp(丹方.最高可炼品, 1, 9);
        if (品按钮.Count != 最高) 重建品按钮(最高);
        for (int i = 0; i < 最高; i++)
        {
            int p = i + 1;
            bool 选 = p == 选中品;
            品按钮[i].GetComponent<Image>().color = 选 ? 强调色 : new Color(0.32f, 0.30f, 0.30f, 1f);
            品文字[i].color = 选 ? 按钮字色 : 正文色;
            // 品按钮上带成功率，一眼看出"高品更难"
            品文字[i].text = $"{p}\n{炉.实际成功率(丹方, p):P0}";
        }
    }

    // ============================================================ 搭界面

    void 重建丹方(int n)
    {
        foreach (var b in 丹方按钮) if (b != null) Destroy(b.gameObject);
        丹方按钮.Clear(); 丹方文字.Clear();
        if (丹方排 == null) return;
        for (int i = 0; i < n; i++)
        {
            int 序 = i;
            var b = UIBuildUtils.CreateButton("丹方" + i, 丹方排, 字体, "", 20);
            var 图 = b.GetComponent<Image>();
            图.raycastTarget = true;                       // ★ 必须显式开
            var lbl = b.GetComponentInChildren<Text>();
            lbl.alignment = TextAnchor.MiddleLeft;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 46f; le.minHeight = 46f;
            b.onClick.AddListener(() => 点丹方(炼丹炉.取().全部丹方()[序].id));
            丹方按钮.Add(b); 丹方文字.Add(lbl);
        }
    }

    void 重建品按钮(int n)
    {
        foreach (var b in 品按钮) if (b != null) Destroy(b.gameObject);
        品按钮.Clear(); 品文字.Clear();
        if (品排 == null) return;
        for (int i = 0; i < n; i++)
        {
            int p = i + 1;
            var b = UIBuildUtils.CreateButton("品" + p, 品排, 字体, "", 16);
            var 图 = b.GetComponent<Image>();
            图.raycastTarget = true;
            var lbl = b.GetComponentInChildren<Text>();
            lbl.alignment = TextAnchor.MiddleCenter;
            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 62f; le.preferredHeight = 56f;
            le.minWidth = 62f; le.minHeight = 56f;
            b.onClick.AddListener(() => 点品(p));
            品按钮.Add(b); 品文字.Add(lbl);
        }
    }

    void 搭界面()
    {
        if (画布 != null) return;

        var 根 = new GameObject("AlchemyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        // 比灵田(2600) 高一点，但低于暂停菜单(3000)
        画布.sortingOrder = 2650;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        // ★★ 幕布 = 面板本体。**只 SetActive 它**，所以不可能出现"遮罩没关掉"。
        var 幕 = UIBuildUtils.CreateImage("炼丹幕布", 根.transform, 幕布色);
        幕.raycastTarget = true;
        UIBuildUtils.Stretch(幕.rectTransform);
        面板 = 幕.rectTransform;

        // 内层：居中显示的内容框（不是全屏）
        var 框 = UIBuildUtils.CreateImage("内容框", 面板, 面板色);
        框.raycastTarget = true;
        var 框rt = 框.rectTransform;
        框rt.anchorMin = new Vector2(0.5f, 0.5f);
        框rt.anchorMax = new Vector2(0.5f, 0.5f);
        框rt.pivot = new Vector2(0.5f, 0.5f);
        框rt.sizeDelta = new Vector2(960f, 600f);
        框rt.anchoredPosition = Vector2.zero;

        var 竖 = UIBuildUtils.AddVerticalLayout(框rt, 10f, new RectOffset(24, 24, 18, 18));
        竖.childAlignment = TextAnchor.UpperCenter;
        竖.childControlWidth = true;
        竖.childControlHeight = false;
        竖.childForceExpandWidth = true;
        竖.childForceExpandHeight = false;

        // ---- 标题行 ----
        var 头 = UIBuildUtils.CreateRect("头", 框rt);
        头.sizeDelta = new Vector2(920f, 44f);
        var 头排 = 头.gameObject.AddComponent<HorizontalLayoutGroup>();
        头排.childAlignment = TextAnchor.MiddleLeft;
        头排.childControlWidth = true; 头排.childControlHeight = true;
        头排.childForceExpandWidth = true;

        标题文本 = UIBuildUtils.CreateText("标题", 头, 字体, "丹房 · 炼丹", 30, TextAnchor.MiddleLeft, 标题色);
        标题文本.rectTransform.sizeDelta = new Vector2(420f, 40f);
        灵气文本 = UIBuildUtils.CreateText("灵气", 头, 字体, "灵气 -", 20, TextAnchor.MiddleRight, 正文色);
        灵气文本.rectTransform.sizeDelta = new Vector2(480f, 40f);
        var 头le = 头.gameObject.AddComponent<LayoutElement>();
        头le.preferredHeight = 44f; 头le.minHeight = 44f;

        // ---- 主体：左丹方列表 + 右详情 ----
        var 主体 = UIBuildUtils.CreateRect("主体", 框rt);
        主体.sizeDelta = new Vector2(920f, 430f);
        var 主排 = 主体.gameObject.AddComponent<HorizontalLayoutGroup>();
        主排.spacing = 16f;
        主排.childAlignment = TextAnchor.UpperLeft;
        主排.childControlWidth = true; 主排.childControlHeight = true;
        主排.childForceExpandWidth = false;
        var 主le = 主体.gameObject.AddComponent<LayoutElement>();
        主le.preferredHeight = 430f; 主le.minHeight = 200f;

        // 左：丹方列表
        var 左 = UIBuildUtils.CreateImage("左栏", 主体, new Color(0.20f, 0.19f, 0.22f, 1f));
        var 左le = 左.gameObject.AddComponent<LayoutElement>();
        左le.preferredWidth = 240f; 左le.minWidth = 220f;
        var 左竖 = UIBuildUtils.AddVerticalLayout(左.rectTransform, 6f, new RectOffset(10, 10, 10, 10));
        左竖.childAlignment = TextAnchor.UpperCenter;
        左竖.childControlWidth = true; 左竖.childControlHeight = false;
        左竖.childForceExpandWidth = true; 左竖.childForceExpandHeight = false;
        丹方排 = 左.rectTransform;

        // 右：详情 + 品选择 + 炼制按钮
        var 右 = UIBuildUtils.CreateImage("右栏", 主体, new Color(0.20f, 0.19f, 0.22f, 1f));
        var 右le = 右.gameObject.AddComponent<LayoutElement>();
        右le.preferredWidth = 660f; 右le.minWidth = 400f;
        var 右竖 = UIBuildUtils.AddVerticalLayout(右.rectTransform, 8f, new RectOffset(14, 14, 12, 12));
        右竖.childAlignment = TextAnchor.UpperLeft;
        右竖.childControlWidth = true; 右竖.childControlHeight = false;
        右竖.childForceExpandWidth = true; 右竖.childForceExpandHeight = false;

        丹方详情 = UIBuildUtils.CreateText("详情", 右.rectTransform, 字体, "", 19,
            TextAnchor.UpperLeft, 正文色);
        丹方详情.rectTransform.sizeDelta = new Vector2(620f, 240f);
        var 详le = 丹方详情.gameObject.AddComponent<LayoutElement>();
        详le.preferredHeight = 240f; 详le.minHeight = 120f;

        var 品区 = UIBuildUtils.CreateRect("品区", 右.rectTransform);
        品区.sizeDelta = new Vector2(620f, 60f);
        var 品排组 = 品区.gameObject.AddComponent<HorizontalLayoutGroup>();
        品排组.spacing = 6f;
        品排组.childAlignment = TextAnchor.MiddleLeft;
        品排组.childControlWidth = false; 品排组.childControlHeight = true;
        品排组.childForceExpandWidth = false; 品排组.childForceExpandHeight = false;
        品排 = 品区;
        var 品le = 品区.gameObject.AddComponent<LayoutElement>();
        品le.preferredHeight = 60f; 品le.minHeight = 60f;

        炼制按钮 = UIBuildUtils.CreateButton("炼制", 右.rectTransform, 字体, "炼　制", 26);
        var 炼图 = 炼制按钮.GetComponent<Image>();
        炼图.raycastTarget = true;
        炼图.color = 按钮色;
        var 炼字 = 炼制按钮.GetComponentInChildren<Text>();
        炼字.color = 按钮字色;
        var 炼le = 炼制按钮.gameObject.AddComponent<LayoutElement>();
        炼le.preferredHeight = 58f; 炼le.minHeight = 58f;
        炼制按钮.onClick.AddListener(点炼制);

        // ---- 提示行 ----
        提示文本 = UIBuildUtils.CreateText("提示", 框rt, 字体, "", 19, TextAnchor.MiddleCenter, 正文色);
        提示文本.rectTransform.sizeDelta = new Vector2(900f, 30f);
        var 提le = 提示文本.gameObject.AddComponent<LayoutElement>();
        提le.preferredHeight = 30f; 提le.minHeight = 30f;

        // ---- 关闭 ----
        string 关字 = 开关按键 == KeyCode.None ? "关　闭（ESC / 再按 F）" : "关闭（" + 开关按键 + "）";
        var 关 = UIBuildUtils.CreateButton("关闭", 框rt, 字体, 关字, 20);
        var 关图 = 关.GetComponent<Image>();
        关图.raycastTarget = true;
        关图.color = 按钮暗色;
        关.GetComponentInChildren<Text>().color = 正文色;
        var 关le = 关.gameObject.AddComponent<LayoutElement>();
        关le.preferredHeight = 46f; 关le.minHeight = 46f;
        关.onClick.AddListener(关闭);
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
