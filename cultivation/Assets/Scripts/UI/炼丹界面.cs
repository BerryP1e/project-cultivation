using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **炼丹界面（丹房）** —— 布局照用户 2026-10-02 给的设计图；交互按用户 2026-10-02 第二轮要求。
///
/// ## 布局
///
/// ```
/// ┌──────────────────────────────────────────────────────────────┐
/// │                          丹房                    灵气 150/150 │
/// │  ┌────────────┐  ┌───────────────────────┐  ┌─────────────┐ │
/// │  │  成品结果   │  │ 各种信息的提供          │  │ 获得的丹方   │ │
/// │  │（点一下切回 │  │ 【材料】…               │  │ 或 背包      │ │
/// │  │  丹方列表） │  │ 【1 品丹】成功率…        │  │ （点材料格后 │ │
/// │  └────────────┘  │ 说明…                  │  │  切过来的）  │ │
/// │  ┌──────┐┌──────┐└───────────────────────┘  └─────────────┘ │
/// │  │主材   ││辅材   │ …… ×4              ┌───────────────┐    │
/// │  └──────┘└──────┘                     │    开  炼 ！   │    │
/// │        [清空材料]      提示行          └───────────────┘    │
/// │                        [ 关　闭（ESC / 再按 F） ]              │
/// └──────────────────────────────────────────────────────────────┘
/// ```
///
/// ## 交互（用户口径）
///
/// · **丹方要先"获得"才会列出来**：来源两种 —— 任务给（阶段24 打 `丹方_<id>` 标记）、
///   以及**只要炼出来过就算学会**（`炼丹炉.炼制()` 成功时自己补标记）。没学会的**不显示**。
/// · 材料够的丹方亮、不够的暗。
/// · **点主材 / 辅材格 → 右侧切成「背包」**，点背包里的东西就放进"当前选中的那一格"
///   （同一格再点同一样 = 数量 +1；点别的 = 换掉，数量回到 1）。
/// · **点成品结果框 → 右侧切回「丹方」**；点某个丹方 = 高亮它 + **自动把它的预设材料填进格子**。
/// · **「开炼！」**：点了丹方就按那个丹方炼；自己摆的材料则要求**与某个已学会丹方的配方完全一致**
///   （按 id 总数量比），不一致就提示「这些材料配不出丹方」，**不扣材料**。
///
/// ## 踩过的坑
///
/// 1. 幕布就是面板本体，**只 `SetActive` 它**（否则关不掉、全屏遮罩一直吃点击，踩坑 H25）。
/// 2. `UIBuildUtils.CreateImage` 默认 `raycastTarget = false`，可点的底图必须显式开。
/// 3. 布局组 `childControlHeight` 必须是 `true`，否则不认 `LayoutElement.preferredHeight`
///    （实测每行默认 100 高，4 条就被裁出可视区）。
/// 4. 材料格 / 预览方块的 **Image 要挂在格子自己身上**（刷新时直接改它的颜色，挂子物体上会 NRE）。
/// </summary>
[DisallowMultipleComponent]
public class 炼丹界面 : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("**默认关（None）**：本界面由 `StationInteractor` 实例化出来（= 就是要显示），关界面直接 Destroy。")]
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
    public Color 开炼色 = new Color(0.84f, 0.17f, 0.14f, 1f);
    public Color 警告色 = new Color(1f, 0.66f, 0.40f, 1f);
    public Color 选中格色 = new Color(0.52f, 0.44f, 0.26f, 1f);

    enum 右栏 { 丹方, 背包 }

    Canvas 画布;
    RectTransform 面板;                 // ★ 幕布本体（只 SetActive 它）
    Text 标题文本, 灵气文本, 信息文本, 预览名, 提示文本;
    RectTransform 预览方块, 丹方内容, 背包内容, 丹方板, 背包板;
    Button 开炼按钮;
    RectTransform[] 槽格;
    Text[] 槽字;
    readonly List<Button> 丹方按钮 = new List<Button>();
    readonly List<Text> 丹方文字 = new List<Text>();
    readonly List<Image> 丹方底 = new List<Image>();
    readonly List<Button> 背包按钮 = new List<Button>();
    readonly List<Text> 背包文字 = new List<Text>();

    // ---- 状态 ----
    右栏 当前栏 = 右栏.丹方;
    int 选中格 = -1;                        // -1 无；0 = 主材；1..4 = 辅材
    readonly string[] 槽物品 = new string[5];
    readonly int[] 槽数量 = new int[5];
    string 选中丹方 = "";

    float 提示到期 = -1f;
    float 下次刷新;

    public bool 已打开 => 面板 != null && 面板.gameObject.activeSelf;

    void Awake()
    {
        if (字体 == null) 取默认字体();
        搭界面();
        // ★【不要在这里 关闭()】StationInteractor 是 Instantiate 出来用的。
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
        if (面板 != null) UIInkMotion.提笔(面板.transform);
        if (面板 == null) return;
        面板.gameObject.SetActive(false);
    }

    // ============================================================ 交互

    /// <summary>点材料格 → 右侧切成背包，之后点背包里的东西就放这一格</summary>
    void 点格(int 序)
    {
        选中格 = 序;
        当前栏 = 右栏.背包;
        重建背包();
        切栏();
    }

    /// <summary>点成品结果框 → 右侧切回丹方列表</summary>
    void 点成品框()
    {
        当前栏 = 右栏.丹方;
        选中格 = -1;
        切栏();
    }

    /// <summary>点背包里的一样东西 → 放进当前选中的格子（同一样再点 = 数量 +1）</summary>
    void 点背包物品(string id)
    {
        if (选中格 < 0 || string.IsNullOrEmpty(id)) return;
        if (槽物品[选中格] == id) 槽数量[选中格] = 槽数量[选中格] + 1;
        else { 槽物品[选中格] = id; 槽数量[选中格] = 1; }
        选中丹方 = "";                 // 手动摆料 ⇒ 不再挂某个丹方
        刷新();
    }

    /// <summary>点丹方 → 高亮 + 自动把它的预设材料填进格子</summary>
    void 点丹方(string id)
    {
        var 炉 = 炼丹炉.取();
        var 丹方 = 炉 != null ? 炉.取丹方(id) : null;
        if (丹方 == null) return;

        选中丹方 = id;
        清空槽();
        var 材料 = 炉.全部材料(丹方);
        for (int i = 0; i < 材料.Count && i < 槽格.Length; i++)
        {
            槽物品[i] = 材料[i].Key;
            槽数量[i] = 材料[i].Value;
        }
        当前栏 = 右栏.丹方;
        选中格 = -1;
        刷新();
    }

    void 点清空()
    {
        清空槽();
        选中丹方 = "";
        提示("已清空材料。");
        刷新();
    }

    void 清空槽()
    {
        for (int i = 0; i < 槽物品.Length; i++) { 槽物品[i] = ""; 槽数量[i] = 0; }
    }

    /// <summary>
    /// 手动摆的材料**跟哪个已学会丹方的配方完全一致**（按 id 的总数量比）；
    /// 一个也对不上就返回空串（用户口径：不一致 ⇒ 配不出，不扣材料）。
    /// </summary>
    string 匹配配方(炼丹炉 炉)
    {
        var 我 = 摆料表();
        if (我.Count == 0) return "";
        foreach (var d in 炉.已学会的丹方())
        {
            var 它 = 炉.全部材料(d);
            if (它.Count != 我.Count) continue;
            bool 一样 = true;
            foreach (var kv in 它)
            {
                int 有;
                if (!我.TryGetValue(kv.Key, out 有) || 有 != kv.Value) { 一样 = false; break; }
            }
            if (一样) return d.id;
        }
        return "";
    }

    Dictionary<string, int> 摆料表()
    {
        var 表 = new Dictionary<string, int>();
        for (int i = 0; i < 槽物品.Length; i++)
        {
            if (string.IsNullOrEmpty(槽物品[i]) || 槽数量[i] <= 0) continue;
            int 旧;
            表.TryGetValue(槽物品[i], out 旧);
            表[槽物品[i]] = 旧 + 槽数量[i];
        }
        return 表;
    }

    void 点开炼()
    {
        var 炉 = 炼丹炉.取();
        if (炉 == null) return;

        string 要炼 = 选中丹方;
        if (string.IsNullOrEmpty(要炼)) 要炼 = 匹配配方(炉);
        if (string.IsNullOrEmpty(要炼))
        {
            提示("这些材料配不出丹方（要和某个已学会丹方的配方完全一致）。", 警告色);
            return;
        }
        var r = 炉.炼制(要炼);
        处理炼制完成(r);
        刷新();
    }

    void 处理炼制完成(炼丹结果 r)
    {
        提示(r.文本, r.成功 ? 强调色 : (r.受理 ? 警告色 : 正文色), 5f);
    }

    void 提示(string 文本, Color 色 = default(Color), float 秒 = 3f)
    {
        if (提示文本 == null) return;
        提示文本.text = 文本;
        提示文本.color = 色 == default(Color) ? 正文色 : 色;
        提示到期 = Time.unscaledTime + 秒;
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

        刷新槽();
        刷新丹方列表();
        if (当前栏 == 右栏.背包) 重建背包();
        切栏();
        刷新信息与按钮(炉);
    }

    void 切栏()
    {
        if (丹方板 != null) 丹方板.gameObject.SetActive(当前栏 == 右栏.丹方);
        if (背包板 != null) 背包板.gameObject.SetActive(当前栏 == 右栏.背包);
    }

    void 刷新槽()
    {
        var 板 = FindObjectOfType<UIPanelData>();
        for (int i = 0; i < 槽格.Length; i++)
        {
            if (槽格[i] == null || 槽字[i] == null) continue;
            string id = 槽物品[i];
            bool 有料 = !string.IsNullOrEmpty(id) && 槽数量[i] > 0;
            if (!有料)
            {
                槽字[i].text = i == 0 ? "（点一下放主材）" : "（点一下放辅材）";
                槽字[i].color = 灰字色;
            }
            else
            {
                var 定义 = 取物品(id);
                int 有 = 定义 != null && 板 != null ? 板.物品数量(定义) : 0;
                string 名 = 定义 != null ? 定义.物品名 : id;
                槽字[i].text = 名 + "\n×" + 槽数量[i] + "\n(有 " + 有 + ")";
                槽字[i].color = 有 >= 槽数量[i] ? 正文色 : 警告色;
            }
            // 正在选的那一格高亮
            槽格[i].GetComponent<Image>().color = (i == 选中格) ? 选中格色 : new Color(0.24f, 0.23f, 0.26f, 1f);
            if (InkUITheme.Enabled)
            {
                bool 足够 = !有料 || (取物品(id) != null && 板 != null && 板.物品数量(取物品(id)) >= 槽数量[i]);
                string state = !足够 ? "disabled" : i == 选中格 ? "selected" : 有料 ? "filled" : "empty";
                InkUITheme.Image(槽格[i].GetComponent<Image>(), "CultivationAlchemy/material-slot-" + state);
                槽字[i].color = 足够 ? InkUITheme.Ink : new Color(.65f, .24f, .18f);
            }
        }
    }

    void 刷新信息与按钮(炼丹炉 炉)
    {
        var 板 = FindObjectOfType<UIPanelData>();
        var 丹方 = !string.IsNullOrEmpty(选中丹方) ? 炉.取丹方(选中丹方) : null;
        string 手动匹配 = 丹方 == null ? 匹配配方(炉) : "";
        var 结算 = 丹方 != null ? 丹方 : (string.IsNullOrEmpty(手动匹配) ? null : 炉.取丹方(手动匹配));

        var sb = new StringBuilder();
        if (结算 != null)
        {
            sb.AppendLine("【材料】");
            foreach (var kv in 炉.全部材料(结算))
            {
                var 定义 = 取物品(kv.Key);
                int 有 = 定义 != null && 板 != null ? 板.物品数量(定义) : 0;
                string 名 = 定义 != null ? 定义.物品名 : kv.Key;
                sb.AppendLine($"　{名} ×{kv.Value}　(有 {有}){(有 >= kv.Value ? "" : "　✗")}");
            }
            sb.AppendLine();
            sb.AppendLine($"【{结算.品} 品丹】成功率 {炉.实际成功率(结算):P0}　耗灵气 {炉.实际耗气(结算)}");
            sb.AppendLine(结算.说明);
            if (丹方 == null) sb.AppendLine();
            if (丹方 == null) sb.AppendLine("（你自己摆的材料和「" + 结算.名 + "」的配方一致）");
        }
        else
        {
            sb.AppendLine("【材料】");
            sb.AppendLine("　点下面的「主材」「辅材」格 → 右边会变成背包，点一下就放进去。");
            sb.AppendLine("　同一格再点同一样 = 数量 +1；点别的 = 换掉。");
            sb.AppendLine();
            sb.AppendLine("　也可以点「成品结果」框切回丹方列表，直接点一个丹方 —— 会自动把预设材料摆好。");
            if (摆料表().Count > 0) sb.AppendLine("\n（这些材料配不出丹方 —— 要和某个已学会丹方的配方完全一致）");
        }
        if (信息文本 != null) 信息文本.text = sb.ToString();

        // ---- 成品结果框 ----
        if (预览名 != null)
            预览名.text = 结算 != null ? (结算.名 + "\n" + 结算.品 + " 品") : "?\n（配不出）";
        if (预览方块 != null)
            预览方块.GetComponent<Image>().color = InkUITheme.Enabled ? Color.clear : 结算 != null
                ? new Color(0.78f, 0.70f, 0.42f, 1f)
                : new Color(0.35f, 0.33f, 0.36f, 1f);
        // 保留点击成品区域切回丹方列表，透明命中区不会盖住丹炉插画。
        if (InkUITheme.Enabled && 预览方块 != null)
            预览方块.GetComponent<Button>().transition = Selectable.Transition.None;

        // ---- 开炼按钮 ----
        string 原因 = "";
        bool 可以 = false;
        if (结算 != null) 可以 = 炉.能炼(结算, out 原因);
        else if (摆料表().Count == 0) 原因 = "先摆材料，或点一个丹方。";
        else 原因 = "这些材料配不出丹方。";
        if (开炼按钮 != null)
        {
            开炼按钮.interactable = 可以;
            开炼按钮.GetComponent<Image>().color = 可以 ? 开炼色 : 按钮暗色;
            InkUITheme.Button(开炼按钮, "cinnabar");
        }
        if (!可以 && 提示文本 != null && string.IsNullOrEmpty(提示文本.text)) 提示文本.text = 原因;
    }

    void 刷新丹方列表()
    {
        var 炉 = 炼丹炉.取();
        var 丹方们 = 炉.已学会的丹方();
        if (丹方按钮.Count != 丹方们.Count) 重建丹方(丹方们.Count);
        for (int i = 0; i < 丹方按钮.Count && i < 丹方们.Count; i++)
        {
            var d = 丹方们[i];
            string 原因;
            bool 够 = 炉.能炼(d, out 原因);
            bool 选 = d.id == 选中丹方;
            丹方文字[i].text = 够 ? d.名 : d.名 + "（缺料）";
            丹方底[i].color = 选 ? 按钮色 : (够 ? new Color(0.42f, 0.38f, 0.30f, 1f) : new Color(0.24f, 0.23f, 0.26f, 1f));
            丹方文字[i].color = 选 ? 按钮字色 : (够 ? 正文色 : 灰字色);
            if (InkUITheme.Enabled)
            {
                InkUITheme.Choice(丹方按钮[i], "CultivationAlchemy/recipe-row", 选);
                丹方文字[i].color = 够 ? InkUITheme.Ink : new Color(.6f, .26f, .18f);
            }
        }
    }

    void 重建丹方(int n)
    {
        foreach (var b in 丹方按钮) if (b != null) Destroy(b.gameObject);
        丹方按钮.Clear(); 丹方文字.Clear(); 丹方底.Clear();
        if (丹方内容 == null) return;
        var 炉 = 炼丹炉.取();
        for (int i = 0; i < n; i++)
        {
            string id = 炉.已学会的丹方()[i].id;
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

    /// <summary>背包：**全部物品**（用户口径），点一下就放进当前选中的格子</summary>
    void 重建背包()
    {
        var 板 = FindObjectOfType<UIPanelData>();
        // ⚠️ `UIPanelData.物品` 是**按"堆"存的**（每 `给物品` 一次就多一条），
        //    直接遍历会出来一堆重复行（实测生灵草重复 7 行、回春丹 3 行）。必须**按 id 去重**，
        //    数量用 `物品数量(定义)`（它是合计）。
        var 表 = new Dictionary<string, ItemDefinition>();
        var 序 = new List<string>();
        if (板 != null)
            foreach (var d in 板.物品)
            {
                if (d == null || string.IsNullOrEmpty(d.物品id)) continue;
                if (表.ContainsKey(d.物品id)) continue;
                表[d.物品id] = d;
                序.Add(d.物品id);
            }
        var 物品们 = new List<ItemDefinition>();
        foreach (var id in 序)
        {
            var d = 表[id];
            if (板.物品数量(d) > 0) 物品们.Add(d);
        }

        // 行数变了才重建（否则每 0.35 秒重建一次会把滚动位置也重置）
        if (背包按钮.Count != 物品们.Count)
        {
            foreach (var b in 背包按钮) if (b != null) Destroy(b.gameObject);
            背包按钮.Clear(); 背包文字.Clear();
            for (int i = 0; i < 物品们.Count; i++)
            {
                string id = 物品们[i].物品id;
                var b = UIBuildUtils.CreateButton("背包" + i, 背包内容, 字体, "", 19);
                var 图 = b.GetComponent<Image>();
                图.raycastTarget = true;
                图.color = new Color(0.34f, 0.32f, 0.30f, 1f);
                var lbl = b.GetComponentInChildren<Text>();
                lbl.alignment = TextAnchor.MiddleLeft;
                lbl.color = 正文色;
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 44f; le.minHeight = 44f;
                b.onClick.AddListener(() => 点背包物品(id));
                背包按钮.Add(b); 背包文字.Add(lbl);
            }
        }
        // 数量每帧都可能变 → 文本每次刷新
        for (int i = 0; i < 背包按钮.Count && i < 物品们.Count; i++)
        {
            var d = 物品们[i];
            int 有 = 板 != null ? 板.物品数量(d) : 0;
            背包文字[i].text = d.物品名 + "　×" + 有;
            bool 选中 = 选中格 >= 0 && 槽物品[选中格] == d.物品id;
            背包按钮[i].GetComponent<Image>().color = 选中 ? 按钮色 : new Color(0.34f, 0.32f, 0.30f, 1f);
            背包文字[i].color = 选中 ? 按钮字色 : 正文色;
            if (InkUITheme.Enabled)
            {
                InkUITheme.Choice(背包按钮[i], "SkillsPage/skills/row", 选中);
                背包文字[i].color = InkUITheme.Ink;
            }
        }
    }

    // ============================================================ 搭界面

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

    /// <summary>材料格：**Image 挂在格子自己身上**，并挂一个 Button（要能点）</summary>
    static Image 放格(RectTransform 父, string 名, float x, float y, float w, float h)
    {
        var rt = 放(父, 名, x, y, w, h);
        var 图 = rt.gameObject.AddComponent<Image>();
        图.color = new Color(0.24f, 0.23f, 0.26f, 1f);
        图.raycastTarget = true;
        rt.gameObject.AddComponent<Button>();
        return 图;
    }

    void 搭界面()
    {
        if (画布 != null) return;

        var 根 = new GameObject("AlchemyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        画布.sortingOrder = 2650;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        var 幕 = UIBuildUtils.CreateImage("炼丹幕布", 根.transform, 幕布色);
        幕.raycastTarget = true;
        UIBuildUtils.Stretch(幕.rectTransform);
        面板 = 幕.rectTransform;

        var 框图 = UIBuildUtils.CreateImage("内容框", 面板, 面板色);
        框图.raycastTarget = true;
        var 框 = 框图.rectTransform;
        框.anchorMin = new Vector2(0.5f, 0.5f);
        框.anchorMax = new Vector2(0.5f, 0.5f);
        框.pivot = new Vector2(0.5f, 0.5f);
        框.sizeDelta = new Vector2(1560f, 880f);
        框.anchoredPosition = Vector2.zero;

        // ---- 标题 + 灵气 ----
        标题文本 = UIBuildUtils.CreateText("标题", 放(框, "标题位", 0f, 16f, 1560f, 56f), 字体, "丹房", 44, TextAnchor.MiddleCenter, 标题色);
        UIBuildUtils.Stretch(标题文本.rectTransform);
        灵气文本 = UIBuildUtils.CreateText("灵气", 放(框, "灵气位", 1100f, 26f, 420f, 40f), 字体, "灵气 -", 20, TextAnchor.MiddleRight, 正文色);
        UIBuildUtils.Stretch(灵气文本.rectTransform);

        // ---- 左：成品结果（**可点 → 切回丹方列表**）----
        var 预览板 = 放板(框, "成品预览", 40f, 100f, 360f, 260f, 板色);
        var 方块图 = 放格(预览板.rectTransform, "预览方块", 20f, 20f, 320f, 150f);
        方块图.color = new Color(0.35f, 0.33f, 0.36f, 1f);
        方块图.GetComponent<Button>().onClick.AddListener(点成品框);
        预览方块 = 方块图.rectTransform;
        预览名 = UIBuildUtils.CreateText("预览名", 放(预览板.rectTransform, "预览名位", 20f, 178f, 320f, 66f), 字体, "?", 22, TextAnchor.UpperCenter, 标题色);
        UIBuildUtils.Stretch(预览名.rectTransform);

        // ---- 中：各种信息的提供 ----
        var 信息板 = 放板(框, "信息板", 420f, 100f, 700f, 260f, 板色);
        var 信息标题 = UIBuildUtils.CreateText("信息标题", 放(信息板.rectTransform, "信息标题位", 18f, 12f, 664f, 30f), 字体, "各种信息的提供", 20, TextAnchor.MiddleLeft, 灰字色);
        UIBuildUtils.Stretch(信息标题.rectTransform);
        信息文本 = UIBuildUtils.CreateText("信息", 放(信息板.rectTransform, "信息位", 18f, 46f, 664f, 200f), 字体, "", 19, TextAnchor.UpperLeft, 正文色);
        UIBuildUtils.Stretch(信息文本.rectTransform);

        // ---- 右上：丹方板 / 背包板（同一个位置，二选一显示）----
        var 右外 = 放(框, "右栏位", 1140f, 100f, 380f, 460f);
        丹方板 = 建右板(右外, "丹方板", "获得的丹方",
            "（只要炼出来过就能学会，没学会的不显示；材料够就亮，不够就暗）", out 丹方内容);
        背包板 = 建右板(右外, "背包板", "背包",
            "（点一下放进当前选中的格子；同一格再点同一样 = 数量 +1）", out 背包内容);
        背包板.gameObject.SetActive(false);

        // ---- 下：主材 1 格 + 辅材 4 格（**都要能点**）----
        槽格 = new RectTransform[5];
        槽字 = new Text[5];
        for (int i = 0; i < 5; i++)
        {
            string 名 = i == 0 ? "主材格" : ("辅材格" + (i - 1));
            float x = i == 0 ? 40f : 260f + (i - 1) * 216f;
            var 图 = 放格(框, 名, x, 384f, 200f, 132f);
            int 序 = i;
            图.GetComponent<Button>().onClick.AddListener(() => 点格(序));
            槽格[i] = 图.rectTransform;
            槽字[i] = 格内字(槽格[i], i == 0 ? "主材" : "辅材");
        }

        // ---- 清空材料 ----
        var 清 = UIBuildUtils.CreateButton("清空", 放(框, "清空位", 40f, 530f, 200f, 46f), 字体, "清空材料", 18);
        UIBuildUtils.Stretch(清.GetComponent<RectTransform>());
        清.GetComponent<Image>().raycastTarget = true;
        清.GetComponent<Image>().color = 按钮暗色;
        清.GetComponentInChildren<Text>().color = 正文色;
        清.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
        清.onClick.AddListener(点清空);

        // ---- 右下：大红「开炼！」----
        开炼按钮 = UIBuildUtils.CreateButton("开炼", 放(框, "开炼位", 1140f, 580f, 380f, 170f), 字体, "开炼！", 46);
        UIBuildUtils.Stretch(开炼按钮.GetComponent<RectTransform>());
        var 炼图 = 开炼按钮.GetComponent<Image>();
        炼图.raycastTarget = true;
        炼图.color = 开炼色;
        var 炼字 = 开炼按钮.GetComponentInChildren<Text>();
        炼字.color = new Color(1f, 0.97f, 0.94f, 1f);
        炼字.alignment = TextAnchor.MiddleCenter;
        炼字.fontSize = 46;
        开炼按钮.onClick.AddListener(点开炼);

        // ---- 提示行 + 关闭 ----
        提示文本 = UIBuildUtils.CreateText("提示", 放(框, "提示位", 260f, 540f, 860f, 40f), 字体, "", 19, TextAnchor.MiddleLeft, 正文色);
        UIBuildUtils.Stretch(提示文本.rectTransform);

        string 关字 = 开关按键 == KeyCode.None ? "关　闭（ESC / 再按 F）" : "关闭（" + 开关按键 + "）";
        var 关 = UIBuildUtils.CreateButton("关闭", 放(框, "关闭位", 40f, 800f, 1560f, 48f), 字体, 关字, 20);
        UIBuildUtils.Stretch(关.GetComponent<RectTransform>());
        关.GetComponent<Image>().raycastTarget = true;
        关.GetComponent<Image>().color = 按钮暗色;
        关.GetComponentInChildren<Text>().color = 正文色;
        关.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
        关.onClick.AddListener(关闭);
    }

    /// <summary>右上那两块板（丹方板 / 背包板）结构一样：标题 + 提示 + 可滚动列表</summary>
    RectTransform 建右板(RectTransform 父, string 名, string 标题, string 提示, out RectTransform 内容)
    {
        var 板 = 放板(父, 名, 0f, 0f, 380f, 460f, 板色);
        var 标 = UIBuildUtils.CreateText("标", 放(板.rectTransform, "标位", 16f, 12f, 348f, 32f), 字体, 标题, 22, TextAnchor.MiddleLeft, 标题色);
        UIBuildUtils.Stretch(标.rectTransform);
        var 提 = UIBuildUtils.CreateText("提", 放(板.rectTransform, "提位", 16f, 48f, 348f, 52f), 字体, 提示, 15, TextAnchor.UpperLeft, 灰字色);
        UIBuildUtils.Stretch(提.rectTransform);

        var 视口 = 放(板.rectTransform, "视口", 16f, 106f, 348f, 338f);
        视口.gameObject.AddComponent<RectMask2D>();
        内容 = UIBuildUtils.CreateRect("内容", 视口);
        内容.anchorMin = new Vector2(0f, 1f);
        内容.anchorMax = new Vector2(1f, 1f);
        内容.pivot = new Vector2(0.5f, 1f);
        内容.sizeDelta = Vector2.zero;
        内容.anchoredPosition = Vector2.zero;
        var 竖 = UIBuildUtils.AddVerticalLayout(内容, 6f, new RectOffset(0, 0, 0, 0));
        竖.childAlignment = TextAnchor.UpperCenter;
        竖.childControlWidth = true;
        竖.childControlHeight = true;      // ★ 必须 true，否则不认 LayoutElement 的高度
        竖.childForceExpandWidth = true; 竖.childForceExpandHeight = false;
        内容.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var 滚 = 板.gameObject.AddComponent<ScrollRect>();
        滚.viewport = 视口; 滚.content = 内容;
        滚.horizontal = false; 滚.vertical = true;
        滚.movementType = ScrollRect.MovementType.Clamped;
        滚.scrollSensitivity = 30f;
        return 板.rectTransform;
    }

    Text 格内字(RectTransform 格, string 小标题)
    {
        var 标题 = UIBuildUtils.CreateText("格标题", 放(格, "格标题位", 10f, 6f, 180f, 24f), 字体, 小标题, 15, TextAnchor.UpperLeft, 灰字色);
        UIBuildUtils.Stretch(标题.rectTransform);
        var 字 = UIBuildUtils.CreateText("格字", 放(格, "格字位", 10f, 30f, 180f, 96f), 字体, "—", 18, TextAnchor.MiddleCenter, 正文色);
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
