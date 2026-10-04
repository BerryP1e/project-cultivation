using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 功法「八九玄功」提供的普攻方法（功法表 `普攻方法id = basic_jiuba_01`）。
///
/// ## 这一份特殊在哪
/// 它和 <see cref="BasicFrostSpike01"/> / <see cref="BasicThunder01"/> 那些**远程**普攻的区别只有三点：
///
/// 1. **近战** —— 没有飞弹、没有落地特效，伤害靠**手里的刀真的扫到目标**才算数；
/// 2. **四段顺序轮转** —— 每次出手用序列里的下一段，循环（**不做连段窗口**，就是依次轮着放）；
/// 3. **御风时换片段** —— 处于御风状态时，把序列里每个名字拼上 <see cref="御风动作后缀"/>
///    再去 Load（那四个 `_御风` 片段**下半身是御风 Idle**，所以飞着砍时腿不会忽然落地）。
///
/// ```
/// 选中一段动作（地面版 / 御风版）
///   ↓ 播到 出手进度（默认 0.4）
///   ↓ 在 [出手进度, 出手进度+判定窗口] 这段进度里【逐帧】拿武器网格和锁定目标的身躯求交
/// 碰到的那一帧 → 只结算一次【物理 + 普通攻击】伤害（打的是锁定目标）
/// ```
///
/// ## 已知边界（都是有意为之，别当成 bug）
/// * **窗口内没扫到 = 打空**，这一下不结算任何伤害（和 <see cref="NpcBodyBounds"/> 那套近战怪的
///   「武器网格实时求交」口径完全一致）。想让它更容易命中，就调大 <see cref="判定外扩"/>
///   或者把 <see cref="判定窗口"/> 开长一点。
/// * **动作播不起来**（没 Animator / 片段为空）时**立刻结算一次**，不然这一下就白按了
///   （和 <see cref="BasicFrostSpike01"/> 的早退口径一致）。
/// * **只打锁定目标**：没锁定就不出手（<see cref="需要锁定目标"/> 关掉也只是"空砍"，
///   不会顺手打到旁边的怪 —— 不给别的目标结算伤害）。
/// * 索敌范围**不在本组件里判**：右键锁定的射程由 <see cref="NpcTargeting"/> 按**神识范围**
///   统一限制（所有武器共用一条规则），所以这里和 <see cref="BasicFrostSpike01"/> 一样
///   没有「索敌范围」字段，唯一的目标来源就是锁定单位。
///
/// ## 为什么逐帧求交用的是「碰撞体」而不是每次都算身躯包围盒
/// <see cref="NpcBodyBounds.取"/> 对蒙皮网格是**逐顶点自己蒙皮**（它的注释里写明
/// 「只在生成碰撞体 / 标定贴地时调，不在每帧」），放进逐帧循环会白烧性能。
/// 而 NPC 的碰撞体本来就是 `NpcBodyBounds.算胶囊` **按身躯（排除武器/飘带）**算出来的，
/// 所以逐帧用 `Collider.bounds` 又便宜又正确；只有**没有碰撞体**时才退回
/// <see cref="NpcBodyBounds.取"/>，而且**每次出手只算一次**（见 <see cref="取目标体积"/>）。
/// </summary>
public class BasicJiuba01 : MonoBehaviour
{
    [Tooltip("本普攻方法在功法表「普攻方法id」里的标识。境界页靠它反查提供这个普攻的功法")]
    public string 方法id = "basic_jiuba_01";

    // ============================================================ 动作

    [Header("出手动作（四段顺序轮转）")]
    [Tooltip("四段近战动作的 Resources 路径（相对 Assets/resources，不带扩展名）。\n" +
             "每次出手用【下一段】，循环；不做连段窗口。\n" +
             "★ 顺序是策划给的：长刀女 Attack1 → 长刀女 Attack2 → 杨戬 Attack1 → 杨戬 Attack2")]
    public string[] 动作序列 = new string[]
    {
        "技能动作/八九玄功/长刀女_Attack1",
        "技能动作/八九玄功/长刀女_Attack2",
        "技能动作/八九玄功/杨戬_Attack1",
        "技能动作/八九玄功/杨戬_Attack2",
    };

