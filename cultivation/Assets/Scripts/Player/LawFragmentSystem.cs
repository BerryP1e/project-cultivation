using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>独立宝箱掉落、法则页实例生成、六槽装备与统一属性修正。</summary>
public static class LawFragmentSystem
{
    public static readonly AttributeType[] 副属性池 = {
        AttributeType.MaxHealth, AttributeType.Attack, AttributeType.Defense, AttributeType.MaxSpirit,
        AttributeType.MoveSpeed, AttributeType.CritChance, AttributeType.CritDamage, AttributeType.CritResist,
        AttributeType.Insight, AttributeType.InsightDamage, AttributeType.InsightResist, AttributeType.Dodge,
        AttributeType.IgnoreDodge, AttributeType.CooldownReduction, AttributeType.AttackSpeed,
        AttributeType.HealthRegen, AttributeType.SpiritRegen, AttributeType.HealingBonus,
        AttributeType.BasicAttackBonus, AttributeType.BasicAttackReduction, AttributeType.ActiveSpellBonus,
        AttributeType.ActiveSpellReduction, AttributeType.PassiveSpellBonus, AttributeType.PassiveSpellReduction };
    static System.Random random = new System.Random();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { random = new System.Random(); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Connect() { CombatDamagePipeline.击杀 -= OnKill; CombatDamagePipeline.击杀 += OnKill; }
    static void OnKill(CombatHitEvent hit)
    {
        if (!(hit.目标 is NpcTarget target)) return;
        var player = hit.上下文.攻击属性 as PlayerCombatStats;
        if (!player && hit.上下文.攻击属性 is NpcInstance ally)
        {
            var spirit = ally.GetComponent<FormationSpirit>();
            var owner = spirit && spirit.管理器 ? spirit.管理器.主人 : null;
            if (owner) player = owner.GetComponent<PlayerCombatStats>();
        }
        if (!player) return;
        var npc = target.单位;
        if (!npc || !npc.定义 || npc.类型 != NpcKind.妖魔 || npc.定义.死亡后立即重生 || npc.GetComponent<FormationSpirit>()) return;
        var cfg = LawFragmentCatalog.取(); if (!cfg) return;
        int level = npc.击杀计入等级 > 0 ? npc.击杀计入等级 : npc.当前补正等级 > 0 ? Mathf.RoundToInt(npc.当前补正等级) : npc.定义.境界;
        int tier = 掉落境界(level, random.NextDouble(), random.NextDouble(), cfg);
        if (tier < 0) return;
        var panel = player.GetComponent<PlayerCultivation>()?.面板数据 ?? UnityEngine.Object.FindObjectOfType<UIPanelData>();
        if (panel) panel.给物品(cfg.境界[tier].宝箱);
    }
    public static int 掉落境界(int level, double dropRoll, double upgradeRoll, LawFragmentCatalog cfg)
    {
        if (level <= 0 || dropRoll >= cfg.宝箱掉率) return -1;
        // 升境界是掉箱成功后的条件概率；未掉箱时不能另发高境界宝箱。
        int tier = Mathf.Clamp((level - 1) / 10, 0, 8);
        return upgradeRoll < cfg.高一境界概率 ? Mathf.Min(8, tier + 1) : tier;
    }
    public static LawFragment 生成(int tier, System.Random rng = null)
    {
        var cfg = LawFragmentCatalog.取(); rng = rng ?? random; tier = Mathf.Clamp(tier, 0, 8);
        var realm = cfg.境界[tier]; var names = cfg.称呼.Where(x => x.主属性.Length == realm.主属性数).ToArray();
        var name = names[rng.Next(names.Length)];
        var law = new LawFragment { id = "law_" + Guid.NewGuid().ToString("N"), 境界 = tier, 称呼id = name.id };
        foreach (var a in name.主属性) law.属性.Add(Roll(a, tier, true, true, realm.主增幅, rng));
        var pool = 副属性池.Except(name.主属性).ToList();
        for (int i = 0; i < realm.副属性数; i++)
        {
            int index = rng.Next(pool.Count); var a = pool[index]; pool.RemoveAt(index);
            bool big = rng.NextDouble() < cfg.副属性大增幅概率;
            var roll = Roll(a, tier, false, big, big ? realm.副增幅 : realm.小增幅, rng);
            law.属性.Add(roll);
        }
        重算评分(law);
        return law;
    }
    public static void 重算评分(LawFragment law)
    {
        var realm = LawFragmentCatalog.取().境界[law.境界];
        float quality = 0, weight = 0;
        foreach (var r in law.属性) { float w = r.主属性 ? 2 : 1; quality += r.品质 * w; weight += w; }
        law.品质 = quality / weight;
        law.评分 = Mathf.RoundToInt(100 * Mathf.Pow(2.25f, law.境界) * (realm.主属性数 + realm.副属性数 * .35f) * (.5f + .5f * law.品质));
    }
    static LawFragment.Roll Roll(AttributeType a, int tier, bool main, bool big, Vector2 range, System.Random rng)
    {
        float quality = LawFragmentBalance.抽品质(rng.NextDouble(), LawFragmentCatalog.取().高值稀有指数);
        range = LawFragmentBalance.取区间(a, tier, main, big, range);
        return new LawFragment.Roll { 属性 = a, 主属性 = main, 大增幅 = big, 品质 = quality, 数值 = Mathf.Lerp(range.x, range.y, quality) };
    }
    /// <summary>百分制属性的小固定值按小数存储：.01 = 一个百分点。</summary>
    public static float 固定值基准(AttributeType a)
    {
        switch (a) {
            case AttributeType.MaxHealth: return 100;
            case AttributeType.Attack: case AttributeType.Defense: return 10;
            case AttributeType.MaxSpirit: return 50;
            case AttributeType.MoveSpeed: return 1;
            case AttributeType.HealthRegen: case AttributeType.SpiritRegen: return 1;
            case AttributeType.Sense: return 10;
            default: return .05f;
        }
    }
    /// <summary>只为缺乏初始值的属性提供增幅换算尺度，不凭空增加基准值本身。</summary>
    public static float 增幅基准(AttributeType a)
    {
        if (a == AttributeType.AttackSpeed) return 1;
        if (a == AttributeType.CritDamage || a == AttributeType.InsightDamage) return 1.5f;
        return 固定值基准(a);
    }
    public static void 获得(UIPanelData panel, LawFragment law)
    {
        if (panel.法则碎片.Exists(x => x.id == law.id)) return;
        panel.法则碎片.Add(law); panel.RaiseChanged(); ToastUI.获得法则(law);
    }
    public static LawFragment 取槽(UIPanelData panel, int slot)
    { return panel.法则碎片.Find(x => x.id == panel.法则装备[slot]); }
    public static bool 装备(UIPanelData panel, string id, int slot)
    {
        if (slot < 0 || slot >= panel.法则装备.Count || !panel.法则碎片.Exists(x => x.id == id)) return false;
        if (panel.炉中法则.Contains(id)) return false;
        int existing = panel.法则装备.IndexOf(id);
        if (existing == slot) return true;
        string previous = panel.法则装备[slot];
        if (existing >= 0) panel.法则装备[existing] = previous;
        panel.法则装备[slot] = id; panel.RaiseChanged(); return true;
    }
    public static void 卸下(UIPanelData panel, int slot) { panel.法则装备[slot] = ""; panel.RaiseChanged(); }
    public static void 汇总(UIPanelData panel, AttributeSet percent, AttributeSet flat, AttributeSet basis = null)
    {
        if (!panel) return;
        var lawPercent = new AttributeSet(); var lawFlat = new AttributeSet();
        var used = new HashSet<string>();
        foreach (var id in panel.法则装备)
        {
            if (string.IsNullOrEmpty(id) || !used.Add(id)) continue;
            var law = panel.法则碎片.Find(x => x.id == id); if (law == null) continue;
            foreach (var r in law.属性) { if (r.大增幅) lawPercent[r.属性] += r.数值; else lawFlat[r.属性] += r.数值; }
        }
        LawFragmentBalance.限制汇总(lawPercent, lawFlat, basis);
        percent.Add(lawPercent); flat.Add(lawFlat);
    }
    public static void 结算(AttributeSet values, AttributeSet percent, AttributeSet flat)
    {
        for (int i = 0; i < AttributeUtil.Count; i++)
        {
            var a = (AttributeType)i; float value = values[a];
            values[a] = value + (Mathf.Approximately(value, 0) ? 增幅基准(a) : Mathf.Abs(value)) * percent[a] + flat[a];
        }
    }
    public static void 恢复(UIPanelData panel, List<LawFragment> owned, List<string> equipped)
    {
        panel.法则碎片 = new List<LawFragment>(); panel.法则装备 = new List<string>();
        var cfg = LawFragmentCatalog.取(); var seen = new HashSet<string>();
        if (owned != null) foreach (var law in owned)
            if (law != null && !string.IsNullOrEmpty(law.id) && seen.Add(law.id) && law.境界 >= 0 && law.境界 < 9 && cfg.找称呼(law.称呼id) != null)
            { LawFragmentBalance.校准区间数值(law); panel.法则碎片.Add(law); }
        for (int i = 0; i < cfg.装备槽数; i++)
        {
            string id = equipped != null && i < equipped.Count ? equipped[i] : "";
            panel.法则装备.Add(!string.IsNullOrEmpty(id) && seen.Contains(id) && !panel.法则装备.Contains(id) ? id : "");
        }
    }
}
