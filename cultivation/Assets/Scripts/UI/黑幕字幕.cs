using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **黑幕白字过场**（用户 2026-09-27 的主线需求）。
///
/// 需求原文：
///   · 黑幕白字**逐字打出**；**左键长按屏幕加速**；文字**尽量居中**；字号合适。
///   · 四幕里还要"**黑屏加白屏闪一下**"表示大师兄一刀把野猪劈了 → <see cref="闪白"/>。
///
/// 用法（协程，方便在任务/演出里顺序编排）：
/// ```csharp
/// yield return 黑幕字幕.确保().说(new[] {
///     "这是一个漫长的故事",
///     "讲述了一个少年如何一步步争渡成仙",
///     "那个少年的名字叫做：" });
/// ```
/// 每行打完停 <see cref="行间停顿"/> 秒再打下一行；**点一下**立刻打完当前行，
/// **按住左键**打字速度 ×<see cref="长按加速倍率"/>。
///
/// 顺带提供**演出锁**：<see cref="演出中"/> 为真时，别的地方（玩家移动/攻击）应该停手。
/// 用 <see cref="开始演出"/> / <see cref="结束演出"/> 成对增减（支持嵌套），
/// 过场协程里用 <c>using</c> 风格的 <see cref="演出区"/> 更省事。
/// </summary>
[DisallowMultipleComponent]
public class 黑幕字幕 : MonoBehaviour
{
    public static 黑幕字幕 实例 { get; private set; }

    [Header("外观")]
    public Font 字体;
    [Tooltip("字号（1080p 参考分辨率下，44 左右看着合适）")]
    public int 字号 = 44;
    public Color 幕色 = Color.black;
    public Color 字色 = new Color(0.94f, 0.94f, 0.94f, 1f);

    [Header("打字")]
    [Tooltip("每秒打几个字")]
    public float 每秒字数 = 18f;

    [Tooltip("按住左键时的加速倍率")]
    public float 长按加速倍率 = 6f;

    [Tooltip("每行打完停多久再换下一行（秒）")]
    public float 行间停顿 = 0.7f;

    [Tooltip("打完之后要不要等玩家点一下才结束这一行")]
    public bool 每行等点击 = false;

    Canvas 画布;
    Image 幕;
    Image 闪;
    Text 文本;
    bool 在打字;

    /// <summary>黑幕是否在显示</summary>
    public bool 幕在显示 => 幕 != null && 幕.gameObject.activeSelf;

    // ============================================================ 演出锁

    static int 锁层数;
    /// <summary>正在演出（过场/黑幕/强制对话…）—— 玩家移动与攻击应该听它的</summary>
    public static bool 演出中 => 锁层数 > 0;

    /// <summary>
    /// 有人从**协程之外**动过黑幕锁（目前只有 播黑幕 会）。
    /// 用来判断「锁还在、但驱动它的协程可能已经随切场景被销毁」——那种情况下
    /// 切场景前要自己把幕收掉，否则会留下"该黑屏却没黑屏"的漏洞。
    /// </summary>
    static bool 协程持锁;
    public static bool 幕被协程持锁 => 协程持锁;
    public static void 标记协程持锁() { 协程持锁 = true; }
    public static void 清除协程持锁() { 协程持锁 = false; }

    public static void 开始演出() => 锁层数++;
    public static void 结束演出() { 锁层数 = Mathf.Max(0, 锁层数 - 1); }
    public static void 强制解锁() { 锁层数 = 0; 协程持锁 = false; }

    // ============================================================ 门面

    public static 黑幕字幕 确保()
    {
        if (实例 != null) return 实例;
        var go = new GameObject("黑幕字幕");
        return go.AddComponent<黑幕字幕>();
    }

    /// <summary>打一段（每行一句），打完整段才返回</summary>
    public static IEnumerator 说(params string[] 行) => 确保().逐行打(行, 0f);

    public static void 清空() { if (实例 != null) 实例.立即清空(); }

    /// <summary>黑幕+白字一起收掉（露出场景）</summary>
    public static void 收幕() { if (实例 != null) 实例.收起(); }

    /// <summary>只落下黑幕（不打字），供「纯黑幕遮罩」用</summary>
    public static void 落下幕() { 确保().落下(); }

