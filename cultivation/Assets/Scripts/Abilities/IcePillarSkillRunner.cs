using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **寒墟**（向前冰柱档 `ActiveSkillKind.向前冰柱`）的执行体。
///
/// 策划口径（用户 2026-09-29，含两处更正）：
/// ```
/// 用太虚炼气诀的普攻动作（普攻_远程_01），在**动画 40% 节点**出手
/// 以【鼠标落点】为方向，放**一道**连续向前的冰柱（特效 frost-wave 本身就是那根长冰柱）：
///   冰柱的**近端在玩家这里**、另一端朝鼠标方向
///   → 路径上的敌人吃【第一段：特殊 + 主动神通】
///   → 随后这些敌人**脚下**出现 frost-ring，吃【第二段】
///   → frost-ring 之后再在脚下出现 frost-spike，吃【第三段】
/// 二段三段同样是【特殊 + 主动神通】
/// ```
///
/// ## ★ 冰柱是「一个特效」，不是铺很多个
/// 用户 2026-09-29 更正：**别再一节节铺 frost-wave**。这个 prefab 本身就是一根
/// **11.5 米长的连续冰柱**，而且它的**长度轴是本地 +X**（实测：原样生成时粒子沿世界 X 伸 11.5m、
/// Y 8.3m、Z 只有 2.3m；绕 Y 转 90° 后长轴跟着转到 Z）。
/// 所以摆放要两件事一起做：
///   1. 旋转：`LookRotation(方向) × Euler(0,−90,0)` —— 让**本地 +X** 对准朝向；
///   2. 位置：沿朝向挪 **半个柱长** —— prefab 的柱体是**以生成点为中心**的
///      （实测粒子中心只偏 (−0.12, 0.69, −0.06)），不挪的话玩家会站在柱子**中间**，
///      而用户要的是「一段在玩家这里（起始那边）」。
///
/// ## 三段怎么排期
/// · **一段**：冰柱放出后，伤害**从玩家往前方扫**（`范围` 米），扫过谁打谁（按 NpcInstance 去重）。
/// · **二段**：被打中的敌人各自记账，`伤害间隔` 秒后在其**脚下**放 frost-ring 并结算。
/// · **三段**：二段之后再等 `三段延迟` 秒（填 0 = 自动取 frost-ring 自然时长，最多 `三段最长延迟`）。
///
/// ## 特效摆放的坑（都实测过）
/// · 这批 `frost-*` prefab **本来就是竖着做的**（子节点全在 +Y），旋转别照抄 `frost-shock` 的 −90。
/// · 存活**不要写死短秒数**：一律走 <see cref="特效摆放.量特效总时长"/>，
///   否则粒子还没淡完就被 Destroy →「忽然消失」（冰刺上踩过）。
/// </summary>
public class IcePillarSkillRunner : MonoBehaviour
{
    ActiveDivineAbility 神通;
    PlayerCombatStats 战斗属性;
    Transform 玩家;
    LayerMask 敌人层;
    Camera 相机;
    PlayerAnimationController 动画;

    AnimationClip 动作;
    bool 已出手;
    Vector3 方向;
    float 起算时刻;
    float 已扫描到;
    float 速度;
    bool 铺完;

    /// <summary>被打中的敌人（去重 + 记二/三段的时刻）</summary>
    class 命中账
    {
        public NpcInstance npc;
        public float 二段时刻;
        public float 三段时刻;
        public bool 二段已做;
        public bool 三段已做;
    }
    readonly List<命中账> 账本 = new List<命中账>();
    readonly HashSet<NpcInstance> 去重 = new HashSet<NpcInstance>();
    readonly Collider[] 命中缓存 = new Collider[64];

    public int 一段命中数 { get; private set; }
    public int 二段命中数 { get; private set; }
    public int 三段命中数 { get; private set; }
    public float 累计伤害 { get; private set; }
    /// <summary>这次放到第几成动作才出手（调试用，实测应该 ≈0.40）</summary>
    public float 出手时动作进度 { get; private set; }
    public bool 已放出冰柱 => 已出手;

    // ============================================================ 动作（对齐普攻）

    [Header("出手动作（和太虚炼气诀的普攻同一个动作）")]
    [Tooltip("普攻动作片段（Assets/resources/技能动作/ 下，不带扩展名）")]
    public string 动作名 = "普攻_远程_01";

