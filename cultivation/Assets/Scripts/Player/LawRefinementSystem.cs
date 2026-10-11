using System;
using System.Linq;
using UnityEngine;

/// <summary>炉中仍是玩家持有的实例；开始熔炼才原子消耗，动画不承担结算。</summary>
public static class LawRefinementSystem
{
    public static long 熔炼产出(LawFragment law)
    {
        var mains = law.属性.Where(x => x.主属性).ToArray();
        float quality = mains.Length == 0 ? 0 : mains.Average(x => 主属性进度(law, x));
        return (long)Math.Round(LawFragmentCatalog.取().境界[law.境界].熔炼基础精粹 * (1d + quality * LawFragmentCatalog.取().熔炼品质加成));
    }
    public static float 主属性进度(LawFragment law, LawFragment.Roll roll)
    {
        var range = LawFragmentBalance.取区间(roll.属性, law.境界, true, true, LawFragmentCatalog.取().境界[law.境界].主增幅);
        return Mathf.InverseLerp(range.x, range.y, roll.数值);
    }
    public static long 洗练消耗(LawFragment law) => LawFragmentCatalog.取().境界[law.境界].洗练消耗精粹;
    public static bool 可洗练(LawFragment law) => law != null && law.属性.Any(x => x.主属性 && 主属性进度(law, x) < 1 - .00001f);
    public static bool 放入(UIPanelData panel, string id, bool 允许卸下 = false)
    {
        panel.EnsureLists();
        if (!panel.法则碎片.Exists(x => x.id == id) || panel.炉中法则.Contains(id)) return false;
        int slot = panel.法则装备.IndexOf(id);
        if (slot >= 0 && !允许卸下) return false;
        if (slot >= 0) panel.法则装备[slot] = "";
        panel.炉中法则.Add(id); panel.RaiseChanged(); return true;
    }
    public static int 全部放入(UIPanelData panel)
    {
        panel.EnsureLists();
        var ids = panel.法则碎片.Where(x => !panel.法则装备.Contains(x.id) && !panel.炉中法则.Contains(x.id)).Select(x => x.id).ToArray();
        panel.炉中法则.AddRange(ids); if (ids.Length > 0) panel.RaiseChanged(); return ids.Length;
    }
    public static void 取回全部(UIPanelData panel) { panel.炉中法则.Clear(); panel.RaiseChanged(); }
    public static long 熔炼(UIPanelData panel)
    {
        panel.EnsureLists();
        var queued = panel.法则碎片.Where(x => panel.炉中法则.Contains(x.id) && !panel.法则装备.Contains(x.id)).ToArray();
        long yield = queued.Aggregate(0L, (sum, law) => checked(sum + 熔炼产出(law)));
        if (yield == 0 || panel.法则精粹 > long.MaxValue - yield) return 0;
        var ids = queued.Select(x => x.id).ToHashSet();
        panel.法则碎片.RemoveAll(x => ids.Contains(x.id)); panel.炉中法则.Clear(); panel.法则精粹 += yield; panel.RaiseChanged(); return yield;
    }
    public static bool 洗练(UIPanelData panel, LawFragment law, System.Random rng = null)
    {
        if (law == null || !panel.法则碎片.Contains(law) || panel.炉中法则.Contains(law.id) || !可洗练(law)) return false;
        long cost = 洗练消耗(law); if (cost <= 0 || panel.法则精粹 < cost) return false;
        var candidates = law.属性.Where(x => x.主属性 && 主属性进度(law, x) < 1 - .00001f).ToArray();
        var roll = candidates[(rng ?? new System.Random()).Next(candidates.Length)];
        var range = LawFragmentBalance.取区间(roll.属性, law.境界, true, true, LawFragmentCatalog.取().境界[law.境界].主增幅);
        roll.品质 = Mathf.Min(1, 主属性进度(law, roll) + LawFragmentCatalog.取().洗练进度提升);
        roll.数值 = Mathf.Lerp(range.x, range.y, roll.品质); panel.法则精粹 -= cost;
        LawFragmentSystem.重算评分(law); panel.RaiseChanged(); return true;
    }
}
