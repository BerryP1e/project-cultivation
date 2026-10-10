using System.Collections.Generic;
using UnityEngine;

/// <summary>神识范围圈内按间隔结算特殊/被动伤害，在命中节点生成原生竖直 lightning-ray。由 PlayerAbilityLoader 内置注册。共享依赖位于 Art/VFX/Shared。</summary>
[DisallowMultipleComponent]
public class QianJieLeiYu : MonoBehaviour
{
    // ============================================================ 配置

    [Header("引用（留空自动找）")]
    [Tooltip("玩家属性（算神识范围、当攻击方）。留空自动在本体找")]
    public PlayerCombatStats 战斗属性;

    [Tooltip("灵气（持续消耗 / 耗尽自动关闭）。留空自动在本体找")]
    public PlayerVitals 灵气;

    [Tooltip("玩家锁定管理器（选目标用，暂时只做日志）。留空自动找")]
    public NpcTargeting 目标管理器;

    [Header("触发条件")]
    [Tooltip("启用这个被动后光环是否**常驻**（用户要求：装备了就维持）")]
    public bool 常驻 = true;

    [Tooltip("灵力耗尽时自动关闭，灵力回满后自动重开")]
    public bool 灵力耗尽自动关闭 = true;

    [Header("范围（跟神识走）")]
    [Tooltip("神通表里没填「维持消耗灵力」时，用这个兜底（点/秒）")]
    public float 每秒消耗灵力兜底 = 1f;

    [Tooltip("神识为 0 时的基础半径（米）")]
    public float 基础半径 = 4f;

    [Tooltip("每 1 点神识增加多少米")]
    public float 每点神识半径 = 0.8f;

    [Tooltip("半径上限（米）")]
    public float 半径上限 = 40f;

    [Tooltip("勾上 = 特效按【范围 ÷ 特效基准半径】缩放（用户要求『大小随范围变化』）")]
    public bool 特效随范围缩放 = true;

    [Tooltip("★ 光环特效在 scale = 1 时的半径（米）。**换特效要重新标定。**\n\n" +
             "用户 2026-09-28 指定：**以 `black-border-dust` 这个组件的尺寸作为缩放标杆**，\n" +
             "让它的半径 = 技能实际作用半径（这样视觉边界和伤害范围对齐）。\n\n" +
             "实测（scale 1、相对锚点的水平半径）：\n" +
             "  black-border-dust       4.727  ← 取这个当基准 ★\n" +
             "  black-border-particles  4.309\n" +
             "  black-border-tint       5.001\n" +
             "  zaps / lightnings-circles  10.8 / 11.4（电弧会甩到黑圈外面，属正常）")]
    public float 特效基准半径 = 4.727f;

    [Tooltip("★ 光环的**竖直对齐方式**。\n\n" +
             "实测 `lightning-arc-black-ring` 的黑圈部件在 scale 下会占很大的竖直范围：\n" +
             "scale 2.962 时相对锚点 **y −5.5 ~ +8.1**（也就是顶到 +8m）。\n" +
             "如果按「几何中心」摆在玩家脚下，圈的上半部分就会抬到 8m 高，\n" +
             "**把敌人的血条和选中环挡住**（用户 2026-09-28 报的）。\n\n" +
             "勾上（默认）= 让光环的**最低点贴住 `光环抬高`**（黑圈贴地）；\n" +
             "关掉 = 退回「几何中心对齐」。")]
    public bool 光环贴地 = true;

    [Tooltip("光环**发射器节点**离地高度（米）。配合 光环贴地 用。\n\n" +
             "⚠️ 注意这里量的是「节点 / 发射器」的高度，不是「粒子包围盒」的高度 ——\n" +
             "实测 `black-border-dust` 节点的粒子 `startSize` 很大，粒子会从节点\n" +
             "**向下延伸好几米**（scale 2.962 时包围盒最低点比节点低约 6.8m）。\n" +
             "所以想让整圈都露出来，这个值要给得比想象的更积极。\n\n" +
             "用户 2026-09-28：「稍微往上抬一点，现在看不到完整的特效」")]
    public float 光环抬高 = 1.5f;

