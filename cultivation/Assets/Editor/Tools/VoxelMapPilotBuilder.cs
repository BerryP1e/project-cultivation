using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Collections;

/// <summary>Build a small opt-in manifest without saving or regenerating the formal scene.</summary>
public static class VoxelMapPilotBuilder
{
    public const string ScenePath="Assets/Scenes/Sect_Wilderness.scene";
    public const string SettingsPath="Assets/resources/Voxel/MapPilots/Sect_Wilderness.asset";

    [MenuItem("修仙/体素/定位宗门野外试点配置")]
    public static void 定位配置()
    {Selection.activeObject=AssetDatabase.LoadAssetAtPath<VoxelMapPilotSettings>(SettingsPath);}

    [MenuItem("修仙/体素/建立宗门野外岩石试点")]
    public static void 建立()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
        if(AssetDatabase.LoadAssetAtPath<VoxelMapPilotSettings>(SettingsPath))
        {定位配置();Debug.Log("[体素试点] 配置已存在，保留当前配置和烘焙。");return;}
        var current=SceneManager.GetActiveScene();
        if(current.isDirty)throw new InvalidOperationException("Current scene contains unsaved edits.");
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var filters=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true));
            var selected=filters.Where(f=>f.name=="stone_012" && f.transform.parent && f.transform.parent.name=="山口石" && Mathf.Abs(f.transform.position.z-82)<.01f).ToArray();
            if(selected.Length!=2)throw new InvalidOperationException("Expected exactly two entrance rocks. Audit the scene before expanding the pilot.");
            var prefab=PrefabUtility.GetCorrespondingObjectFromSource(selected[0].gameObject);
            if(!prefab || prefab.GetComponentsInChildren<MeshFilter>().Length!=1)throw new InvalidOperationException("Pilot expects a single static mesh prefab.");
            // One shared bake; each runtime instance owns only its mutable state and changed chunks.
            string volumePath=VoxelPrototypeBaker.创建(prefab,.055f,8,true,true,false);
            var data=AssetDatabase.LoadAssetAtPath<VoxelVolumeAsset>(volumePath);
            var entries=new List<VoxelMapPilotSettings.Entry>();
            foreach(var filter in selected)
            {
                if(PrefabUtility.GetCorrespondingObjectFromSource(filter.gameObject)!=prefab)throw new InvalidOperationException("Unexpected mixed source prefabs.");
                var path=new List<Transform>();var t=filter.transform;while(t.parent){path.Add(t);t=t.parent;}path.Reverse();
                entries.Add(new VoxelMapPilotSettings.Entry {
                    名称=filter.name+"_"+filter.transform.position.x.ToString("F1"),根名称=t.name,
                    子索引=path.Select(n=>n.GetSiblingIndex()).ToArray(),子名称=path.Select(n=>n.name).ToArray(),
                    预期位置=filter.transform.position,预期旋转=filter.transform.rotation,预期缩放=filter.transform.lossyScale,
                    源网格=filter.sharedMesh,源材质=filter.GetComponent<MeshRenderer>().sharedMaterials,
                    预期包围盒=计算包围盒(filter),数据=data });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));AssetDatabase.Refresh();
            var settings=ScriptableObject.CreateInstance<VoxelMapPilotSettings>();settings.场景路径=ScenePath;settings.替换项=entries.ToArray();
            AssetDatabase.CreateAsset(settings,SettingsPath);AssetDatabase.SaveAssets();Selection.activeObject=settings;
            Debug.Log($"VOXEL_PILOT_BUILT rocks={entries.Count} sharedChunks={data.分块坐标.Length} sourceSceneDirty={scene.isDirty}; source scene and prefabs were not saved.");
        }
        finally {if(opened)EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(current);}
    }
    public static Bounds 计算包围盒(MeshFilter filter)
    {
        using(var meshes=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
        using(var vertices=new NativeArray<Vector3>(meshes[0].vertexCount,Allocator.Temp))
        {
            meshes[0].GetVertices(vertices);var bounds=new Bounds(filter.transform.TransformPoint(vertices[0]),Vector3.zero);
            for(int i=1;i<vertices.Length;i++)bounds.Encapsulate(filter.transform.TransformPoint(vertices[i]));
            return bounds;
        }
    }
}
