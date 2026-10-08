using UnityEngine;

/// <summary>Immutable bake. Runtime destruction owns a copy of occupancy, not of every mesh.</summary>
public sealed class VoxelVolumeAsset : ScriptableObject
{
    public Vector3Int 尺寸;
    public Vector3 原点;
    public float 格子边长 = .15f;
    public int 分块边长 = 8;
    public bool 平滑表面 = true;
    public byte[] 实体;
    public int[] 实体索引;
    // Positive inside; distances are in volume-local metres, not binary occupancy.
    public float[] 距离;
    public Color32[] 颜色;
    public Vector3[] 表面三角点;
    public Vector2[] 表面三角UV;
    public Vector3[] 表面三角法线;
    public bool 限制水平范围;
    public Vector2 水平最小,水平最大;
    public TerrainData 地形源;
    public float[] 保护下界;
    public Vector3Int[] 分块坐标;
    public Mesh[] 预烘焙网格;
    public Mesh 完整外观;
    public Material 材质;
    public string 来源;
    public Mesh 原网格;
    public Material[] 原材质;
    public int 烘焙版本;
    public int 实体数量;

    public int 索引(int x, int y, int z) => x + 尺寸.x * (y + 尺寸.y * z);
    public bool 范围内(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < 尺寸.x && y < 尺寸.y && z < 尺寸.z;
}
