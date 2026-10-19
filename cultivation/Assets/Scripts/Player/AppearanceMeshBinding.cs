using System;
using UnityEngine;

/// <summary>外观网格使用目标骨架的绑定矩阵；同名同序不等于绑定姿势相同。</summary>
public static class AppearanceMeshBinding
{
    /// <summary>返回由调用方持有并销毁的兼容网格，不修改导入的共享资产。</summary>
    public static Mesh 创建兼容网格(SkinnedMeshRenderer source, Mesh reference, Transform[] bones, bool 重定位顶点 = false, BoneWeight[] 修复权重 = null, bool 解剖全身 = false)
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
        var weights = 修复权重 ?? source.sharedMesh.boneWeights;
        if (weights.Length != vertices.Length) throw new InvalidOperationException("外观修复权重数量不匹配");
        mesh.boneWeights = weights;
        var originalBinds = source.sharedMesh.bindposes;
        var corrections = new Matrix4x4[bones.Length];
        var normalCorrections = new Matrix4x4[bones.Length];
        for (int i = 0; i < corrections.Length; i++)
            corrections[i] = binds[i].inverse * originalBinds[i];
        if(解剖全身)应用全身解剖坐标(corrections,originalBinds,binds);
        // 自动绑骨的 roll 不是解剖朝向。右手拇指在两套 rig 中可相差近 180 度；
        // 用它直接重定位会在手掌/指根的混合区制造扭转。由腕、指根和指节建立
        // 解剖坐标，只改变静止网格的适配；动画仍使用全部原手指骨和原权重。
        foreach (string side in new[] { "L", "R" })
        {
            int Index(string name) => Array.FindIndex(sourceBones, b => b.name == side + "_" + name);
            int hand = Index("Hand"), middle = Index("Mid1"), index = Index("Index1"), pinky = Index("Pinky1");
            if (hand < 0 || middle < 0 || index < 0 || pinky < 0) continue;
            Matrix4x4 Frame(Matrix4x4[] inverseBinds, int bone, int next, int previous)
            {
                Vector3 Point(int n) => inverseBinds[n].inverse.GetColumn(3);
                var origin = Point(bone);
                var forward = bone == hand ? Point(middle) - origin : next >= 0 ? Point(next) - origin : origin - Point(previous);
                var palmNormal = Vector3.Cross(Point(index) - Point(pinky), Point(middle) - Point(hand));
                if (forward.sqrMagnitude < 1e-10f || palmNormal.sqrMagnitude < 1e-10f)
                    throw new InvalidOperationException("手部解剖坐标退化：" + sourceBones[bone].name);
                // LookRotation 的局部 Z 沿指节，局部 Y 沿掌面法向。
                return Matrix4x4.TRS(origin, Quaternion.LookRotation(forward, palmNormal), Vector3.one);
            }
            void Align(int bone, int next = -1, int previous = -1)
            {
                corrections[bone] = Frame(binds, bone, next, previous) * Frame(originalBinds, bone, next, previous).inverse;
            }
            Align(hand);
            foreach (string finger in new[] { "Thumb", "Index", "Mid", "Ring", "Pinky" })
            {
                int a = Index(finger + "1"), b = Index(finger + "2"), c = Index(finger + "3");
                if (a < 0 || b < 0 || c < 0) continue;
                Align(a, b); Align(b, c); Align(c, -1, b);
                // 新着装的手指先随整只手做一致的静止姿态映射，保留源手形及指骨权重。
                // 对每一小节各自重建坐标会把自动骨轴误差放大成指间撕裂。
                if(解剖全身){corrections[a]=corrections[hand];corrections[b]=corrections[hand];corrections[c]=corrections[hand];}
            }
        }
        for (int i = 0; i < corrections.Length; i++) normalCorrections[i] = corrections[i].inverse.transpose;
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

    // 自动绑骨的骨轴 roll 不能作为衣型重定位依据。用实际关节连线与身体前向建立统一坐标。
    public static void 应用全身解剖坐标(Matrix4x4[] corrections,Matrix4x4[] source,Matrix4x4[] target)
    {
        int[,] segments={{1,21},{2,3},{3,4},{4,5},{5,6},{12,13},{13,15},{15,16},{21,22},{22,23},{23,24},{24,26},{26,27},{30,31},{31,32},{32,36},{54,55},{55,56},{56,60}};
        Matrix4x4 Frame(Matrix4x4[] binds,int bone,int next){
            Vector3 P(int i)=>binds[i].inverse.GetColumn(3);
            var up=(P(26)-P(1)).normalized;var left=(P(31)-P(55)).normalized;var forward=Vector3.Cross(left,up).normalized;
            var axis=P(next)-P(bone);if(axis.sqrMagnitude<1e-8f)axis=up;
            if(Mathf.Abs(Vector3.Dot(axis.normalized,forward))>.95f)forward=up;
            return Matrix4x4.TRS(P(bone),Quaternion.LookRotation(axis,forward),Vector3.one);
        }
        for(int i=0;i<segments.GetLength(0);i++){int bone=segments[i,0],next=segments[i,1];corrections[bone]=Frame(target,bone,next)*Frame(source,bone,next).inverse;}
        for(int i=6;i<=11;i++)corrections[i]=corrections[5];
        for(int i=16;i<=20;i++)corrections[i]=corrections[15];
    }
}
