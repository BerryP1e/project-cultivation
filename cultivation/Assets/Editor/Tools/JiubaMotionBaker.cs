using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Rebuild player-specific weapon motions without modifying scenes or asset GUIDs.</summary>
public static class JiubaMotionBaker
{
    const string Folder = "Assets/resources/技能动作/八九玄功/";
    const string Library = "Assets/resources/Animation Library/1/UAL1_Standard.fbx";
    const string KnightGeneric = "Assets/resources/Animation Library/Magical-Knight_Set/Animation/Generic/move_run_01.fbx";
    const string KnightHuman = "Assets/resources/Animation Library/Magical-Knight_Set/Animation/Humanoid/move_run_01.fbx";

    static AnimationClip GroundMove()
    {
        // 两份FBX字节完全相同，只有导入器的Generic/Humanoid模式不同。
        // 使用现有Humanoid导入避免改动用户指定的Generic原资产；不允许悄悄换成另一套动作。
        using (var sha = System.Security.Cryptography.SHA256.Create())
            if (!sha.ComputeHash(System.IO.File.ReadAllBytes(KnightGeneric)).SequenceEqual(sha.ComputeHash(System.IO.File.ReadAllBytes(KnightHuman))))
                throw new InvalidOperationException("Knight Generic/Humanoid source mismatch; rebuild the retarget reference from the requested Generic FBX");
        return Load(KnightHuman);
    }

