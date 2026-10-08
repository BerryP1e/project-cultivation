using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Only exposed faces, with cross-chunk neighbour checks. No per-voxel objects.</summary>
public static class VoxelChunkMesher
{
    static readonly Vector3Int[] Directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0,0,1), new Vector3Int(0,0,-1) };

    public static Mesh 生成(VoxelVolumeAsset data, byte[] cells, Vector3Int chunk, float[] distances = null)
    {
        if(data.平滑表面)
        {var smooth=VoxelSmoothMesher.生成(data,cells,chunk,distances);VoxelSurfaceProjection.应用(data,smooth);VoxelTerrainMesh.处理(data,smooth);return smooth;}
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color32>();
        var indices = new List<int>();
        int size = data.分块边长;
        var start = chunk * size;
        var end = Vector3Int.Min(start + Vector3Int.one * size, data.尺寸);
        for (int z=start.z; z<end.z; z++) for (int y=start.y; y<end.y; y++) for (int x=start.x; x<end.x; x++)
        {
            int index = data.索引(x,y,z);
            if (cells[index] == 0) continue;
            var center = data.原点 + new Vector3(x+.5f,y+.5f,z+.5f) * data.格子边长;
            for (int face=0; face<6; face++)
            {
                var n = Directions[face];
                int nx=x+n.x, ny=y+n.y, nz=z+n.z;
                if (data.范围内(nx,ny,nz) && cells[data.索引(nx,ny,nz)] != 0) continue;
                Vector3 normal = n;
                Vector3 u = face < 2 ? Vector3.up : Vector3.right;
                Vector3 v = Vector3.Cross(normal,u);
                float half = data.格子边长 * .5f;
                Vector3 p = center + normal * half;
                int b = vertices.Count;
                vertices.Add(p + (-u-v)*half); vertices.Add(p + (u-v)*half);
                vertices.Add(p + (u+v)*half); vertices.Add(p + (-u+v)*half);
                for(int k=0;k<4;k++) { normals.Add(normal); colors.Add(data.颜色[index]); }
                indices.Add(b); indices.Add(b+1); indices.Add(b+2);
                indices.Add(b); indices.Add(b+2); indices.Add(b+3);
            }
        }
        var mesh = new Mesh { name = "VoxelChunk_" + chunk, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
        mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
        VoxelSurfaceProjection.应用(data,mesh);
        VoxelTerrainMesh.处理(data,mesh);
        return mesh;
    }
}
