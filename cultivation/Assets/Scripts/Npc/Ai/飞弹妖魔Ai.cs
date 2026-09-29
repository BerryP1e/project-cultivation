using UnityEngine;

/// <summary>
/// **飞弹妖魔通用基类** —— "会从某个部位吐飞弹"的怪共用它（白鹿精那一套的通用版）。
///
/// ### 用户给它定的 5 个要素，在这里怎么落地
/// | 要素 | 落地 |
/// |---|---|
/// | **1 出生点** | `普攻挂点/神通挂点`（骨骼名）+ `挂点偏移`。**偏移是用量测工具量出来的**，
///   见菜单「修仙 / 怪物 / 量飞弹出生点」—— 因为这些怪的武器是**蒙皮网格不是骨骼**，
///   连玄蜂的尾巴都**没有对应骨骼**，只能量出「最近的骨骼 + 局部偏移」 |
/// | **2 飞行路径** | **直线匀速、不追踪**（`NpcProjectile.设置直线飞行`）：起飞锁定方向，躲开就打不中 |
/// | **3 能否被阻挡** | **不和场景碰撞**（纯粒子弹道）→ 墙挡不住；只检测 `拦截层`
///   （技能神通 / 法宝 / 灵阵 放到那层就能拦下）。**默认 0 = 现在谁都拦不住**（那三样还没做） |
/// | **4 消灭点** | `消灭距离` 米飞出去就**自行淡化消散**（不是被撞掉） |
/// | **5 命中效果** | `特殊` 走特殊伤害（会心判定），`物理` 走暴击判定；命中特效按主题给 |
///
/// ### 两种普攻
/// `普攻是近战 = true` → a1 是**近战横扫**（按动作范围判定，和 `近战妖魔Ai` 一个口径），
/// a2 才是飞弹 —— **蜘蛛精**就是这种混合怪。
/// 混合怪必须给两招设**距离门槛**（`普攻最大出手距离` / `神通最小出手距离`），
/// 否则它会随机挑到近战招、站在 12 米外砍空气。
///
/// ### 动画进度
/// `普攻进度 / 神通进度` 都来自**用户口述的动画百分比**（例：百眼魔君 35.5% / 64%），
/// 不是猜的。出生点偏移也是按**同一个进度**量出来的 —— 换动作要重新量。
/// </summary>
public class 飞弹妖魔Ai : NpcAiDemon
{
    [Header("站位（丢飞弹的，站远处打）")]
    public float 站位距离 = 12f;
    public float 索敌 = 18f;
    public float 脱战 = 26f;

    [Header("飞弹三件套（同一套主题的 弹道 / 命中 / 闪光）")]
    public string 弹道路径 = "";
    public string 命中路径 = "";
    public string 闪光路径 = "";
    public float 飞弹速度 = 18f;
    [Tooltip("飞出这么远就自行淡化消散（消灭点，米）")]
    public float 消灭距离 = 22f;
    [Tooltip("**能被谁拦下**。0 = 谁都拦不住。灵阵/法宝做出来之后把它们放进这一层")]
    public LayerMask 拦截层 = 0;

    [Header("普攻 a1")]
    public bool 有普攻 = true;
    [Tooltip("勾上 = a1 是**近战横扫**（按动作范围判定），a2 才是飞弹")]
    public bool 普攻是近战 = false;
    public string 普攻挂点 = "Bip01 R Hand";
    public Vector3 普攻挂点偏移 = Vector3.zero;
    [Range(0.02f, 0.98f)] public float 普攻进度 = 0.5f;
    public DamageNature 普攻属性 = DamageNature.特殊;
    public float 普攻倍率 = 1f;
    public float 普攻冷却 = 3f;
    public float 普攻弹体缩放 = 0.7f;
    public float 普攻命中缩放 = 1.25f;
    [Tooltip("近战 a1 的出手门槛：**超过这个距离就不出**（0 = 不限）。混合怪必填")]
    public float 普攻最大出手距离 = 0f;
    [Tooltip("近战 a1 的量测峰值进度（见《怪物近战判定》的量测工具）")]
    [Range(0.05f, 0.95f)] public float 普攻近战峰值 = 0.5f;
    [Tooltip("近战 a1 的最前伸（米），只用来算站位")]
    public float 普攻近战前伸 = 2f;

