using UnityEngine;

/// <summary>灵虚剑决：法师 AttackA、AttackB、SkillA 的完整三式近战。</summary>
public sealed class BasicLingxuSword01 : BasicJiuba01
{
    public override bool 刚性本地网格 => true;
    protected override void Awake()
    {
        方法id="basic_lingxu_sword_01";
        动作序列=new[]{"技能动作/灵虚剑决/A1","技能动作/灵虚剑决/A2","技能动作/灵虚剑决/A3"};
        // 使用完整源动作；未另设御风混合，不借用其他功法的片段。
        御风动作后缀="";
        武器节点名="LingxuSword";
        出手最大距离=2.6f;出手进度=.18f;判定窗口=.77f;判定外扩=.55f;
        base.Awake();
    }
}
