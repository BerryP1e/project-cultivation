using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 功法「玄霄雷决」提供的普攻方法（功法表 `普攻方法id = basic_thunder_01`）。
///
/// ## 和其它普攻方法的区别
/// 按策划说明：**在动画的 40% 节点**对**已锁定的敌人**召一道雷下劈，
/// 命中后**有概率**沿敌群蔓延成闪电链。
///
/// ★ **必须先用右键锁定一个目标**（和 <see cref="BasicRemoteAttack01"/> 同一套规矩）：
///   没锁定就不出手、也不进冷却。闪电链的"找周边的人"是**锁定目标命中之后**才发生的蔓延，
///   不是一开始就随便挑一个最近的打。
///
/// ## 两个伤害
///   · 下劈的雷：`特殊 + 普通攻击`（吃会心 / 普攻加成）
///   · 闪电链：同上，倍率可以单独配（一般比下劈低）
///
/// ## 特效缩放
/// 雷按**敌人大小**等比缩放（大树/巨兽身上雷更大）。做法是改特效根的 localScale ——
/// 实测 UnionAssetes 的 Electro_* prefab 整棵树的粒子都是 `simulationSpace = Local`，
/// 所以缩放根节点是等比放大，不会像 World 空间那样只把粒子"摊开"。
///
/// ## 闪电链的"拉伸"
/// 同样是 Local 空间的好处：把特效的**长度轴**拉到 ≈ 两个敌人的水平距离，
/// 雷就被拉成一道横跨两者的链。长度轴**运行时按 prefab 的 shape 自动探测**（见 生成闪电链）。
/// </summary>
public class BasicThunder01 : MonoBehaviour
{
    [Tooltip("本普攻方法在功法表「普攻方法id」里的标识。境界页靠它反查提供这个普攻的功法")]
    public string 方法id = "basic_thunder_01";

    // ============================================================ 动作

    [Header("出手动作")]
    [Tooltip("普攻动作片段（Assets/resources/技能动作/ 下，不带扩展名）。留空 = 不播动作，直接出手")]
    public string 普攻动作名 = "普攻_远程_01";

    [Tooltip("动作播到百分之多少时召雷（0.4 = 动画 40% 处）")]
    [Range(0.05f, 0.95f)] public float 出手进度 = 0.4f;

    [Tooltip("出手冷却（秒）。实际冷却 = 这个值 ÷ 攻速系数，和太虚炼气诀同一套换算")]
    public float 基础冷却 = 1.1f;

    [Tooltip("**自动出手**：冷却好了就自己打，不用按键")]
    public bool 自动出手 = true;

    [Tooltip("按键也还能触发（自动出手关掉后就是纯手动）")]
    public bool 按住连发 = true;

    [Header("按键")]
    public KeyCode 攻击键 = KeyCode.Mouse1;

    // ============================================================ 索敌

    [Header("索敌（神识）")]
    [Tooltip("★ 现在**只用于闪电链的搜索起点距离**（下劈的雷是劈锁定目标的，不再自动找最近的）。\n" +
             "神识为 0 时的基础值（米）")]
    public float 基础索敌范围 = 3.5f;

    [Tooltip("每 1 点神识增加多少米（和太虚炼气诀同一套换算）")]
    public float 每点神识范围 = 0.55f;

    [Tooltip("索敌半径上限（米）")]
    public float 索敌范围上限 = 18f;

    [Tooltip("敌人所在层")]
    public LayerMask 敌人层 = ~0;

    // ============================================================ 伤害

    [Header("伤害")]
    [Tooltip("下劈的雷：伤害属性。策划要求「特殊」")]
    public DamageNature 伤害属性 = DamageNature.特殊;

    [Tooltip("下劈的雷：伤害类别。策划要求「普攻」")]
    public AttackKind 攻击类别 = AttackKind.普通攻击;

    [Tooltip("下劈的雷：技能倍率")]
    public float 技能倍率 = 1f;

    // ============================================================ 特效

