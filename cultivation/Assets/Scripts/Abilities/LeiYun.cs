using UnityEngine;

/// <summary>
/// 被动神通【雷云】的运行时表现。
///
/// ## 策划说明（2026-09-28）
/// ```
/// 装备该被动神通后**持续消耗灵力**
/// 在角色身边生成一朵雷云（特效 lightning-cloud）
/// 当角色**锁定目标**时，雷云每隔一段时间召唤闪电
///   · ⚠️ 那道闪电的特效**暂时空置待补**（`闪电特效路径 = ""`）：
///     一端是**锁定目标**、一端是**雷云**。
///     空置时**照样出手、照样掉血，只是看不到落雷**。
///   · 然后在敌人那里生成**受击特效** lightning-arc-flash
///   · 受击特效的大小取决于**敌人尺寸**
///   · 敌人受到【特殊 + 被动神通】伤害
/// ```
///
/// ## 怎么被装上
/// 在 `PlayerAbilityLoader.被动神通表` 里登记
/// `{神通id = ability_leiyun, 组件类名 = [LeiYun]}`
/// （5 个场景都要加），启用被动就自动 AddComponent、停用就 enabled = false。
///
/// ## 和千劫雷狱的区别
/// | | 千劫雷狱 | 雷云 |
/// |---|---|---|
/// | 触发 | 敌人在**范围**内自动挨打 | **锁定了目标**才劈 |
/// | 表现 | 脚下常驻雷罚 + 一圈黑环 | 头顶一朵云 + 一道线连到目标 |
/// | 伤害 | 特殊 + 被动神通 | 特殊 + 被动神通（同一套加成） |
/// </summary>
[DisallowMultipleComponent]
public class LeiYun : MonoBehaviour
{
    // ============================================================ 配置

    [Header("引用（留空自动找）")]
    [Tooltip("玩家属性（当攻击方）。留空自动在本体找")]
    public PlayerCombatStats 战斗属性;

    [Tooltip("灵气（持续消耗 / 耗尽自动关闭）。留空自动在本体找")]
    public PlayerVitals 灵气;

    [Tooltip("锁定管理器 —— 雷云只在**锁定了目标**时才劈。留空自动在本体找")]
    public NpcTargeting 目标管理器;

    [Header("触发")]
    [Tooltip("灵力耗尽时自动关闭，灵力回满后自动重开")]
    public bool 灵力耗尽自动关闭 = true;

    [Tooltip("每隔多少秒召一道雷")]
    public float 出手间隔 = 2f;

    [Tooltip("锁定目标超出这个距离就不劈（米）。0 = 不限制")]
    public float 最大距离 = 20f;

    [Tooltip("神通表里没填「维持消耗灵力」时，用这个兜底（点/秒）")]
    public float 每秒消耗灵力兜底 = 1f;

    [Header("伤害")]
    [Tooltip("伤害属性。策划要求「特殊」")]
    public DamageNature 伤害属性 = DamageNature.特殊;

    [Tooltip("技能倍率")]
    public float 技能倍率 = 1f;

    [Header("雷云")]
    [Tooltip("雷云特效。路径相对 Assets/resources、不带扩展名")]
    public string 雷云特效路径 =
        "特效/战斗法术/Combat Magic VFX Vol.1/resources/lightning-fx/lightning-cloud";

    [Tooltip("雷云相对角色根节点的偏移（米）。**往上挪到头顶**")]
    public Vector3 雷云偏移 = new Vector3(0f, 3.5f, 0f);

    [Tooltip("雷云特效的生成旋转（欧拉角）。躺平/倒立就改这里")]
    public Vector3 雷云旋转欧拉 = Vector3.zero;

    [Tooltip("雷云特效缩放（1 = 原大小）")]
    public float 雷云缩放 = 1f;

    [Tooltip("雷云要不要跟着角色朝向转")]
    public bool 雷云跟随朝向 = false;

    [Header("雷云跟随（拖尾感）")]
    [Tooltip("★ 开 = 云用**插值**追角色：角色一动，云会稍微拖在后面再跟上来，不会死贴在旁边。\n" +
             "关 = 每帧硬贴（老行为，就是那种「卡死在主角旁边」的感觉）")]
    public bool 跟随用插值 = true;

