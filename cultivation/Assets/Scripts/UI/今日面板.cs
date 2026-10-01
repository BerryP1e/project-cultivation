using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **「今日」面板** —— 右上角、纪年 HUD 下面那一小块：这一天该做的四件事 + 打勾。
///
/// 由 `场景自举.补玩家与相机()` 自动补到**主相机**上（和 `纪年HUD` / `灵田界面` / `任务引导`
/// 同一处），**不写进任何场景** —— 那条路的理由见踩坑总库 B 段"补组件不写场景"。
///
/// ## 为什么挂在**右下角**（第一版放右上角，实测挤成一团）
///
/// 右上角已经有三样东西：`纪年HUD`（日期 + 修炼机会）、`ToastUI`（"新的一天"、
/// 伏笔提示都往那儿飘）、以及塔层 HUD。第一版把面板放右上角 ⇒
/// **和 Toast 叠在同一行**（截图里"新的一天"和日期糊在一起）。
/// 右下角是空的（血条、快捷栏、主线追踪都在左半边），而且"今天还差什么"本来就该一眼看见。
///
/// `sortingOrder` 给 2390：比纪年 HUD（2400）低一档，真撞上了也是纪年压住它。
///
/// ## 刷新口径
///
/// **每帧重算**（不做脏标记）：内容就是几行字符串，`text` 只在真的变了时才赋值
/// （Unity 的 `Text.text` setter 每次都会触发重建网格，每帧无脑赋值会白白重排版）。
/// </summary>
public class 今日面板 : MonoBehaviour
{
    static 今日面板 实例;
    public static 今日面板 取() => 实例;

    [Tooltip("面板距屏幕**右下角**的边距")]
    public Vector2 边距 = new Vector2(24f, 22f);

    public Color 标题色 = new Color(0.95f, 0.85f, 0.55f, 1f);
    public Color 未完成色 = new Color(0.72f, 0.74f, 0.78f, 1f);
    public Color 完成色 = new Color(0.55f, 0.95f, 0.60f, 1f);
    public Color 汇总色 = new Color(0.62f, 0.64f, 0.70f, 1f);
    public Color 底色 = new Color(0.05f, 0.06f, 0.09f, 0.78f);

    Canvas 画布;
    Text 标题;
    Text 汇总;
    readonly List<Text> 行 = new List<Text>();
    Font 字体;
    日常循环 订阅的循环;

    void Awake() { 实例 = this; }

    void OnDestroy()
    {
        if (实例 == this) 实例 = null;
        if (订阅的循环 != null) 订阅的循环.变化 -= 刷新;
    }

    void Start()
    {
        取默认字体();
        搭界面();
        刷新();
    }

    void Update()
    {
        // `日常循环` 和本组件是同一帧被补到相机上的，谁先 Awake 不定 —— 这里自己补齐引用
        if (订阅的循环 != 日常循环.取())
        {
            if (订阅的循环 != null) 订阅的循环.变化 -= 刷新;
            订阅的循环 = 日常循环.取();
            if (订阅的循环 != null) 订阅的循环.变化 += 刷新;
        }
        刷新();
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

    void 搭界面()
    {
        if (画布 != null) return;

        var 根 = new GameObject("DailyCanvas", typeof(Canvas), typeof(CanvasScaler));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        画布.sortingOrder = 2390;      // 纪年 HUD 是 2400，要比它低
        画布.gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        var 底 = UIBuildUtils.CreateRect("今日底", 根.transform);
        底.anchorMin = new Vector2(1f, 0f);
        底.anchorMax = new Vector2(1f, 0f);
        底.pivot = new Vector2(1f, 0f);
        底.sizeDelta = new Vector2(420f, 186f);
        底.anchoredPosition = new Vector2(-边距.x, 边距.y);
        var 图 = UIBuildUtils.CreateImage("底图", 底, 底色);
        UIBuildUtils.Stretch(图.rectTransform);

        // ⚠️ 布局组**不能直接挂在底板上**：那会把同级的"底图"也当成一行去排版，
        //    背景会被挤成一条（实测过）。所以再套一层"内容"容器，布局只作用于它里面的字。
        var 内容 = UIBuildUtils.CreateRect("内容", 底);
        UIBuildUtils.Stretch(内容, 0f);

        var 竖 = UIBuildUtils.AddVerticalLayout(内容, 3f, new RectOffset(10, 10, 8, 8));
        竖.childAlignment = TextAnchor.UpperRight;
        竖.childControlWidth = true;
        竖.childControlHeight = true;
        竖.childForceExpandWidth = true;
        竖.childForceExpandHeight = false;

        标题 = UIBuildUtils.CreateText("今日标题", 内容, 字体, "今日", 20, TextAnchor.MiddleRight, 标题色);
        标题.rectTransform.sizeDelta = new Vector2(0f, 26f);
        UIBuildUtils.AddOutline(标题.rectTransform, new Color(0f, 0f, 0f, 0.85f));

        foreach (var x in 日常循环.全部项)
        {
            var t = UIBuildUtils.CreateText("今日项_" + x, 内容, 字体, 日常循环.项名(x), 17, TextAnchor.MiddleRight, 未完成色);
            t.rectTransform.sizeDelta = new Vector2(0f, 22f);
            行.Add(t);
        }

        汇总 = UIBuildUtils.CreateText("今日汇总", 内容, 字体, "", 15, TextAnchor.MiddleRight, 汇总色);
        汇总.rectTransform.sizeDelta = new Vector2(0f, 30f);
    }

    void 刷新()
    {
        if (标题 == null) return;

        var c = 日常循环.取();
        if (c == null)
        {
            标题.text = "今日";
            foreach (var t in 行) t.text = "—";
            汇总.text = "";
            return;
        }

        var 时 = 时间管理器.取();
        标题.text = "今日 · " + (时 != null ? 时.纪年文本 : "—");

        for (int i = 0; i < 行.Count && i < 日常循环.全部项.Length; i++)
        {
            var x = 日常循环.全部项[i];
            bool 完 = c.已完成(x);
            string 文 = (完 ? "✓ " : "○ ") + 日常循环.项名(x);
            if (行[i].text != 文) 行[i].text = 文;
            行[i].color = 完 ? 完成色 : 未完成色;
        }

        string 汇 = c.今日完成数 + "/" + c.今日总项数;
        if (时 != null) 汇 += "　修炼机会 " + 时.还能修炼几次 + "/" + 时.每日修炼上限;
        if (汇总.text != 汇) 汇总.text = 汇;
    }
}
