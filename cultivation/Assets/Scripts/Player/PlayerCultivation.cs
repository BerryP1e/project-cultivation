using System;
using UnityEngine;

/// <summary>
/// **玩家的修为/境界运行时。** 按《修仙境界 &amp; 功法数值体系设计》实现。
///
/// ## 核心模型
///
/// · **总灵气是角色通用积累，与功法无关** —— 功法只决定「灵气值对应什么境界」的换算比例。
/// · **等效基准灵气 S = 总灵气 ÷ 难度系数 K**，K = 功法难度等级 ÷ 100。
///   拿 S 去对照境界表的累计灵气阈值，就是当前境界。
/// · **杀怪给「修炼次数」**（次数 = 基础 1 次 × 等级差系数），
///   回修炼小屋消耗次数一次性转成灵气。
/// · 单次修炼产出的灵气**只随大境界提升、且不乘 K** ——
///   所以难度越高的功法，同样一次修炼推不动境界，需要更多怪 ✓（这正是设计意图）
///
/// ## 为什么 修炼次数 内部存 float
///
/// 等级差系数有 0.2 / 0.5 / 0.8 这种小数（打低级怪只给 0.2 次），
/// 用 int 存会直接抹成 0。所以内部累积 float，对外给整数值 ——
/// 打 5 只低级怪 = 攒够 1 次 ✓
///
/// 挂在 **Player** 上。
/// </summary>
public class PlayerCultivation : MonoBehaviour
{
    [Header("引用（留空自动找）")]
    [Tooltip("角色面板数据，当前功法从这里的「当前功法」读")]
    public UIPanelData 面板数据;

    [Header("境界表")]
    [Tooltip("90 个境界定义，按等级升序。用菜单「修仙/接线玩家修炼系统」自动填好")]
    public RealmDefinition[] 境界表;

    [Tooltip("全部功法（按 id 查用，存档读档要按 id 还原）。用菜单「修仙/接线玩家修炼系统」自动填")]
    public GongFaDefinition[] 功法表;

    [Header("修为（存档要保存）")]
    [Tooltip("总灵气。与功法无关的通用积累")]
    public long 总灵气;

    [Tooltip("修炼次数的**小数累积**。对外看 修炼次数（整数）")]
    public float 修炼次数累积;

    [Header("杀怪掉落修炼次数")]
    [Tooltip("同级怪物的基础掉落次数")]
    public float 同级基础次数 = 1f;

    [Tooltip("等级差系数（按「怪物等级 − 玩家等级」分段）：≤−5 / −4~−3 / −2~−1 / 0 / +1~+2 / +3~+4 / ≥+5")]
    public float[] 等级差系数 = { 0.2f, 0.5f, 0.8f, 1.0f, 1.5f, 2.0f, 3.0f };

    [Header("调试")]
    public bool 打印修为日志 = true;

    // ============================================================ 派生状态

    /// <summary>当前修炼的功法</summary>
    public GongFaDefinition 当前功法 => 面板数据 != null ? 面板数据.当前功法 : null;

    /// <summary>难度系数 K = 功法难度等级 ÷ 100（以 D=100 为基准 → K=1.0）</summary>
    public float 难度系数
    {
        get
        {
            var g = 当前功法;
            return g != null ? Mathf.Max(0.01f, g.难度等级 / 100f) : 1f;
        }
    }

    /// <summary>等效基准灵气 S = 总灵气 ÷ K。用它去对照境界表</summary>
    public long 等效基准灵气 => (long)(总灵气 / 难度系数);

    /// <summary>
    /// 当前境界（按等效基准灵气 S 查境界表）。
    ///
    /// 【语义】表里的 ``累计灵气(n)`` = **从 1 级升到 n+1 级所需的总灵气**，
    /// 所以「累计 <= S 的最大 n」对应的当前等级是 **n + 1** 而不是 n。
    /// 一开始少加了这一级，导致 S 刚好等于某个累计值时卡在 100% 不升级 ✗
    /// </summary>
    public RealmDefinition 当前境界
    {
        get
        {
            if (境界表 == null || 境界表.Length == 0) return null;
            return 查境界(等效基准灵气) ?? 境界表[0];
        }
    }

    /// <summary>按等级取境界定义</summary>
    public RealmDefinition 取境界(int 等级)
    {
        if (境界表 == null) return null;
        for (int i = 0; i < 境界表.Length; i++)
            if (境界表[i] != null && 境界表[i].等级 == 等级) return 境界表[i];
        return null;
    }

    /// <summary>当前修为等级（1~90）</summary>
    public int 等级 => 当前境界 != null ? 当前境界.等级 : 1;

