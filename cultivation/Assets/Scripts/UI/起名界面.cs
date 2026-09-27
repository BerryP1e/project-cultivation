using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **玩家起名界面**（主线第一幕用）。
///
/// 需求原文：「玩家起名，回车键确认，**5 个汉字以内**」。
///
/// 做法：复用黑幕那套视觉（全屏黑底 + 居中白字），中间一行是输入内容 + 闪烁光标，
/// 下面一行小字提示。输入不走 `InputField`（省得和中文输入法、UI 焦点打架），
/// 直接用 <c>Input.inputString</c> 收字符并自己过滤/限长。
///
/// 用法（协程，接着黑幕字幕往下排）：
/// ```csharp
/// yield return 黑幕字幕.说("这是一个漫长的故事", "讲述了一个少年如何一步步争渡成仙", "那个少年的名字叫做：");
/// string 名字 = null;
/// yield return 起名界面.取名("那个少年的名字叫做：", s => 名字 = s);
/// // 之后 名字 就是玩家输入（<=5 字），也可以随时读 起名界面.当前名字
/// ```
/// 输入规则：汉字 / 字母 / 数字都能收；最多 <see cref="最大字数"/> 个字符；
/// 退格删除；**回车（或小键盘回车）确认**；空名字不让确认。
/// </summary>
[DisallowMultipleComponent]
public class 起名界面 : MonoBehaviour
{
    public static 起名界面 实例 { get; private set; }

    [Header("外观")]
    public Font 字体;
    public int 标题字号 = 44;
    public int 输入字号 = 64;
    public int 提示字号 = 22;
    public Color 幕色 = Color.black;
    public Color 字色 = new Color(0.94f, 0.94f, 0.94f, 1f);
    public Color 提示色 = new Color(0.55f, 0.55f, 0.55f, 1f);

    [Header("规则")]
    [Tooltip("最多几个字符（用户要求 5 个汉字以内）")]
    public int 最大字数 = 5;

    [Tooltip("光标闪烁周期（秒）")]
    public float 光标周期 = 0.7f;

    /// <summary>玩家最终确定的名字（没确认过就是空串）</summary>
    public static string 当前名字 { get; private set; } = "";

    /// <summary>界面上正在输入的内容</summary>
    public string 缓冲 { get; private set; } = "";

    bool 已确认;
    // 注：原有一个 `在输入` 字段只写不读（输入状态实际看 已确认 与 缓冲），已删。
    Canvas 画布;
    Image 幕;
    Text 标题文本;
    Text 输入文本;
    Text 光标;
    Text 提示文本;

    public bool 在取名 => 幕 != null && 幕.gameObject.activeSelf;

    // ============================================================ 门面

    public static 起名界面 确保()
    {
        if (实例 != null) return 实例;
        var go = new GameObject("起名界面");
        return go.AddComponent<起名界面>();
    }

    /// <summary>弹起名界面，等到玩家回车确认才返回；确认的名字通过回调给出</summary>
    public static IEnumerator 取名(string 标题, System.Action<string> 完成)
    {
        var ui = 确保();
        yield return ui.做取名(标题);
        if (完成 != null) 完成(ui.缓冲);
    }

    // ============================================================ 主流程

    /// <summary>
    /// **自动化测试用**：置真之后，起名界面会在一小段延时后自动确认（用 <see cref="测试名字"/>）。
    /// 这样 AI 可以自己把开场跑到切场景那一步去验证，不用人手动输入。
    /// 只在编辑器 + 由调试脚本设置时才生效，正常游玩不受影响。
    /// </summary>
    public static bool 自动确认 = false;
    public static string 测试名字 = "测试";

