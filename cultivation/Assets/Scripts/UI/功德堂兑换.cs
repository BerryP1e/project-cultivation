using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **功德堂兑换** —— 用**宗门贡献**换东西（灵田开拓令 / 升阶令 / 练功木桩 / 灵种）。
///
/// 用户需求原文里，这些东西的来路就是「**在宗门贡献处兑换的道具**」：
/// 灵田升阶令、灵田开拓令、练功木桩在此之前**游戏里没有任何来源**（只能靠 F1 面板发），
/// 这个界面就是补上那条正路。
///
/// ## 一、卖什么：**直接扫物品库，不另建兑换表**
///
/// 判据只有一个：`ItemDefinition.兑换消耗贡献 > 0`。
/// 好处是"表里加一行就自动上架"（本工程绝大多数物品都是这么进来的），
/// 不用维护第二份清单 —— 两份清单必然分叉（踩坑总库 A10 那条"NPC 表不是唯一来源"就是同款教训）。
///
/// ## 二、为什么面板自己画、而不是挂 `界面预制体`
///
/// 和「传送圈 / 灵田地块 / 炼丹炉」一样：**行数取决于数据**（上架几件就有几行），
/// 预制体表达不了。所以照 `StationInteractor.打开界面` 里那两条分支的做法：
/// 组件自己造面板、自己开自己关，`当前界面` 交给交互器记着（否则 ESC 关不掉）。
///
/// ## 三、贡献从哪来
///
/// 打镇妖塔（每清空一波给一点，见 `TowerController`）+ 任务表 `奖励贡献` 列。
/// 这里只负责**花**。
/// </summary>
[DisallowMultipleComponent]
public class 功德堂兑换 : MonoBehaviour
{
    [Header("外观")]
    public Font 字体;
    public Color 幕布色 = new Color(0f, 0f, 0f, 0.55f);
    public Color 底板色 = new Color(0.06f, 0.06f, 0.08f, 0.96f);
    public Color 标题色 = new Color(1f, 0.84f, 0.42f);
    public Color 正常色 = new Color(0.92f, 0.92f, 0.88f);
    public Color 不足色 = new Color(0.72f, 0.45f, 0.42f);
    public Color 按钮可点色 = new Color(0.20f, 0.42f, 0.26f);
    public Color 按钮灰色 = new Color(0.24f, 0.24f, 0.26f);
    public int 字号 = 22;
    public float 行高 = 54f;
    public float 面板宽 = 900f;

    [Header("调试")]
    public bool 打印日志 = false;

    Canvas 画布;
    Image 幕布;
    RectTransform 面板;
    RectTransform 兑换内容;
    readonly List<GameObject> 兑换行 = new List<GameObject>();
    Text 贡献文本, 提示文本;
    readonly List<Button> 按钮s = new List<Button>();
    readonly List<Text> 行文本s = new List<Text>();
    readonly List<ItemDefinition> 上架的 = new List<ItemDefinition>();

    /// <summary>面板根（交给 `StationInteractor.当前界面`，ESC 才关得掉）</summary>
    public GameObject 面板根 => 面板 != null ? 面板.gameObject : null;
    public bool 面板已开 => 面板 != null && 面板.gameObject.activeSelf;

    void Awake()
    {
        if (字体 == null) 取默认字体();
        宗门贡献.变化 += 处理贡献变化;
    }

    void OnDestroy() { 宗门贡献.变化 -= 处理贡献变化; }
    void Update() => 同步幕布();
    void 同步幕布() { if (幕布 != null) 幕布.gameObject.SetActive(面板已开); }

    void 处理贡献变化(int 值) { if (面板已开) { 刷新贡献文本(); 刷新按钮(); } }

    public void 开面板()
    {
        if (面板 == null) 建面板();
        面板.gameObject.SetActive(true);
        同步幕布();
        刷新();
        if (打印日志) Debug.Log("[功德堂] 打开兑换（上架 " + 上架的.Count + " 件，贡献 " + 宗门贡献.当前 + "）", this);
    }

    public void 关面板() { if (面板 != null) 面板.gameObject.SetActive(false); 同步幕布(); UiEscRegistry.记录关闭(); }

    // ============================================================ 数据

    void 收上架的()
    {
        上架的.Clear();
        var 库 = QuestDatabase.取();     // 运行时物品库（任务库资产里带着 物品库 那份清单）
        if (库 == null || 库.物品库 == null)
        {
            Debug.LogWarning("[功德堂] 取不到物品库 —— 跑一次「修仙/从配置表生成资产」", this);
            return;
        }
        foreach (var it in 库.物品库)
            if (it != null && it.兑换消耗贡献 > 0) 上架的.Add(it);
        上架的.Sort((a, b) =>
        {
            int c = a.兑换消耗贡献.CompareTo(b.兑换消耗贡献);
            return c != 0 ? c : string.CompareOrdinal(a.DisplayName, b.DisplayName);
        });
    }

