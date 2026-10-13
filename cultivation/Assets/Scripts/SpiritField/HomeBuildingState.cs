using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>固定格数、按种类堆叠的建筑仓库。转移必须先校验再扣款；生产没有背包兜底。</summary>
[Serializable]
public sealed class HomeBuildingStorage
{
    public List<HomeStorageSlot> 格 = new List<HomeStorageSlot>();
    public const int 堆叠上限 = 999;
    public void 确保(int count)
    {
        if (格 == null) 格 = new List<HomeStorageSlot>();
        while (格.Count < count) 格.Add(new HomeStorageSlot());
        for (int i = 0; i < 格.Count; i++)
        {
            if (格[i] == null) 格[i] = new HomeStorageSlot();
            HomeAnimalCatalog.清理退役物品(格[i]);
            if (格[i].数量 <= 0) { 格[i].数量 = 0; 格[i].物品id = ""; }
        }
    }
    public int 数量(string id) => 格.Where(s => s != null && s.物品id == id).Sum(s => s.数量);
    public bool 能存(string id, int count, int index = -1)
    {
        if (string.IsNullOrEmpty(id) || count <= 0 || count > 堆叠上限) return false;
        if (index >= 0) return index < 格.Count && (格[index].物品id == id || 格[index].数量 == 0) && 格[index].数量 <= 堆叠上限 - count;
        return 格.Any(s => (s.数量 == 0 || s.物品id == id) && s.数量 <= 堆叠上限 - count);
    }
    public bool 存入(string id, int count, int index = -1)
    {
        if (!能存(id, count, index)) return false;
        var slot = index >= 0 ? 格[index] : 格.FirstOrDefault(s => s.物品id == id && s.数量 <= 堆叠上限 - count) ?? 格.First(s => s.数量 == 0);
        slot.物品id = id; slot.数量 += count; return true;
    }
    public bool 取出(string id, int count)
    {
        if (count <= 0 || 数量(id) < count) return false;
        foreach (var slot in 格)
        {
            if (slot.物品id != id) continue;
            int n = Mathf.Min(count, slot.数量); slot.数量 -= n; count -= n;
            if (slot.数量 == 0) slot.物品id = "";
            if (count == 0) break;
        }
        return true;
    }
}

