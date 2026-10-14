using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Rotate the entire JuLong model space +90 degrees about X without a rotated prefab wrapper.
/// Skeleton and renderer receive the same change of basis, so mesh-local vertices/bindposes stay valid.
/// Animated boundary transforms receive the same rotation (values AND derivatives), not an idle-only fix.
/// </summary>
public static class JuLongCoordinateBaker
{
    public const string SourcePath = "Assets/resources/NPC/Mount/JuLong_01/drgon.fbx";
    public const string PrefabPath = "Assets/resources/NPC/Mount/JuLong_01/JuLong_01.prefab";
    public const string ClipFolder = "Assets/resources/NPC/Mount/JuLong_01/Animations";
    public const string ControllerPath = "Assets/Animations/NPC/JuLong.controller";
    const string SkeletonPath = "BossLeiLong (merge)/BossLeiLong";
    const string RendererPath = "BossLeiLong (merge)/BossLeiLong_MengPi/BossLeiLong";
    static readonly string[] Boundaries = { SkeletonPath, RendererPath };
    static readonly Quaternion Conversion = Quaternion.Euler(90, 0, 0);

    [MenuItem("修仙/动画/烘焙巨龙整体X轴＋90°（不动场景）")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请停止Play后烘焙巨龙资源");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (!source || !controller) throw new InvalidOperationException("缺少巨龙FBX或控制器");
        var clips = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        if (clips.Length != 6) throw new InvalidOperationException("源巨龙应包含六段动画");
        if (!AssetDatabase.IsValidFolder(ClipFolder))
            AssetDatabase.CreateFolder("Assets/resources/NPC/Mount/JuLong_01", "Animations");
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // Both unanimated wrapper parents must be identity: boundaries then share model-space axes.
            foreach (string path in Boundaries)
            {
                var boundary = root.transform.Find(path);
                var original = source.transform.Find(path);
                if (!boundary || !original) throw new InvalidOperationException("巨龙层级不匹配: " + path);
                for (var parent = boundary.parent; parent != root.transform; parent = parent.parent)
                    if (parent.localPosition.sqrMagnitude > 1e-10f || Quaternion.Angle(parent.localRotation, Quaternion.identity) > .001f
                        || (parent.localScale - Vector3.one).sqrMagnitude > 1e-10f)
                        throw new InvalidOperationException("边界父节点必须保持单位变换: " + parent.name);
            }
            bool originalPose = Boundaries.All(p => Quaternion.Angle(root.transform.Find(p).localRotation,
                source.transform.Find(p).localRotation) < .001f);
            bool convertedPose = Boundaries.All(p => Quaternion.Angle(root.transform.Find(p).localRotation,
                Conversion * source.transform.Find(p).localRotation) < .001f);
            if (!originalPose && !convertedPose)
                throw new InvalidOperationException("巨龙静态边界有额外旋转，请先检查自定义修改");

            var convertedClips = new Dictionary<string, AnimationClip>();
            foreach (var clip in clips)
            {
                var converted = UnityEngine.Object.Instantiate(clip);
                converted.name = clip.name;
                foreach (string path in Boundaries) ConvertBoundaryCurves(converted, path);
                string targetPath = ClipFolder + "/" + clip.name + ".anim";
                var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
                if (saved)
                {
                    EditorUtility.CopySerialized(converted, saved);
                    UnityEngine.Object.DestroyImmediate(converted);
                    EditorUtility.SetDirty(saved);
                }
                else { saved = converted; AssetDatabase.CreateAsset(saved, targetPath); }
                convertedClips.Add(clip.name, saved);
            }
            foreach (string path in Boundaries)
            {
                var target = root.transform.Find(path);
                var original = source.transform.Find(path);
                target.localPosition = Conversion * original.localPosition;
                target.localRotation = Conversion * original.localRotation;
                target.localScale = original.localScale;
            }
            // Collider data belongs to prefab/model space as well, not the skeleton's local space.
            if (originalPose)
                foreach (var collider in root.GetComponents<CapsuleCollider>())
                {
                    collider.center = Conversion * collider.center;
                    if (collider.direction == 1) collider.direction = 2;
                    else if (collider.direction == 2) collider.direction = 1;
                }
            foreach (var layer in controller.layers) ReplaceMotions(layer.stateMachine, convertedClips);
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[巨龙坐标] +90°已烘焙：六段动画、骨架/蒙皮边界、碰撞体；成品根节点零旋转");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void ReplaceMotions(AnimatorStateMachine machine, Dictionary<string, AnimationClip> clips)
    {
        foreach (var child in machine.states)
            if (child.state.motion is AnimationClip clip && clips.TryGetValue(clip.name, out var replacement))
            {
                child.state.motion = replacement;
                EditorUtility.SetDirty(child.state);
            }
        foreach (var child in machine.stateMachines) ReplaceMotions(child.stateMachine, clips);
    }

    static EditorCurveBinding Binding(string path, string property) => EditorCurveBinding.FloatCurve(path, typeof(Transform), property);

    static void ConvertBoundaryCurves(AnimationClip clip, string path)
    {
        var rotation = "xyzw".Select(c => AnimationUtility.GetEditorCurve(clip, Binding(path, "m_LocalRotation." + c))).ToArray();
        if (rotation.Any(c => c != null))
        {
            if (rotation.Any(c => c == null)) throw new InvalidOperationException("四元数曲线缺少通道: " + clip.name);
            float s = Mathf.Sqrt(.5f);
            // Q * q. Exact linear combinations preserve imported key times and Hermite tangents.
            var rotated = new[] { Combine(rotation[0], rotation[3], s, s), Combine(rotation[1], rotation[2], s, -s),
                Combine(rotation[2], rotation[1], s, s), Combine(rotation[3], rotation[0], s, -s) };
            for (int i = 0; i < 4; i++) AnimationUtility.SetEditorCurve(clip, Binding(path, "m_LocalRotation." + "xyzw"[i]), rotated[i]);
        }
        var position = "xyz".Select(c => AnimationUtility.GetEditorCurve(clip, Binding(path, "m_LocalPosition." + c))).ToArray();
        if (position.Any(c => c != null))
        {
            if (position.Any(c => c == null)) throw new InvalidOperationException("位移曲线缺少通道: " + clip.name);
            var rotated = new[] { Copy(position[0], 1), Copy(position[2], -1), Copy(position[1], 1) };
            for (int i = 0; i < 3; i++) AnimationUtility.SetEditorCurve(clip, Binding(path, "m_LocalPosition." + "xyz"[i]), rotated[i]);
        }
    }

    static AnimationCurve Copy(AnimationCurve curve, float factor)
    {
        var keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
        { var k = keys[i]; k.value *= factor; k.inTangent *= factor; k.outTangent *= factor; keys[i] = k; }
        return new AnimationCurve(keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
    }

    static AnimationCurve Combine(AnimationCurve a, AnimationCurve b, float factorA, float factorB)
    {
        var ak = a.keys; var bk = b.keys;
        if (ak.Length != bk.Length) throw new InvalidOperationException("四元数关键帧未对齐，不能做无损转换");
        for (int i = 0; i < ak.Length; i++)
        {
            var k = ak[i]; var other = bk[i];
            if (k.time != other.time || k.weightedMode != WeightedMode.None || other.weightedMode != WeightedMode.None
                || !Finite(k.inTangent) || !Finite(k.outTangent) || !Finite(other.inTangent) || !Finite(other.outTangent))
                throw new InvalidOperationException("四元数插值格式不支持无损转换");
            k.value = factorA * k.value + factorB * other.value;
            k.inTangent = factorA * k.inTangent + factorB * other.inTangent;
            k.outTangent = factorA * k.outTangent + factorB * other.outTangent;
            ak[i] = k;
        }
        return new AnimationCurve(ak) { preWrapMode = a.preWrapMode, postWrapMode = a.postWrapMode };
    }
    static bool Finite(float value) => !float.IsInfinity(value) && !float.IsNaN(value);
}
