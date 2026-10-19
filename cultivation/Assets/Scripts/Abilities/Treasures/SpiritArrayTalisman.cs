using UnityEngine;

/// <summary>浮世万象的法宝入口。编排和生成物统一交给灵阵执行器。</summary>
public static class SpiritArrayTalisman
{
    public const string Id="treasure_spirit_array_talisman";
    public static bool Equipped(UIPanelData data)=>data&&data.当前法宝&&data.当前法宝.法宝id==Id&&data.已拥有(data.当前法宝);
    public static bool Cast(GameObject player,UIPanelData data,Vector3? aim=null)
    {
        if(!player||!Equipped(data))return false;
        var driver=player.GetComponent<ActiveSkillCaster>();if(!driver)return false;
        driver.面板数据=data;
        if(!driver.生命)driver.生命=player.GetComponent<PlayerVitals>();
        if(!driver.战斗属性)driver.战斗属性=player.GetComponent<PlayerCombatStats>();
        if(!driver.目标管理器)driver.目标管理器=player.GetComponent<NpcTargeting>();
        if(!driver.动画)driver.动画=player.GetComponentInChildren<PlayerAnimationController>();
        var definition=data.灵阵.Find(d=>d&&d.灵阵id==data.当前灵阵编排);
        if(!definition){data.ShowHint("请先右键浮世万象选择施放阵列");return false;}
        var caster=player.GetComponent<SpiritArrayCaster>();if(!caster)caster=player.AddComponent<SpiritArrayCaster>();
        if(caster.TreasureCooldown>0){data.ShowHint("浮世万象尚在冷却");return false;}
        if(driver.动画&&driver.动画.施法占用中){data.ShowHint("当前动作尚未结束");return false;}
        if(!caster.TryCast(driver,definition,aim,out string error)){data.ShowHint(error);return false;}
        caster.BeginTreasureCooldown(Mathf.Max(0,data.当前法宝.冷却时间));return true;
    }
}
