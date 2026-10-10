using System.Collections.Generic;
using UnityEngine;

/// <summary>水龙炮跟随锁定目标；小剑阵留在施放点，伤害类别读取定义（被动神通）。</summary>
public class DirectedAbilityRunner : MonoBehaviour
{
    ActiveSkillCaster 玩家;
    ActiveDivineAbility 神通;
    NpcInstance 目标;
    GameObject 特效;
    float 结束时间, 下次伤害;
    Vector3 中心;
    bool 手动;
    public int 结算次数 { get; private set; }

    public void 初始化(ActiveSkillCaster 施法者, ActiveDivineAbility 定义, NpcInstance 锁定,Vector3? 落点=null)
    {
        玩家 = 施法者; 神通 = 定义; 目标 = 锁定;
        手动=落点.HasValue;中心 = 落点 ?? (锁定 != null ? 锁定.transform.position : 玩家.transform.position);
        transform.position = 中心;
        特效 = AbilityVfxUtility.生成(神通.特效资源路径, transform,
            神通.结算方式 == ActiveSkillKind.小剑阵 ? Mathf.Max(.05f, 神通.范围 / 10f) : 1f);
        结束时间 = Time.time + Mathf.Max(.1f, 神通.持续时长);
        下次伤害 = Time.time + Mathf.Max(0f, 神通.首次造成伤害时间);
    }

    void Update()
    {
        if (玩家 == null || !玩家.isActiveAndEnabled || 玩家.生命 == null || 玩家.生命.IsDead || Time.time >= 结束时间)
        { Destroy(gameObject); return; }
        bool 炮 = 神通.结算方式 == ActiveSkillKind.定向水炮;
        if (炮)
        {
            if (!手动 && (目标 == null || 目标.IsDead || Vector3.Distance(玩家.transform.position, 目标.transform.position) > 神通.范围))
            { Destroy(gameObject); return; }
            Vector3 起点 = AbilityVfxUtility.命中点(玩家.transform) + 玩家.transform.forward * .3f;
            AbilityVfxUtility.对准光束(特效, 起点, 手动?中心:AbilityVfxUtility.命中点(目标.transform));
        }
        if (Time.time < 下次伤害 || 玩家.战斗属性 == null) return;
        下次伤害 = Time.time + Mathf.Max(.05f, 神通.伤害间隔);
        var 规则 = new AttackSpec(神通.伤害属性, 神通.攻击类别, false, 神通.伤害倍率);
        if (炮) { if(目标) {CombatDamagePipeline.命中(目标,new CombatHitContext(玩家.战斗属性,规则,玩家,神通.神通id,目标.transform.position,中心-玩家.transform.position,结算次数)); 结算次数++;}else CombatImpactPipeline.接触(神通.神通id,中心,source:玩家); }
        else
        {
            var 已打 = new HashSet<NpcInstance>();
            foreach (var 碰撞 in Physics.OverlapSphere(中心, 神通.范围, 玩家.敌人层, QueryTriggerInteraction.Ignore))
            {
                var 怪 = 碰撞.GetComponentInParent<NpcInstance>();
                if (怪 == null || 怪.IsDead || (!怪.是敌对目标 && 怪 != 目标) || !已打.Add(怪)) continue;
                CombatDamagePipeline.命中(怪,new CombatHitContext(玩家.战斗属性,规则,玩家,神通.神通id,怪.transform.position,Vector3.down,结算次数)); 结算次数++;
            }
        }
    }

    void OnDestroy()
    {
        if (!Application.isPlaying) return;
        foreach (var 剑 in GetComponentsInChildren<AbilityFallingSword>()) 剑.消散();
    }
}
