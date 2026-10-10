using UnityEngine;

/// <summary>水龙炮：全状态43%左手出水，定姿引导，停止发射后渐淡并完成收招。</summary>
[DefaultExecutionOrder(100)]
public sealed class WaterDragonSkillRunner : MonoBehaviour
{
    public const string 动作资源路径="技能动作/神通施法/水龙炮_施法";
    public const float 出手进度=.43f;
    public const float 水柱粗细=.05f;
    public const float 消散时长=.65f;
    enum Phase { Windup,Active,Fade,Tail }
    Phase phase;
    ActiveSkillCaster caster;
    ActiveDivineAbility ability;
    NpcInstance target;
    PlayerAnimationController actions;
    AnimationClip motion;
    Transform hand;
    GameObject effect;
    ParticleSystem[] particles;
    ParticleSystemRenderer[] renderers;
    Color[] colors;
    MaterialPropertyBlock block;
    Vector3? manual;
    float end,nextDamage,fadeStart,started;
    bool holding,owns;
    public bool 已释放 { get; private set; }
    public bool 正在消散 => phase==Phase.Fade;
    public int 结算次数 { get; private set; }
    public GameObject 当前特效 => effect;

    public void 初始化(ActiveSkillCaster player,ActiveDivineAbility definition,NpcInstance locked,Vector3? point=null)
    {
        caster=player;ability=definition;target=locked;manual=point;actions=player.动画;
        motion=Resources.Load<AnimationClip>(动作资源路径);
        if(actions && motion && actions.播动作(motion,1f))owns=actions.占用施法(this);
        if(!owns){Destroy(gameObject);return;}
        var animator=actions.animator;
        if(animator&&animator.isHuman)hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
        started=Time.time;phase=Phase.Windup;
    }

    void Update()
    {
        if(phase==Phase.Fade)
        {
            float alpha=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time-fadeStart)/消散时长));
            Fade(alpha);
            if(alpha<=0){if(effect)Destroy(effect);phase=Phase.Tail;}
            return;
        }
        if(phase==Phase.Tail)
        {
            if(!actions||!actions.动作播放中||actions.当前动作!=motion||Time.time-fadeStart>3f)
            {Release();Destroy(gameObject);}
            return;
        }
        if(!ValidTarget() || !actions || actions.当前动作!=motion || !actions.动作播放中)
        {Finish();return;}
        if(phase==Phase.Windup)
        {
            if(actions.动作进度>=出手进度){actions.定住动作(true);holding=true;StartBeam();}
            else if(Time.time-started>4f)Finish();
            return;
        }
        if(Time.time>=end){Finish();return;}
        if(Time.time<nextDamage||!caster.战斗属性)return;
        nextDamage=Time.time+Mathf.Max(.05f,ability.伤害间隔);
        if(target)
        {
            CombatDamagePipeline.命中(target,new CombatHitContext(caster.战斗属性,new AttackSpec(ability.伤害属性,ability.攻击类别,false,ability.伤害倍率),caster,ability.神通id,AbilityVfxUtility.命中点(target.transform),target.transform.position-caster.transform.position,结算次数));
            结算次数++;
        }
        else if(manual.HasValue)CombatImpactPipeline.接触(ability.神通id,manual.Value,source:caster);
    }

    void LateUpdate()
    {
        if(phase!=Phase.Active||!effect||!caster)return;
        // 手腕坐标轴因Avatar而异，直接用实际左手位置作为炮口，不套右手偏移。
        Vector3 origin=hand?hand.position:AbilityVfxUtility.命中点(caster.transform)+caster.transform.forward*.3f;
        var destination=manual??(target?AbilityVfxUtility.命中点(target.transform):origin+caster.transform.forward);
        AbilityVfxUtility.对准光束(effect,origin,destination,水柱粗细);
    }

    bool ValidTarget()
    {
        if(!caster||!caster.isActiveAndEnabled||!caster.生命||caster.生命.IsDead)return false;
        if(manual.HasValue)return true;
        return target&&!target.IsDead&&(ability.范围<=0||Vector3.Distance(caster.transform.position,target.transform.position)<=ability.范围);
    }
    void StartBeam()
    {
        phase=Phase.Active;已释放=true;
        effect=AbilityVfxUtility.生成(ability.特效资源路径,transform,1);
        end=Time.time+Mathf.Max(.1f,ability.持续时长);nextDamage=Time.time+Mathf.Max(0,ability.首次造成伤害时间);
        if(!effect){Finish();return;}
        particles=effect.GetComponentsInChildren<ParticleSystem>(true);
        renderers=effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
        colors=new Color[renderers.Length];block=new MaterialPropertyBlock();
        for(int i=0;i<renderers.Length;i++)
        {
            var material=renderers[i].sharedMaterial;
            colors[i]=material&&material.HasProperty("_TintColor")?material.GetColor("_TintColor"):Color.white;
        }
        LateUpdate();
    }
    void Finish()
    {
        if(phase==Phase.Fade||phase==Phase.Tail)return;
        if(holding&&actions&&actions.当前动作==motion)actions.定住动作(false);
        holding=false;fadeStart=Time.time;phase=Phase.Fade;
        if(!effect){Release();Destroy(gameObject);return;}
        // 冻结最后一帧水柱方向；收手和目标离开都不会带着尾散突然扫向别处。
        foreach(var p in particles)if(p)p.Stop(false,ParticleSystemStopBehavior.StopEmitting);
        if(!ValidTarget()){Release();}
    }
    void Fade(float alpha)
    {
        if(!effect)return;
        // 粒子拖尾材质虽出现在sharedMaterials[1]，却不是可寻址的submesh。
        // 使用Renderer级属性块，让主体和拖尾一同渐淡，避免index1越界。
        for(int i=0;i<renderers.Length;i++)if(renderers[i])
        {
            renderers[i].GetPropertyBlock(block);block.SetColor("_TintColor",colors[i]*alpha);renderers[i].SetPropertyBlock(block);block.Clear();
        }
        var scale=effect.transform.localScale;scale.x=scale.y=水柱粗细*Mathf.Lerp(.12f,1,alpha);effect.transform.localScale=scale;
    }
    void Release()
    {
        if(!actions)return;
        if(holding&&actions.当前动作==motion)actions.定住动作(false);
        holding=false;if(owns)actions.释放施法(this);owns=false;
    }
    void OnDisable()=>Release();
}