    [Tooltip("★ 光环里要**去掉的部件名**（按名字找，可填多个）。删的是**整棵子树**。\n\n" +
             "默认去掉 `zaps` —— 它是那根会往外甩的**电弧**，用户 2026-09-28 明确不要：\n" +
             "「我就是不想要那个电弧了」。\n\n" +
             "⚠️ 层级关系（`zaps` 是 `flames` 的子物件，电弧的整棵子树都在它下面）：\n" +
             "```\n" +
             "black-border-tint\n" +
             "  ├─ flames              ← 只是个空壳容器，本身也是粒子\n" +
             "  │   └─ zaps            ← 去掉这个（电弧，含下面一串）\n" +
             "  │       ├─ lightnings-circles\n" +
             "  │       └─ rays/flashes\n" +
             "  └─ black-border-dust   ← 黑圈（保留，它是范围标杆）\n" +
             "```\n" +
             "删 `zaps` 会**连同它的子物件一起删**，这正是要的效果。\n" +
             "清空这个数组 = 保留全部部件。")]
    public string[] 光环去掉的部件 = new string[] { "zaps" };

    [Header("骑乘时的特殊处理")]
    [Tooltip("★ 装备**骑乘类坐骑**时，光环对齐**坐骑的根节点**而不是角色（用户 2026-09-28 要求）\n" +
             "—— 人骑在坐骑背上，贴角色脚下的话圈会穿过坐骑身体。\n\n" +
             "**这里填「不贴坐骑」的例外坐骑id**（它们不是「骑着」的那种）：\n" +
             "  · `mount_shenlong_01` 上古神龙 —— 环身特效，本来就围在角色周围\n" +
             "  · `mount_chibang_01`  灵翅     —— 长在背上的翅膀\n" +
             "其余坐骑（狼/鹿/熊猫/麒麟/灵鹤…）都会自动贴坐骑根。")]
    public string[] 不贴坐骑的坐骑id = new string[]
    {
        "mount_shenlong_01",
        "mount_chibang_01",
    };

    [Header("伤害")]
    [Tooltip("伤害属性。策划要求「特殊」")]
    public DamageNature 伤害属性 = DamageNature.特殊;

    [Tooltip("技能倍率")]
    public float 技能倍率 = 1f;

    [Tooltip("每隔多少秒判一次伤害。\n" +
             "用户 2026-09-28：*「频率太快了一点」* → 从 1s 调慢到 1.5s。")]
    public float 伤害间隔 = 1.5f;

    [Tooltip("敌人所在层")]
    public LayerMask 敌人层 = ~0;

    [Header("特效")]
    [Tooltip("光环特效（以角色为中心）。路径相对 Assets/resources、不带扩展名")]
    public string 光环特效路径 =
        "CombatVFX/CombatMagic/lightning-fx/lightning-arc-black-ring";

    [Tooltip("雷罚特效（出现在每个敌人脚下）")]
    public string 雷罚特效路径 =
        "CombatVFX/CombatMagic/lightning-fx/lightning-ray";

    [Tooltip("★ 两个特效的**生成旋转**（欧拉角）。\n\n" +
             "**默认 (-90, 0, 0)** —— 用户 2026-09-28 实测确认：不转是躺平的，" +
             "绕 X 转 −90 才是对的（和玄霄雷决的 `lightning-ray` 同一个值）。\n" +
             "光环和雷罚都要转。")]
    public Vector3 特效旋转欧拉 = new Vector3(-90f, 0f, 0f);

    [Tooltip("★ 雷罚的表现方式。\n\n" +
             "勾上（默认）= **常驻**：敌人只要在范围内，脚下就**一直**有雷罚，\n" +
             "离开范围 / 死亡才收掉。用户 2026-09-28 明确要求：\n" +
             "「那个雷罚特效不是消失又出现的，而是一直出现在受影响的单位的脚下」。\n\n" +
             "关掉 = 旧行为（每次结算现放一个、播完自己消失），会看到一闪一闪。")]
    public bool 雷罚常驻 = true;