    /// <summary>当前境界名，例如「炼气第1层」</summary>
    public string 境界名 => 当前境界 != null ? 当前境界.境界名 : "—";

    /// <summary>九大境界名，例如「炼气」</summary>
    public string 大境界名 => 当前境界 != null ? 当前境界.大境界.ToString() : "—";

    /// <summary>升到下一级还需要多少**基准**灵气（不含 K）</summary>
    public long 升级所需灵气 => 当前境界 != null ? 当前境界.升级所需灵气 : 0;

    /// <summary>本级的起点累计（= 上一级的 累计灵气；等级 1 时为 0）</summary>
    public long 本级起点累计 => 取累计(等级 - 1);

    /// <summary>本级已经积累的基准灵气</summary>
    public long 本级已积累 => Math.Max(0L, 等效基准灵气 - 本级起点累计);

    /// <summary>当前小境界的进度 0~1（满级恒为 1）</summary>
    public float 进度
    {
        get
        {
            var d = 当前境界;
            if (d == null) return 0f;
            if (d.突破类型 == BreakthroughKind.满级) return 1f;
            // 分母用"到门槛还差多少"：L 级要从 取累计(L-1) 攒到 取累计(L)
            long 需 = System.Math.Max(1L, 取累计(d.等级) - 本级起点累计);
            return Mathf.Clamp01(本级已积累 / (float)需);
        }
    }

    /// <summary>
    /// **这一级的灵气攒满了吗**（可以尝试破境了）。
    ///
    /// ⚠️【踩过的坑·2026-10-01】判据**不能写成 `进度 >= 1f`** —— 那是永远不成立的！
    ///
    /// 原因：`进度` 的区间是 <c>[取累计(L-1), 取累计(L))</c>，
    /// 而 `查境界` 的晋级判据是 `累计灵气 &lt;= S` ⇒ **`S` 一到 `取累计(L)` 就立刻变成 L+1 级**、
    /// 进度当场归 0。所以 `进度` 的**上确界是 1 但取不到**，最高只会到 99.x%。
    ///
    /// 正确的判据是"**已经够到下一级的门槛了**" —— 也就是
    /// `等效基准灵气 >= 取累计(等级)`。
    ///
    /// > 通用教训：**用"区间型"进度做阈值判定时，先想清楚端点归谁。**
    /// > 半开区间 `[a, b)` 上的进度，`>= 1` 是个够不到的条件。
    /// </summary>
    public bool 本级已满
    {
        get
        {
            var d = 当前境界;
            if (d == null) return false;
            if (d.突破类型 == BreakthroughKind.满级) return false;
            // 有了破境封顶之后，判据就很直白了：**本级进度攒到 100% 就能破**。
            // S 超过门槛也不会晋级（被 查境界 的封顶挡住），所以进度会停在 100%。
            return 本级进度 >= 1f;
        }
    }

    /// <summary>单次修炼能拿到的灵气（只随大境界变，**不乘 K**）</summary>
    public float 单次修炼灵气 => 当前境界 != null ? 当前境界.单次修炼灵气 : 2f;

    [Header("破境（用户 2026-10-01 定：灵气攒满必须手动破境）")]
    [Tooltip("**已解锁的最高等级**。`查境界` 不会再给出比它更高的等级。\n\n" +
             "【为什么需要这个字段】原来 `查境界` 是 `等级 = f(总灵气)` 的**纯函数** ——\n" +
             "灵气一够就自动涨级，于是「攒满但还没破境」这个状态**根本不存在**，\n" +
             "破境按钮没机会触发、失败也没意义。\n" +
             "加了这个封顶之后：灵气只涨进度，**攒满就卡在 100%**，\n" +
             "必须点破境（并成功）才把封顶 +1。")]
    public int 已解锁最高等级 = 1;

    /// <summary>
    /// **破境加成**（0~1 加法）。= 服下对应大境界的**破境丹**时写进来的那份提升
    /// （`RealmDefinition.服丹成功率` − `RealmDefinition.基础成功率`）。
    ///
    /// 【为什么是一个"值"而不是一个"有没有"的开关】用户 2026-10-01：
    /// > 「成功率就是个时时变化的值，服用后改变这个值就好了」
    /// 所以 UI 只显示 <see cref="当前破境成功率"/> 这**一个数**，服丹它就自己变；
    /// 不再出现"75% → 服丹后 95%"这种并排两个静态数字。
    ///
    /// 【生命周期】服丹时写入 → `尝试破境()` **不论成败都清空**（丹在背包里"使用"那一刻
    /// 就已经消耗掉了，这里只是把那份加成用掉，否则一颗丹能反复生效）。
    /// **要进存档**（用户 2026-10-01 明确要求），否则服了丹还没破境就退出会白吃一颗。
    /// </summary>
    public float 破境加成 = 0f;

