using UnityEngine;

[CreateAssetMenu(menuName = "修仙/法则/开启宝箱")]
public sealed class LawChestUseEffect : 物品使用效果
{
    [Range(0, 8)] public int 境界;
    public override bool 能使用(物品使用请求 r) => r.面板 && r.面板.物品数量(r.物品) > 0 && LawFragmentCatalog.取();
    public override bool 使用(物品使用请求 r)
    {
        if (!能使用(r)) return false;
        LawFragmentSystem.获得(r.面板, LawFragmentSystem.生成(境界)); return true;
    }
}