    /// <summary>
    /// 落下黑幕、逐行打字，然后**由黑幕自己**收起。
    ///
    /// 为什么不让调用方用协程等：落黑幕的那段代码往往挂在**会被切场景销毁**的对象上
    /// （四幕第 3 阶段就是在古古镇落的幕、随后立刻切到宗门）。协程一死就没人收幕，
    /// 而黑幕对象是 `DontDestroyOnLoad` 的 —— 表现就是「换场景后一直黑着、也不解除」。
    /// 把倒计时挂在黑幕自己的 Update 上，谁都杀不掉。
    ///
    /// 收幕条件：**字全部打完**（不在打字了）**并且**打完后又停留了 `至少停留秒`。
    /// 所以字一定会打完，0.5 秒只是"读一眼"的停留时间。
    /// </summary>
    public static void 下落并定时收起(float 至少停留秒, string[] 行)
    {
        var c = 确保();
        c.落幕后自动收 = true;
        c.打完后的停留 = Mathf.Max(0f, 至少停留秒);
        c.打字完成时刻 = -1f;
        // 死线兜底：按"最长一行的字数 / 打字速度"再放宽 15 秒，防止打字卡住导致永远黑屏
        int 最长 = 0;
        if (行 != null) foreach (var s in 行) if (s != null && s.Length > 最长) 最长 = s.Length;
        c.自动收幕死线 = Time.time + (最长 / Mathf.Max(1f, c.每秒字数)) + Mathf.Max(2f, 至少停留秒) + 15f;
        c.StartCoroutine(c.逐行打(行 ?? new string[0], 0f));
    }

    /// <summary>打字结束时刻（-1 = 还没打完）；自动收幕要等它出现</summary>
    float 打字完成时刻 = -1f;
    /// <summary>打字打完后再停留多久才收幕</summary>
    float 打完后的停留 = 0.5f;
    /// <summary>是否处于"打完就自动收幕"模式</summary>
    bool 落幕后自动收;
    /// <summary>兜底：万一打字卡住，最多黑屏这么久也要收幕</summary>
    float 自动收幕死线 = -1f;

    public static IEnumerator 闪白(float 时长 = 0.3f) => 确保().做闪白(时长);