    [Tooltip("插值追上目标大约要多久（秒）。越大越「懒」、拖得越明显。\n" +
             "角色匀速跑时，稳定落后距离 ≈ 移动速度 × 这个值\n" +
             "（角色 6 m/s、填 0.35 → 大约拖后 2m）")]
    public float 跟随平滑时间 = 0.35f;

    [Tooltip("追上去的最大速度（米/秒）。防止角色突然加速时云「抽」一下。0 = 不限制")]
    public float 跟随最大速度 = 14f;

    [Tooltip("角色**瞬移**（传送 / 复活 / 骑乘切换）时拖一条长尾巴很难看，\n" +
             "云离目标超过这个距离就直接跟上去。0 = 永不瞬移、永远插值")]
    public float 跟随瞬移距离 = 20f;

    [Tooltip("云**转向**的插值速度（度/秒）。只在 雷云跟随朝向 打开时有用。0 = 硬转")]
    public float 朝向插值速度 = 240f;

    [Header("闪电（连到目标的线）")]
    [Tooltip("★ 暂时空置（用户 2026-09-28 决定）：自制的「雷链」表现不合格已删除。\n" +
             "留空 = 雷云照样出手、照样掉血，只是不放那道闪电，不会报错。\n" +
             "以后找到合适的资源填这里；要求与踩过的坑见 docs/guides/闪电链特效.md")]
    public string 闪电特效路径 = "";

    [Tooltip("闪电的粗细")]
    public float 闪电粗细 = 0.75f;

    [Tooltip("闪电基准长度（对应 prefab 的 shape scale）。拉伸系数 = 距离 ÷ 这个值")]
    public float 闪电基准长度 = 1f;

    [Tooltip("闪电最短长度（两点重叠时别被压成 0）")]
    public float 闪电最小长度 = 1f;

    [Tooltip("闪电两端固定外溢多少米（换特效后用探针重新标定；多数填 0）")]
    public float 闪电长度补偿 = 0f;

    [Tooltip("闪电出现多久后消失（用户要求「短暂时间就消失」）")]
    public float 闪电存活 = 0.4f;

    [Tooltip("探不出 prefab 长度轴时的兜底。留空/填错不要紧：运行时会自动探测")]
    public Vector3 闪电兜底拉伸轴 = new Vector3(0f, 0f, 1f);

    [Header("受击特效（贴在被劈的敌人身上）")]
    [Tooltip("受击特效")]
    public string 受击特效路径 =
        "特效/战斗法术/Combat Magic VFX Vol.1/resources/lightning-fx/lightning-arc-flash";

    [Tooltip("受击特效的生成旋转（欧拉角）")]
    public Vector3 受击旋转欧拉 = Vector3.zero;

    [Tooltip("受击特效在敌人这么高时 scale = 1。按敌人模型高度等比缩放")]
    public float 受击基准敌人高度 = 1.8f;

    [Tooltip("受击特效缩放下限")]
    public float 受击缩放下限 = 0.5f;

    [Tooltip("受击特效缩放上限")]
    public float 受击缩放上限 = 3f;

    [Tooltip("受击特效相对敌人脚下的抬高（米）。可以是负数")]
    public float 受击抬高 = 1f;

    [Tooltip("受击特效存活时间（秒）")]
    public float 受击存活 = 1f;

    [Header("调试")]
    [Tooltip("打印日志")]
    public bool 打印日志 = true;

    // ============================================================ 运行时

    /// <summary>现在是不是开着（灵力够 + 启用了）</summary>
    public bool 生效中 { get; private set; }

    /// <summary>累计造成的伤害（调试 / 自动化验证用）</summary>
    public float 累计伤害 { get; private set; }

    /// <summary>累计出手次数</summary>
    public int 出手次数 { get; private set; }

    GameObject 云;
    Vector3 跟随速度;              // Vector3.SmoothDamp 用的速度缓存
    float 下次出手时刻;

