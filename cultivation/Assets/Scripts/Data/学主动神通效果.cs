using UnityEngine;

/// <summary>**使用后获得一门主动神通**（不可重复习得）。</summary>
[CreateAssetMenu(fileName = "学主动神通_", menuName = "修仙/物品效果/学主动神通", order = 12)]
public class 学主动神通效果 : 物品使用效果
{
    [Tooltip("使用后要获得的主动神通")]
    public ActiveDivineAbility 神通;

    [Tooltip("按 id 指定主动神通（**物品表只填 id 时用这个**）")]
    public string 神通id = "";

    /// <summary>取神通：优先引用，没有就按 id 反查</summary>
    public ActiveDivineAbility 取神通()
    {
        if (神通 == null && !string.IsNullOrEmpty(神通id)) 神通 = 能力查找.按id<ActiveDivineAbility>(神通id);
        return 神通;
    }

    public override bool 能使用(物品使用请求 请求)
    {
        var a = 取神通();
        return a != null && 请求.面板 != null && !请求.面板.已获得主动(a);
    }

    public override string 不能用原因(物品使用请求 请求)
    {
        var a = 取神通();
        return a == null ? "效果没配主动神通（神通id 填错？）" : ("已经获得「" + a.DisplayName + "」了");
    }

    public override bool 使用(物品使用请求 请求)
    {
        var a = 取神通();
        if (a == null || !能使用(请求)) return false;
        if (!请求.面板.获得主动(a)) return false;
        Debug.Log("[物品] 获得主动神通「" + a.DisplayName + "」", a);
        return true;
    }
}