    // ============================================================ 生命周期

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(gameObject); return; }
        实例 = this;
        // 过场黑幕要能跨场景：四幕是「黑屏 + 白字 → 切到宗门」，加载时不能把画面露出来
        DontDestroyOnLoad(gameObject);
        搭界面();
        收起();
    }

    void OnDestroy() { if (实例 == this) 实例 = null; }

    void Update()
    {
        // ★ 自动收幕：由黑幕自己判断，谁都杀不掉（落黑幕的协程会随切场景销毁）
        //   收幕条件 = 字打完 + 打完后又停留了 打完后的停留 秒；
        //   另有死线兜底，防止打字卡住导致永远黑屏。
        if (落幕后自动收)
        {
            bool 到点 = 打字完成时刻 > 0f && Time.time >= 打字完成时刻 + 打完后的停留;
            bool 超时 = 自动收幕死线 > 0f && Time.time >= 自动收幕死线;
            if (到点 || 超时)
            {
                落幕后自动收 = false;
                自动收幕死线 = -1f;
                Debug.Log("[黑幕] 自动收幕（" + (到点 ? "字已打完 + 停留 " + 打完后的停留.ToString("F1") + "s" : "超时兜底")
                    + "）t=" + Time.time.ToString("F2"));
                收起();
                强制解锁();
                任务管理器.清演出中();      // 黑幕收掉 = 演出结束，放行阶段推进
            }
        }

        // 整段演出期间也上锁（防止黑幕期间玩家乱跑）
        if (幕在显示 && !演出中) 开始演出();
        else if (!幕在显示 && 演出中) 结束演出();
    }

    // ============================================================ 打字

    /// <summary>逐行打字。额外停顿 = 每行打完再多等这么久</summary>
    public IEnumerator 逐行打(string[] 行, float 额外停顿)
    {
        落下();
        for (int i = 0; i < 行.Length; i++)
        {
            立即清空();
            yield return 打一行(行[i]);
            float 停 = 行间停顿 + 额外停顿;
            if (停 > 0f) yield return new WaitForSeconds(停);
        }
    }

    IEnumerator 打一行(string 整句)
    {
        全句 = 整句 ?? "";
        文本.text = "";
        在打字 = true;

        float 出 = 0f;
        while (出 < 全句.Length)
        {
            // 长按左键 → 加速（用户要求）
            float 倍 = Input.GetMouseButton(0) ? Mathf.Max(1f, 长按加速倍率) : 1f;
            // 点一下 → 直接打完这一行
            if (Input.GetMouseButtonDown(0)) { 文本.text = 全句; break; }

            出 += 每秒字数 * 倍 * Time.unscaledDeltaTime;
            int 个数 = Mathf.Clamp(Mathf.FloorToInt(出), 0, 全句.Length);
            if (文本.text.Length != 个数) 文本.text = 全句.Substring(0, 个数);
            yield return null;
        }
        文本.text = 全句;

        if (每行等点击)
        {
            // 等一次"按下—抬起"，免得同一次点击被吃两次
            yield return null;
            while (!Input.GetMouseButtonDown(0)) yield return null;
        }
        在打字 = false;
        打字完成时刻 = Time.time;      // ★ 自动收幕要等这个时刻出现
    }

    [Tooltip("当前这一行的完整文本（打字机内部用）")]
    string 全句;

    public void 立即清空() { if (文本 != null) 文本.text = ""; }

    // ============================================================ 幕 / 闪白

    void 落下()
    {
        if (幕 != null) 幕.gameObject.SetActive(true);
        if (画布 != null) 画布.enabled = true;
    }

    public void 收起()
    {
        立即清空();
        if (幕 != null) 幕.gameObject.SetActive(false);
        if (画布 != null) 画布.enabled = false;
        结束演出();
        锁层数 = 0;
    }

    public IEnumerator 做闪白(float 时长)
    {
        落下();
        if (闪 == null) yield break;
        闪.gameObject.SetActive(true);
        // 黑 → 白 → 透明
        yield return 淡(闪, new Color(1f, 1f, 1f, 0f), Color.white, 时长 * 0.35f);
        yield return new WaitForSeconds(0.06f);
        yield return 淡(闪, Color.white, new Color(1f, 1f, 1f, 0f), 时长 * 0.6f);
        闪.gameObject.SetActive(false);
    }

    static IEnumerator 淡(Graphic g, Color 从, Color 到, float 时长)
    {
        if (g == null) yield break;
        float t = 0f;
        g.color = 从;
        while (t < 时长)
        {
            t += Time.unscaledDeltaTime;
            g.color = Color.Lerp(从, 到, Mathf.Clamp01(t / Mathf.Max(0.0001f, 时长)));
            yield return null;
        }
        g.color = 到;
    }

    // ============================================================ 搭界面

    void 搭界面()
    {
        画布 = gameObject.GetComponent<Canvas>();
        if (画布 == null) 画布 = gameObject.AddComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        画布.sortingOrder = 2900;                 // 高于对话框(2600)，低于暂停菜单(3000)
        if (gameObject.GetComponent<CanvasScaler>() == null)
        {
            var cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
        }
        if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        // 黑幕（挡住整个画面，也吃掉点击 —— 但打字逻辑用的是全局 Input，照样能加速）
        幕 = 建图("黑幕", 幕色, 0);
        // 白闪（在黑幕之上）
        闪 = 建图("白闪", new Color(1f, 1f, 1f, 0f), 1);

        // 正文：居中，留左右边距，行距宽松
        文本 = 建字("字幕", 字号);
        文本.alignment = TextAnchor.MiddleCenter;
        文本.lineSpacing = 1.35f;
        var rt = (RectTransform)文本.transform;
        rt.anchorMin = new Vector2(0.12f, 0.35f);
        rt.anchorMax = new Vector2(0.88f, 0.65f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    Image 建图(string 名, Color 色, int 序)
    {
        var go = new GameObject(名, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var img = go.GetComponent<Image>();
        img.color = 色;
        img.raycastTarget = true;                 // 吃掉点击，别点到后面的 UI
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.transform.SetSiblingIndex(Mathf.Clamp(序, 0, transform.childCount - 1));
        return img;
    }

    Text 建字(string 名, int 号)
    {
        var go = new GameObject(名, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(transform, false);
        var t = go.GetComponent<Text>();
        t.font = 取字体();
        t.fontSize = 号;
        t.color = 字色;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>
    /// 取字体。**必须优先中文字体** —— 内置的 `LegacyRuntime.ttf` 不含中文，
    /// 而黑幕多半是运行时 `确保()` 新建的、Inspector 上没挂字体，
    /// 于是「黑幕起了、字一个都不显示」（四幕实测）。
    ///
    /// `SimHei.ttf` 不在 Resources 下（运行时 `Resources.Load` 取不到），
    /// 所以这里照抄 `DeathScreenUI` / `CultivationUI` 已验证的写法：
    /// 编辑器里用 AssetDatabase 按路径/名字找。（真机打包场景另说，那要走 Resources。）
    /// </summary>
    Font 取字体()
    {
        if (字体 != null) return 字体;
#if UNITY_EDITOR
        字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
        if (字体 == null)
        {
            foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:Font"))
            {
                var f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (f != null && f.name.ToLowerInvariant().Contains("simhei")) { 字体 = f; break; }
            }
        }
        if (字体 == null)
            Debug.LogError("[黑幕字幕] 找不到中文字体 SimHei，黑幕文字会显示不出来", this);
#endif
        return 字体 != null ? 字体 : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // ---- ASCII 别名 ----
    public static bool InCutscene => 演出中;
    public static 黑幕字幕 Ensure() => 确保();
}