    [Tooltip("★ 动作播到几成时放出冰柱（0.4 = 动画 40% 节点，和普攻/追踪弹同一套口径）")]
    [Range(0.05f, 0.95f)] public float 出手进度 = 0.4f;

    [Tooltip("动作播放速度倍率。1 = 原速（施法动作不受攻速影响）")]
    public float 动作速度 = 1f;

    // ============================================================ 冰柱

    [Header("冰柱（一个 frost-wave，从玩家脚下朝鼠标方向）")]
    [Tooltip("冰柱特效。空着就用表里「特效资源路径」那列")]
    public string 冰柱特效路径 = "";

    [Tooltip("★ **冰柱的自然长度**（米）：frost-wave 在 scale=1 时实测 **11.5 米**。\n" +
             "用途：把柱体沿朝向挪「半个柱长」，让**近端正好在玩家脚下**（prefab 是以生成点为中心的）。\n" +
             "换了特效要重新量（先原样生成、跑 0.5 秒，读粒子的世界包围盒最长边）。")]
    public float 冰柱基准长度 = 11.5f;

    [Tooltip("生成旋转（欧拉角）。**一般别动** —— 朝向由代码按鼠标方向算（本地 +X 对前方）。")]
    public Vector3 冰柱旋转欧拉 = Vector3.zero;

    [Tooltip("【调试/摆拍用】非零 = **强制用这个方向**当朝向，不看鼠标。\n" +
             "自动化测试（鼠标不在场景里）和截图摆拍时用；正常玩留 (0,0,0)")]
    public Vector3 调试方向 = Vector3.zero;

    [Tooltip("冰柱缩放（1 = 原大小）。缩放会同时按比例改变柱长")]
    public float 冰柱缩放 = 1f;

    [Tooltip("冰柱相对地面的抬高（米）")]
    public float 冰柱抬高 = 0f;

    // ============================================================ 推进 / 伤害

    [Header("推进与伤害")]
    [Tooltip("伤害扫过整条路径用多久（秒）。填 0 = 用表里「持续时长」")]
    public float 推进时长 = 0f;

    [Tooltip("伤害走廊宽度（米）：以扫描点为圆心、这个宽度的一半为半径取敌人")]
    public float 冰柱宽度 = 2.6f;

    [Header("二段 / 三段（都放在敌人脚下）")]
    [Tooltip("二段特效（frost-ring）。空着就用表里「命中特效路径」那列")]
    public string 二段特效路径 = "";

    [Tooltip("三段特效（frost-spike）。空着就用表里「三段特效路径」那列")]
    public string 三段特效路径 = "";

    [Tooltip("三段延迟（秒）：二段 frost-ring 出来之后再过多久出 frost-spike。\n" +
             "**填 0 = 自动**：取 frost-ring 自然时长，但最多 三段最长延迟")]
    public float 三段延迟 = 1.5f;

    [Tooltip("自动三段延迟的上限（秒）。frost-ring 自然时长 10.1s（长尾），全等它太久")]
    public float 三段最长延迟 = 3f;

    [Tooltip("二段伤害倍率（相对表里「伤害倍率」的倍数）")]
    public float 二段倍率 = 1f;

    [Tooltip("三段伤害倍率（相对表里「伤害倍率」的倍数）")]
    public float 三段倍率 = 1f;

    [Tooltip("二段特效缩放（1 = 原大小；按体型缩放见下）")]
    public float 二段缩放 = 1f;

    [Tooltip("三段特效缩放")]
    public float 三段缩放 = 1f;

    [Tooltip("勾上（默认）：二/三段特效按**敌人体型**等比缩放")]
    public bool 按体型缩放 = true;

    [Tooltip("基准敌人高度（米）：敌人这么高时 scale = 1")]
    public float 基准敌人高度 = 1.8f;

    public float 缩放下限 = 0.6f;
    public float 缩放上限 = 2.5f;

    [Header("兜底")]
    [Tooltip("执行体自己的存活上限（秒）")]
    public float 最长存活 = 14f;

    [Tooltip("打印日志")]
    public bool 打印日志 = true;

    // ============================================================ 初始化

    public void 初始化(ActiveDivineAbility 神通, PlayerCombatStats 战斗属性, Transform 玩家,
                        LayerMask 敌人层, Camera 相机, PlayerAnimationController 动画)
    {
        this.神通 = 神通;
        this.战斗属性 = 战斗属性;
        this.玩家 = 玩家;
        this.敌人层 = 敌人层;
        this.相机 = 相机;
        this.动画 = 动画;
    }