    [Tooltip("御风状态下的动作后缀：处于御风时，把序列里每个名字拼上这个后缀再 Load\n" +
             "（那四个片段已经存在：`长刀女_Attack1_御风` 这种，下半身是御风 Idle）。\n" +
             "取不到就**退回不带后缀的那个**并打一次警告")]
    public string 御风动作后缀 = "_御风";

    [Tooltip("动作播到百分之多少时开判定窗（0.4 = 动画 40% 节点）")]
    [Range(0.05f, 0.95f)] public float 出手进度 = 0.4f;

    [Tooltip("出手冷却（秒）。实际冷却 = 这个值 ÷ 攻速系数，和太虚炼气诀同一套换算")]
    public float 基础冷却 = 1.1f;

    [Tooltip("**自动出手**：冷却好了、且锁定了目标，就自己打，不用按键")]
    public bool 自动出手 = true;

    [Tooltip("按键也还能触发（自动出手关掉后就是纯手动）")]
    public bool 按住连发 = true;

    [Header("按键")]
    public KeyCode 攻击键 = KeyCode.Mouse1;

    // ============================================================ 近战判定

    [Header("近战判定（武器网格实时求交）")]
    [Tooltip("★ 勾上（默认）= 用**武器网格**做实时判定：在判定窗口里逐帧拿它去碰锁定目标的身躯，\n" +
             "**碰到的那一帧**才结算 —— 没碰到就是打空。\n" +
             "关掉 = 退回「到出手进度就打」（必中，不吃位置）。")]
    public bool 用武器网格判定 = true;

    [Tooltip("★ 武器子物件名（可以填多个，用 `|` 隔开）。\n" +
             "默认 `YangJian_03_Weapon` = 武器 prefab 里那个**蒙皮网格节点**的名字\n" +
             "（`武器挂载` 只改实例根节点的名字，网格节点名不变，所以这个名字照样找得到）。\n" +
             "**留空 / 找不到 = 退回「到出手进度就打」**。")]
    public string 武器节点名 = "YangJian_03_Weapon";

    [Tooltip("判定窗有多长（进度比例）。窗口 = [出手进度, 出手进度 + 这个值]，\n" +
             "窗口内**逐帧**求交，命中只结算一次。\n" +
             "★ 默认 0.55（窗口 0.40~0.95）是**照同模型的实测值定的**：长刀忠魂用的就是长刀女这套模型，\n" +
             "  它的实测接触窗是 **0.48~0.92**（见 NpcAiChangDaoZhongHun 的注释）；\n" +
             "  第一版我填 0.25（窗口只到 0.65），把 0.65~0.92 整段砍掉了，实测**会打空**。\n" +
             "  换片段/换武器要重新量（工程有 `近战动作量测` 工具，但只覆盖 NPC）")]
    [Range(0.01f, 0.9f)] public float 判定窗口 = 0.55f;

    [Tooltip("把武器判定的包围盒往外扩多少米（判定宽容度）")]
    public float 判定外扩 = 0.45f;

    [Tooltip("刀的横截面半径（米）。留 0 = 自动取蒙皮网格的次小半轴")]
    public float 武器半径 = 0f;

    [Tooltip("★ 每次出手把「判定窗内刀与目标的**最小距离**」打进日志 —— 用来**量**出手进度/判定窗口/判定外扩，\n" +
             "别猜（工程 NPC 侧踩过：用猜的外扩 ⇒ 穷奇 8 秒 0 次接触、0 伤害）")]
    public bool 打印判定距离 = true;

    [Tooltip("★ 勾上（默认）= **必须先用右键锁定一个目标**才出手，没锁定就不出手、也不进冷却。\n" +
             "关掉 = 可以空砍（但仍然只对锁定目标结算伤害）")]
    public bool 需要锁定目标 = true;

    [Tooltip("★ 出手最大距离（米）。超过它 **既不出手、也不进冷却**（和「没锁定」同一条路径）。\n" +
             "为什么必须有：右键锁定可以锁到 **40 米**（神识范围封顶），而刀只有 **4.1 米** 长，\n" +
             "玩家身上又没有任何自动贴近 —— 不设门槛就会**站在远处无限空砍、0 伤害、冷却照扣**。\n" +
             "留 0 或负数 = 不限制（退回旧行为）")]
    public float 出手最大距离 = 1.7f;

    // ============================================================ 伤害

