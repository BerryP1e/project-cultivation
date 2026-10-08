using UnityEngine;

/// <summary>
/// **追踪弹档**（`ActiveSkillKind.追踪弹`）的执行体 —— 现在服务两个神通：/// **瞬雷天闪**（球 lightning-sphere）与 **冰暴术**（球 frost-crystal、命中 frost-frozen-tomb 贴地）。
///
/// 策划口径（用户 2026-09-28）：
/// ```
/// 用太虚炼气诀的那个普攻动作（普攻_远程_01）
/// 在动画的 40% 节点，对【锁定的敌人】放出一颗闪电球（特效 lightning-sphere）
/// 闪电球用比较快的速度飞过去，**带追踪**
/// 命中后放命中特效（lightning-explode）
/// 对锁定单位造成【特殊 + 主动神通】伤害
/// 冷却 10s、每次冷却完积攒一次、最多 3 次，消耗大量灵力
/// ```
/// 冷却 / 充能 / 灵力 / 倍率 都由 <see cref="ActiveDivineAbility"/> 那边管
/// （`ActiveSkillCaster` 处理），这里只管"动作 → 出弹 → 追踪 → 命中结算"。
///
/// 飞行器直接复用 <see cref="NpcProjectile"/> 的**追踪模式**（已经踩过坑调好：
/// 三维方向追踪、按距离补发沿路粒子、目标死了也不会卡住）。
/// </summary>
public class HomingBoltSkillRunner : MonoBehaviour
{
    ActiveDivineAbility 神通;
    PlayerCombatStats 战斗属性;
    NpcInstance 锁定;
    PlayerAnimationController 动画;
    Transform 玩家;
    LayerMask 敌人层;

    AnimationClip 动作;
    float 起算时刻;
    bool 已发射;
    bool 已放飞;
    float 放飞时刻;
    bool 已结算;
    NpcProjectile 弹;
    GameObject 球;
    Transform 挂点;              // 缓存住手部骨骼（别每帧遍历整棵角色树）

    /// <summary>命中的敌人个数（0 或 1；调试用）</summary>
    public int 命中数 { get; private set; }
    /// <summary>本次造成的伤害（调试用）</summary>
    public float 累计伤害 { get; private set; }
    /// <summary>是不是已经放出去了（还在手上长的时候是 false；调试用）</summary>
    public bool 已放弹 => 已放飞;

    [Header("动作（对齐普攻）")]
    [Tooltip("播哪个动作。`普攻_远程_01` 就是太虚炼气诀的普攻动作")]
    public string 动作名 = "普攻_远程_01";

    [Tooltip("★ 动作播到几成时生成雷球（0.4 = 40% 节点，和玄霄雷决的普攻同一个口径）")]
    [Range(0.05f, 0.95f)] public float 出手进度 = 0.4f;

    [Tooltip("动作播放速度倍率。1 = 原速")]
    public float 动作速度 = 1f;

    [Tooltip("★ 到节点后**把动作定住**这么久（秒），让雷球在手上把粒子长出来，再继续播、再飞出去。\n" +
             "用户 2026-09-28 指定 1.5 秒。填 0 = 不停，立刻飞（球会偏淡，因为特效来不及长）")]
    public float 长球停顿时长 = 1.5f;

    [Header("闪电球")]
    [Tooltip("闪电球特效。路径相对 Assets/resources、不带扩展名")]
    public string 球特效路径 =
        "特效/战斗法术/Combat Magic VFX Vol.1/resources/lightning-fx/lightning-sphere";

    [Tooltip("闪电球的生成旋转（欧拉角）。躺平/倒立就改这里")]
    public Vector3 球旋转欧拉 = Vector3.zero;

    [Tooltip("闪电球缩放（1 = 原大小）")]
    public float 球缩放 = 1f;

    [Tooltip("★ 出弹挂点：球生成在角色的哪根骨骼上（就是「手上」）。\n" +
             "找不到这根骨骼时退回「角色根 + 出弹抬高」")]
    public string 出弹挂点名 = "R_Hand";

