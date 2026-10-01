using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **一块地的操作窗** —— 走近那块地按 F 弹出来的小窗。
///
/// 用户 2026-10-01 定死了里面的动作：
/// > 「按 F **只有种植/收获/挪动地块**。」
/// 加上他上一轮选的"升阶令 = 走到地前按 F 把这块地升品阶"，所以窗里一共四件事：
/// **种植 / 收获 / 挪动地块 / 升级这块地**。
/// （原来那个"开拓"没了 —— 开拓已经变成"从背包用开拓令把地摆下来"，见 <see cref="灵田摆放器"/>。）
///
/// ## 信息不在这里重复
///
/// "这块地什么情况"是**田块上方的信息牌**（<see cref="灵田地块牌"/>）负责的，
/// 走近就能看见、不用按 F。这个窗只负责**动手**，所以它刻意做得小。
///
/// ## 为什么这个窗口是"每块地一个"、由地块自己造
///
/// 它必须知道"是哪一块地"，而 `StationInteractable.界面预制体` 是个**共用预制体**，
/// 装不下这个上下文。所以和 `Teleporter` 一样：谁的地盘谁造面板
/// （见 `灵田地块.开面板()` 与 `StationInteractor` 里那条 `灵田地块` 分支）。
///
/// ## ⚠️ 幕布是"根本体"的子物体，一销毁就一起没了
///
/// 项目踩过很贵的坑：全屏幕布没跟着面板关 → **整个 UI 都点不动**（幕布全屏 + 吃射线，
/// Console 却没有任何报错，踩坑 H25）。这里从结构上杜绝：**幕布和面板建在同一个根下**，
/// 关窗口 = 销毁整个根，不存在"面板关了、幕布还在"的中间状态。
/// </summary>
[DisallowMultipleComponent]
public class 灵田地块界面 : MonoBehaviour
{
    // ============================================================ 配色

    static readonly Color 幕布色 = new Color(0f, 0f, 0f, 0.55f);
    static readonly Color 面板色 = new Color(0.16f, 0.14f, 0.12f, 0.98f);
    static readonly Color 标题色 = new Color(0.97f, 0.93f, 0.80f, 1f);
    static readonly Color 正文色 = new Color(0.88f, 0.87f, 0.83f, 1f);
    static readonly Color 按钮字色 = new Color(0.10f, 0.09f, 0.07f, 1f);
    static readonly Color 强调色 = new Color(0.78f, 0.52f, 0.33f, 1f);

    /// <summary>
    /// 按钮"不可点"时的字色。
    /// 【为什么不是把深色字调暗】uGUI 的 `Button` 在 `interactable = false` 时会把
    /// **底图**按 `disabledColor` 压暗，字不受影响 —— 底图已经暗了，
    /// 再把本就深的字调成半透明 ⇒ **深字压深底，直接看不见**（实测截到过）。
    /// </summary>
    static readonly Color 灰字色 = new Color(0.80f, 0.78f, 0.74f, 0.9f);

    const float 窗宽 = 500f;
    const float 窗高 = 570f;

    // ============================================================ 状态

    灵田地块 块;
    Font 字体;
    Text 标题文本, 状态文本, 提示文本;
    RectTransform 动作区;
    Button 主按钮;
    Text 主按钮字;
    Button 升级按钮;
    Text 升级按钮字;

    readonly List<Button> 种子按钮 = new List<Button>();
    readonly List<Text> 种子文字 = new List<Text>();
    string 选中灵植 = "";

    // ============================================================ 入口