    [Header("下劈雷特效")]
    [Tooltip("雷特效的 Resources 路径（相对 Assets/resources，不带扩展名）")]
    public string 雷特效路径 = "特效/战斗法术/Combat Magic VFX Vol.1/resources/lightning-fx/lightning-ray";

    [Tooltip("★ 雷特效的**生成旋转**（欧拉角）。\n\n" +
             "**默认 (-90, 0, 0)** —— 用户 2026-09-28 实测确认：`lightning-ray` 原样生成是**躺平**的；\n" +
             "转 +90 会**倒过来**，正确的是 **−90**。\n\n" +
             "如果以后换特效又出现躺平/倒立，就改这个值（±90 互换即可）。")]
    public Vector3 雷特效旋转欧拉 = new Vector3(-90f, 0f, 0f);

    [Tooltip("【粒子对齐方式开关】把雷球/雷柱那几个部件从 `Billboard + alignment=View`" +
             "（永远正对相机）改成 `alignment=World`（不随相机转）。\n\n" +
             "用途：如果特效在 50° 俯视机位下看着歪斜，勾上它柱子会在世界里始终竖直；" +
             "代价是俯视时贴图被斜看、可能更扁。**两种都试一下，哪个顺眼留哪个。**\n" +
             "（旧特效 `Electro_Strike_01` 需要它；新特效 `lightning-ray` 未必要。）")]
    public bool 雷特效不随相机 = false;

    [Tooltip("雷特效播多久后销毁（秒）。\n" +
             "⚠️ 注意这是**真实时间**：特效内部被 `雷特效速度` 加速后，这里的秒数也要跟着缩短，\n" +
             "否则特效早就播完了还挂在那里（勾了 `存活随速度缩短` 就自动处理）。")]
    public float 雷特效存活 = 1.2f;

    [Tooltip("★ 雷特效的**整体播放速度倍率**。**1 = 原速（默认）**。\n\n" +
             "做法：把整棵树的 `main.simulationSpeed` 乘上这个值 —— 所有粒子的发射、\n" +
             "生命周期、速度一起加速。\n\n" +
             "⚠️ 如果只是**某个部件起步慢**（例：`ground-flashes` 有个 startDelay），\n" +
             "**直接去 prefab 里把那个 startDelay 改成 0**，别用这个整体加速 ——\n" +
             "整体加速会把所有部件（含 ground-pebbles）一起改快，不是你要的。")]
    public float 雷特效速度 = 1f;

    [Tooltip("勾上（默认）：自动把 `雷特效存活` 按速度缩短（存活 ÷ 速度），\n" +
             "这样加速后特效播完就消失、不会多挂一段空等。")]
    public bool 存活随速度缩短 = true;

    [Tooltip("【基准敌人高度】(米)：敌人这么高时特效 scale = 1。\n" +
             "雷会按「敌人高度 ÷ 这个值」等比缩放，再夹进下面的上下限")]
    public float 基准敌人高度 = 1.8f;

    [Tooltip("特效缩放下限（小怪身上别小到看不见）")]
    public float 缩放下限 = 0.6f;

    [Tooltip("特效缩放上限（巨兽身上别大到糊屏）")]
    public float 缩放上限 = 2.5f;

    [Tooltip("雷特效相对敌人脚底的抬高（米），免得整道雷埋进地里")]
    public float 雷抬高 = 0f;

    // ============================================================ 闪电链

    [Header("闪电链（命中后按概率蔓延）")]
    [Tooltip("掉出闪电链的概率（0.35 = 35%）")]
    [Range(0f, 1f)] public float 闪电链概率 = 0.35f;

    [Tooltip("闪电链能跳到多远（米）：以被打中的敌人为中心找下一个")]
    public float 闪电链范围 = 8f;

    [Tooltip("一条链最多跳几次（防止无限蔓延 / 卡死）")]
    public int 闪电链最多跳数 = 4;

    [Tooltip("闪电链伤害倍率（一般比下劈低）")]
    public float 闪电链倍率 = 0.6f;