    // ============================================================ 生命周期

    void Awake() { 解析引用(); }

    void OnEnable()
    {
        解析引用();
        下次出手时刻 = Time.time;
        开启();
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
            if (!生效中 && 灵气.有灵气) 开启();
        }
        else if (!生效中) 开启();

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

        // ---- 云跟着角色 ----
        维护云();

        // ---- 锁了目标就定期劈 ----
        if (Time.time >= 下次出手时刻)
        {
            var 目标 = 锁定单位;
            if (目标 != null) 出手(目标);
            下次出手时刻 = Time.time + Mathf.Max(0.1f, 出手间隔);
        }
    }

    // ============================================================ 锁定目标

    /// <summary>
    /// 当前锁定的目标。**没锁定就不劈**（策划要求：
    /// 「当角色锁定目标时，雷云每隔一段时间会召唤闪电」）。
    /// </summary>
    public NpcInstance 锁定单位
    {
        get
        {
            var t = 目标管理器 != null ? 目标管理器.LockedNpc : null;
            if (t == null || t.IsDead) return null;
            if (最大距离 > 0.01f)
            {
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d > 最大距离) return null;
            }
            return t;
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

    [Tooltip("雷云在被动神通表里的 id")]
    public string 神通id = "ability_leiyun";

    PassiveDivineAbility 神通缓存;

    /// <summary>表里那条【雷云】被动（按 <see cref="神通id"/> 找）</summary>
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

    UIPanelData _面板;
    UIPanelData 面板数据
    {
        get
        {
            if (_面板 == null) _面板 = FindObjectOfType<UIPanelData>();
            return _面板;
        }
    }

    /// <summary>开启（生成云）</summary>
    public void 开启()
    {
        if (生效中) return;
        生效中 = true;
        下次出手时刻 = Time.time;
        生成云();
        if (打印日志)
            Debug.Log("[雷云] 开启：每 " + 出手间隔.ToString("0.##") + "s 劈一次、每秒耗灵 "
                + 每秒消耗灵力.ToString("0.##"), this);
    }

    /// <summary>关闭（收掉云）</summary>
    public void 关闭()
    {
        if (!生效中) return;
        生效中 = false;
        if (云 != null) Destroy(云);
        云 = null;
        跟随速度 = Vector3.zero;
        if (打印日志)
            Debug.Log("[雷云] 关闭（累计出手 " + 出手次数 + " 次、" + 累计伤害.ToString("0.##") + " 伤害）", this);
    }

    // ============================================================ 雷云

    void 生成云()
    {
        if (string.IsNullOrEmpty(雷云特效路径)) return;

        云 = 特效摆放.生成(雷云特效路径, transform.position + 雷云偏移, 雷云旋转欧拉,
                           雷云缩放, 对齐到锚点: false, 存活秒: 0f, 名: "雷云");
        if (云 == null)
        {
            Debug.LogWarning("[雷云] 云生成失败（看上面的「找不到特效」提示）", this);
            return;
        }

        // 新云直接从目标位开始，别继承上一次的插值速度（否则一开就自己飘一下）
        跟随速度 = Vector3.zero;
        if (雷云跟随朝向) 云.transform.rotation = transform.rotation * Quaternion.Euler(雷云旋转欧拉);
    }

    /// <summary>云这一帧"应该"在哪（还没过插值）</summary>
    Vector3 云目标位 => 雷云跟随朝向
        ? transform.position + transform.rotation * 雷云偏移     // 跟着角色朝向算偏移
        : transform.position + 雷云偏移;                          // 用世界轴算偏移

    /// <summary>
    /// 让云跟着角色。
    ///
    /// ⚠️ 用户 2026-09-28：以前是每帧 `position = 目标位` **硬贴**，
    /// 观感是「卡死在主角旁边」很生硬。改成**插值跟随**：
    /// 角色一动，云先拖在后面、再慢慢追上来（`SmoothDamp`），停下后自己归位。
    ///
    /// 位置和朝向都插值；朝向只在 <see cref="雷云跟随朝向"/> 打开时才管。
    /// </summary>
    void 维护云()
    {
        if (云 == null) { 生成云(); return; }

        var 云t = 云.transform;
        Vector3 目标位 = 云目标位;

        if (!跟随用插值)
        {
            云t.position = 目标位;                    // 老行为：硬贴
        }
        else if (跟随瞬移距离 > 0f && Vector3.Distance(云t.position, 目标位) > 跟随瞬移距离)
        {
            // 角色传送/复活时别拖一条长尾巴，直接跟上去
            云t.position = 目标位;
            跟随速度 = Vector3.zero;
        }
        else
        {
            float 速度上限 = 跟随最大速度 > 0f ? 跟随最大速度 : Mathf.Infinity;
            云t.position = Vector3.SmoothDamp(云t.position, 目标位, ref 跟随速度,
                                             Mathf.Max(0.01f, 跟随平滑时间), 速度上限, Time.deltaTime);
        }

        if (雷云跟随朝向)
        {
            var 目标旋 = transform.rotation * Quaternion.Euler(雷云旋转欧拉);
            云t.rotation = 朝向插值速度 > 0f
                ? Quaternion.RotateTowards(云t.rotation, 目标旋, 朝向插值速度 * Time.deltaTime)
                : 目标旋;
        }
    }

    /// <summary>雷云的世界位置（闪电的这一端）。**用的是插值后的真实位置**</summary>
    public Vector3 云位置 => 云 != null ? 云.transform.position
                                       : transform.position + 雷云偏移;

    // ============================================================ 出手

    void 出手(NpcInstance 目标)
    {
        if (战斗属性 == null) return;

        出手次数++;

        Vector3 敌位 = 目标.transform.position;
        // 闪电连到敌人**中部**（和闪电链一个口径），不是脚下
        float 敌高 = 特效摆放.量高度(目标.transform, 受击基准敌人高度);
        Vector3 敌中点 = 敌位 + Vector3.up * (敌高 * 0.5f);

        生成闪电(云位置, 敌中点);

        // 受击特效贴在敌人身上，大小随敌人尺寸
        float 缩放 = 按体型算缩放(敌高);
        生成受击特效(敌位, 缩放);

        var 规则 = new AttackSpec(伤害属性, AttackKind.被动神通, false, 技能倍率);
        var 结算目标 = new NpcTarget(目标);
        var 结果 = 结算目标.受到攻击(战斗属性, 规则, this);
        累计伤害 += 结果.伤害;

        if (打印日志)
            Debug.Log("[雷云] 落雷「" + 目标.DisplayName + "」 " + 结果
                + "（缩放 " + 缩放.ToString("0.##") + "｜距离 "
                + Vector3.Distance(云位置, 敌位).ToString("0.##") + "m）", 目标);
    }

    /// <summary>一道从雷云拉到目标中部的闪电（复用「拉伸链」工具）</summary>
    void 生成闪电(Vector3 起点, Vector3 终点)
    {
        特效摆放.生成拉伸链(闪电特效路径, 起点, 终点,
                            闪电粗细, 闪电基准长度, 闪电最小长度,
                            闪电存活, 闪电兜底拉伸轴, 闪电长度补偿, "雷云闪电_" + name);
    }

    /// <summary>贴在敌人身上的受击特效</summary>
    void 生成受击特效(Vector3 敌人脚下, float 缩放)
    {
        特效摆放.生成(受击特效路径, 敌人脚下 + Vector3.up * 受击抬高, 受击旋转欧拉,
                      缩放, 对齐到锚点: true, 存活秒: 受击存活, 名: "雷云受击");
    }

    /// <summary>受击特效缩放 = 敌人模型高度 ÷ 基准高度，夹进上下限</summary>
    float 按体型算缩放(float 敌高)
    {
        if (敌高 <= 0.01f) return 1f;
        return Mathf.Clamp(敌高 / Mathf.Max(0.1f, 受击基准敌人高度), 受击缩放下限, 受击缩放上限);
    }

    // ---- ASCII 别名 ----
    public bool IsActive => 生效中;
    public float TotalDamage => 累计伤害;
    public int CastCount => 出手次数;
}
