using UnityEngine;

/// <summary>
/// 被动神通。常驻生效，不占主动技能位，提供 26 项属性增益。
/// 对应 lore/被动神通父类.txt。
/// </summary>
[CreateAssetMenu(fileName = "PassiveAbility_", menuName = "修仙/被动神通父类", order = 3)]
public class PassiveDivineAbility : DivineAbilityDefinition
{
    public override bool IsActive => false;

    [Header("lore 字段 · 被动神通提供的增益")]
    [Tooltip("被动神通常驻提供的 26 项属性增益")]
    public AttributeSet 增益 = new AttributeSet();

    [Header("百分比增幅（1 = +100%，同类相加）")]
    [Min(0)] public float 气血回复增幅;
    [Min(0)] public float 灵力回复增幅;
    [Min(0)] public float 神识增幅;
    [Min(0)] public float 压制持续时间 = 5;

    public PassiveSkillKind 结算方式;
    public string 特效资源路径 = "";
    public float 攻击间隔 = 3f;
    public float 伤害倍率 = 0.8f;
    public float 范围 = 20f;
    [Range(0f, 1f)] public float 减伤比例 = 0.25f;

    void OnValidate()
    {
        ValidateCommon();
    }
}

public enum PassiveSkillKind { 属性增益, 邪眼, 物理护罩, 特殊护罩, 双重护罩, 绝对护罩, 自成循环, 灵势威压, 内化世界, 身外化身, 空间腾挪 }
