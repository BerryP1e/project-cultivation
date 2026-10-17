using UnityEngine;

/// <summary>主线退出后的新开局物资。仅在创建新档时写入，不反复补发。</summary>
public static class HomeFacilityStarter
{
    public static void 准备新档(SaveData data)
    {
        foreach (var id in new[] { "item_home_alchemy_hall", "item_home_merit_hall", "item_home_technique_hall", "item_home_deacon_hall", "item_home_forge_hall", "item_gongfa_taixu_lianqi", "item_recipe_dan_juqi", "item_muzhuang" }) data.背包物品.Add(id);
        for (int i = 0; i < 4; i++) data.背包物品.Add("item_lingtian_kaituo");
        for (int i = 0; i < 8; i++) data.背包物品.Add("item_home_expansion");
        for (int i = 0; i < 12; i++) data.背包物品.Add("item_seed_shengling");
        for (int i = 0; i < 6; i++) data.背包物品.Add("item_seed_ninglu");
        data.法宝.Add(TowerRealmTreasure.法宝id);
        data.当前法宝 = TowerRealmTreasure.法宝id;
        data.宗门贡献 = 100;
    }
}
