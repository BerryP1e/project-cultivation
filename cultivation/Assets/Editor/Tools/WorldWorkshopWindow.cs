using System;
using System.IO;
using System.Linq;
using System.Text;
using Cultivation.WorldAuthoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WorldWorkshopWindow : EditorWindow
{
    Vector2 scroll;
    [MenuItem("修仙/场景工坊/地图管理与一致性检查",false,0)]
    public static void Open()=>GetWindow<WorldWorkshopWindow>("场景工坊");
    void OnGUI()
    {
        EditorGUILayout.LabelField("洞府 · 宗门野外 · 镇妖阁",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("编辑正式地图后点保存，停止工坊会自动更新游戏地图。这里也可以手动应用已保存地图；功能引用保留。",MessageType.Info);
        if(GUILayout.Button("打开独立地图编辑器"))WorldMapEditorBuilder.Open();
        scroll=EditorGUILayout.BeginScrollView(scroll);
        for(int i=0;i<WorldSceneMigration.Scenes.Length;i++)
        {
            string scene=WorldSceneMigration.Scenes[i],label=WorldSceneMigration.Labels[i];EditorGUILayout.Space();EditorGUILayout.LabelField(label+" · "+scene,EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("打开游戏场景")){if(!EditorApplication.isPlaying&&EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene("Assets/Scenes/"+scene+".scene");}
                if(GUILayout.Button("编辑这张地图"))Edit(label);
                using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))if(GUILayout.Button("应用已保存地图"))Apply(scene,label);
            }
        }
        EditorGUILayout.Space();if(GUILayout.Button("检查家园与镇妖阁（只报告）"))Audit();
        EditorGUILayout.EndScrollView();
    }
    static void Edit(string label)
    {
        if(EditorApplication.isPlaying){var editor=UnityEngine.Object.FindObjectOfType<WorldMapEditorController>();if(editor){editor.GetComponent<WorldMapEditorUI>()?.FlushName();if(editor.Dirty){editor.Save();if(editor.Dirty)return;}editor.Load(label);editor.GetComponent<WorldMapEditorUI>()?.SetMapName(editor.Data.name);editor.FocusSpawn();return;}Debug.LogWarning("先停止当前游戏，再打开工坊。");return;}
        WorldMapEditorBuilder.Open();if(SceneManager.GetActiveScene().path!=WorldMapEditorBuilder.ScenePath)return;SessionState.SetString("WorldWorkshop.PendingMap",label);EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod]
    static void Initialize(){EditorApplication.playModeStateChanged-=Changed;EditorApplication.playModeStateChanged+=Changed;WorldMapEditorController.MapSaved-=Saved;WorldMapEditorController.MapSaved+=Saved;WatchPending();}
    static void WatchPending(){EditorApplication.update-=ApplyPending;EditorApplication.update+=ApplyPending;}
    static void Saved(WorldMapData data)
    {
        var scene=Array.Find(WorldSceneMigration.Scenes,x=>string.Equals(x,data.sourceScene,StringComparison.OrdinalIgnoreCase));if(scene==null)return;
        SessionState.SetString("WorldWorkshop.PendingApply."+scene,data.name);
        WatchPending();
        if(!EditorApplication.isPlaying)ApplyPending();
    }
    static void ApplyPending()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        foreach(var scene in WorldSceneMigration.Scenes){var key="WorldWorkshop.PendingApply."+scene;var label=SessionState.GetString(key,"");if(string.IsNullOrEmpty(label))continue;try{Apply(scene,label);SessionState.EraseString(key);}catch(Exception exception){Debug.LogError("地图自动更新失败，已保存文件保留，可在管理窗口重试："+exception.Message);}}
        EditorApplication.update-=ApplyPending;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.EnteredEditMode){WatchPending();return;}
        if(state!=PlayModeStateChange.EnteredPlayMode)return;string pending=SessionState.GetString("WorldWorkshop.PendingMap","");if(string.IsNullOrEmpty(pending))return;
        void LoadWhenReady(){if(!EditorApplication.isPlaying){EditorApplication.update-=LoadWhenReady;return;}var editor=UnityEngine.Object.FindObjectOfType<WorldMapEditorController>();if(!editor||editor.Data==null||editor.PlayerReference==null||!editor.GetComponent<WorldMapEditorUI>())return;EditorApplication.update-=LoadWhenReady;SessionState.EraseString("WorldWorkshop.PendingMap");editor.Load(pending);editor.GetComponent<WorldMapEditorUI>().SetMapName(editor.Data.name);editor.FocusSpawn();}
        EditorApplication.update+=LoadWhenReady;
    }
    public static void Apply(string scene,string label)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("停止运行后再应用。");var data=WorldMapStorage.Load(label);if(!string.Equals(data.sourceScene,scene,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("地图所属场景不匹配，不能覆盖其他游戏场景。");WorldFeatureRuntime.Synchronize(data);
        string path="Assets/WorldAuthoring/Scenes/"+scene+"/Map.asset";var asset=AssetDatabase.LoadAssetAtPath<WorldMapAsset>(path);if(!asset)throw new InvalidOperationException("场景尚未迁移。");
        string backup=Path.GetFullPath("../.local/backups/map-apply");Directory.CreateDirectory(backup);File.WriteAllText(Path.Combine(backup,scene+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json"),asset.sourceJson);
        asset.sourceJson=JsonUtility.ToJson(data);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);Debug.Log("已应用「"+label+"」到原游戏场景；功能关联保留。");
    }
    public static string Audit()
    {
        var report=new StringBuilder("# 场景地图一致性检查\n");int errors=0;var prior=SceneManager.GetActiveScene();
        foreach(var name in WorldSceneMigration.Scenes)
        {
            var path="Assets/Scenes/"+name+".scene";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();var loader=roots.Select(x=>x.GetComponent<WorldMapLoader>()).FirstOrDefault(x=>x);if(!loader||!loader.map){report.AppendLine("\n- "+name+"：尚未迁移");errors++;continue;}
                var data=JsonUtility.FromJson<WorldMapData>(loader.map.sourceJson);var issues=WorldMapConsistency.Check(data,loader.map.palette,loader.bindings.Where(x=>x.target).Select(x=>x.key));
                foreach(var feature in data.features)if(!loader.bindings.Any(x=>x.key==feature.id&&x.target)&&loader.map.palette.Feature(feature.templateId).references.Length>0)issues.Add("新增功能需核对外部绑定："+feature.label);
                foreach(var root in roots)foreach(var teleporter in root.GetComponentsInChildren<Teleporter>(true))foreach(var option in teleporter.选项)if(!option.暂未开放&&!string.IsNullOrEmpty(option.场景)&&!EditorBuildSettings.scenes.Any(x=>x.enabled&&Path.GetFileNameWithoutExtension(x.path)==option.场景))issues.Add("传送目标未加入构建："+option.场景);
                errors+=issues.Count;report.AppendLine("\n## "+name+"\n外观 "+data.objects.Count+"，功能 "+data.features.Count+"，问题 "+issues.Count+"。");foreach(var issue in issues)report.AppendLine("- "+issue);
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        SceneManager.SetActiveScene(prior);Directory.CreateDirectory("../docs/reports");File.WriteAllText("../docs/reports/家园与镇妖阁一致性检查.md",report.ToString());Debug.Log("场景一致性检查："+errors+" 项，见 docs/reports/家园与镇妖阁一致性检查.md");return report.ToString();
    }
}
