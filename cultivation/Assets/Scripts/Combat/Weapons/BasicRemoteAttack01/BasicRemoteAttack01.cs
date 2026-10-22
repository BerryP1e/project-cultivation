using System;
using UnityEngine;

/// <summary>
/// 功法「太虚炼气诀」提供的普攻方法（功法表 `普攻方法id = basic_remoteattack_01`）。
///
/// ## 行为（按策划说明）
///
/// · 左键逐次出招；未锁定朝鼠标点，锁定后攻击目标，邪眼可自动连打。
/// · 与玄霄/沧澜共用四式：原火球、大火球、单颗陨石、背后六球外绕齐射。
/// · 四式分别在40%、40%、43%、50%出手，伤害全部属于特殊属性普通攻击。
/// · 动作结束即可接下一式，无额外段间冷却；攻速继续缩放动作时间。
/// · A1/A2从右手出膛；A3从目标上方落下；A4从身后两侧出发。
/// · 飞行：**持续追踪**，直到命中 / 锁定单位超出**神识范围** / 超过 **8 秒**
/// · 命中：对锁定单位造成**特殊普通攻击**
/// · 阻挡：**无法被场景阻挡**（飞弹零物理）；只检测「拦截层」——
///   技能神通 / 法宝 / 灵阵 放那一层就能把它拦下来
///
/// ## 和其它普攻方法的区别
///
/// `basic_sword_01` 是「飞剑自己飞出去穿目标」，**玩家角色不播动画**；
/// 这个使用完整的四段人物动作，所以它依赖
/// <see cref="PlayerAnimationController.播动作"/>（普攻/施法共用那套）。
///
/// ## 术语
///
/// 伤害目标统一叫**锁定单位**（见 <see cref="ICombatTarget"/>）——
/// 以后 NPC 也可能是召唤单位帮玩家打，所以别写死"玩家/敌人"。
/// </summary>
public class BasicRemoteAttack01 : MonoBehaviour
{
    [Header("功法接线")]
    [Tooltip("本普攻方法在功法表「普攻方法id」里的标识")]
    public string 方法id = "basic_remoteattack_01";

    [Header("攻击动作")]
    [Tooltip("出手时播的角色动作。人形片段，会自动重定向到玩家骨架")]
    public AnimationClip 普攻动作;

    [Header("太虚四式（与玄霄、沧澜共用动作）")]
    public string[] 动作序列={"技能动作/普攻_远程_01","技能动作/玄霄雷诀/A2","技能动作/玄霄雷诀/A3","技能动作/玄霄雷诀/A4"};
    public float[] 出手节点={.4f,.4f,.43f,.5f};
    public float[] 四式倍率={1f,1.25f,1.6f,.35f};
    public float 第二式弹体倍率=1.35f;
    public float 第三式范围=2.2f;
    public int 出手序号 {get;private set;}
    public int 当前式 {get;private set;}
    public int 本次发射数 {get;private set;}
    public int 下次式=>出手序号%4;
    public float 本次出手节点=>出手节点!=null&&出手节点.Length>当前式?出手节点[当前式]:出手进度;
    AnimationClip 本次动作;

    [Tooltip("在动画的百分之多少处生成飞弹（0.4 = 40%）")]
    [Range(0.05f, 0.95f)]
    public float 出手进度 = 0.4f;

    [HideInInspector] // 保留旧场景序列化字段；三套法术普攻不再读取段间冷却。
    public float 基础冷却 = 0f;

    [Header("出生点 = 右手骨骼末端")]
    [Tooltip("从哪根骨骼出膛。默认右手中指末端（= 右手骨骼的最末端）")]
    public HumanBodyBones 出生骨骼 = HumanBodyBones.RightMiddleDistal;

    [Tooltip("骨骼找不到时的退路")]
    public HumanBodyBones 备用出生骨骼 = HumanBodyBones.RightHand;

    [Tooltip("沿骨骼自身坐标系再加一点偏移（让飞弹离手一点，别嵌进模型里）")]
    public Vector3 出生偏移 = new Vector3(0f, 0f, 0.15f);

