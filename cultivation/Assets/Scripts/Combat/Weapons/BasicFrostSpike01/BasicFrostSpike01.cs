using UnityEngine;

/// <summary>
/// 功法「沧澜寒渊录」提供的普攻方法（功法表 `普攻方法id = basic_frostspike_01`）。
///
/// ## 效果（策划说明）
/// **用太虚炼气诀那个普攻动作**，在**动画的 40% 节点**对**已锁定的敌人**召唤一根冰刺，
/// 特效是 `frost-shock`，造成 **特殊 + 普通攻击** 伤害。
/// 特效大小**随敌人体型缩放**（巨兽身上冰刺更大）。
///
/// ```
/// 普攻动作 普攻_远程_01（1.2s，和太虚炼气诀同一个片段）
///   ↓ 播到 40% 节点（0.48s）
/// 锁定敌人脚下生成 frost-shock（等比缩放到敌人体型）
///   ↓ 同一帧结算一次【特殊 + 普通攻击】伤害
/// ```
///
/// ★ **必须先用右键锁定一个目标**（和 <see cref="BasicThunder01"/> / <see cref="BasicRemoteAttack01"/>
///   同一套规矩）：没锁定就不出手、也不进冷却。
///
/// ## 和 <see cref="BasicThunder01"/>（玄霄雷决）的关系
/// 这一份是照它抄的骨架，**删掉了闪电链**那一整块（本功法没有蔓延），
/// 所以也不需要「索敌范围」那几个字段 —— 唯一的目标来源就是锁定单位。
/// 出手机制（`动画.动作进度 >= 出手进度`）、按体型缩放、特效摆放全部同一套口径，
/// 改这里的时候建议两边一起看一眼，别让它们漂移。
///
/// ## 特效摆放与缩放踩过的坑，这里直接复用 <see cref="特效摆放"/>
///   · prefab 常"躺平/倒立" → 旋转要实测给（见 <see cref="冰刺特效旋转欧拉"/>）；
///   · 缩放要先改 `scalingMode = Hierarchy`，否则只拉开间距、不放大粒子（工具里做了）；
///   · 竖直方向**不做重定位**，只对齐水平 —— 冰刺要长在敌人脚下，不是飘在腰上。
/// </summary>
public class BasicFrostSpike01 : MonoBehaviour
{
    [Tooltip("本普攻方法在功法表「普攻方法id」里的标识。境界页靠它反查提供这个普攻的功法")]
    public string 方法id = "basic_frostspike_01";

    // ============================================================ 动作

    [Header("出手动作")]
    [Tooltip("普攻动作片段（Assets/resources/技能动作/ 下，不带扩展名）。\n" +
             "★ 策划要求「用太虚练气决的那个普攻动作」，也就是这个片段")]
    public string 普攻动作名 = "普攻_远程_01";

    [Tooltip("动作播到百分之多少时召冰刺（0.4 = 动画 40% 节点）")]
    [Range(0.05f, 0.95f)] public float 出手进度 = 0.4f;

    [Tooltip("出手冷却（秒）。实际冷却 = 这个值 ÷ 攻速系数，和太虚炼气诀同一套换算")]
    public float 基础冷却 = 1.1f;

    [Tooltip("**自动出手**：冷却好了、且锁定了目标，就自己打，不用按键")]
    public bool 自动出手 = true;

    [Tooltip("按键也还能触发（自动出手关掉后就是纯手动）")]
    public bool 按住连发 = true;

    [Header("按键")]
    public KeyCode 攻击键 = KeyCode.Mouse1;

    // ============================================================ 伤害

    [Header("伤害")]
    [Tooltip("伤害属性。策划要求「特殊」")]
    public DamageNature 伤害属性 = DamageNature.特殊;

    [Tooltip("伤害类别。策划要求「普攻」")]
    public AttackKind 攻击类别 = AttackKind.普通攻击;

    [Tooltip("技能倍率（占位 1，按策划给的数值调）")]
    public float 技能倍率 = 1f;

    // ============================================================ 特效

    [Header("冰刺特效")]
    [Tooltip("冰刺特效的 Resources 路径（相对 Assets/resources，不带扩展名）。策划指定 frost-shock")]
    public string 冰刺特效路径 = "特效/战斗法术/Combat Magic VFX Vol.1/resources/frost-fx/frost-shock";