    [Header("闪电链特效")]
    [Tooltip("★ 暂时空置（用户 2026-09-28 决定）：自制的「雷链」表现不合格已删除。\n" +
             "留空 = 只算伤害、不放特效，不会报错。\n" +
             "以后找到合适的资源，把路径填这里，并按下面几个字段重新标定 ——\n" +
             "要求与踩过的坑见 docs/guides/闪电链特效.md")]
    public string 闪电链特效路径 = "";

    [Tooltip("闪电链特效存活（秒）。策划要求「出现一下很快消失」")]
    public float 闪电链存活 = 0.42f;

    [Tooltip("闪电链特效的最小拉伸（米），防止两个敌人重叠时特效被压成 0")]
    public float 闪电链最小长度 = 1f;

    [Tooltip("闪电链两端固定外溢多少米（换特效后用探针重新标定；多数填 0）")]
    public float 闪电链长度补偿 = 0f;

    [Tooltip("闪电链特效的粗细缩放。1 = 原样")]
    public float 闪电链粗细 = 0.75f;

    [Tooltip("【特效原始长度】(米)：prefab 在 scale=1 时沿【拉伸轴】有多长。\n" +
             "拉伸系数 = （距离 − 长度补偿）÷ 这个值。\n" +
             "⚠️ 换特效必须重新标定，而且 prefab 要满足两条：\n" +
             "  scalingMode = Hierarchy（Shape 会把 localScale 作用两次）\n" +
             "  renderMode = Billboard（Stretch 在速度≈0 的粒子上会看不见）")]
    public float 闪电链基准长度 = 1f;

    [Tooltip("特效沿【本地哪根轴】拉伸。留空/填错不要紧：运行时会按 prefab 的 shape 自动探测最长的那个轴")]
    public Vector3 闪电链拉伸轴 = new Vector3(0f, 0f, 1f);

    [Tooltip("被闪电链打中时贴在敌人身上的命中特效（这个没删，还在用）")]
    public string 命中特效路径 = "特效/战斗法术/Combat Magic VFX Vol.1/resources/lightning-fx/lightning-arc-flash";

    [Tooltip("命中特效存活（秒）")]
    public float 命中特效存活 = 0.8f;

    // ============================================================ 引用

    [Header("引用（留空自动找）")]
    public PlayerCombatStats 玩家战斗属性;
    public NpcTargeting 目标管理器;
    public PlayerAnimationController 动画;

    [Header("调试")]
    public bool 打印战斗日志 = true;

    // ============================================================ 锁定目标

    /// <summary>
    /// **锁定单位** —— 要劈的目标。没锁定就是 null，此时**不出手**。
    ///
    /// 和 <see cref="BasicRemoteAttack01.锁定单位"/> 是同一套做法：
    /// 从 <see cref="NpcTargeting.LockedNpc"/> 取，死了就当没锁。
    /// </summary>
    public ICombatTarget 锁定单位
    {
        get
        {
            var npc = 目标管理器 != null ? 目标管理器.LockedNpc : null;
            if (npc == null || npc.IsDead) return null;
            return 缓存 ??= new NpcTarget(npc);
        }
    }

    /// <summary>缓存适配器，避免每次出手都 new（锁定的目标没变就复用）</summary>
    NpcTarget 缓存;
    int 缓存目标id;

    // ============================================================ 对外状态

    public float 攻速系数 => 玩家战斗属性 != null
        ? Mathf.Max(0.1f, 玩家战斗属性.当前属性[AttributeType.AttackSpeed])
        : 1f;

    public float 实际冷却 => 基础冷却 / Mathf.Max(0.1f, 攻速系数);
    public float 冷却剩余 => Mathf.Max(0f, 下次可出手时间 - Time.time);
    public float 索敌半径 => PlayerCombatStats.算神识范围(玩家战斗属性, 基础索敌范围, 每点神识范围, 索敌范围上限);

    /// <summary>现在能不能出手：**必须锁定了目标** + 冷却好了 + 没在做动作</summary>
    public bool 可以出手 => 锁定单位 != null && 冷却剩余 <= 0f && !出手动作中;

