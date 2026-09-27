using UnityEngine;

/// <summary>
/// **使用后学会一门功法**。已经学会的→不能用（用户要求："无法重复习得"）。
/// 学会之后如果主角还没有当前功法，会自动把这门设为当前修炼的功法
/// （不然新号学完功法，属性/普攻方法还都是空的）。
/// </summary>
[CreateAssetMenu(fileName = "学功法_", menuName = "修仙/物品效果/学功法", order = 11)]
public class 学功法效果 : 物品使用效果
{
    [Tooltip("使用后要学会的功法")]
    public GongFaDefinition 功法;

    [Tooltip("按 id 指定功法（**物品表只填 id 时用这个**）。\n" +
             "引用为空的运行时按它反查；生成器会把引用也填上，两处都填更保险")]
    public string 功法id = "";

    /// <summary>取功法：优先引用，没有就按 id 反查（并缓存回来）</summary>
    public GongFaDefinition 取功法()
    {
        if (功法 == null && !string.IsNullOrEmpty(功法id)) 功法 = 能力查找.按id<GongFaDefinition>(功法id);
        return 功法;
    }

    public override bool 能使用(物品使用请求 请求)
    {
        var g = 取功法();
        return g != null && 请求.面板 != null && !请求.面板.已学(g);
    }

    public override string 不能用原因(物品使用请求 请求)
    {
        var g = 取功法();
        return g == null ? "效果没配功法（功法id 填错？）" : ("已经学会「" + g.DisplayName + "」了");
    }

    public override bool 使用(物品使用请求 请求)
    {
        var g = 取功法();
        if (g == null || !能使用(请求)) return false;
        if (!请求.面板.学会(g)) return false;
        if (请求.面板.当前功法 == null) 请求.面板.当前功法 = g;
        Debug.Log("[物品] 学会功法「" + g.DisplayName + "」", g);
        return true;
    }
}
