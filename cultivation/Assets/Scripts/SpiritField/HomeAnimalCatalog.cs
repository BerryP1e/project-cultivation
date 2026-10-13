using System;
using UnityEngine;

[Serializable]
public sealed class HomeAnimalDefinition
{
    public string id, 名, 幼崽id, 产物id, 喜爱食物id;
    [Min(1)] public int 容积 = 1, 粪便数量 = 1;
    [Min(.01f)] public float 产出间隔天 = 1, 粪便间隔天 = .5f;
    public GameObject 模型;
    public AnimationClip 待机, 行走;
    public float 半径 = .3f;
}

/// <summary>家畜使用独立的展示预制体，保留原骨架动画，不带野生 NPC 的战斗、血条和跟随 AI。</summary>
[CreateAssetMenu(menuName = "修仙/家园动物目录")]
public sealed class HomeAnimalCatalog : ScriptableObject
{
    public const int 兽栏容量 = 10;
    public const string 粪便id = "item_home_manure", 肥料id = "item_home_fertilizer";
    // 定稿移除的内容只用于旧档清理，不参与目录、兑换或重建。
    public const string 已移除物种id = "maolv", 已移除幼崽id = "item_home_cub_maolv";
    public static void 清理退役物品(HomeStorageSlot slot)
    {
        if (slot != null && slot.物品id == 已移除幼崽id) { slot.物品id = ""; slot.数量 = 0; }
    }
    public HomeAnimalDefinition[] 动物 = new HomeAnimalDefinition[0];
    static HomeAnimalCatalog cache;
    public static HomeAnimalCatalog 取() => cache ? cache : (cache = Resources.Load<HomeAnimalCatalog>("Home/Animals"));
    public HomeAnimalDefinition 找(string id) => Array.Find(动物, d => d != null && d.id == id);
    public HomeAnimalDefinition 按幼崽(string id) => Array.Find(动物, d => d != null && d.幼崽id == id);
    public static bool 是食物(string id)
    {
        foreach (var plant in 灵植库.全部) if (plant.产物id == id) return true;
        return false;
    }
}