    [Tooltip("出弹点相对角色的抬高（米）。**只在找不到 出弹挂点 时**当兜底用")]
    public float 出弹抬高 = 1.2f;

    [Tooltip("飞行速度（米/秒）。策划要求「比较快」")]
    public float 飞行速度 = 22f;

    [Tooltip("追踪转向速率（度/秒）。越大拐弯越灵；太小会绕圈追不上")]
    public float 转向速率 = 720f;

    [Tooltip("目标离球多近算命中（米）")]
    public float 命中半径 = 1.0f;

    [Tooltip("追踪最长持续多久（秒）就放弃")]
    public float 最长追踪时间 = 6f;

    [Header("命中特效")]
    [Tooltip("命中特效。路径相对 Assets/resources、不带扩展名")]
    public string 命中特效路径 =
        "特效/战斗法术/Combat Magic VFX Vol.1/resources/lightning-fx/lightning-explode";

    [Tooltip("命中特效的生成旋转（欧拉角）")]
    public Vector3 命中特效旋转欧拉 = Vector3.zero;

    [Tooltip("命中特效缩放（1 = 原大小）")]
    public float 命中特效缩放 = 1f;

    [Tooltip("命中特效抬高（米）：放在「锁定目标 + 抬高」处。1 左右大约在胸口")]
    public float 命中特效抬高 = 1f;

    [Tooltip("勾上 = 把命中特效的几何中心对齐到目标身上（推荐）")]
    public bool 命中特效对齐 = true;

    [Tooltip("★ 勾上 = 命中特效放在**敌人脚下**（y = 敌人根部）而不是胸口高度。\n" +
             "冰暴术的 frost-frozen-tomb 要贴地；瞬雷天闪的 lightning-explode 保持不勾（打胸口）")]
    public bool 命中特效贴地 = false;

    [Tooltip("命中特效存活（秒）。勾了下面的「按自然」时，它只是**下限**")]
    public float 命中特效存活 = 2f;

    [Tooltip("★ 勾上（默认）：命中特效存活**至少**取 prefab 的自然总时长（×1.05）。\n" +
             "为什么：`frost-frozen-tomb` 自然 **20.7 秒**（冰冢慢慢长起来那类），\n" +
             "写死 2 秒会把它硬切 →「忽然消失」。和冰刺/寒墟同一套口径。")]
    public bool 命中特效存活按自然 = true;

    [Header("兜底")]
    [Tooltip("执行体自己的存活上限（秒）。超过就强制收尾，防止卡住")]
    public float 最长存活 = 10f;

    [Tooltip("打印日志")]
    public bool 打印日志 = true;

    Vector3? 手动点;
    public float 环境破坏半径=2.5f;
    public void 初始化(ActiveDivineAbility 神通, PlayerCombatStats 战斗属性, NpcInstance 锁定,
                        PlayerAnimationController 动画, Transform 玩家, LayerMask 敌人层,Vector3? 落点=null)
    {
        this.神通 = 神通;
        this.战斗属性 = 战斗属性;
        this.锁定 = 锁定;
        this.动画 = 动画;
        this.玩家 = 玩家;
        this.敌人层 = 敌人层;
        手动点=落点;
    }

    /// <summary>日志与物件名前缀：用**神通自己的名字**（这个 runner 现在也服务「冰暴术」）</summary>
    string 名称 => 神通 != null && !string.IsNullOrEmpty(神通.神通名称) ? 神通.神通名称 : "追踪弹";