    void Start()
    {
        if (神通 == null || 玩家 == null) { Destroy(gameObject); return; }

        if (string.IsNullOrEmpty(冰柱特效路径)) 冰柱特效路径 = 神通.特效资源路径;
        if (string.IsNullOrEmpty(二段特效路径)) 二段特效路径 = 神通.命中特效路径;
        if (string.IsNullOrEmpty(三段特效路径)) 三段特效路径 = 神通.三段特效路径;

        if (动画 == null) 动画 = 玩家.GetComponent<PlayerAnimationController>();
        if (动画 == null) 动画 = 玩家.GetComponentInChildren<PlayerAnimationController>();
        if (!string.IsNullOrEmpty(动作名)) 动作 = Resources.Load<AnimationClip>("技能动作/" + 动作名);

        // 播普攻动作；播不起来（没动画组件 / 找不到片段）就**立刻放冰柱**，
        // 免得整个神通因为动画问题变成哑炮（和追踪弹/普攻一个口径）。
        bool 播上了 = false;
        if (动画 != null && 动作 != null) 播上了 = 动画.播动作(动作, Mathf.Max(0.05f, 动作速度));
        if (!播上了)
        {
            if (动画 == null) Debug.LogWarning("[寒墟] 找不到 PlayerAnimationController，直接放冰柱", this);
            else if (动作 == null) Debug.LogWarning("[寒墟] 找不到动作 Assets/resources/技能动作/" + 动作名 + ".anim，直接放冰柱", this);
            出手();
            return;
        }

        if (打印日志)
            Debug.Log("[寒墟] 播动作「" + 动作名 + "」（" + 动作.length.ToString("0.##")
                + "s），播到 " + (出手进度 * 100f).ToString("0") + "% 放冰柱", this);
    }

    // ============================================================ 主循环

    void Update()
    {
        if (神通 == null || 玩家 == null) { 收尾(); return; }

        // ---- 1) 等动作播到出手进度 → 放冰柱 ----
        if (!已出手)
        {
            float 进度 = 动画 != null ? 动画.动作进度 : 1f;
            bool 还在播 = 动画 != null && 动画.动作播放中;
            if (进度 >= 出手进度 || !还在播) { 出手时动作进度 = 进度; 出手(); return; }
            return;
        }

        // ---- 2) 一段伤害：从玩家往前方扫（位置从0推进到 范围）----
        float 长度 = Mathf.Max(1f, 神通.范围 > 0f ? 神通.范围 : 12f);
        float 时长 = 推进时长 > 0f ? 推进时长 : (神通.持续时长 > 0f ? 神通.持续时长 : 0.9f);
        速度 = 长度 / Mathf.Max(0.15f, 时长);

        float 走过 = Mathf.Min(速度 * (Time.time - 起算时刻), 长度);
        float 步 = Mathf.Max(0.3f, 冰柱宽度 * 0.5f);
        while (已扫描到 <= 走过 && 已扫描到 <= 长度)
        {
            扫一段(已扫描到);
            已扫描到 += 步;
        }
        if (走过 >= 长度) 铺完 = true;

        // ---- 3) 二段 / 三段排期 ----
        float 现在 = Time.time;
        foreach (var 账 in 账本)
        {
            if (账.npc == null) continue;
            if (!账.二段已做 && 现在 >= 账.二段时刻) { 账.二段已做 = true; 二段(账.npc); }
            if (账.二段已做 && !账.三段已做 && 现在 >= 账.三段时刻) { 账.三段已做 = true; 三段(账.npc); }
        }

        bool 账清完 = true;
        foreach (var 账 in 账本) if (!账.三段已做) { 账清完 = false; break; }
        if ((铺完 && 账清完) || Time.time - 起算时刻 >= Mathf.Max(1f, 最长存活)) 收尾();
    }