    public bool 出手动作中 { get; private set; }

    float 下次可出手时间;
    bool 本轮已出手;
    AnimationClip 普攻动作;

    void Awake() => 解析引用();

    void 解析引用()
    {
        if (玩家战斗属性 == null) 玩家战斗属性 = GetComponent<PlayerCombatStats>();
        if (目标管理器 == null) 目标管理器 = GetComponent<NpcTargeting>();
        if (动画 == null) 动画 = GetComponent<PlayerAnimationController>();
        if (动画 == null) 动画 = GetComponentInChildren<PlayerAnimationController>();
    }

    void OnEnable()
    {
        解析引用();
        if (动画 == null) return;
        if (普攻动作 == null && !string.IsNullOrEmpty(普攻动作名))
        {
            普攻动作 = Resources.Load<AnimationClip>("技能动作/" + 普攻动作名);
            if (普攻动作 == null)
                Debug.LogWarning("[basic_thunder_01] 找不到动作 Assets/resources/技能动作/" + 普攻动作名 + ".anim", this);
        }
    }

    void Update()
    {
        解析引用();

        // 目标换了就丢掉缓存的适配器，保证 锁定单位 跟着走（和 basic_remoteattack_01 一样）
        var npc = 目标管理器 != null ? 目标管理器.LockedNpc : null;
        int id = npc != null ? npc.GetInstanceID() : 0;
        if (id != 缓存目标id) { 缓存目标id = id; 缓存 = null; }

        // 动作播到 出手进度 → 召雷
        if (出手动作中 && !本轮已出手)
        {
            float 进度 = 动画 != null ? 动画.动作进度 : 1f;
            if (进度 >= 出手进度 || !(动画 != null && 动画.动作播放中))
            {
                本轮已出手 = true;
                召雷();
            }
        }

        if (出手动作中 && (动画 == null || !动画.动作播放中))
        {
            出手动作中 = false;
            下次可出手时间 = Time.time + 实际冷却;
        }

        // ★ 没锁定目标就不会走到这里（可以出手 里判了 锁定单位 != null），
        //   所以"没锁定时不攻击、也不进冷却"是自然成立的。
        if (可以出手 && (自动出手 || AttackPressed())) 出手();
    }

    bool AttackPressed() => 按住连发 ? Input.GetKey(攻击键) : Input.GetKeyDown(攻击键);

    /// <summary>出手一次（外部系统 / 测试可以直接调）</summary>
    public bool 出手()
    {
        if (!可以出手) return false;

        出手动作中 = true;
        本轮已出手 = false;

        if (动画 != null && 普攻动作 != null)
        {
            // 攻速直接决定动画播放速度（和 basic_remoteattack_01 一致）
            if (!动画.播动作(普攻动作, 攻速系数))
            {
                本轮已出手 = true;
                召雷();
            }
        }
        else
        {
            本轮已出手 = true;
            召雷();
        }
        return true;
    }

    // ============================================================ 召雷

