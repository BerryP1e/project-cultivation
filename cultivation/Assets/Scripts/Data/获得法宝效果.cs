using UnityEngine;

[CreateAssetMenu(menuName="修仙/物品效果/获得法宝")]
public class 获得法宝效果 : 物品使用效果
{
    public string 法宝id;
    public TreasureDefinition 法宝;
    public TreasureDefinition 取法宝() {
        if(法宝==null)法宝=PanelDatabase.取()?.法宝.Find(t=>t!=null && t.法宝id==法宝id);
        return 法宝;
    }
    public override bool 能使用(物品使用请求 request) {
        if(request.面板==null)return false;
        request.面板.EnsureLists();return 取法宝()!=null && !request.面板.已拥有(取法宝());
    }
    public override string 不能用原因(物品使用请求 request)=>取法宝()==null?"法宝资料不存在":"已与此法宝认主";
    public override bool 使用(物品使用请求 request) {
        if(!能使用(request))return false;
        request.面板.已拥有法宝.Add(法宝id);request.面板.RaiseChanged();
        request.面板.ShowHint("已认主「"+取法宝().DisplayName+"」，可在法宝界面装备");return true;
    }
}
