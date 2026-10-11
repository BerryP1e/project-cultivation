using UnityEngine;

/// <summary>仅移速、攻速、闪避、冷却和两项回复独立成长；其余属性使用通用境界区间。</summary>
public static class LawFragmentBalance
{
    public static Vector2 取区间(AttributeType type, int tier, bool main, bool big, Vector2 ordinary)
    {
        var rule = LawFragmentCatalog.取().找敏感属性(type);
        if (rule == null) return big ? ordinary : ordinary * LawFragmentSystem.固定值基准(type);
        return (big ? main ? rule.登仙主增幅 : rule.登仙副增幅 : rule.登仙固定值) * LawFragmentCatalog.取().境界[Mathf.Clamp(tier, 0, 8)].敏感成长;
    }
    /// <summary>Beta(1,k+1) 逆分布；k=2 时区间最高10%的概率仅0.1%。</summary>
    public static float 抽品质(double uniform, float exponent)
    { return 1 - Mathf.Pow(1 - Mathf.Clamp01((float)uniform), 1 / (Mathf.Max(0, exponent) + 1)); }
    /// <summary>本次数值调整保留词条种类与品质，将越界值映射回现行区间，不重新随机。</summary>
    public static void 校准区间数值(LawFragment law)
    {
        var realm = LawFragmentCatalog.取().境界[law.境界];
        foreach (var roll in law.属性)
        {
            var ordinary = roll.主属性 ? realm.主增幅 : roll.大增幅 ? realm.副增幅 : realm.小增幅;
            var range = 取区间(roll.属性, law.境界, roll.主属性, roll.大增幅, ordinary);
            if (roll.数值 < range.x - .00001f || roll.数值 > range.y + .00001f) roll.数值 = Mathf.Lerp(range.x, range.y, Mathf.Clamp01(roll.品质));
        }
    }
    public static void 限制汇总(AttributeSet percent, AttributeSet flat, AttributeSet basis)
    {
        foreach (var rule in LawFragmentCatalog.取().敏感属性)
        {
            var a = rule.属性; float p = Mathf.Clamp(percent[a], 0, rule.装备增幅上限), f = Mathf.Clamp(flat[a], 0, rule.装备固定上限);
            if (basis != null)
            {
                float scale = Mathf.Approximately(basis[a], 0) ? LawFragmentSystem.增幅基准(a) : Mathf.Abs(basis[a]);
                float cap = float.PositiveInfinity;
                if (a == AttributeType.MoveSpeed || a == AttributeType.AttackSpeed) cap = scale * rule.装备增幅上限;
                if (rule.实际增量上限 > 0) cap = Mathf.Min(cap, rule.实际增量上限);
                if (rule.回复占比上限 > 0) cap = Mathf.Min(cap, Mathf.Max(0, basis[rule.回复参照]) * rule.回复占比上限);
                // 固定值仍在百分比之后；达到总上限时优先保留固定部分。
                f = Mathf.Min(f, cap); p = Mathf.Min(p, Mathf.Max(0, cap - f) / Mathf.Max(.0001f, scale));
            }
            percent[a] = p; flat[a] = f;
        }
    }
}
