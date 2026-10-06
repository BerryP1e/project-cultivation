using UnityEngine;

/// <summary>太虚剑决三式，复用近战网格判定与结算；使用指定来源的完整动作。</summary>
public sealed class BasicTaixuSword01 : BasicJiuba01
{
    protected override void Awake()
    {
        方法id = "basic_taixu_sword_01";
        动作序列 = new[] { "技能动作/太虚剑决/A1", "技能动作/太虚剑决/A2", "技能动作/太虚剑决/A3" };
        // 御风攻击保留原上身动作，下身使用漂浮混合片段。
        御风动作后缀 = "_御风";
        武器节点名 = "ShenMiRen_03";
        出手最大距离 = 2.3f;
        出手进度 = .18f; 判定窗口 = .77f;
        判定外扩 = .55f;
        base.Awake();
    }
}