    // ============================================================ 兑换

    void 买(int 下标)
    {
        if (下标 < 0 || 下标 >= 上架的.Count) return;
        var it = 上架的[下标];
        if (!宗门贡献.够(it.兑换消耗贡献))
        {
            说("贡献不够：还差 " + (it.兑换消耗贡献 - 宗门贡献.当前));
            return;
        }
        if (!宗门贡献.扣(it.兑换消耗贡献, "兑换「" + it.DisplayName + "」")) return;

        灵田.给道具(it.物品id, 1);      // 本工程"往背包塞一件东西"的公共入口
        说("换到了「" + it.DisplayName + "」");
        Debug.Log("[功德堂] 兑换「" + it.DisplayName + "」花 " + it.兑换消耗贡献
                  + " 贡献，剩 " + 宗门贡献.当前, it);
        刷新贡献文本();
        刷新按钮();
    }

    // ============================================================ 界面

    void 取默认字体()
    {
#if UNITY_EDITOR
        字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
#endif
        if (字体 == null)
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                if (f != null && f.name.ToLowerInvariant().Contains("simhei")) { 字体 = f; return; }
    }

    void 建面板()
    {
        var 根 = new GameObject("功德堂Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        画布.sortingOrder = 2600;            // 和炼丹界面一档：高于 HUD / 追踪，低于暂停菜单
        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);

        var 幕 = UIBuildUtils.CreateImage("幕布", 根.transform, 幕布色);
        幕布 = 幕;
        幕.raycastTarget = true;             // 挡住射线，别点穿到背后
        UIBuildUtils.Stretch(幕.rectTransform);

        面板 = UIBuildUtils.CreateImage("功德堂", 根.transform, 底板色).rectTransform;
        面板.anchorMin = 面板.anchorMax = new Vector2(0.5f, 0.5f);
        面板.pivot = new Vector2(0.5f, 0.5f);
        面板.sizeDelta = new Vector2(面板宽, 640f);
        面板.anchoredPosition = Vector2.zero;

        var 标题 = UIBuildUtils.CreateText("标题", 面板, 字体, "功德堂 · 贡献兑换", 34,
            TextAnchor.MiddleCenter, 标题色);
        标题.rectTransform.anchorMin = new Vector2(0f, 1f);
        标题.rectTransform.anchorMax = new Vector2(1f, 1f);
        标题.rectTransform.pivot = new Vector2(0.5f, 1f);
        标题.rectTransform.sizeDelta = new Vector2(0f, 56f);
        标题.rectTransform.anchoredPosition = new Vector2(0f, -12f);

        贡献文本 = UIBuildUtils.CreateText("贡献", 面板, 字体, "", 字号,
            TextAnchor.MiddleCenter, 正常色);
        贡献文本.rectTransform.anchorMin = new Vector2(0f, 1f);
        贡献文本.rectTransform.anchorMax = new Vector2(1f, 1f);
        贡献文本.rectTransform.pivot = new Vector2(0.5f, 1f);
        贡献文本.rectTransform.sizeDelta = new Vector2(0f, 34f);
        贡献文本.rectTransform.anchoredPosition = new Vector2(0f, -70f);

        提示文本 = UIBuildUtils.CreateText("提示", 面板, 字体, "", 20,
            TextAnchor.MiddleCenter, 标题色);
        提示文本.rectTransform.anchorMin = new Vector2(0f, 0f);
        提示文本.rectTransform.anchorMax = new Vector2(1f, 0f);
        提示文本.rectTransform.pivot = new Vector2(0.5f, 0f);
        提示文本.rectTransform.sizeDelta = new Vector2(0f, 30f);
        提示文本.rectTransform.anchoredPosition = new Vector2(0f, 62f);

        var 关 = UIBuildUtils.CreateButton("关闭", 面板, 字体, "关闭", 24);
        关.name = "关闭";
        var 关rt = 关.GetComponent<RectTransform>();
        关rt.anchorMin = 关rt.anchorMax = new Vector2(0.5f, 0f);
        关rt.pivot = new Vector2(0.5f, 0f);
        关rt.sizeDelta = new Vector2(220f, 46f);
        关rt.anchoredPosition = new Vector2(0f, 10f);
        关.GetComponent<Image>().raycastTarget = true;
        关.onClick.AddListener(关面板);
    }

    void 刷新()
    {
        收上架的();
        if (按钮s.Count != 上架的.Count) 建行();
        刷新贡献文本();
        刷新按钮();
        说("");
    }

