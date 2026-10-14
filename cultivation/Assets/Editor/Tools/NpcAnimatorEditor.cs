using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>Preview the clips already assigned to a prefab's Animator, without animating the asset or scene.</summary>
[CustomEditor(typeof(NpcAnimator))]
public sealed class NpcAnimatorEditor : Editor
{
    NpcPrefabAnimationPreview preview;
    void OnEnable() => EditorApplication.update += Tick;
    void OnDisable() { EditorApplication.update -= Tick; preview?.Dispose(); preview = null; }
    void Tick() { if (preview != null && preview.Advance()) Repaint(); }
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!target) return;
        var npc = (NpcAnimator)target;
        var animator = npc.GetComponent<Animator>();
        if (!animator || !animator.runtimeAnimatorController) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("预制体动画预览", EditorStyles.boldLabel);
        if (preview == null) preview = new NpcPrefabAnimationPreview(npc.gameObject);
        if (!preview.HasClips) return;
        preview.DrawControls();
        var rect = GUILayoutUtility.GetRect(120, Mathf.Clamp(EditorGUIUtility.currentViewWidth * .7f, 200, 360), GUILayout.ExpandWidth(true));
        preview.Draw(rect);
        if (GUILayout.Button("打开大预览窗口")) NpcAnimationPreviewWindow.Open(npc.gameObject, preview.Clip);
    }
}

public sealed class NpcAnimationPreviewWindow : EditorWindow
{
    NpcPrefabAnimationPreview preview;
    [SerializeField] GameObject source;

    public static NpcAnimationPreviewWindow Open(GameObject root, AnimationClip clip = null)
    {
        var window = GetWindow<NpcAnimationPreviewWindow>("预制体动画");
        window.minSize = new Vector2(460, 420);
        window.preview?.Dispose();
        window.source = root;
        window.preview = new NpcPrefabAnimationPreview(root);
        if (clip) window.preview.SelectClip(clip);
        window.Show();
        return window;
    }

    [MenuItem("Assets/预览预制体动画", false, 25)]
    [MenuItem("修仙/动画/预览选中NPC预制体")]
    static void OpenSelected()
    {
        var selected = Selection.activeGameObject;
        var npc = selected ? selected.GetComponentInParent<NpcAnimator>() : null;
        if (!npc && selected) npc = selected.GetComponentInChildren<NpcAnimator>(true);
        Open(npc ? npc.gameObject : selected);
    }
    [MenuItem("Assets/预览预制体动画", true)]
    static bool CanOpenSelected() => Selection.activeGameObject && Selection.activeGameObject.GetComponentInChildren<Animator>(true);

    void OnEnable()
    {
        EditorApplication.update += Tick;
        if (source) preview = new NpcPrefabAnimationPreview(source);
    }
    void OnDisable() { EditorApplication.update -= Tick; preview?.Dispose(); preview = null; }
    void Tick() { if (preview != null && preview.Advance()) Repaint(); }
    void OnGUI()
    {
        if (preview == null || !source) { EditorGUILayout.HelpBox("在Project中选择预制体，右键选择「预览预制体动画」。", MessageType.Info); return; }
        EditorGUILayout.LabelField(source.name, EditorStyles.boldLabel);
        if (!preview.HasClips) { EditorGUILayout.HelpBox("Animator尚未绑定动画控制器。", MessageType.Info); return; }
        preview.DrawControls();
        var rect = GUILayoutUtility.GetRect(120, 120, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        preview.Draw(rect);
    }
}

/// <summary>All objects live in PreviewRenderUtility's private scene; no source object is sampled.</summary>
public sealed class NpcPrefabAnimationPreview : IDisposable
{
    readonly PreviewRenderUtility renderer;
    readonly AnimationClip[] clips;
    readonly string[] labels;
    PlayableGraph graph;
    AnimationClipPlayable motion;
    readonly AnimationPlayableOutput output;
    Bounds bounds;
    Bounds poseBounds;
    float poseRadius;
    readonly Mesh bakedMesh = new Mesh();
    sealed class SkinData
    {
        public SkinnedMeshRenderer renderer;
        public Vector3[] vertices;
        public BoneWeight[] weights;
        public Matrix4x4[] bindposes;
    }
    SkinData[] skins = Array.Empty<SkinData>();
    int index;
    double lastUpdate;
    float yaw = -25, pitch = 18, zoom = 1;
    public GameObject PreviewObject { get; }
    public bool HasClips => clips.Length > 0;
    public AnimationClip Clip => HasClips ? clips[index] : null;
    public int ClipCount => clips.Length;
    public float Time { get; private set; }
    public bool Playing { get; set; } = true;
    public bool Loop { get; set; } = true;
    public bool Follow { get; set; } = true;
    public float Speed { get; set; } = 1;

