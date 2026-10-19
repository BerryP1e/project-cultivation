using System;
using UnityEngine;

/// <summary>人体蒙皮、独立外袍表面与长发骨链。由衣服预处理生成，不改原 FBX。</summary>
public sealed class AppearanceClothProfile : ScriptableObject
{
    [Serializable]
    public sealed class Bone
    {
        public string 名称;
        public int 人体骨索引;
        public int 上级衣骨 = -1;
        public Vector3 局部位置;
        public Quaternion 局部旋转 = Quaternion.identity;
        public Vector3 末端偏移;
        public bool 衣袖;
        public bool 头发;
    }

    public Mesh 蒙皮;
    public Mesh 布料蒙皮;
    public Mesh 裤装蒙皮;
    public Material 裤装材质;
    public int[] 裤靴面分组 = new int[0]; // 0裤装、1左靴、2右靴；用于分别判定闭合实体。
    public float[] 布料顶点约束 = new float[0];
    public int[] 布料同点 = new int[0];
    public float[] 袍面覆盖 = new float[0], 袍面进度 = new float[0];
    public float[] 闭襟覆盖 = new float[0];
    public Vector3[] 鞋口锚点 = new Vector3[0]; // 左右脚骨局部坐标，只用于已手工分区的薄腿。
    public int[] 鞋口骨索引 = new int[0];
    public float[] 鞋靴半径 = new float[0];
    public Vector3[] 鞋趾锚点 = new Vector3[0];
    public float 细腿半径 = .009f;
    public bool 仅保留原鞋;
    [Range(0,1)] public float 闭襟收束;
    public float 闭襟腿距 = .12f;
    public Vector3[] 原鞋筒锚点 = new Vector3[0]; // 已明确分出的原鞋上口，小腿骨局部空间。
    public float[] 原鞋筒半径 = new float[0];
    public Vector3[] 原鞋掌锚点 = new Vector3[0]; // 原鞋底包围球，脚骨局部空间。
    public float[] 原鞋掌半径 = new float[0];
    public int 原布料三角数;
    public int 补全人体顶点数, 补全人体三角数;
    public int 移除封底三角数;
    public string[] 人体骨名;
    public Bone[] 衣骨;
    public int[] 长发顶点 = new int[0];
    public bool 姿态空间随动;
    [Range(0, 1)] public float 地面柔化;
    [Range(0, 1)] public float 长发释放;
    [Range(15, 65)] public float 衣摆末端限角 = 55;
    [Range(0, 1)] public float 衣摆下垂 = .75f;
    [Range(0, .06f)] public float 衣摆波幅 = .032f;
    [Range(3, 20)] public float 回弹频率 = 8;
    [Range(0, 2)] public float 阻尼 = .85f;
    [Range(3, 40)] public float 单节限角 = 18;
    [Range(0, .3f)] public float 微风强度 = .035f;
}
