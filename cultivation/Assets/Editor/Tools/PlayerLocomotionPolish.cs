using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>只修玩家专用 Walk/Run 与控制器引用；不改源 FBX、持械动作或场景。</summary>
public static class PlayerLocomotionPolish
{
    [MenuItem("修仙/动画/减轻默认移动侧摆（不动场景）")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first");
        const string folder = "Assets/Animations/Locomotion";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Animations", "Locomotion");
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/PlayerLocomotion.controller");
        foreach (var name in new[] { "Armature|Walk_Loop", "Armature|Sprint_Loop" })
        {
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/resources/Animation Library/1/UAL1_Standard.fbx")
                .OfType<AnimationClip>().First(c => c.name == name);
            var clip = new AnimationClip { name = name, frameRate = source.frameRate };
            foreach (var b in AnimationUtility.GetCurveBindings(source))
            {
                if (b.type != typeof(Animator)) continue;
                var curve = AnimationUtility.GetEditorCurve(source, b);
                string p = b.propertyName;
                bool torso = (p.Contains("Chest") || p.Contains("Spine") || p.Contains("Head") || p.Contains("Neck")) && (p.Contains("Left-Right") || p.Contains("Twist") || p.Contains("Turn"));
                bool legs = (p.Contains("Leg") || p.Contains("Foot")) && (p.Contains("In-Out") || p.Contains("Twist"));
                if (torso || legs || p == "RootT.x")
                {
                    float factor = torso ? .25f : .3f;
                    var keys = curve.keys; float center = (keys.Min(k => k.value) + keys.Max(k => k.value)) * .5f;
                    for (int i = 0; i < keys.Length; i++) { keys[i].value = center + (keys[i].value - center) * factor; keys[i].inTangent *= factor; keys[i].outTangent *= factor; }
                    curve.keys = keys;
                }
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            DampRootSway(clip, source);
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            string path = folder + "/" + (name.Contains("Walk") ? "PlayerWalkSteady" : "PlayerRunSteady") + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing == null) { AssetDatabase.CreateAsset(clip, path); existing = clip; }
            else { EditorUtility.CopySerialized(clip, existing); Object.DestroyImmediate(clip); }
            foreach (var layer in controller.layers) Replace(layer.stateMachine, name, existing);
            EditorUtility.SetDirty(existing);
        }
        EditorUtility.SetDirty(controller); AssetDatabase.SaveAssets();
        Debug.Log("[Locomotion] 已减轻默认胸颈侧扭与腿脚侧摆；源 FBX/持械/场景不变");
    }
    static void DampRootSway(AnimationClip clip, AnimationClip source)
    {
        // Humanoid 的整身侧转也在 RootQ；只缩肌肉仍会留下髋骨左右摆动。
        var curves = "xyzw".Select(axis => AnimationUtility.GetEditorCurve(source,
            EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axis))).ToArray();
        if (curves.Any(c => c == null)) return;
        int count = Mathf.Max(2, Mathf.CeilToInt(source.length * source.frameRate));
        var rotations = new Quaternion[count + 1];
        var sum = Vector4.zero;
        for (int i = 0; i <= count; i++)
        {
            float time = source.length * i / count;
            var q = new Quaternion(curves[0].Evaluate(time), curves[1].Evaluate(time), curves[2].Evaluate(time), curves[3].Evaluate(time)).normalized;
            if (i > 0 && Quaternion.Dot(rotations[0], q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            rotations[i] = q; sum += new Vector4(q.x, q.y, q.z, q.w);
        }
        var center = new Quaternion(sum.x, sum.y, sum.z, sum.w).normalized;
        var keys = new Keyframe[4][];
        for (int j = 0; j < 4; j++) keys[j] = new Keyframe[count + 1];
        for (int i = 0; i <= count; i++)
        {
            var q = Quaternion.Slerp(center, rotations[i], .3f);
            float time = source.length * i / count;
            for (int j = 0; j < 4; j++) keys[j][i] = new Keyframe(time, q[j]);
        }
        for (int j = 0; j < 4; j++)
        {
            var curve = new AnimationCurve(keys[j]);
            for (int i = 0; i <= count; i++) curve.SmoothTangents(i, 0);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + "xyzw"[j]), curve);
        }
    }
    static void Replace(AnimatorStateMachine machine, string name, AnimationClip replacement)
    {
        foreach (var state in machine.states)
        {
            if (state.state.motion is BlendTree tree) ReplaceTree(tree, name, replacement);
            else if (state.state.motion != null && state.state.motion.name == name) state.state.motion = replacement;
        }
        foreach (var child in machine.stateMachines) Replace(child.stateMachine, name, replacement);
    }
    static void ReplaceTree(BlendTree tree, string name, AnimationClip replacement)
    {
        var children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].motion is BlendTree nested) ReplaceTree(nested, name, replacement);
            else if (children[i].motion != null && children[i].motion.name == name) children[i].motion = replacement;
        }
        tree.children = children;
    }
}