    /// <summary>
    /// 对**锁定的目标**召一道雷下劈。
    ///
    /// ★ 2026-09-28 用户更正：**必须先锁定**才对敌人召雷，
    ///   不是"自动挑周边最近的那个打"。所以这里不再 `找最近的敌人`，
    ///   直接取 <see cref="锁定单位"/>；没锁定就什么都不做（也不会进冷却，因为压根没出手）。
    /// </summary>
    public void 召雷()
    {
        if (玩家战斗属性 == null) 解析引用();
        if (玩家战斗属性 == null)
        {
            Debug.LogWarning("[basic_thunder_01] 没有 PlayerCombatStats，算不了伤害", this);
            return;
        }

        var 目标 = 锁定单位;
        if (目标 == null)
        {
            if (打印战斗日志) Debug.Log("[basic_thunder_01] 没有锁定目标 → 不召雷", this);
            return;
        }

        float 缩放 = 按体型算缩放(目标);
        生成雷(目标, 缩放);

        var 规则 = new AttackSpec(伤害属性, 攻击类别, false, 技能倍率);
        var 结果 = 目标.受到攻击(玩家战斗属性, 规则, this);

        if (打印战斗日志)
            Debug.Log("[basic_thunder_01] 雷劈锁定目标「" + 目标.名字 + "」 " + 结果
                + "（缩放 " + 缩放.ToString("0.##")
                + " = 高的 " + 取敌人高度(目标).ToString("0.##") + "m ÷ 基准 " + 基准敌人高度.ToString("0.##") + "m"
                + "｜攻速 " + 攻速系数.ToString("0.##")
                + "｜冷却 " + 实际冷却.ToString("0.##") + "s）", this);

        if (结果.命中 && 结果.伤害 > 0f)
        {
            // 整条链共享「已访问」：起点自己先进去，之后每一步都往里加，
            // 这样 A→B 之后 B 不会再跳回 A（否则来回弹到跳数上限）
            var 已访问 = new HashSet<NpcInstance>();
            var 首个Npc = 目标.取Npc();
            if (首个Npc != null) 已访问.Add(首个Npc);
            尝试闪电链(目标, 已访问, 1);
        }
    }

    /// <summary>在目标身上放一道下劈的雷</summary>
    void 生成雷(ICombatTarget 目标, float 缩放)
    {
        Vector3 位 = 目标.根 != null ? 目标.根.position : 目标.判定点;
        位 += Vector3.up * 雷抬高;

        // 统一走「特效摆放」：旋转 → 开等比缩放 → 只对齐水平
        // （**竖直不做重定位** —— 用户明确要求对齐目标的"根部"、不是中部）
        var go = 特效摆放.生成(雷特效路径, 位, 雷特效旋转欧拉,
                              Mathf.Clamp(缩放, 缩放下限, 缩放上限),
                              对齐到锚点: false, 存活秒: 0f, 名: "雷劈_" + 目标.名字);
        if (go == null) return;

        特效摆放.只对齐水平(go, 位);
        开播放速度(go);                       // 整个特效按 雷特效速度 加速
        if (雷特效不随相机) 改成不随相机(go);

        // 存活时间要跟着速度缩短，否则加速后特效播完了还挂一段空等
        float 存活 = Mathf.Max(0.1f, 雷特效存活);
        if (存活随速度缩短) 存活 /= Mathf.Max(0.05f, 雷特效速度);
        Destroy(go, 存活);
    }