    public NpcPrefabAnimationPreview(GameObject source)
    {
        lastUpdate = EditorApplication.timeSinceStartup;
        renderer = new PreviewRenderUtility();
        renderer.camera.fieldOfView = 35;
        renderer.camera.clearFlags = CameraClearFlags.SolidColor;
        renderer.camera.backgroundColor = new Color(.16f, .17f, .16f);
        renderer.ambientColor = new Color(.5f, .5f, .5f);
        renderer.lights[0].intensity = 1.2f;
        renderer.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
        renderer.lights[1].intensity = .8f;
        renderer.lights[1].transform.rotation = Quaternion.Euler(340, 145, 0);
        clips = Array.Empty<AnimationClip>(); labels = Array.Empty<string>();
        if (!source) return;
        try
        {
            PreviewObject = renderer.InstantiatePrefabInScene(source);
            PreviewObject.hideFlags = HideFlags.HideAndDontSave;
            PreviewObject.transform.position = Vector3.zero;
            // Remove gameplay/ExecuteAlways behaviour only from the private clone.
            foreach (var behaviour in PreviewObject.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var audio in PreviewObject.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(audio);
            foreach (var collider in PreviewObject.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in PreviewObject.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var particles in PreviewObject.GetComponentsInChildren<ParticleSystem>(true)) particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var skin in PreviewObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            skins = PreviewObject.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => new SkinData {
                renderer = s,
                vertices = s.sharedMesh && (s.sharedMesh.isReadable || !Application.isPlaying) ? s.sharedMesh.vertices : null,
                weights = s.sharedMesh && (s.sharedMesh.isReadable || !Application.isPlaying) ? s.sharedMesh.boneWeights : null,
                bindposes = s.sharedMesh ? s.sharedMesh.bindposes : null
            }).ToArray();
            var animator = PreviewObject.GetComponentInChildren<Animator>(true);
            if (!animator || !animator.runtimeAnimatorController) return;
            var order = source.GetComponent<NpcAnimator>()?.动作列表 ?? Array.Empty<string>();
            clips = animator.runtimeAnimatorController.animationClips.Where(c => c).Distinct()
                .OrderBy(c => { int i = Array.IndexOf(order, c.name); return i < 0 ? int.MaxValue : i; }).ThenBy(c => c.name).ToArray();
            labels = clips.Select(c => Label(c.name)).ToArray();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            graph = PlayableGraph.Create("NpcPrefabPreview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            graph.Play();
            if (HasClips) SelectClip(clips[0]);
        }
        catch { Dispose(); throw; }
    }

    static string Label(string name)
    {
        switch (name)
        {
            case "GroundIdle": return "地面待机 · " + name;
            case "GroundMove": return "地面行走 · " + name;
            case "Takeoff": return "起飞 · " + name;
            case "FlapLoop": return "扇翼基础循环 · " + name;
            case "AirIdle": return "空中悬停 · " + name;
            case "AirMove": return "空中平飞 · " + name;
            case "AirPowered": return "平飞扑翼 · " + name;
            case "AirGlide": return "展翼滑翔 · " + name;
            case "GroundBreathStart": return "地面吐息前摇 · " + name;
            case "GroundBreathLoop": return "地面持续吐息 · " + name;
            case "GroundBreathEnd": return "地面吐息后摇 · " + name;
            case "GroundBreathRepeat": return "地面再次蓄势（倒播） · " + name;
            case "AirBreathStart": return "空中吐息前摇 · " + name;
            case "AirBreathLoop": return "空中持续吐息 · " + name;
            case "AirBreathEnd": return "空中吐息后摇 · " + name;
            case "AirBreathRepeat": return "空中再次蓄势（倒播） · " + name;
            case "Landing": return "落地 · " + name;
            case "ZhanLi": return "待机 · " + name;
            case "XingZou": return "移动 · " + name;
            case "PuTongGongJi": return "普通攻击 · " + name;
            case "JiNengGongJi1": return "技能一 · " + name;
            case "JiNengGongJi2": return "技能二 · " + name;
            case "SiWang": return "死亡 · " + name;
            default: return name;
        }
    }

    public void SelectClip(AnimationClip clip)
    {
        int next = Array.IndexOf(clips, clip);
        if (next < 0) return;
        if (motion.IsValid()) { output.SetSourcePlayable(Playable.Null); graph.DestroyPlayable(motion); }
        index = next;
        motion = AnimationClipPlayable.Create(graph, clip);
        motion.SetApplyFootIK(false);
        motion.SetApplyPlayableIK(false);
        output.SetSourcePlayable(motion);
        Fit();
        SetTime(0);
        lastUpdate = EditorApplication.timeSinceStartup;
    }

    public void SetTime(float time)
    {
        if (!HasClips || !motion.IsValid()) return;
        Time = Mathf.Clamp(time, 0, Clip.length);
        motion.SetTime(Time);
        graph.Evaluate(0);
        poseBounds = ReadPoseBounds();
        lastUpdate = EditorApplication.timeSinceStartup;
    }

    public bool Advance()
    {
        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastUpdate);
        if (!Playing || !HasClips) { lastUpdate = now; return false; }
        if (delta < 1f / 30) return false;
        float next = Time + Mathf.Min(delta, .1f) * Speed;
        if (next >= Clip.length)
        {
            if (Loop && Clip.length > 0) next %= Clip.length;
            else { next = Clip.length; Playing = false; }
        }
        SetTime(next);
        return true;
    }

