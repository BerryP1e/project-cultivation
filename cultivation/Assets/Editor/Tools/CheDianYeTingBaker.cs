using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Derive companion locomotion from the corrected JuLong source, never edit the source clips or scene.</summary>
public static class CheDianYeTingBaker
{
    public const string Output = "Assets/resources/Abilities/CheDianYeTing";
    const string BodyPath = "BossLeiLong (merge)/BossLeiLong/000/Bip001";
    const string Boundary = "BossLeiLong (merge)/BossLeiLong";
    const string MeshPath = "BossLeiLong (merge)/BossLeiLong_MengPi/BossLeiLong";
    struct Pose { public Vector3 p, s; public Quaternion q; }
    sealed class Motion
    {
        public string name; public float start, end; public bool loop; public Pose[][] frames;
        public float Length => end - start;
    }
    static Transform[] nodes;
    static string[] paths;
    static int body, head, boundary;
    static Transform root;
    static Pose[] ground, hover, forward;
    static Quaternion hoverHead, forwardHead;
    static AnimationClip skill, idle, walk, attack;

    [MenuItem("修仙/神通/制作掣电烨霆随行动作（不动场景）")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("请停止Play后烘焙");
        Folder(Output + "/Animations");
        skill = AssetDatabase.LoadAssetAtPath<AnimationClip>(JuLongCoordinateBaker.ClipFolder + "/JiNengGongJi1.anim");
        idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(JuLongCoordinateBaker.ClipFolder + "/ZhanLi.anim");
        walk = AssetDatabase.LoadAssetAtPath<AnimationClip>(JuLongCoordinateBaker.ClipFolder + "/XingZou.anim");
        attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(JuLongCoordinateBaker.ClipFolder + "/PuTongGongJi.anim");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(JuLongCoordinateBaker.PrefabPath);
        if (!skill || !idle || !walk || !attack || !source) throw new InvalidOperationException("缺少已修正的巨龙资源");
        var model = UnityEngine.Object.Instantiate(source);
        model.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            root = model.transform;
            paths = AnimationUtility.GetCurveBindings(skill).Select(b => b.path).Distinct().OrderBy(p => p.Count(c => c == '/')).ThenBy(p => p).ToArray();
            nodes = paths.Select(p => root.Find(p)).ToArray();
            if (nodes.Any(t => !t)) throw new InvalidOperationException("源动画与骨架不匹配");
            body = Array.IndexOf(paths, BodyPath); head = Array.FindIndex(nodes, n => n.name == "Bip001 Head"); boundary = Array.IndexOf(paths, Boundary);
            idle.SampleAnimation(model, 0); ground = Read();
            skill.SampleAnimation(model, 4.08f); hover = Read(); hoverHead = nodes[head].rotation;
            skill.SampleAnimation(model, 6.0f); forward = Read(); forwardHead = nodes[head].rotation;
            var groundIdle = Bake("GroundIdle", idle, 0, idle.length, true, 0);
            var groundMove = Bake("GroundMove", walk, 0, walk.length, true, 0);
            // Full authored down/up stroke, chosen by repeated wing phase inside the user's reference window.
            var flap = Bake("FlapLoop", skill, 85f / 30, 112f / 30, true, 1);
            var airIdle = Bake("AirIdle", skill, 85f / 30, 112f / 30, true, 2);
            var airMove = Bake("AirMove", skill, 157f / 30, 181f / 30, true, 3);
            var takeoff = Bake("Takeoff", skill, 1.6f, 4.6f, false, 4);
            var landing = Bake("Landing", skill, 11.74f, skill.length, false, 4);
            BlendStart(takeoff, groundIdle, .4f);
            BlendEnd(takeoff, airIdle, .7f, true);
            BlendEnd(landing, groundIdle, .75f, false);
            var powered = FlightPattern("AirPowered", airMove, airIdle, false);
            var glide = FlightPattern("AirGlide", airMove, airIdle, true);
            var motions = new List<Motion> { groundIdle, groundMove, takeoff, flap, airIdle, airMove, landing, powered, glide };
            foreach (bool air in new[] { false, true })
            {
                string prefix = air ? "AirBreath" : "GroundBreath";
                motions.Add(BreathMotion(prefix + "Start", .87f, 2.39f, 0, air, airIdle));
                motions.Add(BreathMotion(prefix + "Loop", 2.39f, 3.39f, 1, air, airIdle));
                motions.Add(BreathMotion(prefix + "End", 2.39f, 5f, 2, air, airIdle));
                motions.Add(BreathMotion(prefix + "Repeat", 3.4f, 2.39f, 3, air, airIdle));
            }
            var clips = motions.ToDictionary(m => m.name, Save);
            var controller = Controller(clips);
            var npc = model.GetComponent<NpcAnimator>();
            npc.动作列表 = motions.Select(m => m.name).ToArray(); npc.待机动作 = "GroundIdle"; npc.移动动作 = "GroundMove";
            model.GetComponent<Animator>().runtimeAnimatorController = controller;
            Write(groundIdle.frames[0]);
            // Preserve the actual display node edited by the user, as well as root size/heading.
            var display = root.Find(MeshPath);
            display.localPosition = new Vector3(.62238f, -.59212f, 1.0902f);
            display.localRotation = Quaternion.identity;
            display.localScale = Vector3.one * 2.819921f;
            var skin = display.GetComponent<SkinnedMeshRenderer>();
            string meshPath = Output + "/CompanionMesh.asset";
            var mesh = UnityEngine.Object.Instantiate(skin.sharedMesh); mesh.name = "CompanionMesh";
            var meshData = new SerializedObject(mesh); meshData.FindProperty("m_IsReadable").boolValue = true; meshData.ApplyModifiedPropertiesWithoutUndo();
            MarkGhostBodyMesh(mesh,skin.bones);
            var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (savedMesh) { EditorUtility.CopySerialized(mesh, savedMesh); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(savedMesh); }
            else { savedMesh = mesh; AssetDatabase.CreateAsset(savedMesh, meshPath); }
            skin.sharedMesh = savedMesh;
            root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity; root.localScale = Vector3.one * 2.5f;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            model.name = "CheDianYeTing_Companion"; model.hideFlags = HideFlags.None;
            var companion = PrefabUtility.SaveAsPrefabAsset(model, Output + "/Companion.prefab");
            var definition = AssetDatabase.LoadAssetAtPath<CheDianYeTingDefinition>(Output + "/Definition.asset");
            if (!definition) { definition = ScriptableObject.CreateInstance<CheDianYeTingDefinition>(); AssetDatabase.CreateAsset(definition, Output + "/Definition.asset"); }
            definition.Companion = companion;
            definition.GroundIdle = clips["GroundIdle"]; definition.GroundMove = clips["GroundMove"]; definition.Takeoff = clips["Takeoff"];
            definition.FlapLoop = clips["FlapLoop"]; definition.AirIdle = clips["AirIdle"]; definition.AirMove = clips["AirMove"]; definition.Landing = clips["Landing"];
            definition.Scale = Vector3.one * 2.5f;
            definition.TransitionSeconds = 1.6f; definition.MoveTransitionSeconds = 1.35f;
            definition.GroundRunSpeed = 2.7f; definition.GroundCatchupSpeed = 3.8f;
            definition.GroundReferenceSpeed = 1.35f; definition.Acceleration = 2.8f; definition.Braking = 4.2f;
            definition.BreathDissolveSeconds = 1.3f;
            definition.BreathDuration = 1.725f; definition.FirstWindupSeconds = .58f; definition.RepeatWindupSeconds = .24f; definition.RecoverySeconds = .7f; definition.ReturnToGhostDelay = 0; definition.ReturnToGhostSeconds = 1.6f; definition.ManifestSeconds = .6f;
            definition.ComboWindowSeconds = 1.3f; definition.ComboTimeScale = .18f;
            BindAdditional(definition, clips);
            string materialPath = Output + "/BreathGlow.mat";
            var glow = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!glow) { glow = new Material(Shader.Find("Legacy Shaders/Particles/Additive")); glow.name = "CheDianBreathGlow"; AssetDatabase.CreateAsset(glow, materialPath); }
            glow.shader = Shader.Find("Cultivation/CheDianBeam");
            if (glow.HasProperty("_TintColor")) glow.SetColor("_TintColor", new Color(.8f,1.1f,1.6f,.8f));
            definition.BreathMaterial = glow; EditorUtility.SetDirty(glow);
            var plume = AssetDatabase.LoadAssetAtPath<Material>(Output + "/BreathPlume.mat");
            if (!plume) { plume = new Material(Shader.Find("Cultivation/CheDianPlume")); AssetDatabase.CreateAsset(plume, Output + "/BreathPlume.mat"); }
            plume.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Shared/Textures/Projectiles/Smoke/FX_Smoke_3.png");
            plume.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Shared/Textures/Projectiles/Noise/FX_Noise_1.png"));
            plume.SetFloat("_Glow", 2.1f); definition.BreathPlumeMaterial = plume; EditorUtility.SetDirty(plume);
            var distortion = AssetDatabase.LoadAssetAtPath<Material>(Output + "/BreathDistortion.mat");
            if (!distortion) { distortion = new Material(Shader.Find("Cultivation/CheDianDistortion")); AssetDatabase.CreateAsset(distortion, Output + "/BreathDistortion.mat"); }
            distortion.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Shared/Textures/Projectiles/Noise/FX_Noise_1.png"));
            definition.BreathDistortionMaterial = distortion; EditorUtility.SetDirty(distortion);
            definition.BreathImpact = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/resources/CombatVFX/CombatMagic/lightning-fx/lightning-explode.prefab");
            EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
            DataTableImporter.ImportTables(false, new[] { "主动神通表", "物品表" });
            Icon(companion, definition.AirIdle);
            QuestDatabaseBuilder.收集(false); 面板库收集器.收集(); AssetDatabase.SaveAssets();
            CompactClips();
            Debug.Log("[掣电烨霆] 17段动作：随行、扑翼/滑翔、地面/空中四段吐息；2.5倍伴龙预制体与四次充能入口完成");
        }
        finally { UnityEngine.Object.DestroyImmediate(model); nodes = null; root = null; }
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    public static void CompactClips()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Output + "/Animations" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var copy = UnityEngine.Object.Instantiate(clip); copy.name = clip.name;
            try { UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { copy }, path, false); }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            clips[Path.GetFileNameWithoutExtension(path)] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First();
        }
        // Binary serialization assigns local fileID 1; keep GUIDs and rebind the original controller/definition.
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Output + "/Companion.controller");
        foreach (var state in controller.layers[0].stateMachine.states)
            if (clips.TryGetValue(state.state.name, out var clip)) { state.state.motion = clip; EditorUtility.SetDirty(state.state); }
        EditorUtility.SetDirty(controller);
        var definition = AssetDatabase.LoadAssetAtPath<CheDianYeTingDefinition>(Output + "/Definition.asset");
        definition.GroundIdle = clips["GroundIdle"]; definition.GroundMove = clips["GroundMove"]; definition.Takeoff = clips["Takeoff"];
        definition.FlapLoop = clips["FlapLoop"]; definition.AirIdle = clips["AirIdle"]; definition.AirMove = clips["AirMove"]; definition.Landing = clips["Landing"];
        BindAdditional(definition, clips);
        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssets();
    }
    static void BindAdditional(CheDianYeTingDefinition d, Dictionary<string, AnimationClip> clips)
    {
        d.AirPowered = clips["AirPowered"]; d.AirGlide = clips["AirGlide"];
        d.GroundBreathStart = clips["GroundBreathStart"]; d.GroundBreathLoop = clips["GroundBreathLoop"];
        d.GroundBreathEnd = clips["GroundBreathEnd"]; d.GroundBreathRepeat = clips["GroundBreathRepeat"];
        d.AirBreathStart = clips["AirBreathStart"]; d.AirBreathLoop = clips["AirBreathLoop"];
        d.AirBreathEnd = clips["AirBreathEnd"]; d.AirBreathRepeat = clips["AirBreathRepeat"];
    }
    static Pose[] Read() => nodes.Select(n => new Pose { p = n.localPosition, q = n.localRotation, s = n.localScale }).ToArray();
    static void Write(Pose[] pose) { for (int i = 0; i < nodes.Length; i++) { nodes[i].localPosition = pose[i].p; nodes[i].localRotation = pose[i].q; nodes[i].localScale = pose[i].s; } }
    static bool Wing(string name) => name.Contains("Clavicle") || name.Contains("UpperArm") || name.Contains("Forearm") || name.EndsWith(" Hand") || name.Contains("Finger");
    static Pose Mix(Pose a, Pose b, float t) => new Pose { p = Vector3.Lerp(a.p, b.p, t), q = Quaternion.Slerp(a.q, b.q, t), s = Vector3.Lerp(a.s, b.s, t) };

    static Motion FlightPattern(string name, Motion level, Motion flap, bool glide)
    {
        float length = glide ? 2.4f : .9f;
        var result = new Motion { name = name, start = 0, end = length, loop = true, frames = new Pose[Mathf.RoundToInt(length * 60) + 1][] };
        int left = Array.FindIndex(nodes, n => n.name == "Bip001 L Hand"), right = Array.FindIndex(nodes, n => n.name == "Bip001 R Hand");
        var spread = level.frames[0]; float best = 0;
        foreach (var frame in level.frames) { Write(frame); float span = Vector3.Distance(nodes[left].position, nodes[right].position); if (span > best) { best = span; spread = frame; } }
        for (int sample = 0; sample < result.frames.Length; sample++)
        {
            float t = sample / 60f; var pose = At(level, t); var beat = At(flap, t);
            for (int bone = 0; bone < nodes.Length; bone++) if (Wing(nodes[bone].name))
            {
                pose[bone] = glide ? spread[bone] : beat[bone];
                if (glide && nodes[bone].name.Contains("UpperArm")) pose[bone].q *= Quaternion.AngleAxis(Mathf.Sin(t/length*Mathf.PI*2)*1.5f, Vector3.forward);
            }
            result.frames[sample] = pose;
        }
        CloseLoop(result); return result;
    }
    static Motion BreathMotion(string name, float first, float last, int phase, bool air, Motion airIdle)
    {
        float length = phase == 3 ? 1.35f : Mathf.Abs(last - first);
        var result = new Motion { name = name, start = 0, end = length, loop = phase == 1, frames = new Pose[Mathf.CeilToInt(length*60)+1][] };
        for (int sample = 0; sample < result.frames.Length; sample++)
        {
            float u = sample / (float)(result.frames.Length-1), t = u*length;
            attack.SampleAnimation(root.gameObject, phase == 1 ? 2.39f : Mathf.Lerp(first,last,u));
            var raw = Read();
            // Keep root travel/heading under the AI. Weight retreats slightly, never lunges at the target.
            raw[body].p = ground[body].p;
            Write(raw);
            var chest = nodes.First(n => n.name == "Bip001 Spine2"); var heading = chest.position-nodes[body].position; heading.y = 0;
            if (heading.sqrMagnitude>.000001f) raw[boundary].q = Quaternion.Euler(0,-Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg,0)*raw[boundary].q;
            Write(raw); var desiredHead = nodes[head].rotation;
            var pose = air ? At(airIdle,t) : (Pose[])raw.Clone();
            if (air)
            {
                for (int bone = 0; bone < nodes.Length; bone++)
                {
                    var n = nodes[bone].name;
                    if (Wing(n) || bone == boundary || bone == body) continue;
                    float weight = n.Contains("Spine") || n.Contains("Neck") ? .76f : n.Contains("Head") ? .9f : .15f;
                    pose[bone] = Mix(pose[bone],raw[bone],weight);
                }
                pose[body].q *= Quaternion.Slerp(Quaternion.identity,Quaternion.Inverse(ground[body].q)*raw[body].q,.22f);
            }
            float force = phase == 0 ? Mathf.SmoothStep(0,1,u) : phase == 2 ? 1-Mathf.SmoothStep(0,1,u) : phase == 3 ? Mathf.SmoothStep(.35f,1,u) : 1;
            Write(pose);
            var shift = nodes[body].parent.InverseTransformVector(new Vector3(0,-.016f*force,-.022f*force));
            pose[body].p = ground[body].p+shift;
            float anticipation = phase == 3 ? Mathf.Pow(Mathf.Max(0,Mathf.Sin(u*Mathf.PI)),.7f) : 0;
            if (anticipation > 0)
            {
                pose[body].p += nodes[body].parent.InverseTransformVector(new Vector3(0,-.025f,-.045f)*anticipation);
                int neck = Array.FindIndex(nodes,n=>n.name=="Bip001 Neck");
                pose[neck].p += nodes[neck].parent.InverseTransformVector(Vector3.back*.10f*anticipation);
                int upperChest = Array.FindIndex(nodes,n=>n.name=="Bip001 Spine2");
                pose[upperChest].q = Quaternion.Inverse(nodes[upperChest].parent.rotation)*Quaternion.AngleAxis(-12*anticipation,Vector3.right)*nodes[upperChest].rotation;
                desiredHead = Quaternion.AngleAxis(-8*anticipation,Vector3.right)*desiredHead;
            }
            // Expand the upper chest without grotesquely scaling the whole creature.
            float tremor = phase == 1 ? Mathf.Sin(t*Mathf.PI*2*7)*.0025f : 0;
            for (int bone = 0; bone < nodes.Length; bone++) if (nodes[bone].name.Contains("Spine"))
            {
                pose[bone].s = Vector3.Scale(pose[bone].s,new Vector3(1+.018f*force+.025f*anticipation+tremor,1+.006f*force,1+.018f*force+.025f*anticipation+tremor));
                if (phase == 1) pose[bone].q *= Quaternion.AngleAxis(Mathf.Sin(t*Mathf.PI*2*7)*.22f,Vector3.forward);
            }
            Write(pose);
            // Counter the chest/hover parent movement at the head. Leave a tiny living tremor, not a sweeping hose.
            pose[head].q = Quaternion.Inverse(nodes[head].parent.rotation)*desiredHead;
            if (phase == 1) pose[head].q *= Quaternion.AngleAxis(Mathf.Sin(t*Mathf.PI*2*7)*.28f,Vector3.forward);
            result.frames[sample] = pose;
        }
        if (result.loop) CloseLoop(result);
        return result;
    }

    // mode 0 ground, 1 generic flap, 2 climbing hover, 3 level flight, 4 one-shot lift/land.
    static Motion Bake(string name, AnimationClip source, float start, float end, bool loop, int mode)
    {
        var motion = new Motion { name = name, start = start, end = end, loop = loop };
        int count = Mathf.CeilToInt(motion.Length * 60); motion.frames = new Pose[count + 1][];
        for (int sample = 0; sample <= count; sample++)
        {
            float u = sample / (float)count; float t = Mathf.Lerp(start, end, u);
            source.SampleAnimation(root.gameObject, t);
            var wingPose = Read();
            if (mode == 1 || mode == 2) skill.SampleAnimation(root.gameObject, Mathf.Lerp(3.06f, 4.8f, u));
            var raw = Read(); var rawHead = nodes[head].rotation;
            var pose = (Pose[])raw.Clone();
            if (mode >= 1 && mode <= 3)
            {
                var reference = mode == 3 ? forward : hover;
                float bodyWeight = mode == 1 ? .65f : mode == 2 ? .18f : .35f;
                for (int i = 0; i < nodes.Length; i++)
                {
                    string boneName = nodes[i].name;
                    if (Wing(boneName)) { pose[i] = wingPose[i]; continue; }
                    float weight = boneName.Contains("Spine") || boneName.Contains("Neck") ? .3f : .65f;
                    if (i == body || i == boundary) weight = bodyWeight;
                    pose[i] = Mix(reference[i], raw[i], weight);
                }
                pose[body].p = ground[body].p + (raw[body].p - reference[body].p) * .035f;
                Write(pose);
                var targetHead = mode == 3 ? forwardHead : hoverHead;
                var desired = targetHead * Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(targetHead) * rawHead, mode == 3 ? .18f : .1f);
                pose[head].q = Quaternion.Inverse(nodes[head].parent.rotation) * desired;
            }
            else
            {
                // Remove source trajectory while keeping the small authored crouch/bob. Player follow owns travel.
                float crouch = Mathf.Clamp(raw[body].p.x - ground[body].p.x, -.03f, .10f);
                pose[body].p = ground[body].p + new Vector3(crouch, 0, 0);
                if (mode == 4)
                {
                    Write(pose);
                    var chest = nodes.First(n => n.name == "Bip001 Spine2");
                    var direction = chest.position - nodes[body].position; direction.y = 0;
                    if (direction.sqrMagnitude > .000001f)
                    {
                        float heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                        pose[boundary].q = Quaternion.Euler(0, -heading, 0) * pose[boundary].q;
                    }
                }
            }
            motion.frames[sample] = pose;
        }
        if (loop) CloseLoop(motion);
        return motion;
    }
    static void CloseLoop(Motion motion)
    {
        int last = motion.frames.Length - 1;
        for (int bone = 0; bone < nodes.Length; bone++)
        {
            var first = motion.frames[0][bone]; var end = motion.frames[last][bone];
            var correction = first.q * Quaternion.Inverse(end.q);
            for (int frame = 1; frame <= last; frame++)
            {
                var pose = motion.frames[frame][bone]; float u = frame / (float)last;
                pose.p += (first.p - end.p) * u;
                pose.s += (first.s - end.s) * u;
                pose.q = Quaternion.Slerp(Quaternion.identity, correction, Mathf.SmoothStep(0, 1, u)) * pose.q;
                motion.frames[frame][bone] = pose;
            }
            motion.frames[last][bone] = first;
        }
    }
    static Pose[] At(Motion motion, float time)
    {
        if (motion.loop) time = Mathf.Repeat(time, motion.Length); else time = Mathf.Clamp(time, 0, motion.Length);
        float cursor = time / motion.Length * (motion.frames.Length - 1);
        int first = Mathf.FloorToInt(cursor), second = Mathf.Min(first + 1, motion.frames.Length - 1);
        return motion.frames[first].Select((p, i) => Mix(p, motion.frames[second][i], cursor - first)).ToArray();
    }
    static void BlendStart(Motion motion, Motion previous, float seconds)
    {
        for (int frame = 0; frame < motion.frames.Length; frame++)
        {
            float t = frame / (float)(motion.frames.Length - 1) * motion.Length; if (t > seconds) break;
            var origin = At(previous, t); float weight = Mathf.SmoothStep(0, 1, t / seconds);
            for (int bone = 0; bone < nodes.Length; bone++) motion.frames[frame][bone] = Mix(origin[bone], motion.frames[frame][bone], weight);
        }
    }
    static void BlendEnd(Motion motion, Motion next, float seconds, bool matchWings)
    {
        float phase = 0;
        if (matchWings)
        {
            float best = float.MaxValue; var last = motion.frames.Last();
            for (int frame = 0; frame < next.frames.Length - 1; frame++)
            {
                float error = 0; for (int bone = 0; bone < nodes.Length; bone++) if (Wing(nodes[bone].name)) error += Quaternion.Angle(last[bone].q, next.frames[frame][bone].q);
                if (error < best) { best = error; phase = frame / (float)(next.frames.Length - 1) * next.Length; }
            }
        }
        for (int frame = 0; frame < motion.frames.Length; frame++)
        {
            float t = frame / (float)(motion.frames.Length - 1) * motion.Length;
            if (t < motion.Length - seconds) continue;
            var target = At(next, phase + t - motion.Length);
            float weight = Mathf.SmoothStep(0, 1, (t - motion.Length + seconds) / seconds);
            for (int bone = 0; bone < nodes.Length; bone++) motion.frames[frame][bone] = Mix(motion.frames[frame][bone], target[bone], weight);
        }
    }

    static AnimationClip Save(Motion motion)
    {
        var clip = new AnimationClip { name = motion.name, frameRate = 60 };
        for (int bone = 0; bone < nodes.Length; bone++)
        {
            for (int frame = 1; frame < motion.frames.Length; frame++)
            {
                var pose = motion.frames[frame][bone];
                if (Quaternion.Dot(motion.frames[frame - 1][bone].q, pose.q) < 0) pose.q = new Quaternion(-pose.q.x, -pose.q.y, -pose.q.z, -pose.q.w);
                motion.frames[frame][bone] = pose;
            }
            for (int channel = 0; channel < 10; channel++)
            {
                int axis = channel < 3 ? channel : channel < 7 ? channel - 3 : channel - 7;
                string property = channel < 3 ? "m_LocalPosition." : channel < 7 ? "m_LocalRotation." : "m_LocalScale.";
                var values = motion.frames.Select(p => channel < 3 ? p[bone].p[axis] : channel < 7 ? p[bone].q[axis] : p[bone].s[axis]).ToArray();
                if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw new InvalidOperationException("Non-finite animation channel: " + motion.name + " " + paths[bone]);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(paths[bone], typeof(Transform), property + "xyzw"[axis]), Curve(values, motion.Length, motion.loop));
            }
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = motion.loop; settings.loopBlend = false;
        settings.startTime = 0; settings.stopTime = motion.Length; settings.keepOriginalOrientation = settings.keepOriginalPositionY = settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        string path = Output + "/Animations/" + motion.name + ".anim";
        var old = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (old) { EditorUtility.CopySerialized(clip, old); UnityEngine.Object.DestroyImmediate(clip); EditorUtility.SetDirty(old); return old; }
        AssetDatabase.CreateAsset(clip, path); return clip;
    }
    static AnimationCurve Curve(float[] values, float length, bool loop)
    {
        if (values.Max() - values.Min() < 1e-6f) return AnimationCurve.Constant(0, length, values[0]);
        int last = values.Length - 1; float dt = length / last;
        var keys = new Keyframe[values.Length];
        float seam = loop ? (values[1] - values[last - 1]) / (2 * dt) : 0;
        for (int i = 0; i <= last; i++)
        {
            float tangent = i == 0 ? loop ? seam : (values[1] - values[0]) / dt
                : i == last ? loop ? seam : (values[last] - values[last - 1]) / dt : (values[i + 1] - values[i - 1]) / (2 * dt);
            keys[i] = new Keyframe(i * dt, values[i], tangent, tangent);
        }
        return new AnimationCurve(keys);
    }
    static AnimatorController Controller(Dictionary<string, AnimationClip> clips)
    {
        string path = Output + "/Companion.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        if (!controller.parameters.Any(p => p.name == "Action")) controller.AddParameter("Action", AnimatorControllerParameterType.Int);
        var machine = controller.layers[0].stateMachine; int index = 0;
        foreach (var item in clips)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == item.Key) ?? machine.AddState(item.Key);
            state.motion = item.Value;
            if (item.Key == "GroundIdle") machine.defaultState = state;
            if (!machine.anyStateTransitions.Any(t => t.destinationState == state))
            {
                var transition = machine.AddAnyStateTransition(state); transition.hasExitTime = false; transition.duration = .8f; transition.hasFixedDuration = true; transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.Equals, index, "Action");
            }
            EditorUtility.SetDirty(state); index++;
        }
        EditorUtility.SetDirty(controller); return controller;
    }

    public static void MarkGhostBodyMesh(Mesh mesh,Transform[] bones)
    {
        var weights=mesh.boneWeights;var colors=new Color32[mesh.vertexCount];
        float Stage(int index)=>index>=0&&index<bones.Length?CheDianManifestation.BoneStage(bones[index]):.48f;
        for(int i=0;i<colors.Length;i++)
        {
            var w=weights[i];float stage=Stage(w.boneIndex0)*w.weight0+Stage(w.boneIndex1)*w.weight1+Stage(w.boneIndex2)*w.weight2+Stage(w.boneIndex3)*w.weight3;
            colors[i]=new Color32((byte)Mathf.RoundToInt(stage*255),255,255,255);
        }
        mesh.colors32=colors;
    }
    static void Icon(GameObject companion, AnimationClip pose)
    {
        const string path = "Assets/resources/UI/InkUI/AbilityArt/icon-ability-chedian_yeting.png";
        using (var preview = new NpcPrefabAnimationPreview(companion))
        {
            preview.SelectClip(pose); preview.SetTime(pose.length * .18f);
            var image = preview.Capture(512, 512, true);
            try { File.WriteAllBytes(path, image.EncodeToPNG()); } finally { UnityEngine.Object.DestroyImmediate(image); }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        var ability = AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>("Assets/Data/Generated/ActiveDivineAbility/" + CheDianYeTingDefinition.AbilityId + ".asset");
        var book = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_" + CheDianYeTingDefinition.AbilityId + ".asset");
        ability.图标 = book.图标 = sprite; EditorUtility.SetDirty(ability); EditorUtility.SetDirty(book);
    }

    [MenuItem("修仙/调试/给予掣电烨霆玉简（仅当前Play）")]
    static void GiveBook()
    {
        if (!Application.isPlaying) { Debug.Log("请在Play中给予玉简"); return; }
        var panel = UnityEngine.Object.FindFirstObjectByType<UIPanelData>();
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Data/Generated/ItemDefinition/item_ability_chedian_yeting.asset");
        if (panel && item) panel.给物品(item, 1);
    }
}
