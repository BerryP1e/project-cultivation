using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

/// <summary>保留定稿旋身的身体/腿趋势，离线耦合肩臂腕指；地面后闪只提取悬浮可用的趋势。</summary>
public static class YufengDodgeBaker
{
    public const string Folder = "Assets/Animations/凭虚御风/闪避";
    const string IdlePath = "Assets/Animations/凭虚御风/八向御风/御风八向_悬停.anim";
    const string PlayerPath = "Assets/Art/Characters/Tripo/better_player_test/better_player_test.fbx";
    const string CatalogPath = "Assets/resources/Abilities/YufengDodgeAnimations.asset";

    [MenuItem("修仙/动画/重建御风闪避（不动场景）")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("请先停止 Play");
        var spin = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/凭虚御风/Kimodo/御风_左旋闪避.anim");
        var back = AssetDatabase.LoadAllAssetsAtPath("Assets/resources/Animation Library/Magical-Knight_Set/Animation/Humanoid/inplace/avoid_back_inplace.fbx")
            .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdlePath);
        if (!spin || !spin.humanMotion || !back.humanMotion || !idle) throw new InvalidOperationException("缺少有效的人形闪避/御风基准");
        var right = Mirror(spin);
        try
        {
            var catalog = AssetDatabase.LoadAssetAtPath<FlightDodgeAnimations>(CatalogPath);
            if (!catalog) { catalog = ScriptableObject.CreateInstance<FlightDodgeAnimations>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.自由手 = MakeSet("", "自由手", idle, spin, right, back);
            string[] profiles = { "八九玄功", "太虚剑决", "灵虚剑决" };
            string[] ids = { "gongfa_jiuba_xuangong", "gongfa_taixu_jianjue", "gongfa_lingxu_jianjue" };
            catalog.持械 = profiles.Select((name, i) => MakeSet(ids[i], name,
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/resources/技能动作/御风八向/" + name + "/御风八向_悬停.anim"), spin, right, back)).ToArray();
            ConfigureController(AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/PlayerLocomotion.controller"));
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("[御风闪避] 独立前倾旋身、左旋、右旋与后闪及三套持械变体已烘焙；保留源片段与场景");
        }
        finally { Object.DestroyImmediate(right); }
    }

    public static void RebuildJiuba()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before rebaking");
        var spin=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/凭虚御风/Kimodo/御风_左旋闪避.anim");
        var back=AssetDatabase.LoadAllAssetsAtPath("Assets/resources/Animation Library/Magical-Knight_Set/Animation/Humanoid/inplace/avoid_back_inplace.fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/resources/技能动作/御风八向/八九玄功/御风八向_悬停.anim");
        var right=Mirror(spin);
        try{MakeSet("gongfa_jiuba_xuangong","八九玄功",idle,spin,right,back);}
        finally{Object.DestroyImmediate(right);}
    }

    /// <summary>只让闪避所在Action状态独立加速，移动状态不继承其倍率。</summary>
    public static void ConfigureController(AnimatorController controller)
    {
        if (!controller) throw new InvalidOperationException("缺少玩家动作控制器");
        string parameter = PlayerAnimationController.闪避动作倍率参数;
        if (!controller.parameters.Any(p => p.name == parameter))
            controller.AddParameter(new AnimatorControllerParameter { name = parameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var action = controller.layers[0].stateMachine.states.First(s => s.state.name == "Action").state;
        action.speedParameter = parameter;
        action.speedParameterActive = true;
        EditorUtility.SetDirty(action); EditorUtility.SetDirty(controller);
    }

    static FlightDodgeAnimations.Set MakeSet(string id, string profile, AnimationClip idle, AnimationClip left, AnimationClip right, AnimationClip back)
    {
        if (!idle) throw new InvalidOperationException("缺少御风持械基准 " + profile);
        return new FlightDodgeAnimations.Set { 功法id = id,
            左旋 = Adapt(left, idle, profile, "左旋", false), 右旋 = Adapt(right, idle, profile, "右旋", false),
            后闪 = Adapt(back, idle, profile, "后闪", true), 前旋 = Adapt(left, idle, profile, "前旋", false, true) };
    }

    static AnimationClip Adapt(AnimationClip source, AnimationClip idle, string profile, string direction, bool backwards, bool forwards = false)
    {
        var forward=forwards?AssetDatabase.LoadAssetAtPath<AnimationClip>(profile=="自由手"
            ?"Assets/Animations/凭虚御风/八向御风/御风八向_前.anim"
            :"Assets/resources/技能动作/御风八向/"+profile+"/御风八向_前.anim"):null;
        if(forwards&&!forward)throw new InvalidOperationException("缺少御风前进基准 "+profile);
        var clip = Object.Instantiate(source); clip.name = "御风闪避_" + direction + "_" + profile; clip.frameRate = 60;
        var humanoidProperties = new System.Collections.Generic.HashSet<string>(HumanTrait.MuscleName.Select(HandMotionRetargeting.CurveName));
        foreach (var b in AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Animator) && !b.propertyName.StartsWith("Root") && !humanoidProperties.Contains(b.propertyName)).ToArray())
            AnimationUtility.SetEditorCurve(clip, b, null); // 无脚IK的闪避不保留源地面脚/手IK目标曲线。
        // 独立武器节点沿用各功法悬停参考；不携带源动画的无关 Transform 曲线。
        foreach (var b in AnimationUtility.GetCurveBindings(clip).Where(b => b.type != typeof(Animator)).ToArray()) AnimationUtility.SetEditorCurve(clip, b, null);
        foreach (var b in AnimationUtility.GetCurveBindings(idle).Where(b => b.type != typeof(Animator))) AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(idle, b));
        bool grip = profile == "八九玄功" || profile == "太虚剑决";
        int frames = Mathf.CeilToInt(source.length * 60);
        foreach (string muscle in HumanTrait.MuscleName)
        {
            string name = HandMotionRetargeting.CurveName(muscle);
            var src = Get(source, name); var ready = Get(idle, name);
            var curve = new AnimationCurve(); float rest = ready != null ? ready.Evaluate(0) : 0;
            bool arm = muscle.Contains(" Arm ") || muscle.Contains(" Forearm ") || muscle.Contains(" Shoulder ") || muscle.Contains(" Hand ") || name.Contains("Hand.");
            bool rightHand = muscle.StartsWith("Right ");
            // 肌肉定义是骨骼局部坐标：身体旋转时肩臂自然跟随，不能钉死腕的世界朝向。
            if (arm && !(rightHand && grip))
            {
                if (muscle.EndsWith("Arm Down-Up")) rest = Mathf.Clamp(rest - .3f, -.75f, -.25f);
                if (muscle.EndsWith("Forearm Stretch")) rest = Mathf.Min(rest, -.05f);
            }
            for (int f = 0; f <= frames; f++)
            {
                float u = (float)f / frames, time = source.length * u;
                float envelope = Mathf.SmoothStep(0, 1, Mathf.Min(u / .14f, (1 - u) / .18f));
                float value = src != null ? src.Evaluate(time) : rest;
                float weight = arm ? (rightHand && grip ? .06f : .16f) : 1f;
                if (backwards)
                {
                    // 腿保持御风弯曲基准，仅留后撤收腿的趋势；足踝不保留蹬地滚动。
                    if (muscle.Contains("Upper Leg") || muscle.Contains("Lower Leg")) weight = .38f;
                    else if (muscle.Contains("Foot") || muscle.Contains("Toes")) weight = .1f;
                    else if (!arm) weight = .65f;
                }
                // 起止回到各自御风姿态，运动段采用收臂基准＋原片小幅惯性。
                float baseline = ready != null ? ready.Evaluate(0) : 0;
                value = Mathf.Lerp(baseline, Mathf.Lerp(rest, value, weight), envelope);
                curve.AddKey(time, Mathf.Clamp(value, -1, 1));
            }
            Put(clip, name, curve);
        }
        var roots = Enumerable.Range(0, 7).Select(_ => new AnimationCurve()).ToArray();
        var baseQ = Rotation(idle, 0); var firstQ = Rotation(source, 0);
        float sourceHeight = Get(source, "RootT.y").Evaluate(0), idleHeight = Get(idle, "RootT.y").Evaluate(0);
        Quaternion previous = baseQ;
        for (int f = 0; f <= frames; f++)
        {
            float u = (float)f / frames, time = source.length * u;
            float envelope = Mathf.SmoothStep(0, 1, Mathf.Min(u / .14f, (1 - u) / .18f));
            var delta = Quaternion.Inverse(firstQ) * Rotation(source, time);
            if (backwards) delta = Quaternion.Slerp(Quaternion.identity, delta, .55f);
            var q = Quaternion.Slerp(baseQ, baseQ * delta, envelope);
            if(forwards)
            {
                // 保留原侧闪全部旋转/四肢，只把身体连同原旋转轴一起略微前倾。
                var tilt=Quaternion.Slerp(Quaternion.identity,Rotation(forward,0)*Quaternion.Inverse(baseQ),envelope*.35f);
                q=tilt*q;
            }
            if (Quaternion.Dot(previous, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w); previous = q;
            // 不拼地面起跳高度；仅保留很轻的浮动。实际悬浮高度由御风组件控制。
            float y = idleHeight + Mathf.Clamp((Get(source, "RootT.y").Evaluate(time) - sourceHeight) * (backwards ? .12f : .5f), -.04f, .07f) * envelope;
            var p = new Vector3(0, y, 0);
            for (int k = 0; k < 7; k++) roots[k].AddKey(time, k < 3 ? p[k] : q[k - 3]);
        }
        for (int k = 0; k < 7; k++) Put(clip, k < 3 ? "RootT." + "xyz"[k] : "RootQ." + "xyzw"[k - 3], roots[k]);
        YufengHandPoseBaker.Hands(clip, grip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = settings.loopBlend = false;
        settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
        settings.loopBlendOrientation = settings.loopBlendPositionY = settings.loopBlendPositionXZ = true;
        settings.heightFromFeet = false; AnimationUtility.SetAnimationClipSettings(clip, settings);
        clip.EnsureQuaternionContinuity();
        if(profile=="八九玄功"){JiubaHandPoseBaker.Apply(clip,false);JiubaCarryPoseBaker.Apply(clip);}
        string path = Folder + "/" + profile + "/" + clip.name + ".anim";
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (old) { EditorUtility.CopySerialized(clip, old); EditorUtility.SetDirty(old); Object.DestroyImmediate(clip); return old; }
        AssetDatabase.CreateAsset(clip, path); return clip;
    }

    static AnimationClip Mirror(AnimationClip source)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath), scene);
        var animator = rig.GetComponent<Animator>(); animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
        var controller = new AnimatorController(); controller.AddLayer("Mirror");
        var state = controller.layers[0].stateMachine.AddState("Pose"); state.motion = source; state.mirror = true; controller.layers[0].stateMachine.defaultState = state;
        var graph = PlayableGraph.Create("FlightDodgeMirror"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playable = AnimatorControllerPlayable.Create(graph, controller); AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(playable); graph.Play();
        var clip = Object.Instantiate(source); var curves = Enumerable.Range(0, HumanTrait.MuscleCount).Select(_ => new AnimationCurve()).ToArray();
        try
        {
            using (var handler = new HumanPoseHandler(animator.avatar, rig.transform))
            {
                var pose = new HumanPose(); int frames = Mathf.CeilToInt(source.length * 60);
                for (int f = 0; f <= frames; f++)
                {
                    float u = (float)f / frames; playable.Play(state.nameHash, 0, u); graph.Evaluate(0); handler.GetHumanPose(ref pose);
                    for (int m = 0; m < curves.Length; m++) curves[m].AddKey(source.length * u, pose.muscles[m]);
                }
            }
            for (int m = 0; m < curves.Length; m++) Put(clip, HandMotionRetargeting.CurveName(HumanTrait.MuscleName[m]), curves[m]);
            foreach (string property in new[] { "RootT.x", "RootQ.y", "RootQ.z" })
            {
                var c = Get(source, property); if (c == null) continue;
                var keys = c.keys; for (int k = 0; k < keys.Length; k++) { keys[k].value *= -1; keys[k].inTangent *= -1; keys[k].outTangent *= -1; }
                c.keys = keys; Put(clip, property, c);
            }
            return clip;
        }
        finally { graph.Destroy(); Object.DestroyImmediate(controller); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    static Quaternion Rotation(AnimationClip c, float time) => new Quaternion(Get(c, "RootQ.x").Evaluate(time), Get(c, "RootQ.y").Evaluate(time), Get(c, "RootQ.z").Evaluate(time), Get(c, "RootQ.w").Evaluate(time)).normalized;
    static AnimationCurve Get(AnimationClip c, string name) => AnimationUtility.GetEditorCurve(c, EditorCurveBinding.FloatCurve("", typeof(Animator), name));
    static void Put(AnimationClip c, string name, AnimationCurve curve)
    {
        curve = Reduce(curve, name.StartsWith("RootQ.") ? .00008f : .0005f);
        for (int k = 0; k < curve.length; k++) { AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear); }
        AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve("", typeof(Animator), name), curve);
    }
    // 只压缩离线的线性采样，误差有限；常量曲线仅保留首尾，不增加运行时处理。
    static AnimationCurve Reduce(AnimationCurve curve, float tolerance)
    {
        var keys = curve.keys; if (keys.Length < 3) return curve;
        var keep = new bool[keys.Length]; keep[0] = keep[keys.Length - 1] = true;
        void Split(int first, int last)
        {
            float error = tolerance; int at = -1;
            for (int i = first + 1; i < last; i++)
            {
                float u = (keys[i].time - keys[first].time) / (keys[last].time - keys[first].time);
                float difference = Mathf.Abs(keys[i].value - Mathf.Lerp(keys[first].value, keys[last].value, u));
                if (difference > error) { error = difference; at = i; }
            }
            if (at < 0) return; keep[at] = true; Split(first, at); Split(at, last);
        }
        Split(0, keys.Length - 1);
        return new AnimationCurve(keys.Where((_, i) => keep[i]).ToArray());
    }
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
