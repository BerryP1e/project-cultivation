using UnityEngine;

/// <summary>沧澜寒渊录四式：冰震、冰刺、路径冰波、玩家周围冰环；均为特殊属性普攻。</summary>
public class BasicFrostSpike01 : MonoBehaviour
{
    [Tooltip("本普攻方法在功法表「普攻方法id」里的标识。境界页靠它反查提供这个普攻的功法")]
    public string 方法id = "basic_frostspike_01";

    // ============================================================ 动作

    [Header("出手动作")]
    [HideInInspector] // 保留旧场景字段，实际四式片段由动作序列指定。
    public string 普攻动作名 = "普攻_远程_01"; // A1资源名，四式实际使用动作序列

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
    [HideInInspector] // 四式固定特殊属性普攻；旧场景值不覆盖普攻规则。
    public DamageNature 伤害属性 = DamageNature.特殊;

    [HideInInspector]
    public AttackKind 攻击类别 = AttackKind.普通攻击;

    [Tooltip("技能倍率（占位 1，按策划给的数值调）")]
    public float 技能倍率 = 1f;

    // ============================================================ 特效

    [Header("冰刺特效")]
    [Tooltip("冰刺特效的 Resources 路径（相对 Assets/resources，不带扩展名）。策划指定 frost-shock")]
    public string 冰刺特效路径 = "CombatVFX/CombatMagic/frost-fx/frost-shock";

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


    [Header("沧澜四式（复用玄霄动作）")]
    public string[] 动作序列={"技能动作/普攻_远程_01","技能动作/玄霄雷诀/A2","技能动作/玄霄雷诀/A3","技能动作/玄霄雷诀/A4"};
    public float[] 出手节点={.4f,.4f,.43f,.5f};
    const string FrostRoot="CombatVFX/CombatMagic/frost-fx/";
    public string 第二式特效路径=FrostRoot+"frost-spike",冰波特效路径=FrostRoot+"frost-wave",冰环特效路径=FrostRoot+"frost-ring";
    public float 冰波长度=12f,冰波宽度=2.6f,冰波推进时长=.9f;
    public float 第四式范围=5f,冰环基准半径=5f;
    public LayerMask 敌人层=~0;

