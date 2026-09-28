using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「闪烁位移」型主动神通的执行体（目前 = 雷动千闪）。
///
/// 由 <see cref="ActiveSkillCaster"/> 在施放时挂到一个临时物件上，跑完自己销毁。
///
/// ## 策划说明（2026-09-28）
/// 主角朝**鼠标方向**（只是方向）闪烁位移；如果所指方向超出**神识**，最远只闪到神识边缘。
/// 在**原地**留一个闪电特效（<see cref="起点特效路径"/>）、
/// 在**落点**再留一个（<see cref="终点特效路径"/>），两个特效停留一下下就消失，
/// 并对**当时在那里的敌人**造成特殊属性的**主动神通**伤害。
///
/// ## 为什么位移要「关掉 CharacterController 再挪」
/// 和 <c>Teleporter.放下玩家</c> 同一个坑：直接写 transform.position 会被
/// CharacterController 当成"卡进墙里"再弹出来。所以先禁用、挪完再启用。
///
/// ## 落点安全
/// 只做**一个简单的地面探测**（从落点上空往下打），够不着就退回原地 —— 
/// 免得闪进地图外或掉进虚空。真正精细的寻路/贴墙处理不在本次范围。
///
/// ⚠️ **但御风 / 骑乘时要另算**：那两种状态下玩家本来就在空中（御风 2.4m、
/// 坐骑最高 5.64m），绝不能把落点贴到地面，否则会看到"闪一下就落地、再飞起来"。
/// 见 <see cref="在空中"/> / <see cref="算空中落点"/>。
/// </summary>
public class BlinkSkillRunner : MonoBehaviour
{
    ActiveDivineAbility 神通;
    PlayerCombatStats 战斗属性;
    PlayerVitals 生命;
    Camera 相机;
    Transform 玩家;
    LayerMask 敌人层;

    Vector3 起点;
    Vector3 落点;
    float 起算时刻;
    float 描边半径;
    bool 已放终点特效;
    float 下次结算时刻;

    readonly HashSet<NpcInstance> 已打过的 = new HashSet<NpcInstance>();

    /// <summary>打到的敌人总数（调试用）</summary>
    public int 命中数 { get; private set; }
    /// <summary>累计伤害（调试用）</summary>
    public float 累计伤害 { get; private set; }

    /// <summary>起点特效资源路径（留空则用神通自己的特效）</summary>
    public string 起点特效路径 = "";
    /// <summary>终点特效资源路径</summary>
    public string 终点特效路径 = "";

    [Tooltip("特效播多久后销毁（秒）")]
    public float 特效存活 = 1.2f;

    [Tooltip("两个特效各自相对地面的抬高（米）")]
    public float 特效抬高 = 0f;

    [Tooltip("★ 两个特效的**生成旋转**（欧拉角）。\n\n" +
             "默认 (0,0,0)。如果 `lightning-impact` / `lightning-arc-flash` 出现躺平或倒立，" +
             "在这里填 −90,0,0（和玄霄雷决的 `lightning-ray` 同一个值），或 +90,0,0。")]
    public Vector3 特效旋转欧拉 = Vector3.zero;

    [Tooltip("勾上 = 把特效的**几何中心**对齐到落点（默认开）。\n\n" +
             "资源包的 prefab 部件常常不在原点 —— 实测 `lightning-impact` 的 8 个部件\n" +
             "全在本地 z = 1.68，直接生成会整蓬飘出去 1.68m。\n" +
             "关掉就退回「原样摆在锚点」。")]
    public bool 对齐脚下 = true;

    [Tooltip("最大闪烁距离上限（米），防止神识堆高后闪太远")]
    public float 最大闪烁距离 = 30f;

    [Tooltip("执行体自己的存活上限（秒）。超过就强制结束，防止卡住")]
    public float 最长存活 = 3f;

    [Tooltip("打印日志")]
    public bool 打印日志 = true;

    [Tooltip("打印落点计算的详细诊断（排查闪不动这类问题用）")]
    public bool 打印诊断 = false;

