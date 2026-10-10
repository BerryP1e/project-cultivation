using UnityEngine;

/// <summary>
/// 玩家的「当前值」容器：气血与灵气。
/// 上限取自 PlayerCombatStats 汇总出来的「气血 / 灵力」，回复取「气血回复 / 灵力回复」。
///
/// 说明：lore 里战斗属性叫「灵力」，而策划口述飞行消耗时说「灵气」，
/// 这里统一按同一个资源处理，运行时对外叫【灵气】，上限就是「灵力」那条属性。
/// </summary>
public class PlayerVitals : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("属性来源。留空则自动在本体找 PlayerCombatStats")]
    public PlayerCombatStats 属性;

    [Header("当前值")]
    public float 当前气血 = 1f;
    public float 当前灵气 = 1f;

    [Header("回复")]
    [Tooltip("是否按属性里的回复速度自动回复")]
    public bool 自动回复 = true;

    [Tooltip("回复速度的整体倍率，方便测试时调快")]
    public float 回复倍率 = 1f;

    /// <summary>气血上限</summary>
    public float 气血上限 => 属性 != null ? Mathf.Max(1f, 属性.当前属性[AttributeType.MaxHealth]) : 1f;

    /// <summary>灵气上限（= 战斗属性里的「灵力」）</summary>
    public float 灵气上限 => 属性 != null ? Mathf.Max(1f, 属性.当前属性[AttributeType.MaxSpirit]) : 1f;

    /// <summary>灵气比例 0~1</summary>
    public float 灵气比例 => Mathf.Clamp01(当前灵气 / 灵气上限);

    /// <summary>是否还有灵气</summary>
    public bool 有灵气 => 当前灵气 > 0f;

    void Awake()
    {
        if (属性 == null) 属性 = GetComponent<PlayerCombatStats>();
        当前气血 = 气血上限;
        当前灵气 = 灵气上限;
    }

    void Update()
    {
        // ★ 过场保护必须**每帧都跑**，所以放在最前面。
        //
        // 【踩过的坑】本方法原来第一行是 `if (!自动回复) return;` ——
        // 如果把过场保护放在它后面，一旦有人把「自动回复」关掉，
        // 宽限倒计时就永远不走了：过场保护会**永久卡在 true**（变成无敌），
        // 或者永远进不了保护。这种"被一个不相干的 early return 吃掉"的 bug
        // 特别难查，所以宁可把它放在最上面。
        Update过场保护();

        if (!自动回复) return;
        float dt = Time.deltaTime;

        float 气血回复 = 属性 != null ? 属性.当前属性[AttributeType.HealthRegen] : 0f;
        float 灵力回复 = 属性 != null ? 属性.当前属性[AttributeType.SpiritRegen] : 0f;

        if (气血回复 > 0f) 当前气血 = Mathf.Min(气血上限, 当前气血 + 气血回复 * 回复倍率 * dt);
        if (灵力回复 > 0f) 当前灵气 = Mathf.Min(灵气上限, 当前灵气 + 灵力回复 * 回复倍率 * dt);
    }

    /// <summary>灵气是否够扣</summary>
    public bool 够灵气(float amount) => amount <= 0f || 当前灵气 >= amount;

    /// <summary>扣灵气。返回是否成功（不够就不扣）</summary>
    public bool 扣灵气(float amount)
    {
        if (amount <= 0f) return true;
        if (当前灵气 < amount) return false;
        当前灵气 -= amount;
        return true;
    }

    /// <summary>扣灵气，扣到 0 为止（用于「持续性消耗」，不要求一次扣满）</summary>
    public float 扣灵气直到零(float amount)
    {
        if (amount <= 0f) return 0f;
        float actual = Mathf.Min(amount, 当前灵气);
        当前灵气 -= actual;
        return actual;
    }

    /// <summary>
    /// 回满气血和灵气。
    ///
    /// 【重要】**同时清掉死亡标记。**
    ///
    /// 以前这里不清 `已死亡`，而全项目没有任何地方会自动清它（只有手动 <see cref="复活"/>）——
    /// 于是玩家一旦被打到 0 血，`已死亡` 就**永久为真**。
    /// 而所有敌对 NPC 的决策第一行就是「玩家已死 → 待机」，
    /// 结果就是：**打到一半全场 NPC 突然集体不动，连新召唤出来的也不动**。
    /// （排查了很久，一开始还以为是 NPC 状态机挂了 —— 其实是玩家这边。）
    ///
    /// "回满"的语义本来就是"恢复到满状态"，那就应该是活的，所以在这里清标记。
    /// </summary>
    public void 回满()
    {
        已死亡 = false;
        当前气血 = 气血上限;
        当前灵气 = 灵气上限;
    }

    /// <summary>
    /// **回一笔气血**（丹药「愈伤丹」走这里），返回实际回了多少。
    /// 返回 0 = 已经是满血（调用方据此判断"这丹吃了白吃"）。
    /// </summary>
    public float 回复气血(float 点数)
    {
        if (点数 <= 0f || 已死亡) return 0f;
        float 前 = 当前气血;
        当前气血 = Mathf.Min(气血上限, 当前气血 + 点数);
        // ⚠️ 故意**不**触发 `气血变化`：那个事件的口径是"实际扣掉的气血 + 是否致命一击"，
        //    是给受伤表现用的；治疗发它会让订阅方把它当成一次受伤。
        return 当前气血 - 前;
    }

    /// <summary>**回一笔灵力**（丹药「清障丹」走这里），返回实际回了多少</summary>
    public float 回复灵气(float 点数)
    {
        if (点数 <= 0f || 已死亡) return 0f;
        float 前 = 当前灵气;
        当前灵气 = Mathf.Min(灵气上限, 当前灵气 + 点数);
        return 当前灵气 - 前;
    }

    // ------------------------------------------------------------ 受伤

    /// <summary>气血变化（参数：本组件、实际扣掉的气血、是否是致命一击）</summary>
    public event System.Action<PlayerVitals, float, bool> 气血变化;

    /// <summary>死亡</summary>
    public event System.Action<PlayerVitals> 死亡;

    /// <summary>是否已经倒下</summary>
    public bool 已死亡 { get; private set; }

    /// <summary>
    /// **无敌**（= 调试开关 或 重生保护 或 切场景过场 任一开着）。
    ///
    /// 【为什么是「或」而不是一个可写字段】有三个来源要叠加：
    /// · <see cref="调试无敌"/> —— F1 调试面板的手动开关（调试期不扣血）
    /// · <see cref="保护中"/>   —— <see cref="PlayerDeathSequence"/> 的重生保护
    /// · **切场景过场 / 演出期间** —— 见 <see cref="过场保护"/>（这里自带，不靠外部写）
    ///
    /// 以前 `无敌` 是个 `{ get; set; }` 自动属性，只有重生保护在写它 ——
    /// 而**重生保护到期会把它设成 false**。如果调试面板也去写同一个字段，
    /// 就会互相覆盖 ✗
    /// 现在拆成几个方向明确的来源，读的时候取并集，谁都不覆盖谁。
    /// </summary>
    public bool 无敌 => 调试无敌 || 保护中 || 过场保护;

    /// <summary>
    /// **调试用无敌**（F1 面板勾上）。勾上后不扣血，面板关掉也继续生效，
    /// 取消勾选 / 重进 Play 才恢复。
    ///
    /// 【和「无敌」的区别】`无敌` 是只读的最终判定；这个是只属于调试面板的开关。
    /// 写在 <see cref="PlayerVitals"/> 而不是面板里，是为了让
    /// 「重开一局就归零」由这里统一负责（面板是场景序列化的，字段会残留）。
    /// </summary>
    public bool 调试无敌;

    /// <summary>重生保护开着吗（由 <see cref="PlayerDeathSequence"/> 维护）</summary>
    public bool 保护中 { get; set; }

    [Header("切场景过场保护（防「黑幕一结束就死了」）")]
    [Tooltip("演出（黑幕过场 / 强制演出 / 起名…）结束后，额外再免疫多少秒。\n" +
             "留给玩家「看清场面 + 拉开距离」的时间；0 = 演出一结束立刻可被打。\n\n" +
             "【为什么需要】镇妖塔一加载完就开始刷怪，而黑幕还要 3~6 秒才收 ——\n" +
             "那段时间玩家不能走不能打，会被围着打死。")]
    [Min(0f)]
    public float 过场保护宽限 = 2.5f;

    /// <summary>
    /// **切场景过场保护开着吗** —— 演出中，或演出刚结束的宽限期内。
    ///
    /// ★ **这个属性自己会把状态推到最新（自带兜底），不依赖 Update 跑过。**
    ///
    /// 【踩过的坑·很关键】第一版把它做成「`Update` 里每帧写一个字段」，
    /// 结果：起幕那一帧 `演出中` 刚变 true，而 `受到伤害` 读到的还是上一帧的旧值
    /// ⇒ **保护晚一帧生效**。实测（模拟演出中打 999999）玩家照样被打死 ✗
    ///
    /// 所以改成：**每次读它都顺手推进一次状态**。
    /// `Update` 里仍然每帧调一次（负责宽限的连续倒计时），
    /// 但即使 `Update` 没跑（组件刚启用 / 被禁用 / 那一帧还没到），
    /// 读的时候也算得对。
    ///
    /// > 通用教训：**"保护类"状态不要只在一处攒，读它的地方要能自己算出来。**
    /// > 伤害判定是逐帧时序问题的高发区 —— 差一帧就是"死了"和"没死"的区别。
    /// </summary>
    public bool 过场保护
    {
        get
        {
            bool 在演出中 = 黑幕字幕.演出中;

            if (在演出中)
            {
                过场宽限剩余 = 过场保护宽限;          // 演出期间一直把宽限续满
                上次在演出中 = true;
                return true;
            }

            if (上次在演出中)
            {
                // ★ 演出刚结束（可能是这一帧刚结束）—— 宽限从这一刻起算
                过场宽限剩余 = 过场保护宽限;
                上次在演出中 = false;
                return 过场宽限剩余 > 0f;
            }

            return 过场宽限剩余 > 0f;
        }
    }

    /// <summary>过场保护剩余的宽限时间（<=0 = 只剩"演出中"这一条）</summary>
    [SerializeField] float 过场宽限剩余;
    bool 上次在演出中;

    /// <summary>
    /// 每帧推进宽限倒计时。
    ///
    /// 【为什么不能只靠 <see cref="过场保护"/> 的 getter】
    /// getter 是"懒计算"，只在有人读的时候才推进；没人读就不动。
    /// 所以宽限的**连续倒计时**仍然需要一个每帧的驱动 —— 就是这里。
    /// 两者不冲突：getter 负责"读到的那一刻是否正确"，这里负责"时间真的在走"。
    /// </summary>
    void Update过场保护()
    {
        bool 在演出中 = 黑幕字幕.演出中;

        if (在演出中)
        {
            // 演出期间不发散（getter 里已经保证返回 true）
            过场宽限剩余 = 过场保护宽限;
        }
        else if (上次在演出中)
        {
            // 让 getter 去处理"刚结束"那一帧的交接，这里不抢
            // （读一次就会把 上次在演出中 置回 false 并把宽限续满）
            var _ = 过场保护;
        }
        else if (过场宽限剩余 > 0f)
        {
            过场宽限剩余 = Mathf.Max(0f, 过场宽限剩余 - Time.deltaTime);
        }
    }

    /// <summary>
    /// 受到伤害（NPC 的 AI 打玩家走这里）。
    /// 返回实际扣掉的气血 —— 伤害公式由调用方用 <see cref="CombatCalculator"/> 算完再传进来，
    /// 这里只管扣当前值。
    /// </summary>
    public float 受到伤害(float 伤害, DamageNature 属性类型 = DamageNature.物理)
        => 应用战斗伤害(伤害, 属性类型).实际伤害;

    public event System.Action<PlayerVitals, AttackResult> 结算完成;
    public void 通知战斗结算(AttackResult result) => 结算完成?.Invoke(this, result);

    public CombatDamageApplication 应用战斗伤害(float 伤害, DamageNature 属性类型)
    {
        if (已死亡) return new CombatDamageApplication(0, 0, 0, false, CombatHitState.InvalidTarget);
        if (无敌) return new CombatDamageApplication(0, 0, 0, false, CombatHitState.Immune);
        if (伤害 <= 0f) return new CombatDamageApplication(0, 0, 0, false, CombatHitState.NoDamage);

        float 原伤害 = 伤害;
        var 护罩 = GetComponent<PassiveShieldAbilities>();
        if (护罩 != null && 护罩.isActiveAndEnabled) 伤害 = 护罩.过滤伤害(伤害, 属性类型);
        float 吸收 = Mathf.Max(0, 原伤害 - 伤害);
        if (伤害 <= 0f) return new CombatDamageApplication(0, 吸收, 0, false, CombatHitState.ShieldBlocked);

        float 实际 = Mathf.Min(伤害, 当前气血);
        当前气血 -= 实际;

        bool 致命 = 当前气血 <= 0f;
        if (致命)
        {
            当前气血 = 0f;
            已死亡 = true;
        }

        气血变化?.Invoke(this, 实际, 致命);
        if (致命) 死亡?.Invoke(this);
        return new CombatDamageApplication(实际, 吸收, Mathf.Max(0, 伤害 - 实际), 致命, CombatHitState.Hit);
    }

    /// <summary>满血复活并清除死亡标记</summary>
    public void 复活()
    {
        已死亡 = false;
        当前气血 = 气血上限;
        当前灵气 = 灵气上限;
    }

    // ---- ASCII 别名 ----
    public float CurrentHealth { get => 当前气血; set => 当前气血 = value; }
    public float CurrentSpirit { get => 当前灵气; set => 当前灵气 = value; }
    public float MaxSpirit => 灵气上限;
    public float SpiritPercent => 灵气比例;
    public bool HasSpirit => 有灵气;
    public bool SpendSpirit(float amount) => 扣灵气(amount);
    public void Refill() => 回满();
    public float TakeDamage(float amount) => 受到伤害(amount);
    public bool IsDead => 已死亡;
}