    /// <summary>
    /// 到 40% 节点：取鼠标方向 → 放**一根** frost-wave（近端在玩家、长度朝前方）→ 开始扫伤害。
    /// </summary>
    void 出手()
    {
        if (已出手) return;
        已出手 = true;
        起算时刻 = Time.time;
        已扫描到 = 0f;
        方向 = 取鼠标方向();

        float 缩放 = Mathf.Max(0.05f, 冰柱缩放);
        float 柱长 = Mathf.Max(1f, 冰柱基准长度 * 缩放);

        // ① 位置：沿朝向挪半个柱长 —— prefab 的柱体是以生成点为中心的，
        //    不挪的话玩家站在柱子中间，而不是「一段在玩家这里」。
        var 位 = 玩家.position + Vector3.up * 冰柱抬高 + 方向 * (柱长 * 0.5f);

        // ② 旋转：让**本地 +X**（实测的长度轴）对准朝向。
        //    LookRotation 让本地 +Z 对准朝向，所以再绕 Y 转 −90 把 +X 换到 +Z 的位置。
        var 旋 = Quaternion.LookRotation(方向, Vector3.up) * Quaternion.Euler(0f, -90f, 0f);

        var go = 特效摆放.生成(冰柱特效路径, 位, 旋.eulerAngles, 缩放,
                              对齐到锚点: false, 存活秒: 特效存活(冰柱特效路径, 3f), 名: "寒墟_冰柱");

        if (打印日志)
            Debug.Log("[寒墟] 出手（动作进度 " + 出手时动作进度.ToString("0.000") + "）：方向 " + 方向.ToString("F2")
                + "，冰柱长 " + 柱长.ToString("0.#") + "m、近端在玩家脚下（生成点 " + 位.ToString("F2") + "）"
                + (go == null ? "★生成失败" : ""), this);
    }

    /// <summary>扫一段：走廊里的敌人各吃一次一段伤害，并记下二三段的时刻</summary>
    void 扫一段(float d)
    {
        var 中心 = 玩家.position + 方向 * d;
        VoxelCombatDamage.Sphere(中心,Mathf.Max(.35f,冰柱宽度*.5f));
        int n = Physics.OverlapSphereNonAlloc(中心, Mathf.Max(0.2f, 冰柱宽度 * 0.5f), 命中缓存,
                                              敌人层, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = 命中缓存[i];
            if (c == null) continue;
            var npc = c.GetComponentInParent<NpcInstance>();
            if (npc == null || npc.IsDead) continue;
            if (!去重.Add(npc)) continue;

            float 伤 = 结算(npc, 神通.伤害倍率, "一段·冰柱");
            一段命中数++;

            float 二段时刻 = Time.time + Mathf.Max(0f, 神通.伤害间隔);
            账本.Add(new 命中账
            {
                npc = npc,
                二段时刻 = 二段时刻,
                三段时刻 = 二段时刻 + 算三段延迟(),
            });

            if (打印日志)
                Debug.Log("[寒墟] 一段·冰柱扫到「" + npc.DisplayName + "」 伤害 " + 伤.ToString("0.##")
                    + "（离玩家 " + d.ToString("0.#") + "m）", npc);
        }
    }

    /// <summary>二段：脚下 frost-ring + 结算</summary>
    void 二段(NpcInstance npc)
    {
        if (npc == null || npc.IsDead) return;
        float 缩 = 算缩放(npc);
        特效摆放.生成(二段特效路径, 脚下(npc), 冰柱旋转欧拉, 缩 * Mathf.Max(0.05f, 二段缩放),
                      对齐到锚点: false, 存活秒: 特效存活(二段特效路径, 3f), 名: "寒墟_冰环");
        float 伤 = 结算(npc, 神通.伤害倍率 * 二段倍率, "二段·冰环");
        二段命中数++;
        if (打印日志)
            Debug.Log("[寒墟] 二段·冰环「" + npc.DisplayName + "」 伤害 " + 伤.ToString("0.##")
                + "（缩放 " + 缩.ToString("0.##") + "）", npc);
    }

    /// <summary>三段：脚下 frost-spike + 结算</summary>
    void 三段(NpcInstance npc)
    {
        if (npc == null || npc.IsDead) return;
        float 缩 = 算缩放(npc);
        特效摆放.生成(三段特效路径, 脚下(npc), 冰柱旋转欧拉, 缩 * Mathf.Max(0.05f, 三段缩放),
                      对齐到锚点: false, 存活秒: 特效存活(三段特效路径, 3f), 名: "寒墟_冰刺");
        float 伤 = 结算(npc, 神通.伤害倍率 * 三段倍率, "三段·冰刺");
        三段命中数++;
        if (打印日志)
            Debug.Log("[寒墟] 三段·冰刺「" + npc.DisplayName + "」 伤害 " + 伤.ToString("0.##"), npc);
    }

