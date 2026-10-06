using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>Opt-in player regression check: -npc-animation-smoke -npc-animation-report FILE.</summary>
public sealed class NpcAnimationSmokeTest : MonoBehaviour
{
    [Serializable] public class Report
    {
        public int prefabs, actions, movingPrefabs;
        public bool passed;
        public List<string> failures = new List<string>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!Environment.GetCommandLineArgs().Contains("-npc-animation-smoke")) return;
        var host = new GameObject("NPC animation regression check");
        DontDestroyOnLoad(host);
        host.AddComponent<NpcAnimationSmokeTest>();
    }
    IEnumerator Start()
    {
        var report = new Report();
        var prefabs = Resources.LoadAll<GameObject>("NPC/Demon")
            .Where(p => p.GetComponent<NpcInstance>() != null).OrderBy(p => p.name).ToArray();
        foreach (var prefab in prefabs)
        {
            report.prefabs++;
            var parent = new GameObject("NPC animation check fixture");
            parent.SetActive(false);
            var clone = Instantiate(prefab, parent.transform);
            foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is NpcAnimator) && !(behaviour is Npc动画自检)) behaviour.enabled = false;
            foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var animator = clone.GetComponent<Animator>();
            var player = clone.GetComponent<NpcAnimator>();
            if (animator == null || player == null)
            {
                report.failures.Add(prefab.name + ": missing Animator/NpcAnimator");
                Destroy(parent); continue;
            }
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            parent.SetActive(true);
            yield return null; // Awake / animation repair / Start must all run.
            var mapping = NpcAnimationCatalog.Find(animator.runtimeAnimatorController);
            if (mapping == null || mapping.actions.Length == 0)
                report.failures.Add(prefab.name + ": no runtime animation mapping");
            else
            {
                bool moved = false;
                var bones = clone.GetComponentsInChildren<Transform>(true).Where(t => t != clone.transform).ToArray();
                foreach (var action in mapping.actions)
                {
                    report.actions++;
                    if (!player.PlayAction(action.name) || player.取动作长度(action.name) <= 0f)
                    { report.failures.Add(prefab.name + "/" + action.name + ": request rejected"); continue; }
                    // A pending initial Idle transition can finish before the requested transition starts.
                    for (int tick = 0; tick < 8; tick++)
                    {
                        animator.Update(.1f);
                        if (!animator.IsInTransition(0) && animator.GetCurrentAnimatorStateInfo(0).fullPathHash
                            == Animator.StringToHash(action.statePath)) break;
                    }
                    if (animator.GetCurrentAnimatorStateInfo(0).fullPathHash != Animator.StringToHash(action.statePath))
                        report.failures.Add(prefab.name + "/" + action.name + ": wrong state");
                    var positions = bones.Select(t => t.localPosition).ToArray();
                    var rotations = bones.Select(t => t.localRotation).ToArray();
                    animator.Update(.13f);
                    animator.Update(.13f);
                    for (int i = 0; i < bones.Length; i++)
                        if ((bones[i].localPosition - positions[i]).sqrMagnitude > .0000000001f
                            || Quaternion.Angle(bones[i].localRotation, rotations[i]) > .01f) { moved = true; break; }
                }
                if (moved) report.movingPrefabs++;
                else report.failures.Add(prefab.name + ": bones never moved");
            }
            Destroy(parent);
        }
        report.passed = report.prefabs > 0 && report.failures.Count == 0;
        var args = Environment.GetCommandLineArgs();
        int pathArg = Array.IndexOf(args, "-npc-animation-report");
        string path = pathArg >= 0 && pathArg + 1 < args.Length ? args[pathArg + 1]
            : Path.Combine(Application.dataPath, "../Builds/npc-animation-smoke.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log("[NPC animation regression] prefabs=" + report.prefabs + " actions=" + report.actions
            + " moving=" + report.movingPrefabs + " failures=" + report.failures.Count + " passed=" + report.passed);
#if !UNITY_EDITOR
        Application.Quit(report.passed ? 0 : 1);
#else
        Destroy(gameObject);
#endif
    }
}