    /// <summary>
    /// 把整棵树的粒子系统按 `雷特效速度` 加速。
    ///
    /// 用 `main.simulationSpeed` —— 它会把粒子的**发射节奏、生命周期、速度**一起乘上倍率，
    /// 是真正的"整个特效加速"。比逐个改 duration / startLifetime / startSpeed 可靠得多
    /// （那种做法要改十几个部件，而且漏一个就不同步）。
    /// </summary>
    void 开播放速度(GameObject go)
    {
        float 速 = Mathf.Max(0.05f, 雷特效速度);
        if (Mathf.Approximately(速, 1f)) return;

        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null) continue;
            var m = ps.main;
            m.simulationSpeed = 速;
        }
    }

    /// <summary>
    /// 把「雷球 / 雷柱」这几个部件从 `Billboard + View`（永远正对相机）
    /// 改成 `alignment = World`（在世界里固定朝向，不随相机转）。
    ///
    /// 为什么只改这几个：环（`p6_rings_02`）本来就该面向相机（爆炸环要正对镜头才好看），
    /// 而**竖直的雷柱和球**才是"随相机转就歪"的元凶。
    /// </summary>
    void 改成不随相机(GameObject go)
    {
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            string n = ps.name;
            if (!(n.Contains("glow") || n.Contains("Lt") || n.Contains("spdrop") || n.Contains("sub_p")))
                continue;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            if (r != null) r.alignment = ParticleSystemRenderSpace.World;
        }
    }

    // ============================================================ 闪电链

    /// <summary>
    /// 按概率从「来源」蔓延一条闪电链到附近的下一个敌人；命中后可以继续蔓延。
    ///
    /// ⚠️ `已访问` 必须是**整条链共享**的（不能只排除上一个）——
    /// 否则 A→B 之后 B 又能跳回 A，两个敌人之间来回弹到跳数上限 ✗
    /// </summary>
    void 尝试闪电链(ICombatTarget 来源, HashSet<NpcInstance> 已访问, int 已跳数)
    {
        if (已跳数 > Mathf.Max(1, 闪电链最多跳数)) return;
        if (Random.value > 闪电链概率) return;

        var 来源根 = 来源.根;
        Vector3 起点 = 来源根 != null ? 来源根.position : 来源.判定点;

        var 下一个 = 找最近的敌人(起点, 闪电链范围, 已访问);
        if (下一个 == null) return;

        var 下一个Npc = 下一个.取Npc();
        if (下一个Npc != null) 已访问.Add(下一个Npc);

        float 缩放 = 按体型算缩放(下一个);
        // 起点那一侧也按**来源敌人的半身**抬高 → 链从双方中部对穿
        生成闪电链(起点, 下一个, 缩放, 取敌人高度(来源) * 0.5f);

        var 规则 = new AttackSpec(伤害属性, 攻击类别, false, 闪电链倍率);
        var 结果 = 下一个.受到攻击(玩家战斗属性, 规则, this);

        if (打印战斗日志)
            Debug.Log("[basic_thunder_01] 闪电链 →「" + 下一个.名字 + "」 " + 结果
                + "（第 " + 已跳数 + " 跳｜缩放 " + 缩放.ToString("0.##") + "）", this);

        // 命中特效：贴在被链到的敌人身上
        生成命中特效(下一个, 缩放);

        if (结果.命中 && 结果.伤害 > 0f) 尝试闪电链(下一个, 已访问, 已跳数 + 1);
    }

    /// <summary>
    /// 生成一道从「起点」连到「目标」的闪电链。
    ///
    /// 拉伸做法：特效本地的那根"长度轴"指向目标（`LookRotation(目标 − 起点)`），
    /// 再把该轴的 localScale 设成「两端三维距离 ÷ 基准长度」→ 雷正好横跨两者；
    /// 另外两根轴按体型缩放（决定粗细）。
    ///
    /// ⚠️ 长度轴**不是固定的**：这里**先按 prefab 的 shape 自动探测**（各粒子 `shape.scale`
    /// 最长的那个轴），探不出来才用字段上配的 <see cref="闪电链拉伸轴"/>。
    ///
    /// ⚠️ **特效槽现在空置待补** —— 自制的 `雷链` 已删，路径为空时这里直接 return，
    /// 只有伤害没有视觉。补特效要照 `docs/guides/闪电链特效.md`：
    /// prefab 必须 `scalingMode = Hierarchy`（`Shape` 会把 localScale 作用两次）
    /// 且 `renderMode = Billboard`（`Stretch` 在速度≈0 的粒子上会看不见）。
    /// </summary>
    /// <param name="起点抬高">起点那一侧要抬多高（= 来源敌人的半身）；0 = 用基准半高兜底</param>
    void 生成闪电链(Vector3 起点, ICombatTarget 目标, float 缩放, float 起点抬高 = 0f)
    {
        if (string.IsNullOrEmpty(闪电链特效路径)) return;
        var prefab = Resources.Load<GameObject>(闪电链特效路径);
        if (prefab == null)
        {
            Debug.LogWarning("[basic_thunder_01] 找不到闪电链特效：" + 闪电链特效路径, this);
            return;
        }

        Vector3 终点 = 目标.根 != null ? 目标.根.position : 目标.判定点;

        // ★ 两端的抬高**各自按自己那个敌人量**（半身高度），让链从双方的**中部**发出。
        //   以前是两侧都用固定的「基准敌人高度 × 0.5」—— 木桩（1.6m）之类矮目标上
        //   会显得链贴地/穿模。用户 2026-09-28 要求「闪电链从锁定目标的中部生成」。
        float 起点半高 = 起点抬高 > 0.01f ? 起点抬高 : 基准敌人高度 * 0.5f;
        float 终点半高 = Mathf.Max(0.1f, 取敌人高度(目标) * 0.5f);
        起点 += Vector3.up * 起点半高;
        终点 += Vector3.up * 终点半高;

        Vector3 向 = 终点 - 起点;
        if (向.sqrMagnitude < 0.0001f) 向 = Vector3.forward;
        // ★ 三维距离：两端高度不同时（台阶/浮空敌人）水平距离会让链短一截
        float 距离 = 向.magnitude;

        var 中点 = (起点 + 终点) * 0.5f;
        // 让本地 Z 轴指向目标；下面再按探测出来的长度轴分配缩放
        var 旋 = Quaternion.LookRotation(向.normalized, Vector3.up);

        var go = Instantiate(prefab, 中点, 旋);
        go.name = "闪电链_" + 目标.名字;

        var 长度轴 = 探测长度轴(prefab);
        // 把"长度轴指向目标"这件事一次性做掉：旋转后再绕 Y 补一个角度
        go.transform.rotation = 旋 * 长度轴旋转修正(长度轴);

        float 拉伸 = Mathf.Max(闪电链最小长度, 距离 - Mathf.Max(0f, 闪电链长度补偿))
                   / Mathf.Max(0.05f, 闪电链基准长度);
        float 粗 = Mathf.Clamp(缩放, 缩放下限, 缩放上限) * Mathf.Max(0.05f, 闪电链粗细);
        go.transform.localScale = 按轴分配(长度轴, 粗, 拉伸);

        Destroy(go, Mathf.Max(0.05f, 闪电链存活));
    }

    /// <summary>本地哪根轴是"长度轴"：看所有粒子 shape 的 scale，取最大的那根；探不出来用字段配的</summary>
    Vector3 探测长度轴(GameObject prefab)
    {
        var 最 = Vector3.zero;
        foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            var s = ps.shape;
            if (!s.enabled) continue;
            var sc = s.scale;
            if (sc.x > 最.x) 最.x = sc.x;
            if (sc.y > 最.y) 最.y = sc.y;
            if (sc.z > 最.z) 最.z = sc.z;
        }
        if (最.sqrMagnitude < 0.0001f) return 归一(闪电链拉伸轴);
        // 只保留最大的那一根（长度轴就是它）
        if (最.x >= 最.y && 最.x >= 最.z) return Vector3.right;
        if (最.y >= 最.x && 最.y >= 最.z) return Vector3.up;
        return Vector3.forward;
    }

    static Vector3 归一(Vector3 v)
    {
        if (v.sqrMagnitude < 0.0001f) return Vector3.forward;
        var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (a.x >= a.y && a.x >= a.z) return Vector3.right;
        if (a.y >= a.x && a.y >= a.z) return Vector3.up;
        return Vector3.forward;
    }

    /// <summary>
    /// `LookRotation` 让**本地 Z** 指向目标。如果长度轴是 X，就要再绕 Y 转 90°
    /// 让本地 X 对上本地 Z 原本的朝向。
    /// </summary>
    static Quaternion 长度轴旋转修正(Vector3 长度轴)
    {
        if (长度轴 == Vector3.right) return Quaternion.Euler(0f, 90f, 0f);
        if (长度轴 == Vector3.up) return Quaternion.Euler(0f, 0f, 90f);
        return Quaternion.identity;
    }

    static Vector3 按轴分配(Vector3 长度轴, float 粗, float 拉)
    {
        if (长度轴 == Vector3.right) return new Vector3(拉, 粗, 粗);
        if (长度轴 == Vector3.up) return new Vector3(粗, 拉, 粗);
        return new Vector3(粗, 粗, 拉);
    }

    /// <summary>被链到 / 被劈中的敌人身上贴一个命中特效</summary>
    void 生成命中特效(ICombatTarget 目标, float 缩放)
    {
        if (string.IsNullOrEmpty(命中特效路径)) return;
        var prefab = Resources.Load<GameObject>(命中特效路径);
        if (prefab == null) return;

        Vector3 位 = 目标.根 != null ? 目标.根.position : 目标.判定点;
        位 += Vector3.up * (基准敌人高度 * 0.5f);

        var go = Instantiate(prefab, 位, Quaternion.identity);
        go.name = "雷击命中_" + 目标.名字;
        go.transform.localScale = Vector3.one * Mathf.Clamp(缩放, 缩放下限, 缩放上限);
        Destroy(go, Mathf.Max(0.05f, 命中特效存活));
    }

    // ============================================================ 工具

    /// <summary>
    /// 找以「中心」为圆心、半径内**最近**的敌人。
    /// 同一个 NPC 身上常有好几个碰撞体，所以按 NpcInstance 去重。
    /// <paramref name="排除"/> 里的 NPC 不参与（闪电链用来防止跳回走过的敌人）。
    /// </summary>
    ICombatTarget 找最近的敌人(Vector3 中心, float 半径, HashSet<NpcInstance> 排除)
    {
        var 命中s = Physics.OverlapSphere(中心, Mathf.Max(0.05f, 半径), 敌人层, QueryTriggerInteraction.Ignore);

        NpcInstance 最近Npc = null;
        float 最近 = float.MaxValue;
        _去过重.Clear();

        foreach (var c in 命中s)
        {
            if (c == null) continue;
            var npc = c.GetComponentInParent<NpcInstance>();
            if (npc == null || npc.IsDead) continue;
            if (npc.transform == transform) continue;                 // 别打自己
            if (排除 != null && 排除.Contains(npc)) continue;
            if (!_去过重.Add(npc)) continue;                          // 同一 NPC 的多个碰撞体

            float d = (npc.transform.position - 中心).sqrMagnitude;
            if (d < 最近) { 最近 = d; 最近Npc = npc; }
        }

        return 最近Npc != null ? new NpcTarget(最近Npc) : null;
    }

    readonly HashSet<NpcInstance> _去过重 = new HashSet<NpcInstance>();

    /// <summary>
    /// 按敌人"体型"算特效缩放：敌人高度 ÷ 基准敌人高度，再夹进上下限。
    ///
    /// **嫌小/嫌大就调 `基准敌人高度`**：调大 → 特效变小；调小 → 特效变大。
    /// 开 `打印战斗日志` 会打出「高的 Xm ÷ 基准 Ym = 缩放 Z」，照着调就行。
    /// </summary>
    float 按体型算缩放(ICombatTarget 目标)
    {
        float 高 = 取敌人高度(目标);
        if (高 <= 0.01f) return 1f;
        return Mathf.Clamp(高 / Mathf.Max(0.1f, 基准敌人高度), 缩放下限, 缩放上限);
    }

    /// <summary>量敌人的高度：优先碰撞体包围盒，其次渲染体包围盒，都取不到用基准值</summary>
    float 取敌人高度(ICombatTarget 目标)
    {
        var 根 = 目标 != null ? 目标.根 : null;
        if (根 == null) return 基准敌人高度;

        var cols = 根.GetComponentsInChildren<Collider>();
        bool 有 = false;
        var b = new Bounds();
        foreach (var c in cols)
        {
            if (c == null || c.isTrigger) continue;
            if (!有) { b = c.bounds; 有 = true; } else b.Encapsulate(c.bounds);
        }
        if (!有)
        {
            var rs = 根.GetComponentsInChildren<Renderer>();
            foreach (var r in rs)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!有) { b = r.bounds; 有 = true; } else b.Encapsulate(r.bounds);
            }
        }
        return 有 ? Mathf.Max(0.05f, b.size.y) : 基准敌人高度;
    }

    // ---- ASCII 别名 ----
    public float AttackSpeedFactor => 攻速系数;
    public float SenseRadius => 索敌半径;
    public bool CanAttack => 可以出手;
}
