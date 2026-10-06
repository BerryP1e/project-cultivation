using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class NpcAnimationCatalogBuilder : IPreprocessBuildWithReport
{
    const string Path = "Assets/resources/NPC数据/NPC动作映射.asset";
    public int callbackOrder => -100;
    public void OnPreprocessBuild(BuildReport report) => Bake();

    [MenuItem("Cultivation/NPC Assets/Bake Runtime Animation Catalog")]
    public static void Bake()
    {
        var controllers = new HashSet<AnimatorController>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/resources/NPC" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var player in prefab.GetComponentsInChildren<NpcAnimator>(true))
            {
                RuntimeAnimatorController controller = player.GetComponent<Animator>().runtimeAnimatorController;
                while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
                if (!(controller is AnimatorController concrete))
                    throw new BuildFailedException("NPC has no usable controller: " + prefab.name);
                controllers.Add(concrete);
            }
        }
        var entries = new List<NpcAnimationCatalog.Controller>();
        foreach (var controller in controllers.OrderBy(c => AssetDatabase.GetAssetPath(c)))
        {
            var sm = controller.layers[0].stateMachine;
            bool usesAction = controller.parameters.Any(p => p.name == "Action" && p.type == AnimatorControllerParameterType.Int);
            var actions = new List<NpcAnimationCatalog.Action>();
            foreach (var child in sm.states)
            {
                var state = child.state;
                var indices = sm.anyStateTransitions.Where(t => t.destinationState == state)
                    .SelectMany(t => t.conditions)
                    .Where(c => c.parameter == "Action" && c.mode == AnimatorConditionMode.Equals)
                    .Select(c => (int)c.threshold).Distinct().ToArray();
                if (usesAction && indices.Length != 1)
                    throw new BuildFailedException("Unsupported NPC state mapping: " + controller.name + "/" + state.name);
                if (!(state.motion is AnimationClip clip)) continue; // Empty imported states are not playable actions.
                actions.Add(new NpcAnimationCatalog.Action { name = state.name, index = usesAction ? indices[0] : Array.IndexOf(sm.states.Select(c => c.state).ToArray(), state), clip = clip,
                    statePath = controller.layers[0].name + "." + state.name });
            }
            if (actions.Select(a => a.index).Distinct().Count() != actions.Count)
                throw new BuildFailedException("Invalid NPC Action indices: " + controller.name);
            entries.Add(new NpcAnimationCatalog.Controller { controller = controller, usesActionParameter = usesAction, actions = actions.OrderBy(a => a.index).ToArray() });
        }
        var asset = AssetDatabase.LoadAssetAtPath<NpcAnimationCatalog>(Path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<NpcAnimationCatalog>(); AssetDatabase.CreateAsset(asset, Path); }
        asset.controllers = entries.ToArray();
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("[NPC animation catalog] Baked " + entries.Count + " controllers, " + entries.Sum(c => c.actions.Length) + " actions; scenes and prefabs untouched.");
    }
}
