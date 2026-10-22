using UnityEngine;

public enum WeaponType { 长戟, 灵剑, 飞剑 }

/// <summary>武器负责物理普攻模组；修行属性与特殊普攻仍来自当前功法。</summary>
[CreateAssetMenu(fileName="Weapon_",menuName="修仙/武器")]
public sealed class WeaponDefinition : ScriptableObject,IPanelEntry
{
    public string 武器id;
    public string 武器名称;
    public WeaponType 武器类型;
    public QualityTier 品阶=QualityTier.凡品;
    public string 普攻方法id;
    public string 模型资源路径;
    [Min(1)] public int 连招段数=3;
    public Sprite 图标;
    [TextArea(2,6)] public string 介绍;
    public bool 手持=>武器类型!=WeaponType.飞剑;
    public string DisplayName=>string.IsNullOrEmpty(武器名称)?name:武器名称;
    public string DisplayDescription=>介绍+"\n\n"+武器类型+" · 物理普通攻击\nE · "+连招段数+"式连招";
    public QualityTier DisplayTier=>品阶;
    public Sprite DisplayIcon=>图标;
}
