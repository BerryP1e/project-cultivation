using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>掉落时确定的实例数据；数值、评分与唯一 id 原样存档，读取不重新随机。</summary>
[Serializable]
public sealed class LawFragment : IPanelEntry
{
    [Serializable] public sealed class Roll
    {
        public AttributeType 属性;
        public bool 主属性, 大增幅;
        public float 数值, 品质;
        public string 描述 => AttributeUtil.GetDisplayName(属性) + (大增幅 ? "  +" + (数值 * 100).ToString("0.#") + "% 增幅" : "  +" + AttributeUtil.Format(属性, 数值));
    }
    public string id, 称呼id;
    public int 境界;
    public List<Roll> 属性 = new List<Roll>();
    public int 评分;
    public float 品质;
    public string DisplayName => 名称;
    public string DisplayDescription => 说明;
    public Sprite DisplayIcon => 图标;
    public QualityTier DisplayTier => (QualityTier)Mathf.Min(6, 境界);
    public string 名称 => LawFragmentCatalog.取().找称呼(称呼id).称呼 + LawFragmentCatalog.取().境界[境界].后缀;
    public Sprite 图标 { get { var realm = LawFragmentCatalog.取().境界[境界]; return realm.晶体图标 ? realm.晶体图标 : realm.图标; } }
    public Color 颜色 {
        get {
            var name = LawFragmentCatalog.取().找称呼(称呼id);
            float hue = ((int)name.主属性[0] * .16f + (name.主属性.Length > 1 ? (int)name.主属性[1] * .029f : 0) + (name.主属性.Length > 2 ? .12f : 0)) % 1;
            return Color.HSVToRGB(hue, .25f, .94f);
        }
    }
    public string 说明 {
        get {
            var sb = new StringBuilder();
            sb.Append(LawFragmentCatalog.取().境界[境界].境界名).Append("期法则\n评分 ").Append(评分).Append("  ·  品质 ").Append((品质 * 100).ToString("0")).Append("%\n\n主属性\n");
            foreach (var roll in 属性) if (roll.主属性) sb.Append(roll.描述).Append('\n');
            if (属性.Exists(x => !x.主属性)) { sb.Append("\n副属性\n"); foreach (var roll in 属性) if (!roll.主属性) sb.Append(roll.描述).Append('\n'); }
            sb.Append("\n先计算增幅，再加固定值。\n零值属性按基准折算增幅。");
            if (属性.Exists(x => LawFragmentCatalog.取().找敏感属性(x.属性) != null))
                sb.Append("\n机动、概率与回复有法则叠加上限；移速最多+200%，攻速最多+100%。");
            return sb.ToString();
        }
    }
}
