using UnityEngine;

[CreateAssetMenu(menuName="修仙/物品效果/获得武器")]
public sealed class 获得武器效果 : 物品使用效果
{
    public string 武器id;
    public WeaponDefinition 武器;
    public WeaponDefinition 取武器(){if(!武器)武器=WeaponCatalog.找(武器id);return 武器;}
    public override bool 能使用(物品使用请求 request)=>request.面板&&取武器()&&!request.面板.已拥有武器(取武器());
    public override string 不能用原因(物品使用请求 request)=>!取武器()?"武器资料不存在":"已经拥有这把武器";
    public override bool 使用(物品使用请求 request)
    {
        if(!能使用(request)||!request.面板.获得武器(取武器()))return false;
        if(!request.面板.当前武器)request.面板.装备武器(取武器());
        request.面板.ShowHint("获得武器  "+取武器().DisplayName);return true;
    }
}