    public static GameObject 造(灵田地块 目标)
    {
        var 根 = new GameObject("灵田地块界面", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = 根.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1800;              // 低于暂停菜单(3000)与设施占位幕布(2500)

        var scaler = 根.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var 窗 = 根.AddComponent<灵田地块界面>();
        窗.块 = 目标;
        窗.搭界面(根.transform);
        窗.订阅();
        窗.刷新();
        return 根;
    }

    // ============================================================ 搭界面

    void 搭界面(Transform 根)
    {
        字体 = 找字体();

        // ---- 幕布：全屏、**吃射线**（挡住底下的场景点击）----
        // `UIBuildUtils.CreateImage` 默认 raycastTarget = false，这里必须显式打开。
        var 幕布 = UIBuildUtils.CreateImage("幕布", 根, 幕布色);
        幕布.raycastTarget = true;
        UIBuildUtils.Stretch(幕布.rectTransform);

        // ---- 面板本体 ----
        var 面 = UIBuildUtils.CreateImage("面板", 根, 面板色);
        面.raycastTarget = true;
        var 面rt = 面.rectTransform;
        面rt.anchorMin = 面rt.anchorMax = new Vector2(0.5f, 0.5f);
        面rt.pivot = new Vector2(0.5f, 0.5f);
        面rt.anchoredPosition = Vector2.zero;
        面rt.sizeDelta = new Vector2(窗宽, 窗高);

        标题文本 = UIBuildUtils.CreateText("标题", 面rt, 字体, "", 26, TextAnchor.MiddleLeft, 标题色);
        锚顶(标题文本.rectTransform, -14f, 36f);

        状态文本 = UIBuildUtils.CreateText("状态", 面rt, 字体, "", 19, TextAnchor.UpperLeft, 正文色);
        锚顶(状态文本.rectTransform, -56f, 92f);

        // ---- 动作区：种子列表 / 主按钮 ----
        动作区 = UIBuildUtils.CreateRect("动作区", 面rt);
        动作区.anchorMin = new Vector2(0f, 0f);
        动作区.anchorMax = new Vector2(1f, 1f);
        动作区.offsetMin = new Vector2(16f, 178f);
        动作区.offsetMax = new Vector2(-16f, -156f);

        // ---- 底部：提示一行 + 挪动/升级 + 关闭 ----
        提示文本 = UIBuildUtils.CreateText("提示", 面rt, 字体, "", 15, TextAnchor.UpperLeft,
            new Color(1f, 0.72f, 0.40f));
        提示文本.rectTransform.anchorMin = new Vector2(0f, 0f);
        提示文本.rectTransform.anchorMax = new Vector2(1f, 0f);
        提示文本.rectTransform.pivot = new Vector2(0.5f, 0f);
        提示文本.rectTransform.anchoredPosition = new Vector2(0f, 122f);
        提示文本.rectTransform.sizeDelta = new Vector2(-32f, 48f);

        var 挪 = UIBuildUtils.CreateButton("挪动地块", 面rt, 字体, "挪动地块", 18);
        锚底(挪.GetComponent<RectTransform>(), new Vector2(16f, 70f), new Vector2(160f, 42f), false);
        挪.onClick.AddListener(点挪动);

        升级按钮 = UIBuildUtils.CreateButton("升级田地", 面rt, 字体, "升级这块地", 18);
        锚底(升级按钮.GetComponent<RectTransform>(), new Vector2(-16f, 70f), new Vector2(300f, 42f), true);
        升级按钮字 = 升级按钮.GetComponentInChildren<Text>();
        升级按钮.onClick.AddListener(点升级);

        var 关 = UIBuildUtils.CreateButton("关闭", 面rt, 字体, "关　闭（ESC）", 18);
        锚底(关.GetComponent<RectTransform>(), new Vector2(0f, 16f), new Vector2(220f, 42f), false);
        关.onClick.AddListener(() => 块.关面板());
    }

    static void 锚顶(RectTransform rt, float y, float 高)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-32f, 高);
    }

    /// <summary><paramref name="靠右"/> = 贴右边（否则贴左边）</summary>
    static void 锚底(RectTransform rt, Vector2 位置, Vector2 尺寸, bool 靠右)
    {
        float x = 靠右 ? 1f : 0f;
        rt.anchorMin = rt.anchorMax = new Vector2(x, 0f);
        rt.pivot = new Vector2(x, 0f);
        rt.anchoredPosition = 位置;
        rt.sizeDelta = 尺寸;
    }

    // ============================================================ 订阅

    void 订阅()
    {
        var 田 = 灵田.取();
        if (田 == null) return;
        田.变化 -= 刷新;
        田.变化 += 刷新;
    }

    void OnDestroy()
    {
        var 田 = 灵田.取();
        if (田 != null) 田.变化 -= 刷新;
    }

    // ============================================================ 刷新

    void 刷新()
    {
        var b = 块 != null ? 块.状态 : null;
        if (b == null || 状态文本 == null) return;

        标题文本.text = "第 " + (块.编号 + 1) + " 块地 · " + 灵田品阶说明.中文名(b.品阶);

        if (b.待种植)
        {
            状态文本.text = "待种植。\n这块地：" + 灵田品阶说明.中文名(b.品阶)
                          + "（产量 ×" + 灵田品阶说明.产量倍率(b.品阶).ToString("F2")
                          + "　生长 ×" + 灵田品阶说明.生长倍率(b.品阶).ToString("F2") + "）";
        }
        else
        {
            var d = b.植;
            状态文本.text = (d != null ? d.名 : b.作物id) + "　" + b.阶段名 + "\n"
                          + "生长进度 " + (b.进度 * 100f).ToString("0.#") + "%"
                          + (b.已成熟 ? "" : "　还需约 " + b.剩余天数.ToString("0.0") + " 天") + "\n"
                          + "这块地：" + 灵田品阶说明.中文名(b.品阶);
        }

        清空动作区();
        if (b.待种植) 建播种动作(b);
        else if (b.已成熟) 主按钮 = 建动作按钮("收　获", true, 点收获);
        else 主按钮 = 建动作按钮("还没熟，再等等", false, null);

        // ---- 升级这一行：需求一直写出来，不够就变灰并补一句为什么 ----
        string 升级原因 = "";
        bool 能升级 = 灵田.取() != null && 灵田.取().能升级(块.编号, out 升级原因);
        升级按钮.interactable = 能升级;
        升级按钮字.color = 能升级 ? 按钮字色 : 灰字色;
        string 需求 = 灵田.取() != null ? 灵田.取().升级需求文本(块.编号) : "";
        提示文本.text = b.已满阶 ? 需求
            : (能升级 ? "可升级！" + 需求 : 需求 + "　（" + 升级原因 + "）");
    }

    void 清空动作区()
    {
        种子按钮.Clear();
        种子文字.Clear();
        主按钮 = null;
        主按钮字 = null;
        for (int i = 动作区.childCount - 1; i >= 0; i--) Destroy(动作区.GetChild(i).gameObject);
    }

    // ---- 待种植：选种子 + 种下 ----
    void 建播种动作(灵田地块状态 b)
    {
        var 标题行 = UIBuildUtils.CreateText("种子标题", 动作区, 字体,
            "选一株种下去（灰的是这块地还种不了 / 没种子）：", 16, TextAnchor.UpperLeft,
            new Color(正文色.r, 正文色.g, 正文色.b, 0.75f));
        标题行.rectTransform.anchorMin = new Vector2(0f, 1f);
        标题行.rectTransform.anchorMax = new Vector2(1f, 1f);
        标题行.rectTransform.pivot = new Vector2(0.5f, 1f);
        标题行.rectTransform.anchoredPosition = Vector2.zero;
        标题行.rectTransform.sizeDelta = new Vector2(0f, 22f);

        var 田 = 灵田.取();
        var 排 = UIBuildUtils.CreateRect("种子排", 动作区);
        排.anchorMin = new Vector2(0f, 1f);
        排.anchorMax = new Vector2(1f, 1f);
        排.pivot = new Vector2(0.5f, 1f);
        排.anchoredPosition = new Vector2(0f, -28f);
        排.sizeDelta = new Vector2(0f, 32f * 5f + 5f * 4f);
        UIBuildUtils.AddVerticalLayout(排, 5f, new RectOffset(0, 0, 0, 0));

        foreach (var d in 灵植库.全部)
        {
            if (d == null) continue;
            string 不能原因 = "";
            bool 能 = 田 != null && 田.能种(块.编号, d.id, out 不能原因);
            int 有 = 灵田.数物品(d.种子id);

            // 【文案必须短、而且禁止换行】按钮上一换行就顶到下一行 ——
            // 原来把完整原因塞进按钮里，五行种子直接叠成一坨看不清（实测截到过）。
            string 文案 = d.名 + "　" + 灵田品阶说明.中文名(d.品阶) + "·种子" + 有;
            if (!能) 文案 += "　" + 短原因(不能原因);

            var btn = UIBuildUtils.CreateButton("种子_" + d.id, 排, 字体, 文案, 16);
            btn.interactable = 能;
            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 30f; le.preferredHeight = 30f;

            var 字 = btn.GetComponentInChildren<Text>();
            if (字 != null)
            {
                字.horizontalOverflow = HorizontalWrapMode.Overflow;
                字.alignment = TextAnchor.MiddleLeft;
                字.rectTransform.offsetMin = new Vector2(12f, 0f);
            }

            string id = d.id;
            btn.onClick.AddListener(() => { 选中灵植 = id; 刷新(); });
            if (选中灵植 == d.id) btn.GetComponent<Image>().color = 强调色;

            种子按钮.Add(btn);
            种子文字.Add(字);
        }

        bool 可种 = !string.IsNullOrEmpty(选中灵植) && 田 != null && 田.能种(块.编号, 选中灵植, out _);
        var 选中的 = 灵植库.取(选中灵植);
        主按钮 = 建动作按钮(选中的 != null ? "种下「" + 选中的.名 + "」" : "先在上面选一株", 可种, 点播种);
    }

    static string 短原因(string 完整)
    {
        if (string.IsNullOrEmpty(完整)) return "";
        if (完整.Contains("没有")) return "没种子";
        if (完整.Contains("需要")) return "品阶不够";
        if (完整.Contains("已经种")) return "已种";
        return "种不了";
    }

    Button 建动作按钮(string 文案, bool 可点, UnityEngine.Events.UnityAction 动作)
    {
        var btn = UIBuildUtils.CreateButton("主按钮", 动作区, 字体, 文案, 20);
        var rt = btn.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 44f);
        btn.interactable = 可点;
        if (可点 && 动作 != null) btn.onClick.AddListener(动作);
        主按钮字 = btn.GetComponentInChildren<Text>();
        if (!可点 && 主按钮字 != null) 主按钮字.color = 灰字色;
        return btn;
    }

    // ============================================================ 按钮动作
    //
    // 全部**只调 灵田（数据管理者）**，本窗口不自己改数据 —— 校验与执行只有一份。

    void 点播种()
    {
        var 田 = 灵田.取();
        if (田 != null && !田.播种(块.编号, 选中灵植))
        {
            田.能种(块.编号, 选中灵植, out string 原因);
            提示文本.text = "种不了：" + 原因;
        }
    }

    void 点收获()
    {
        var 田 = 灵田.取();
        if (田 == null) return;
        int 量 = 田.收一块(块.编号);
        提示文本.text = 量 > 0 ? "收到 " + 量 + " 株，已进背包" : "这块地还没成熟";
    }

    void 点升级()
    {
        var 田 = 灵田.取();
        if (田 != null && !田.升级(块.编号))
        {
            田.能升级(块.编号, out string 原因);
            提示文本.text = "升不了：" + 原因;
        }
    }

    /// <summary>
    /// 挪动：进摆放态，并**把这个窗关掉**。
    /// 【为什么必须关】摆放态要看地面和鼠标，窗口（尤其那块全屏幕布）会挡住画面和点击。
    /// </summary>
    void 点挪动()
    {
        if (灵田摆放器.开始挪动(块.编号, out string 原因))
            块.关面板();
        else
            提示文本.text = "挪不了：" + 原因;
    }

    // ============================================================ 字体

    static Font 找字体()
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