    [Header("伤害")]
    [Tooltip("伤害属性。近战砍人 = **物理**（走暴击判定）")]
    public DamageNature 伤害属性 = DamageNature.物理;

    [Tooltip("伤害类别。普攻 = **普通攻击**")]
    public AttackKind 攻击类别 = AttackKind.普通攻击;

    [Tooltip("伤害倍率（= AttackSpec 的技能倍率）。普攻先填 1，按策划给的数值调")]
    public float 伤害倍率 = 1f;

    // ============================================================ 引用

    [Header("引用（留空自动找）")]
    public PlayerCombatStats 玩家战斗属性;
    public NpcTargeting 目标管理器;
    public PlayerAnimationController 动画;
    public YufengFlight 御风;
    [Tooltip("过场/对话演出锁（留空自动找玩家身上的）。演出期间不自动出手")]
    public 演出锁 演出锁;

    [Header("调试")]
    public bool 打印战斗日志 = true;

    // ============================================================ 对外状态

    public float 攻速系数 => 玩家战斗属性 != null
        ? Mathf.Max(0.1f, 玩家战斗属性.当前属性[AttributeType.AttackSpeed])
        : 1f;

    public float 实际冷却 => 基础冷却 / Mathf.Max(0.1f, 攻速系数);
    public float 冷却剩余 => Mathf.Max(0f, 下次可出手时间 - Time.time);

    /// <summary>实际冷却的别名（跟着攻速缩放的那个值）</summary>
    public float 冷却 => 实际冷却;

    /// <summary>现在能不能出手：锁定了目标（如果要求锁定）+ **距离够得着** + 冷却好了 + 没在做动作 + 没有过场演出在锁</summary>
    public bool 可以出手 => (!需要锁定目标 || 锁定单位 != null) && 在出手距离内 && !演出中 && 冷却剩余 <= 0f && !出手动作中;

    /// <summary>锁定目标离玩家多远（没锁定返回 0）</summary>
    public float 到目标距离
    {
        get
        {
            var t = 锁定单位;
            if (t == null || t.根 == null) return 0f;
            return Vector3.Distance(t.根.position, transform.position);
        }
    }

    /// <summary>距离够不够得着：没锁定 / 没限制 / 在最大距离内 ⇒ 够</summary>
    public bool 在出手距离内 => 出手最大距离 <= 0f || 锁定单位 == null || 到目标距离 <= 出手最大距离;

    /// <summary>过场/对话演出期间不自动挥砍（本组件不在 `演出锁.要锁的组件` 名单里，所以自己判）</summary>
    public bool 演出中 => 演出锁 != null && 演出锁.正在锁;

    public bool 出手动作中 { get; private set; }

    /// <summary>这一次出手用的动作路径（日志/测试用）</summary>
    public string 当前动作路径 { get; private set; } = "";

    /// <summary>这一次出手用的是不是御风版片段</summary>
    public bool 当前是御风版 { get; private set; }

    /// <summary>轮转用的出手序号（一直自增，取片段时按序列长度取模）</summary>
    public int 出手序号 { get; private set; }

    float 下次可出手时间;
    bool 本轮已结算;

    // 每次出手的判定标定数据（用于"量"窗口/外扩，见 打印判定距离）
    float 本次最近距离;
    bool 本次最近有效;
    float 上次阈值;

    // ---- 锁定目标（和 BasicFrostSpike01 同一套：死了就当没锁）----

    public ICombatTarget 锁定单位
    {
        get
        {
            var npc = 目标管理器 != null ? 目标管理器.LockedNpc : null;
            if (npc == null || npc.IsDead) return null;
            return 缓存 ??= new NpcTarget(npc);
        }
    }

    NpcTarget 缓存;
    int 缓存目标id;

    // ---- 片段缓存 ----

    readonly Dictionary<string, AnimationClip> 片段缓存 = new Dictionary<string, AnimationClip>();
    readonly HashSet<string> 报过找不到 = new HashSet<string>();

    // ---- 武器判定体缓存 ----

    Transform 判定体根;
    Renderer[] 判定体缓存;
    bool 报过找不到武器;

    // ---- 目标体积缓存（只给"没有碰撞体"的兜底用，每次出手重置）----

    Bounds 缓存目标体积;
    bool 缓存目标体积有效;

    void Awake() => 解析引用();

