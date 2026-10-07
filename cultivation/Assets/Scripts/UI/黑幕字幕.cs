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

    [Header("落下 / 收起")]
    [Tooltip("**落黑幕的默认淡入步数**。0 = 不淡入，直接全黑（默认）。\n" +
             "★ 不是每个黑幕都要淡入 —— 只有需要「一帧一帧暗下去」的过场才传步数。\n" +
             "步数 = 分几档（例如 24 步 ≈ 2 秒 × 12fps），是**阶梯式**变化，不是平滑插值。")]
    public int 默认淡入步数 = 0;

    [Tooltip("**收黑幕的默认淡出步数**。0 = 不淡出，直接收掉（默认）")]
    public int 默认淡出步数 = 0;

    [Tooltip("阶梯淡入/淡出的步频（步/秒）。24 步 + 12fps ≈ 2 秒")]
    public float 淡步频 = 12f;

    Canvas 画布;
    Image 幕;
    Image 闪;
    Text 文本;
    // 注：原来有个 `在打字` 字段判断"字打完没"，现在统一走 `打字完成时刻`
    //（见 逐行打 / 下落并定时收起 / Update 的自动收幕），所以那个字段已无用、删掉。

    /// <summary>黑幕是否在显示</summary>
    public bool 幕在显示 => 幕 != null && 幕.gameObject.activeSelf;

    /// <summary>静态版：场上有黑幕实例、且幕正盖着（任务管理器等外部判定用）</summary>
    public static bool 有幕在显示 => 实例 != null && 实例.幕在显示;

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
    /// **切场景过渡** —— 由 <see cref="Update"/> 每帧驱动的状态机。
    ///
    /// ★ 为什么不写成协程：`LoadSceneAsync(Single)` 会**卸载旧场景**，
    ///   而协程是挂在旧场景里的 任务管理器 上的 —— 场景一卸载，协程当场死掉，
    ///   后面"摆位 / 等2秒 / 收幕 / 放行阶段"**一行都不会执行**。
    ///   实测就是这么"太虚宗的黑幕永远不收、大师兄对话提前弹"的。
    ///   黑幕自己带 `DontDestroyOnLoad`，所以把它当成跨场景的驱动器最稳。
    ///
    /// 流程（用户 2026-09-27 定）：
    ///   ① 立刻落黑幕 + 打出场景名（**在加载之前**，第一眼绝不能看到新场景）
    ///   ② 开始异步加载（黑幕与字全程盖着）
    ///   ③ 加载完 + 把玩家放到落点（不走"出生点→拖过去"）
    ///   ④ 从打出场景名起算至少 2 秒 → 收幕
    ///   ⑤ **等淡出真正跑完**才放行后续阶段
    /// </summary>
    public class 场景过渡状态
    {
        public string 目标场景;
        public Vector3 落点;
        public bool 有落点;
        /// <summary>按**名字**在新场景里找落点（传送点用；坐标落点用 落点/有落点）</summary>
        public string 落点名;
        /// <summary>1.2 秒的"落点接管"，到点后要不要强制把玩家钉到落点</summary>
        public bool 强制落点;
        public int 淡入档数 = 24, 淡出档数 = 24;
        public float 起算时刻;
        public UnityEngine.AsyncOperation 加载;
        public int 段位;          // 0=刚开始 1=加载中 2=摆位 3=等2秒 4=等淡出 5=完成
        public float 摆位死线;
        public float 淡出死线;
        /// <summary>"字全部打完"的时刻（用来算"打完后再停 2 秒"）</summary>
        public float 字打完时刻 = -1f;
    }

    /// <summary>切场景时"字全部打完之后"再停留多久才淡出（用户 2026-09-27 定：2 秒）</summary>
    public const float 字打完后停留 = 2f;

    static 场景过渡状态 过渡;

    /// <summary>
    /// 开始一次切场景过渡（**坐标**落点版，任务表「切换场景」用）。
    /// 调用后由黑幕自己把它跑完（跨场景安全）。
    /// </summary>
    public static void 开始场景过渡(string 场景名, string 显示名, Vector3 落点 = default(Vector3),
                                    int 淡入档数 = 24, int 淡出档数 = 24)
        => 开始场景过渡(场景名, 显示名, 落点, null, 淡入档数, 淡出档数);

    /// <summary>
    /// 开始一次切场景过渡（**任意宿主**可调：任务表切场景 / 传送点 / 传送门…）。
    /// 统一在这里做"黑幕+场景名 → 加载 → 摆位 → 停 2 秒 → 淡出"，
    /// 这样**任何**切场景路径都有同样的过场惯例（用户 2026-09-27 要求）。
    /// </summary>
    /// <param name="落点名">按名字在新场景里找落点（传送点用）；与 <paramref name="落点"/> 二选一</param>
    public static void 开始场景过渡(string 场景名, string 显示名, Vector3 落点, string 落点名,
                                    int 淡入档数 = 24, int 淡出档数 = 24)
    {
        var c = 确保();
        // 过渡期间不许自动收幕（三个标记都要清，见 下落并停留不收起 / 显示场景名 的说明）
        c.待自动收 = false;
        c.落幕后自动收 = false;
        c.不自动收 = false;
        c.收幕淡出步数 = Mathf.Max(0, 淡出档数);
        c.落下(0);
        c.立即清空();
        if (c.文本 != null) c.文本.text = 显示名 ?? 场景名;

        // ★ 场景名是**瞬间显示**的（不走打字机），所以"字打完"必须当场置上。
        //   踩过的坑（2026-09-27，表现是"切场景卡住不动"）：
        //   上一步「从此…」走的是 下落并停留不收起，它把 打字完成时刻 置成了 -1；
        //   这里不补上的话 `字打完且幕全黑` 永远为 false，
        //   过渡的"等字打完再停 2 秒"就永远等不到 → 卡死在第 3 段位（实测段位=3、字打完时刻=-1）。
        c.打字完成时刻 = Time.unscaledTime;
        c.可点击收幕时刻 = Time.unscaledTime;
        开始演出();

        过渡 = new 场景过渡状态
        {
            目标场景 = 场景名,
            落点 = 落点,
            有落点 = 落点 != Vector3.zero,
            落点名 = 落点名,
            淡入档数 = 淡入档数,
            淡出档数 = 淡出档数,
            起算时刻 = Time.unscaledTime,
            段位 = 0,
        };
        // ★ 标记"正在接力"：这期间不要让存档里的位置覆盖落点（见 跨场景数据.正在接力）
        跨场景数据.正在接力 = true;
        Debug.Log("[切场景] ① 黑幕+场景名「" + 显示名 + "」已打出，准备加载「" + 场景名 + "」"
            + (string.IsNullOrEmpty(落点名) ? "" : "，落点「" + 落点名 + "」"));
    }

    /// <summary>过渡是否还在进行（任务管理器据此决定要不要推进阶段）</summary>
    public static bool 过渡进行中 => 过渡 != null;

    void 推进场景过渡()
    {
        var s = 过渡;
        if (s == null) return;

        switch (s.段位)
        {
            case 0:      // 开始加载
                s.加载 = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    s.目标场景, UnityEngine.SceneManagement.LoadSceneMode.Single);
                if (s.加载 == null)
                {
                    Debug.LogError("[切场景] LoadSceneAsync 失败：「" + s.目标场景 + "」要加进 Build Settings");
                    过渡 = null;
                    收起();
                    return;
                }
                s.段位 = 1;
                Debug.Log("[切场景] ② 开始加载「" + s.目标场景 + "」");
                break;

            case 1:      // 等加载
                if (s.加载 != null && !s.加载.isDone) return;
                Debug.Log("[切场景] ③ 加载完成，开始摆位");
                s.段位 = 2;
                s.摆位死线 = Time.unscaledTime + 3f;

                // 落点两种给法：直接坐标（任务表）或按名字在新场景里找（传送点）
                if (string.IsNullOrEmpty(s.落点名) && s.有落点)
                {
                    任务管理器.请求落点(s.落点);
                }
                else if (!string.IsNullOrEmpty(s.落点名))
                {
                    var 点 = 找场景物体(s.落点名);
                    if (点 != null)
                    {
                        任务管理器.请求落点(点.position, 0f);   // 0 秒接管 = 直接钉上去
                        Debug.Log("[切场景] 落点「" + s.落点名 + "」→ " + 点.position.ToString("F2"));
                    }
                    else
                    {
                        Debug.LogWarning("[切场景] 新场景里找不到落点「" + s.落点名 + "」");
                    }
                }
                break;

            case 2:      // 等落点摆好（最多 3 秒）
                if (任务管理器.落点进行中_公开 && Time.unscaledTime < s.摆位死线) return;
                s.段位 = 3;
                break;

            case 3:      // 等"字打完" → 再等 2 秒 → 收幕
                //
                // 用户要求：**字全部打出来之后再停 2 秒**才切场景（不是从起幕算）。
                // 所以不能直接用"从打出场景名起算"的那个计时，要等字打完再重新起算。
                if (!字打完且幕全黑) return;
                if (s.字打完时刻 <= 0f)
                {
                    s.字打完时刻 = Time.unscaledTime;
                    Debug.Log("[切场景] ④ 字已打完，再停 " + 字打完后停留.ToString("F1") + "s 再切景");
                    return;
                }
                if (Time.unscaledTime - s.字打完时刻 < 字打完后停留) return;
                // 兜底：也不能早于"从起幕算起的最短停留"（防止场景名还没看清就切）
                if (Time.unscaledTime - s.起算时刻 < 任务管理器.场景名最短停留) return;
                Debug.Log("[切场景] ⑤ 开始淡出（起幕起共 " + (Time.unscaledTime - s.起算时刻).ToString("F2")
                    + "s，字打完后又停 " + 字打完后停留.ToString("F1") + "s）");
                收起();
                s.段位 = 4;
                s.淡出死线 = Time.unscaledTime + 4f;
                break;

            case 4:      // 等淡出真正跑完，才放行阶段
                if (幕在显示 && Time.unscaledTime < s.淡出死线) return;
                Debug.Log("[切场景] ⑥ 黑幕已收（幕可见=" + 幕在显示 + "），放行后续阶段");
                过渡 = null;
                跨场景数据.正在接力 = false;     // ★ 接力结束，存档位置恢复生效
                强制解锁();
                任务管理器.清演出中();
                break;
        }
    }

    /// <summary>
    /// 在当前（已加载的）场景里按名字找**落点**物体，含 inactive。
    ///
    /// ★ 为什么要排除带 <c>Teleporter</c> 的那个（踩过的坑 2026-09-27）：
    ///   落点常常和它对应的传送门**同名**（例如 Sect 里既有叫 `sect2 to tower` 的传送门，
    ///   也有叫 `sect2 to tower` 的落点）。按名字遍历时先命中的往往是传送门 ——
    ///   于是玩家被**放到传送门身上**，立刻又踩进它自己的触发圈，来回弹。
    ///   所以优先返回**没有 Teleporter 组件**的那个。
    /// </summary>
    public static Transform 找场景物体(string 名)
        => 找场景物体(名, t => t.GetComponent("Teleporter") == null)
           ?? 找场景物体(名, null);

    /// <summary>按名字找物体；<paramref name="筛"/> 为 null 时不筛</summary>
    public static Transform 找场景物体(string 名, System.Func<Transform, bool> 筛)
    {
        if (string.IsNullOrEmpty(名)) return null;
        var 场景 = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var g in 场景.GetRootGameObjects())
        {
            if (g.name == 名 && (筛 == null || 筛(g.transform))) return g.transform;
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                if (t.name == 名 && (筛 == null || 筛(t))) return t;
        }
        return null;
    }

    /// <summary>
    /// 落下黑幕、逐行打字，然后**由黑幕自己**收起。
    ///
    /// 为什么不让调用方用协程等：落黑幕的那段代码往往挂在**会被切场景销毁**的对象上
    /// （四幕第 3 阶段就是在古古镇落的幕、随后立刻切到宗门）。协程一死就没人收幕，
    /// 而黑幕对象是 `DontDestroyOnLoad` 的 —— 表现就是「换场景后一直黑着、也不解除」。
    /// 把倒计时挂在黑幕自己的 Update 上，谁都杀不掉。
    ///
    /// 收幕条件（用户 2026-09-27 定的观感）：
    ///   ① 字**全部打完**（不会被切断）
    ///   ② 打完后又停留了 <paramref name="打完停留秒"/> 秒
    ///   ③ **或者**玩家在这期间点了一下左键（想快点跳过）
    /// 也就是说：打完至少看 2 秒，等不及就点一下。
    /// </summary>
    /// <param name="打完停留秒">字打完之后至少停多久才收幕</param>
    /// <param name="行">要打的文本（每行一句）</param>
    /// <param name="每秒字数">这次演出专用的打字速度；≤0 = 用组件上的 每秒字数</param>
    /// <param name="淡入步数">阶梯淡入的档数；0 = 立刻全黑（默认，大部分黑幕都该这样）</param>
    /// <param name="淡出步数">阶梯淡出的档数；0 = 立刻收掉（默认）</param>
    public static void 下落并定时收起(float 打完停留秒, string[] 行, float 每秒字数 = 0f,
                                      int 淡入步数 = 0, int 淡出步数 = 0)
    {
        var c = 确保();
        // ★ 收幕参数要在跑协程**之前**设好（协程第一帧就落下，那时就要用到步数）
        c.收幕淡出步数 = Mathf.Max(0, 淡出步数);
        c.打完后的停留 = Mathf.Max(0f, 打完停留秒);
        c.打字速度覆盖 = 每秒字数 > 0f ? 每秒字数 : 0f;
        c.打字完成时刻 = -1f;                    // 本次是新演出，清掉上一次的
        c.可点击收幕时刻 = -1f;
        // ★「打完自动收」只在这里开：`说()` 那种只打字的调用不该自动收幕
        //   （否则开场三句 `说()` 之间黑幕会一黑一亮，见 逐行打 的说明）
        //   ⚠️ 必须在 StartCoroutine 之前设：协程第一帧就会调 落下()，
        //      而 落下() 会把 落幕后自动收 清成 false（起幕 = 取消上次残留的收幕）。
        c.待自动收 = false;
        c.StartCoroutine(c.逐行打(行 ?? new string[0], 0f, 淡入步数));
        c.落幕后自动收 = true;
        c.演出开始时刻 = Time.unscaledTime;
        // 死线兜底：按"最长一行的字数 / 打字速度"再放宽 15 秒，防止打字卡住导致永远黑屏
        int 最长 = 0;
        if (行 != null) foreach (var s in 行) if (s != null && s.Length > 最长) 最长 = s.Length;
        // 阶梯淡入本身也要时间：步数 / 淡步频
        float 淡入耗时 = 淡入步数 > 0 ? 淡入步数 / Mathf.Max(1f, c.淡步频) : 0f;
        c.自动收幕死线 = Time.unscaledTime + 淡入耗时 + (最长 / c.有效每秒字数)
            + Mathf.Max(2f, c.打完后的停留) + 15f;
        c.待自动收 = true;                       // 起幕后登记，避免被 落下() 清掉。

        Debug.Log("[黑幕开关] 待收=" + c.待自动收 + " 自动收=" + c.落幕后自动收
            + " 停留=" + c.打完后的停留.ToString("F2")
            + " 死线=" + c.自动收幕死线.ToString("F2")
            + " | timeScale=" + Time.timeScale.ToString("F2")
            + " 幕可见=" + c.幕在显示
            + " 实例=" + c.GetInstanceID());
    }

    /// <summary>
    /// **打完字后停在黑幕上、不自动收幕**，由调用方接着用。
    /// 用在「紧接着就要切场景」的场合：这样两段黑幕之间**不会淡出露画面**
    /// （用户报过：整块黑幕中间断一下）。调用方切完场景再自己 收幕()。
    ///
    /// <paramref name="打完停留秒"/>：字全部打出来之后**再停多久**才允许推进
    /// （用户 2026-09-27 要求 2 秒：「最后一个字打完马上转场，没有等 2 秒」）。
    /// 计时从"字打完"那一刻起算，不是从起幕算。
    /// </summary>
    public static void 下落并停留不收起(string[] 行, float 每秒字数 = 0f, int 淡入步数 = 0,
                                        float 打完停留秒 = 0f)
    {
        var c = 确保();
        c.待自动收 = false;
        c.落幕后自动收 = false;
        c.不自动收 = true;                 // 打完就停住，别收
        c.打完停留 = Mathf.Max(0f, 打完停留秒);
        c.字打完时刻 = -1f;
        c.放行截止时刻 = -1f;              // ★ 由 Update 在"字打完"那一刻算出来
        c.打字速度覆盖 = 每秒字数 > 0f ? 每秒字数 : 0f;
        c.打字完成时刻 = -1f;
        c.可点击收幕时刻 = -1f;
        c.演出开始时刻 = Time.unscaledTime;
        c.StartCoroutine(c.逐行打(行 ?? new string[0], 0f, 淡入步数));
    }

    /// <summary>这一次「不自动收幕」的演出，字打完之后还要停多久（0 = 打完即可推进）</summary>
    float 打完停留;

    /// <summary>"字打完"的时刻（用来算 放行截止时刻）</summary>
    float 字打完时刻 = -1f;

    /// <summary>
    /// **可以放行的时刻**（字打完 + 打完停留）。由 <see cref="Update"/> 算出，-1 = 还没算。
    ///
    /// ⚠️ 为什么不用一个"判断型 getter"（踩过的坑 2026-09-27）：
    ///   原来写成 `本句可以推进 { 字打完时刻 ??= 打字完成时刻; return now - 字打完时刻 >= 打完停留; }`，
    ///   但它是**被任务管理器每帧调用**的，带副作用 + 依赖调用时机，
    ///   实测放行只等了 0.27s 而不是 2s —— 时序完全失控。
    ///   改成"在 Update 里算出截止时刻、外部只读比较"，时序就确定了。
    /// </summary>
    float 放行截止时刻 = -1f;

    /// <summary>「不收幕」这条演出是否已经打完字 + 停够 打完停留 秒（任务管理器读它）</summary>
    public static bool 本句可以推进
        => 实例 != null && 实例.放行截止时刻 > 0f && Time.unscaledTime >= 实例.放行截止时刻;

    /// <summary>把"字打完 → 放行"这段实际等了多久打出来，供验证</summary>
    public static void 记录放行时刻(string 谁)
    {
        var c = 实例;
        if (c == null || c.字打完时刻 < 0f) return;
        Debug.Log("[黑幕计时] " + 谁 + " 放行：字打完于 t=" + c.字打完时刻.ToString("F2")
            + "，放行于 t=" + Time.unscaledTime.ToString("F2")
            + "，实际等了 " + (Time.unscaledTime - c.字打完时刻).ToString("F2")
            + "s（要求 " + c.打完停留.ToString("F1") + "s）");
        c.字打完时刻 = -1f;
    }

    /// <summary>这一次演出要"打完自动收幕"</summary>
    bool 待自动收;
    bool 字幕打字中;

    /// <summary>这一次演出打完字就停住、不自动收幕（见 下落并停留不收起）</summary>
    bool 不自动收;

    /// <summary>这次收幕要用的阶梯淡出档数（0 = 立刻收）</summary>
    int 收幕淡出步数;

    /// <summary>这一次演出开始计时的时刻（用来判定"同一帧内落下又收起"）</summary>
    float 演出开始时刻 = -1f;

    /// <summary>打字结束时刻（-1 = 还没打完）；自动收幕要等它出现</summary>
    float 打字完成时刻 = -1f;
    /// <summary>打字打完后再停留多久才收幕</summary>
    float 打完后的停留 = 0.5f;
    /// <summary>是否处于"打完就自动收幕"模式</summary>
    bool 落幕后自动收;

    int 诊断帧;
    /// <summary>兜底：万一打字卡住，最多黑屏这么久也要收幕</summary>
    float 自动收幕死线 = -1f;

    /// <summary>
    /// 这一次演出的**打字速度覆盖**（≤0 = 用 <see cref="每秒字数"/>）。
    /// 用户 2026-09-27：四幕「从此，一个平凡的少年踏上了修仙路」打得太快，要慢一点。
    /// </summary>
    float 打字速度覆盖;

    /// <summary>当前有效的每秒字数</summary>
    float 有效每秒字数 => 打字速度覆盖 > 0f ? 打字速度覆盖 : Mathf.Max(1f, 每秒字数);

    /// <summary>这一次演出打完字后「可以被点击收幕」的时刻（-1 = 还没打完）</summary>
    float 可点击收幕时刻 = -1f;

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
        // ★ 切场景过渡：由黑幕自己每帧推进（它带 DontDestroyOnLoad，跨场景不会被销毁）。
        //   不能写成协程 —— 协程挂在旧场景的任务管理器上，场景一卸载就死了。
        推进场景过渡();

        // ★「字打完 → 再停 打完停留 秒 → 放行」的计时**只在这里算**（每帧、确定性）。
        //   外部（任务管理器）只读 本句可以推进，不参与计时 —— 踩过的坑见 放行截止时刻 的说明。
        if (不自动收 || 待自动收)
        {
            if (打字完成时刻 > 0f && 字打完时刻 < 0f)
            {
                字打完时刻 = 打字完成时刻;
                放行截止时刻 = 字打完时刻 + 打完停留;
                Debug.Log("[黑幕] 字已全部打出（t=" + 字打完时刻.ToString("F2")
                    + "），放行截止 t=" + 放行截止时刻.ToString("F2")
                    + "（再停 " + 打完停留.ToString("F1") + "s）");
            }
        }

        // ★ 自动收幕：由黑幕自己判断，谁都杀不掉（落黑幕的协程会随切场景销毁）
        //   收幕条件 = 字打完 + 打完后又停留了 打完后的停留 秒；
        //   另有死线兜底，防止打字卡住导致永远黑屏。
        if (待自动收)
        {
            // 字打完的时刻 → 从那一刻起「点击可收幕」和「停留计时」同时开始
            if (打字完成时刻 > 0f && 可点击收幕时刻 < 0f)
                可点击收幕时刻 = 打字完成时刻;

            // ★ 兜底：如果「打字完成时刻」因为任何原因没写上，这里自己判定一次 ——
            //   幕已经全黑（淡入协程结束）且文字不再变化，就算打完了。
            if (打字完成时刻 <= 0f && !字幕打字中 && 淡入协程 == null && 幕在显示)
            {
                打字完成时刻 = Time.unscaledTime;
                Debug.Log("[黑幕] 退而求其次：由「幕已全黑」判定打字完成");
            }

            bool 到点 = 打字完成时刻 > 0f && Time.unscaledTime >= 打字完成时刻 + 打完后的停留;
            // 不想等就点一下左键（但要等字打完，否则会打断打字）
            bool 点击跳过 = 可点击收幕时刻 > 0f && Input.GetMouseButtonDown(0);
            bool 超时 = 自动收幕死线 > 0f && Time.unscaledTime >= 自动收幕死线;

            // 临时诊断：每 30 帧报一次"为什么还没收"
            诊断帧++;
            if (!到点 && !超时 && 诊断帧 % 30 == 0)
                Debug.Log("[黑幕为啥没收] 打字完成时刻=" + 打字完成时刻.ToString("F2")
                    + " 打完停留=" + 打完后的停留.ToString("F2")
                    + " now=" + Time.unscaledTime.ToString("F2")
                    + " 到点=" + 到点 + " 超时=" + 超时
                    + " 死线=" + 自动收幕死线.ToString("F2")
                    + " 淡入协程=" + (淡入协程 != null ? "在跑" : "null"));

            if (到点 || 点击跳过 || 超时)
            {
                待自动收 = false;
                落幕后自动收 = false;
                演出开始时刻 = -1f;
                自动收幕死线 = -1f;
                可点击收幕时刻 = -1f;
                string 因 = 到点 ? ("字已打完 + 停留 " + 打完后的停留.ToString("F1") + "s")
                          : 点击跳过 ? "玩家点击跳过"
                          : "超时兜底";
                Debug.Log("[黑幕] 自动收幕（" + 因 + "，淡出 " + 收幕淡出步数 + " 档）t=" + Time.unscaledTime.ToString("F2"));
                立即收掉(收幕淡出步数);
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
    public IEnumerator 逐行打(string[] 行, float 额外停顿) => 逐行打(行, 额外停顿, 默认淡入步数);

    /// <summary>
    /// 逐行打字（可指定阶梯淡入档数；0 = 立刻全黑）。
    ///
    /// ⚠️ 这里**不**打开"打完自动收"。
    /// 踩过的坑（2026-09-27）：一度在这里设了 <c>落幕后自动收 = true</c>，
    /// 于是 `说()` 这种"只负责打一段字"的调用也会在打完 0.5 秒后自动收幕 ——
    /// 开场是**三句分开 yield 的 `说()`**，结果每句之间黑幕都消失一次，
    /// 用户看到的就是"开头那几个黑幕不连贯，每过一个黑幕要取消一下"。
    /// 自动收幕只应该由 <see cref="下落并定时收起"/> 这种"整段过场"来开启。
    /// </summary>
    public IEnumerator 逐行打(string[] 行, float 额外停顿, int 淡入步数)
    {
        字幕打字中 = true;
        落下(淡入步数);
        立即清空();
        while (淡入协程 != null) yield return null;
        打字完成时刻 = -1f;
        可点击收幕时刻 = -1f;
        for (int i = 0; i < 行.Length; i++)
        {
            立即清空();
            yield return 打一行(行[i]);
            float 停 = 行间停顿 + 额外停顿;
            if (停 > 0f) yield return new WaitForSecondsRealtime(停);
        }
        字幕打字中 = false;
    }

    IEnumerator 打一行(string 整句)
    {
        全句 = 整句 ?? "";
        文本.text = "";

        float 出 = 0f;
        while (出 < 全句.Length)
        {
            // 长按左键 → 加速（用户要求，保留）
            float 倍 = Input.GetMouseButton(0) ? Mathf.Max(1f, 长按加速倍率) : 1f;

            // ★ 没有"点一下直接打完"这条路了（用户 2026-09-27 明确要求）：
            //   黑幕的字**必须逐字打出来**。
            //   原来这里有个 `if (GetMouseButtonDown(0)) { 文本.text = 全句; break; }`，
            //   而新档开局玩家一定点过鼠标（主菜单选档位），那次点击会被第一句吃掉
            //   → 「这是一个漫长的故事」整句蹦出来、完全没有打字过程（实测踩过两次）。
            //   想快点看就**长按左键**（上面的 倍），不要用单击跳过。

            出 += 有效每秒字数 * 倍 * 每帧上限(Time.unscaledDeltaTime);
            int 个数 = Mathf.Clamp(Mathf.FloorToInt(出), 0, 全句.Length);
            if (文本.text.Length != 个数) 文本.text = 全句.Substring(0, 个数);
            yield return null;
        }
        文本.text = 全句;
        打字完成时刻 = Time.unscaledTime;      // ★ 自动收幕要等这个时刻出现
    }

    /// <summary>
    /// 把每帧的步进**夹住上限**。
    ///
    /// 踩过的坑（2026-09-27）：新档第一帧必定有一次超长帧卡顿
    /// （场景加载 + 首次初始化全挤在那一帧），`unscaledDeltaTime` 会变成一个很大的值，
    /// 于是 `出 += 每秒字数 × 那个值` **一次就超过整句长度** →
    /// 「这是一个漫长的故事」整句蹦出来、完全没有打字过程（后面几句却正常）。
    /// 夹到 1/30 秒之后，无论卡多久，一帧最多只推进 1/30 秒的字。
    /// </summary>
    static float 每帧上限(float dt) => Mathf.Min(dt, 1f / 30f);

    [Tooltip("当前这一行的完整文本（打字机内部用）")]
    string 全句;

    public void 立即清空() { if (文本 != null) 文本.text = ""; }

    // ============================================================ 幕 / 闪白

    void 落下() => 落下(默认淡入步数);

    /// <summary>
    /// 落黑幕。步数 ≤ 0 = **立刻全黑**（默认）；&gt; 0 = 分这么多档**阶梯式**暗下去。
    ///
    /// 做成立即的原因（用户 2026-09-27 更正）：不是每个黑幕都要淡入 ——
    /// 「切场景的黑幕」「拿黑幕当遮罩」这些都要瞬间盖住，淡入反而会把画面露出来。
    /// 只有明确要"一帧一帧暗下去"的过场才传步数。
    /// </summary>
    void 落下(int 步数)
    {
        // ★ 起幕 = 取消一切"正在收幕/打完自动收"的残留状态。
        //   否则上一次演出排下的收幕协程会在这一帧之后把幕抹掉（见 收起 的说明）。
        落幕后自动收 = false;
        自动收幕死线 = -1f;
        可点击收幕时刻 = -1f;
        打字完成时刻 = -1f;
        if (淡入协程 != null) { StopCoroutine(淡入协程); 淡入协程 = null; }
        // 起幕要能打断"正在进行的淡出"，否则两边会抢 alpha（旧淡出把新幕又抹回去）
        if (收起协程 != null) { StopCoroutine(收起协程); 收起协程 = null; }
        正在收幕 = false;

        if (幕 != null) 幕.gameObject.SetActive(true);
        if (画布 != null) 画布.enabled = true;
        // 注意：**不**在这里清 演出开始时刻 —— 它由 下落并定时收起 在起幕前设好，
        //       守卫（同一帧内不许收幕）要靠它。落下幕() 这种即起即收的路径用
        //       「幕此刻是否可见」来判断就够了（见 立即收掉）。
        if (演出开始时刻 < 0f) 演出开始时刻 = Time.unscaledTime;

        if (步数 <= 0)
        {
            设幕透明度(1f);
            return;
        }
        设幕透明度(0f);            // 从全透明起，马上交给阶梯协程
        淡入协程 = StartCoroutine(阶梯淡(步数, 1f));
    }

    /// <summary>幕和字一起设透明度（0 = 全透明，1 = 全不透明）</summary>
    void 设幕透明度(float a)
    {
        if (幕 != null) 幕.color = new Color(幕色.r, 幕色.g, 幕色.b, 幕色.a * a);
        // 字比幕晚一点出来：前半段只有幕在暗下去，看起来更像"暗场"而不是"字浮上来"
        float 字a = Mathf.Clamp01((a - 0.5f) * 2f);
        if (文本 != null) 文本.color = new Color(字色.r, 字色.g, 字色.b, 字色.a * 字a);
    }

    Coroutine 淡入协程;
    Coroutine 收起协程;

    /// <summary>
    /// **阶梯式**淡入 / 淡出：把 0→目标 分成 `步数` 档，每档之间按 <see cref="淡步频"/> 停顿。
    ///
    /// 为什么不用 Lerp 平滑：用户要的是"**一帧一帧**"的观感（像老式过场的跳帧渐暗），
    /// 平滑插值看起来是一团糊的渐变，不是那个味道。
    /// </summary>
    IEnumerator 阶梯淡(int 步数, float 目标)
    {
        float 起始 = 当前幕透明度();
        float 每步 = Mathf.Max(0.02f, 1f / Mathf.Max(1f, 淡步频));
        for (int i = 1; i <= 步数; i++)
        {
            // 每一档直接跳到该档的透明度，中间不插值 → 阶梯感
            设幕透明度(Mathf.Lerp(起始, 目标, (float)i / 步数));
            float t = 0f;
            while (t < 每步) { t += Time.unscaledDeltaTime; yield return null; }
        }
        设幕透明度(目标);
        // ★ 收尾必须把句柄清掉！踩过的坑（2026-09-27）：
        //   淡入协程跑完却不置 null，于是 `淡入协程 != null` 永远成立，
        //   判断"淡入结束了吗"的地方全部失效 ——
        //   实测「从此…」那句卡死在阶段3：幕已经全黑了，条件却永远不满足。
        淡入协程 = null;
    }

    float 当前幕透明度() => 幕 != null ? Mathf.Clamp01(幕.color.a / Mathf.Max(0.001f, 幕色.a)) : 0f;

    /// <summary>阶梯淡入/淡出的步频（步/秒）。给外部算"N 档 ≈ 几秒"用</summary>
    public static float 取淡步频()
    {
        var c = 实例;
        return c != null ? Mathf.Max(1f, c.淡步频) : 12f;
    }

    /// <summary>N 档阶梯淡入/淡出大约要几秒（= 档数 ÷ 淡步频）。给任务表算「等待秒」用</summary>
    public static float 档数换算秒(int 档数) => 档数 / 取淡步频();

    /// <summary>黑幕从当前透明度阶梯淡到透明并关掉（步数 ≤ 0 = 直接收）</summary>
    IEnumerator 淡出幕(int 步数)
    {
        if (步数 > 0) yield return 阶梯淡(步数, 0f);
        立即清空();                               // ★ 字跟幕一起淡完才清（见 立即收掉 的说明）
        if (幕 != null) 幕.gameObject.SetActive(false);
        if (文本 != null) 文本.color = 字色;      // 还原，下次不用重新初始化
        if (画布 != null) 画布.enabled = false;
        收起协程 = null;
        正在收幕 = false;                        // 收完了，允许下一次收幕
    }

    public void 收起() => 收起(默认淡出步数);

    /// <summary>
    /// 收黑幕。步数 ≤ 0 = **立刻收掉**（默认）；&gt; 0 = 阶梯式淡出去。
    ///
    /// ⚠️ 立刻收掉是有意的：切场景时黑幕要一路盖着，新场景加载完才允许收
    /// （淡出会把加载中途的画面露出来）。
    /// ⚠️⚠️ **同帧内「落下 → 收起」必须合并**（踩过的坑，见 <see cref="落下(int)"/>）：
    /// `Awake` 里会调一次 收起 把幕藏起来，而 `Start` 紧接着就会 说()/落下()。
    /// 如果那次收起还是"排一个几帧后才跑的淡出协程"，它就会在**下一帧把刚落下的幕抹掉**
    /// —— 表现是「新档开场的第一个黑幕不见了」。所以同一帧里以最后一次调用为准。
    /// </summary>
    public void 收起(int 步数)
    {
        // 「同一帧内刚开了一段会自动收幕的演出，又有人来收幕」= 互相打架，以起幕为准。
        // （踩过：Awake 里那次 收起 排下的协程，把 Start 里刚落下的开场黑幕抹掉了）
        if (落幕后自动收 && 演出开始时刻 >= 0f && Time.unscaledTime - 演出开始时刻 < 0.001f)
        {
            Debug.LogWarning("[黑幕] 同一帧内「开始演出 → 收幕」，已忽略这次收幕（否则会立刻抹掉刚显示的幕）");
            return;
        }
        立即收掉(步数);
    }

    /// <summary>
    /// 真正执行收幕（不做同帧判定）。
    ///
    /// ⚠️ <see cref="正在收幕"/> 必须在这里**同步**设上，不能放在协程里 ——
    /// 踩过的坑（2026-09-27，表现是"黑幕卡住、点左键和等待都没反应"）：
    /// 原来这个标志在 `淡出幕` 协程的第一行才设，而协程要到**下一帧**才跑。
    /// 于是同一个 `Update` 里可能第二次调到这里 →
    /// `StopCoroutine(收起协程)` 把**刚起步还没跑第一行**的淡出协程掐死 → 再 Start 一个 →
    /// 下一帧又重复……淡出永远跑不完，幕永远关不掉、`演出中` 永远为真、阶段永远不推进。
    /// </summary>
    void 立即收掉(int 步数)
    {
        if (正在收幕) return;            // 已经在淡出中，别重入（否则会不停 Stop+Start 掐死它）
        正在收幕 = true;

        // ★ 这里**不**清字：字要跟着幕一起淡出。
        //   踩过的坑（用户 2026-09-27："从此"黑幕和切场景黑幕之间会短暂断开一下）：
        //   原来在淡出**开始前**就 立即清空()，于是淡出的那两秒是"黑而空白"，
        //   然后下一段才把「太虚宗」打出来 —— 看起来就是黑幕断了一下。
        //   清字挪到 淡出幕 的结尾（那时幕已经不可见了）。
        结束演出();
        锁层数 = 0;
        if (淡入协程 != null) { StopCoroutine(淡入协程); 淡入协程 = null; }
        // ⚠️ 已经藏起来了就别再排协程：Awake 里会调一次，那一下必须当场生效
        if (!幕在显示)
        {
            立即清空();
            if (画布 != null) 画布.enabled = false;
            正在收幕 = false;
            return;
        }
        收起协程 = StartCoroutine(淡出幕(Mathf.Max(0, 步数)));
    }

    /// <summary>正在执行淡出（防重入；由 立即收掉 同步设上、由 淡出幕 收尾清掉）</summary>
    bool 正在收幕;

    /// <summary>
    /// **字打完、且幕已经全黑** —— 用作 `条件=黑幕落下完成` 的判据。
    ///
    /// 为什么需要它（用户 2026-09-27 报「两段黑幕中间断开一下」）：
    ///   四幕「从此…」那句原本是 `自动收幕`，也就是**把幕淡出到全透明**，
    ///   然后下一阶段的切场景过渡再把 alpha 设回 1 —— 那一瞬 alpha 掉下去，
    ///   看起来就是整块黑幕断了一下。
    ///   改成：这一句**不收幕**，幕保持全黑；`条件=黑幕落下完成` 一满足就进下一阶段，
    ///   由切场景过渡**原地接着用**这块黑幕（只换字），全程 alpha 不掉。
    /// </summary>
    public static bool 字打完且幕全黑
    {
        get
        {
            var c = 实例;
            if (c == null) return false;
            if (!c.幕在显示) return false;                 // 幕还没落/已经收了
            if (c.淡入协程 != null) return false;          // 还在淡入
            if (c.正在收幕) return false;                  // 正在收幕（说明在淡出）
            if (c.打字完成时刻 <= 0f) return false;        // 字还没打完
            return true;
        }
    }

    /// <summary>
    /// **阶梯淡入已经跑完**（幕已经全黑）。
    /// 任务表的 `条件=黑幕落下完成` 用它 —— 否则 `条件=无` 会在下一帧就推进，
    /// 把还没淡完的黑幕直接收掉（表现成"黑幕一闪就没了"）。
    /// </summary>
    public static bool 落黑幕完成
    {
        get
        {
            var c = 实例;
            if (c == null) return true;                 // 没有黑幕对象 = 没什么可等的
            if (!c.幕在显示) return true;               // 幕都收着了，算"完成"，别把阶段卡死
            return c.淡入协程 == null;                  // 淡入协程结束 = 已经全黑
        }
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
