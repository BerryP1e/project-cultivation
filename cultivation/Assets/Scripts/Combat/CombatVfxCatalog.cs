using System;
using UnityEngine;

/// <summary>战斗表现配置；只描述资源、方向、大小和寿命，不参与伤害公式。</summary>
[CreateAssetMenu(menuName="修仙/战斗特效目录")]
public sealed class CombatVfxCatalog : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public string id;
        public GameObject 预制体;
        public float 缩放=1f;
        public Vector3 旋转欧拉;
        public bool 跟随来向;
        public float 散布半径;
        public float 最长存活=12f;
    }
    public Entry[] 特效=Array.Empty<Entry>();
    public Entry 查找(string id)=>Array.Find(特效,e=>e!=null&&e.id==id);
}
