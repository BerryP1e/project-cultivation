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

    [Header("用动画里自带的弹体（箭魔那种：动作里本来就有一支箭）")]
    [Tooltip("模型里**跟着动画动的那支箭**的节点名（SkinnedMeshRenderer）。\n" +
             "填了 = 发射时取**它在那一刻的真实世界位置**当出生点，并在飞行期间把它藏起来 ——\n" +
             "看起来就是「动画里那支箭真的飞出去了」；动作结束后再放回来（下次拉弓还要用）。\n" +
             "留空 = 老做法（用 挂点 + 挂点偏移）。\n" +
             "**为什么更好**：用户 2026-09-29 提的 ——「他射箭的动画中其实已经有了箭这个组件……\n" +
             "能否让动画结束了箭也不消失，让它真正的飞出去？」。\n" +
             "这样出生点**永远和动画里那支箭重合**，不用靠量出来的骨骼+偏移去近似。")]
    public string 动画弹体节点名 = "";

    SkinnedMeshRenderer 动画弹体;
    bool 动画弹体已藏;
    Vector3 动画弹体出膛点;
    bool 动画弹体出膛点有效;

    protected override void Start()
    {
        base.Start();
        状态变化 += 看状态变化;
        if (string.IsNullOrEmpty(动画弹体节点名)) return;
        var t = 找挂点(动画弹体节点名);
        动画弹体 = t != null ? t.GetComponent<SkinnedMeshRenderer>() : null;
        if (动画弹体 == null)
            Debug.LogWarning("[AI] " + name + " 找不到「动画弹体」节点「" + 动画弹体节点名
                + "」，这一只用挂点+偏移出膛", this);
    }

    /// <summary>攻击结束 → 把动画里那支箭放回来（拉弓还要用）</summary>
    void 看状态变化(NpcAiBase 谁, NpcAiState 旧, NpcAiState 新)
    {
        if (旧 != NpcAiState.攻击) return;
        if (动画弹体 == null || !动画弹体已藏) return;
        动画弹体.enabled = true;
        动画弹体已藏 = false;
    }

    /// <summary>
    /// 发射前先量出「动画里那支箭」此刻的世界中心，并把它藏起来。
    /// **必须在 base 之前量** —— 藏掉之后就算不出来了。
    /// </summary>
    protected override void 生成子弹(NpcAttackConfig 配置, AttackSpec 规则)
    {
        if (动画弹体 != null)
        {
            动画弹体出膛点 = 算动画弹体世界中心();
            动画弹体出膛点有效 = true;
            动画弹体.enabled = false;
            动画弹体已藏 = true;
        }
        base.生成子弹(配置, 规则);
        动画弹体出膛点有效 = false;
    }

    protected override Vector3 取出膛点(NpcAttackConfig 配置 = null)
        => 动画弹体出膛点有效 ? 动画弹体出膛点 : base.取出膛点(配置);

    /// <summary>
    /// 用「动画里那支箭」当弹体时**不要前移**。
    ///
    /// 基类那个前移（"推出自己碰撞体"）是为了让 **QFX 那种飞行中一碰就炸的实体弹丸**
    /// 不在施法者身上自爆；我们这支箭是**纯网格、不和场景碰撞**，不需要。
    /// 实测：不移的话箭会比动画里那支箭**凭空往前跳 1.075 米**（= 自己碰撞体半径 + 0.35）。
    /// </summary>
    protected override float 出膛余量() => 动画弹体 != null ? 0f : base.出膛余量();

    /// <summary>
    /// 「动画里那支箭」此刻的世界中心 —— **自己手动蒙皮算**：
    /// `骨骼.localToWorldMatrix × bindpose × 顶点` 加权，再取顶点 AABB 中心。
    ///
    /// 为什么不能直接用它自己的包围盒：蒙皮网格的 `Renderer.bounds` **不反映真实蒙皮范围**
    /// （实测那支箭真实长 1.9 米，`bounds.size` 只有 0.14 米 —— 见踩坑 B40/B47）。
    /// 顶点才 246 个，一次发射算一遍，开销可以忽略。
    /// </summary>
    Vector3 算动画弹体世界中心()
    {
        var sm = 动画弹体;
        var m = sm != null ? sm.sharedMesh : null;
        if (m == null) return 取出膛点(null);

        var vs = m.vertices;
        var bw = m.boneWeights;
        var bp = m.bindposes;
        var 骨 = sm.bones;
        bool 有权重 = bw != null && bw.Length == vs.Length && bp != null && 骨 != null && 骨.Length > 0;

        bool 有 = false;
        var 盒 = new Bounds();
        for (int i = 0; i < vs.Length; i++)
        {
            Vector3 w;
            if (有权重)
            {
                w = Vector3.zero;
                float 总 = 0f;
                for (int k = 0; k < 4; k++)
                {
                    int bi = k == 0 ? bw[i].boneIndex0 : k == 1 ? bw[i].boneIndex1 : k == 2 ? bw[i].boneIndex2 : bw[i].boneIndex3;
                    float wt = k == 0 ? bw[i].weight0 : k == 1 ? bw[i].weight1 : k == 2 ? bw[i].weight2 : bw[i].weight3;
                    if (wt <= 0.0001f || bi < 0 || bi >= 骨.Length || 骨[bi] == null) continue;
                    w += (骨[bi].localToWorldMatrix * bp[bi]).MultiplyPoint3x4(vs[i]) * wt;
                    总 += wt;
                }
                if (总 < 0.0001f) w = sm.transform.TransformPoint(vs[i]);
            }
            else w = sm.transform.TransformPoint(vs[i]);

            if (!有) { 盒 = new Bounds(w, Vector3.zero); 有 = true; }
            else 盒.Encapsulate(w);
        }
        return 有 ? 盒.center : 取出膛点(null);
    }

    protected override void 取默认参数()
    {
        确保攻击方式();
        base.取默认参数();

        索敌范围 = 索敌;
        脱战范围 = 脱战;
        攻击距离 = Mathf.Max(1.2f, 站位距离);
        转向速度 = 900f;          // 丢飞弹的，转身要利索
        奔跑倍率 = 1f;

        // ★【别去动 `攻击期间锁定朝向`】—— 保持默认 true（= 射击时**不**把身体纠向玩家）。
        //
        // 用户 2026-09-29 明确要求：「**对于箭魔这种，在射击时不要朝向玩家就好了**」。
        // 原因是这套施法/射箭动作**自带它自己的站位和转体**（弓手本来就是侧身站、
        // 身体随拉弓转，只有持弓的手指向目标）。AI 每帧把身体纠向玩家 =
        // **跟动作抢方向盘**，看起来就是"瞄的方向和朝向对不上"。
        //
        // 【走过的弯路·留个记性】我上一版恰恰是把它设成 false（每帧纠向玩家），
        // 测出来 transform 是 0° 了、也发的图，但那是**把动作的转体整个抵消掉**换来的，
        // 手感是错的。用户一句话点醒：**该让动作自己演，别纠**。
        攻击期间锁定朝向 = true;

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