    [Tooltip("★ 冰刺特效的**生成旋转**（欧拉角）。\n\n" +
             "**默认 (-90, 0, 0)，2026-09-29 已实测标定**：在相机前并排生成 0 / −90 / +90 三种，\n" +
             "拍图对比后判定 —— 0 会**向左倾**、+90 向**右倾**，**−90 才是竖直朝上**（冰刺从地里拔出来）。\n\n" +
             "同一批资源包的 prefab 都是「躺平」的，`lightning-ray` 当年也是 −90，所以这里是同一个坑。\n" +
             "换特效后如果又发现躺平/倒立，改这个值（±90 互换即可）。")]
    public Vector3 冰刺特效旋转欧拉 = new Vector3(-90f, 0f, 0f);

    [Tooltip("冰刺特效存活（秒）。\n★ 勾了下面的「存活按特效自动」（默认）时，这个值只当**兜底**用。")]
    public float 冰刺特效存活 = 1.2f;

    [Tooltip("★ 勾上（默认）：**按 prefab 的自然总时长**决定多久后销毁。\n\n" +
             "为什么：写死一个偏短的秒数会在粒子还没淡完时把物件 Destroy —— 表现就是冰刺**忽然消失**。\n" +
             "实测 `frost-shock` 的自然总时长 **9.1 秒**（`spikes-*` 的粒子寿命 0~9 秒随机 → 慢慢融化消失），\n" +
             "早先写死 1.2 秒就是这么被硬切的（用户 2026-09-29 报的）。")]
    public bool 存活按特效自动 = true;

    [Tooltip("自动算出来的存活 × 这个系数（留一点余量让它彻底淡完）")]
    public float 存活倍率 = 1.05f;

    [Tooltip("自动存活的上限（秒）。**0 = 不封顶**。\n" +
             "嫌冰刺在地上留太久（frost-shock 自然要 ~9.5 秒）就填个 4 之类的值；\n" +
             "⚠️ 填得比自然时长小会重新出现「硬切」，只是没那么突兀。")]
    public float 存活上限 = 0f;

    [Tooltip("冰刺相对敌人**脚底**的抬高（米）。0 = 正好在脚下（冰刺从地里拔出来）")]
    public float 冰刺抬高 = 0f;

    [Tooltip("★ **对齐基准子节点**：以这个子节点的世界位置当特效的「中心」。\n\n" +
             "为什么需要：`frost-shock` 的**根节点不在几何中心**（实测根自身带 (−2.75, 0, 3.21) 偏移），\n" +
             "真正的中心是子节点 **`spikes-second`**（相对根 +0.196m）。用户 2026-09-29 指出。\n\n" +
             "留空 / 找不到该子节点 = 退回「按粒子几何中心只对齐水平」。")]
    public string 对齐参考子节点 = "spikes-second";

    [Header("按敌人体型缩放")]
    [Tooltip("【基准敌人高度】(米)：敌人这么高时特效 scale = 1。\n" +
             "冰刺会按「敌人高度 ÷ 这个值」等比缩放，再夹进下面的上下限")]
    public float 基准敌人高度 = 1.8f;

    [Tooltip("特效缩放下限（小怪身上别小到看不见）")]
    public float 缩放下限 = 0.6f;

    [Tooltip("特效缩放上限（巨兽身上别大到糊屏）")]
    public float 缩放上限 = 2.5f;

    // ============================================================ 引用

    [Header("引用（留空自动找）")]
    public PlayerCombatStats 玩家战斗属性;
    public NpcTargeting 目标管理器;
    public PlayerAnimationController 动画;

    [Header("调试")]
    public bool 打印战斗日志 = true;

    // ============================================================ 锁定目标

    /// <summary>
    /// **锁定单位** —— 要扎冰刺的目标。没锁定就是 null，此时**不出手**。
    /// 和 <see cref="BasicThunder01.锁定单位"/> 是同一套做法（死了就当没锁）。
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

