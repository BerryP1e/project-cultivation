using System;
using UnityEngine;

/// <summary>外观网格使用目标骨架的绑定矩阵；同名同序不等于绑定姿势相同。</summary>
public static class AppearanceMeshBinding
{
    /// <summary>返回由调用方持有并销毁的兼容网格，不修改导入的共享资产。</summary>
    public static Mesh 创建兼容网格(SkinnedMeshRenderer source, Mesh reference, Transform[] bones, bool 重定位顶点 = false)
    {
        var sourceBones = source.bones;
        var binds = reference.bindposes;
        if (sourceBones.Length != bones.Length || binds.Length != bones.Length)
            throw new InvalidOperationException("外观骨骼数量与目标骨架不匹配");
        for (int i = 0; i < bones.Length; i++)
            if (sourceBones[i] == null || bones[i] == null || sourceBones[i].name != bones[i].name)
                throw new InvalidOperationException("外观骨骼顺序不匹配，索引 " + i);
        // 旧外观沿用共同模型空间，默认只调整 bindpose，避免村民手臂反扭。
        // 新长袍预处理显式启用重定位，适配源/目标手臂姿态和指骨轴向。
        var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
        if (!重定位顶点)
        {
            mesh.name = source.sharedMesh.name + "_AlignedBinding";
            mesh.bindposes = binds;
            return mesh;
        }
        var vertices = source.sharedMesh.vertices;
        var normals = source.sharedMesh.normals;
        var tangents = source.sharedMesh.tangents;
        var weights = source.sharedMesh.boneWeights;
        var originalBinds = source.sharedMesh.bindposes;
        var corrections = new Matrix4x4[bones.Length];
        var normalCorrections = new Matrix4x4[bones.Length];
        for (int i = 0; i < corrections.Length; i++)
        { corrections[i] = binds[i].inverse * originalBinds[i]; normalCorrections[i] = corrections[i].inverse.transpose; }
        for (int i = 0; i < vertices.Length; i++)
        {
            var w = weights[i];
            Vector3 position = Vector3.zero, normal = Vector3.zero, tangent = Vector3.zero;
            void Apply(int index, float weight)
            {
                if (weight <= 0) return;
                var correction = corrections[index];
                position += correction.MultiplyPoint3x4(vertices[i]) * weight;
                if (normals.Length > i) normal += normalCorrections[index].MultiplyVector(normals[i]) * weight;
                if (tangents.Length > i) tangent += correction.MultiplyVector(tangents[i]) * weight;
            }
            Apply(w.boneIndex0,w.weight0);Apply(w.boneIndex1,w.weight1);Apply(w.boneIndex2,w.weight2);Apply(w.boneIndex3,w.weight3);
            vertices[i] = position;
            if (normals.Length > i) normals[i] = normal.normalized;
            if (tangents.Length > i) tangents[i] = new Vector4(tangent.x,tangent.y,tangent.z,tangents[i].w);
        }
        mesh.vertices = vertices;
        if (normals.Length == vertices.Length) mesh.normals = normals;
        if (tangents.Length == vertices.Length) mesh.tangents = tangents;
        mesh.name = source.sharedMesh.name + "_AlignedBinding";
        mesh.bindposes = binds;
        mesh.RecalculateBounds();
        return mesh;
    }
}