    void Fit()
    {
        bool first = true;
        poseRadius = .1f;
        // Keep a stable model size while following movement; optionally show the complete trajectory.
        for (int sample = 0; sample <= 8; sample++)
        {
            SetTime(Clip.length * sample / 8);
            if (first) { bounds = poseBounds; first = false; } else bounds.Encapsulate(poseBounds);
            poseRadius = Mathf.Max(poseRadius, poseBounds.extents.magnitude);
        }
        zoom = 1;
    }

    Bounds ReadPoseBounds()
    {
        bool first = true;
        var result = new Bounds(Vector3.zero, Vector3.one);
        foreach (var data in skins)
        {
            var skin = data.renderer;
            // Tuanjie BakeMesh repeats transform scale for a scaled skinned renderer. Use real skin matrices.
            bool manual = data.vertices != null && data.weights != null && data.weights.Length == data.vertices.Length && data.bindposes.Length == skin.bones.Length;
            var matrices = manual ? skin.bones.Select((b, i) => b.localToWorldMatrix * data.bindposes[i]).ToArray() : null;
            if (!manual) skin.BakeMesh(bakedMesh);
            var vertices = manual ? data.vertices : bakedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point;
                if (manual)
                {
                    var w = data.weights[i]; var v = vertices[i];
                    point = matrices[w.boneIndex0].MultiplyPoint3x4(v) * w.weight0 + matrices[w.boneIndex1].MultiplyPoint3x4(v) * w.weight1
                        + matrices[w.boneIndex2].MultiplyPoint3x4(v) * w.weight2 + matrices[w.boneIndex3].MultiplyPoint3x4(v) * w.weight3;
                }
                else point = skin.transform.TransformPoint(vertices[i]);
                if (first) { result = new Bounds(point, Vector3.zero); first = false; } else result.Encapsulate(point);
            }
        }
        foreach (var mesh in PreviewObject.GetComponentsInChildren<MeshRenderer>(true))
            if (first) { result = mesh.bounds; first = false; } else result.Encapsulate(mesh.bounds);
        return result;
    }