    [Tooltip("雷罚特效的存活时间（秒）。**只在 雷罚常驻 = false 时有用**")]
    public float 雷罚存活 = 1.1f;

    [Tooltip("雷罚特效在同一敌人身上的**重放间隔**（秒）。≤0 = 每次伤害都重放")]
    public float 雷罚重放间隔 = 1.5f;

    [Tooltip("雷罚在敌人这么高时 scale = 1。按敌人模型高度等比缩放。\n" +
             "用户 2026-09-28：*「雷罚的特效可能还是太大了一点」* → 从 1.8 调大到 2.4（越大约小）。\n" +
             "木桩高 1.6m → scale = 1.6 ÷ 2.4 = 0.67")]
    public float 雷罚基准敌人高度 = 2.4f;

    [Tooltip("雷罚缩放下限")]
    public float 雷罚缩放下限 = 0.5f;

    [Tooltip("雷罚缩放上限")]
    public float 雷罚缩放上限 = 3f;

    [Tooltip("雷罚相对敌人脚下（根节点）的抬高（米）。**可以是负数**。\n\n" +
             "用户 2026-09-28 手调了其中一个雷罚，反推出来的值是 **−0.18**：\n" +
             "雷罚的「几何中心」会落到脚下 −0.18m，部件实际覆盖 0.06~0.30m ——\n" +
             "也就是薄薄一圈贴在敌人脚背上，观感最好。")]
    public float 雷罚抬高 = -0.18f;

    [Tooltip("生成特效时把粒子系统改成 scalingMode=Hierarchy（否则 transform 缩放不放大粒子）")]
    public bool 开等比缩放 = true;

    [Header("调试")]
    [Tooltip("打印日志")]
    public bool 打印日志 = true;

    [Tooltip("打印光环对齐的详细诊断（排查「位置不对」用）")]
    public bool 打印诊断 = false;

    [Tooltip("在 Scene 视图画出范围圈")]
    public bool 画范围线 = true;

    // ============================================================ 运行时

    /// <summary>当前生效半径（米）= 基础 + 神识 × 每点，封顶</summary>
    public float 半径 => PlayerCombatStats.算神识范围(战斗属性, 基础半径, 每点神识半径, 半径上限);

    /// <summary>现在是不是开着（灵力够 + 启用了）</summary>
    public bool 生效中 { get; private set; }

    /// <summary>累计造成的伤害（调试 / 自动化验证用）</summary>
    public float 累计伤害 { get; private set; }

    /// <summary>累计命中次数</summary>
    public int 命中次数 { get; private set; }

    GameObject 光环;
    float 下次伤害时刻;

    /// <summary>每个敌人脚下那个**常驻**雷罚物件（雷罚常驻 模式用）</summary>
    readonly Dictionary<NpcInstance, GameObject> 雷罚实例 = new Dictionary<NpcInstance, GameObject>();

    /// <summary>非常驻模式：每个敌人上一次放雷罚的时刻（防刷屏）</summary>
    readonly Dictionary<NpcInstance, float> 上次雷罚 = new Dictionary<NpcInstance, float>();
    readonly List<NpcInstance> 待清理 = new List<NpcInstance>();

    // ============================================================ 生命周期

    void Awake() { 解析引用(); }
    void OnEnable()
    {
        解析引用();
        下次伤害时刻 = Time.time;          // 一开就打一次，别等一个间隔
        if (常驻) 开启();
    }

    void OnDisable() { 关闭(); }

    void OnDestroy() { 关闭(); }

    void 解析引用()
    {
        if (战斗属性 == null) 战斗属性 = GetComponent<PlayerCombatStats>();
        if (灵气 == null) 灵气 = GetComponent<PlayerVitals>();
        if (目标管理器 == null) 目标管理器 = GetComponent<NpcTargeting>();
    }