    void Start()
    {
        if (神通 == null || 玩家 == null || (锁定 == null && !手动点.HasValue)) { Destroy(gameObject); return; }

        起算时刻 = Time.time;

        if (动画 == null) 动画 = 玩家.GetComponent<PlayerAnimationController>();
        if (动画 == null) 动画 = 玩家.GetComponentInChildren<PlayerAnimationController>();

        // 动作片段和普攻同一个目录、同一个加载方式
        if (!string.IsNullOrEmpty(动作名))
            动作 = Resources.Load<AnimationClip>("技能动作/" + 动作名);

        // 播动作。播不起来（没动画组件 / 找不到片段）就**立刻出弹**，
        // 免得整个神通因为动画问题变成哑炮（和 BasicThunder01 一个口径）。
        bool 播上了 = false;
        if (动画 != null && 动作 != null) 播上了 = 动画.播动作(动作, Mathf.Max(0.05f, 动作速度));
        if (!播上了)
        {
            if (动画 == null)
                Debug.LogWarning("[" + 名称 + "] 找不到 PlayerAnimationController，直接出弹", this);
            else if (动作 == null)
                Debug.LogWarning("[" + 名称 + "] 找不到动作 Assets/resources/技能动作/" + 动作名 + ".anim，直接出弹", this);
            生成球();          // 没有动作可定时，照样走"球先在手上长一下再飞"的流程
            return;
        }

        if (打印日志)
            Debug.Log("[" + 名称 + "] 播动作「" + 动作名 + "」（" + 动作.length.ToString("0.##")
                + "s），播到 " + (出手进度 * 100f).ToString("0") + "% 出弹", this);
    }

    void Update()
    {
        if (神通 == null) { 收尾(); return; }

        // ---- 1) 等动作播到出手进度 → **定住动作** + 在手上生成雷球 ----
        if (!已发射)
        {
            float 进度 = 动画 != null ? 动画.动作进度 : 1f;
            bool 还在播 = 动画 != null && 动画.动作播放中;
            // 进度到了，或者动作已经播完了（防呆，别因为进度的边界问题不出弹）
            if (进度 >= 出手进度 || !还在播) 生成球();
        }

        // ---- 2) 球在手上等的这段时间，**每帧贴着手**（跟着角色走）----
        //   定住的是**动画**，不是人 —— 角色照样能走动；
        //   球要是只在生成那一刻摆一次，人一走球就落在原地了（用户 2026-09-28 发现）。
        if (已发射 && !已放飞 && 球 != null) 球.transform.position = 出弹点();

        // ---- 3) 停够了 → 放开动作 + 把球放出去 ----
        if (已发射 && !已放飞 && Time.time >= 放飞时刻) 放飞();

        // ---- 4) 弹没了但没结算（被打断 / 目标死了 / 超时消散）→ 收尾 ----
        if (已放飞 && !已结算 && 弹 == null) { 收尾(); return; }

        // ---- 5) 兜底 ----
        if (Time.time - 起算时刻 >= Mathf.Max(0.5f, 最长存活)) 收尾();
    }

    /// <summary>收尾时**务必把动作放开**，否则玩家的动画会永远停在那一帧</summary>
    void OnDestroy()
    {
        if (动画 != null && 动画.动作已定住) 动画.定住动作(false);
    }

    void 收尾()
    {
        if (动画 != null && 动画.动作已定住) 动画.定住动作(false);
        if (打印日志 && 已发射)
            Debug.Log("[" + 名称 + "] 结束：命中 " + 命中数 + " 个敌人，合计 " + 累计伤害.ToString("0.##"), this);
        Destroy(gameObject);
    }

    // ============================================================ 出弹

