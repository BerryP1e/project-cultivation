using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **灵田界面** —— 查看所有格子、播种、一键收取。
///
/// ## 怎么打开
///
/// 默认 **<see cref="开关按键"/> = F4**（随时可开，不需要走到田边 ——
/// 灵田是"挂机"玩法，不该强迫玩家跑图）。
/// 也可以由别处调 <see cref="打开"/> / <see cref="关闭"/>。
///
/// ## 布局
///
/// ```
/// ┌──────────────────────────────────────┐
/// │ 洞府灵田 · 一阶下品        格数 3/6  │
/// │ 可收 2 格            [ 一键收取 ]    │
/// ├──────────────────────────────────────┤
/// │  ┌────┐ ┌────┐ ┌────┐                 │
/// │  │ 1  │ │ 2  │ │ 3  │   ← 每个格子：  │
/// │  │生灵草│ │ 空 │ │成熟!│     名字/进度  │
/// │  └────┘ └────┘ └────┘                 │
/// ├──────────────────────────────────────┤
/// │ 种子： [生灵草] [凝露花] [清心莲 锁]  │
/// │        ↑ 点种子，再点空格子 = 种下     │
/// └──────────────────────────────────────┘
/// ```
///
/// ## 交互方式（为什么是"先点种子再点格子"）
///
/// 比"每个格子一个下拉菜单"少一半点击，也让"哪个种子不能种"能在选种子那一步
/// 就直观地锁掉（品阶不够的种子直接灰色不可选）。
///
/// ## 和 `TowerUI` 一样是运行时自搭 Canvas
///
/// 项目没导 TMP，统一 uGUI 传统 Text + SimHei；运行时搭就不会把界面序列化进场景，
/// 也就不会漂移（见 `docs/ai/踩坑总库.md`）。
///
/// ⚠️ **`UIBuildUtils.CreateImage` 默认 `raycastTarget = false`**，
/// 按钮底图必须显式开，否则**点不动而且没有任何报错**。
/// </summary>
[DisallowMultipleComponent]
public class 灵田界面 : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("打开/关闭灵田界面的按键")]
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
    public Color 空格色 = new Color(0.30f, 0.26f, 0.22f, 1f);
    public Color 生长色 = new Color(0.24f, 0.34f, 0.22f, 1f);
    public Color 成熟色 = new Color(0.36f, 0.42f, 0.16f, 1f);
    public Color 按钮色 = new Color(0.72f, 0.58f, 0.30f, 1f);
    public Color 按钮暗色 = new Color(0.30f, 0.28f, 0.26f, 1f);
    public Color 按钮字色 = new Color(0.10f, 0.09f, 0.07f, 1f);
    public Color 警告色 = new Color(1f, 0.72f, 0.40f, 1f);

    Canvas 画布;
    RectTransform 面板;
    Image 幕布;
    Text 标题文本;
    Text 副标题文本;
    Text 提示文本;
    RectTransform 格子排;
    RectTransform 种子排;

    readonly List<Button> 格子按钮 = new List<Button>();
    readonly List<Text> 格子文字 = new List<Text>();
    readonly List<Button> 种子按钮 = new List<Button>();
    readonly List<Text> 种子文字 = new List<Text>();

    /// <summary>当前选中的种子 id（点种子按钮设置）</summary>
    string 选中种子 = "";
    /// <summary>提示文字到期时刻</summary>
    float 提示到期 = -1f;

    public bool 已打开 => 面板 != null && 面板.gameObject.activeSelf;

    void Awake()
    {
        if (字体 == null) 取默认字体();
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
        var 田 = FindObjectOfType<灵田>();
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
        选中种子 = "";
        刷新();
        Debug.Log("[灵田界面] 已打开（" + 开关按键 + " 关闭）");
    }

    public void 关闭()
    {
        if (面板 == null) return;
        面板.gameObject.SetActive(false);
        同步幕布();
    }

    /// <summary>
    /// ⚠️ **幕布必须跟着面板一起显隐！**
    ///
    /// 【踩过的坑·2026-10-01】幕布是挂在**画布**下的（不是面板下），
    /// 所以只 `面板.SetActive(false)` 的话**幕布还在** ——
    /// 它全屏、`raycastTarget = true`、sortingOrder 又比所有 HUD 都高，
    /// 结果就是**整个游戏界面全部点不动**（用户报的"所有的 UI 都点击不了"）。
    ///
    /// 这个 bug 特别阴：Console 一片干净、没有任何报错，
    /// 只是"什么都点不了"。凡是在画布下自己搭全屏遮罩的，都要记得这一条。
    /// </summary>
    void 同步幕布()
    {
        if (幕布 == null) return;
        bool 要显示 = 面板 != null && 面板.gameObject.activeSelf;
        if (幕布.gameObject.activeSelf != 要显示) 幕布.gameObject.SetActive(要显示);
    }

    // ============================================================ 操作

    void 点格子(int 格号)
    {
        var 田 = 灵田.取();
        if (田 == null) return;

        var 格 = 格号 < 田.所有格.Count ? 田.所有格[格号] : null;
        if (格 == null) return;

        // 成熟 → 直接收
        if (格.已成熟)
        {
            int 量 = 田.收一格(格号);
            var d = 格.植;
            提示($"收了 {量} 个");
            return;
        }

        // 长着 → 提示还剩多久
        if (!格.是空的)
        {
            var d = 格.植;
            float 还剩 = Mathf.Max(0f, (d != null ? d.成熟天数 : 0f) - 格.已生长天数);
            提示($"「{(d != null ? d.名 : "?")}」还要 {还剩:F1} 天成熟");
            return;
        }

        // 空格 → 用选中的种子种下
        if (string.IsNullOrEmpty(选中种子))
        {
            提示("先在下面选一个种子");
            return;
        }

        string 原因;
        if (!田.能种(格号, 选中种子, out 原因))
        {
            提示(原因, true);
            return;
        }
        田.播种(格号, 选中种子);
        提示("已种下");
    }

    void 点种子(string 种子id)
    {
        var 田 = 灵田.取();
        var d = 灵植库.取(种子id);
        if (d == null) return;

        if (田 != null && !灵植库.可种(d, 田.品阶))
        {
            提示($"「{d.名}」需要 {灵田品阶说明.中文名(d.品阶)} 灵田", true);
            return;
        }

        选中种子 = 选中种子 == 种子id ? "" : 种子id;
        刷新();
        if (!string.IsNullOrEmpty(选中种子))
            提示($"已选「{d.名}」——点一个空格子种下");
    }

    void 点一键收取()
    {
        var 田 = 灵田.取();
        if (田 == null) return;
        string 明细;
        int 总 = 田.一键收取(out 明细);
        if (总 <= 0) 提示("没有成熟可收的灵植");
        else 提示("收获：" + 明细);
    }

    void 提示(string 文字, bool 警告 = false)
    {
        if (提示文本 == null) return;
        提示文本.text = 文字;
        提示文本.color = 警告 ? 警告色 : 正文色;
        提示到期 = Time.unscaledTime + 4f;
    }

    // ============================================================ 刷新

    void 刷新()
    {
        var 田 = 灵田.取();
        if (田 == null || 面板 == null || !面板.gameObject.activeSelf) return;

        if (标题文本 != null)
            标题文本.text = "洞府灵田 · " + 灵田品阶说明.中文名(田.品阶);

        if (副标题文本 != null)
        {
            int 可收 = 田.可收格数;
            副标题文本.text = $"已种 {田.已种格数}/{田.格数}　可收 {可收} 格"
                             + $"　产量 ×{灵田品阶说明.产量倍率(田.品阶):F2}"
                             + $"　生长 ×{灵田品阶说明.生长倍率(田.品阶):F2}";
        }

        刷新格子(田);
        刷新种子(田);
    }

    void 刷新格子(灵田 田)
    {
        int n = 田.所有格.Count;
        // 格子数变了就重建（正常情况下不变，只有扩容时）
        if (格子按钮.Count != n) 重建格子(n);

        for (int i = 0; i < n; i++)
        {
            var 格 = 田.所有格[i];
            var 图 = 格子按钮[i].GetComponent<Image>();
            var 文 = 格子文字[i];

            string 文本;
            if (格 == null || 格.是空的)
            {
                图.color = 空格色;
                文本 = $"{i + 1}\n空";
            }
            else if (格.已成熟)
            {
                图.color = 成熟色;
                var d = 格.植;
                文本 = $"{i + 1} 可收\n{(d != null ? d.名 : "?")}";
            }
            else
            {
                图.color = 生长色;
                var d = 格.植;
                float 天 = 格.已生长天数;
                float 总 = d != null ? d.成熟天数 : 1f;
                文本 = $"{i + 1} {(d != null ? d.名 : "?")}\n{天:F1}/{总:F0} 天";
            }
            文.text = 文本;
            // 成熟的高亮一下，好找
            文.color = (格 != null && 格.已成熟) ? new Color(1f, 0.96f, 0.62f) : 正文色;
        }
    }

    void 刷新种子(灵田 田)
    {
        var 全部 = 灵植库.全部;
        if (种子按钮.Count != 全部.Count) 重建种子(全部.Count);

        for (int i = 0; i < 全部.Count; i++)
        {
            var d = 全部[i];
            bool 能种 = 田 != null && 灵植库.可种(d, 田.品阶);
            bool 选中 = 选中种子 == d.id;

            var 图 = 种子按钮[i].GetComponent<Image>();
            图.color = 选中 ? 按钮色 : (能种 ? new Color(0.42f, 0.36f, 0.28f, 1f) : 按钮暗色);

            种子文字[i].text = 能种 ? d.名 : d.名 + "（" + 灵田品阶说明.中文名(d.品阶) + "）";
            种子文字[i].color = 能种 ? (选中 ? 按钮字色 : 正文色) : new Color(0.62f, 0.60f, 0.58f);
        }
    }

    // ============================================================ 搭界面

    void 重建格子(int n)
    {
        foreach (var b in 格子按钮) if (b != null) Destroy(b.gameObject);
        格子按钮.Clear(); 格子文字.Clear();
        if (格子排 == null) return;

        // 列数由 GridLayoutGroup 的 constraintCount 控制（见 搭界面），这里只管生成按钮
        for (int i = 0; i < n; i++)
        {
            int 编号 = i;
            var b = UIBuildUtils.CreateButton("格" + (i + 1), 格子排, 字体, "", 20);
            var 图 = b.GetComponent<Image>();
            图.raycastTarget = true;                    // ★ 必须显式开，见类注释
            图.color = 空格色;
            var lbl = b.GetComponentInChildren<Text>();
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.color = 正文色;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;

            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 150f; le.preferredHeight = 90f;
            le.minWidth = 150f; le.minHeight = 90f;

            b.onClick.AddListener(() => 点格子(编号));
            格子按钮.Add(b);
            格子文字.Add(lbl);
        }
    }

    void 重建种子(int n)
    {
        foreach (var b in 种子按钮) if (b != null) Destroy(b.gameObject);
        种子按钮.Clear(); 种子文字.Clear();
        if (种子排 == null) return;

        var 全部 = 灵植库.全部;
        for (int i = 0; i < n && i < 全部.Count; i++)
        {
            string id = 全部[i].id;
            var b = UIBuildUtils.CreateButton("种" + id, 种子排, 字体, 全部[i].名, 20);
            var 图 = b.GetComponent<Image>();
            图.raycastTarget = true;
            图.color = new Color(0.42f, 0.36f, 0.28f, 1f);
            var lbl = b.GetComponentInChildren<Text>();
            lbl.color = 正文色;

            var le = b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 190f; le.preferredHeight = 52f;
            le.minWidth = 160f; le.minHeight = 46f;

            b.onClick.AddListener(() => 点种子(id));
            种子按钮.Add(b);
            种子文字.Add(lbl);
        }
    }

    void 搭界面()
    {
        if (画布 != null) return;

        var 根 = new GameObject("SpiritFieldCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        // 比 HUD(2400) / 塔(2500) 都高，但低于暂停菜单(3000)
        画布.sortingOrder = 2600;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        // 全屏幕布（同时挡住射线，免得点穿到背后的 HUD）
        幕布 = UIBuildUtils.CreateImage("幕布", 根.transform, 幕布色);
        幕布.raycastTarget = true;          // 挡住射线，防止点穿到背后的 HUD
        UIBuildUtils.Stretch(幕布.rectTransform);

        // 居中面板
        var 板 = UIBuildUtils.CreateImage("面板", 幕布.rectTransform, 面板色);
        板.raycastTarget = true;
        面板 = 板.rectTransform;
        面板.anchorMin = new Vector2(0.5f, 0.5f);
        面板.anchorMax = new Vector2(0.5f, 0.5f);
        面板.pivot = new Vector2(0.5f, 0.5f);
        面板.sizeDelta = new Vector2(760f, 620f);
        面板.anchoredPosition = Vector2.zero;

        var 竖 = UIBuildUtils.AddVerticalLayout(面板, 12f, new RectOffset(26, 26, 22, 22));
        竖.childAlignment = TextAnchor.UpperCenter;
        竖.childControlWidth = true;
        竖.childControlHeight = false;
        竖.childForceExpandWidth = true;
        竖.childForceExpandHeight = false;

        // ---- 标题 ----
        标题文本 = UIBuildUtils.CreateText("标题", 面板, 字体, "洞府灵田", 32,
            TextAnchor.MiddleCenter, 标题色);
        标题文本.rectTransform.sizeDelta = new Vector2(700f, 40f);

        副标题文本 = UIBuildUtils.CreateText("副标题", 面板, 字体, "", 20,
            TextAnchor.MiddleCenter, 正文色);
        副标题文本.rectTransform.sizeDelta = new Vector2(700f, 28f);

        // ---- 一键收取 ----
        var 收 = UIBuildUtils.CreateButton("一键收取", 面板, 字体, "一键收取", 24);
        var 收图 = 收.GetComponent<Image>();
        收图.raycastTarget = true;
        收图.color = 按钮色;
        var 收字 = 收.GetComponentInChildren<Text>();
        收字.color = 按钮字色;
        var 收le = 收.gameObject.AddComponent<LayoutElement>();
        收le.preferredHeight = 56f; 收le.minHeight = 56f;
        收.onClick.AddListener(点一键收取);

        // ---- 格子区 ----
        var 格 = UIBuildUtils.CreateRect("格子区", 面板);
        格.sizeDelta = new Vector2(700f, 200f);
        var 格排 = 格.gameObject.AddComponent<GridLayoutGroup>();
        格排.cellSize = new Vector2(150f, 90f);
        格排.spacing = new Vector2(12f, 12f);
        格排.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        格排.constraintCount = 3;
        格排.childAlignment = TextAnchor.UpperCenter;
        格子排 = 格;
        var 格le = 格.gameObject.AddComponent<LayoutElement>();
        格le.preferredHeight = 200f; 格le.minHeight = 100f;

        // ---- 提示行 ----
        提示文本 = UIBuildUtils.CreateText("提示", 面板, 字体, "", 19,
            TextAnchor.MiddleCenter, 正文色);
        提示文本.rectTransform.sizeDelta = new Vector2(700f, 26f);

        // ---- 种子区 ----
        var 种标 = UIBuildUtils.CreateText("种子标题", 面板, 字体, "种子（点一个选中，再点空格子种下）", 18,
            TextAnchor.MiddleCenter, 正文色);
        种标.rectTransform.sizeDelta = new Vector2(700f, 24f);

        var 种 = UIBuildUtils.CreateRect("种子区", 面板);
        种.sizeDelta = new Vector2(700f, 60f);
        var 种排 = 种.gameObject.AddComponent<HorizontalLayoutGroup>();
        种排.spacing = 10f;
        种排.childAlignment = TextAnchor.MiddleCenter;
        种排.childControlWidth = false;
        种排.childControlHeight = true;
        种排.childForceExpandWidth = false;
        种排.childForceExpandHeight = true;
        种子排 = 种;
        var 种le = 种.gameObject.AddComponent<LayoutElement>();
        种le.preferredHeight = 60f; 种le.minHeight = 56f;

        // ---- 关闭按钮 ----
        var 关 = UIBuildUtils.CreateButton("关闭", 面板, 字体, "关闭（" + 开关按键 + "）", 20);
        var 关图 = 关.GetComponent<Image>();
        关图.raycastTarget = true;
        关图.color = 按钮暗色;
        var 关字 = 关.GetComponentInChildren<Text>();
        关字.color = 正文色;
        var 关le = 关.gameObject.AddComponent<LayoutElement>();
        关le.preferredHeight = 48f; 关le.minHeight = 48f;
        关.onClick.AddListener(关闭);
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

    // ---- ASCII 别名 ----
    public void Open() => 打开();
    public void Close() => 关闭();
    public void Toggle() => 切换();
}
