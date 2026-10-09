using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Share a bake per source mesh/material; keep thousands of pristine scene objects unchanged until hit.</summary>
public static class VoxelWildernessBuilder
{
    const int BakeVersion=4;
    static bool Opaque(Material material)
    {
        if(!material || !material.shader)return false;
        string name=material.shader.name;
        if(name.IndexOf("Transparent",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Leaves",StringComparison.OrdinalIgnoreCase)>=0)return false;
        return !material.HasProperty("_Mode") || material.GetFloat("_Mode")==0;
    }
    [MenuItem("修仙/体素/建立整张宗门野外破坏清单")]
    public static void 建立()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play before baking.");
        var root=GameObject.Find("野外环境");
        var manifest=AssetDatabase.LoadAssetAtPath<VoxelMapPilotSettings>(VoxelMapPilotBuilder.SettingsPath);
        if(!root || !manifest || root.scene.path!=manifest.场景路径 || root.scene.isDirty)throw new InvalidOperationException("Open the unchanged formal wilderness scene first.");
        var existing=manifest.替换项.Where(e=>e.数据.烘焙版本>=BakeVersion || e.预先加载).GroupBy(e=>Key(e.源网格,e.源材质)).ToDictionary(g=>g.Key,g=>g.First().数据);
        foreach(string guid in AssetDatabase.FindAssets("t:VoxelVolumeAsset",new[]{"Assets/Art/Environment/World/Voxel/Bakes"}))
        {
            var baked=AssetDatabase.LoadAssetAtPath<VoxelVolumeAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if(baked && baked.烘焙版本>=BakeVersion && baked.原网格 && baked.实体数量>0 && baked.实体.Length<=160000)existing[Key(baked.原网格,baked.原材质)]=baked;
        }
        var filters=root.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled).ToArray();
        var selected=filters.Where(f=>IsRock(f) || IsTreeTrunk(f)).ToArray();
        var entries=new List<VoxelMapPilotSettings.Entry>();int types=0;
        foreach(var group in selected.GroupBy(f=>Key(f.sharedMesh,f.GetComponent<MeshRenderer>().sharedMaterials)))
        {
            var first=group.First();var materials=first.GetComponent<MeshRenderer>().sharedMaterials;bool tree=IsTreeTrunk(first);
            if(!existing.TryGetValue(group.Key,out var data))
            {
                float maxScale=group.Max(f=>MaxScale(f.transform.lossyScale));float cell=(tree?.08f:.12f)/maxScale;
                var size=first.sharedMesh.bounds.size;
                float minimum=float.PositiveInfinity;for(int axis=0;axis<3;axis++)if(size[axis]>1e-7f)minimum=Mathf.Min(minimum,size[axis]);
                cell=Mathf.Min(cell,minimum/4);
                while(EstimatedCells(size,cell)>160000)cell*=1.1f;
                var temp=new GameObject((tree?"Trunk_":"Rock_")+first.sharedMesh.name);temp.hideFlags=HideFlags.HideAndDontSave;
                try
                {
                    temp.AddComponent<MeshFilter>().sharedMesh=first.sharedMesh;temp.AddComponent<MeshRenderer>().sharedMaterials=materials;
                    string path=VoxelPrototypeBaker.创建(temp,cell,8,true,true,false,true);
                    data=AssetDatabase.LoadAssetAtPath<VoxelVolumeAsset>(path);data.来源=AssetDatabase.GetAssetPath(first.sharedMesh)+"#"+first.sharedMesh.name;
                    data.原网格=first.sharedMesh;data.原材质=materials;data.烘焙版本=BakeVersion;
                    if(data.实体数量==0)throw new InvalidOperationException("Empty source bake: "+data.来源);
                    EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();existing.Add(group.Key,data);types++;
                }
                finally {UnityEngine.Object.DestroyImmediate(temp);}
            }
            foreach(var filter in group)
            {
                var path=new List<Transform>();var t=filter.transform;while(t.parent){path.Add(t);t=t.parent;}path.Reverse();
                int ascent=0;
                if(tree)
                {
                    var resource=PrefabUtility.GetNearestPrefabInstanceRoot(filter.gameObject);
                    var ancestor=filter.transform;while(resource && ancestor.gameObject!=resource && ancestor.parent){ascent++;ancestor=ancestor.parent;}
                    if(ascent>4)throw new InvalidOperationException("Unexpected tree hierarchy: "+filter.name);
                }
                entries.Add(new VoxelMapPilotSettings.Entry {
                    名称=(tree?"树干/":"岩石/")+filter.name,根名称=t.name,子索引=path.Select(n=>n.GetSiblingIndex()).ToArray(),子名称=path.Select(n=>n.name).ToArray(),
                    预期位置=filter.transform.position,预期旋转=filter.transform.rotation,预期缩放=filter.transform.lossyScale,
                    源网格=filter.sharedMesh,源材质=filter.GetComponent<MeshRenderer>().sharedMaterials,预期包围盒=VoxelMapPilotBuilder.计算包围盒(filter),数据=data,
                    树木=tree,资源上溯层数=ascent,树冠网格=CanopyMeshes(filter,ascent),预先加载=!tree && filter.name=="stone_012" && Mathf.Abs(filter.transform.position.z-82)<.01f });
            }
        }
        foreach(var data in entries.Select(e=>e.数据).Distinct())VoxelSourceAppearance.同步(data);
        manifest.替换项=entries.OrderByDescending(e=>e.预先加载).ToArray();EditorUtility.SetDirty(manifest);AssetDatabase.SaveAssets();
        Debug.Log($"WILDERNESS_VOXEL_MANIFEST rocks={entries.Count(e=>!e.树木)} trunks={entries.Count(e=>e.树木)} sharedTypes={existing.Count} newBakes={types}; pristine sources remain lazy, scene unchanged.");
    }
    static bool IsRock(MeshFilter f)
    {return f.name.StartsWith("stone_",StringComparison.OrdinalIgnoreCase);}
    static Mesh[] CanopyMeshes(MeshFilter trunk,int ascent)
    {
        var root=trunk.transform;for(int i=0;i<ascent;i++)root=root.parent;
        return root.GetComponentsInChildren<Renderer>(true).Where(r=>!r.transform.IsChildOf(trunk.transform))
            .Select(r=>r.GetComponent<MeshFilter>()?.sharedMesh).ToArray();
    }
    static bool IsTreeTrunk(MeshFilter f)
    {
        var t=f.transform;bool forest=false;while(t){if(t.name=="森林"){forest=true;break;}t=t.parent;}
        return forest && f.GetComponent<MeshRenderer>().sharedMaterials.All(Opaque);
    }
    static float MaxScale(Vector3 v)=>Mathf.Max(Mathf.Abs(v.x),Mathf.Max(Mathf.Abs(v.y),Mathf.Abs(v.z)));
    static double EstimatedCells(Vector3 size,float step)=>((double)Mathf.CeilToInt(size.x/step)+4)*(Mathf.CeilToInt(size.y/step)+4)*(Mathf.CeilToInt(size.z/step)+4);
    static string Key(Mesh mesh,Material[] materials)=>mesh.GetInstanceID()+"/"+string.Join(",",materials.Select(m=>m?m.GetInstanceID():0));
}
