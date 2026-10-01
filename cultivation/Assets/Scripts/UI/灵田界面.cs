using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **灵田总览（只读）** —— 默认 **F4** 开关，随时看一眼全局。
///
/// ## 它为什么变成"只读"了
///
/// 用户 2026-10-01 明确否掉了旧做法：
/// > 「我希望是一个很完整的灵田系统，可以直接和灵田互动、选择种植什么作物，
/// >   而不是**现在一个面板控制一片田地**。」
///
/// 所以**操作全部下放到地里**：走近某块地按 F，弹的是那一块地的小窗
/// （<see cref="灵田地块界面"/>）—— 种植、收获、挪动、升级它。
///
/// 这个 F4 界面留着的理由是"挂机玩法不该强迫玩家跑图"：
/// 站在宗门也能一眼看到每块地什么情况，并且**一键收取**（纯收割，不算操作玩法）。
///
/// ## 布局
///
/// ```
/// ┌────────────────────────────────────────────┐
/// │ 洞府灵田 · 总览                             │
/// │ 地块 4　待种植 1　已种 3　可收 1            │
/// ├────────────────────────────────────────────┤
/// │ 1 块田  一阶下品  生灵草  45%               │
/// │ 2 块田  一阶中品  已成熟 ← 可收             │
/// │ 3 块田  一阶下品  待种植（走近按 F 播种）    │
/// │ …                                          │
/// ├────────────────────────────────────────────┤
/// │ [ 一键收取 ]                 [ 关　闭 ]     │
/// └────────────────────────────────────────────┘
/// ```
///
/// ⚠️ **[坑] 幕布必须跟着面板一起关**：全屏幕布 + 吃射线 + sortingOrder 比 HUD 高，
/// 只要它没关，**整个 UI 都会点不动，而且 Console 里一个报错都没有**（踩坑 H25）。
/// 这里的做法是：幕布和面板都挂在同一个 Canvas 下，`同步幕布()` 在打开/关闭/**每帧兜底**
/// 都被调用。
/// </summary>
[DisallowMultipleComponent]
public class 灵田界面 : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("打开/关闭总览的按键")]
    public KeyCode 开关按键 = KeyCode.F4;

    [Tooltip("进游戏就自动打开（调试用）")]
    public bool 开局自动打开 = false;

    [Header("字体")]
    public Font 字体;

    [Header("配色")]
    public Color 幕布色 = new Color(0f, 0f, 0f, 0.72f);
    public Color 面板色 = new Color(0.16f, 0.14f, 0.12f, 0.97f);
    public Color 标题色 = new Color(0.97f, 0.93f, 0.80f, 1f);
    public Color 正文色 = new Color(0.88f, 0.87f, 0.83f, 1f);
    public Color 可收色 = new Color(1f, 0.85f, 0.42f, 1f);
    public Color 荒色 = new Color(0.62f, 0.60f, 0.55f, 1f);

    Canvas 画布;
    GameObject 幕布;
    RectTransform 面板;
    Text 汇总文本;
    Text 清单文本;
    Text 提示文本;

    float 提示到期 = -1f;

    public bool 已打开 => 面板 != null && 面板.gameObject.activeSelf;

    // ============================================================ 生命周期

    void Awake()
    {
        if (字体 == null) 字体 = 找字体();
        搭界面();
        关闭();
    }

    void Start()
    {
        var 田 = 灵田.取();
        if (田 != null)
        {
            田.变化 -= 刷新;
            田.变化 += 刷新;
        }
        刷新();
        if (开局自动打开) 打开();
    }

    void OnDestroy()
    {
        var 田 = 灵田.取();
        if (田 != null) 田.变化 -= 刷新;
    }

    void Update()
    {
        // 每帧兜底同步：万一有别的代码直接 SetActive 了面板，幕布也不会留在那儿挡点击
        同步幕布();

        if (Input.GetKeyDown(开关按键)) 切换();

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
        同步幕布();
        刷新();
    }

    public void 关闭()
    {
        if (面板 == null) return;
        面板.gameObject.SetActive(false);
        同步幕布();
    }

    /// <summary>
    /// 让幕布的显隐永远跟着面板。
    /// 【为什么是"幕布单独一个物体"而不是"面板的子物体"】—— 历史结构如此（见踩坑 H25），
    /// 所以只能用"每帧兜底同步"来保证两者不脱节。
    /// </summary>
    void 同步幕布()
    {
        if (幕布 == null || 面板 == null) return;
        bool 应显 = 面板.gameObject.activeSelf;
        if (幕布.activeSelf != 应显) 幕布.SetActive(应显);
    }

    // ============================================================ 搭界面

    void 搭界面()
    {
        var 根 = new GameObject("灵田总览", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        画布.sortingOrder = 2600;             // 高于 HUD，低于暂停菜单

        var scaler = 根.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---- 幕布 ----
        var 幕 = UIBuildUtils.CreateImage("幕布", 根.transform, 幕布色);
        幕.raycastTarget = true;             // ★ 必须吃射线，否则会点到后面的场景
        UIBuildUtils.Stretch(幕.rectTransform);
        幕布 = 幕.gameObject;

        // ---- 面板 ----
        var 面 = UIBuildUtils.CreateImage("面板", 根.transform, 面板色);
        面.raycastTarget = true;
        面板 = 面.rectTransform;
        面板.anchorMin = 面板.anchorMax = new Vector2(0.5f, 1f);
        面板.pivot = new Vector2(0.5f, 1f);
        面板.anchoredPosition = new Vector2(0f, -90f);
        面板.sizeDelta = new Vector2(720f, 620f);

        var 标 = UIBuildUtils.CreateText("标题", 面板, 字体, "洞府灵田 · 总览", 28,
            TextAnchor.MiddleLeft, 标题色);
        标.rectTransform.anchorMin = new Vector2(0f, 1f);
        标.rectTransform.anchorMax = new Vector2(1f, 1f);
        标.rectTransform.pivot = new Vector2(0.5f, 1f);
        标.rectTransform.anchoredPosition = new Vector2(0f, -14f);
        标.rectTransform.sizeDelta = new Vector2(-36f, 38f);

        汇总文本 = UIBuildUtils.CreateText("汇总", 面板, 字体, "", 19,
            TextAnchor.UpperLeft, 正文色);
        汇总文本.rectTransform.anchorMin = new Vector2(0f, 1f);
        汇总文本.rectTransform.anchorMax = new Vector2(1f, 1f);
        汇总文本.rectTransform.pivot = new Vector2(0.5f, 1f);
        汇总文本.rectTransform.anchoredPosition = new Vector2(0f, -56f);
        汇总文本.rectTransform.sizeDelta = new Vector2(-36f, 46f);

        清单文本 = UIBuildUtils.CreateText("清单", 面板, 字体, "", 18,
            TextAnchor.UpperLeft, 正文色);
        清单文本.rectTransform.anchorMin = new Vector2(0f, 0f);
        清单文本.rectTransform.anchorMax = new Vector2(1f, 1f);
        清单文本.rectTransform.offsetMin = new Vector2(18f, 84f);
        清单文本.rectTransform.offsetMax = new Vector2(-18f, -108f);
        清单文本.lineSpacing = 1.25f;
        // ★ `UIBuildUtils.CreateText` 默认 supportRichText = false（别的界面用不上富文本），
        //   而这里的清单要靠 <color> 把「可收 / 荒地」标出来。不打开的话标签会**照字面显示**，
        //   玩家看到的就是 `<color=#FFD96A>已成熟</color>`（实测踩到）。
        清单文本.supportRichText = true;

        提示文本 = UIBuildUtils.CreateText("提示", 面板, 字体, "", 17,
            TextAnchor.LowerLeft, 可收色);
        提示文本.rectTransform.anchorMin = new Vector2(0f, 0f);
        提示文本.rectTransform.anchorMax = new Vector2(1f, 0f);
        提示文本.rectTransform.pivot = new Vector2(0.5f, 0f);
        提示文本.rectTransform.anchoredPosition = new Vector2(0f, 56f);
        提示文本.rectTransform.sizeDelta = new Vector2(-36f, 30f);

        var 收 = UIBuildUtils.CreateButton("一键收取", 面板, 字体, "一键收取全部成熟", 20);
        var 收rt = 收.GetComponent<RectTransform>();
        收rt.anchorMin = new Vector2(0f, 0f);
        收rt.anchorMax = new Vector2(0f, 0f);
        收rt.pivot = new Vector2(0f, 0f);
        收rt.anchoredPosition = new Vector2(18f, 16f);
        收rt.sizeDelta = new Vector2(240f, 44f);
        收.onClick.AddListener(点一键收取);

        var 关 = UIBuildUtils.CreateButton("关闭", 面板, 字体, "关　闭（F4）", 20);
        var 关rt = 关.GetComponent<RectTransform>();
        关rt.anchorMin = new Vector2(1f, 0f);
        关rt.anchorMax = new Vector2(1f, 0f);
        关rt.pivot = new Vector2(1f, 0f);
        关rt.anchoredPosition = new Vector2(-18f, 16f);
        关rt.sizeDelta = new Vector2(180f, 44f);
        关.onClick.AddListener(关闭);
    }

    // ============================================================ 刷新

    void 刷新()
    {
        var 田 = 灵田.取();
        if (汇总文本 == null) return;
        if (田 == null)
        {
            汇总文本.text = "（找不到灵田）";
            清单文本.text = "";
            return;
        }

        var sb = new StringBuilder();

        if (田.总块数 == 0)
        {
            汇总文本.text = "还没有灵田";
            清单文本.text = "个人洞府里一块地都还没有。\n\n"
                          + "开垦办法：在背包里找到「" + 灵田.取物品名(灵田.开拓令id) + "」，"
                          + "点「使用」→ 鼠标会变成一块半透明的灵田 →\n"
                          + "在洞府里找个有地面的空地放下（[R] 旋转朝向，[左键] 放下，[右键] 取消）。\n\n"
                          + "⚠️ 只能在个人洞府里开垦。";
            return;
        }

        汇总文本.text = "地块 " + 田.总块数 + "　待种植 " + 田.待种数
                      + "　已种 " + 田.已种数 + "　可收 " + 田.可收数;

        for (int i = 0; i < 田.总块数; i++)
        {
            var b = 田.状态(i);
            if (b == null) continue;
            sb.Append((i + 1).ToString().PadLeft(2)).Append(" 块田　");
            sb.Append(灵田品阶说明.中文名(b.品阶).PadRight(5)).Append("　");

            if (b.待种植)
            {
                sb.Append(荒色标签("待种植（走近按 F 播种）"));
            }
            else
            {
                var d = b.植;
                sb.Append(d != null ? d.名 : b.作物id);
                if (b.已成熟) sb.Append("　").Append(可收色标签("已成熟 ← 可收"));
                else sb.Append("　").Append((b.进度 * 100f).ToString("0")).Append("%")
                      .Append("（还需 ").Append(b.剩余天数.ToString("0.0")).Append(" 天）");
            }
            sb.Append('\n');
        }
        sb.Append("\n操作要点：走近某块地按 F 就能种植 / 收获 / 挪动 / 升级这一块地；");
        sb.Append("想开新地用背包里的「").Append(灵田.取物品名(灵田.开拓令id)).Append("」。");
        清单文本.text = sb.ToString();
    }

    // uGUI 传统 Text 支持富文本，用颜色区分"荒 / 可收"最省事（不用为每行建一个 Text）
    string 可收色标签(string s) => "<color=#FFD96A>" + s + "</color>";
    string 荒色标签(string s) => "<color=#9E9A8E>" + s + "</color>";

    // ============================================================ 按钮

    void 点一键收取()
    {
        var 田 = 灵田.取();
        if (田 == null) return;
        string 明细;
        int 总 = 田.一键收取(out 明细);
        提示文本.text = 总 > 0 ? "收到：" + 明细 : 明细;
        提示到期 = Time.unscaledTime + 4f;
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
