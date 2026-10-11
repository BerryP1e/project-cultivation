using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

/// <summary>独立播放原作 Generic 伴龙；按玩家动作进度采样，保留相对源髋骨的位置和统一比例。</summary>
public sealed class TaichuDragonRig : System.IDisposable
{
    public readonly GameObject 根;
    public readonly SkinnedMeshRenderer 龙网格;
    readonly Transform 源髋骨;
    readonly Animator 玩家;
    readonly TaichuDragonDefinition 配置;
    readonly PlayableGraph graph;
    readonly AnimationClipPlayable playable;
    readonly AnimationClip clip;
    readonly List<Material> materials = new List<Material>();
    Vector3[] sourceVertices;
    Matrix4x4[] bindposes, skinMatrices;
    Transform[] bones;
    byte[] weightCounts;
    BoneWeight1[] weights;
    float alpha = 1f;
    Vector3 crystalOffset;
    readonly List<Transform> crystals = new List<Transform>();
    Mesh crystalMesh;
    Vector3[] crystalTemplate, crystalNormals, combinedVertices, combinedNormals;
    Vector3 crystalAnchor;
    Vector3? destination;
    readonly Dictionary<string, TaichuFireVisual> fire = new Dictionary<string, TaichuFireVisual>();
    public Vector3 旅程偏移 { get; private set; }
    public float 长度 => clip.length;
    public float 当前进度 { get; private set; }
    struct Pose { public Vector3 point; public Quaternion rotation; }
    readonly Dictionary<string, Pose> fromPose = new Dictionary<string, Pose>();
    Transform[] blendingBones;
    Pose[] localRest, targetPose;
    Pose?[] originPose;
    float blendTime;

    public void 推进过渡(float dt) { blendTime += dt; }