    public ICombatTarget 锁定单位 => 目标管理器&&目标管理器.LockedNpc&&!目标管理器.LockedNpc.IsDead?new NpcTarget(目标管理器.LockedNpc):null;
    public float 攻速系数 => 玩家战斗属性?Mathf.Max(.1f,玩家战斗属性.当前属性[AttributeType.AttackSpeed]):1f;
    public float 实际冷却 => 基础冷却/攻速系数;
    public float 冷却剩余 => Mathf.Max(0,下次可出手时间-Time.time);
    public bool 可以出手 => enabled&&!UiEscRegistry.SceneInputBlocked&&!(GetComponent<PlayerVitals>()?.IsDead??false)
        &&!(动画&&动画.施法占用中)&&!出手动作中&&冷却剩余<=0&&(手动请求||锁定单位!=null);
    public bool 出手动作中 {get;private set;}
    public int 出手序号 {get;private set;}
    public int 当前式 {get;private set;}
    public int 本次命中数 {get;private set;}
    public float 本次出手节点 => 出手节点!=null&&出手节点.Length>当前式?出手节点[当前式]:出手进度;
    public int 下次式 => 出手序号%4;
    public float 下次范围 => 下次式==3?第四式范围:下次式==2?冰波宽度*.5f:.6f;
    public AttackSpec 普攻规则 => new AttackSpec(DamageNature.特殊,AttackKind.普通攻击,false,技能倍率);
    AnimationClip 本次动作;
    NpcInstance 本次目标;
    bool 本轮已出手,手动请求,手动攻击;
    Vector3 手动点;
    float 下次可出手时间;
    void Awake()=>解析引用();
    void OnEnable()=>解析引用();
    void OnDisable(){出手动作中=false;本轮已出手=true;手动请求=手动攻击=false;}
    void 解析引用(){
        if(!玩家战斗属性)玩家战斗属性=GetComponent<PlayerCombatStats>();
        if(!目标管理器)目标管理器=GetComponent<NpcTargeting>();
        if(!动画)动画=GetComponent<PlayerAnimationController>()??GetComponentInChildren<PlayerAnimationController>();
    }
    void Update(){
        解析引用();
        if(出手动作中&&(GetComponent<PlayerVitals>()?.IsDead??false)){出手动作中=false;本轮已出手=true;return;}
        if(出手动作中&&动画&&动画.当前动作!=本次动作){出手动作中=false;本轮已出手=true;下次可出手时间=Time.time+实际冷却;}
        if(出手动作中&&!本轮已出手&&动画&&动画.动作进度>=本次出手节点){本轮已出手=true;召冰刺();}
        if(出手动作中&&(!动画||!动画.动作播放中)){出手动作中=false;本轮已出手=true;下次可出手时间=Time.time+实际冷却;}
        if(可以出手&&(自动出手||(按住连发?Input.GetKey(攻击键):Input.GetKeyDown(攻击键))))出手();
    }
    public bool 手动出手(Vector3 point){
        if(目标管理器&&目标管理器.LockedNpc)return false;
        if(Vector3.Distance(point,transform.position)>PlayerManualAim.SenseRange(gameObject))return false;
        手动点=point;手动请求=true;try{return 出手();}finally{手动请求=false;}
    }
    public bool 出手(){
        解析引用();if(!可以出手||动作序列==null||动作序列.Length!=4)return false;
        当前式=下次式;本次动作=Resources.Load<AnimationClip>(动作序列[当前式]);
        if(!动画||!本次动作||!动画.播动作(本次动作,攻速系数))return false;
        本次目标=目标管理器?目标管理器.LockedNpc:null;手动攻击=手动请求;
        出手序号++;本次命中数=0;出手动作中=true;本轮已出手=false;return true;
    }
    public void 召冰刺(){
        if(!玩家战斗属性)return;
        var target=本次目标&&!本次目标.IsDead?new NpcTarget(本次目标):null;
        // Fourth stroke is anchored on the player even if the selected enemy has died.
        if(当前式!=3&&target==null&&!手动攻击)return;
        var point=手动攻击?手动点:target!=null?target.根.position:transform.position;
        if(当前式==2){
            var direction=point-transform.position;direction.y=0;
            if(direction.sqrMagnitude<.001f)direction=transform.forward;direction.y=0;
            var runner=new GameObject("沧澜寒渊录_冰波路径").AddComponent<FrostWaveAttackRunner>();
            runner.命中回调=n=>本次命中数++;
            runner.初始化(玩家战斗属性,transform.position,direction.normalized,冰波长度,冰波宽度,冰波推进时长/攻速系数,敌人层,普攻规则,本次目标,冰波特效路径);
            return;
        }
        if(当前式==3){
            point=transform.position;生成落地特效(冰环特效路径,point,Vector3.zero,第四式范围/冰环基准半径,"沧澜寒渊录_A4_冰环");
            VoxelCombatDamage.Ellipsoid(point,第四式范围,.325f);
            命中范围(point,第四式范围);return;
        }
        float scale=target!=null?按体型算缩放(target):1f;
        if(当前式==0){
            var go=生成落地特效(冰刺特效路径,point+Vector3.up*冰刺抬高,冰刺特效旋转欧拉,scale,"沧澜寒渊录_A1_冰震");
            if(go&&!特效摆放.对齐子节点到(go,对齐参考子节点,point+Vector3.up*冰刺抬高))特效摆放.只对齐水平(go,point);
        }else 生成落地特效(第二式特效路径,point+Vector3.up*冰刺抬高,Vector3.zero,scale,"沧澜寒渊录_A2_冰刺");
        VoxelCombatDamage.Ellipsoid(point,.6f,.325f);
        if(手动攻击)命中范围(point,.6f);else 命中(target.取Npc());
    }
    void 命中范围(Vector3 center,float radius){
        var seen=new System.Collections.Generic.HashSet<NpcInstance>();
        foreach(var c in Physics.OverlapSphere(center,radius,敌人层,QueryTriggerInteraction.Ignore)){
            var n=c.GetComponentInParent<NpcInstance>();if(!n||n.IsDead||(!n.是敌对目标&&n!=本次目标)||!seen.Add(n))continue;命中(n);
        }
        if(本次目标&&!本次目标.IsDead&&!seen.Contains(本次目标)&&Vector3.Distance(本次目标.transform.position,center)<=radius)命中(本次目标);
    }
    void 命中(NpcInstance npc){
        if(!npc||npc.IsDead)return;var result=new NpcTarget(npc).受到攻击(玩家战斗属性,普攻规则,this);
        if(result.命中)本次命中数++;if(打印战斗日志)Debug.Log("[沧澜寒渊录] A"+(当前式+1)+" "+npc.DisplayName+" "+result,this);
    }
    GameObject 生成落地特效(string path,Vector3 point,Vector3 rotation,float scale,string name){
        var prefab=Resources.Load<GameObject>(path);float life=存活按特效自动?特效摆放.量特效总时长(prefab,冰刺特效存活)*存活倍率:冰刺特效存活;
        if(存活上限>0)life=Mathf.Min(life,存活上限);
        return 特效摆放.生成(path,point,rotation,scale,对齐到锚点:false,存活秒:Mathf.Max(.1f,life),名:name);
    }
    float 按体型算缩放(ICombatTarget target)=>Mathf.Clamp(特效摆放.量高度(target.根,基准敌人高度)/Mathf.Max(.1f,基准敌人高度),缩放下限,缩放上限);
    public float AttackSpeedFactor=>攻速系数;
    public bool CanAttack=>可以出手;
}
