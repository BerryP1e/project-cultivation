using UnityEngine;

[CreateAssetMenu(menuName = "修仙/物品效果/学坐骑")]
public class 学坐骑效果 : 物品使用效果
{
    public MountDefinition 坐骑;
    public string 坐骑id;
    public MountDefinition 取坐骑()
    {
        if (坐骑 == null) {
            var 库 = PanelDatabase.取();
            if (库 != null && 库.坐骑 != null) 坐骑 = 库.坐骑.Find(x => x != null && x.坐骑id == 坐骑id);
        }
        return 坐骑;
    }
    public override bool 能使用(物品使用请求 请求)
    {
        if (请求.面板 == null) return false;
        请求.面板.EnsureLists();
        return 取坐骑() != null && 坐骑.坐骑id != "mount_julong_01" && !请求.面板.已学坐骑.Contains(坐骑.坐骑id);
    }
    public override string 不能用原因(物品使用请求 请求) => 取坐骑() == null ? "坐骑资料不存在" : "已经掌握此坐骑的御兽之法";
    public override bool 使用(物品使用请求 请求)
    {
        if (!能使用(请求)) return false;
        请求.面板.已学坐骑.Add(坐骑.坐骑id);
        请求.面板.RaiseChanged();
        请求.面板.ShowHint("已学会驾驭「" + 坐骑.DisplayName + "」");
        return true;
    }
}