    /// <summary>
    /// ① 到节点：**把动作定住**，在主角手上生成雷球 —— 但**先不飞**。
    ///
    /// 停这一会儿（<see cref="长球停顿时长"/>）就是为了让球上的粒子长出来：
    /// `lightning-sphere` 要零点几秒才成形，直接飞的话整个飞行过程只有 0.2 秒左右，
    /// 球还是一片稀的（用户 2026-09-28 要的解决办法就是"停 1.5 秒让它长出来"）。
    /// </summary>
    void 生成球()
    {
        if (已发射) return;
        已发射 = true;

        Vector3 起点 = 出弹点();

        var prefab = string.IsNullOrEmpty(球特效路径) ? null : Resources.Load<GameObject>(球特效路径);
        if (prefab != null) 球 = Instantiate(prefab, 起点, Quaternion.Euler(球旋转欧拉));
        else
        {
            球 = new GameObject("闪电球(无特效)");
            Debug.LogWarning("[" + 名称 + "] 找不到闪电球特效：" + 球特效路径
                + "（路径要相对 Assets/resources、不带扩展名）→ 只有判定、没有球", this);
        }
        球.name = 名称 + "_球";
        球.transform.position = 起点;
        if (!Mathf.Approximately(球缩放, 1f)) 球.transform.localScale *= 球缩放;

        // 先挂上飞行器但**关掉**：Awake 里的"起播粒子"要现在跑（有些包的粒子得外部启动），
        // 但 Update 绝不能跑 —— 否则它以为自己要飞向世界原点，一帧就"到达"了。
        弹 = 球.GetComponent<NpcProjectile>();
        if (弹 == null) 弹 =球.AddComponent<NpcProjectile>();
        弹.到达时 += 命中;
        弹.enabled = false;

        // 定住动作，等球长出来
        if (动画 != null) 动画.定住动作(true);
        放飞时刻 = Time.time + Mathf.Max(0f, 长球停顿时长);

        if (打印日志)
            Debug.Log("[" + 名称 + "] 到节点：动作进度 " + (动画 != null ? 动画.动作进度.ToString("0.000") : "无")
                + "，球已在手上 " + 起点.ToString("F2")
                + "，定住动作 " + 长球停顿时长.ToString("0.##") + "s 等它长出来", this);
    }

    /// <summary>② 停够了：放开动作 + 把球放出去追踪目标</summary>
    void 放飞()
    {
        已放飞 = true;
        if (动画 != null) 动画.定住动作(false);

        if (球 == null || (锁定 == null && !手动点.HasValue)) { 收尾(); return; }

        Vector3 目标点 = 手动点 ?? (锁定.transform.position + Vector3.up * 1.1f);
        Vector3 向 = 目标点 - 球.transform.position;
        if (向.sqrMagnitude > 0.0001f) 球.transform.rotation = Quaternion.LookRotation(向.normalized, Vector3.up);

        弹.命中半径 = Mathf.Max(0.2f, 命中半径);
        弹.最长追踪时间 = Mathf.Max(0.5f, 最长追踪时间);
        弹.追踪终止距离 = 神通 != null && 神通.范围 > 0f ? 神通.范围 : 0f;
        弹.最大飞行距离 = 0f;                       // 交给 追踪终止距离 / 最长追踪时间 收尾
        弹.enabled = true;
        弹.检测体素=true;
        if(手动点.HasValue)弹.设置飞行(目标点,Mathf.Max(1,飞行速度));
        else 弹.设置追踪飞行(锁定.transform, Mathf.Max(1f, 飞行速度), Mathf.Max(30f, 转向速率));

        if (打印日志)
            Debug.Log("[" + 名称 + "] 放飞：从 " + 球.transform.position.ToString("F2") + " → 目标「" + (锁定?锁定.DisplayName:"鼠标落点")
                + "」 " + 目标点.ToString("F2") + "（距离 " + Vector3.Distance(球.transform.position, 目标点).ToString("0.##")
                + "m｜速度 " + 飞行速度 + "m/s｜转向 " + 转向速率 + "°/s）", this);
    }

    /// <summary>
    /// 出弹点 = 角色手上。按 <see cref="出弹挂点名"/> 在角色子树里找那根骨骼（找到就缓存）；
    /// 找不到（换了模型 / 改了骨骼名）就退回「角色根 + <see cref="出弹抬高"/>」。
    /// </summary>
    Vector3 出弹点()
    {
        if (挂点 == null && 玩家 != null && !string.IsNullOrEmpty(出弹挂点名))
        {
            foreach (var t in 玩家.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == 出弹挂点名) { 挂点 = t; break; }
        }
        if (挂点 != null) return 挂点.position;
        return (玩家 != null ? 玩家.position : transform.position) + Vector3.up * 出弹抬高;
    }

    // ============================================================ 命中