    /// <summary>当前的破境成功率（基础 + 丹药加成），实时值 —— UI 直接显示它</summary>
    public float 当前破境成功率
    {
        get
        {
            if (当前境界 == null) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(当前境界.基础成功率) + Mathf.Clamp01(破境加成));
        }
    }

    /// <summary>写入破境加成（由 `服丹效果` 调）</summary>
    public void 设置破境加成(float 加成)
    {
        破境加成 = Mathf.Clamp01(加成);
        修为变化?.Invoke(this);
    }

    /// <summary>清掉破境加成（破境之后 / 读档重置时用）</summary>
    public void 清空破境加成()
    {
        if (破境加成 <= 0f) return;
        破境加成 = 0f;
        修为变化?.Invoke(this);
    }

    /// <summary>剩余修炼次数（整数）</summary>
    public int 修炼次数 => Mathf.Max(0, Mathf.FloorToInt(修炼次数累积));

    /// <summary>下一次修炼还差的次数（0~1 的小数进度）</summary>
    public float 次数小数部分 => Mathf.Clamp01(修炼次数累积 - Mathf.Floor(修炼次数累积));

    /// <summary>是不是满级了</summary>
    public bool 已满级 => 当前境界 != null && 当前境界.突破类型 == BreakthroughKind.满级;

    /// <summary>当前这一级要哪种突破</summary>
    public BreakthroughKind 突破类型 => 当前境界 != null ? 当前境界.突破类型 : BreakthroughKind.未指定;

    /// <summary>突破材料 id（留空 = 策划还没定）</summary>
    public string 突破材料 => 当前境界 != null ? 当前境界.突破材料 : "";
    public int 材料数量 => 当前境界 != null ? 当前境界.材料数量 : 1;

    // ---- ASCII 别名 ----
    public long TotalSpirit => 总灵气;
    public int Level => 等级;
    public float DifficultyK => 难度系数;
    public long EffectiveSpirit => 等效基准灵气;
    public int CultivationCharges => 修炼次数;
    public float Progress => 进度;
    public bool IsMaxLevel => 已满级;

    // ============================================================ 事件

    /// <summary>修为有变化（灵气 / 次数 / 等级 / 换功法）</summary>
    public event Action<PlayerCultivation> 修为变化;

    /// <summary>修炼了一次（参数：本次获得的灵气）</summary>
    public event Action<PlayerCultivation, float> 修炼了;

    /// <summary>境界等级变了（参数：旧等级、新等级）</summary>
    public event Action<PlayerCultivation, int, int> 等级变化;

    /// <summary>杀了怪拿到修炼次数（参数：怪物等级、本次次数）</summary>
    public event Action<PlayerCultivation, int, float> 击杀获得次数;

    void Awake()
    {
        if (面板数据 == null) 面板数据 = GetComponent<UIPanelData>();
        if (面板数据 == null) 面板数据 = FindObjectOfType<UIPanelData>();
        确保境界表();
    }

    int 上次等级 = -1;

    void Update()
    {
        int 现 = 等级;
        if (上次等级 < 0) { 上次等级 = 现; return; }
        if (现 != 上次等级)
        {
            var 旧 = 上次等级;
            上次等级 = 现;
            if (打印修为日志)
                Debug.Log("[修炼] 境界变化：" + 旧 + " 级 → " + 现 + " 级（" + 境界名 + "）", this);
            等级变化?.Invoke(this, 旧, 现);
            修为变化?.Invoke(this);
        }
    }

    /// <summary>境界表没接线时，自动从 Resources 兜底找一遍</summary>
    void 确保境界表()
    {
        if (境界表 != null && 境界表.Length > 0) return;
        var 找 = Resources.LoadAll<RealmDefinition>("");
        if (找 != null && 找.Length > 0)
        {
            Array.Sort(找, (a, b) => a.等级.CompareTo(b.等级));
            境界表 = 找;
        }
    }

    // ============================================================ 修炼

    /// <summary>
    /// **消耗 1 次修炼，把次数转成灵气。** 返回本次获得的灵气（0 = 没次数了）
    /// </summary>
    public float 修炼一次()
    {
        if (修炼次数 <= 0)
        {
            if (打印修为日志) Debug.Log("[修炼] 没有修炼次数了", this);
            return 0f;
        }

        // ★ 本级的灵气攒满（100%）之后**不再涨** —— 必须先破境，否则修炼是浪费次数。
        //   这正是用户要的"最多修炼到 100"。
        if (本级已满)
        {
            if (打印修为日志) Debug.Log("[修炼] 本境界已圆满，需先破境才能继续积累灵气", this);
            return 0f;
        }

        float 得 = 单次修炼灵气;
        修炼次数累积 = Mathf.Max(0f, 修炼次数累积 - 1f);
        总灵气 += (long)得;

        if (打印修为日志)
            Debug.Log("[修炼] 修炼一次：+" + 得.ToString("0.#") + " 灵气（共 " + 总灵气
                + "｜等效 " + 等效基准灵气 + "｜剩 " + 修炼次数 + " 次）", this);

        修炼了?.Invoke(this, 得);
        修为变化?.Invoke(this);
        return 得;
    }

    /// <summary>一次性把次数全用掉（「闭关」按钮）。返回获得的总灵气</summary>
    public long 闭关全部修炼()
    {
        // ⚠️【踩过的坑·2026-10-01】这个 while 里**必须有"灵气满了就停"的判据**！
        //
        //   原来的写法是 `while (修炼次数 > 0)` 里做 `修炼次数累积 -= 1f` ——
        //   看起来会终止，但 `修炼次数` 是 `Mathf.FloorToInt(修炼次数累积)`（整数）。
        //   灵气一旦攒满（本级进度 100%），`修炼一次()` 会在扣次数**之前**返回 0；
        //   而这里没那个闸门，于是：扣 1 次 → 加灵气 → 次数是整数不可能被小数消耗掉
        //   ⇒ **`修炼次数` 永远是同一个值 → 死循环，游戏直接卡死**。
        //
        //   两个闸门一起加：① 每轮先查 `本级已满`；② 循环条件里也带上 —— 双保险。
        long 共 = 0;
        while (修炼次数 > 0 && !本级已满)
        {
            // ★ 必须调 修炼一次() 而不是自己抄一遍扣减逻辑 —— 它就是那个带闸门的实现。
            //   自己抄 = 抄掉闸门（这正是本 bug 的由来）。
            float 得 = 修炼一次();
            if (得 <= 0f) break;          // 没次数了 / 灵气满了 → 收手，别死转
            共 += (long)得;
        }
        if (共 > 0)
        {
            if (打印修为日志) Debug.Log("[修炼] 闭关：+" + 共 + " 灵气（共 " + 总灵气 + "）", this);
            修为变化?.Invoke(this);
        }
        return 共;
    }

    // ============================================================ 破境（带概率）

    /// <summary>
    /// **尝试破境**（用户 2026-10-01 定的规则）。
    ///
    /// ## 规则
    ///
    /// · 灵气必须**攒满**（<see cref="进度"/> >= 1），否则不受理
    /// · **小境界突破**（1→2 … 9→10）**有概率**：
    ///   基础成功率 = <see cref="RealmDefinition.基础成功率"/>；
    ///   服下**对应大境界**的破境丹 → 抬到 <see cref="RealmDefinition.服丹成功率"/>
    /// · **失败就要重新刷一整个小境界** —— 把进度打回
    ///   <see cref="RealmDefinition.失败保留进度"/>（默认 0 = 全清）
    /// · 不论成败，**服下的丹都会消耗掉**
    /// · **大境界突破**（10→下一境界1）需要**专属丹药**，策划说"后续再说" ——
    ///   所以这里**故意不受理**，并明确告诉玩家缺什么，**绝不静默白送一级**
    ///
    /// 【为什么用 `总灵气` 反推进度而不是单独存一个"经验"字段】
    /// 这个系统的等级本来就是**从总灵气算出来的**（见 <see cref="查境界"/>）。
    /// 单独存一份进度会立刻和总灵气不同步 —— 那是最难查的一类 bug。
    ///
    /// 【丹药从哪来】参 <see cref="PlayerCultivation.丹药查找器"/>。
    /// 留空时退到 `QuestDatabase.物品库` + `UIPanelData`（背包）。分开是为了
    /// **修为系统不必依赖 UI 面板**（修为是纯数值，UI 是表现层）。
    /// </summary>
    public 破境结果 尝试破境()
    {
        if (当前境界 == null) return 破境结果.拒绝("境界表没接线，无法破境");
        if (已满级) return 破境结果.拒绝("已至巅峰，无需再破境");

        // ---- 1) 灵气够不够 ----
        // ★ 用 本级已满 而不是 进度 >= 1f（后者永远不成立，见 本级已满 的注释）
        if (!本级已满)
            return 破境结果.拒绝("灵气未满（" + (本级进度 * 100f).ToString("0.#") + "%），先去闭关修炼");

        var 级 = 当前境界;

        // ---- 2) 大境界突破：专属丹药还没做，明确拒绝（别静默白送） ----
        if (级.突破类型 == BreakthroughKind.大境界突破)
            return 破境结果.拒绝("大境界突破需要「" + (string.IsNullOrEmpty(级.突破材料) ? "专属丹药（尚未定名）" : 级.突破材料)
                                 + "」，此物尚未现世");

        // ---- 3) 成功率 = 基础 + 已服丹药的加成（**一个实时值**，见 当前破境成功率） ----
        // 丹药是在**背包里"服用"**那一刻消耗掉的（`服丹效果`），这里只读它留下的加成。
        // 小境界突破**不需要"提交物品"**，所以这里也没有任何材料检查。
        bool 有丹 = 破境加成 > 0.001f;
        float 成功率 = 当前破境成功率;

        // ---- 4) 判定 ----
        // ⚠️ 必须写全 `UnityEngine.Random`：本文件有 `using System;`，
        //    裸写 `Random` 会在 System.Random 和 UnityEngine.Random 之间歧义（CS0104）。
        //    而且两者语义完全不同 —— System.Random 没有静态 value，写错了编译能过但行为不是随机的。
        bool 成功 = UnityEngine.Random.value < 成功率;

        // ---- 5) 把丹药那份加成用掉（不论成败）----
        // 丹早就被吃掉了，这里不清的话一颗丹能反复生效。
        清空破境加成();

        // ---- 6) 改灵气 ----
        // 本级起点（进度 0 的位置）与下一级起点（进度 100% 的位置）
        long 本级起点 = 取累计(级.等级 - 1);
        long 下一级 = 取累计(级.等级);

        if (成功)
        {
            // ★ 成功才解锁下一级。原来这里只是把灵气顶上去、
            //   靠 查境界 自动涨级 —— 那样"失败"就永远没意义了。
            已解锁最高等级 = Mathf.Clamp(级.等级 + 1, 1, 90);
            设置总灵气(Math.Min(下一级, (long)(本级起点累计 + 级.升级所需灵气)));
            Debug.Log($"[破境] ✓ 成功：{级.境界名} → 已解锁 {已解锁最高等级} 级"
                      + $"（成功率 {成功率:P0}{(有丹 ? "，服丹" : "")}）");
            修为变化?.Invoke(this);
            return new 破境结果
            {
                受理 = true, 成功 = true, 成功率 = 成功率, 服了丹 = 有丹,
                文本 = "破境成功！" + (有丹 ? "（服丹相助）" : ""),
            };
        }

        // 失败：进度打回「失败保留进度」
        float 保留 = Mathf.Clamp01(级.失败保留进度);
        long 新S = 本级起点 + (long)((下一级 - 本级起点) * 保留);
        设置总灵气(新S);

        Debug.Log($"[破境] ✗ 失败：{级.境界名}（成功率 {成功率:P0}{(有丹 ? "，服丹" : "")}）"
                  + $"→ 进度保留 {保留:P0}，需重新积攒");
        修为变化?.Invoke(this);

        return new 破境结果
        {
            受理 = true, 成功 = false, 成功率 = 成功率, 服了丹 = 有丹,
            文本 = 保留 <= 0f
                ? $"破境失败（成功率 {成功率:P0}），这一层的灵气散了，需重新积攒"
                : $"破境失败（成功率 {成功率:P0}），保留了 {保留:P0} 的进度",
        };
    }

    /// <summary>
    /// 丹药查找器：参数（丹药 id、需要几个），返回背包里够不够。
    /// 留空时退到 <see cref="背包里有丹"/>。
    /// </summary>
    public System.Func<string, int, bool> 丹药查找器;

    /// <summary>丹药消耗器：参数（丹药 id、消耗几个）。留空时退到 <see cref="消耗丹"/> 的默认实现</summary>
    public System.Action<string, int> 丹药消耗器;

    /// <summary>默认的"背包里有没有" —— 走项目唯一的物品库 + 背包面板</summary>
    bool 背包里有丹(string 丹id, int 数量)
    {
        var 库 = QuestDatabase.取();
        if (库 == null) return false;
        var 定义 = 库.找物品(丹id);
        if (定义 == null) return false;
        var 板 = GameObject.FindObjectOfType<UIPanelData>();
        if (板 == null) return false;
        板.EnsureLists();
        return 板.物品数量(定义) >= 数量;
    }

    void 消耗丹(string 丹id, int 数量)
    {
        if (丹药消耗器 != null) { 丹药消耗器(丹id, 数量); return; }

        var 库 = QuestDatabase.取();
        if (库 == null) return;
        var 定义 = 库.找物品(丹id);
        if (定义 == null) return;
        var 板 = GameObject.FindObjectOfType<UIPanelData>();
        if (板 == null) return;
        板.EnsureLists();
        if (板.移除物品(定义, 数量)) 板.RaiseChanged();
    }

    // ============================================================ 杀怪 → 修炼次数

    /// <summary>
    /// **杀掉一只怪 → 掉落修炼次数。**
    /// 单只 = 基础 1 次 × 等级差系数（等级差 = 怪物等级 − 玩家等级）。
    /// 用「修炼次数累积」做小数累积，打 5 只低级怪（各 0.2）能攒够 1 次。
    /// </summary>
    public float 记录击杀(int 怪物等级)
    {
        int 差 = 怪物等级 - 等级;
        float 次 = 同级基础次数 * 取等级差系数(差);
        修炼次数累积 += 次;

        if (打印修为日志)
            Debug.Log("[修炼] 击杀 " + 怪物等级 + " 级怪（差 " + (差 >= 0 ? "+" : "") + 差
                + "）→ +" + 次.ToString("0.##") + " 次（现有 " + 修炼次数 + " 次）", this);

        击杀获得次数?.Invoke(this, 怪物等级, 次);
        修为变化?.Invoke(this);
        return 次;
    }

    /// <summary>等级差系数（按设计文档的分段）</summary>
    public float 取等级差系数(int 等级差)
    {
        int i;
        if (等级差 <= -5) i = 0;
        else if (等级差 <= -3) i = 1;
        else if (等级差 <= -1) i = 2;
        else if (等级差 == 0) i = 3;
        else if (等级差 <= 2) i = 4;
        else if (等级差 <= 4) i = 5;
        else i = 6;

        if (等级差系数 == null || 等级差系数.Length <= i) return 1f;
        return 等级差系数[i];
    }

    // ============================================================ 功法转换

    /// <summary>
    /// **转修功法。** 返回是否转成功。
    ///
    /// · **低难度 → 高难度**（K 变大）：总灵气先扣 **5% 损耗**，再用新 K 换算境界。
    /// · **高难度 → 低难度**（K 变小）：**境界不会提升**，灵气最多填满当前小境界
    ///   （也就是"差一步突破"），超出部分逸散。
    /// </summary>
    public bool 转修功法(GongFaDefinition 新功法)
    {
        if (新功法 == null || 面板数据 == null) return false;
        if (新功法 == 面板数据.当前功法) return false;

        var 旧功法 = 面板数据.当前功法;
        float 旧K = 旧功法 != null ? Mathf.Max(0.01f, 旧功法.难度等级 / 100f) : 1f;
        float 新K = Mathf.Max(0.01f, 新功法.难度等级 / 100f);

        int 旧等级 = 等级;
        long 旧灵气 = 总灵气;

        if (新K > 旧K)
        {
            // 低 → 高：扣 5% 损耗，境界按新 K 重算（总灵气减少，等效基准跟着降）
            总灵气 = (long)(总灵气 * 0.95);
            if (打印修为日志)
                Debug.Log("[修炼] 转修（低→高）：" + (旧功法 != null ? 旧功法.功法名称 : "无")
                    + " → " + 新功法.功法名称 + "｜K " + 旧K.ToString("0.0") + " → " + 新K.ToString("0.0")
                    + "｜总灵气 " + 旧灵气 + " → " + 总灵气 + "（扣 5% 损耗）", this);
        }
        else
        {
            // 高 → 低：境界不许涨。把等效基准灵气压到「当前等级的累计上限」
            //
            // 【境界封顶（用户 2026-09-23 定）】转到一个**上限更低**的功法时，允许转，
            // 但境界要被压到新功法的「最高可修炼境界」以内。
            // 例：境界 40 的时候转到一个只到 9 级的功法 → 境界压回 9 级，多余的灵气逸散。
            int 封顶等级 = Mathf.Min(旧等级, Mathf.Max(1, 新功法.最高可修炼境界));
            // ★ 取 累计(封顶等级 − 1)：因为「累计(n) = 升到 n+1 级所需」，
            //   要**停在** L 级就得用 累计(L−1)（此时 本级已积累 = 0、进度 0%）。
            //   以前写 累计(旧等级) 按这个语义推出来是 旧等级+1 —— 说是"不提升"其实涨了一级 ✗
            long 上限S = 取累计(封顶等级 - 1);
            总灵气 = Math.Min(总灵气, (long)(上限S * 新K));
            if (打印修为日志)
                Debug.Log("[修炼] 转修（高→低）：K " + 旧K.ToString("0.0") + " → " + 新K.ToString("0.0")
                    + "｜境界不提升（封顶 " + 封顶等级 + " 级"
                    + (封顶等级 < 旧等级 ? "，★被新功法的「最高可修炼境界 " + 新功法.最高可修炼境界 + "」压下来了" : "")
                    + "）｜总灵气 " + 旧灵气 + " → " + 总灵气, this);
        }

        面板数据.当前功法 = 新功法;
        面板数据.RaiseChanged();

        // 让 PlayerAbilityLoader 把普攻方法也跟着换掉
        var 装载 = GetComponent<PlayerAbilityLoader>();
        if (装载 != null) 装载.Refresh();

        上次等级 = 等级;      // 别把转换本身当成一次"升级"
        修为变化?.Invoke(this);
        return true;
    }

    /// <summary>转修之后境界会变成什么（给 UI 的「转修后境界预估」用），不真的改</summary>
    public string 预估转修后境界(GongFaDefinition 新功法)
    {
        if (新功法 == null) return "—";
        float 新K = Mathf.Max(0.01f, 新功法.难度等级 / 100f);
        var 旧功法 = 当前功法;
        float 旧K = 旧功法 != null ? Mathf.Max(0.01f, 旧功法.难度等级 / 100f) : 1f;

        long 预估总 = 总灵气;
        if (新K > 旧K) 预估总 = (long)(总灵气 * 0.95);                       // 低→高：扣 5%
        else
        {
            // 高→低：封顶。**和 转修功法() 用同一个封顶等级**，否则界面上的预估会和实际结果不一致。
            int 封顶等级 = Mathf.Min(等级, Mathf.Max(1, 新功法.最高可修炼境界));
            预估总 = Math.Min(总灵气, (long)(取累计(封顶等级 - 1) * 新K));   // ★ 和 转修功法() 同一套
        }

        long S = (long)(预估总 / 新K);
        var d = 查境界(S);
        return d != null ? d.境界名 : "—";
    }

    // ============================================================ 查表工具

    /// <summary>
    /// 按「等效基准灵气 S」查境界。
    ///
    /// 【语义必须和 <see cref="当前境界"/> 完全一致】
    /// 表里的 `累计灵气(n)` = **从 1 级升到 n+1 级所需的总灵气**，
    /// 所以「累计灵气 ≤ S 的最大 n」对应的**当前等级是 n+1**。
    ///
    /// 以前这里取的是 `n`（少加 1），而 <see cref="当前境界"/> 取的是 `n+1` ——
    /// **两个函数对同一个 S 会差一级**。查境界 只被「转修后境界预估」用，
    /// 于是界面上的预估总比实际结果低一级 ✗
    /// 现在两边都走这一份实现（`当前境界` 直接调本函数）。
    ///
    /// 【为什么不能依赖数组顺序】以前的写法是「遇到更大的等级就 `else break`」，
    /// 隐含要求 <see cref="境界表"/> 按等级升序。表一旦乱序（实测：按资产名字母序 →
    /// 元婴第10层排在第 0 位）就会**提前退出**、把「炼气第1层」的玩家判成「元婴第10层」。
    /// 现在全表扫一遍，与顺序无关（90 条，开销可忽略）。
    /// </summary>
    public RealmDefinition 查境界(long S)
    {
        if (境界表 == null || 境界表.Length == 0) return null;

        int 推定 = 1;                                   // 兜底：等级 1
        for (int i = 0; i < 境界表.Length; i++)
        {
            var d = 境界表[i];
            if (d == null) continue;
            if (d.累计灵气 <= S) 推定 = Mathf.Min(90, d.等级 + 1);
        }

        // ★ 破境封顶：**不给出比"已解锁最高等级"更高的等级**。
        //   灵气够了进度会到 100%，但等级卡住不动 —— 必须玩家点破境才放行。
        //   （见 已解锁最高等级 的注释）
        推定 = Mathf.Clamp(推定, 1, Mathf.Clamp(已解锁最高等级, 1, 90));

        return 取境界(推定) ?? 境界表[0];
    }

    /// <summary>按功法 id 找功法（读档用）</summary>
    public GongFaDefinition 取功法(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (面板数据 != null && 面板数据.当前功法 != null && 面板数据.当前功法.功法id == id) return 面板数据.当前功法;
        // Scene-local serialized tables predate newly added arts. The Resources catalog
        // is the shared source for save restoration in every gameplay scene and builds.
        var 库 = PanelDatabase.取();
        if (库 != null && 库.功法 != null)
            foreach (var g in 库.功法)
                if (g != null && g.功法id == id) return g;
        if (功法表 != null)
            foreach (var g in 功法表)
                if (g != null && g.功法id == id) return g;
        return null;
    }

    /// <summary>某个等级的「累计灵气」；等级 ≤ 0 返回 0</summary>
    public long 取累计(int 等级)
    {
        if (等级 <= 0) return 0;
        if (境界表 == null) return 0;
        for (int i = 0; i < 境界表.Length; i++)
            if (境界表[i] != null && 境界表[i].等级 == 等级) return 境界表[i].累计灵气;
        return 0;
    }

    /// <summary>直接设总灵气（读档 / 调试用）</summary>
    public void 设置总灵气(long 值)
    {
        总灵气 = Math.Max(0, 值);
        上次等级 = 等级;
        修为变化?.Invoke(this);
    }

    /// <summary>
    /// **加一笔总灵气**（丹药「聚气丹」走这里），返回实际加了多少。
    ///
    /// 【为什么要夹一层】总灵气被 `已解锁最高等级` 封了顶：攒满当前小境界就**停住**、
    /// 等玩家手动破境。所以这里要按"本级还能吃多少"截断，
    /// 否则一颗丹会把灵气灌到下一级去，等于绕过破境按钮白送等级
    /// （那个坑见 `查境界` 的注释）。
    /// </summary>
    public long 加总灵气(long 点数)
    {
        if (点数 <= 0) return 0;

        long 上限 = 取累计(已解锁最高等级);      // 本级封顶：到了就不能再涨
        long 余量 = System.Math.Max(0L, 上限 - 总灵气);
        long 实际 = System.Math.Min(点数, 余量);
        if (实际 <= 0) return 0;

        总灵气 += 实际;
        上次等级 = 等级;
        修为变化?.Invoke(this);
        return 实际;
    }

    /// <summary>
    /// **把灵气灌到"刚好可以破境"**（调试 / 测试用）。
    ///
    /// 注意是 `取累计(等级)` 而不是 `取累计(等级-1)` ——
    /// 灌到后者只是"本级起点"，灌多了又会因为跨过门槛而**升级**。
    /// </summary>
    public void 调试灌满本级()
    {
        var d = 当前境界;
        if (d == null) return;
        // 灌到门槛即可 —— 有了破境封顶，够了也不会晋级，进度**停在 100%** 等玩家点破境
        long 门槛 = System.Math.Max(1L, 取累计(d.等级));
        设置总灵气((long)(门槛 * 难度系数));
    }

    /// <summary>
    /// 本级进度（**含端点**，可以到 1.0 = 可以破境了）。
    ///
    /// 和 <see cref="进度"/> 的区别：`进度` 是半开区间上算的、上确界 1 取不到；
    /// 这个版本用"本级终点"做分母，所以 1.0 是**够得到**的 —— 破境判定用这个。
    /// </summary>
    public float 本级进度
    {
        get
        {
            var d = 当前境界;
            if (d == null) return 0f;
            if (d.突破类型 == BreakthroughKind.满级) return 1f;
            // 分母用"到门槛还差多少"：L 级要从 取累计(L-1) 攒到 取累计(L)
            long 需 = System.Math.Max(1L, 取累计(d.等级) - 本级起点累计);
            return Mathf.Clamp01(本级已积累 / (float)需);
        }
    }

    /// <summary>
    /// **确保数据就绪**（境界表 / 面板数据接上）。
    ///
    /// 为什么要有这个公开入口：`取数据()` / `确保境界表()` 原来是私有的，
    /// 只在 `Start`/`Update` 里跑。于是**从外部（调试脚本、别的系统）第一次调
    /// `修炼一次()` 时，`当前境界` 还是 null**，会静默什么都不做 —— 很难查。
    /// 公开一个幂等的"确保就绪"，调用方先调它就不会踩这个坑。
    /// </summary>
    public void 确保就绪()
    {
        if (面板数据 == null) 面板数据 = GetComponent<UIPanelData>();
        if (面板数据 == null) 面板数据 = FindObjectOfType<UIPanelData>();
        确保境界表();
    }

    /// <summary>直接设修炼次数（读档 / 调试用）</summary>
    public void 设置修炼次数(float 值)
    {
        修炼次数累积 = Math.Max(0f, 值);
        修为变化?.Invoke(this);
    }

    // ---- ASCII 别名 ----
    public float GainKillCharge(int monsterLevel) => 记录击杀(monsterLevel);
    public float CultivateOnce() => 修炼一次();
    public bool SwitchGongFa(GongFaDefinition next) => 转修功法(next);
    public string PreviewRealmAfterSwitch(GongFaDefinition next) => 预估转修后境界(next);
}