    public TaichuDragonRig(TaichuDragonDefinition.DragonMotion motion, Animator player, TaichuDragonDefinition definition, TaichuDragonRig previous = null)
    {
        玩家 = player; 配置 = definition; clip = motion.龙动作;
        if (previous != null && previous.龙网格)
            foreach (var bone in previous.龙网格.bones)
                if (bone) fromPose[previous.路径(bone)] = new Pose {
                    point = player.transform.InverseTransformPoint(bone.position),
                    rotation = Quaternion.Inverse(player.transform.rotation) * bone.rotation };
        根 = Object.Instantiate(motion.龙预制体); 根.name = "太初伴龙_" + clip.name;
        根.layer = 2;
        foreach (var t in 根.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        龙网格 = 根.GetComponentInChildren<SkinnedMeshRenderer>();
        blendingBones = (Transform[])龙网格.bones.Clone();
        // Parents must be restored before children so world-space pose interpolation is stable.
        System.Array.Sort(blendingBones, (a, b) => 路径(a).Length.CompareTo(路径(b).Length));
        localRest = new Pose[blendingBones.Length]; targetPose = new Pose[blendingBones.Length]; originPose = new Pose?[blendingBones.Length];
        for (int i = 0; i < blendingBones.Length; i++)
        {
            var bone = blendingBones[i]; localRest[i] = new Pose { point = bone.localPosition, rotation = bone.localRotation };
            if (fromPose.TryGetValue(路径(bone), out var pose)) originPose[i] = pose;
        }
        源髋骨 = 根.transform.Find(definition.源髋骨路径);

        var clones = new Dictionary<Material, Material>();
        foreach (var r in 根.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            for (int i = 0; i < shared.Length; i++)
            {
                if (!shared[i]) continue;
                if (clones.TryGetValue(shared[i], out var reused)) { shared[i] = reused; continue; }
                var m = new Material(shared[i]); clones.Add(shared[i], m); materials.Add(m); shared[i] = m;
                if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2);
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.EnableKeyword("_ALPHABLEND_ON"); m.renderQueue = (int)RenderQueue.Transparent;
            }
            r.sharedMaterials = shared;
        }
        var animator = 根.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        graph = PlayableGraph.Create("TaichuDragon"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
        AnimationPlayableOutput.Create(graph, "Dragon", animator).SetSourcePlayable(playable); graph.Play();
        建立冰晶();
        采样(0); 透明度(previous == null ? 0 : 1);
        foreach (var r in 根.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name.StartsWith("Hedra") && 配置.火弹特效)
            {
                r.enabled = false;
                var visual = Object.Instantiate(配置.火弹特效, r.transform.position, r.transform.rotation);
                visual.name = "太初火弹凝聚_" + r.name; visual.transform.SetParent(根.transform, true);
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
                fire.Add(路径(r.transform), visual.GetComponent<TaichuFireVisual>() ?? visual.AddComponent<TaichuFireVisual>());
            }

    }
    public void 采样(float progress)
    {
        当前进度 = progress;
        // Generic clips may animate rotation only. Reset untouched local translations before sampling.
        for (int i = 0; i < blendingBones.Length; i++)
        { blendingBones[i].localPosition = localRest[i].point; blendingBones[i].localRotation = localRest[i].rotation; }
        playable.SetTime(Mathf.Clamp01(progress) * clip.length); graph.Evaluate(0);
        float size = 玩家.humanScale * Mathf.Abs(玩家.transform.lossyScale.y) / Mathf.Max(.01f, 配置.源髋骨高度);
        根.transform.localScale = Vector3.one * size;
        根.transform.rotation = 玩家.transform.rotation * Quaternion.Euler(0, 配置.源朝向补偿, 0);
        var hips = 玩家.GetBoneTransform(HumanBodyBones.Hips);
        if (源髋骨 && hips) 根.transform.position += hips.position - 源髋骨.position;
        旅程偏移 = Vector3.zero; crystalOffset = Vector3.zero;
        if (destination.HasValue && hips)
        {
            float envelope = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .65f, progress))
                * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.8f, 1, progress)));
            var fullOffset = destination.Value - (hips.position + 根.transform.TransformVector(crystalAnchor));
            旅程偏移 = fullOffset * envelope;
            if (progress >= .65f) crystalOffset = 根.transform.InverseTransformVector(fullOffset - 旅程偏移);
            根.transform.position += 旅程偏移;
        }
        // Blend the actual rendered skeleton, including interrupted blends. No overlapping ghosts.
        float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(blendTime / 配置.姿势过渡秒数));
        if (fromPose.Count > 0 && blend < 1)
        {
            for (int i = 0; i < blendingBones.Length; i++)
            { targetPose[i] = new Pose { point = blendingBones[i].position, rotation = blendingBones[i].rotation }; }
            for (int i = 0; i < blendingBones.Length; i++)
            {
                var bone = blendingBones[i];
                if (originPose[i].HasValue)
                {
                    var pose = originPose[i].Value;
                    bone.SetPositionAndRotation(Vector3.Lerp(玩家.transform.TransformPoint(pose.point), targetPose[i].point, blend),
                        Quaternion.Slerp(玩家.transform.rotation * pose.rotation, targetPose[i].rotation, blend));
                }
            }
        }
        else fromPose.Clear();
        更新冰晶();
        foreach (var pair in fire)
        {
            var visual = pair.Value; if (!visual || visual.已消散) continue;
            var node = 根.transform.Find(pair.Key); if (!node) continue;
            visual.transform.SetPositionAndRotation(node.position, node.rotation);
            float amount = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.03f, .25f, progress));
            visual.transform.localScale = Vector3.one * (.65f * amount / size);
        }
    }
    public void 透明度(float value)
    {
        value = Mathf.Clamp01(value); if (Mathf.Approximately(alpha, value)) return; alpha = value;
        foreach (var m in materials) if (m.HasProperty("_Color")) { var c = m.color; c.a = value; m.color = c; }
    }
    /// <summary>使用与渲染相同的骨矩阵和完整权重，避免 BakeMesh 在缩放骨架上重复计入比例。</summary>
    public void 取世界顶点(List<Vector3> output)
    {
        if (sourceVertices == null)
        {
            var mesh = 龙网格.sharedMesh;
            sourceVertices = mesh.vertices; bindposes = mesh.bindposes; bones = 龙网格.bones;
            skinMatrices = new Matrix4x4[bones.Length];
            using (var counts = mesh.GetBonesPerVertex()) weightCounts = counts.ToArray();
            using (var skinWeights = mesh.GetAllBoneWeights()) weights = skinWeights.ToArray();
        }
        for (int i = 0; i < bones.Length; i++) skinMatrices[i] = bones[i].localToWorldMatrix * bindposes[i];
        output.Clear(); int at = 0;
        for (int i = 0; i < sourceVertices.Length; i++)
        {
            Vector3 point = Vector3.zero;
            for (int j = 0; j < weightCounts[i]; j++)
            {
                var w = weights[at++]; point += skinMatrices[w.boneIndex].MultiplyPoint3x4(sourceVertices[i]) * w.weight;
            }
            output.Add(point);
        }
    }
    string 路径(Transform t)
    {
        string path = t.name;
        while (t.parent && t.parent != 根.transform) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
    void 建立冰晶()
    {
        Mesh template = null; Material material = null;
        foreach (var r in 根.GetComponentsInChildren<MeshRenderer>(true)) if (r.name.StartsWith("Pyramid"))
        {
            crystals.Add(r.transform); template = r.GetComponent<MeshFilter>().sharedMesh; material = r.sharedMaterial; r.enabled = false;
        }
        if (!template || crystals.Count == 0) return;
        crystalTemplate = template.vertices; crystalNormals = template.normals;
        int count = crystalTemplate.Length;
        combinedVertices = new Vector3[count * crystals.Count]; combinedNormals = new Vector3[combinedVertices.Length];
        var uv = new Vector2[combinedVertices.Length]; var sourceUv = template.uv; var sourceTriangles = template.triangles;
        var triangles = new int[sourceTriangles.Length * crystals.Count];
        for (int i = 0; i < crystals.Count; i++)
        {
            for (int j = 0; j < count; j++) uv[i * count + j] = sourceUv[j];
            for (int j = 0; j < sourceTriangles.Length; j++) triangles[i * sourceTriangles.Length + j] = i * count + sourceTriangles[j];
        }
        crystalMesh = new Mesh { name = "太初冰晶合批" }; crystalMesh.MarkDynamic();
        crystalMesh.vertices = combinedVertices; crystalMesh.normals = combinedNormals; crystalMesh.uv = uv; crystalMesh.triangles = triangles;
        var go = new GameObject("太初冰晶合批"); go.layer = 2; go.transform.SetParent(根.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = crystalMesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }
    void 更新冰晶()
    {
        if (!crystalMesh) return;
        int at = 0;
        foreach (var node in crystals)
        {
            var matrix = 根.transform.worldToLocalMatrix * node.localToWorldMatrix;
            var normals = matrix.inverse.transpose;
            for (int j = 0; j < crystalTemplate.Length; j++, at++)
            {
                combinedVertices[at] = matrix.MultiplyPoint3x4(crystalTemplate[j]) + crystalOffset;
                combinedNormals[at] = normals.MultiplyVector(crystalNormals[j]).normalized;
            }
        }
        crystalMesh.vertices = combinedVertices; crystalMesh.normals = combinedNormals; crystalMesh.RecalculateBounds();
    }
    public void 设置目标旅程(Vector3 point)
    {
        if (!crystalMesh || !源髋骨) return;
        采样(.7f);
        var bounds = crystalMesh.bounds;
        var anchor = 根.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        crystalAnchor = 根.transform.InverseTransformVector(anchor - 源髋骨.position);
        var direction = point - 玩家.transform.position; direction.y = 0;
        direction = direction.sqrMagnitude > .0001f ? direction.normalized : 玩家.transform.forward;
        float forwardReach = 0;
        foreach (var v in combinedVertices)
            forwardReach = Mathf.Max(forwardReach, Vector3.Dot(根.transform.TransformPoint(v) - anchor, direction));
        var placement = point - direction * Mathf.Max(0, forwardReach - 配置.冰晶前沿深入);
        // Already in reach: no backwards journey; the authored glacier still covers this target.
        if (Vector3.Dot(placement - anchor, direction) < 0) { placement.x = anchor.x; placement.z = anchor.z; }
        destination = placement; 采样(0);
    }
    public void 取冰晶世界顶点(List<Vector3> output)
    {
        output.Clear(); if (!crystalMesh) return;
        var matrix = 根.transform.localToWorldMatrix;
        foreach (var v in combinedVertices) output.Add(matrix.MultiplyPoint3x4(v));
    }
    public TaichuFireVisual 释放火弹(string path)
    {
        if (!fire.TryGetValue(path, out var visual)) return null;
        fire.Remove(path); visual.transform.SetParent(null, true); return visual;
    }
    public Transform 冰刺节点(string path) => 根.transform.Find(path);
    public void Dispose()
    {
        if (graph.IsValid()) graph.Destroy();
        foreach (var visual in fire.Values) if (visual) { visual.transform.SetParent(null, true); visual.消散(); }
        fire.Clear();
        if (crystalMesh) { if (Application.isPlaying) Object.Destroy(crystalMesh); else Object.DestroyImmediate(crystalMesh); }
        if (根) { if (Application.isPlaying) Object.Destroy(根); else Object.DestroyImmediate(根); }
        foreach (var m in materials) if (m) { if (Application.isPlaying) Object.Destroy(m); else Object.DestroyImmediate(m); }
        materials.Clear();
    }
}
