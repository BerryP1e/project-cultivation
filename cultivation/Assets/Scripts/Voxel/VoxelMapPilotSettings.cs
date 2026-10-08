using System;
using UnityEngine;

/// <summary>Opt-in scene manifest. Source scenes and prefabs remain untouched.</summary>
public sealed class VoxelMapPilotSettings : ScriptableObject
{
    public bool 启用试点 = true;
    public string 场景路径;
    public VoxelTerrainSettings 地面;
    public Entry[] 替换项 = Array.Empty<Entry>();

    [Serializable]
    public sealed class Entry
    {
        public string 名称;
        // Root name, then child indices and expected names. Reject stale paths instead of replacing another object.
        public string 根名称;
        public int[] 子索引;
        public string[] 子名称;
        public Vector3 预期位置;
        public Quaternion 预期旋转;
        public Vector3 预期缩放;
        public Mesh 源网格;
        public Material[] 源材质;
        public Bounds 预期包围盒;
        public VoxelVolumeAsset 数据;
        public bool 预先加载 = true;
        public bool 树木;
        public int 资源上溯层数;
        public Mesh[] 树冠网格 = Array.Empty<Mesh>();
    }
}