    [Header("飞弹外观")]
    public string 弹道特效路径 = "CombatVFX/Projectiles/ProjectilesFX/VFX_Prefabs/Projectiles_Particles/VFX_Priest_Projectile_Only";
    public string 闪光特效路径 = "CombatVFX/Projectiles/ProjectilesFX/VFX_Prefabs/Flashes/VFX_Priest_Flash";

    [Tooltip("弹体缩放")]
    public float 弹体缩放 = 0.65f;

    [Tooltip("闪光存活（秒）")]
    public float 闪光特效存活 = 0.8f;

    [Tooltip("共享命中特效目录中的Impact_Spark缩放倍率")]
    public float 命中特效缩放 = 1.25f;

    [Header("飞弹飞行")]
    [Tooltip("飞行速度（米/秒）")]
    public float 飞弹速度 = 18f;

    [Tooltip("追踪转向速率（度/秒）。越小越容易被绕开")]
    public float 追踪转向速度 = 540f;

    [Tooltip("追踪最长持续多久（秒）就放弃并消散")]
    public float 最长追踪时间 = 8f;

    [Tooltip("能被哪些层拦下来（技能神通 / 法宝 / 灵阵）。0 = 谁都拦不住")]
    public LayerMask 拦截层 = 0;

    [Header("神识范围（追踪终止距离）")]
    [Tooltip("神识为 0 时的基础范围（米）")]
    public float 基础神识范围 = 4f;

    [Tooltip("每 1 点神识增加多少米")]
    public float 每点神识范围 = 0.8f;

    [Tooltip("范围上限")]
    public float 神识范围上限 = 40f;

    [Header("伤害规则")]
    [Tooltip("策划要求：命中造成**特殊普通攻击**")]
    public DamageNature 伤害属性 = DamageNature.特殊;
    public AttackKind 攻击类别 = AttackKind.普通攻击;
    public float 技能倍率 = 1f;

    [Header("按键")]
    public KeyCode 攻击键 = KeyCode.Mouse1;

    [Tooltip("**自动出手**：只要锁定了单位、冷却好了，就自己一直打，不用按键")]
    public bool 自动出手 = true;

    [Tooltip("按键也还能触发（自动出手关掉后就是纯手动）。按住 = 连发，只点一下 = 单发")]
    public bool 按住连发 = true;

    [Header("引用（留空自动找）")]
    public PlayerCombatStats 玩家战斗属性;
    public NpcTargeting 目标管理器;
    public PlayerAnimationController 动画;

    [Header("调试")]
    public bool 打印战斗日志 = true;

    // ============================================================ 对外状态

    /// <summary>**锁定单位** —— 当前要打的目标（没锁定就是 null）</summary>
    public ICombatTarget 锁定单位
    {
        get
        {
            var npc = 目标管理器 != null ? 目标管理器.LockedNpc : null;
            if (npc == null || npc.IsDead) return null;
            if(缓存目标id!=npc.GetInstanceID()){缓存目标id=npc.GetInstanceID();缓存=null;}
            return 缓存 ??= new NpcTarget(npc);
        }
    }

    /// <summary>缓存适配器，避免每次攻击都 new（锁定的目标没变就复用）</summary>
    NpcTarget 缓存;
    int 缓存目标id;

    /// <summary>攻速系数（来自玩家属性汇总）。1 = 标准</summary>
    public float 攻速系数 => 玩家战斗属性 != null
        ? Mathf.Max(0.1f, 玩家战斗属性.当前属性[AttributeType.AttackSpeed])
        : 1f;

    /// <summary>动作自身控制出招节奏，结束后不追加等待。</summary>
    public float 实际冷却 => 0f;

    /// <summary>冷却还剩几秒</summary>
    public float 冷却剩余 => Mathf.Max(0f, 下次可出手时间 - Time.time);