    void 解析引用()
    {
        if (玩家战斗属性 == null) 玩家战斗属性 = GetComponent<PlayerCombatStats>();
        if (目标管理器 == null) 目标管理器 = GetComponent<NpcTargeting>();
        if (动画 == null) 动画 = GetComponent<PlayerAnimationController>();
        if (动画 == null) 动画 = GetComponentInChildren<PlayerAnimationController>();
        if (御风 == null) 御风 = GetComponent<YufengFlight>();
        if (演出锁 == null) 演出锁 = GetComponent<演出锁>();
    }

    void OnEnable() => 解析引用();

    JiubaWeaponVfx 刃光;

    void OnDisable()
    {
        出手动作中 = false;
        if (刃光 != null) 刃光.停止(true);
    }

    void Update()
    {
        解析引用();

        // 目标换了就丢掉缓存的适配器（保证 锁定单位 跟着走，和 basic_frostspike_01 一样）
        var npc = 目标管理器 != null ? 目标管理器.LockedNpc : null;
        int id = npc != null ? npc.GetInstanceID() : 0;
        if (id != 缓存目标id) { 缓存目标id = id; 缓存 = null; }

        if (出手动作中 && !本轮已结算) 推进判定();

        if (出手动作中 && (动画 == null || !动画.动作播放中))
        {
            出手动作中 = false;
            下次可出手时间 = Time.time + 实际冷却;
        }

        // ★ 没锁定目标就不会走到这里（可以出手 里判了），所以"没锁定时不攻击、也不进冷却"自然成立
        if (可以出手 && (自动出手 || AttackPressed())) 出手();
    }

    bool AttackPressed() => 按住连发 ? Input.GetKey(攻击键) : Input.GetKeyDown(攻击键);

    // ============================================================ 出手

    /// <summary>出手一次（外部系统 / 测试可以直接调）</summary>
    public bool 出手()
    {
        if (!可以出手) return false;
        if (刃光 == null) 刃光 = GetComponent<JiubaWeaponVfx>() ?? gameObject.AddComponent<JiubaWeaponVfx>();

        出手动作中 = true;
        本轮已结算 = false;
        本次最近距离 = 0f; 本次最近有效 = false; 上次阈值 = 0f;
        缓存目标体积有效 = false;

        var 片段 = 取下一片段(out var 路径, out var 是御风版);
        当前动作路径 = 路径;
        当前是御风版 = 是御风版;

        var 目标 = 锁定单位;
        if (目标 != null && 目标.根 != null)
        {
            var movement = GetComponent<PlayerController>();
            if (movement != null) movement.对准攻击方向(目标.根.position - transform.position);
        }

        if (打印战斗日志)
            Debug.Log("[basic_jiuba_01] 出手：片段「" + 路径 + "」"
                + (是御风版 ? "（御风版）" : "（地面版）")
                + "｜目标「" + 取目标名() + "」"
                + "｜攻速 " + 攻速系数.ToString("0.##")
                + "｜冷却 " + 实际冷却.ToString("0.##") + "s"
                + "｜出手进度 " + 出手进度.ToString("0.##")
                + "｜判定窗口 " + 出手进度.ToString("0.##") + "~" + Mathf.Clamp01(出手进度 + 判定窗口).ToString("0.##"), this);

        if (动画 != null && 片段 != null)
        {
            // 攻速直接决定动画播放速度（和 basic_remoteattack_01 / basic_thunder_01 / basic_frostspike_01 一致）
            if (!动画.播动作(片段, 攻速系数))
            {
                // 播不起来就立刻结算，别让这一下白按（见文件头「已知边界」）
                本轮已结算 = true;
                结算伤害(Vector3.zero, "动作播不起来，立即结算");
            }
        }
        else
        {
            本轮已结算 = true;
            结算伤害(Vector3.zero, 片段 == null ? "没有可用片段，立即结算" : "没有动画组件，立即结算");
        }
        return true;
    }

