using UnityEngine;

[CreateAssetMenu(menuName = "修仙/物品效果/学丹方")]
public class 学丹方效果 : 物品使用效果
{
    public string 丹方id;
    public override bool 能使用(物品使用请求 请求) => 灵丹库.取(丹方id) != null
        && 灵丹库.取(丹方id).是丹方 && !炼丹炉.已学会(丹方id);
    public override string 不能用原因(物品使用请求 请求) => 灵丹库.取(丹方id) == null ? "丹方不存在" : "已经学会此丹方";
    public override bool 使用(物品使用请求 请求)
    {
        if (!能使用(请求)) return false;
        对话标记.添加(炼丹炉.丹方标记(丹方id));
        请求.面板?.ShowHint("已学会「" + 灵丹库.取(丹方id).名 + "」丹方");
        return true;
    }
}