    void 命中()
    {
        if (已结算) return;
        已结算 = true;
        bool 环境=手动点.HasValue || 弹!=null && 弹.击中体素;
        Vector3 环境点=环境 && 弹!=null?弹.命中点:(锁定?锁定.transform.position:手动点.GetValueOrDefault());
        VoxelCombatDamage.Sphere(环境点,环境破坏半径*Mathf.Max(.1f,命中特效缩放));

        // 1) 命中特效：放在锁定目标身上（**贴地**时放在敌人脚下 —— 冰暴术的 frost-frozen-tomb）
        if (!string.IsNullOrEmpty(命中特效路径) && (锁定 != null || 环境))
        {
            float 存活 = Mathf.Max(0.1f, 命中特效存活);
            if (命中特效存活按自然)
            {
                var 预制 = Resources.Load<GameObject>(命中特效路径);
                存活 = Mathf.Max(存活, 特效摆放.量特效总时长(预制, 命中特效存活) * 1.05f);
            }

            Vector3 爆点 = 环境?环境点:命中特效贴地
                ? 锁定.transform.position
                : 锁定.transform.position + Vector3.up * 命中特效抬高;

            特效摆放.生成(命中特效路径, 爆点, 命中特效旋转欧拉,
                          命中特效缩放, 对齐到锚点: 命中特效贴地 ? false : 命中特效对齐,
                          存活秒: 存活, 名: 名称 + "_命中");

            if (打印日志)
                Debug.Log("[" + 名称 + "] 命中特效「" + 命中特效路径 + "」放在 "
                    + (命中特效贴地 ? "脚下" : "胸口") + " " + 爆点.ToString("F2")
                    + "，存活 " + 存活.ToString("0.##") + "s", this);
        }

        // 2) 伤害：只打锁定的那一个（【特殊 + 主动神通】）
        if (战斗属性 != null && 锁定 != null && !锁定.IsDead && !环境)
        {
            var 规则 = new AttackSpec(神通.伤害属性, AttackKind.主动神通, false, 神通.伤害倍率);
            var 目标 = new NpcTarget(锁定);
            var 结果 = 目标.受到攻击(战斗属性, 规则, this);
            命中数++;
            累计伤害 += 结果.伤害;

            if (打印日志)
                Debug.Log("[" + 名称 + "] 命中「" + 目标.名字 + "」 " + 结果
                    + "（倍率 " + 神通.伤害倍率.ToString("0.##") + "｜" + 神通.伤害属性 + "）", 锁定);
        }

        if(环境 && 战斗属性!=null)
        {
            var seen=new System.Collections.Generic.HashSet<NpcInstance>();
            foreach(var col in Physics.OverlapSphere(环境点,环境破坏半径,敌人层,QueryTriggerInteraction.Ignore))
            {
                var npc=col.GetComponentInParent<NpcInstance>();
                if(npc && !npc.IsDead && npc.是敌对目标 && seen.Add(npc))npc.ReceiveAttack(战斗属性,new AttackSpec(神通.伤害属性,AttackKind.主动神通,false,神通.伤害倍率));
            }
        }
        收尾();
    }

    // ---- ASCII 别名 ----
    public int HitCount => 命中数;
    public float TotalDamage => 累计伤害;
    public bool HasFired => 已发射;

    // ============================================================
    // ⚠️ 这里**不要**用 ParticleSystem.Simulate() 去"预热"特效！
    //
    // 2026-09-28 我加过一版预热（想让球一生成就是"长好的样子"），结果
    // **特效被冻成一帧静止画面** —— `Simulate()` 会把粒子系统 `isPaused = true`，
    // 之后再没解开，于是球和命中爆炸都成了静态图片（用户当场发现）。
    //
    // 而且当初"球不出粒子"的判断本身就是错的：我把测试物件丢在 (400,20,400)，
    // 那里**在相机视锥外**，而这些特效的 `cullingMode = PauseAndCatchup`
    // —— 被剔除的粒子系统会**停止模拟**。搬进画面里就正常了（实测 2→9→13→18 颗）。
    // 详见 docs/ai/踩坑总库.md B35/B36。
    // ============================================================
}