[Serializable]
public sealed class HomeAnimalState
{
    public string uid = Guid.NewGuid().ToString("N"), 物种id;
    public float 饱食天数, 产物进度, 粪便进度;
}
[Serializable]
public sealed class HomeBreedingState
{
    public string 物种id;
    public float 配对天数, 诞生阈值;
}
[Serializable]
public sealed class HomePenState
{
    public List<HomeAnimalState> 动物 = new List<HomeAnimalState>();
    public List<HomeBreedingState> 繁育 = new List<HomeBreedingState>();
    public HomeBuildingStorage 食槽 = new HomeBuildingStorage(), 收获篮 = new HomeBuildingStorage();
    public void 确保()
    {
        if (动物 == null) 动物 = new List<HomeAnimalState>();
        if (繁育 == null) 繁育 = new List<HomeBreedingState>();
        if (食槽 == null) 食槽 = new HomeBuildingStorage(); if (收获篮 == null) 收获篮 = new HomeBuildingStorage();
        食槽.确保(4); 收获篮.确保(8);
        动物.RemoveAll(a => a == null || a.物种id == HomeAnimalCatalog.已移除物种id);
        繁育.RemoveAll(b => b == null || b.物种id == HomeAnimalCatalog.已移除物种id);
        foreach (var a in 动物) if (string.IsNullOrEmpty(a.uid)) a.uid = Guid.NewGuid().ToString("N");
    }
    public int 占用(HomeAnimalCatalog catalog) => 动物.Sum(a => (catalog ? catalog.找(a.物种id) : null)?.容积 ?? HomeAnimalCatalog.兽栏容量);
    public bool 能放(HomeAnimalDefinition d, HomeAnimalCatalog catalog) => d != null && 占用(catalog) + d.容积 <= HomeAnimalCatalog.兽栏容量;
    public bool 放入(HomeAnimalDefinition d, HomeAnimalCatalog catalog)
    {
        确保(); if (!能放(d, catalog)) return false;
        动物.Add(new HomeAnimalState { 物种id = d.id }); return true;
    }
    void 喂食(HomeAnimalState a, HomeAnimalDefinition d)
    {
        if (a.饱食天数 > .00001f) return;
        if (食槽.取出(d.喜爱食物id, 1)) { a.饱食天数 = 1; return; }
        var other = 食槽.格.FirstOrDefault(s => s.数量 > 0 && HomeAnimalCatalog.是食物(s.物品id));
        if (other != null && 食槽.取出(other.物品id, 1)) a.饱食天数 = .5f;
    }
    static float 新繁育阈值()
    {
        // Weibull(k=2)：等待越久，单位时间内诞生的条件概率越高。阈值入存档，读档不重掷。
        return .75f + 3 * Mathf.Sqrt(-Mathf.Log(Mathf.Max(.000001f, 1 - UnityEngine.Random.value)));
    }
    public void 推进(float days, HomeAnimalCatalog catalog)
    {
        确保(); if (!catalog || days <= 0 || float.IsNaN(days) || float.IsInfinity(days)) return;
        while (days > .000001f)
        {
            float step = Mathf.Min(days, 1f / 24);
            foreach (var a in 动物)
            {
                var d = catalog.找(a.物种id); if (d != null) 喂食(a, d);
            }
            // 分段点包括食物耗尽时刻，大步推进与逐帧推进得到相同的饱食生产时长。
            foreach (var a in 动物) if (a.饱食天数 > .00001f) step = Mathf.Min(step, a.饱食天数);
            days -= step;
            foreach (var d in catalog.动物)
            {
                var pair = 动物.Where(a => a.物种id == d.id && a.饱食天数 > 0).OrderByDescending(a => a.饱食天数).Take(2).ToArray();
                if (pair.Length < 2 || !能放(d, catalog)) continue;
                var b = 繁育.Find(s => s.物种id == d.id);
                if (b == null) { b = new HomeBreedingState { 物种id = d.id }; 繁育.Add(b); }
                if (b.诞生阈值 <= 0) b.诞生阈值 = 新繁育阈值();
                b.配对天数 += Mathf.Min(step, pair[1].饱食天数);
                if (b.配对天数 >= b.诞生阈值)
                { 放入(d, catalog); b.配对天数 = 0; b.诞生阈值 = 新繁育阈值(); }
            }
            foreach (var a in 动物)
            {
                var d = catalog.找(a.物种id); if (d == null) continue;
                float active = Mathf.Min(step, Mathf.Max(0, a.饱食天数));
                a.饱食天数 = Mathf.Max(0, a.饱食天数 - step);
                if (active <= 0) continue;
                if (!string.IsNullOrEmpty(d.产物id))
                {
                    a.产物进度 = Mathf.Min(Mathf.Max(.01f, d.产出间隔天), a.产物进度 + active);
                    if (a.产物进度 + .000001f >= d.产出间隔天 && 收获篮.存入(d.产物id, 1)) a.产物进度 = 0;
                }
                a.粪便进度 = Mathf.Min(Mathf.Max(.01f, d.粪便间隔天), a.粪便进度 + active);
                if (a.粪便进度 + .000001f >= d.粪便间隔天 && 收获篮.存入(HomeAnimalCatalog.粪便id, d.粪便数量)) a.粪便进度 = 0;
            }
        }
    }
}

