using System;
using UnityEngine;

/// <summary>法则的唯一数值配置。表由显式编辑器导入生成，运行时不读取 CSV。</summary>
[CreateAssetMenu(menuName = "修仙/法则/配置")]
public sealed class LawFragmentCatalog : ScriptableObject
{
    [Serializable] public sealed class RealmRule
    {
        public string 境界名, 后缀;
        public int 主属性数, 副属性数;
        public Vector2 主增幅, 副增幅, 小增幅;
        public float 敏感成长;
        public long 熔炼基础精粹, 洗练消耗精粹;
        public Sprite 图标;
        public Sprite 晶体图标;
        public ItemDefinition 宝箱;
    }
    [Serializable] public sealed class NameRule
    {
        public string id, 称呼;
        public AttributeType[] 主属性;
    }
    [Serializable] public sealed class SensitiveRule
    {
        public AttributeType 属性;
        public Vector2 登仙主增幅, 登仙副增幅, 登仙固定值;
        public float 装备增幅上限, 装备固定上限, 实际增量上限;
        public AttributeType 回复参照;
        public float 回复占比上限;
    }
    [Range(0, 1)] public float 宝箱掉率 = .1f;
    [Tooltip("仅在宝箱掉落成功后判定；10% 掉率 × 5% 升境界 = 每次击杀 0.5%。")]
    [Range(0, 1)] public float 高一境界概率 = .05f;
    [Range(0, 1)] public float 副属性大增幅概率 = .4f;
    [Min(1)] public int 装备槽数 = 6;
    [Min(0)] public float 高值稀有指数 = 2;
    [Min(0)] public float 熔炼品质加成 = 1;
    [Range(.001f, 1)] public float 洗练进度提升 = .05f;
    public SensitiveRule[] 敏感属性 = new SensitiveRule[0];
    public RealmRule[] 境界 = new RealmRule[9];
    public Sprite[] 墨纹图层, 灵气图层;
    public NameRule[] 称呼 = new NameRule[112];
    static LawFragmentCatalog cached;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() { cached = null; }
    public static LawFragmentCatalog 取() => cached ? cached : cached = Resources.Load<LawFragmentCatalog>("Laws/Catalog");
    public NameRule 找称呼(string id) => Array.Find(称呼, x => x.id == id);
    public SensitiveRule 找敏感属性(AttributeType type) => Array.Find(敏感属性, x => x.属性 == type);
}
