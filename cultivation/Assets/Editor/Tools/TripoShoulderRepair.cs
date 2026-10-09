using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>在模型副本上外移肩关节，保持肘/手位置和静态蒙皮，重建独立Humanoid Avatar。</summary>
public static class TripoShoulderRepair
{
    public sealed class Result : IDisposable
    {
        public GameObject root;
        public Avatar avatar;
        public readonly List<Mesh> meshes = new List<Mesh>();
        public float shoulderBefore, shoulderAfter, elbowDrift, handDrift;
        public Vector3 rootPosition;
        internal Transform[] restBones;
        internal SkeletonBone[] restPose;
        public void 恢复绑定姿势()
        {
            for (int i = 0; i < restBones.Length; i++)
            {
                restBones[i].localPosition = restPose[i].position;
                restBones[i].localRotation = restPose[i].rotation;
                restBones[i].localScale = restPose[i].scale;
            }
        }
        readonly Dictionary<SkinnedMeshRenderer, Material[]> originals = new Dictionary<SkinnedMeshRenderer, Material[]>();
        readonly List<Material> previewMaterials = new List<Material>();
        public void 设置预览材质()
        {
            if (originals.Count != 0) return;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                originals[renderer] = renderer.sharedMaterials;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    var material = new Material(Shader.Find("Unlit/Texture")) { mainTexture = source != null ? source.mainTexture : null, hideFlags = HideFlags.HideAndDontSave };
                    previewMaterials.Add(material);
                    return material;
                }).ToArray();
            }
        }
        public void 恢复原材质() { foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value; }
        public void Dispose()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (avatar != null && !EditorUtility.IsPersistent(avatar)) UnityEngine.Object.DestroyImmediate(avatar);
            foreach (var mesh in meshes) if (mesh != null && !EditorUtility.IsPersistent(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
            foreach (var material in previewMaterials) if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }

    public static Result 创建副本(GameObject source, float leftRatio, float rightRatio, float riseRatio)
    {
        if (source == null || !EditorUtility.IsPersistent(source)) throw new InvalidOperationException("请选Project中的FBX或模型预制体，不直接修场景实例");
        if (source.GetComponentsInChildren<MonoBehaviour>(true).Length != 0) throw new InvalidOperationException("请选择原始FBX或纯模型预制体，不选带NPC业务脚本的预制体");
        if (new[] { leftRatio, rightRatio, riseRatio }.Any(v => float.IsNaN(v) || float.IsInfinity(v)) || leftRatio < 0 || rightRatio < 0 || leftRatio > .55f || rightRatio > .55f || Mathf.Abs(riseRatio) > .25f)
            throw new InvalidOperationException("肩部参数超出范围");
        var result = new Result();
        try
        {
            result.root = UnityEngine.Object.Instantiate(source);
            result.root.name = source.name;
            result.rootPosition = result.root.transform.localPosition;
            result.root.hideFlags = HideFlags.HideAndDontSave;
            // Only retain the art rig. Do not clone an NPC's gameplay behaviour into previews/exports.
            foreach (var component in result.root.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(component);
            foreach (var component in result.root.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(component);
            foreach (var component in result.root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(component);
            var transforms = result.root.GetComponentsInChildren<Transform>(true);
            if (transforms.GroupBy(t => t.name).Any(g => g.Count() != 1)) throw new InvalidOperationException("骨架含同名节点，无法可靠重建Avatar");
            var lookup = transforms.ToDictionary(t => t.name);
            Transform Bone(string name) => lookup.TryGetValue(name, out var bone) ? bone : throw new InvalidOperationException("缺少Tripo骨骼：" + name);
            var left = Bone("L_Upperarm"); var right = Bone("R_Upperarm");
            var up = (Bone("Head").position - Bone("Hip").position).normalized;
            var across = Vector3.ProjectOnPlane(right.position - left.position, up).normalized;
            if (across.sqrMagnitude < .9f) throw new InvalidOperationException("无法确定肩部横轴");
            result.shoulderBefore = Vector3.Distance(left.position, right.position);
            var renderers = result.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var oldMatrices = renderers.ToDictionary(r => r, r => r.bones.Select(b => b.localToWorldMatrix).ToArray());
            var elbows = new[] { Bone("L_Forearm"), Bone("R_Forearm") };
            var hands = new[] { Bone("L_Hand"), Bone("R_Hand") };
            var elbowPositions = elbows.Select(b => b.position).ToArray();
            var handPositions = hands.Select(b => b.position).ToArray();
            Move(left, elbows[0], -across, up, leftRatio, riseRatio);
            Move(right, elbows[1], across, up, rightRatio, riseRatio);
            result.shoulderAfter = Vector3.Distance(left.position, right.position);
            for (int i = 0; i < 2; i++)
            {
                result.elbowDrift = Mathf.Max(result.elbowDrift, Vector3.Distance(elbows[i].position, elbowPositions[i]));
                result.handDrift = Mathf.Max(result.handDrift, Vector3.Distance(hands[i].position, handPositions[i]));
            }
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMesh == null || renderer.sharedMesh.bindposes.Length != renderer.bones.Length) throw new InvalidOperationException("蒙皮绑定矩阵数量不匹配");
                var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh);
                mesh.name = renderer.sharedMesh.name + "_ShoulderRepair";
                result.meshes.Add(mesh);
                var binds = mesh.bindposes;
                // Keep B(new) * inverseBind(new) == B(old) * inverseBind(old).
                // This also preserves source models with a nontrivial renderer/bind-space transform.
                for (int i = 0; i < binds.Length; i++) binds[i] = renderer.bones[i].worldToLocalMatrix * oldMatrices[renderer][i] * binds[i];
                mesh.bindposes = binds;
                renderer.sharedMesh = mesh;
                renderer.updateWhenOffscreen = true;
            }
            var animator = result.root.GetComponent<Animator>();
            if (animator == null) animator = result.root.AddComponent<Animator>();
            if (result.root.GetComponentsInChildren<Animator>(true).Length != 1) throw new InvalidOperationException("请选择仅含一个Animator的模型根");
            var description = Mapping(source, lookup);
            description.skeleton = transforms.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            var rest = description.skeleton;
            result.restBones = transforms; result.restPose = rest;
            animator.runtimeAnimatorController = null;
            animator.enabled = false;
            result.avatar = AvatarBuilder.BuildHumanAvatar(result.root, description);
            if (!result.avatar.isValid || !result.avatar.isHuman) throw new InvalidOperationException("新Avatar未通过Humanoid校验，已取消输出");
            result.avatar.name = source.name + "_ShoulderAvatar";
            animator.avatar = result.avatar;
            animator.applyRootMotion = false;
            // Avatar assignment can reinitialise an already-Humanoid model's transforms.
            // Restore the authored rest pose so mesh matrices and prefab rest bones agree.
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localPosition = rest[i].position;
                transforms[i].localRotation = rest[i].rotation;
                transforms[i].localScale = rest[i].scale;
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }

    static void Move(Transform shoulder, Transform elbow, Vector3 outward, Vector3 up, float ratio, float rise)
    {
        var children = shoulder.GetComponentsInChildren<Transform>(true).Where(t => t != shoulder).ToArray();
        var positions = children.Select(t => t.position).ToArray();
        var rotations = children.Select(t => t.rotation).ToArray();
        Vector3 start = shoulder.position, end = elbow.position;
        Vector3 offset = (outward * ratio + up * rise) * Vector3.Distance(start, end);
        var turn = Quaternion.FromToRotation(end - start, end - start - offset);
        shoulder.position += offset;
        shoulder.rotation = turn * shoulder.rotation;
        for (int i = 0; i < children.Length; i++)
        {
            bool twist = children[i].name.Contains("UpperarmTwist");
            float fraction = Mathf.Clamp01(Vector3.Dot(positions[i] - start, end - start) / (end - start).sqrMagnitude);
            children[i].SetPositionAndRotation(positions[i] + (twist ? offset * (1 - fraction) : Vector3.zero), twist ? turn * rotations[i] : rotations[i]);
        }
    }

    static HumanDescription Mapping(GameObject source, Dictionary<string, Transform> bones)
    {
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as ModelImporter;
        var description = importer != null ? importer.humanDescription : new HumanDescription();
        if (description.human == null || description.human.Length == 0)
        {
            var standard = AssetImporter.GetAtPath("Assets/Art/Characters/Tripo/better_player_test/better_player_test.fbx") as ModelImporter;
            if (standard == null) throw new InvalidOperationException("缺少Tripo标准Humanoid映射");
            description = standard.humanDescription;
            description.human = description.human.Where(h => bones.ContainsKey(h.boneName)).ToArray();
        }
        if (description.human.Any(h => !bones.ContainsKey(h.boneName))) throw new InvalidOperationException("模型不符合Tripo标准映射");
        return description;
    }

    public static string 导出(Result result, string folder)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出Play");
        if (!AssetDatabase.IsValidFolder(folder)) throw new InvalidOperationException("输出必须选择已存在的Assets目录");
        result.恢复原材质();
        result.恢复绑定姿势();
        string name = result.root.name + "_肩部修复";
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".asset");
        // Avatar is the main asset, meshes are subassets; the prefab references both.
        AssetDatabase.CreateAsset(result.avatar, path);
        foreach (var mesh in result.meshes) AssetDatabase.AddObjectToAsset(mesh, path);
        foreach (var t in result.root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.None;
        string prefabPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".prefab");
        result.root.transform.localPosition = result.rootPosition;
        result.root.GetComponent<Animator>().enabled = true;
        PrefabUtility.SaveAsPrefabAsset(result.root, prefabPath);
        AssetDatabase.SaveAssets();
        return prefabPath;
    }
}