    void Update()
    {
        解析引用();

        // 灵气不够 → 熄火；回满了 → 自己亮起来
        if (灵力耗尽自动关闭 && 灵气 != null)
        {
            if (生效中 && !灵气.有灵气) { 关闭(); return; }
            if (!生效中 && 常驻 && 灵气.有灵气) 开启();
        }
        else if (常驻 && !生效中) 开启();

        if (!生效中) return;

        // ---- 持续消耗灵力 ----
        float 每秒 = 每秒消耗灵力;
        if (灵气 != null && 每秒 > 0f)
        {
            float cost = 每秒 * Time.deltaTime;
            if (cost > 0f)
            {
                灵气.扣灵气直到零(cost);
                if (!灵气.有灵气 && 灵力耗尽自动关闭) { 关闭(); return; }
            }
        }

        // ---- 光环跟着角色走 + 跟着范围缩放 ----
        维护光环();

        // ---- 雷罚：常驻模式下每帧维护"谁脚下该有"（敌人会动，要跟着） ----
        if (雷罚常驻 && !使用落雷) 维护雷罚();

        // ---- 到点结算 ----
        if (Time.time >= 下次伤害时刻)
        {
            下次伤害时刻 = Time.time + Mathf.Max(0.05f, 伤害间隔);
            结算一次();
        }
    }

    // ============================================================ 开关

    /// <summary>每秒消耗的灵力：优先用神通表里的「维持消耗灵力」</summary>
    public float 每秒消耗灵力
    {
        get
        {
            var a = 取神通();
            return a != null && a.维持消耗灵力 > 0f ? a.维持消耗灵力 : 每秒消耗灵力兜底;
        }
    }

    PassiveDivineAbility 神通缓存;

    /// <summary>表里那条【千劫雷狱】被动（按 <see cref="神通id"/> 找）</summary>
    public PassiveDivineAbility 取神通()
    {
        if (神通缓存 != null) return 神通缓存;
        var 面板 = 面板数据;
        if (面板 == null || 面板.神通 == null) return null;
        foreach (var a in 面板.神通)
        {
            var p = a as PassiveDivineAbility;
            if (p != null && p.神通id == 神通id) { 神通缓存 = p; return p; }
        }
        return null;
    }

    [Tooltip("千劫雷狱在被动神通表里的 id")]
    public string 神通id = "ability_qianjie_leiyu";

    UIPanelData _面板;
    UIPanelData 面板数据
    {
        get
        {
            if (_面板 == null) _面板 = FindObjectOfType<UIPanelData>();
            return _面板;
        }
    }

    /// <summary>开启光环（生成特效）</summary>
    public void 开启()
    {
        if (生效中) return;
        生效中 = true;
        下次伤害时刻 = Time.time;      // 开启瞬间就打一次
        生成光环();

        if (打印日志)
            Debug.Log("[千劫雷狱] 开启：半径 " + 半径.ToString("0.##") + "m、每 "
                + 伤害间隔.ToString("0.##") + "s 结算、每秒耗灵 " + 每秒消耗灵力.ToString("0.##"), this);
    }

    /// <summary>关闭光环（收掉特效）</summary>
    public void 关闭()
    {
        if (!生效中) return;
        生效中 = false;
        if (光环 != null) Destroy(光环);
        光环 = null;

        // 常驻的雷罚也要一起收掉，否则会留在场上
        foreach (var kv in 雷罚实例) if (kv.Value != null) Destroy(kv.Value);
        雷罚实例.Clear();
        上次雷罚.Clear();

        if (打印日志) Debug.Log("[千劫雷狱] 关闭（累计 " + 命中次数 + " 次命中、" + 累计伤害.ToString("0.##") + " 伤害）", this);
    }

    // ============================================================ 光环

    /// <summary>
    /// 光环现在该挂在谁身上。
    ///
    /// **特殊情况（用户 2026-09-28）**：装备**骑乘类坐骑**时，光环要对齐**坐骑的根节点**、
    /// 而不是角色的根节点 —— 因为人是骑在坐骑背上的，贴角色脚下的话圈会穿过坐骑的身体。
    ///
    /// 例外：**上古神龙 / 灵翅** 这两种不是"骑着"的
    /// （神龙是环身特效、灵翅是长在背上的翅膀），仍然对齐角色。
    /// 例外的 id 在 <see cref="不贴坐骑的坐骑id"/> 里配。
    /// </summary>
    public Transform 锚点
    {
        get
        {
            var 骑 = 取坐骑组件();
            if (骑 == null || !骑.骑乘中 || 骑.坐骑实例 == null) return transform;
            if (骑.坐骑定义 == null || string.IsNullOrEmpty(骑.坐骑定义.坐骑id)) return transform;

            string id = 骑.坐骑定义.坐骑id;
            if (不贴坐骑的坐骑id != null)
                foreach (var x in 不贴坐骑的坐骑id)
                    if (!string.IsNullOrEmpty(x) && x == id) return transform;   // 这类对齐角色

            return 骑.坐骑实例.transform;      // 骑乘类坐骑 → 对齐坐骑根
        }
    }

