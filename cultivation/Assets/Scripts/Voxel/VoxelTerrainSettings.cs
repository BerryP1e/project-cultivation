using UnityEngine;

public sealed class VoxelTerrainSettings : ScriptableObject
{
    public bool 启用 = true;
    public TerrainData 源地形;
    public Vector3 预期位置;
    [Min(1)] public float 实体厚度 = 4;
    [Min(.1f)] public float 底部承托厚度 = .35f;
    [Range(8,32)] public int 每区孔洞格数 = 16;
    public Material 地块材质;
}