    /// <summary>
    /// 动作播到判定窗：逐帧拿武器网格碰锁定目标，碰到就结算一次。
    /// 窗口过了还没碰到 = **打空**（这一下不结算伤害）。
    /// </summary>
    void 推进判定()
    {
        var 目标 = 锁定单位;
        float 进度 = 动画 != null ? 动画.动作进度 : 1f;
        bool 还在播 = 动画 != null && 动画.动作播放中;
        float 止 = Mathf.Clamp01(出手进度 + 判定窗口);

        // 没开网格判定 / 没配武器名 / 武器节点找不到 → 退回「到出手进度就打」
        if (!用武器网格判定 || string.IsNullOrEmpty(武器节点名) || 取武器判定体() == null)
        {
            if (进度 >= 出手进度 || !还在播)
            {
                本轮已结算 = true;
                结算伤害(Vector3.zero, "到进度即结算（没用武器网格判定）");
            }
            return;
        }

        if (进度 >= 出手进度 && 进度 <= 止)
        {
            if (武器扫到目标(out var 接触点))
            {
                本轮已结算 = true;
                结算伤害(接触点, "武器网格扫到（进度 " + 进度.ToString("0.###") + "）");
            }
            return;
        }

        // 窗口已经过去 / 动作结束了，还没扫到 → 打空
        if (进度 > 止 || !还在播)
        {
            本轮已结算 = true;
            if (打印战斗日志)
                Debug.Log("[basic_jiuba_01] 打空：「" + 取目标名() + "」没被武器扫到"
                    + "（窗口 " + 出手进度.ToString("0.##") + "~" + 止.ToString("0.##")
                    + "，动作进度 " + 进度.ToString("0.###")
                    + (打印判定距离 && 本次最近有效 ? "，窗口内刀离目标最近 " + 本次最近距离.ToString("F2") + " 米（判定阈值 " + 上次阈值.ToString("F2") + "）" : "")
                    + "）", this);
        }
    }

    // ============================================================ 结算

    /// <summary>对**锁定的目标**结算一次【物理 + 普通攻击】伤害</summary>
    void 结算伤害(Vector3 接触点, string 原因)
    {
        if (玩家战斗属性 == null) 解析引用();
        if (玩家战斗属性 == null)
        {
            Debug.LogWarning("[basic_jiuba_01] 没有 PlayerCombatStats，算不了伤害", this);
            return;
        }

        var 目标 = 锁定单位;
        if (目标 == null)
        {
            if (打印战斗日志) Debug.Log("[basic_jiuba_01] 没有锁定目标 → 不结算（" + 原因 + "）", this);
            return;
        }

        // 伤害公式的唯一入口在 NpcInstance/PlayerVitals 内部的 CombatCalculator（见 ICombatTarget.受到攻击）
        var 规则 = new AttackSpec(伤害属性, 攻击类别, false, 伤害倍率);
        var 结果 = 目标.受到攻击(玩家战斗属性, 规则, this);
        if (结果.命中)
        {
            var 位置 = 接触点 == Vector3.zero ? 取目标体积(目标).center : 接触点;
            BasicSword01HitEffect.Spawn(位置, 位置 - transform.position, 结果.暴击,
                new Color(1f, 0.88f, 0.55f));
        }

        if (打印战斗日志)
            Debug.Log("[basic_jiuba_01] 砍「" + 目标.名字 + "」 " + 结果
                + "（" + 原因 + "，片段「" + 当前动作路径 + "」"
                + "，接触点 " + 接触点.ToString("F2")
                + "，倍率 " + 伤害倍率.ToString("0.##") + "）", this);
    }

    // ============================================================ 武器判定体