    /// <summary>现在能不能出手</summary>
    public bool 可以出手 => enabled && !BasicAttackGate.OtherAttackRunning(this) && isActiveAndEnabled && !(GetComponent<PlayerVitals>()?.IsDead??false) && !(GetComponent<演出锁>()?.正在锁??false)
        && !UiEscRegistry.SceneInputBlocked && !(动画 != null && 动画.施法占用中) && (手动请求 || 锁定单位 != null) && 冷却剩余 <= 0f && !出手动作中;
    bool 手动请求,手动攻击;
    Vector3 手动点;
    public bool 手动出手(Vector3 point)
    {
        if((GetComponent<PlayerVitals>()?.IsDead ?? false) || (GetComponent<演出锁>()?.正在锁 ?? false))return false;
        手动请求=true;手动点=transform.position+Vector3.ClampMagnitude(point-transform.position,神识范围);
        try{return 出手();}finally{手动请求=false;}
    }

    /// <summary>正在做攻击动作</summary>
    public bool 出手动作中 { get; private set; }

    /// <summary>上一次出手的结果（调试用）</summary>
    public AttackResult 上次结果 { get; private set; }

    /// <summary>飞弹命中锁定单位时触发（接飘字 / 音效）</summary>
    public event Action<ICombatTarget, AttackResult> 命中时;

    /// <summary>神识范围 = 追踪终止距离（超出就放弃）</summary>
    public float 神识范围 => Mathf.Min(神识范围上限, 基础神识范围 + 当前神识 * 每点神识范围);

    float 当前神识 => 玩家战斗属性 != null ? 玩家战斗属性.当前神识 : 0f;

    float 下次可出手时间;
    bool 本轮已出弹;
    public void 中断未出手普攻()
    {
        if(!出手动作中)return;
        出手动作中=false;本轮已出弹=true;
        下次可出手时间=Time.time+实际冷却;
    }

    // ---- ASCII 别名 ----
    public ICombatTarget LockedUnit => 锁定单位;
    public float AttackSpeedFactor => 攻速系数;
    public float ActualCooldown => 实际冷却;
    public float CooldownRemaining => 冷却剩余;
    public bool CanAttack => 可以出手;

    void Awake()
    {
        if (玩家战斗属性 == null) 玩家战斗属性 = GetComponent<PlayerCombatStats>();
        if (目标管理器 == null) 目标管理器 = GetComponent<NpcTargeting>();
        if (动画 == null) 动画 = GetComponent<PlayerAnimationController>();
        if (动画 == null) 动画 = GetComponentInChildren<PlayerAnimationController>();

        // 【为什么要有这段】本组件是 PlayerAbilityLoader 在**运行时反射 AddComponent** 装上的，
        // 所以拿不到 Inspector 里拖的引用 —— 动作片段只能靠 Resources 自己加载。
        // 想换动作：换 Assets/resources/技能动作/普攻_远程_01.anim，或者运行时给 普攻动作 赋值。
        if (普攻动作 == null)
            普攻动作 = Resources.Load<AnimationClip>("技能动作/普攻_远程_01");
        if (普攻动作 == null)
            Debug.LogWarning("[basic_remoteattack_01] 找不到动作 Assets/resources/技能动作/普攻_远程_01.anim，" +
                             "缺少动作时不接受出手", this);
    }

    void Update()
    {
        // 目标换了就丢掉缓存的适配器，保证 锁定单位 跟着走
        var npc = 目标管理器 != null ? 目标管理器.LockedNpc : null;
        int id = npc != null ? npc.GetInstanceID() : 0;
        if (id != 缓存目标id) { 缓存目标id = id; 缓存 = null; }

        // 闪避打断普攻时丢弃尚未发出的子弹，不能借闪避动作的进度出弹。
        if (出手动作中 && ((GetComponent<PlayerVitals>()?.IsDead??false) || 动画 && (动画.施法占用中 || 动画.当前动作!=本次动作)))
        {
            出手动作中 = false; 本轮已出弹 = true;
            下次可出手时间 = Time.time + 实际冷却;
        }
        // 动作播到 出手进度 → 出弹
        if (出手动作中 && !本轮已出弹)
        {
            float 进度 = 动画 != null ? 动画.动作进度 : 1f;
            if (进度 >= 本次出手节点 || !(动画 != null && 动画.动作播放中))
            {
                本轮已出弹 = true;
                生成飞弹();
            }
        }

        if (出手动作中 && (动画 == null || !动画.动作播放中))
        {
            出手动作中 = false;
            下次可出手时间 = Time.time + 实际冷却;
        }

        // 输入
        // 出手：**自动出手**开着就自己一直打；关掉才看按键
        if (可以出手 && 自动出手 && 目标管理器 && 目标管理器.自动索敌可用) 出手();
    }

