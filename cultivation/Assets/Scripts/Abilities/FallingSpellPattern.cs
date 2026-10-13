using UnityEngine;

/// <summary>天降特效的几何和时序。范围/倍率/冷却仍由神通表管理，源素材不参与结算。</summary>
public sealed class FallingSpellPattern : MonoBehaviour
{
    public ParticleSystem 飞行粒子;
    public ParticleSystem 起点闪光;
    [Min(0)] public float 接触后留痕时间;
    public ParticleSystem 召唤阵;
    public GameObject 命中特效;
    public GameObject 额外命中特效;
    [Range(.05f, .9f)] public float 出手进度 = .4f;
    [Min(1)] public int 数量 = 1;
    [Min(0)] public float 间隔 = .08f;
    public Vector3 起点偏移 = new Vector3(0, 10, 0);
    [Min(1)] public float 飞行速度 = 35;
    [Min(.01f)] public float 弹体半径 = .1f;
    [Min(0)] public float 命中半径; // 0 = 单次大范围攻击，采用神通范围。
    public float 命中特效缩放 = 1;
}
