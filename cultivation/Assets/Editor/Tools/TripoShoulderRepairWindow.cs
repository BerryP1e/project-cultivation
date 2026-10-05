using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class TripoShoulderRepairWindow : EditorWindow
{
    GameObject source;
    float left = .25f, right = .25f, rise, yaw;
    bool linked = true, skeleton = true;
    int motion;
    PreviewRenderUtility preview;
    TripoShoulderRepair.Result before, after;
    PlayableGraph graph;
    AnimationClipPlayable[] players;
    string error, export;
    float height, width;
    Vector3 center, front;

    [MenuItem("修仙/动画/Tripo肩关节修复工具")]
    public static void Open()
    {
        var window = GetWindow<TripoShoulderRepairWindow>("Tripo肩关节修复");
        window.minSize = new Vector2(820, 650);
        window.source = Selection.activeObject as GameObject;
        window.Rebuild();
    }

    void OnDisable() => Clear();
    void Update() { if (preview != null) Repaint(); }
    void Clear()
    {
        if (graph.IsValid()) graph.Destroy();
        before?.Dispose(); after?.Dispose(); before = after = null;
        preview?.Cleanup(); preview = null;
    }

    void Rebuild()
    {
        Clear(); error = null;
        if (EditorApplication.isPlaying) { error = "肩关节工具仅在编辑模式工作，请先退出Play"; return; }
        if (source == null) return;
        try
        {
            before = TripoShoulderRepair.创建副本(source, 0, 0, 0);
            after = TripoShoulderRepair.创建副本(source, left, right, rise);
            before.设置预览材质(); after.设置预览材质();
            preview = new PreviewRenderUtility();
            preview.cameraFieldOfView = 30;
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 1000;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(.16f, .17f, .18f);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, -30, 0);
            preview.lights[1].intensity = .5f;
            var bounds = before.root.GetComponentsInChildren<Renderer>(true).First().bounds;
            foreach (var renderer in before.root.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(renderer.bounds);
            height = bounds.size.y; width = Mathf.Max(bounds.size.x, height * .65f);
            center = bounds.center;
            var bones = before.root.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
            var across = (bones["R_Upperarm"].position - bones["L_Upperarm"].position).normalized;
            front = Vector3.Cross(across, Vector3.up).normalized;
            before.root.transform.position -= across * width * .6f;
            after.root.transform.position += across * width * .6f;
            preview.AddSingleGO(before.root); preview.AddSingleGO(after.root);
            foreach (var renderer in before.root.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.forceMatrixRecalculationPerRender = true;
            foreach (var renderer in after.root.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.forceMatrixRecalculationPerRender = true;
            SetMotion();
        }
        catch (Exception e) { Clear(); error = e.Message; }
    }

    void SetMotion()
    {
        if (graph.IsValid()) graph.Destroy();
        if (motion == 0 || before == null) return;
        AnimationClip clip;
        if (motion == 3) clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/凭虚御风/御风_前进.anim");
        else clip = AssetDatabase.LoadAllAssetsAtPath("Assets/resources/Animation Library/1/UAL1_Standard.fbx").OfType<AnimationClip>().First(c => c.name == (motion == 1 ? "Armature|Idle_Loop" : "Armature|Sprint_Loop"));
        graph = PlayableGraph.Create("TripoShoulderPreview");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        players = new AnimationClipPlayable[2];
        var rigs = new[] { before, after };
        for (int i = 0; i < 2; i++)
        {
            var animator = rigs[i].root.GetComponent<Animator>();
            animator.enabled = true;
            animator.runtimeAnimatorController = null; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            players[i] = AnimationClipPlayable.Create(graph, clip);
            players[i].SetSpeed(0);
            AnimationPlayableOutput.Create(graph, "Rig" + i, animator).SetSourcePlayable(players[i]);
        }
        graph.Play();
    }

    void OnGUI()
    {
        if (EditorApplication.isPlaying) { Clear(); EditorGUILayout.HelpBox("请先退出Play，再使用肩关节修复工具。", MessageType.Info); return; }
        EditorGUILayout.HelpBox("修复的是上臂根部（肩关节），锁骨内端留在胸部。仅处理模型副本，肘和手位置保留；导出独立Mesh、Avatar与纯模型Prefab。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        source = (GameObject)EditorGUILayout.ObjectField("原始Tripo模型", source, typeof(GameObject), false);
        linked = EditorGUILayout.Toggle("双侧联动", linked);
        left = EditorGUILayout.Slider("左肩外移 / 原上臂长度", left, 0, .55f);
        if (linked) right = left;
        using (new EditorGUI.DisabledScope(linked)) right = EditorGUILayout.Slider("右肩外移 / 原上臂长度", right, 0, .55f);
        rise = EditorGUILayout.Slider("肩部升降 / 原上臂长度", rise, -.25f, .25f);
        if (EditorGUI.EndChangeCheck()) Rebuild();
        EditorGUI.BeginChangeCheck();
        motion = GUILayout.Toolbar(motion, new[] { "绑定姿势", "待机", "跑步", "御风前进" });
        if (EditorGUI.EndChangeCheck()) Rebuild();
        skeleton = EditorGUILayout.Toggle("显示肩肘手连线", skeleton);
        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        if (after != null)
        {
            EditorGUILayout.LabelField("肩宽", after.shoulderBefore.ToString("F4") + " → " + after.shoulderAfter.ToString("F4") + " 模型单位");
            EditorGUILayout.LabelField("肘 / 手位置误差", after.elbowDrift.ToString("G3") + " / " + after.handDrift.ToString("G3"));
        }
        var rect = GUILayoutUtility.GetRect(400, 10000, 250, 10000, GUILayout.ExpandHeight(true));
        if (preview != null && Event.current.type == EventType.Repaint)
        {
            if (graph.IsValid())
            {
                foreach (var player in players) player.SetTime(EditorApplication.timeSinceStartup % player.GetAnimationClip().length);
                graph.Evaluate(0);
            }
            preview.BeginPreview(rect, GUIStyle.none);
            var target = center;
            preview.camera.transform.position = target + Quaternion.Euler(0, yaw, 0) * front * Mathf.Max(height * 2.5f, width * 3.2f) + Vector3.up * height * .05f;
            preview.camera.transform.LookAt(target);
            preview.Render();
            GUI.DrawTexture(rect, preview.EndPreview(), ScaleMode.StretchToFill, false);
            if (skeleton) { DrawBones(before.root, rect, new Color(1, .35f, .3f)); DrawBones(after.root, rect, new Color(1, .83f, .25f)); }
        }
        if (preview != null)
        {
            GUI.Label(new Rect(rect.x + 15, rect.y + 10, 180, 25), "原始肩位（红）");
            GUI.Label(new Rect(rect.center.x + 15, rect.y + 10, 180, 25), "修后肩位（金）");
            if (Event.current.type == EventType.MouseDrag && rect.Contains(Event.current.mousePosition)) { yaw += Event.current.delta.x; Event.current.Use(); Repaint(); }
        }
        using (new EditorGUI.DisabledScope(after == null || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("导出新的肩部修复模型…", GUILayout.Height(30)))
            {
                string folder = EditorUtility.OpenFolderPanel("选择Assets中的输出目录", Application.dataPath, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    string relative = FileUtil.GetProjectRelativePath(folder);
                    if (relative != "Assets" && !relative.StartsWith("Assets/")) error = "必须选Assets内部目录";
                    else try { if (graph.IsValid()) graph.Destroy(); export = TripoShoulderRepair.导出(after, relative); Rebuild(); } catch (Exception e) { error = e.Message; }
                }
            }
        }
        if (!string.IsNullOrEmpty(export)) EditorGUILayout.HelpBox("已导出：" + export + "。这是纯模型，尚未替换游戏中的模型引用。", MessageType.Info);
    }

    void DrawBones(GameObject rig, Rect rect, Color color)
    {
        Vector3 Screen(Transform t) { var v = preview.camera.WorldToViewportPoint(t.position); return new Vector3(rect.x + v.x * rect.width, rect.y + (1 - v.y) * rect.height, 0); }
        var map = rig.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
        Handles.BeginGUI(); Handles.color = color;
        foreach (string side in new[] { "L_", "R_" })
        {
            var points = new[] { "Clavicle", "Upperarm", "Forearm", "Hand" }.Select(n => Screen(map[side + n])).ToArray();
            Handles.DrawAAPolyLine(3, points);
            var p = points[1]; Handles.DrawAAPolyLine(3, Enumerable.Range(0, 25).Select(i => p + new Vector3(Mathf.Cos(i * Mathf.PI / 12), Mathf.Sin(i * Mathf.PI / 12), 0) * 7).ToArray());
        }
        Handles.EndGUI();
    }
}