    bool AttackPressed() => 按住连发 ? Input.GetKey(攻击键) : Input.GetKeyDown(攻击键);

    /// <summary>出手一次（外部系统 / 测试可以直接调）</summary>
    public bool 出手()
    {
        if (!可以出手) return false;
        int phase=下次式;
        var clip=phase==0?普攻动作:动作序列!=null&&动作序列.Length>phase?Resources.Load<AnimationClip>(动作序列[phase]):null;
        clip=WeaponCastingAnimations.Resolve(gameObject,phase,clip);
        if(!clip || !动画)return false;
        if(phase==2&&!FallingSpellRunner.普攻陨石资源就绪())return false;
        if(!动画.播动作(clip,攻速系数))return false;
        当前式=phase;出手序号++;本次动作=clip;本次发射数=0;
        手动攻击=手动请求;
        var point=手动攻击?手动点:目标管理器.LockedNpc.transform.position;
        GetComponent<PlayerController>()?.对准攻击方向(point-transform.position);

        出手动作中 = true;
        本轮已出弹 = false;
        当前锁定 = 锁定单位;

        return true;
    }

    ICombatTarget 当前锁定;

    // ============================================================ 出弹

    void 生成飞弹()
    {
        var target=手动攻击?null:当前锁定;
        if(!手动攻击&&(target==null||target.已倒下))return;
        if(!玩家战斗属性)return;
        int phase=当前式;
        float damage=技能倍率*(四式倍率!=null&&四式倍率.Length>phase?四式倍率[phase]:1);
        Vector3 point=手动攻击?手动点:target.判定点;
        if(phase==2){
            var root=target?.根;var npc=root?root.GetComponent<NpcInstance>():null;
            var ground=手动攻击?手动点:target.根.position;
            if(FallingSpellRunner.尝试普攻陨石(this,npc,ground,damage,第三式范围))本次发射数=1;
            return;
        }
        var from=取出生点();
        生成装饰特效(闪光特效路径,from,point-from,闪光特效存活,phase==1?第二式弹体倍率:1);
        int count=phase==3?6:1;
        // 回调捕获本枚弹的段数、倍率和目标，后续连段与换锁不会改变已经发出的弹。
        for(int i=0;i<count;i++){
            float side=i%2==0?-1:1;int row=i/2;
            var origin=phase==3?transform.TransformPoint(new Vector3(side*(.7f+row*.4f),1.1f+row*.6f,-.9f-row*.1f)):from;
            float size=弹体缩放*(phase==1?第二式弹体倍率:1);
            var bolt=NpcProjectile.发射(弹道特效路径,origin,point,飞弹速度,size,0,拦截层,null);
            bolt.name="Taixu_A"+(phase+1)+"_Bolt"+i;
            bolt.检测体素=true;
            if(target!=null){bolt.追踪终止距离=神识范围;bolt.最长追踪时间=最长追踪时间;bolt.设置追踪飞行(target.根,飞弹速度,追踪转向速度);}
            if(phase==3){
                var control=transform.TransformPoint(new Vector3(side*(3.2f+row*.5f),1.4f+row*.6f,-1.9f));
                var outward=transform.TransformPoint(new Vector3(side*(2.3f+row*.45f),1.3f+row*.6f,.6f));
                bolt.设置外绕出膛(control,outward,.42f,.08f+row*.035f);
            }
            bolt.到达时+=()=>{
                if(!this||!enabled||GetComponent<PlayerVitals>()?.IsDead==true)return;
                if(bolt.击中体素||target==null)环境命中(bolt.命中点,bolt.飞行朝向,phase,damage);
                else {CombatImpactPipeline.接触(方法id,bolt.命中点,playEffect:false,source:this);结算命中(target,bolt,phase,damage);}
            };
            本次发射数++;
        }
    }
    void 环境命中(Vector3 point,Vector3 direction,int phase,float damage)
    {
        CombatImpactPipeline.接触("basic_remoteattack_01",point,visualScale:命中特效缩放,source:this,direction:-direction);
        var seen=new System.Collections.Generic.HashSet<NpcInstance>();
        foreach(var c in Physics.OverlapSphere(point,.6f,~0,QueryTriggerInteraction.Ignore))
        {var npc=c.GetComponentInParent<NpcInstance>();if(npc && !npc.IsDead && npc.是敌对目标 && seen.Add(npc))记录命中(new NpcTarget(npc),new CombatHitContext(玩家战斗属性,new AttackSpec(DamageNature.特殊,AttackKind.普通攻击,false,damage),this,方法id,point,-direction,phase,playHitVfx:false));}
    }

