using UnityEngine;

/// <summary>
/// 功法父类。对应 lore/功法父类.txt：所有的功法使用此父类。
/// </summary>
[CreateAssetMenu(fileName = "GongFa_", menuName = "修仙/功法父类", order = 1)]
public class GongFaDefinition : ScriptableObject, IPanelEntry
{
    [Header("lore 字段 · 基本信息")]
    [Tooltip("功法名称")]
    public string 功法名称 = "新功法";

    [Tooltip("功法id，全局唯一")]
    public string 功法id = "";

    [Tooltip("功法品阶")]
    public QualityTier 功法品阶 = QualityTier.凡品;

    [Header("lore 字段 · 修炼条件")]
    [Tooltip("功法修炼门槛：可开始修炼的最低境界序号")]
    public int 修炼门槛 = 0;

    [Tooltip("功法最高可修炼境界：可修炼到的最高境界序号")]
    public int 最高可修炼境界 = 9;

    [Tooltip("功法难度等级：每提升一级境界所需的吐纳经验")]
    [Min(1)]
    public long 难度等级 = 100;

    [Header("lore 字段 · 普攻")]
    [Tooltip("功法提供的普攻方法id。指向具体的普攻实现（技能表/方法名）")]
    public string 普攻方法id = "";

    [Header("lore 字段 · 每次升级提供的增益")]
    [Tooltip("功法每升一级，玩家获得的属性增益。26 项，与玩家基本属性一一对应")]
    public AttributeSet 每级增益 = new AttributeSet();

    [Header("成长曲线（用户 2026-09-30 定：要指数，不要线性）")]
    [Tooltip("按等级**指数**累加每级增益（推荐开）。\n\n" +
             "关掉 = 老行为 `增益 × 等级`（线性）。\n\n" +
             "【为什么必须开】老版本玩家是线性、怪物是几何 —— 越到后期玩家越碾压。\n" +
             "打开后玩家和怪物共用 LevelCurve 一条曲线（每大境界 ×25 的质变台阶）。")]
    public bool 指数成长 = true;

    [Tooltip("曲线「档内」增长相对全局曲线的倍率。1 = 完全跟随（推荐）。\n" +
             "想让这门功法档内涨得慢/快就调它 —— 但**别只调玩家这边**，\n" +
             "那正是老版本把握不住平衡的原因。")]
    public float 成长档内倍率 = 1f;

    [Tooltip("勾上 = 这门功法的增益**不随等级变**（固定值）。\n" +
             "给「练了只提供普攻方法、不给属性」的特殊功法用。")]
    public bool 固定增益 = false;

    [Header("UI 用")]
    public Sprite 图标;
    [TextArea(2, 8)]
    public string 介绍 = "";

    /// <summary>
    /// **按等级算出的总增益**（26 项）。
    ///
    /// ## 老行为（线性，已弃用）
    ///
    /// <code>总增益 = 每级增益 × 等级</code>
    ///
    /// 问题：怪物是**几何**增长、玩家是**线性**增长 —— 90 级时玩家攻 ×90、怪只涨 ×47，
    /// 于是越到后期玩家越碾压（这就是用户说的「功法增益不合理」）。
    ///
    /// ## 新行为（指数，默认）
    ///
    /// <code>总增益 = 每级增益 × (LevelCurve.倍率(等级) − 1)</code>
    ///
    /// ## 新行为（指数，默认）
    ///
    /// <code>总增益 = 每级增益 × (LevelCurve.倍率(等级) − 1)</code>
    ///
    /// ## 为什么是 `倍率 − 1`
    ///
    /// `每级增益` 的语义是「**升一级**拿到的量」。整条曲线在 L 级涨到 `倍率(L)` 倍，
    /// 那么「L 级累计拿到了多少」= `倍率(L) - 1` 份 ——
    /// L=1 时正好是 **1 份**（不是 0 份，也不是 1.5 份）。
    ///
    /// 【踩过的坑】一开始用的是 `累加倍率`（= 倍率 − 1）但传的是**总量曲线**，
    /// 又试过直接乘 `倍率`：前者让 1 级玩家拿到 **0** 份增益（新手直接变弱），
    /// 后者让 1 级玩家拿到 **1.5 份**（凭空多一级）。
    /// 两者在 1~2 级几乎一样，**所以前期不容易发现**，到面板数值对不上才暴露。
    /// 判据很简单：`GetTotalBonus(1)` 必须 == `每级增益 × 1`。
    ///
    /// ## 为什么和怪物共用一条曲线
    ///
    /// 见 <see cref="LevelCurve"/>：玩家 90 级 / 1 级的攻击比 ≈ 怪 90 级 / 1 级的比，
    /// 这样「塔第 N 层」对玩家来说永远是同一档难度，不会越爬越白给。
    /// </summary>
    public AttributeSet GetTotalBonus(int level)
    {
        if (每级增益 == null) return new AttributeSet();
        int 级 = Mathf.Max(0, level);

        if (固定增益) return 每级增益.ScaledBy(级 > 0 ? 1f : 0f);

        if (!指数成长) return 每级增益.ScaledBy(级);      // 旧行为，留作对照

        return 每级增益.ScaledBy(LevelCurve.累加倍率(级));
    }

    /// <summary>1 级时的总增益（UI 里「练了这门功法能加多少」用）</summary>
    public AttributeSet 一级增益 => GetTotalBonus(1);

    /// <summary>这条曲线在某个等级上的倍率（调试 / UI 展示用）</summary>
    public float 取成长倍率(int level) => LevelCurve.累加倍率(Mathf.Max(0, level));

    // ---- IPanelEntry ----
    public string DisplayName => string.IsNullOrEmpty(功法名称) ? name : 功法名称;
    public string DisplayDescription => 介绍;
    public QualityTier DisplayTier => 功法品阶;
    public Sprite DisplayIcon => 图标;

    void OnValidate()
    {
        if (string.IsNullOrEmpty(功法id)) 功法id = name;
        if (难度等级 < 1) 难度等级 = 1;
        if (最高可修炼境界 < 修炼门槛) 最高可修炼境界 = 修炼门槛;
    }
}