    public void 初始化(ActiveDivineAbility 神通, PlayerCombatStats 战斗属性, PlayerVitals 生命,
                        Camera 相机, Transform 玩家, LayerMask 敌人层,
                        string 起点特效, string 终点特效, float 特效存活秒 = 1.2f)
    {
        this.神通 = 神通;
        this.战斗属性 = 战斗属性;
        this.生命 = 生命;
        this.相机 = 相机 != null ? 相机 : Camera.main;
        this.玩家 = 玩家;
        this.敌人层 = 敌人层;
        起点特效路径 = 起点特效;
        终点特效路径 = 终点特效;
        特效存活 = Mathf.Max(0.1f, 特效存活秒);
    }

    void Start()
    {
        if (玩家 == null || 神通 == null) { Destroy(gameObject); return; }

        起点 = 玩家.position;
        描边半径 = Mathf.Max(0.1f, 神通.范围);
        起算时刻 = Time.unscaledTime;
        下次结算时刻 = Time.unscaledTime;

        // 1) 原地先留一蓬
        放特效(起点特效路径, 起点, "雷动千闪_起点");

        // 2) 算落点并位移
        落点 = 算落点();
        位移玩家(落点);

        // 3) 落点再留一蓬
        放特效(终点特效路径, 落点, "雷动千闪_落点");
        已放终点特效 = true;

        if (打印日志)
            Debug.Log("[雷动千闪] 起点 " + 起点.ToString("F2") + " → 落点 " + 落点.ToString("F2")
                + "（距离 " + Vector3.Distance(起点, 落点).ToString("F2") + "m｜半径 " + 描边半径.ToString("0.##") + "m）", this);
    }

    void Update()
    {
        if (已放终点特效 && Time.unscaledTime >= 下次结算时刻) 结算一次();

        // 结算窗口 = 神通的持续时长（策划填 0.6s：那两个特效"停留一下下"的时长）
        float 窗口 = 神通 != null ? Mathf.Max(0.05f, 神通.持续时长) : 0.6f;
        float 已过 = Time.unscaledTime - 起算时刻;

        // 到窗口就正常收尾；另外给一个兜底上限，防止窗口被配得特别大时一直挂着
        if (已过 >= 窗口 || 已过 >= Mathf.Max(0.1f, 最长存活)) 收尾();
    }

    void 收尾()
    {
        if (打印日志)
            Debug.Log("[雷动千闪] 结束：命中 " + 命中数 + " 个敌人，合计 " + 累计伤害.ToString("0.##"), this);
        Destroy(gameObject);
    }

    // ============================================================ 落点

    /// <summary>
    /// 落点 = 玩家 + 鼠标方向 × 闪烁距离。
    /// 闪烁距离 = min(神识范围, 鼠标点到玩家的距离, 最大闪烁距离)。
    /// </summary>
    Vector3 算落点()
    {
        Vector3 方向 = 鼠标水平方向();
        float 神识距离 = 神识范围();

        // 玩家"想闪多远" = 鼠标所指的水平距离。取不到（鼠标在窗口外/打不到平面）时按神识满距闪
        float 想要 = 神识距离;
        if (相机 != null && 鼠标在画面内())
        {
            var ray = 相机.ScreenPointToRay(Input.mousePosition);
            var 平面 = new Plane(Vector3.up, new Vector3(0f, 玩家.position.y, 0f));
            if (平面.Raycast(ray, out float 距))
            {
                var 平 = ray.GetPoint(距) - 玩家.position;
                平.y = 0f;
                想要 = 平.magnitude;
            }
        }

        float 距离 = Mathf.Clamp(想要, 0f, Mathf.Min(神识距离, 最大闪烁距离));
        var 目标 = 玩家.position + 方向 * 距离;

        // 地面探测：落点上方往下打，打不到就原地不动（别闪进虚空）
        var 落点地面 = 探落点地面(目标);
        if (打印诊断)
            Debug.Log("[雷动千闪] 落点计算：鼠标=" + Input.mousePosition
                + " 方向=" + 方向.ToString("F2") + " 想要=" + 想要.ToString("F2")
                + " 神识=" + 神识距离.ToString("F2") + " 实取=" + 距离.ToString("F2")
                + " 目标=" + 目标.ToString("F2")
                + " 落点地面=" + (落点地面.HasValue ? 落点地面.Value.ToString("F2") : "无")
                + "｜空中(" + 在空中() + ") 当前y=" + 玩家.position.y.ToString("F2"), this);

        if (在空中()) { 是空中位移 = true; return 算空中落点(目标); }
        是空中位移 = false;

        if (落点地面.HasValue)
        {
            目标.y = 落点地面.Value + 0.05f;
            return 目标;
        }

        if (打印诊断) Debug.LogWarning("[雷动千闪] 落点下方没有地面 → 原地不动", this);
        return 玩家.position;
    }

