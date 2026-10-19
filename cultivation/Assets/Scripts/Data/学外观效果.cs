using UnityEngine;

/// <summary>使用学习物品获得并可立即换上外观。已拥有时拒绝使用且不消耗，
/// 成功后的物品扣除统一由物品使用器处理。普通境界赠物与 P 调试物品共用此入口。
/// </summary>
[CreateAssetMenu(fileName = "学外观_", menuName = "修仙/物品效果/学外观", order = 15)]
public class 学外观效果 : 物品使用效果
{
    [Tooltip("使用后要获得的外观")]
    public AppearanceDefinition 外观;

    [Tooltip("按 id 指定外观（**物品表只填 id 时用这个**）")]
    public string 外观id = "";

    [Tooltip("获得后立刻换上（一般勾上：换上新衣服当然要穿上）")]
    public bool 获得即装备 = true;

    /// <summary>取外观：优先引用，没有就按 id 反查</summary>
    public AppearanceDefinition 取外观()
    {
        if (外观 == null && !string.IsNullOrEmpty(外观id)) 外观 = 能力查找.按id<AppearanceDefinition>(外观id);
        return 外观;
    }

    public override bool 能使用(物品使用请求 请求)
    {
        var 外 = 取外观();
        return 外 != null && 取外观组件(请求) != null && !取外观组件(请求).已拥有(外);
    }

    public override string 不能用原因(物品使用请求 请求)
    {
        var 外 = 取外观();
        if (外 == null) return "效果没配外观（外观id 填错？）";
        var 组件 = 取外观组件(请求);
        if (组件 == null) return "玩家身上没有 玩家外观 组件";
        return "已经拥有「" + 外.DisplayName + "」了";
    }

    public override bool 使用(物品使用请求 请求)
    {
        var 外 = 取外观();
        var 组件 = 取外观组件(请求);
        if (外 == null || 组件 == null) return false;
        if (组件.已拥有(外)) return false;          // 不可重复获得

        组件.获得(外, 获得即装备);
        Debug.Log("[物品] 「" + 请求.物品名 + "」使用后获得外观「" + 外.DisplayName + "」", 外);
        return true;      // 消耗掉这个道具
    }

    static 玩家外观 取外观组件(物品使用请求 请求)
    {
        if (请求.玩家 == null) return null;
        return 请求.玩家.GetComponent<玩家外观>();
    }
}
