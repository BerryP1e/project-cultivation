using System;
using UnityEngine;

/// <summary>保留原人体骨序的衣服蒙皮及附加骨链。由衣服预处理生成，不改原 FBX。</summary>
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
    public string[] 人体骨名;
    public Bone[] 衣骨;
    public int[] 长发顶点 = new int[0];
    [Range(3, 20)] public float 回弹频率 = 8;
    [Range(0, 2)] public float 阻尼 = .85f;
    [Range(3, 40)] public float 单节限角 = 18;
    [Range(0, .3f)] public float 微风强度 = .035f;
}