    /// <summary>
    /// 探一个落点正下方**最高的那个落脚面**（世界 y）。探不到返回 null。
    ///
    /// ⚠️ 必须**从高处往下打**，不能从"玩家当前高度"往下打：
    /// 落点处如果有一堵比自己高的墙 / 屋顶，起点就落在它**内部**了，
    /// 而 Physics 默认不命中背面（`queriesHitBackfaces = false`），
    /// 射线会直接穿过去打到地面 —— 实测 `WallSeg_17`（box 高 4m）就是这样：
    /// 从 y=3 往下打只命中 y=0 的地面，从 y=10 往下打才命中 y=4.00 的墙顶。
    ///
    /// `RaycastAll` **不保证有序**，所以要遍历取最高，不能取第一个。
    /// </summary>
    float? 探落点地面(Vector3 落点)
    {
        var 命中s = Physics.RaycastAll(落点 + Vector3.up * 30f, Vector3.down, 80f,
            ~0, QueryTriggerInteraction.Ignore);

        float 最高 = float.NegativeInfinity;
        foreach (var h in 命中s)
        {
            // 别把玩家自己当成地形
            if (h.collider != null && h.collider.GetComponentInParent<PlayerVitals>() != null) continue;
            if (h.point.y > 最高) 最高 = h.point.y;
        }
        return float.IsNegativeInfinity(最高) ? (float?)null : 最高;
    }

    // ---------------------------------------------------------------- 空中位移（御风 / 骑乘）

    /// <summary>
    /// 玩家现在是不是**在空中**（御风悬浮中 / 坐骑上）。
    ///
    /// ★ 为什么必须单独判（2026-09-28 用户报）：
    /// 「骑乘坐骑或者御风时使用雷动千闪会回到地面上然后再次飞起」——
    /// 原因是 <see cref="算落点"/> 最后无脑把落点的 y 贴到地面，
    /// 而御风悬浮在 2.4m、坐骑最高 5.64m，闪一下就被摔到地上，
    /// 飞行逻辑再把人拉回原高度，于是看到"落地又飞起"。
    /// </summary>
    bool 在空中()
    {
        if (玩家 == null) return false;

        var 坐骑 = 玩家.GetComponent<MountRider>();
        if (坐骑 != null && 坐骑.骑乘中) return true;

        var 御风 = 玩家.GetComponent<YufengFlight>();
        if (御风 != null && 御风.御风流程中) return true;

        return false;
    }

    /// <summary>
    /// 空中的落点：**保持玩家当前高度**，只做水平位移。
    ///
    /// 唯一允许的竖直变化：落点下方有**比玩家更高**的地形（山脊 / 屋顶）时抬上去，
    /// 免得横穿山体；否则原高度不动 —— **绝不下沉**。
    /// </summary>
    Vector3 算空中落点(Vector3 目标)
    {
        float 原高 = 玩家.position.y;
        var 落点地面 = 探落点地面(目标);

        目标.y = (落点地面.HasValue && 落点地面.Value > 原高 + 0.05f)
            ? 落点地面.Value + 0.05f      // 顶到更高的地形 → 抬上去
            : 原高;                       // 平地 / 探不到 → 原地维持

        if (打印诊断)
            Debug.Log("[雷动千闪] 空中位移：保持高度 " + 原高.ToString("F2")
                + " → 落点 " + 目标.ToString("F2")
                + "（落点地面 " + (落点地面.HasValue ? 落点地面.Value.ToString("F2") : "无")
                + "，竖直变化 " + (目标.y - 原高).ToString("F2") + "m）", this);
        return 目标;
    }

    /// <summary>
    /// 鼠标是否落在游戏画面内。
    ///
    /// ★ 为什么要判这个（2026-09-28 实测踩到）：鼠标被拖出游戏窗口时
    /// `Input.mousePosition` 会是**窗口外的坐标**（实测拿到 -2399），
    /// 于是 `ScreenPointToRay` 打出来的方向毫无意义 —— 按技能会朝莫名其妙的方向闪，
    /// 甚至算出落点没有地面而"原地不动"。
    /// 鼠标不在画面内时退回**角色正前方**，行为可预期。
    /// </summary>
    bool 鼠标在画面内()
    {
        var m = Input.mousePosition;
        return m.x >= 0f && m.x <= Screen.width && m.y >= 0f && m.y <= Screen.height;
    }

