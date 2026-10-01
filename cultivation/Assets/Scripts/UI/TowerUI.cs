using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **镇妖塔界面**：层数 HUD + 通关选择 + 死亡选择。
///
/// ## 三个部分
///
/// | 部分 | 什么时候出现 | 按钮 |
/// |---|---|---|
/// | **层数 HUD**（左上角一小行） | 进塔就有 | — |
/// | **通关选择** | 本层怪清空时 | 进入下一层 / 留在本层 |
/// | **死亡选择** | 玩家倒下时（10 秒倒数） | 退出塔外 / 进入上一层 |
///
/// ## 为什么是运行时自己搭 Canvas
///
/// 和 <see cref="DeathScreenUI"/> / 暂停菜单一样 —— 项目没导 TMP，
/// 统一用 uGUI 传统 Text + SimHei，运行时搭就不用把界面序列化进场景，
/// 也就**不会漂移**（见 `docs/ai/踩坑总库.md`：UI 是「每个场景一份」的重灾区）。
///
/// ## 必须记住的坑
///
/// · **`UIBuildUtils.CreateImage` 默认 `raycastTarget = false`**（那是 HUD 的省性能约定），
///   按钮的底图必须显式开 `raycastTarget = true`，否则**射线打不到按钮，一点反应都没有，
///   而且 Console 一片干净**（`DeathScreenUI` 里也记过这条）。
/// · HUD 是 ScreenSpaceOverlay，`Camera.Render()` 抓不到，验证要用 `ScreenCapture`。
/// </summary>
[DisallowMultipleComponent]
public class TowerUI : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("塔控。留空自动在场景里找")]
    public TowerController 塔;

    [Header("字体")]
    [Tooltip("中文字体。留空自动找 SimHei")]
    public Font 字体;

    [Header("配色")]
    public Color 幕布色 = new Color(0f, 0f, 0f, 0.62f);
    public Color 标题色 = new Color(0.96f, 0.94f, 0.88f, 1f);
    public Color 正文色 = new Color(0.90f, 0.90f, 0.88f, 1f);
    public Color 按钮底色 = new Color(1f, 0.92f, 0.10f, 1f);
    public Color 按钮字色 = new Color(0.10f, 0.10f, 0.10f, 1f);
    public Color 倒数码色 = new Color(1f, 0.42f, 0.32f, 1f);

    [Header("HUD")]
    [Tooltip("HUD 用的字号")]
    public int HUD字号 = 24;

    [Tooltip("HUD 离屏幕上边缘的距离（像素）")]
    public float HUD顶部间距 = 18f;

    [Tooltip("HUD 的宽度（居中显示用）")]
    public float HUD宽度 = 900f;

    [Tooltip("HUD 文字颜色")]
    public Color HUD颜色 = new Color(0.98f, 0.96f, 0.90f, 1f);

    Canvas 画布;
    Text HUD文本;
    RectTransform 通关面板;
    RectTransform 死亡面板;
    Text 死亡倒计时文本;

    /// <summary>通关选择面板开着吗</summary>
    public bool 通关面板已显示 => 通关面板 != null && 通关面板.gameObject.activeSelf;
    /// <summary>死亡选择面板开着吗</summary>
    public bool 死亡面板已显示 => 死亡面板 != null && 死亡面板.gameObject.activeSelf;

    void Awake()
    {
        if (字体 == null) 取默认字体();
        if (字体 == null) Debug.LogError("[镇妖塔界面] 找不到中文字体，文字会显示成方块", this);
        搭界面();
        隐藏通关选择();
        隐藏死亡选择();
    }

    void Start()
    {
        if (塔 == null) 塔 = FindObjectOfType<TowerController>();
        if (塔 == null)
        {
            Debug.LogWarning("[镇妖塔界面] 场景里没有 TowerController，界面不会更新", this);
            return;
        }
        塔.层变化 += 处理层变化;
        塔.一波清空 += 处理一波清空;
        刷新HUD();
    }

    void OnDestroy()
    {
        if (塔 == null) return;
        塔.层变化 -= 处理层变化;
        塔.一波清空 -= 处理一波清空;
    }

    void 取默认字体()
    {
#if UNITY_EDITOR
        字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
#endif
        if (字体 == null)
        {
            var 全部 = Resources.FindObjectsOfTypeAll<Font>();
            foreach (var f in 全部)
                if (f != null && f.name.ToLowerInvariant().Contains("simhei")) { 字体 = f; return; }
        }
    }

    // ============================================================ 刷新

    void Update()
    {
        // HUD 需要显示「本层还剩几只」，那个值每帧在变，所以每帧刷一次
        // （一个字符串拼接，开销可以忽略；HUD 本来就是每帧刷的，见 PlayerHud）
        if (HUD文本 != null && HUD文本.gameObject.activeSelf) 刷新HUD();
    }

    void 刷新HUD()
    {
        if (塔 == null || HUD文本 == null) return;
        // 等级显示**整数**（玩家视角），补正等级是内部数值、不往界面上摆。
        // 想核对逐层递进时把下面那段的注释拆掉即可。
        HUD文本.text = "镇妖塔　第 " + 塔.当前层 + " 层"
                       + "　怪等级 " + 塔.当前怪物等级
                       + "（大境界 " + 塔.当前大境界 + "）"
                       + "　剩余 " + 塔.本层存活数;
        // + "　数值等级 " + 塔.当前怪物补正等级.ToString("0.0")
    }

    void 处理层变化(int 层)
    {
        刷新HUD();
        隐藏通关选择();
    }

    void 处理一波清空(int 层)
    {
        刷新HUD();
    }

    // ============================================================ 通关选择

    /// <summary>本层清空 —— 问玩家上楼还是留层</summary>
    public void 显示通关选择(int 层, bool 已是顶层)
    {
        if (通关面板 == null) return;
        通关面板.gameObject.SetActive(true);

        var 标题 = 通关面板.Find("标题");
        if (标题 != null)
        {
            var t = 标题.GetComponent<Text>();
            if (t != null) t.text = "第 " + 层 + " 层 · 妖物已清";
        }

        // 到顶层就没有「下一层」了，把按钮改成不可用（而不是藏起来，免得布局跳）
        var 下一层按钮 = 通关面板.Find("下一层");
        if (下一层按钮 != null)
        {
            var b = 下一层按钮.GetComponent<Button>();
            if (b != null)
            {
                b.interactable = !已是顶层;
                var 文字 = 下一层按钮.Find("Label");
                if (文字 != null)
                {
                    var t = 文字.GetComponent<Text>();
                    if (t != null) t.text = 已是顶层 ? "已至顶层" : "进入下一层";
                }
            }
        }
    }

    /// <summary>隐藏通关选择</summary>
    public void 隐藏通关选择()
    {
        if (通关面板 != null) 通关面板.gameObject.SetActive(false);
    }

    // ============================================================ 死亡选择

    /// <summary>玩家倒下 —— 开 10 秒倒数</summary>
    public void 显示死亡选择(int 层, float 时限)
    {
        if (死亡面板 == null) return;
        死亡面板.gameObject.SetActive(true);

        var 标题 = 死亡面板.Find("标题");
        if (标题 != null)
        {
            var t = 标题.GetComponent<Text>();
            if (t != null) t.text = "你在第 " + 层 + " 层倒下";
        }
        更新死亡倒数(时限);
    }

    /// <summary>刷新倒计时文本</summary>
    public void 更新死亡倒数(float 剩)
    {
        if (死亡倒计时文本 == null) return;
        // 整数秒，最后一秒给一位小数 —— 和 HUD 冷却倒计时同一套写法
        死亡倒计时文本.text = 剩 >= 1f
            ? "自动退出：" + Mathf.CeilToInt(剩) + " 秒"
            : "自动退出：" + 剩.ToString("0.0") + " 秒";
    }

    /// <summary>隐藏死亡选择</summary>
    public void 隐藏死亡选择()
    {
        if (死亡面板 != null) 死亡面板.gameObject.SetActive(false);
    }

    // ============================================================ 搭界面

    void 搭界面()
    {
        var 根 = new GameObject("TowerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        // HUD(默认) < 暂停菜单(3000) < 死亡(4000)。
        // 塔的两个面板要盖过 HUD，但不能盖过暂停菜单（ESC 时要能看见暂停菜单）
        画布.sortingOrder = 2500;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        搭HUD(根.transform);
        通关面板 = 搭选择面板(根.transform, "通关选择",
             new[] { ("进入下一层", (System.Action)(() => { if (塔 != null) 塔.下一层(); })),
                     ("留在本层",   (System.Action)(() => { if (塔 != null) 塔.留在本层(); })) });
        死亡面板 = 搭死亡面板(根.transform);
    }

    void 搭HUD(Transform 父)
    {
        // ★ 2026-10-01：从**左上角**挪到**顶部居中**（用户要求）。
        //   左上角会和「机位 yaw」那种调试提示打架，而且居中的层数更好读。
        //   锚点用 (0.5, 1)：随屏幕宽度自动居中，不用管分辨率。
        HUD文本 = UIBuildUtils.CreateText("塔层HUD", 父, 字体, "", HUD字号,
            TextAnchor.UpperCenter, HUD颜色);
        var rt = HUD文本.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(HUD宽度, 40f);
        rt.anchoredPosition = new Vector2(0f, -HUD顶部间距);
        UIBuildUtils.AddOutline(rt, new Color(0f, 0f, 0f, 0.7f));
    }

    /// <summary>搭一个居中面板：幕布 + 标题 + 纵排按钮</summary>
    RectTransform 搭选择面板(Transform 父, string 名, (string 文字, System.Action 动作)[] 按钮)
    {
        var 幕布 = UIBuildUtils.CreateImage(名, 父, 幕布色);
        幕布.raycastTarget = true;              // 挡住射线，防止点穿到背后的 HUD
        UIBuildUtils.Stretch(幕布.rectTransform);
        var 根 = 幕布.rectTransform;

        var 标题 = UIBuildUtils.CreateText("标题", 根, 字体, "", 40,
            TextAnchor.MiddleCenter, 标题色);
        var 标题RT = 标题.rectTransform;
        标题RT.anchorMin = new Vector2(0.5f, 0.5f);
        标题RT.anchorMax = new Vector2(0.5f, 0.5f);
        标题RT.pivot = new Vector2(0.5f, 0.5f);
        标题RT.sizeDelta = new Vector2(1200f, 70f);
        标题RT.anchoredPosition = new Vector2(0f, 150f);
        UIBuildUtils.AddOutline(标题RT, new Color(0f, 0f, 0f, 0.6f));

        var 排 = UIBuildUtils.CreateRect("按钮排", 根);
        排.anchorMin = new Vector2(0.5f, 0.5f);
        排.anchorMax = new Vector2(0.5f, 0.5f);
        排.pivot = new Vector2(0.5f, 0.5f);
        排.sizeDelta = new Vector2(340f, 按钮.Length * 96f);
        排.anchoredPosition = new Vector2(0f, -40f);
        var 竖 = UIBuildUtils.AddVerticalLayout(排, 18f, new RectOffset(0, 0, 0, 0));
        竖.childAlignment = TextAnchor.MiddleCenter;
        竖.childControlWidth = true;
        竖.childControlHeight = false;
        竖.childForceExpandWidth = true;
        竖.childForceExpandHeight = false;

        for (int i = 0; i < 按钮.Length; i++)
        {
            var (文字, 动作) = 按钮[i];
            var b = UIBuildUtils.CreateButton(文字, 排, 字体, 文字, 30);
            b.name = 文字;                       // 名字就是按钮文字，方便 Find
            // ★ 必须显式开：CreateImage 默认 raycastTarget=false，
            //   不开的话按钮点不动而且**没有任何报错**（DeathScreenUI 里同样的坑）
            var 图 = b.GetComponent<Image>();
            if (图 != null) 图.raycastTarget = true;
            图.color = 按钮底色;
            var lbl = b.GetComponentInChildren<Text>();
            if (lbl != null) lbl.color = 按钮字色;

            var 尺寸 = b.gameObject.AddComponent<LayoutElement>();
            尺寸.preferredHeight = 78f;
            尺寸.minHeight = 78f;

            var 捕获 = 动作;
            b.onClick.AddListener(() => 捕获?.Invoke());
        }

        return 根;
    }

    /// <summary>死亡面板比通关面板多一行倒计时</summary>
    RectTransform 搭死亡面板(Transform 父)
    {
        var 根 = 搭选择面板(父, "死亡选择", new[]
        {
            ("退出塔外",     (System.Action)(() => { if (塔 != null) 塔.执行死亡选择(false); })),
            ("进入上一层",   (System.Action)(() => { if (塔 != null) 塔.执行死亡选择(true);  })),
        });

        死亡倒计时文本 = UIBuildUtils.CreateText("倒计时", 根, 字体, "", 28,
            TextAnchor.MiddleCenter, 倒数码色);
        var rt = 死亡倒计时文本.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 44f);
        rt.anchoredPosition = new Vector2(0f, -175f);
        UIBuildUtils.AddOutline(rt, new Color(0f, 0f, 0f, 0.6f));

        return 根;
    }

    // ---- ASCII 别名 ----
    public void ShowClearChoice(int floor, bool isTop) => 显示通关选择(floor, isTop);
    public void HideClearChoice() => 隐藏通关选择();
    public void ShowDeathChoice(int floor, float limit) => 显示死亡选择(floor, limit);
    public void HideDeathChoice() => 隐藏死亡选择();
}