    static AnimationClip Load(string path, string name = null)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .First(c => !c.name.StartsWith("__preview__") && (name == null || c.name == name));
    }

    sealed class Sampler : IDisposable
    {
        public readonly GameObject root;
        public readonly Animator animator;
        public readonly Transform[] bones;
        public readonly string[] paths;
        readonly Vector3[] positions;
        readonly Quaternion[] rotations;
        public Sampler(GameObject player)
        {
            // Clone only the visual rig. Gameplay OnDisable/OnDestroy callbacks on Player
            // must not run on disposable editor sampling objects.
            root = UnityEngine.Object.Instantiate(player.GetComponentInChildren<Animator>(true).gameObject);
            root.name = "JiubaSampling";
            foreach (var b in root.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator = root.GetComponentInChildren<Animator>(true);
            animator.runtimeAnimatorController = null;
            var armature = animator.transform.Find("Armature");
            if (armature == null) throw new InvalidOperationException("Player Armature missing");
            bones = armature.GetComponentsInChildren<Transform>(true);
            paths = bones.Select(t => AnimationUtility.CalculateTransformPath(t, animator.transform)).ToArray();
            positions = bones.Select(t => t.localPosition).ToArray();
            rotations = bones.Select(t => t.localRotation).ToArray();
        }
        public void Sample(AnimationClip clip, float time)
        {
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = positions[i];
                bones[i].localRotation = rotations[i];
            }
            if (clip.humanMotion) clip.SampleAnimation(animator.gameObject, time);
            else
            {
                // Generic curve evaluation must not rely on Humanoid SampleAnimation.
                var lookup = paths.Select((p, i) => new { p, i }).ToDictionary(x => x.p, x => x.i);
                var ps = (Vector3[])positions.Clone();
                var qs = (Quaternion[])rotations.Clone();
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (b.type != typeof(Transform) || !lookup.TryGetValue(b.path, out int index)) continue;
                    float v = AnimationUtility.GetEditorCurve(clip, b).Evaluate(time);
                    int axis = "xyzw".IndexOf(b.propertyName[b.propertyName.Length - 1]);
                    if (axis < 0) continue;
                    if (b.propertyName.StartsWith("m_LocalPosition.") || b.propertyName.StartsWith("localPosition.")) ps[index][axis] = v;
                    else if (b.propertyName.StartsWith("m_LocalRotation.") || b.propertyName.StartsWith("localRotation.")) qs[index][axis] = v;
                }
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i].localPosition = ps[i];
                    bones[i].localRotation = qs[i].normalized;
                }
            }
        }
        public void Dispose() { UnityEngine.Object.DestroyImmediate(root); }
    }

    [MenuItem("修仙/动画/重建八九玄功动作（不动场景）")]
    public static void 重建()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before rebuilding animations");
        var player = GameObject.Find("Player");
        if (player == null) throw new InvalidOperationException("Open a player scene first");
        JiubaCarryPoseBaker.校准武器();
        using (var upper = new Sampler(player))
        using (var lower = new Sampler(player))
        {
            var idle = Load(Library, "Armature|Idle_Loop");
            var flight = Load("Assets/Animations/凭虚御风/御风_Idle.anim");
            foreach (var who in new[] { "长刀女", "杨戬" })
            foreach (var attack in new[] { "Attack1", "Attack2" })
            {
                string source = who == "长刀女" ? "ChangDaoNv" : "YangJian";
                var clip = Load("Assets/resources/NPC/Human/" + source + "_Animation/" + source + "@" + attack + ".FBX");
                string name = who + "_" + attack;
                // All four ground attacks retain the complete source motion, including the
                // authored jump in YangJian Attack2. Only the flight variants split the body.
                Bake(upper, lower, clip, idle, name, clip.length, false, false, false);
                Bake(upper, lower, clip, flight, name + "_御风", clip.length, false, true, false);
            }
            var run = GroundMove();
            var yangIdle = Load("Assets/resources/NPC/Human/YangJian_Animation/YangJian@Idle.FBX");
            var names = new[] { "持刀_站", "持刀_走", "持刀_跑", "持刀_御风Idle", "持刀_御风前进" };
            var bases = new[] { idle, Load(Library, "Armature|Walk_Loop"), Load(Library, "Armature|Sprint_Loop"), flight,
                Load("Assets/Animations/凭虚御风/御风_前进.anim") };
            for (int i = 0; i < names.Length; i++)
            {
                // 地面移动用用户指定的Knight完整步态，持戟只覆盖右臂。
                // 保留片段名以兼容现有控制器。
                bool groundMoving = i == 1 || i == 2;
                if (groundMoving)
                    Bake(upper, lower, run, bases[i], names[i], run.length, true, false, false);
                else
                {
                    // 持械仅替换右臂；玩家胸腰和御风躯干沿用各自基础动作，
                    // 不把杨戬为不同体型制作的脊柱后弯移植到玩家身上。
                    var carry = UnityEngine.Object.Instantiate(bases[i]);
                    carry.name = names[i];
                    var settings = AnimationUtility.GetAnimationClipSettings(carry);
                    settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(carry, settings);
                    Save(carry, names[i], yangIdle);
                }
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[JiubaMotionBaker] rebuilt 13 clips; GUIDs preserved; scenes untouched");
    }

    [MenuItem("修仙/动画/仅修正长戟两次前刺（不动场景）")]
    public static void 重建前刺()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        var player=GameObject.Find("Player");if(!player)throw new InvalidOperationException("Player missing");
        using(var upper=new Sampler(player))using(var lower=new Sampler(player)){
            var idle=Load(Library,"Armature|Idle_Loop");var flight=Load("Assets/Animations/凭虚御风/御风_Idle.anim");
            foreach(var who in new[]{"长刀女","杨戬"}){
                string source=who=="长刀女"?"ChangDaoNv":"YangJian";
                var clip=Load("Assets/resources/NPC/Human/"+source+"_Animation/"+source+"@Attack1.FBX");
                Bake(upper,lower,clip,idle,who+"_Attack1",clip.length,false,false,false);
                Bake(upper,lower,clip,flight,who+"_Attack1_御风",clip.length,false,true,false);
            }
        }
        AssetDatabase.SaveAssets();
    }

    public static void 重建地面移动()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
        var player=GameObject.Find("Player");if(!player)throw new InvalidOperationException("Player missing");
        using(var upper=new Sampler(player))using(var lower=new Sampler(player)){
            var run=GroundMove();foreach(string name in new[]{"持刀_走","持刀_跑"})Bake(upper,lower,run,run,name,run.length,true,false,false);
        }
        AssetDatabase.SaveAssets();
    }

    static void Bake(Sampler upper, Sampler lower, AnimationClip source, AnimationClip basis,
        string name, float duration, bool loop, bool split, bool freeze)
    {
        if (split && name.EndsWith("Attack2_御风"))
        {
            // The two sweeping attacks rely on the authored hips/root turn. Splitting
            // off those hips changes the torso/weapon relationship and twists the slash.
            var ground = Load(Folder + name.Replace("_御风", "") + ".anim");
            string path = Folder + name + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var complete = UnityEngine.Object.Instantiate(ground); complete.name = name;
            lower.Sample(basis, 0);
            var flightPose = new HumanPose();
            using (var handler = new HumanPoseHandler(lower.animator.avatar, lower.animator.transform))
                handler.GetHumanPose(ref flightPose);
            for (int i = 0; i < 3; i++)
            {
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT." + "xyz"[i]);
                var curve = existing ? AnimationUtility.GetEditorCurve(existing, binding) : null;
                AnimationUtility.SetEditorCurve(complete, binding, curve ?? AnimationCurve.Constant(0, duration, flightPose.bodyPosition[i]));
            }
            var flightSettings = AnimationUtility.GetAnimationClipSettings(ground);
            flightSettings.loopTime = false; flightSettings.loopBlendPositionY = true; flightSettings.keepOriginalPositionY = true;
            flightSettings.loopBlendOrientation = true; flightSettings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(complete, flightSettings);
            if (existing) { EditorUtility.CopySerialized(complete, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(complete); }
            else AssetDatabase.CreateAsset(complete, path);
            return;
        }
        if (!split)
        {
            bool moving = name == "持刀_走" || name == "持刀_跑";
            var native = moving ? new AnimationClip { frameRate = source.frameRate } : UnityEngine.Object.Instantiate(source);
            if (moving)
                foreach (var binding in AnimationUtility.GetCurveBindings(source))
                    if (binding.type == typeof(Animator))
                        AnimationUtility.SetEditorCurve(native, binding, AnimationUtility.GetEditorCurve(source, binding));
            native.name = name;
            var nativeSettings = AnimationUtility.GetAnimationClipSettings(source);
            nativeSettings.loopTime = loop;
            nativeSettings.loopBlendPositionY = true;
            nativeSettings.keepOriginalPositionY = true;
            // 完整HumanPose重新编码后，身体朝向补偿也在RootQ中。
            // 跑步同样必须烘入姿态，否则丢掉补偿只留下放大的骨盆反扭。
            nativeSettings.loopBlendOrientation = true;
            nativeSettings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(native, nativeSettings);
            // 胸腰与腿部完整保留Knight步态，不再传递杨戬骨盆和胸廓反扭。
            HandMotionRetargeting.Apply(native,HandMotionRetargeting.AvatarFor(source));
            Save(native, name,source);
            return;
        }
        int count = upper.bones.Length, frames = Mathf.CeilToInt(duration * 60);
        var humanCurves = new AnimationCurve[HumanTrait.MuscleCount + 7];
        for (int i = 0; i < humanCurves.Length; i++) humanCurves[i] = new AnimationCurve();
        var poseHandler = new HumanPoseHandler(upper.animator.avatar, upper.animator.transform);
        var pose = new HumanPose();
        Quaternion previousBody = Quaternion.identity;
        var hip = upper.bones.First(t => t.name == "Hip");
        upper.Sample(source, 0);
        var testHand = upper.bones.First(t => t.name == "R_Hand");
        Vector3 startHand = testHand.position;
        upper.Sample(source, source.length * .5f);
        Debug.Log("[JiubaMotionBaker] " + name + " human=" + source.humanMotion + " source=" + source.name + " handDelta=" + Vector3.Distance(startHand, testHand.position));
        var pelvis = upper.bones.First(t => t.name == "Pelvis");
        bool Lower(Transform t) => t == hip || t == pelvis || t.IsChildOf(pelvis) || !t.IsChildOf(hip);
        for (int f = 0; f <= frames; f++)
        {
            float time = duration * f / frames;
            // Non-looping attacks must sample their final pose, never wrap to frame zero.
            float sourceTime = freeze ? source.length * .5f : loop ? source.length * f / frames : Mathf.Min(time, source.length);
            upper.Sample(source, sourceTime);
            lower.Sample(basis, loop ? basis.length * f / frames : Mathf.Repeat(time, basis.length));
            for (int i = 0; i < count; i++)
            {
                bool useLower = split && Lower(upper.bones[i]);
                // Compose in local skeletal space: do not preserve the aerial source's world flip
                // after replacing its hips. Arms and spine retain their relationship to the torso.
                if (useLower)
                {
                    upper.bones[i].localPosition = lower.bones[i].localPosition;
                    upper.bones[i].localRotation = lower.bones[i].localRotation;
                }
            }
            poseHandler.GetHumanPose(ref pose);
            var body = pose.bodyRotation;
            if (f > 0 && Quaternion.Dot(previousBody, body) < 0) body = new Quaternion(-body.x, -body.y, -body.z, -body.w);
            previousBody = body;
            for (int i = 0; i < humanCurves.Length; i++)
            {
                int k = i - HumanTrait.MuscleCount;
                float value = k < 0 ? pose.muscles[i] : k < 3 ? pose.bodyPosition[k] : body[k-3];
                humanCurves[i].AddKey(new Keyframe(time, value));
            }
        }
        poseHandler.Dispose();
        var result = new AnimationClip { name = name, frameRate = 60 };
        for (int i = 0; i < humanCurves.Length; i++)
        {
            var c = humanCurves[i];
            if (c.keys.All(key => Mathf.Abs(key.value - c.keys[0].value) < .00001f))
                c = AnimationCurve.Linear(0, c.keys[0].value, duration, c.keys[0].value);
            for (int j = 0; j < c.length; j++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, j, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(c, j, AnimationUtility.TangentMode.Linear);
            }
            int k = i - HumanTrait.MuscleCount;
            string property = k < 0 ? HumanTrait.MuscleName[i] : k < 3 ? "RootT." + "xyz"[k] : "RootQ." + "xyzw"[k-3];
            AnimationUtility.SetEditorCurve(result, EditorCurveBinding.FloatCurve("", typeof(Animator), property), c);
        }
        result.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(result); settings.loopTime = loop;
        settings.loopBlendPositionY = true;
        settings.keepOriginalPositionY = true;
        if (name.EndsWith("Attack1_御风"))
        {
            // The two thrusts need their authored turn even with flight legs. The body
            // rotation reconstructed from the split rig instead follows the arm swing.
            // Keep flight position/leg muscles, but use the source thrust's body rotation.
            foreach (char axis in "xyzw")
            {
                var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), "RootQ." + axis);
                AnimationUtility.SetEditorCurve(result, binding, AnimationUtility.GetEditorCurve(source, binding));
            }
            settings.loopBlendOrientation = true;
            settings.keepOriginalOrientation = true;
            result.EnsureQuaternionContinuity();
        }
        AnimationUtility.SetAnimationClipSettings(result, settings);
        HandMotionRetargeting.Apply(result,HandMotionRetargeting.AvatarFor(source));
        Save(result, name,source);
    }

    static void Save(AnimationClip result, string name,AnimationClip source)
    {
        HandMotionRetargeting.Apply(result);
        JiubaHandPoseBaker.Apply(result);
        JiubaCarryPoseBaker.Apply(result,name.Contains("Attack")?source:null);
        if (!name.Contains("Attack"))
        {
            // 先按基础片段原导入语义采样，再烘入HumanPose的身体补偿；
            // 预先改根烘焙设置会改变待机/飞行胸腰本身的参考姿态。
            var settings = AnimationUtility.GetAnimationClipSettings(result);
            settings.loopBlendOrientation = settings.keepOriginalOrientation = true;
            settings.loopBlendPositionY = settings.keepOriginalPositionY = true;
            AnimationUtility.SetAnimationClipSettings(result, settings);
        }
        string path = Folder + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) { EditorUtility.CopySerialized(result, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(result); }
        else AssetDatabase.CreateAsset(result, path);
    }
}