    [Header("主动神通 a2")]
    public bool 有神通 = true;
    public string 神通挂点 = "Bip01 R Hand";
    public Vector3 神通挂点偏移 = Vector3.zero;
    [Range(0.02f, 0.98f)] public float 神通进度 = 0.64f;
    public DamageNature 神通属性 = DamageNature.特殊;
    public float 神通倍率 = 2.2f;
    public float 神通冷却 = 8f;
    public float 神通弹体缩放 = 0.85f;
    public float 神通命中缩放 = 1.6f;
    [Tooltip("飞弹的出手门槛：**不到这个距离就不出**（0 = 不限）。混合怪用它把飞弹留给远处")]
    public float 神通最小出手距离 = 0f;

    [Header("特效包前缀")]
    [Tooltip("留空则 弹道路径/命中路径/闪光路径 要填**完整** Resources 路径")]
    public string 包前缀 = "";

    protected override void 取默认参数()
    {
        确保攻击方式();
        base.取默认参数();

        索敌范围 = 索敌;
        脱战范围 = 脱战;
        攻击距离 = Mathf.Max(1.2f, 站位距离);
        转向速度 = 900f;          // 丢飞弹的，转身要利索
        奔跑倍率 = 1f;

        if (攻击方式 == null || 攻击方式.Length < 3) return;

        if (有普攻) 布普攻(攻击方式[0]);
        else 攻击方式[0].动作名 = "";

        if (有神通) 布神通(攻击方式[1]);
        else 攻击方式[1].动作名 = "";

        攻击方式[2].动作名 = "";      // Attack3 不用
    }

    void 布普攻(NpcAttackConfig 招)
    {
        招.动作名 = "Attack1";
        招.攻击类别 = AttackKind.普通攻击;
        招.技能倍率 = 普攻倍率;
        招.冷却 = 普攻冷却;
        招.出手最小距离 = 0f;
        招.出手最大距离 = 普攻最大出手距离;

        if (普攻是近战)
        {
            // 近战横扫：按动作范围判定（和 近战妖魔Ai 同一口径），命中特效走基础物理
            招.是施法 = false;
            招.伤害属性 = DamageNature.物理;
            招.用网格判定 = false;
            招.出手进度 = 普攻近战峰值;
            招.判定结束进度 = 0.98f;
            招.命中特效路径 = "";
            清飞弹(招);
            return;
        }

        // 飞弹
        招.是施法 = true;
        招.伤害属性 = 普攻属性;
        招.出手进度 = 普攻进度;
       招.弹体缩放 = 普攻弹体缩放;
        招.命中特效缩放 = 普攻命中缩放;
        招.子弹速度 = 飞弹速度;
        招.最大飞行距离 = 消灭距离;
        招.拦截层 = 拦截层;
        招.挂点 = 普攻挂点;
        招.挂点偏移 = 普攻挂点偏移;
        招.闪光特效路径 = 拼(闪光路径);
        招.子弹特效路径 = 拼(弹道路径);
        招.命中特效路径 = 拼(命中路径);
    }

    void 布神通(NpcAttackConfig 招)
    {
        招.动作名 = "Attack2";
        招.是施法 = true;
        招.伤害属性 = 神通属性;
        招.攻击类别 = AttackKind.主动神通;
        招.技能倍率 = 神通倍率;
        招.冷却 = 神通冷却;
        招.出手进度 = 神通进度;
        招.出手最小距离 = 神通最小出手距离;
        招.出手最大距离 = 0f;
        招.弹体缩放 = 神通弹体缩放;
        招.命中特效缩放 = 神通命中缩放;
        招.子弹速度 = 飞弹速度;
        招.最大飞行距离 = 消灭距离;
        招.拦截层 = 拦截层;
        招.挂点 = 神通挂点;
        招.挂点偏移 = 神通挂点偏移;
        招.闪光特效路径 = 拼(闪光路径);
        招.子弹特效路径 = 拼(弹道路径);
        招.命中特效路径 = 拼(命中路径);
    }

    string 拼(string 路径)
        => string.IsNullOrEmpty(路径) || string.IsNullOrEmpty(包前缀) ? 路径 : 包前缀 + 路径;

    /// <summary>把**飞弹专属**的配置清干净（近战招不能带弹道）</summary>
    static void 清飞弹(NpcAttackConfig 配置)
    {
        配置.子弹特效路径 = "";
        配置.闪光特效路径 = "";
        配置.挂点 = "";
        配置.挂点偏移 = Vector3.zero;
    }

    // ---- 常用前缀，子类直接拼 ----
    protected const string 飞弹库 = "特效/飞弹/ProjectilesFX/VFX_Prefabs/";
    protected const string 弹道子目录 = "Projectiles_Particles/VFX_";
    protected const string 命中的子目录 = "Impacts/VFX_";
    protected const string 闪光子目录 = "Flashes/VFX_";
}