[Serializable]
public sealed class HomeCompostState
{
    public HomeBuildingStorage 原料 = new HomeBuildingStorage(), 成品 = new HomeBuildingStorage();
    public float 进度天;
    public const int 每份粪便 = 3;
    public const float 每份天数 = .5f;
    public void 确保()
    {
        if (原料 == null) 原料 = new HomeBuildingStorage(); if (成品 == null) 成品 = new HomeBuildingStorage();
        原料.确保(1); 成品.确保(1);
    }
    public void 推进(float days)
    {
        确保(); if (days <= 0) return;
        while (days > 0 && 原料.数量(HomeAnimalCatalog.粪便id) >= 每份粪便 && 成品.能存(HomeAnimalCatalog.肥料id, 1))
        {
            float used = Mathf.Min(days, Mathf.Max(0, 每份天数 - 进度天)); days -= used; 进度天 += used;
            if (进度天 + .000001f < 每份天数) break;
            原料.取出(HomeAnimalCatalog.粪便id, 每份粪便); 成品.存入(HomeAnimalCatalog.肥料id, 1); 进度天 = 0;
        }
    }
}

[Serializable]
public sealed class HomeAlchemyState
{
    public HomeBuildingStorage 主材 = new HomeBuildingStorage(), 辅材 = new HomeBuildingStorage(), 成品 = new HomeBuildingStorage();
    public string 丹方id = "", 在炼丹方id = "", 上次结果 = "";
    public float 剩余天数;
    public int 待产数量;
    public bool 自动炼制;
    public static float 耗时(灵丹定义 d) => .15f + .05f * d.品;
    public void 确保()
    {
        if (主材 == null) 主材 = new HomeBuildingStorage(); if (辅材 == null) 辅材 = new HomeBuildingStorage(); if (成品 == null) 成品 = new HomeBuildingStorage();
        主材.确保(1); 辅材.确保(3); 成品.确保(1);
    }
    public bool 材料齐全(灵丹定义 d)
    {
        确保(); if (d == null || 主材.数量(d.主材id) < d.主材数量) return false;
        return 炼丹炉.解析材料(d.辅材).GroupBy(k => k.Key).All(g => 辅材.数量(g.Key) >= g.Sum(k => k.Value));
    }
    public string 开炉原因()
    {
        var d = 灵丹库.取(丹方id);
        if (d == null) return "尚未选择丹方";
        if (!炼丹炉.已学会(d.id)) return "尚未学会此丹方";
        if (!材料齐全(d)) return "炉内药材不足";
        if (!成品.能存(d.id, Mathf.Max(1, d.产量上限))) return "成品格已满或存放着其他丹药";
        if (!QuestDatabase.取() || !QuestDatabase.取().找物品(d.id)) return "丹药物品不存在";
        return "";
    }
    public void 推进(float days)
    {
        确保(); if (days <= 0) return;
        while (days > 0)
        {
            if (string.IsNullOrEmpty(在炼丹方id))
            {
                if (!自动炼制 || !string.IsNullOrEmpty(开炉原因())) break;
                var d = 灵丹库.取(丹方id);
                主材.取出(d.主材id, d.主材数量);
                foreach (var k in 炼丹炉.解析材料(d.辅材)) 辅材.取出(k.Key, k.Value);
                在炼丹方id = d.id; 剩余天数 = 耗时(d);
                // 成败和产量在扣材料时确定并保存，途中切场景/读档不会重掷。
                待产数量 = UnityEngine.Random.value < d.成功率 ? d.掷产量() : 0;
            }
            float used = Mathf.Min(days, Mathf.Max(0, 剩余天数)); days -= used; 剩余天数 -= used;
            if (剩余天数 > .000001f) break;
            var recipe = 灵丹库.取(在炼丹方id);
            if (待产数量 > 0 && !成品.存入(在炼丹方id, 待产数量)) break;
            上次结果 = 待产数量 > 0 ? (recipe?.名 ?? 在炼丹方id) + " ×" + 待产数量 : "炼制失败，药材已消耗";
            // 自动生产不替玩家完成主线的手动炼丹教学。
            在炼丹方id = ""; 待产数量 = 0; 剩余天数 = 0;
        }
    }
}