    /// <summary>飞弹到达 → 对锁定单位结算一次</summary>
    void 结算命中(ICombatTarget 目标, NpcProjectile 飞,int phase,float damage)
    {
        if (目标 == null || 目标.已倒下) return;

        var 规则 = new AttackSpec(DamageNature.特殊,AttackKind.普通攻击,false,damage);
        Vector3 命中点 = 飞 != null ? Vector3.Lerp(飞.命中点, 目标.判定点, 0.85f) : 目标.判定点;
        Vector3 朝向 = 飞 != null ? -飞.飞行朝向 : Vector3.forward;
        记录命中(目标,new CombatHitContext(玩家战斗属性,规则,this,方法id,命中点,朝向,phase,visualScale:命中特效缩放));
    }

    public void 记录命中(ICombatTarget target,CombatHitContext context)
    {var result=CombatDamagePipeline.命中(target,context);记录结果(target,result);}
    public void 记录结果(ICombatTarget target,AttackResult result)
    {上次结果=result;命中时?.Invoke(target,result);}
    void OnDisable(){中断未出手普攻();手动请求=手动攻击=false;}

    /// <summary>右手骨骼末端（拿不到就用备用骨骼，再不行用胸口）</summary>
    public Vector3 取出生点()
    {
        var an = 动画 != null ? 动画.GetComponentInChildren<Animator>() : GetComponentInChildren<Animator>();
        if (an != null)
        {
            var 骨 = an.GetBoneTransform(出生骨骼) ?? an.GetBoneTransform(备用出生骨骼);
            if (骨 != null) return 骨.TransformPoint(出生偏移);
        }
        return transform.position + Vector3.up * 1.2f + transform.forward * 0.3f;
    }

    GameObject 生成装饰特效(string 路径, Vector3 位置, Vector3 朝向, float 存活, float 缩放)
    {
        if (string.IsNullOrEmpty(路径)) return null;
        var prefab = Resources.Load<GameObject>(路径);
        if (prefab == null)
        {
            Debug.LogWarning("[basic_remoteattack_01] 找不到特效：" + 路径, this);
            return null;
        }
        Vector3 前 = 朝向.sqrMagnitude > 0.0001f ? 朝向.normalized : Vector3.forward;
        // 竖直方向要换 up 轴，否则 Quaternion.LookRotation(up, up) 是退化调用（会报错 + 朝向随机）
        Quaternion 旋转 = Mathf.Abs(Vector3.Dot(前, Vector3.up)) > 0.999f
            ? Quaternion.LookRotation(前, Vector3.forward)
            : Quaternion.LookRotation(前, Vector3.up);
        var fx = Instantiate(prefab, 位置, 旋转);
        fx.name = "PlayerFx_" + prefab.name;
        if (!Mathf.Approximately(缩放, 1f)) fx.transform.localScale *= 缩放;
        Destroy(fx, Mathf.Max(0.2f, 存活));
        return fx;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 神识范围);
        var 目标 = 锁定单位;
        if (目标 != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(取出生点(), 目标.判定点);
            Gizmos.DrawWireSphere(目标.判定点, 0.4f);
        }
    }
}
