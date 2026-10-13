using UnityEngine;

/// <summary>骤雨初歇的表现/弹道预算；伤害、灵力、冷却由神通表管理。</summary>
public sealed class SpiralSwordPattern : MonoBehaviour
{
    public ParticleSystem 剑群, 星屑;
    public string 齐射动作 = "神通施法/能量03_施法";
    [Range(96, 384)] public int 剑数 = 288;
    [Range(1, 5)] public float 圈数 = 2.4f;
    [Range(.05f, .9f)] public float 出手进度 = .25f;
    [Range(.05f, .95f)] public float 第二段出剑进度 = .38f;
    [Min(.1f)] public float 布阵时长 = 1.2f;
    [Min(0)] public float 蓄势时长 = .3f;
    [Min(.005f)] public float 发射间隔 = .0065f;
    [Range(0, .7f)] public float 出剑波动幅度 = .35f;
    [Min(.1f)] public float 出剑波动周期 = .7f;
    [Range(8, 96)] public int 最大同时飞剑 = 64;
    [Min(.01f)] public float 剑缩放 = .65f;
    [Range(.1f, 1)] public float 剑幕透明度 = .85f;
    [Range(.01f, .3f)] public float 端部虚化比例 = .12f;
    [Min(.01f)] public float 弹体半径 = .12f;
    [Min(.01f)] public float 命中半径 = .45f;
    [Min(1)] public float 飞行速度 = 38;
    [Min(0)] public float 条带流速 = .55f;
    [Min(.01f)] public float 单剑显现时长 = .3f;
    [Min(.01f)] public float 出阵转向时长 = .22f;
    [Min(0)] public float 插地停留时长 = 1.1f;
    [Min(.01f)] public float 插地淡出时长 = .35f;
    [Range(2, 16)] public int 星光剑间隔 = 6;

    // 连续变化的是逐剑发射速率；不会把剑打包成同时出发的一批。
    public float 出剑速率(float elapsed)
    {
        float phase = Mathf.Max(0, elapsed) * Mathf.PI * 2 / Mathf.Max(.1f, 出剑波动周期);
        float wave = .7f * Mathf.Cos(phase) + .3f * Mathf.Cos(phase * 1.63f + .4f);
        return (1 + Mathf.Clamp(出剑波动幅度, 0, .7f) * wave) / Mathf.Max(.005f, 发射间隔);
    }
}
