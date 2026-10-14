using UnityEditor;
using UnityEngine;

/// <summary>地面Ready保留站姿与Prop1；头颈回正，双臂改用已校准的御风悬停曲线。仅更新成品片段。</summary>
public static class LingxuGroundIdleBaker
{
    public const string Path = "Assets/resources/技能动作/灵虚剑决/持剑_Idle.anim";
    [MenuItem("修仙/动画/修正灵虚地面待机（不动场景）")]
    public static void Bake()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("请在停止Play后修改待机资源");
        var ground = AssetDatabase.LoadAssetAtPath<AnimationClip>(Path);
        var hover = Resources.Load<AnimationClip>("技能动作/御风八向/灵虚剑决/御风八向_悬停");
        if (!ground || !hover) throw new System.InvalidOperationException("缺少灵虚待机资源");
        float length = ground.length;
        var changes = new System.Collections.Generic.List<EditorCurveBinding>();
        var curves = new System.Collections.Generic.List<AnimationCurve>();
        foreach (var binding in AnimationUtility.GetCurveBindings(hover))
        {
            if (binding.type != typeof(Animator) || !Arm(binding.propertyName)) continue;
            var source = AnimationUtility.GetEditorCurve(hover, binding);
            var keys = source.keys;
            float scale = length / hover.length;
            for (int i = 0; i < keys.Length; i++) {
                var key = keys[i]; key.time *= scale; key.inTangent /= scale; key.outTangent /= scale; keys[i] = key;
            }
            var curve = new AnimationCurve(keys) { preWrapMode = WrapMode.Loop, postWrapMode = WrapMode.Loop };
            // 连续循环边界必须同值、同切线；保留中段自然的小幅呼吸变化。
            if (keys.Length > 1) { var last = keys[keys.Length - 1]; last.value = keys[0].value; last.inTangent = keys[0].inTangent; last.outTangent = keys[0].outTangent; keys[keys.Length - 1] = last; curve.keys = keys; }
            changes.Add(binding); curves.Add(curve);
        }
        foreach (var binding in AnimationUtility.GetCurveBindings(ground))
            if (binding.type == typeof(Animator) && (binding.propertyName.StartsWith("Head ") || binding.propertyName.StartsWith("Neck "))) {
                changes.Add(binding); curves.Add(AnimationCurve.Constant(0, length, 0));
            }
        AnimationUtility.SetEditorCurves(ground, changes.ToArray(), curves.ToArray());
        var settings = AnimationUtility.GetAnimationClipSettings(ground); settings.loopTime = true; settings.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(ground, settings);
        EditorUtility.SetDirty(ground); AssetDatabase.SaveAssets();
        Debug.Log("[灵虚地面待机] 更新" + changes.Count + "条头颈/双臂曲线，站姿与独立剑轨迹保留");
    }
    static bool Arm(string name) => name.StartsWith("LeftHand.") || name.StartsWith("RightHand.")
        || ((name.StartsWith("Left ") || name.StartsWith("Right "))
            && (name.Contains("Shoulder") || name.Contains("Arm") || name.Contains("Forearm") || name.Contains("Hand")));
}