    /// <summary>
    /// 取「武器判定体」这一堆渲染体（缓存）。
    /// `武器节点名` 支持 `名字A|名字B`（同一套武器的几个变体名都写上），大小写不敏感 ——
    /// 和 <c>NpcAiBase.取判定体</c> 是同一套写法。
    /// </summary>
    Renderer[] 取武器判定体()
    {
        if (判定体缓存 != null && 判定体根 != null) return 判定体缓存;
        if (判定体根 == null)
        {
            if (string.IsNullOrEmpty(武器节点名)) return null;

            foreach (var 候选 in 武器节点名.Split('|'))
            {
                var 名 = 候选.Trim();
                if (名.Length == 0) continue;
                foreach (var t in GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && string.Equals(t.name, 名, System.StringComparison.OrdinalIgnoreCase))
                    { 判定体根 = t; break; }
                }
                if (判定体根 != null) break;
            }

            if (判定体根 == null)
            {
                if (!报过找不到武器)
                {
                    报过找不到武器 = true;
                    Debug.LogWarning("[basic_jiuba_01] 在自己身上找不到武器节点「" + 武器节点名
                        + "」→ 退回「到出手进度就打」。\n" +
                        "（武器要由 `武器挂载` 组件实例化；prefab 里那个蒙皮网格节点就叫 YangJian_03_Weapon）", this);
                }
                return null;
            }
        }
        判定体缓存 = 判定体根.GetComponentsInChildren<Renderer>(true);
        return 判定体缓存;
    }

    /// <summary>这一帧武器有没有碰到锁定目标。碰到就给出**接触点**（在目标表面/最近点）</summary>
    bool 武器扫到目标(out Vector3 接触点)
    {
        接触点 = Vector3.zero;
        var 目标 = 锁定单位;
        if (目标 == null) return false;

        var 体 = 取武器判定体();
        if (体 == null || 体.Length == 0) return false;

        // ★ 优先走「武器线段 × 目标胶囊」。
        //   为什么不用两个 AABB 相交：这把刀 **4.9 米长、还在转**，`Renderer.bounds` 的 AABB
        //   中心在挥砍中会跑到目标 3~6 米外（实测），"盒相交"时中时不中。
        //   单骨刚性蒙皮的武器可以精确算端点：world = bone.localToWorldMatrix × bindpose。
        Vector3 端A, 端B; float 武器半径;
        bool 有线段 = 取武器线段(体, out 端A, out 端B, out 武器半径);

        if (有线段)
        {
            var 胶囊 = 目标.根 != null ? 目标.根.GetComponentInChildren<CapsuleCollider>() : null;
            if (胶囊 != null)
            {
                var 缩 = 胶囊.transform.lossyScale;
                float 世界半径 = 胶囊.radius * Mathf.Max(Mathf.Abs(缩.x), Mathf.Max(Mathf.Abs(缩.y), Mathf.Abs(缩.z)));
                var 轴 = 胶囊.transform.up;
                float 半 = Mathf.Max(0f, 胶囊.height * 0.5f - 胶囊.radius) * Mathf.Max(Mathf.Abs(缩.y), Mathf.Max(Mathf.Abs(缩.x), Mathf.Abs(缩.z)));
                var q1 = 胶囊.transform.TransformPoint(胶囊.center) + 轴 * 半;
                var q2 = 胶囊.transform.TransformPoint(胶囊.center) - 轴 * 半;

                Vector3 cp, cq;
                float 距 = 线段距离(端A, 端B, q1, q2, out cp, out cq);
                上次阈值 = 世界半径 + 武器半径 + 判定外扩;
                if (!本次最近有效 || 距 < 本次最近距离) { 本次最近距离 = 距; 本次最近有效 = true; }
                if (距 <= 上次阈值)
                {
                    接触点 = cq;
                    return true;
                }
                return false;      // 有胶囊就按线段判，不再退回盒（免得又时中时不中）
            }
        }

        // 兜底：没有线段（非单骨蒙皮）或目标没有胶囊 ⇒ 退回原来的盒相交
        bool 有 = false;
        var 武器体 = new Bounds();
        foreach (var r in 体)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer) continue;          // 粒子不算判定
            if (!有) { 武器体 = r.bounds; 有 = true; } else 武器体.Encapsulate(r.bounds);
        }
        if (!有) return false;
        if (判定外扩 > 0f) 武器体.Expand(判定外扩 * 2f);

        var 目标体 = 取目标体积(目标);
        if (!武器体.Intersects(目标体)) return false;
        接触点 = 目标体.ClosestPoint(武器体.center);
        return true;
    }

    /// <summary>
    /// 取武器的「长轴线段」（世界坐标）+ 横截面半径。
    /// 单骨刚性蒙皮：世界点 = bone.localToWorldMatrix × bindpose × 网格本地点（这才是刀真正所在的位置，
    /// 不受 `Renderer.bounds` 那个乱跳的 AABB 影响）。
    /// </summary>
    bool 取武器线段(Renderer[] 体, out Vector3 端A, out Vector3 端B, out float 半径)
    {
        端A = 端B = Vector3.zero; 半径 = 0.15f;
        foreach (var r in 体)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer) continue;

            var smr = r as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null)
            {
                if (!线段已标定 || 线段SMR != smr) 标定线段(smr);
                if (!线段已标定) continue;
                端A = 线段SMR.transform.TransformPoint(线段本地A);
                端B = 线段SMR.transform.TransformPoint(线段本地B);
                var 缩 = 线段SMR.transform.lossyScale;
                半径 = 线段本地半径 * Mathf.Max(Mathf.Abs(缩.x), Mathf.Max(Mathf.Abs(缩.y), Mathf.Abs(缩.z)));
                return true;
            }

            // 普通渲染体（不是蒙皮）：用它的包围盒最长轴凑一条线段
            var bb = r.bounds;
            var h = bb.size * 0.5f;
            int a = (h.x >= h.y && h.x >= h.z) ? 0 : (h.y >= h.z ? 1 : 2);
            var dd = a == 0 ? new Vector3(h.x, 0f, 0f) : (a == 1 ? new Vector3(0f, h.y, 0f) : new Vector3(0f, 0f, h.z));
            端A = bb.center - dd; 端B = bb.center + dd;
            半径 = Mathf.Max(0.05f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)));
            return true;
        }
        return false;
    }

    SkinnedMeshRenderer 线段SMR;
    Vector3 线段本地A, 线段本地B;
    float 线段本地半径 = 0.15f;
    bool 线段已标定;

    /// <summary>
    /// ★ 用 `SkinnedMeshRenderer.BakeMesh` **问 Unity 要蒙皮后的真实顶点**来标定刀身线段。
    ///
    /// 为什么不能手算：先前用 `bone.localToWorldMatrix × bindpose × 网格包围盒` 算，
    /// 结果与**画面上刀的位置**对不上（算出来刀在脚下一米多、与靶子差 1.8 米，而截图里刀端在胸前）
    /// —— 那个 prefab 的渲染体被重新挂过父节点，bindpose 是原模型坐标系里烘的，手算式不再成立。
    /// `BakeMesh` 拿到的是**引擎自己的蒙皮结果**，不会再出现"算出来的刀和看到的刀不是同一把"。
    /// 烘一次就缓存（端点存渲染体本地坐标，每帧只做一次 TransformPoint）。
    /// </summary>
    void 标定线段(SkinnedMeshRenderer smr)
    {
        var 烘 = new Mesh();
        try
        {
            smr.BakeMesh(烘);
            var 界 = 烘.bounds;
            var 半 = 界.size * 0.5f;
            int 轴 = (半.x >= 半.y && 半.x >= 半.z) ? 0 : (半.y >= 半.z ? 1 : 2);
            var d = 轴 == 0 ? new Vector3(半.x, 0f, 0f) : (轴 == 1 ? new Vector3(0f, 半.y, 0f) : new Vector3(0f, 0f, 半.z));
            线段本地A = 界.center - d;
            线段本地B = 界.center + d;
            线段本地半径 = 武器半径 > 0f ? 武器半径 : Mathf.Max(0.05f, Mathf.Min(半.x, Mathf.Min(半.y, 半.z)));
            线段SMR = smr;
            线段已标定 = true;
            if (打印战斗日志)
                Debug.Log("[basic_jiuba_01] 刀身线段已标定（BakeMesh）：本地 " + 线段本地A.ToString("F3") + " → " + 线段本地B.ToString("F3")
                          + "，长 " + Vector3.Distance(线段本地A, 线段本地B).ToString("F2") + " 米，半径 " + 线段本地半径.ToString("F3"), this);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[basic_jiuba_01] BakeMesh 标定失败：" + e.Message + " → 本轮退回盒相交", this);
            线段已标定 = false;
        }
        finally { UnityEngine.Object.DestroyImmediate(烘); }
    }

    /// <summary>两条线段之间的最近距离（Ericson, Real-Time Collision Detection 的 ClosestPtSegmentSegment）</summary>
    static float 线段距离(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2, out Vector3 最近p, out Vector3 最近q)
    {
        var d1 = p2 - p1;
        var d2 = q2 - q1;
        var r = p1 - q1;
        float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float s, t;
        const float 极小 = 1e-8f;

        if (a <= 极小 && e <= 极小) { s = t = 0f; }
        else if (a <= 极小) { s = 0f; t = Mathf.Clamp01(f / e); }
        else
        {
            float c = Vector3.Dot(d1, r);
            if (e <= 极小) { t = 0f; s = Mathf.Clamp01(-c / a); }
            else
            {
                float b = Vector3.Dot(d1, d2);
                float 分母 = a * e - b * b;
                s = 分母 != 0f ? Mathf.Clamp01((b * f - c * e) / 分母) : 0f;
                t = (b * s + f) / e;
                if (t < 0f) { t = 0f; s = Mathf.Clamp01(-c / a); }
                else if (t > 1f) { t = 1f; s = Mathf.Clamp01((b - c) / a); }
            }
        }
        最近p = p1 + d1 * s;
        最近q = q1 + d2 * t;
        return Vector3.Distance(最近p, 最近q);
    }

    /// <summary>
    /// 锁定目标这一帧的「身躯」体积。
    /// 逐帧走**碰撞体**（便宜，而且 NPC 的碰撞体本身就是按身躯算的）；
    /// 没有碰撞体才退回 <see cref="NpcBodyBounds.取"/>，而且**每次出手只算一次**。
    /// </summary>
    Bounds 取目标体积(ICombatTarget 目标)
    {
        var 根 = 目标 != null ? 目标.根 : null;
        if (根 == null) return new Bounds(Vector3.zero, Vector3.zero);

        var 碰 = 根.GetComponentInChildren<Collider>();
        if (碰 != null && 碰.enabled) return 碰.bounds;

        if (!缓存目标体积有效)
        {
            缓存目标体积有效 = true;
            if (NpcBodyBounds.取(根.gameObject, out var 身躯)) 缓存目标体积 = 身躯;
            else 缓存目标体积 = new Bounds(根.position + Vector3.up * NpcTarget.判定高度, new Vector3(0.8f, 1.6f, 0.8f));
        }
        return 缓存目标体积;
    }

    // ============================================================ 选片段

    /// <summary>取下一段动作（顺序轮转，循环），并按御风状态挑地面版 / 御风版</summary>
    AnimationClip 取下一片段(out string 路径, out bool 是御风版)
    {
        路径 = "";
        是御风版 = false;
        if (动作序列 == null || 动作序列.Length == 0) return null;

        int i = (int)Mathf.Repeat(出手序号, 动作序列.Length);
        出手序号++;
        路径 = 动作序列[i];
        return 取片段(路径, out 是御风版);
    }

    /// <summary>按路径取片段；御风时优先拼后缀，取不到退回地面版并**只警告一次**</summary>
    AnimationClip 取片段(string 路径, out bool 是御风版)
    {
        是御风版 = false;
        if (string.IsNullOrEmpty(路径)) return null;

        bool 在御风 = 御风 != null && 御风.御风流程中;
        if (在御风 && !string.IsNullOrEmpty(御风动作后缀))
        {
            var 御风路径 = 路径 + 御风动作后缀;
            var 御风片段 = 读片段(御风路径);
            if (御风片段 != null) { 是御风版 = true; return 御风片段; }

            if (报过找不到.Add(御风路径))
                Debug.LogWarning("[basic_jiuba_01] 找不到御风版动作「" + 御风路径 + "」→ 退回地面版「" + 路径 + "」", this);
        }
        return 读片段(路径);
    }

    /// <summary>读一个 Resources 片段（缓存；找不到只警告一次，且不缓存 null）</summary>
    AnimationClip 读片段(string 路径)
    {
        if (片段缓存.TryGetValue(路径, out var 已有)) return 已有;

        var 片段 = Resources.Load<AnimationClip>(路径);
        if (片段 != null) 片段缓存[路径] = 片段;
        else if (报过找不到.Add(路径))
            Debug.LogWarning("[basic_jiuba_01] 找不到动作 Assets/resources/" + 路径 + ".anim", this);
        return 片段;
    }

    /// <summary>把判定体缓存清掉（换武器 / 换外观之后要重新解析）</summary>
    public void 清判定缓存()
    {
        判定体根 = null;
        判定体缓存 = null;
        报过找不到武器 = false;
    }

    string 取目标名()
    {
        var 目标 = 锁定单位;
        return 目标 != null ? 目标.名字 : "（没锁定）";
    }

    // ---- ASCII 别名 ----
    public float AttackSpeedFactor => 攻速系数;
    public bool CanAttack => 可以出手;
    public bool ActionPlaying => 出手动作中;
}
