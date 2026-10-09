using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds the saved project without rebuilding or saving scenes.</summary>
public static class WindowsReleaseBuilder
{
    [Serializable]
    public class Result
    {
        public string status, executable, finishedUtc, message;
        public string[] scenes;
        public int errors, warnings;
        public long bytes;
        public double seconds;
    }

    const string Output = "Builds/Windows-x64/cultivation.exe";
    const string ReportPath = "Builds/windows-release-report.json";

    [MenuItem("Cultivation/Release/Build Windows x64")]
    public static void Schedule()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play before building.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your scene edits before building.");
        Directory.CreateDirectory("Builds");
        Build();
    }

    // Also available through -executeMethod WindowsReleaseBuilder.Build in batch mode.
    public static void Build()
    {
        var result = new Result { status = "Building", executable = Path.GetFullPath(Output) };
        try
        {
            result.scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
                .Select(s => s.path).Where(p => AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (result.scenes.Length == 0 || !result.scenes[0].EndsWith("StartScene.scene", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The first scene must be StartScene.");

            // Shader.Find-only effects must survive player shader stripping.
            var graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Shaders", "Assets/Art/Environment/World" }))
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                bool present = Enumerable.Range(0, shaders.arraySize)
                    .Any(i => shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader);
                if (shader == null || present) continue;
                int index = shaders.arraySize++;
                shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            Write(result);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = result.scenes,
                locationPathName = Output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            result.status = report.summary.result.ToString();
            result.errors = (int)report.summary.totalErrors;
            result.warnings = (int)report.summary.totalWarnings;
            result.bytes = (long)report.summary.totalSize;
            result.seconds = report.summary.totalTime.TotalSeconds;
            result.message = string.Join("\n", report.steps.SelectMany(s => s.messages)
                .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                .Select(m => m.content).Distinct());
        }
        catch (Exception e)
        {
            result.status = "Failed";
            result.message = e.ToString();
            Debug.LogException(e);
        }
        finally
        {
            result.finishedUtc = DateTime.UtcNow.ToString("o");
            Write(result);
            Debug.Log("[WindowsRelease] " + JsonUtility.ToJson(result));
        }
        if (Application.isBatchMode) EditorApplication.Exit(result.status == "Succeeded" ? 0 : 1);
    }

    static void Write(Result result)
    {
        Directory.CreateDirectory("Builds");
        File.WriteAllText(ReportPath, JsonUtility.ToJson(result, true));
    }
}
