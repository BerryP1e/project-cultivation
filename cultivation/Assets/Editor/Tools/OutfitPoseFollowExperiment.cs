using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>仅金丹着装的可撤回姿态随动试验；不修改正式外观定义或场景。</summary>
[InitializeOnLoad]
public static class OutfitPoseFollowExperiment
{
    const string Key = "GoldenCorePoseFollowExperiment";
    const string Folder = "Assets/Art/Characters/OutfitCloth/Experiments/PoseFollow";
    const string Authoring = "Assets/Art/Characters/OutfitCloth/Authoring/app_goldencore-pose-follow.json";
    static double nextCheck;

    static OutfitPoseFollowExperiment() { EditorApplication.update += Update; }

    [MenuItem("修仙/外观/金丹姿态随动试验/生成样件")]
    public static void 生成样件()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止运行再生成样件");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Art/Characters/OutfitCloth/Experiments", "PoseFollow");
        var appearance = UnityEngine.Object.Instantiate(AppearanceDatabase.取().取("app_goldencore"));
        try
        {
            var reference = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/外观/修仙者.prefab")
                .GetComponentInChildren<SkinnedMeshRenderer>(true);
            OutfitClothBaker.Bake(appearance, reference, File.ReadAllText(Authoring), Folder);
            appearance.衣服随动.姿态空间随动 = true;
            appearance.衣服随动.衣摆末端限角 = 55;
            appearance.衣服随动.衣摆下垂 = .75f;
            appearance.衣服随动.衣摆波幅 = .032f;
            EditorUtility.SetDirty(appearance.衣服随动);
            AssetDatabase.SaveAssets();
        }
        finally { UnityEngine.Object.DestroyImmediate(appearance); }
    }

    [MenuItem("修仙/外观/金丹姿态随动试验/启用游戏内试穿")]
    public static void 启用试穿()
    {
        if (!AssetDatabase.LoadAssetAtPath<AppearanceClothProfile>(Folder + "/app_goldencore.asset"))
            throw new InvalidOperationException("请先生成金丹姿态随动样件");
        SessionState.SetBool(Key, true);
        nextCheck = 0;
        Debug.Log("金丹姿态随动试穿已启用：运行后穿金丹着装生效，仅本编辑器会话，不写场景或正式外观。");
    }

    [MenuItem("修仙/外观/金丹姿态随动试验/停用游戏内试穿")]
    public static void 停用试穿()
    {
        SessionState.SetBool(Key, false);
        if (EditorApplication.isPlaying)
            foreach (var actor in UnityEngine.Object.FindObjectsOfType<玩家外观>())
                if (actor.当前外观 && actor.当前外观.id == "app_goldencore")
                {
                    var old = actor.当前外观;
                    actor.装备(null); actor.装备(old);
                }
        Debug.Log("金丹姿态随动试穿已停用。");
    }

    static void Update()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying
            || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + .25;
        var profile = AssetDatabase.LoadAssetAtPath<AppearanceClothProfile>(Folder + "/app_goldencore.asset");
        if (!profile) return;
        foreach (var actor in UnityEngine.Object.FindObjectsOfType<玩家外观>())
        {
            if (!actor.当前外观 || actor.当前外观.id != "app_goldencore" || !actor.玩家视觉) continue;
            var rig = actor.玩家视觉.GetComponentsInChildren<AppearanceClothRig>()
                .FirstOrDefault(r => r.当前配置 && r.当前配置 != profile);
            if (!rig) continue;
            var renderer = rig.GetComponent<SkinnedMeshRenderer>();
            AppearanceClothRig.应用(renderer, profile, renderer.bones.Take(profile.人体骨名.Length).ToArray());
        }
    }
}
