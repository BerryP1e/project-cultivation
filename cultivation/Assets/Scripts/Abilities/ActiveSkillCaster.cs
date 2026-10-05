using UnityEngine;

/// <summary>
/// 主动技能栏的快捷键施放器。挂在 Player 上。
///
/// 技能栏有 6 格（神通 / 法宝 / 灵阵 共用，见 <see cref="UIPanelData.主动技能"/>），
/// 第 N 格 → 快捷键 N（默认主键盘 1~6，可同时支持小键盘）。
///
/// 一次施放的顺序：
///   冷却 → 结算方式是否实现 → 是否需要锁定 → 灵力够不够
///   → 扣灵力、进冷却 → 生成特效 + 结算伤害
///
/// 任何一步不过都只给玩家一句提示，**不会静默失败、也不会白扣灵力**。
///
/// 界面开着时默认不接收快捷键（跟 NpcTargeting 的做法一致：
/// 全屏界面开着时不该往场景里灌操作）。场景输入统一由 UiEscRegistry 锁定。
/// </summary>
public class ActiveSkillCaster : MonoBehaviour
{
    [Header("按键（下标 = 技能栏槽位 0~5）")]
    public KeyCode[] 快捷键 =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
    };

    [Tooltip("是否同时接受小键盘 1~6")]
    public bool 同时支持小键盘 = true;

    [Header("输入闸门")]
    [Tooltip("角色面板 / 设施界面开着时是否禁止施放")]
    public bool 界面开着时禁止施放 = true;

    [Header("引用（留空自动找）")]
    public UIPanelData 面板数据;
    public PlayerVitals 生命;
    public PlayerCombatStats 战斗属性;
    public NpcTargeting 目标管理器;

    [Header("特效")]
    [Tooltip("一次性技能（持续时长 = 0）的特效播多久后销毁")]
    public float 一次性特效存活 = 3f;

    [Tooltip("特效在 scale = 1 时的视觉外缘半径（米）。施放时按【范围 ÷ 这个值】缩放特效的 X/Z，\n" +
             "让火焰大小跟着范围走。换了特效资源就要重新标定。\n" +
             "Effect_13_DangerClose 实测 49.1 米 / Effect_13_Explosion 实测 9.9 米")]
    public float 特效基准半径 = 49.1f;

    [Header("施法动作")]
    [Tooltip("玩家动画控制器。留空自动在本体找")]
    public PlayerAnimationController 动画;

    [Tooltip("生成特效时额外施加的欧拉角。\n" +
             "Effect_13_DangerClose 是【倒着做的】：原样播放时粒子全部在地面以下（实测世界 Y = −100~0）\n" +
             "—— 看起来是「从地底往上冒」。绕 X 转 180° 后粒子变成 Y = 0~100，才是「从天而降砸向地面」。\n" +
             "换特效资源时这个值也要跟着改。")]
    public Vector3 特效旋转 = new Vector3(180f, 0f, 0f);

    [Tooltip("范围伤害的检测层级")]
    public LayerMask 敌人层 = ~0;

    [Header("闪烁位移（雷动千闪）")]
    [Tooltip("**起点特效**的文件名（不带路径、不带扩展名）。\n\n" +
             "闪烁神通表里只有一列「特效资源路径」= **落点**特效，起点特效由代码推：\n" +
             "  1. 落点叫 xxx_02 就找同目录的 xxx_01\n" +
             "  2. 否则在同目录里找这个名字（默认 lightning-arc-flash）\n" +
             "  3. 都找不到就和落点用同一个\n" +
             "留空 = 跳过第 2 条。")]
    public string 起点特效同目录名 = "lightning-arc-flash";

    [Header("调试")]
    public bool 打印施法日志 = true;

    static readonly KeyCode[] 小键盘键 =
    {
        KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
        KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,
    };

    readonly float[] 冷却剩余 = new float[6];
    readonly BindingChainSkillRunner[] 持续锁链 = new BindingChainSkillRunner[6];

    /// <summary>每格当前**攒着几次**（充能式技能用；普通技能不走这里）</summary>
    readonly int[] 充能数 = new int[6];
    /// <summary>每格上一次"是谁"，用来发现槽位内容被换掉</summary>
    readonly Object[] 充能上次内容 = new Object[6];

    /// <summary>这一格是不是**充能式**（可积攒次数 &gt; 1）</summary>
    int 充能上限(int 槽位)
    {
        var 神通 = 槽位内容(槽位) as ActiveDivineAbility;
        return 神通 != null ? Mathf.Max(1, 神通.可积攒次数) : 1;
    }

    /// <summary>这一格还剩几次可用（普通技能：就绪 = 1、冷却中 = 0）</summary>
    public int 剩余充能(int 槽位)
    {
        if (槽位 < 0 || 槽位 >= 充能数.Length) return 0;
        if (充能上限(槽位) <= 1) return 冷却剩余秒(槽位) <= 0f ? 1 : 0;
        return 充能数[槽位];
    }

    /// <summary>
    /// 发现槽位内容变了（装上/卸下/换了神通）→ 重置这一格的充能与冷却。
    ///
    /// ⚠️ 为什么不能靠"充能数 == 0 就补满"这种懒初始化：
    ///    充能式技能**正常消耗完**之后充能数也是 0、且冷却剩余也是 0（没在倒计时），
    ///    那样就会被误判成"还没初始化"→ 刚用完三下立刻又回满三次 ✗
    ///    所以必须**显式记住这一格装的是哪个神通**，只有换人了才重置。
    /// </summary>
    void 同步槽位内容()
    {
        for (int i = 0; i < 充能数.Length; i++)
        {
            var 现在 = 槽位内容(i);
            if (充能上次内容[i] == 现在) continue;

            if (持续锁链[i] != null) 持续锁链[i].取消();

            充能上次内容[i] = 现在;
            冷却剩余[i] = 0f;
            充能数[i] = 现在 != null ? 充能上限(i) : 0;
        }
    }

    void Awake() => 解析引用();

    void 解析引用()
    {
        if (面板数据 == null) 面板数据 = FindObjectOfType<UIPanelData>();
        if (生命 == null) 生命 = GetComponent<PlayerVitals>();
        if (战斗属性 == null) 战斗属性 = GetComponent<PlayerCombatStats>();
        if (目标管理器 == null) 目标管理器 = GetComponent<NpcTargeting>();
    }

    void Update()
    {
        // 冷却一直走（暂停时 deltaTime 为 0，自然不会推进）
        for (int i = 0; i < 冷却剩余.Length; i++)
            if (冷却剩余[i] > 0f) 冷却剩余[i] -= Time.deltaTime;

        // 槽位内容可能被装备/卸下改掉 → 先同步（换人了就重置充能与冷却）
        同步槽位内容();

        // ★ 充能式技能：冷却走完 → 回 1 次充能，并**立刻重新开始下一轮冷却**（只要还没攒满）。
        //   于是"冷却 10 秒、可积攒 3 次"的表现是：开场满 3 次，用完 3 次后每 10 秒回 1 次。
        for (int i = 0; i < 充能数.Length; i++)
        {
            int 上限 = 充能上限(i);
            if (上限 <= 1) continue;
            if (充能数[i] >= 上限) continue;        // 攒满了，不用倒计时
            if (冷却剩余[i] > 0f) continue;         // 还在倒计时

            充能数[i]++;
            if (充能数[i] < 上限) 冷却剩余[i] = 冷却总秒(i);
        }

        if (!接收输入()) return;

        int 上限2 = Mathf.Min(6, 快捷键 != null ? 快捷键.Length : 0);
        for (int i = 0; i < 上限2; i++)
        {
            bool 按下 = Input.GetKeyDown(快捷键[i]);
            if (!按下 && 同时支持小键盘 && i < 小键盘键.Length)
                按下 = Input.GetKeyDown(小键盘键[i]);

            if (按下) { 尝试施放(i); return; }   // 一帧只放一个
        }
    }

    /// <summary>现在该不该接收快捷键</summary>
    public bool 接收输入()
    {
        if (Time.timeScale <= 0f) return false;                          // 暂停菜单开着
        if (UiEscRegistry.SceneInputBlocked) return false;
        return true;
    }

    /// <summary>按下技能栏第 N 格（0~5）。返回是否真的放出去了。</summary>
    public bool 尝试施放(int 槽位)
    {
        if(UiEscRegistry.SceneInputBlocked)return false;
        if (面板数据 == null) 解析引用();
        if (面板数据 == null || 面板数据.主动技能 == null) return false;
        if (槽位 < 0 || 槽位 >= 面板数据.主动技能.Count) return false;

        var 内容 = 面板数据.主动技能[槽位];
        if (内容 == null) return false;                        // 空槽：静默

        // ---- 冷却 / 充能 ----
        // 充能式技能（可积攒次数 > 1）看"还剩几次"，普通技能退化成原来的"冷却好没好"。
        同步槽位内容();
        // 关闭持续技能不能被它自己的冷却或灵力检查挡住。
        if (槽位 < 持续锁链.Length && 持续锁链[槽位] != null)
        {
            持续锁链[槽位].取消();
            持续锁链[槽位] = null;
            return true;
        }
        int 上限 = 充能上限(槽位);
        if (上限 > 1)
        {
            if (充能数[槽位] <= 0)
            {
                提示("「" + 取名(内容) + "」次数已用尽，还需 " + 冷却剩余[槽位].ToString("0.0") + " 秒回一次");
                return false;
            }
        }
        else if (槽位 < 冷却剩余.Length && 冷却剩余[槽位] > 0f)
        {
            提示("「" + 取名(内容) + "」冷却中，还需 " + 冷却剩余[槽位].ToString("0.0") + " 秒");
            return false;
        }

        // ---- 目前只有主动神通实现了；法宝 / 灵阵还是占位资产 ----
        var 神通 = 内容 as ActiveDivineAbility;
        if (神通 == null)
        {
            提示("「" + 取名(内容) + "」还没做（目前只实现了主动神通）");
            return false;
        }

        if (神通.结算方式 == ActiveSkillKind.未实现)
        {
            提示("「" + 神通.神通名称 + "」还没做");
            return false;
        }

        // ---- 需要锁定 ----
        var 锁定 = 目标管理器 != null ? 目标管理器.LockedNpc : null;
        if (神通.需要锁定目标 && (锁定 == null || 锁定.IsDead))
        {
            提示("「" + 神通.神通名称 + "」需要先右键锁定一个敌人");
            return false;
        }

        // ---- 灵力 ----
        if (神通.结算方式 >= ActiveSkillKind.定向水炮)
        {
            if (生命 == null || 生命.IsDead) return false;
            if (锁定 != null && 神通.范围 > 0f && Vector3.Distance(transform.position, 锁定.transform.position) > 神通.范围
                && 神通.结算方式 != ActiveSkillKind.小剑阵)
            { 提示("目标超出施放距离"); return false; }
            if (Resources.Load<GameObject>(神通.特效资源路径) == null)
            { 提示("神通特效资源缺失，未消耗灵力"); return false; }
        }
        if (神通.消耗灵力 > 0f)
        {
            if (生命 == null) 解析引用();
            if (生命 == null) return false;

            if (!生命.够灵气(神通.消耗灵力))
            {
                提示("灵力不足：「" + 神通.神通名称 + "」需要 " + 神通.消耗灵力.ToString("0.#")
                     + "，当前 " + 生命.当前灵气.ToString("0.#"));
                return false;
            }
            生命.扣灵气(神通.消耗灵力);
        }

        // ---- 扣次数 / 进冷却 ----
        if (上限 > 1)
        {
            // 充能式：消耗一次；从"满"掉下来才开始倒计时（满的时候不倒计时）
            充能数[槽位] = Mathf.Max(0, 充能数[槽位] - 1);
            if (槽位 < 冷却剩余.Length && 冷却剩余[槽位] <= 0f) 冷却剩余[槽位] = 神通.冷却时间;
        }
        else if (槽位 < 冷却剩余.Length) 冷却剩余[槽位] = 神通.冷却时间;

        if (神通.结算方式 == ActiveSkillKind.持续禁锢)
        {
            var 宿主 = new GameObject("BindingChain_" + 神通.神通id);
            持续锁链[槽位] = 宿主.AddComponent<BindingChainSkillRunner>();
            持续锁链[槽位].初始化(this, 神通, 锁定, 槽位);
        }
        else 施放(神通, 锁定);
        return true;
    }

    void 施放(ActiveDivineAbility 神通, NpcInstance 锁定)
    {
        // 【施法动作】表里「施法动作」列配了才播（例：焚天炎术 → 技能动作2）
        // 片段放在 Assets/resources/技能动作/ 下，所以能 Resources.Load。
        // 注意是"播动作"而不是"等它播完" —— 出手/结算的时机仍由表的「首次造成伤害时间」控制，
        // 这样动画和伤害对齐是策划可调的，不会被动画长度绑架。
        播施法动作(神通);
        if (战斗属性 == null) 解析引用();

        if (神通.结算方式 == ActiveSkillKind.定向水炮 || 神通.结算方式 == ActiveSkillKind.小剑阵)
        {
            var 宿主 = new GameObject("DirectedSkill_" + 神通.神通id);
            宿主.AddComponent<DirectedAbilityRunner>().初始化(this, 神通, 锁定);
            return;
        }

        // ★ 闪烁位移（雷动千闪）：它自己管"位移 + 起落两处特效 + 两处结算"，
        //   和"原地放一蓬范围伤害"是两套节奏，所以单独分流，别塞进下面那套。
        if (神通.结算方式 == ActiveSkillKind.闪烁位移)
        {
            施放闪烁(神通);
            return;
        }

        // ★ 追踪弹（瞬雷天闪）：它自己管"播普攻动作 → 节点出弹 → 追踪 → 命中结算"，
        //   节奏跟着**动画**走（不是跟着表的「首次造成伤害时间」），所以也单独分流。
        if (神通.结算方式 == ActiveSkillKind.追踪弹)
        {
            if (动画 == null) 动画 = GetComponent<PlayerAnimationController>();
            if (动画 == null) 动画 = GetComponentInChildren<PlayerAnimationController>();
            施放追踪弹(神通, 锁定, 动画);
            return;
        }

        // ★ 向前冰柱（寒墟）：以**鼠标落点**为方向推进，自己管"铺冰柱 + 三段伤害"，
        //   和"原地放一蓬范围伤害"是两套节奏，所以也单独分流。
        if (神通.结算方式 == ActiveSkillKind.向前冰柱)
        {
            施放冰柱(神通);
            return;
        }

        // 以锁定的敌人为中心（不需要锁定的技能则以自己为中心）。取施放瞬间的位置。
        Vector3 中心 = (神通.需要锁定目标 && 锁定 != null) ? 锁定.transform.position : transform.position;

        // 特效。就算没配特效也要结算伤害，所以结算体永远都建。
        GameObject 载体 = null;
        if (!string.IsNullOrEmpty(神通.特效资源路径))
        {
            var prefab = Resources.Load<GameObject>(神通.特效资源路径);
            if (prefab != null) 载体 = Instantiate(prefab, 中心, Quaternion.Euler(特效旋转));
            else Debug.LogWarning("[ActiveSkillCaster] 找不到特效资源：" + 神通.特效资源路径
                                  + "（路径要相对 Assets/resources，且不带扩展名）");
        }
        if (载体 == null) 载体 = new GameObject();
        载体.name = "SkillFx_" + 神通.神通id;

        // 特效大小跟着【范围】走：只缩放 X/Z，Y 保持原样。
        //
        // 必须同时把粒子系统改成 Local 空间：World 空间下 transform 缩放只改变粒子的
        // **散布位置**、不改变**单个粒子的尺寸**，结果是火焰被"摊开"而不是"变大"，
        // 大小根本跟不住范围（实测：scale 1→3 时散布 6.2→18.9 米，但粒径恒为 9.94）。
        // 换成 Local 之后缩放才是等比的（外缘半径/scale ≈ 10.5，基本是常数）。
        // 特效本身是静止的，Local / World 在观感上没有区别。
        if (特效基准半径 > 0f && 神通.范围 > 0f)
        {
            float 缩放 = Mathf.Max(0.05f, 神通.范围 / 特效基准半径);
            载体.transform.localScale = new Vector3(缩放, 1f, 缩放);

            foreach (var ps in 载体.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
                {
                    var m = ps.main;
                    m.simulationSpace = ParticleSystemSimulationSpace.Local;
                }
            }
        }

        var runner = 载体.AddComponent<AreaSkillRunner>();
        runner.初始化(神通, 中心, 战斗属性, 敌人层, 锁定, 一次性特效存活);

        if (打印施法日志)
            Debug.Log("[ActiveSkillCaster] 施放「" + 神通.神通名称 + "」中心=" + 中心
                      + " 范围=" + 神通.范围 + " 倍率=" + 神通.伤害倍率
                      + " 属性=" + 神通.伤害属性
                      + " → 命中 " + runner.结算次数 + " 次，合计 " + runner.累计伤害.ToString("0.##"));
    }

    /// <summary>
    /// 向前冰柱型神通（**寒墟**）的执行：以**鼠标落点**为方向，向前推出一道连续的冰柱，
    /// 路径上的敌人吃三段伤害（冰柱 → 脚下 frost-ring → 脚下 frost-spike）。
    ///
    /// 表里那列「特效资源路径」= **第一段的冰柱**（`frost-wave`）；
    /// 二段 / 三段走新增的「命中特效路径」/「三段特效路径」列（`frost-ring` / `frost-spike`）。
    /// 「施法动作」列对这一档**可以留空**（留空 = 不播动作，不影响出招）。
    /// </summary>
    void 施放冰柱(ActiveDivineAbility 神通)
    {
        // 动作由 runner 按**普攻动作**播（和追踪弹同一套），所以要把它传进去
        if (动画 == null) 动画 = GetComponent<PlayerAnimationController>();
        if (动画 == null) 动画 = GetComponentInChildren<PlayerAnimationController>();

        var 宿主 = new GameObject("IcePillar_" + 神通.神通id);
        var runner = 宿主.AddComponent<IcePillarSkillRunner>();
        runner.初始化(神通, 战斗属性, transform, 敌人层, Camera.main, 动画);

        if (打印施法日志)
            Debug.Log("[ActiveSkillCaster] 施放向前冰柱「" + 神通.神通名称 + "」"
                + " 长度=" + 神通.范围 + "m 时长=" + 神通.持续时长 + "s"
                + " 倍率=" + 神通.伤害倍率 + " 属性=" + 神通.伤害属性
                + " 间隔=" + 神通.伤害间隔 + "s"
                + "（动作 40% 节点放冰柱：" + (动画 != null ? "有动画组件" : "★没有动画组件，会立刻放") + "）"
                + "\n  冰柱=" + 神通.特效资源路径
                + "\n  二段=" + 神通.命中特效路径 + (神通.命中特效贴地 ? "（贴地）" : "")
                + "\n  三段=" + 神通.三段特效路径, this);
    }

    /// <summary>
    /// 追踪弹型神通的执行（目前是**瞬雷天闪**）：
    /// 播**普攻动作**，在动画的 `出手进度`（策划要 40%）节点放出一颗会拐弯追踪的弹，
    /// 命中时在命中点放命中特效、并**只对锁定目标**结算一次主动神通伤害。
    ///
    /// 表里只有一列「特效资源路径」，这里约定：
    ///   · 表里那列 = **闪电球**（`lightning-sphere`）
    ///   · **命中特效**（`lightning-explode`）由 runner 自己的字段配 ——
    ///     因为它俩不是"同目录下的两个变体"，而是两个不同用途的资源。
    /// 「施法动作」列对这一档**要留空** —— 动作由 runner 按普攻动作播，
    /// 留空才不会被上面那句 `播施法动作` 再插一个动作进来。
    /// </summary>
    void 施放追踪弹(ActiveDivineAbility 神通, NpcInstance 锁定, PlayerAnimationController 动画组件)
    {
        var 宿主 = new GameObject("HomingBolt_" + 神通.神通id);
        var runner = 宿主.AddComponent<HomingBoltSkillRunner>();
        runner.初始化(神通, 战斗属性, 锁定, 动画组件, transform, 敌人层);

        // 表里那列是"球"；命中特效 runner 自己有默认值（lightning-explode），
        // 神通自己配了「命中特效路径」就覆盖它（**冰暴术 = frost-frozen-tomb**，且要贴地）
        if (!string.IsNullOrEmpty(神通.特效资源路径)) runner.球特效路径 = 神通.特效资源路径;
        if (!string.IsNullOrEmpty(神通.命中特效路径)) runner.命中特效路径 = 神通.命中特效路径;
        runner.命中特效贴地 = 神通.命中特效贴地;

        if (打印施法日志)
            Debug.Log("[ActiveSkillCaster] 施放追踪弹「" + 神通.神通名称 + "」目标="
                + (锁定 != null ? 锁定.DisplayName : "null")
                + " 倍率=" + 神通.伤害倍率 + " 属性=" + 神通.伤害属性
                + " 次数=" + 神通.可积攒次数 + " 冷却=" + 神通.冷却时间
                + "\n  弹=" + runner.球特效路径 + "\n  命中=" + runner.命中特效路径, this);
    }

    /// <summary>
    /// 闪烁位移型神通的执行：起落两处特效 + 两处主动神通伤害。
    ///
    /// 特效路径的用法在这里是**约定**（表里只有一列「特效资源路径」）：
    /// **表里填的是"落点特效"，起点特效自动去同目录/同包里找**。
    ///
    /// 查找顺序：
    ///   1. `xxx_02` → 同目录的 `xxx_01`（老写法，`Electro_Lightning_02` 那套）
    ///   2. 否则在同目录里按名字找"发光/闪烁"类的那个（如 `lightning-glow` / `lightning-flash`）
    ///   3. 再找不到就退回用表里那个（两处一样，至少不会没特效）
    ///
    /// 用户 2026-09-28 把雷动千闪换成了 `lightning-impact`，它没有 `_01/_02` 后缀，
    /// 所以加了第 2 条规则 —— 起落用**同一套特效的不同部件**，观感才统一。
    /// </summary>
    void 施放闪烁(ActiveDivineAbility 神通)
    {
        var 相机 = Camera.main;
        string 终点特效 = 神通.特效资源路径;
        string 起点特效 = 推起点特效路径(终点特效);

        var 宿主 = new GameObject("BlinkSkill_" + 神通.神通id);
        var runner = 宿主.AddComponent<BlinkSkillRunner>();
        runner.初始化(神通, 战斗属性, 生命, 相机, transform, 敌人层, 起点特效, 终点特效, 一次性特效存活);

        if (打印施法日志)
            Debug.Log("[ActiveSkillCaster] 施放闪烁「" + 神通.神通名称 + "」"
                + " 半径=" + 神通.范围 + " 倍率=" + 神通.伤害倍率 + " 属性=" + 神通.伤害属性
                + " 次数=" + 神通.可积攒次数 + " 冷却=" + 神通.冷却时间
                + "\n  起点特效=" + 起点特效 + "\n  终点特效=" + 终点特效, this);
    }

    /// <summary>
    /// 把「落点特效路径」推成「起点特效路径」。
    ///
    /// 三条规则，按顺序试：
    ///   1. `xxx_02` → 同目录的 `xxx_01`（老写法，`Electro_Lightning_02` 那套）
    ///   2. 同目录里找 `起点特效同目录名`（默认 `lightning-arc-flash`）——
    ///      用户 2026-09-28 把雷动千闪换成 `lightning-impact` 后没有 `_01/_02` 后缀，
    ///      所以起点改用同包一个更轻的闪烁特效做区分
    ///   3. 都没有 → 退回用落点那个（两处一样，至少不会没特效）
    /// </summary>
    string 推起点特效路径(string 终点路径)
    {
        if (string.IsNullOrEmpty(终点路径)) return "";

        int 斜杠 = 终点路径.LastIndexOf('/');
        string 目录 = 斜杠 > 0 ? 终点路径.Substring(0, 斜杠 + 1) : "";
        string 名 = 斜杠 > 0 ? 终点路径.Substring(斜杠 + 1) : 终点路径;

        // 规则 1：末尾的 _02 / _2 → _01 / _1
        var m = System.Text.RegularExpressions.Regex.Match(名, @"^(.*_)(\d+)$");
        if (m.Success)
        {
            string 换 = 目录 + m.Groups[1].Value + "01";
            if (Resources.Load<GameObject>(换) != null) return 换;
        }

        // 规则 2：同目录里按名字找起点特效
        if (!string.IsNullOrEmpty(起点特效同目录名))
        {
            string 换 = 目录 + 起点特效同目录名;
            if (Resources.Load<GameObject>(换) != null) return 换;
        }

        return 终点路径;      // 都推不出来 → 两处用同一个
    }

    static string 取名(Object o)
    {
        var e = o as IPanelEntry;
        if (e != null && !string.IsNullOrEmpty(e.DisplayName)) return e.DisplayName;
        return o != null ? o.name : "?";
    }

    void OnDisable()
    {
        foreach (var 链 in 持续锁链) if (链 != null) 链.取消();
    }

    void 提示(string msg)
    {
        if (打印施法日志) Debug.Log("[ActiveSkillCaster] " + msg);
        if (面板数据 != null) 面板数据.ShowHint(msg);
    }

    /// <summary>某格的剩余冷却（秒）</summary>
    public float 冷却剩余秒(int 槽位)
        => (槽位 >= 0 && 槽位 < 冷却剩余.Length) ? Mathf.Max(0f, 冷却剩余[槽位]) : 0f;

    /// <summary>某格装的是什么（没装返回 null）</summary>
    public Object 槽位内容(int 槽位)
        => (面板数据 != null && 面板数据.主动技能 != null
            && 槽位 >= 0 && 槽位 < 面板数据.主动技能.Count)
           ? 面板数据.主动技能[槽位] : null;

    /// <summary>某格的冷却总时长（秒）。空槽 / 没配冷却返回 0</summary>
    public float 冷却总秒(int 槽位)
    {
        var 神通 = 槽位内容(槽位) as ActiveDivineAbility;
        return 神通 != null ? Mathf.Max(0f, 神通.冷却时间) : 0f;
    }

    /// <summary>某格是否正在冷却</summary>
    public bool 冷却中(int 槽位) => 冷却剩余秒(槽位) > 0f;

    /// <summary>
    /// 某格的冷却进度 0~1：**1 = 刚进冷却，0 = 冷却完毕**。
    /// 给 HUD 的暗部遮罩用 —— fillAmount 直接吃这个值，暗部就会随冷却逐渐消退。
    /// </summary>
    public float 冷却比例(int 槽位)
    {
        float 总 = 冷却总秒(槽位);
        if (总 <= 0f) return 0f;
        return Mathf.Clamp01(冷却剩余秒(槽位) / 总);
    }

    /// <summary>某格装的是不是「已实现、能主动施放」的东西（决定 HUD 图标亮不亮）</summary>
    public bool 槽位可用(int 槽位)
    {
        var 神通 = 槽位内容(槽位) as ActiveDivineAbility;
        return 神通 != null && 神通.结算方式 != ActiveSkillKind.未实现;
    }

    // ---- ASCII 别名 ----
    public bool TryCast(int slot) => 尝试施放(slot);
    public bool AcceptsInput() => 接收输入();
    public float CooldownLeft(int slot) => 冷却剩余秒(slot);
    public Object SlotContent(int slot) => 槽位内容(slot);
    public float CooldownTotal(int slot) => 冷却总秒(slot);
    public bool OnCooldown(int slot) => 冷却中(slot);
    public float CooldownRatio(int slot) => 冷却比例(slot);
    public bool SlotUsable(int slot) => 槽位可用(slot);

    // ============================================================ 施法动作

    /// <summary>
    /// 播这个神通配的施法动作。表里没配就什么都不做。
    /// 片段名对应 `Assets/resources/技能动作/&lt;名字&gt;.anim`。
    /// </summary>
    void 播施法动作(ActiveDivineAbility 神通)
    {
        if (神通 == null || string.IsNullOrEmpty(神通.施法动作)) return;

        if (动画 == null) 动画 = GetComponent<PlayerAnimationController>();
        if (动画 == null) 动画 = GetComponentInChildren<PlayerAnimationController>();
        if (动画 == null)
        {
            Debug.LogWarning("[主动神通] 找不到 PlayerAnimationController，播不了施法动作「" + 神通.施法动作 + "」", this);
            return;
        }

        var 片段 = Resources.Load<AnimationClip>("技能动作/" + 神通.施法动作);
        if (片段 == null)
        {
            Debug.LogWarning("[主动神通] 找不到施法动作 Assets/resources/技能动作/" + 神通.施法动作 + ".anim", this);
            return;
        }

        // 施法动作**不受攻速影响**（攻速只管普攻），固定原速
        if (动画.播动作(片段, 1f) && 打印施法日志)
            Debug.Log("[主动神通] 播施法动作「" + 神通.施法动作 + "」（" + 片段.length.ToString("0.##") + "s）", this);
    }
}