    // ============================================================ 工具

    float 结算(NpcInstance npc, float 倍率, string 段名)
    {
        if (战斗属性 == null || npc == null || npc.IsDead) return 0f;
        var 规则 = new AttackSpec(神通.伤害属性, AttackKind.主动神通, false, Mathf.Max(0.01f, 倍率));
        var 目标 = new NpcTarget(npc);
        var 结果 = 目标.受到攻击(战斗属性, 规则, this);
        累计伤害 += 结果.伤害;
        if (打印日志 && 段名 == "一段·冰柱")
            Debug.Log("[寒墟] " + 段名 + "「" + 目标.名字 + "」 " + 结果
                + "（倍率 " + 倍率.ToString("0.##") + "｜" + 神通.伤害属性 + " + 主动神通）", npc);
        return 结果.伤害;
    }

    Vector3 脚下(NpcInstance npc) => npc != null ? npc.transform.position + Vector3.up * 冰柱抬高 : transform.position;

    float 算缩放(NpcInstance npc)
    {
        if (!按体型缩放 || npc == null) return 1f;
        float 高 = 特效摆放.量高度(npc.transform, 基准敌人高度);
        if (高 <= 0.01f) return 1f;
        return Mathf.Clamp(高 / Mathf.Max(0.1f, 基准敌人高度), 缩放下限, 缩放上限);
    }

    /// <summary>三段延迟：填了就用；填 0 = 自动取 frost-ring 自然时长（最多 三段最长延迟）</summary>
    float 算三段延迟()
    {
        if (三段延迟 > 0.01f) return 三段延迟;
        float 环时长 = 特效存活(二段特效路径, 1.5f);
        return Mathf.Clamp(环时长, 0.3f, Mathf.Max(0.3f, 三段最长延迟));
    }

    /// <summary>
    /// 特效存活 = 按 prefab 的自然总时长自动算（**不要写死短秒数**，否则粒子还没淡完就被 Destroy，
    /// 表现就是「忽然消失」——2026-09-29 在冰刺上踩过这个坑）。
    /// </summary>
    float 特效存活(string 路径, float 兜底)
    {
        if (string.IsNullOrEmpty(路径)) return 兜底;
        var prefab = Resources.Load<GameObject>(路径);
        float 自然 = 特效摆放.量特效总时长(prefab, 兜底);
        return Mathf.Max(0.1f, 自然 * 1.05f);
    }

    /// <summary>
    /// 取「鼠标落点」方向：主相机穿过鼠标的射线打到**玩家脚底那层水平面**，
    /// 从玩家指向那个落点（只取水平分量）。取不到（没相机 / 射线平行）就退回角色朝向。
    /// </summary>
    Vector3 取鼠标方向()
    {
        // 调试/摆拍：直接给方向就不看鼠标了
        if (调试方向.sqrMagnitude > 0.0001f)
        {
            var d = 调试方向; d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }

        if (相机 == null) 相机 = Camera.main;
        Vector3 平 = 玩家.forward; 平.y = 0f;

        if (相机 != null)
        {
            var 平面 = new Plane(Vector3.up, new Vector3(0f, 玩家.position.y, 0f));
            var 射线 = 相机.ScreenPointToRay(Input.mousePosition);
            if (平面.Raycast(射线, out float 距离))
            {
                var 落点 = 射线.GetPoint(距离);
                var 向 = 落点 - 玩家.position; 向.y = 0f;
                if (向.sqrMagnitude > 0.04f) 平 = 向;
            }
        }

        if (平.sqrMagnitude < 0.0001f) 平 = Vector3.forward;
        return 平.normalized;
    }

    void 收尾()
    {
        if (打印日志)
            Debug.Log("[寒墟] 结束：一段 " + 一段命中数 + " 次 / 二段 " + 二段命中数
                + " 次 / 三段 " + 三段命中数 + " 次，合计伤害 " + 累计伤害.ToString("0.##"), this);
        Destroy(gameObject);
    }

    // ---- ASCII 别名 ----
    public int HitStage1 => 一段命中数;
    public int HitStage2 => 二段命中数;
    public int HitStage3 => 三段命中数;
    public float TotalDamage => 累计伤害;
}