    public IEnumerator 做取名(string 标题)
    {
        搭界面();
        标题文本.text = 标题 ?? "";
        缓冲 = "";
        已确认 = false;
        幕.gameObject.SetActive(true);
        画布.enabled = true;
        // 演出期间别让玩家乱跑
        黑幕字幕.开始演出();

        开始收字();                 // ★ 挂上文本输入（新 Input System 才收得到输入法的中文）
        float 开始时刻 = Time.unscaledTime;
        float 计时 = 0f;
        while (!已确认)
        {
            // ---- ① 单个按键：退格 / 回车 ----
            收按键(开始时刻);

            // ---- ② 文本：能拿到输入法提交的文本就用它（含中文），否则退回旧 Input ----
            string 收 = 取本帧文本();
            for (int i = 0; i < 收.Length; i++) 收一个字符(收[i]);

            // ---- ③ 自动测试：等 1.5 秒后自动填名字并确认（见 自动确认）----
            if (自动确认 && Time.unscaledTime - 开始时刻 > 1.5f)
            {
                缓冲 = 测试名字;
                已确认 = true;
                Debug.Log("[起名] 自动测试确认，名字=" + 测试名字);
            }

            输入文本.text = 缓冲;
            光标偏移();

            // ---- 光标闪烁（用下划线，不占字符）----
            计时 += Time.unscaledDeltaTime;
            光标.enabled = (计时 % Mathf.Max(0.05f, 光标周期)) < 光标周期 * 0.5f;

            yield return null;
        }

        结束收字();

        // ★ 确认时再卡一次字数：上面是"边打边拦"，但**输入法一次提交一整串**
        //   （一帧给出多个字符）时，那个检查会漏。
        //   用户要求"最后确认时监测五个字"，所以这里兜底截断。
        if (字数(缓冲) > 最大字数) 缓冲 = 截断到字数(缓冲, 最大字数);

        当前名字 = 缓冲;
        Debug.Log("[起名] 玩家名字确定为「" + 当前名字 + "」（" + 字数(当前名字) + " 个字）");
        收起();
    }

    /// <summary>处理一个输入字符（退格 / 回车 / 普通字）</summary>
    void 收一个字符(char c)
    {
        if (c == '\b')
        {
            if (缓冲.Length > 0)
            {
                // 代理对（emoji 等）要一次删两个 char，免得留下半个字符
                int 删 = (缓冲.Length >= 2 && char.IsLowSurrogate(缓冲[缓冲.Length - 1])) ? 2 : 1;
                缓冲 = 缓冲.Substring(0, 缓冲.Length - 删);
            }
        }
        else if (c == '\n' || c == '\r')
        {
            if (缓冲.Length > 0) 已确认 = true;
        }
        else if (!char.IsControl(c) && 字数(缓冲) < 最大字数)
        {
            缓冲 += c;
        }
    }

    /// <summary>主回车 / 小键盘回车确认</summary>
    void 收按键(float 开始时刻)
    {
        // 刚弹出来的那一帧不吃回车 —— 上一段黑幕可能就是被回车推过去的
        if (Time.unscaledTime - 开始时刻 < 0.15f) return;
        if (本帧按下回车() && 缓冲.Length > 0) 已确认 = true;
    }

    // ============================================================ 文本输入通道（可换实现）
    //
    // 为什么不能只用 `Input.inputString`（用户 2026-09-27 报"无法输入中文"）：
    //   旧版 Input 在 Windows 上**收不到输入法组合出来的字符** ——
    //   输入法把中文交给系统时，Unity 只把 ASCII 键位送进 inputString。
    //   新版 Input System 的 `Keyboard.onTextInput` 会带上**提交后的文本**（含中文）。
    //
    // 所以 ProjectSettings 里 activeInputHandler 开成 **2（两套共存）**，
    // 下面这段实现整个被 `#if ENABLE_INPUT_SYSTEM` 包着 ——
    // 包没解析出来时它编译成空的，就自动退回老实现（只有 ASCII 能用）。
    // 这样"装包 / 没装包"两种状态**都能编译**，不会卡在中间状态。

    void 开始收字() { 挂文本输入(true); }
    void 结束收字() { 挂文本输入(false); }