    /// <summary>现在能不能出手：**必须锁定了目标** + 冷却好了 + 没在做动作</summary>
    public bool 可以出手 => !UiEscRegistry.SceneInputBlocked && 锁定单位 != null && 冷却剩余 <= 0f && !出手动作中;

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
                Debug.LogWarning("[basic_frostspike_01] 找不到动作 Assets/resources/技能动作/" + 普攻动作名 + ".anim", this);
        }
    }

    void Update()
    {
        解析引用();

        // 目标换了就丢掉缓存的适配器，保证 锁定单位 跟着走（和 basic_thunder_01 一样）
        var npc = 目标管理器 != null ? 目标管理器.LockedNpc : null;
        int id = npc != null ? npc.GetInstanceID() : 0;
        if (id != 缓存目标id) { 缓存目标id = id; 缓存 = null; }

        // 动作播到 出手进度 → 召冰刺
        if (出手动作中 && !本轮已出手)
        {
            float 进度 = 动画 != null ? 动画.动作进度 : 1f;
            if (进度 >= 出手进度 || !(动画 != null && 动画.动作播放中))
            {
                本轮已出手 = true;
                召冰刺();
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
            // 攻速直接决定动画播放速度（和 basic_remoteattack_01 / basic_thunder_01 一致）
            if (!动画.播动作(普攻动作, 攻速系数))
            {
                本轮已出手 = true;
                召冰刺();
            }
        }
        else
        {
            本轮已出手 = true;
            召冰刺();
        }
        return true;
    }

    // ============================================================ 召冰刺

    /// <summary>
    /// 对**锁定的目标**召一根冰刺：生成 frost-shock，按敌人体型缩放，并结算一次
    /// 【特殊 + 普通攻击】伤害。
    /// </summary>
    public void 召冰刺()
    {
        if (玩家战斗属性 == null) 解析引用();
        if (玩家战斗属性 == null)
        {
            Debug.LogWarning("[basic_frostspike_01] 没有 PlayerCombatStats，算不了伤害", this);
            return;
        }

        var 目标 = 锁定单位;
        if (目标 == null)
        {
            if (打印战斗日志) Debug.Log("[basic_frostspike_01] 没有锁定目标 → 不召冰刺", this);
            return;
        }

        float 缩放 = 按体型算缩放(目标);
        生成冰刺(目标, 缩放);

        var 规则 = new AttackSpec(伤害属性, 攻击类别, false, 技能倍率);
        var 结果 = 目标.受到攻击(玩家战斗属性, 规则, this);

        if (打印战斗日志)
            Debug.Log("[basic_frostspike_01] 冰刺扎「" + 目标.名字 + "」 " + 结果
                + "（缩放 " + 缩放.ToString("0.##")
                + " = 高的 " + 取敌人高度(目标).ToString("0.##") + "m ÷ 基准 " + 基准敌人高度.ToString("0.##") + "m"
                + "｜攻速 " + 攻速系数.ToString("0.##")
                + "｜冷却 " + 实际冷却.ToString("0.##") + "s"
                + "｜特效存活 " + 算存活().ToString("0.##") + "s）", this);
    }

    /// <summary>
    /// 这次生成该让特效活多久。
    ///
    /// 默认**按 prefab 的自然总时长**自动算（× <see cref="存活倍率"/>，再按 <see cref="存活上限"/> 夹），
    /// 免得粒子还没淡完就被 `Destroy` —— 硬切的表现就是**忽然消失**（用户 2026-09-29 报的 bug）。
    /// </summary>
    float 算存活()
    {
        float 存活 = Mathf.Max(0.1f, 冰刺特效存活);
        if (!存活按特效自动) return 存活;

        var 预制 = Resources.Load<GameObject>(冰刺特效路径);
        存活 = Mathf.Max(0.1f, 特效摆放.量特效总时长(预制, 冰刺特效存活) * Mathf.Max(0.5f, 存活倍率));
        if (存活上限 > 0.01f) 存活 = Mathf.Min(存活, 存活上限);
        return 存活;
    }

    /// <summary>在目标脚下放一根冰刺</summary>
    void 生成冰刺(ICombatTarget 目标, float 缩放)
    {
        Vector3 位 = 目标.根 != null ? 目标.根.position : 目标.判定点;
        位 += Vector3.up * 冰刺抬高;

        // 统一走「特效摆放」：旋转 → 开等比缩放 → 对齐（对齐方式见下）
        // 存活**默认按 prefab 自然时长自动算**（见 算存活），别写死短秒数硬切掉淡出
        var go = 特效摆放.生成(冰刺特效路径, 位, 冰刺特效旋转欧拉,
                              Mathf.Clamp(缩放, 缩放下限, 缩放上限),
                              对齐到锚点: false, 存活秒: 算存活(), 名: "冰刺_" + 目标.名字);
        if (go == null) return;

        // ★ 用户 2026-09-29 指出：`frost-shock` 的**根节点不在几何中心**，真正的中心是子节点
        //   `spikes-second`。所以**优先按这个子节点做三维对齐** —— 让它正好落在锚点
        //   （敌人脚下 + 冰刺抬高）上；没配 / 找不到才退回「按粒子几何中心只对齐水平」。
        if (!特效摆放.对齐子节点到(go, 对齐参考子节点, 位))
        {
            特效摆放.只对齐水平(go, 位);
            if (打印战斗日志)
                Debug.LogWarning("[basic_frostspike_01] 找不到对齐参考子节点「" + 对齐参考子节点
                                 + "」，已退回按粒子几何中心只对齐水平", this);
        }
    }

    // ============================================================ 工具

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
        return 特效摆放.量高度(根, 基准敌人高度);
    }

    // ---- ASCII 别名 ----
    public float AttackSpeedFactor => 攻速系数;
    public bool CanAttack => 可以出手;
}