    /// <summary>鼠标所指的水平方向（不在画面内 / 打不到平面时退回角色正前方）</summary>
    Vector3 鼠标水平方向()
    {
        var f = 玩家 != null ? 玩家.forward : Vector3.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.0001f) f = Vector3.forward;
        if (相机 == null || !鼠标在画面内()) return f.normalized;

        var ray = 相机.ScreenPointToRay(Input.mousePosition);
        var 平面 = new Plane(Vector3.up, new Vector3(0f, 玩家.position.y, 0f));
        if (!平面.Raycast(ray, out float 距)) return f.normalized;

        var 平 = ray.GetPoint(距) - 玩家.position;
        平.y = 0f;
        return 平.sqrMagnitude > 0.0001f ? 平.normalized : f.normalized;
    }

    /// <summary>
    /// 神识范围（米）= 基础 4 + 神识 × 0.8，封顶 40。
    /// 玩家侧统一走 <see cref="PlayerCombatStats.算神识范围"/> —— 那个式子
    /// 在本工程里已经有三份副本了，别再加第四份。
    /// </summary>
    float 神识范围() => PlayerCombatStats.算神识范围(战斗属性, 4f, 0.8f, 40f);

    void 位移玩家(Vector3 到)
    {
        var cc = 玩家.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        玩家.position = 到;
        if (cc != null) cc.enabled = true;
    }

    // ============================================================ 结算

    /// <summary>对起点和落点各结算一次（同一敌人只打一次）</summary>
    void 结算一次()
    {
        if (战斗属性 == null) { 收尾(); return; }

        var 规则 = new AttackSpec(神通.伤害属性, AttackKind.主动神通, false, 神通.伤害倍率);
        打一处(起点, 规则);
        打一处(落点, 规则);
        下次结算时刻 = float.MaxValue;      // 一次性结算
    }

    void 打一处(Vector3 中心, AttackSpec 规则)
    {
        var 命中s = Physics.OverlapSphere(中心, 描边半径, 敌人层, QueryTriggerInteraction.Ignore);
        var 去过重 = new HashSet<NpcInstance>();

        foreach (var c in 命中s)
        {
            if (c == null) continue;
            var npc = c.GetComponentInParent<NpcInstance>();
            if (npc == null || npc.IsDead) continue;
            if (!去过重.Add(npc)) continue;
            if (!已打过的.Add(npc)) continue;          // 起落两处都覆盖到时只打一次

            var 目标 = new NpcTarget(npc);
            var 结果 = 目标.受到攻击(战斗属性, 规则, this);
            命中数++;
            累计伤害 += 结果.伤害;

            if (打印日志)
                Debug.Log("[雷动千闪] 命中「" + 目标.名字 + "」 " + 结果, npc);
        }
    }

    // ============================================================ 特效

    void 放特效(string 路径, Vector3 位, string 名)
    {
        // 统一走「特效摆放」：旋转 + 开等比缩放 + 几何中心对齐到脚下。
        // 竖直就放锚点（用户要求「对齐角色的脚下」）；想微调上下用 特效抬高。
        特效摆放.生成(路径, 位 + Vector3.up * 特效抬高, 特效旋转欧拉,
                      1f, 对齐到锚点: 对齐脚下, 存活秒: 特效存活, 名: 名);
    }

    // ---- ASCII 别名 ----
    public int HitCount => 命中数;
    public float TotalDamage => 累计伤害;

    /// <summary>起点 / 落点世界坐标（施放后就位；调试与自动化验证用）</summary>
    public Vector3 起点位 => 起点;
    public Vector3 落点位 => 落点;
    /// <summary>这次是不是空中位移（御风 / 骑乘）</summary>
    public bool 是空中位移 { get; private set; }

    // ---- ASCII 别名 ----
    public Vector3 StartPosition => 起点;
    public Vector3 LandingPosition => 落点;
    public bool IsAirborne => 在空中();
}