    /// <summary>取这一帧收到的文本（含输入法提交的）</summary>
    string 取本帧文本()
    {
        string 出 = null;
        取本帧文本(ref 出);
        return 出 ?? "";
    }

    bool 本帧按下回车()
    {
        bool 出 = false;
        本帧按下回车(ref 出);
        return 出;
    }

#if ENABLE_INPUT_SYSTEM
    // ---------------- 有 Input System 时：真正能收中文的实现 ----------------
    //
    // ⚠️ 两条重要经验（都踩过）：
    //   ① 这一整段被 `#if` 包住是**故意的**：`activeInputHandler = 2` 会立刻定义
    //      ENABLE_INPUT_SYSTEM，但**包本身**要等 Unity 重启才解析出来。夹在中间时
    //      会报 "The type or namespace name 'InputSystem' does not exist"，整个工程编译不过。
    //   ② 有 Input System 之后**绝不能再退回旧 `Input`**：
    //      `activeInputHandler = 2`（两套共存）时，`Input.inputString` / `Input.GetKeyDown`
    //      **返回空**（键盘归新系统管了）。原来写的是"新 API 没内容就退回旧 Input"，
    //      结果两条路都不通 → 起名界面收不到任何输入、卡死在名字界面。

    string 收到文本;
    UnityEngine.InputSystem.Keyboard 监听键盘;

    void 挂文本输入(bool 开)
    {
        收到文本 = null;
        if (开)
        {
            监听键盘 = UnityEngine.InputSystem.Keyboard.current;
            if (监听键盘 != null) 监听键盘.onTextInput += 来文本;
        }
        else if (监听键盘 != null)
        {
            监听键盘.onTextInput -= 来文本;
            监听键盘 = null;
        }
    }

    void 取本帧文本(ref string 出) { 出 = 收到文本; 收到文本 = null; }

    void 本帧按下回车(ref bool 出)
    {
        // ⚠️ 两套都试：`activeInputHandler = 2` 时老 Input 有没有效、`Keyboard.current`
        //    会不会是 null，实测都很不稳（换来换去踩过两次）。两边都问一遍最省事。
        bool 新 = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null) 新 = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
        bool 旧 = false;
        try { 旧 = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter); }
        catch (System.Exception) { }
        出 = 新 || 旧;
    }

    void 来文本(char c) => 收到文本 += c;