    MountRider _坐骑;
    MountRider 取坐骑组件()
    {
        if (_坐骑 == null) _坐骑 = GetComponent<MountRider>();
        return _坐骑;
    }

    void 生成光环()
    {
        if (string.IsNullOrEmpty(光环特效路径)) return;

        // 用「一次性特效摆放」统一处理：旋转 + 开等比缩放
        // 对齐单独做（要按 光环贴地 决定对齐"最低点"还是"几何中心"）
        光环 = 特效摆放.生成(光环特效路径, 锚点.position, 特效旋转欧拉,
                            放大(), 对齐到锚点: false, 存活秒: 0f, 名: "千劫雷狱_光环");

        if (光环 == null)
            Debug.LogWarning("[千劫雷狱] 光环生成失败（看上面的「找不到特效」提示）", this);
        else
            去掉光环部件(光环);

        应用光环缩放();
    }

    /// <summary>
    /// 按 <see cref="光环去掉的部件"/> 删掉光环里的某些部件（**整棵子树一起删**）。
    ///
    /// 用户 2026-09-28 要的是"不要那根电弧"，而电弧是 `flames → zaps → …` 一整条，
    /// 所以这里就是**直接删节点**，连子物件一起带走。
    ///
    /// ⚠️ 曾经写成"只删节点自己、把子物件提到上一层" —— 那是为了"删 flames 但保住 zaps"，
    /// 但用户根本不要 zaps，结果白绕一圈还留了电弧。**别自作聪明保子物件。**
    /// </summary>
    void 去掉光环部件(GameObject go)
    {
        if (光环去掉的部件 == null || 光环去掉的部件.Length == 0) return;

        foreach (var 名 in 光环去掉的部件)
        {
            if (string.IsNullOrEmpty(名)) continue;

            // 先收集再删（避免边遍历边改结构）
            var 待删 = new List<Transform>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == 名) 待删.Add(t);

            foreach (var t in 待删)
            {
                if (t == null) continue;
                if (t.parent == null) continue;      // 连根都要删就跳过，别把整个光环拆了

                int 子树 = t.GetComponentsInChildren<Transform>(true).Length;
                if (打印日志)
                    Debug.Log("[千劫雷狱] 去掉光环部件「" + 名 + "」（含子树共 " + 子树 + " 个物件）", this);
                Destroy(t.gameObject, 0f);          // 延迟到帧末销毁，避免同帧里还在遍历
            }
        }
    }

    void 维护光环()
    {
        if (光环 == null) { 生成光环(); return; }
        应用光环缩放();
    }

    /// <summary>光环大小 = 范围 ÷ 特效基准半径（用户要求「大小随范围变化」）</summary>
    float 放大()
    {
        if (!特效随范围缩放) return 1f;
        float k = 特效基准半径 > 0.01f ? 半径 / 特效基准半径 : 1f;
        return Mathf.Max(0.05f, k);
    }

    /// <summary>
    /// 应用缩放并重新对齐到锚点。
    ///
    /// 缩放会改变部件的世界位置，所以**每次都要重新对齐**。
    ///
    /// 对齐方式见 <see cref="光环贴地"/>：
    ///   · 贴地 → 让整棵树的最低点落在 锚点.y + 光环抬高（黑圈贴地、不挡血条）
    ///   · 否则 → 几何中心对齐（旧行为）
    /// </summary>
    void 应用光环缩放()
    {
        if (光环 == null) return;

        var 根 = 锚点;
        Vector3 锚位 = 根 != null ? 根.position : transform.position;

        光环.transform.localScale = Vector3.one * 放大();

        if (!光环贴地) { 特效摆放.对齐到(光环, 锚位); return; }

        // —— 贴地：水平按中心对齐，竖直把**最低点**放到锚点 + 抬高 ——
        if (!特效摆放.量部件范围(光环, out Bounds 范围)) return;
        var t = 光环.transform;
        float 旧Y = t.position.y;
        float 新Y = 旧Y + (锚位.y + 光环抬高 - 范围.min.y);
        t.position = new Vector3(
            t.position.x + (锚位.x - 范围.center.x),
            新Y,
            t.position.z + (锚位.z - 范围.center.z));

        if (打印诊断)
            Debug.Log("[千劫雷狱] 光环对齐：scale=" + 光环.transform.localScale.x.ToString("0.###")
                + " 锚位=" + 锚位.ToString("F2")
                + " 部件范围y " + 范围.min.y.ToString("F2") + "~" + 范围.max.y.ToString("F2")
                + " → 根Y " + 旧Y.ToString("F2") + " → " + 新Y.ToString("F2"), this);
    }

    // ============================================================ 结算

    void 结算一次()
    {
        if (战斗属性 == null) return;

        float r = 半径;
        var 命中s = Physics.OverlapSphere(transform.position, r, 敌人层, QueryTriggerInteraction.Ignore);
        var 去过重 = new HashSet<NpcInstance>();
        var 规则 = new AttackSpec(伤害属性, AttackKind.被动神通, false, 技能倍率);

        foreach (var c in 命中s)
        {
            if (c == null) continue;
            var npc = c.GetComponentInParent<NpcInstance>();
            if (npc == null || npc.IsDead) continue;
            if (!去过重.Add(npc)) continue;

            // 雷罚：常驻模式下由 维护雷罚() 每帧负责，这里只在非常驻模式补一个
            if (使用落雷 || !雷罚常驻) 放一下雷罚(npc);

            var 目标 = new NpcTarget(npc);
            var 结果 = CombatDamagePipeline.命中(目标,new CombatHitContext(战斗属性,规则,this,"ability_qianjie_leiyu",npc.transform.position,Vector3.down));
            命中次数++;
            累计伤害 += 结果.伤害;

            if (打印日志)
                Debug.Log("[千劫雷狱] 雷罚「" + 目标.名字 + "」 " + 结果, npc);
        }
    }

    /// <summary>
    /// 维护每个受影响敌人脚下的雷罚。
    ///
    /// **常驻模式（默认，用户要求）**：敌人只要还在范围内，脚下就**一直**有雷罚，
    /// 跟着它走；离开范围 / 死亡 / 光环关闭才收掉。
    /// 关掉 `雷罚常驻` 才退回"每次结算现放一个、播完消失"的旧行为。
    /// </summary>
    void 维护雷罚()
    {
        if (string.IsNullOrEmpty(雷罚特效路径)) return;

        float r = 半径;
        var 还在范围 = new HashSet<NpcInstance>();
        var 命中s = Physics.OverlapSphere(transform.position, r, 敌人层, QueryTriggerInteraction.Ignore);

        foreach (var c in 命中s)
        {
            if (c == null) continue;
            var npc = c.GetComponentInParent<NpcInstance>();
            if (npc == null || npc.IsDead) continue;
            if (!还在范围.Add(npc)) continue;

            确保雷罚(npc);
        }

        // 收掉：已经不在范围内 / 死了 / 被销毁的
        收掉离开范围的(还在范围);
    }

    /// <summary>保证这个敌人脚下有一个雷罚（没有就建一个，有就让它跟着走）</summary>
    void 确保雷罚(NpcInstance 敌人)
    {
        if (!雷罚常驻) return;          // 非常驻模式走 放一下雷罚()，不在这里管

        GameObject 现有;
        if (雷罚实例.TryGetValue(敌人, out 现有) && 现有 != null)
        {
            跟随(现有, 敌人);
            return;
        }

        var prefab = Resources.Load<GameObject>(雷罚特效路径);
        if (prefab == null)
        {
            Debug.LogWarning("[千劫雷狱] 找不到雷罚特效：" + 雷罚特效路径, this);
            return;
        }

        Vector3 脚下 = 敌人.transform.position + Vector3.up * 雷罚抬高;
        // 存活秒传 0 = 不自动销毁（常驻，由我们自己在敌人离开时收）
        var go = 特效摆放.生成(雷罚特效路径, 脚下, 特效旋转欧拉,
                              按体型算缩放(敌人), 对齐到锚点: true,
                              存活秒: 0f, 名: "千劫雷狱_雷罚_" + 敌人.DisplayName);
        if (go != null) 雷罚实例[敌人] = go;
    }

    /// <summary>让雷罚跟着敌人走（敌人会动，所以每帧对一次）</summary>
    void 跟随(GameObject 雷罚, NpcInstance 敌人)
    {
        Vector3 脚下 = 敌人.transform.position + Vector3.up * 雷罚抬高;
        特效摆放.对齐到(雷罚, 脚下);
    }

    /// <summary>收掉已经离开范围 / 已死的敌人的雷罚</summary>
    void 收掉离开范围的(HashSet<NpcInstance> 还在范围)
    {
        待清理.Clear();
        foreach (var kv in 雷罚实例)
        {
            var npc = kv.Key;
            bool 该收 = npc == null || npc.IsDead || !还在范围.Contains(npc);
            if (!该收) continue;
            if (kv.Value != null) Destroy(kv.Value);
            待清理.Add(npc);
        }
        foreach (var k in 待清理) 雷罚实例.Remove(k);

        // 非常驻模式的计时表也顺带清一下
        if (!雷罚常驻)
        {
            待清理.Clear();
            foreach (var kv in 上次雷罚)
                if (kv.Key == null || kv.Key.IsDead || Time.time - kv.Value > 30f) 待清理.Add(kv.Key);
            foreach (var k in 待清理) 上次雷罚.Remove(k);
        }
    }

    /// <summary>非常驻模式：在敌人脚下现放一个（按重放间隔限频）</summary>
    bool 使用落雷 => !string.IsNullOrEmpty(雷罚特效路径) && 雷罚特效路径.EndsWith("/lightning-ray");

    void 放一下雷罚(NpcInstance 敌人)
    {
        if (使用落雷)
        {
            var impact = 敌人.transform.position;
            float scale = 按体型算缩放(敌人);
            LightningRayVfx.SpawnVertical(impact, scale, .65f, "千劫雷狱_落雷_" + 敌人.DisplayName);
            return;
        }
        if (雷罚重放间隔 > 0.01f)
        {
            float 上次;
            if (上次雷罚.TryGetValue(敌人, out 上次) && Time.time - 上次 < 雷罚重放间隔) return;
        }
        上次雷罚[敌人] = Time.time;

        Vector3 脚下 = 敌人.transform.position + Vector3.up * 雷罚抬高;
        特效摆放.生成(雷罚特效路径, 脚下, 特效旋转欧拉,
                      按体型算缩放(敌人), 对齐到锚点: true,
                      存活秒: 雷罚存活, 名: "千劫雷狱_雷罚_" + 敌人.DisplayName);
    }

    // ============================================================ 工具

    /// <summary>雷罚缩放 = 敌人模型高度 ÷ 基准高度，夹进上下限</summary>
    float 按体型算缩放(NpcInstance 敌人)
    {
        float 高 = 特效摆放.量高度(敌人 != null ? 敌人.transform : null, 雷罚基准敌人高度);
        if (高 <= 0.01f) return 1f;
        return Mathf.Clamp(高 / Mathf.Max(0.1f, 雷罚基准敌人高度), 雷罚缩放下限, 雷罚缩放上限);
    }

    void OnDrawGizmosSelected()
    {
        if (!画范围线) return;
        Gizmos.color = new Color(0.6f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 半径);
    }

    // ---- ASCII 别名 ----
    public float Radius => 半径;
    public bool IsActive => 生效中;
    public float TotalDamage => 累计伤害;
    public int HitCount => 命中次数;
}
