using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **右上角的轻提示（Toast）**：一条半透明底的文字，淡入 → 停留 → 淡出后自动销毁。
///
/// 用法（静态，任何地方都能调）：
/// <code>
/// ToastUI.提示("已保存");
/// ToastUI.提示("保存中…", 1.5f);
/// </code>
///
/// 多条提示会**从上往下排队**，不会互相盖住。
/// 自己建 Canvas（sortingOrder 5000，盖过暂停菜单），所以不需要任何预制体或场景接线。
/// </summary>
public class ToastUI : MonoBehaviour
{
    [Header("样式")]
    public Color 底色 = new Color(0.06f, 0.07f, 0.10f, 0.86f);
    public Color 字色 = new Color(0.96f, 0.95f, 0.90f, 1f);
    public int 字号 = 22;
    public float 淡入时长 = 0.15f;
    public float 淡出时长 = 0.45f;
    public float 停留时长 = 2.2f;
    public Vector2 尺寸 = new Vector2(340f, 52f);
    public float 右边距 = 24f;
    /// <summary>
    /// 距屏幕顶端的边距。
    ///
    /// 【为什么是 104 而不是 24】右上角已经被 `纪年HUD` 占了（日期一行 + 修炼机会一行，
    /// 顶边距 18 + 约 70 像素高）。本组件也锚在右上角，24 的顶边距会和日期**叠在同一行**
    /// —— 实测截图里"新的一天"和"太虚历 X 年 Y 月 Z 日 · 时辰"糊在一起。
    /// 104 = 18 + 70 + 16 的余量，正好落在纪年 HUD 下面。
    ///
    /// 【改这里就够的原因】`提示()` 里是 `AddComponent` **现建**提示条，
    /// 位置只来自这几个默认值，场景/prefab 里没有第二份。
    /// </summary>
    public float 顶边距 = 104f;
    public float 行间距 = 8f;

    /// <summary>当前活着的提示条（从上往下排用）</summary>
    static readonly List<ToastUI> 活动 = new List<ToastUI>();

    static Canvas 共享画布;
    static Font 共享字体;

    // ============================================================ 静态入口

    /// <summary>弹一条提示。时长 ≤ 0 时用默认停留时长</summary>
    public static void 提示(string 文本, float 时长 = 0f)
    {
        if (string.IsNullOrEmpty(文本)) return;

        var 根 = 取画布();
        if (根 == null) { Debug.Log("[Toast] " + 文本); return; }

        var go = new GameObject("Toast", typeof(RectTransform));
        go.transform.SetParent(根.transform, false);
        var t = go.AddComponent<ToastUI>();
        t.构建(文本, 时长);
    }

    static Canvas 取画布()
    {
        if (共享画布 != null) return 共享画布;

        var go = new GameObject("ToastCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(go);
        共享画布 = go.GetComponent<Canvas>();
        共享画布.renderMode = RenderMode.ScreenSpaceOverlay;
        共享画布.sortingOrder = 5000;                   // 盖过暂停菜单(3000)、修炼界面(2500)

        var 缩放 = go.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        if (共享字体 == null)
        {
#if UNITY_EDITOR
            共享字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
#endif
            if (共享字体 == null)
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                    if (f != null && f.name.ToLowerInvariant().Contains("simhei")) { 共享字体 = f; break; }
        }
        return 共享画布;
    }

    // ============================================================ 单条

    CanvasGroup 组;
    RectTransform 自己;
    float 计时;
    Vector2 基准位置;
    bool 运动名额;
    bool 已提笔;
    RectTransform 底图;

    void 构建(string 文本, float 时长)
    {
        活动.Add(this);
        if (时长 > 0f) 停留时长 = 时长;

        自己 = (RectTransform)transform;
        自己.anchorMin = new Vector2(1f, 1f);
        自己.anchorMax = new Vector2(1f, 1f);
        自己.pivot = new Vector2(1f, 1f);
        自己.sizeDelta = 尺寸;

        var 底 = UIBuildUtils.CreateImage("底", transform, 底色);
        底图 = 底.rectTransform;
        底.raycastTarget = false;                        // 提示不该挡住点击
        UIBuildUtils.Stretch(底.rectTransform, 0f);

        var 文 = UIBuildUtils.CreateText("文字", transform, 共享字体, 文本, 字号,
            TextAnchor.MiddleCenter, 字色);
        文.raycastTarget = false;
        UIBuildUtils.Stretch(文.rectTransform, 10f);

        组 = gameObject.AddComponent<CanvasGroup>();
        组.alpha = 0f;

        排版();
        if (InkUITheme.Enabled) { 淡入时长 = .16f; 淡出时长 = .20f; }
        运动名额 = InkUITheme.Enabled && UIInkMotion.Acquire();
    }

    /// <summary>把活着的提示从上往下排</summary>
    static void 排版()
    {
        float y = 0f;
        for (int i = 0; i < 活动.Count; i++)
        {
            var t = 活动[i];
            if (t == null) continue;
            t.自己.anchoredPosition = new Vector2(-t.右边距, -(t.顶边距 + y));
            t.基准位置 = t.自己.anchoredPosition;
            y += t.尺寸.y + t.行间距;
        }
    }

    void Update()
    {
        计时 += Time.unscaledDeltaTime;                  // 暂停时也要动（timeScale=0）
        float 总 = 淡入时长 + 停留时长 + 淡出时长;
        if (InkUITheme.Enabled && 底图 != null)
            底图.localScale = Vector3.one * (UIInkMotion.减少动效 || !运动名额 ? 1 : Mathf.Lerp(.6f, 1, UIInkMotion.Timing.Cubic(计时 / Mathf.Max(.01f, 淡入时长))));

        if (计时 < 淡入时长) 组.alpha = !InkUITheme.Enabled
            ? Mathf.Clamp01(计时 / Mathf.Max(.01f, 淡入时长))
            : UIInkMotion.减少动效 || !运动名额 ? 1 : UIInkMotion.Timing.Cubic(计时 / Mathf.Max(.01f, 淡入时长));
        else if (计时 < 淡入时长 + 停留时长)
        {
            组.alpha = 1f;
            if (运动名额) { UIInkMotion.Release(); 运动名额 = false; }
        }
        else if (计时 < 总)
        {
            if (!已提笔) { 已提笔 = true; 运动名额 = InkUITheme.Enabled && UIInkMotion.Acquire(); }
            float t = (计时 - 淡入时长 - 停留时长) / Mathf.Max(0.01f, 淡出时长);
            组.alpha = !InkUITheme.Enabled ? 1 - t
                : UIInkMotion.减少动效 || !运动名额 ? 1 : 1 - UIInkMotion.Timing.Quad(t);
            if (!UIInkMotion.减少动效 && 运动名额) 自己.anchoredPosition = 基准位置 + Vector2.up * (10 * t);
        }
        else
        {
            活动.Remove(this);
            排版();
            if (共享画布 != null) Destroy(gameObject);
            else Destroy(gameObject);
        }
    }

    void OnDestroy() { 活动.Remove(this); if (运动名额) { UIInkMotion.Release(); 运动名额 = false; } }

    // ---- ASCII 别名 ----
    public static void Show(string text, float seconds = 0f) => 提示(text, seconds);
}
