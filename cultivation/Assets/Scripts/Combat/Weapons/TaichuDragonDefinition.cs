using System;
using UnityEngine;

/// <summary>太初龙栖诀的成品动作、伴龙及原作召唤节点；表现尺寸不决定伤害倍率。</summary>
[CreateAssetMenu(menuName = "修仙/武器/太初龙栖诀")]
public sealed class TaichuDragonDefinition : ScriptableObject
{
    [Serializable] public sealed class DragonMotion
    {
        public GameObject 龙预制体;
        public AnimationClip 龙动作;
        public AnimationClip 玩家动作;
        public float 判定开始 = .15f, 判定结束 = .9f;
    }
    [Serializable] public sealed class IceRelease
    {
        public string 节点路径;
        [Range(0, 1)] public float 释放进度;
    }
    [Min(.05f)] public float 动作速度倍率 = .6f;
    [Min(.05f)] public float 姿势过渡秒数 = .28f;
    public DragonMotion 待机;
    public DragonMotion 移动, 特殊待机;
    public DragonMotion[] 五式 = new DragonMotion[5];
    [Min(1)] public float 特殊待机等待 = 10f;
    [Min(1)] public float 特殊待机检查间隔 = 5f;
    [Range(0, 1)] public float 特殊待机概率 = .2f;
    [Min(.1f)] public float 冰晶引导半径 = 1f;
    [Tooltip("冰川前沿略越过目标的余量（米），不把冰川中心送到目标脚下。")]
    [Min(0)] public float 冰晶前沿深入 = .35f;
    public GameObject 火弹特效;
    public IceRelease[] 冰刺 = new IceRelease[0];
    public string 源髋骨路径;
    public float 源髋骨高度 = .521f;
    public float 源朝向补偿 = 180f;
    [Min(.1f)] public float 近战距离 = 3f;
    [Min(.01f)] public float 接触容差 = .1f;
    [Tooltip("A1甩尾的攻击包络沿实际龙网格向下覆盖至玩家脚下高度，避免从矮小目标头顶穿过。")]
    public bool 甩尾向下覆盖 = true;
    [Min(.01f)] public float 甩尾接触容差 = .18f;
    [Min(.01f)] public float 雷击半径 = .75f;
    [Range(0, 1)] public float 雷击进度 = .5f;
    [Min(.1f)] public float 火弹速度 = 18f;
    [Min(.01f)] public float 火弹判定半径 = .14f;
    public float[] 伤害倍率 = { 1f, 1f, 1f, 1f, 1f };
}
