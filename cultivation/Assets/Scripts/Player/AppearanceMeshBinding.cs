using System;
using UnityEngine;

/// <summary>外观网格使用目标骨架的绑定矩阵；同名同序不等于绑定姿势相同。</summary>
public static class AppearanceMeshBinding
{
    /// <summary>返回由调用方持有并销毁的兼容网格，不修改导入的共享资产。</summary>
    public static Mesh 创建兼容网格(SkinnedMeshRenderer source, Mesh reference, Transform[] bones)
    {
        var sourceBones = source.bones;
        var binds = reference.bindposes;
        if (sourceBones.Length != bones.Length || binds.Length != bones.Length)
            throw new InvalidOperationException("外观骨骼数量与目标骨架不匹配");
        for (int i = 0; i < bones.Length; i++)
            if (sourceBones[i] == null || bones[i] == null || sourceBones[i].name != bones[i].name)
                throw new InvalidOperationException("外观骨骼顺序不匹配，索引 " + i);
        // 当前两件外观的顶点处于共同模型空间，但右前臂等骨骼的 rest axes 不同。
        // 保留顶点、权重、材质，用目标骨架的 inverse bind 消除整条手臂的反扭。
        // 不重摆顶点：那会将错误的轴向补偿再次烘入模型。
        var mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
        mesh.name = source.sharedMesh.name + "_AlignedBinding";
        mesh.bindposes = binds;
        return mesh;
    }
}