    void 建行()
    {
        if (InkUITheme.Enabled && 兑换内容 == null)
        {
            var view = UIBuildUtils.CreateRect("ExchangeViewport", 面板);
            UIBuildUtils.Place(view, Vector2.zero, Vector2.one, new Vector2(24, 108), new Vector2(-44, -116));
            view.gameObject.AddComponent<RectMask2D>();
            兑换内容 = UIBuildUtils.CreateRect("ExchangeContent", view);
            兑换内容.anchorMin = new Vector2(0, 1); 兑换内容.anchorMax = Vector2.one;
            兑换内容.pivot = new Vector2(.5f, 1); 兑换内容.sizeDelta = Vector2.zero;
            UIBuildUtils.AddVerticalLayout(兑换内容, 8, new RectOffset(0, 0, 0, 0));
            兑换内容.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = 面板.gameObject.AddComponent<ScrollRect>(); scroll.viewport = view; scroll.content = 兑换内容;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; InkUITheme.Scroll(scroll, 116);
        }
        foreach (var row in 兑换行) if (row != null) { row.transform.SetParent(null, false); Destroy(row); }
        兑换行.Clear();
        foreach (var b in 按钮s) if (b != null) Object.Destroy(b.gameObject);
        foreach (var t in 行文本s) if (t != null) Object.Destroy(t.gameObject);
        按钮s.Clear(); 行文本s.Clear();

        for (int i = 0; i < 上架的.Count; i++)
        {
            if (InkUITheme.Enabled)
            {
                int index = i;
                var row = UIBuildUtils.CreateRect("ExchangeRow", 兑换内容);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 102;
                InkUITheme.Image(row.gameObject.AddComponent<Image>(), "SkillsPage/skills/row-normal");
                var label = UIBuildUtils.CreateText("行" + i, row, 字体, "", 19, TextAnchor.MiddleLeft, InkUITheme.Ink);
                UIBuildUtils.Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-172, -6));
                var button = UIBuildUtils.CreateButton("兑换" + i, row, 字体, "兑换", 22);
                UIBuildUtils.Place(button.transform as RectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-156, -25), new Vector2(-12, 25));
                button.onClick.AddListener(() => 买(index));
                按钮s.Add(button); 行文本s.Add(label); 兑换行.Add(row.gameObject);
                continue;
            }
            int 序号 = i;
            float y = -116f - i * 行高;

            var 名 = UIBuildUtils.CreateText("行" + i, 面板, 字体, "", 字号, TextAnchor.MiddleLeft, 正常色);
            名.rectTransform.anchorMin = new Vector2(0f, 1f);
            名.rectTransform.anchorMax = new Vector2(1f, 1f);
            名.rectTransform.pivot = new Vector2(0.5f, 1f);
            名.rectTransform.sizeDelta = new Vector2(-320f, 行高 - 8f);
            名.rectTransform.anchoredPosition = new Vector2(-140f, y);
            行文本s.Add(名);

            var b = UIBuildUtils.CreateButton("兑换" + i, 面板, 字体, "兑换", 22);
            var rt = b.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(150f, 行高 - 12f);
            rt.anchoredPosition = new Vector2(-24f, y);
            var 图 = b.GetComponent<Image>();
            图.raycastTarget = true;         // CreateImage 默认 false，不开点不动
            b.onClick.AddListener(() => 买(序号));
            按钮s.Add(b);
        }
    }

    void 刷新贡献文本()
    {
        if (贡献文本 != null) 贡献文本.text = "宗门贡献：" + 宗门贡献.当前;
    }

    void 刷新按钮()
    {
        for (int i = 0; i < 按钮s.Count && i < 上架的.Count; i++)
        {
            var it = 上架的[i];
            bool 够 = 宗门贡献.够(it.兑换消耗贡献);
            var 图 = 按钮s[i].GetComponent<Image>();
            if (图 != null) 图.color = 够 ? 按钮可点色 : 按钮灰色;
            按钮s[i].interactable = 够;
            InkUITheme.Button(按钮s[i]);
            if (i < 行文本s.Count && 行文本s[i] != null)
            {
                string 说明 = string.IsNullOrWhiteSpace(it.介绍) ? "" : "　—　" + it.介绍;
                行文本s[i].text = it.DisplayName + "　【" + it.兑换消耗贡献 + " 贡献】" + 说明;
                if (InkUITheme.Enabled) 行文本s[i].text += "\n" + (够 ? "兑换后余额：" + (宗门贡献.当前 - it.兑换消耗贡献) : "贡献不足");
                if (InkUITheme.Enabled) 行文本s[i].text = it.DisplayName + "　" + it.兑换消耗贡献 + " 贡献\n" + it.介绍
                    + "\n" + (够 ? "兑换后余额：" + (宗门贡献.当前 - it.兑换消耗贡献) : "贡献不足");
                行文本s[i].color = 够 ? 正常色 : 不足色;
                if (InkUITheme.Enabled && 够) 行文本s[i].color = InkUITheme.Ink;
            }
        }
    }

    void 说(string 文本)
    {
        if (提示文本 != null) 提示文本.text = 文本 ?? "";
    }

    // ---- ASCII 别名 ----
    public void Open() => 开面板();
    public void Close() => 关面板();
    public GameObject PanelRoot => 面板根;
}