#else
    // ---------------- 没有包时：退回旧 Input（中文收不到，但至少能编译能跑） ----------------
    void 挂文本输入(bool 开) { }
    void 取本帧文本(ref string 出) { 出 = Input.inputString; }

    void 本帧按下回车(ref bool 出)
        => 出 = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif

    /// <summary>按**字符**数（不是 char 数）统计，代理对算一个</summary>
    static int 字数(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            n++;
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
        }
        return n;
    }

    /// <summary>按字符数截断（不切坏代理对）</summary>
    static string 截断到字数(string s, int 个)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int n = 0, i = 0;
        for (; i < s.Length && n < 个; i++)
        {
            n++;
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
        }
        return s.Substring(0, Mathf.Min(i, s.Length));
    }

    /// <summary>把光标摆到输入文字的右边（按文字实际渲染宽度算，居中文本对称）</summary>
    void 光标偏移()
    {
        if (光标 == null) return;
        var rt = (RectTransform)光标.transform;
        float 宽 = 0f;
        if (输入文本 != null && !string.IsNullOrEmpty(输入文本.text))
        {
            输入文本.font.RequestCharactersInTexture(输入文本.text, 输入字号, 输入文本.fontStyle);
            宽 = 输入文本.preferredWidth;
        }
        // 文字居中 → 右边缘在 +宽/2；光标以左中为轴心，摆到那里
        rt.anchoredPosition = new Vector2(宽 * 0.5f, 0f);
    }

    public void 收起()
    {
        if (幕 != null) 幕.gameObject.SetActive(false);
        if (画布 != null) 画布.enabled = false;
        黑幕字幕.结束演出();
    }

    /// <summary>外部直接设定名字（读档时用）</summary>
    public static void 设定名字(string 名字) { 当前名字 = 名字 ?? ""; }

    // ============================================================ 搭界面

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(gameObject); return; }
        实例 = this;
        搭界面();
        收起();
    }

    void OnDestroy() { if (实例 == this) 实例 = null; }

    void 搭界面()
    {
        if (画布 != null) return;

        画布 = gameObject.GetComponent<Canvas>();
        if (画布 == null) 画布 = gameObject.AddComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        画布.sortingOrder = 2950;                  // 在字幕(2900)之上、暂停菜单(3000)之下
        if (gameObject.GetComponent<CanvasScaler>() == null)
        {
            var cs = gameObject.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;
        }
        if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

        // 黑幕
        var 幕go = new GameObject("黑幕", typeof(RectTransform), typeof(Image));
        幕go.transform.SetParent(transform, false);
        幕 = 幕go.GetComponent<Image>();
        幕.color = 幕色;
        幕.raycastTarget = true;
        拉满((RectTransform)幕go.transform);

        // 标题（偏上）
        标题文本 = 建字("标题", 标题字号, 字色);
        var 标题rt = (RectTransform)标题文本.transform;
        标题rt.anchorMin = new Vector2(0.1f, 0.62f);
        标题rt.anchorMax = new Vector2(0.9f, 0.72f);
        标题rt.offsetMin = Vector2.zero; 标题rt.offsetMax = Vector2.zero;

        // 输入内容（正中）+ 光标
        //
        // ⚠️ 布局踩过的坑（用户 2026-09-27）：原来把 输入rt 的锚点改成 (0.60,0.51)、
        // 宽度 560 并设成 **MiddleRight**，同时光标另挂在 (0.62,0.51) ——
        // 两者**互不相干**，于是"adada"贴在最右边、光标却孤零零停在 0.62 处，
        // 看起来就是"名字没居中、光标乱飘"。
        // 现在：输入文本**居中显示**，光标按**实际文字宽度**跟在它右边（见 光标偏移()）。
        输入文本 = 建字("输入", 输入字号, 字色);
        var 输入rt = (RectTransform)输入文本.transform;
        输入rt.anchorMin = 输入rt.anchorMax = new Vector2(0.5f, 0.52f);
        输入rt.pivot = new Vector2(0.5f, 0.5f);
        输入rt.anchoredPosition = Vector2.zero;
        输入rt.sizeDelta = new Vector2(1400f, 输入字号 + 16f);
        输入文本.alignment = TextAnchor.MiddleCenter;

        光标 = 建字("光标", 输入字号, 字色);
        光标.text = "_";
        var 光标rt = (RectTransform)光标.transform;
        光标rt.anchorMin = 光标rt.anchorMax = new Vector2(0.5f, 0.52f);
        光标rt.pivot = new Vector2(0f, 0.5f);      // 左中：好按文字宽度右移
        光标rt.sizeDelta = new Vector2(40f, 输入字号 + 10f);
        光标rt.anchoredPosition = Vector2.zero;    // 每帧由 光标偏移() 更新

        // 提示（偏下）
        提示文本 = 建字("提示", 提示字号, 提示色);
        提示文本.text = "输入名字，回车确认（最多 " + 最大字数 + " 个字）";        var 提示rt = (RectTransform)提示文本.transform;
        提示rt.anchorMin = new Vector2(0.1f, 0.30f);
        提示rt.anchorMax = new Vector2(0.9f, 0.38f);
        提示rt.offsetMin = Vector2.zero; 提示rt.offsetMax = Vector2.zero;
    }

    Text 建字(string 名, int 号, Color 色)
    {
        var go = new GameObject(名, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(transform, false);
        var t = go.GetComponent<Text>();
        t.font = 字体 != null ? 字体 : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 号;
        t.color = 色;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void 拉满(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ---- ASCII 别名 ----
    public static string PlayerName => 当前名字;
    public static IEnumerator AskName(string title, System.Action<string> done) => 取名(title, done);
}