    public void DrawControls()
    {
        int next = EditorGUILayout.Popup("动作", index, labels);
        if (next != index) { SelectClip(clips[next]); Playing = true; }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(Playing ? "暂停" : "播放", GUILayout.Width(54))) { Playing = !Playing; lastUpdate = EditorApplication.timeSinceStartup; }
            if (GUILayout.Button("重播", GUILayout.Width(54))) { SetTime(0); Playing = true; }
            Loop = GUILayout.Toggle(Loop, "循环", GUILayout.Width(50));
            Follow = GUILayout.Toggle(Follow, "跟随", GUILayout.Width(50));
        }
        Speed = EditorGUILayout.Slider("播放倍速", Speed, .1f, 2);
        EditorGUI.BeginChangeCheck();
        float time = EditorGUILayout.Slider("进度（秒）", Time, 0, Clip.length);
        if (EditorGUI.EndChangeCheck()) { Playing = false; SetTime(time); }
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("拖动旋转 · 滚轮缩放", EditorStyles.miniLabel);
            if (GUILayout.Button("适配视野", GUILayout.Width(72))) { float timeBefore = Time; Fit(); SetTime(timeBefore); }
        }
    }

    public void Draw(Rect rect)
    {
        var e = Event.current;
        if (rect.Contains(e.mousePosition) && e.type == EventType.MouseDrag && e.button == 0)
        { yaw -= e.delta.x * .6f; pitch = Mathf.Clamp(pitch - e.delta.y * .6f, -85, 85); e.Use(); GUI.changed = true; }
        if (rect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
        { zoom = Mathf.Clamp(zoom * Mathf.Exp(e.delta.y * .06f), .2f, 4); e.Use(); GUI.changed = true; }
        if (e.type != EventType.Repaint || !HasClips) return;
        renderer.BeginPreview(rect, GUIStyle.none);
        FrameCamera(rect.width / Mathf.Max(1, rect.height));
        renderer.Render(true, true);
        var texture = renderer.EndPreview();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
    }

    void FrameCamera(float aspect)
    {
        var camera = renderer.camera;
        float radius = Follow ? poseRadius : Mathf.Max(.1f, bounds.extents.magnitude);
        float halfFov = camera.fieldOfView * Mathf.Deg2Rad * .5f;
        float distance = radius / Mathf.Sin(Mathf.Atan(Mathf.Tan(halfFov) * Mathf.Min(1, aspect))) * 1.08f * zoom;
        var rotation = Quaternion.Euler(pitch, yaw, 0);
        var center = Follow ? poseBounds.center : bounds.center;
        camera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
        camera.nearClipPlane = Mathf.Max(.001f, distance - radius * 1.5f);
        camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 1, distance + radius * 2);
    }

    // Also usable by editor QA without a GUI event or modifying the user's scene.
    public Texture2D Capture(int width = 800, int height = 600, bool transparent = false)
    {
        var background = renderer.camera.backgroundColor;
        if (transparent) renderer.camera.backgroundColor = Color.clear;
        renderer.BeginStaticPreview(new Rect(0, 0, width, height));
        FrameCamera(width / (float)height);
        renderer.Render(true, true);
        Texture2D rgba = null;
        if (transparent)
        {
            // EndStaticPreview exports RGB. Read its render target ourselves to retain cutout alpha.
            var previous = RenderTexture.active;
            RenderTexture.active = renderer.camera.targetTexture;
            rgba = new Texture2D(width, height, TextureFormat.RGBA32, false);
            rgba.ReadPixels(new Rect(0, 0, width, height), 0, 0); rgba.Apply();
            RenderTexture.active = previous;
        }
        var texture = renderer.EndStaticPreview();
        if (rgba) { UnityEngine.Object.DestroyImmediate(texture); texture = rgba; }
        renderer.camera.backgroundColor = background;
        return texture;
    }

    public void Dispose()
    {
        if (graph.IsValid()) graph.Destroy();
        UnityEngine.Object.DestroyImmediate(bakedMesh);
        renderer.Cleanup();
    }
}
