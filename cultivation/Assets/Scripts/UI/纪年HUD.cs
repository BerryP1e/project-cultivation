using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **右下角纪年 HUD** —— 显示「太虚历 X 年 Y 月 Z 日 · 时辰」，
/// 无幕布，仅一行日期与时辰；修炼机会在境界页显示。
///
/// ## 和塔层 HUD 的分工
///
/// 两个 HUD 都是运行时自搭的 ScreenSpaceOverlay Canvas，**互不干扰**：
/// · 塔层 HUD：`sortingOrder = 2500`，只在塔里显示
/// · 本 HUD：`sortingOrder = 1520`（**压在面板之下**：面板最低的 `灵田地块界面` 是 1800），
///   **所有场景都显示**（时间到哪都在走）
///
/// ## 自举
///
/// 由 `场景自举` 补到主相机上，不写进场景（避免"某个场景忘了挂"漂移）。
/// </summary>
[DisallowMultipleComponent]
public class 纪年HUD : MonoBehaviour
{
    [Header("字体")]
    public Font 字体;

    [Header("外观")]
    [Tooltip("字号")]
    public int 字号 = 22;

    [Tooltip("距离屏幕右下角的边距")]
    public Vector2 边距 = new Vector2(20f, 18f);

    [Tooltip("主行文字颜色（日期）")]
    public Color 日期色 = new Color(0.96f, 0.94f, 0.86f, 1f);

    [Tooltip("旧配置兼容颜色，HUD 不再显示修炼机会")]
    public Color 机会色 = new Color(0.72f, 0.90f, 0.98f, 1f);

    [Tooltip("刚跨天时的高亮色")]
    public Color 跨天色 = new Color(1f, 0.90f, 0.42f, 1f);

    [Header("跨天提示")]
    [Tooltip("每天开始时把日期文字闪一下高亮，持续几秒")]
    public float 跨天高亮秒 = 2.5f;

    Canvas 画布;
    Text 日期文本;
    float 高亮到期时刻 = -1f;

    void Awake()
    {
        if (字体 == null) 取默认字体();
        搭界面();
    }

    // ============================================================ 事件订阅
    //
    // ⚠️【踩过的坑·2026-10-01】**订阅要放 OnEnable / OnDisable 成对，别只放 Start + OnDestroy！**
    //
    // 实测症状：跨天时「新的一天」日志打了 **3 遍**，日期推进看着像快了三倍。
    // 挖 `过了一天` 的调用列表发现订阅者有 4 个，其中本组件的 `处理跨天` 重复了 3 次。
    //
    // 根因：主相机在切场景时会被 `场景自举` **重新补组件**，于是 `Start()` 又跑了一次；
    //       而我只在 `OnDestroy()` 里退订 —— `Start` 每跑一次就多一份订阅。
    //
    // 修法：① 放 OnEnable/OnDisable（Unity 保证成对）
    //       ② **先 `-=` 再 `+=`**（即使万一 OnEnable 被重复调用也不会叠加）
    //       ③ 用 `时间管理器.取()` 而不是 `FindObjectOfType`（取不到会自建，语义一致）
    void OnEnable()
    {
        var t = 时间管理器.取();
        if (t == null) return;
        t.过了一天 -= 处理跨天;
        t.过了一天 += 处理跨天;
    }

    void OnDisable()
    {
        var t = 时间管理器.取();
        if (t != null) t.过了一天 -= 处理跨天;
    }

    void Start()
    {
        刷新();
    }

    void Update()
    {
        刷新();

        // 跨天高亮到期就恢复
        if (高亮到期时刻 > 0f && Time.unscaledTime >= 高亮到期时刻)
        {
            高亮到期时刻 = -1f;
            if (日期文本 != null) 日期文本.color = 日期色;
        }
    }

    void 处理跨天(int 天)
    {
        if (日期文本 != null)
        {
            日期文本.color = 跨天色;
            高亮到期时刻 = Time.unscaledTime + 跨天高亮秒;
        }
        Debug.Log("[纪年] 新的一天 —— " + (时间管理器.取() != null ? 时间管理器.取().纪年文本 : ""));
    }

    void 刷新()
    {
        var t = 时间管理器.取();
        if (t == null) return;

        if (日期文本 != null) 日期文本.text = t.纪年文本;
    }

    void 取默认字体()
    {
#if UNITY_EDITOR
        字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
#endif
        if (字体 == null)
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                if (f != null && f.name.ToLowerInvariant().Contains("simhei")) { 字体 = f; return; }
        }
    }

    void 搭界面()
    {
        if (画布 != null) return;

        var 根 = new GameObject("ChronicleCanvas", typeof(Canvas), typeof(CanvasScaler));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        // HUD 层：和 任务引导(1500) 同一档，**压在面板之下**。
        // 【为什么要这么低】面板里最低的是 `灵田地块界面`(1800) —— HUD 若在它之上，
        //   玩家一按 F 打开界面就会被 HUD 压住（用户实测报过这个 bug）。
        //   世界内提示最高只到 500（传送圈），所以 1520 两头都安全。
        画布.sortingOrder = 1520;
        // HUD 不吃射线（不然会挡住右上角的按钮）
        画布.gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        // 仅显示日期与时辰，无底板；修炼机会只在境界页展示。
        var 排 = UIBuildUtils.CreateRect("右下行", 根.transform);
        排.anchorMin = 排.anchorMax = new Vector2(1f, 0f);
        排.pivot = new Vector2(1f, 0f);
        排.sizeDelta = new Vector2(640f, 32f);
        排.anchoredPosition = new Vector2(-边距.x, 边距.y);
        日期文本 = UIBuildUtils.CreateText("日期", 排, 字体, "", 字号,
            TextAnchor.MiddleRight, 日期色);
        UIBuildUtils.Stretch(日期文本.rectTransform);
        日期文本.horizontalOverflow = HorizontalWrapMode.Overflow;
        日期文本.verticalOverflow = VerticalWrapMode.Truncate;
        UIBuildUtils.AddOutline(日期文本.rectTransform, new Color(0f, 0f, 0f, .85f));
    }

    // ---- ASCII 别名 ----
    public void Refresh() => 刷新();
}
