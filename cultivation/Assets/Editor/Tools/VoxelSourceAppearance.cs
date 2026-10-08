using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Upgrade existing native bakes without repeating distance-field rasterization.</summary>
public static class VoxelSourceAppearance
{
    public static void 同步(VoxelVolumeAsset data)
    {
        if(!data.原网格)return;
        if(data.表面三角点==null || data.表面三角点.Length==0)
        {
            if(data.原材质.Length!=1 || data.原材质[0].mainTexture)return;
            using(var array=MeshUtility.AcquireReadOnlyMeshData(data.原网格))
            {
                var mesh=array[0];using(var positions=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
                using(var indices=new NativeArray<int>(mesh.GetSubMesh(0).indexCount,Allocator.Temp))
                {
                    mesh.GetVertices(positions);mesh.GetIndices(indices,0,true);data.表面三角点=new Vector3[indices.Length];data.表面三角UV=new Vector2[indices.Length];
                    for(int i=0;i<indices.Length;i++)data.表面三角点[i]=positions[indices[i]];
                }
            }
            data.材质.SetTexture("_SourceTex",Texture2D.whiteTexture);data.材质.SetColor("_SourceTint",data.原材质[0].color);data.材质.SetFloat("_UseSourceUV",1);
        }
        using(var array=MeshUtility.AcquireReadOnlyMeshData(data.原网格))
        {
            var mesh=array[0];using(var positions=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
            using(var normals=new NativeArray<Vector3>(mesh.vertexCount,Allocator.Temp))
            {
                mesh.GetVertices(positions);mesh.GetNormals(normals);var result=new Vector3[data.表面三角点.Length];int cursor=0;
                for(int s=0;s<mesh.subMeshCount;s++)using(var indices=new NativeArray<int>(mesh.GetSubMesh(s).indexCount,Allocator.Temp))
                {
                    mesh.GetIndices(indices,s,true);
                    foreach(int index in indices)
                    {
                        if(cursor>=result.Length || (data.表面三角点[cursor]-positions[index]).sqrMagnitude>Mathf.Max(data.原网格.bounds.size.sqrMagnitude*1e-10f,1e-22f))
                            throw new InvalidOperationException("Source triangle order differs: "+data.来源);
                        result[cursor++]=normals[index];
                    }
                }
                for(int i=cursor;i<result.Length;i+=3)
                {var n=Vector3.Cross(data.表面三角点[i+1]-data.表面三角点[i],data.表面三角点[i+2]-data.表面三角点[i]).normalized;result[i]=result[i+1]=result[i+2]=n;}
                data.表面三角法线=result;
            }
        }
        var original=data.原材质[0];bool diffuse=original.shader.name.IndexOf("Diffuse",StringComparison.OrdinalIgnoreCase)>=0;
        data.材质.shader=Shader.Find(diffuse?"Cultivation/VoxelSurfaceDiffuse":"Cultivation/VoxelSurface");
        if(original.HasProperty("_Glossiness"))data.材质.SetFloat("_Smoothness",original.GetFloat("_Glossiness"));
        if(original.HasProperty("_Metallic"))data.材质.SetFloat("_Metallic",original.GetFloat("_Metallic"));
        foreach(var chunk in data.预烘焙网格){VoxelSurfaceProjection.应用(data,chunk);EditorUtility.SetDirty(chunk);}
        if(!data.完整外观){data.完整外观=new Mesh {name="PristineSurface",indexFormat=IndexFormat.UInt32};AssetDatabase.AddObjectToAsset(data.完整外观,data);}
        var combine=new System.Collections.Generic.List<CombineInstance>();
        foreach(var chunk in data.预烘焙网格)if(chunk.vertexCount>0)combine.Add(new CombineInstance {mesh=chunk,transform=Matrix4x4.identity});
        data.完整外观.Clear();data.完整外观.CombineMeshes(combine.ToArray(),true,false);
        data.材质.enableInstancing=true;EditorUtility.SetDirty(data.完整外观);
        EditorUtility.SetDirty(data);EditorUtility.SetDirty(data.材质);
    }
}
