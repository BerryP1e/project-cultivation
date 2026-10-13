using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>共享同一套轮廓算法，编辑器地面与玩家扩建结果一致。</summary>
public static class HomeGroundBaker
{
    const string Folder="Assets/Art/Environment/Home";
    const string Stone="Assets/Art/Environment/Imported/Environment1/Stone/environment_stone_dabingshi_001_b.FBX";
    const string Textures="Assets/Art/Environment/World/Models/Materials/Scene/Textures/Dibiao/";
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static Vector2[] Outline(GameObject source,float rounding)
    {
        var points=new List<Vector2>();
        foreach(var f in source.GetComponentsInChildren<MeshFilter>(true))foreach(var v in f.sharedMesh.vertices)
        {var p=source.transform.InverseTransformPoint(f.transform.TransformPoint(v));points.Add(new Vector2(p.x,p.z));}
        float x0=points.Min(p=>p.x),x1=points.Max(p=>p.x),z0=points.Min(p=>p.y),z1=points.Max(p=>p.y);
        var sorted=points.Select(p=>new Vector2((p.x-(x0+x1)*.5f)*44/(x1-x0),(p.y-(z0+z1)*.5f)*44/(z1-z0))).Distinct().OrderBy(p=>p.x).ThenBy(p=>p.y).ToList();
        var hull=new List<Vector2>();
        foreach(var p in sorted){while(hull.Count>=2&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        int lower=hull.Count;
        for(int i=sorted.Count-2;i>=0;i--){var p=sorted[i];while(hull.Count>lower&&Cross(hull[hull.Count-1]-hull[hull.Count-2],p-hull[hull.Count-1])<=0)hull.RemoveAt(hull.Count-1);hull.Add(p);}
        hull.RemoveAt(hull.Count-1);
        // 内缩凸轮廓，再由距离场向外圆角，维持原石台大小。
        var inner=new List<Vector2>(hull);
        for(int i=0;i<hull.Count;i++)
        {
            var a=hull[i];var e=(hull[(i+1)%hull.Count]-a).normalized;var n=new Vector2(-e.y,e.x);
            var clipped=new List<Vector2>();
            for(int j=0;j<inner.Count;j++)
            {
                var p=inner[j];var q=inner[(j+1)%inner.Count];float dp=Vector2.Dot(p-a,n)-rounding,dq=Vector2.Dot(q-a,n)-rounding;
                if(dp>=0)clipped.Add(p);
                if((dp<0&&dq>0)||(dp>0&&dq<0))clipped.Add(Vector2.Lerp(p,q,dp/(dp-dq)));
            }
            inner=clipped;
        }
        for(int i=inner.Count-1;i>=0;i--)if((inner[i]-inner[(i+1)%inner.Count]).sqrMagnitude<.0001f)inner.RemoveAt(i);
        return inner.ToArray();
    }
    static Mesh StoreMesh(Mesh generated,string path)
    {
        generated.name=Path.GetFileNameWithoutExtension(path);
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing)
        {
            // 通过网格 API 更新 GPU 缓冲；只 CopySerialized 会留下旧的可见网格。
            existing.Clear();existing.indexFormat=generated.indexFormat;existing.name=generated.name;
            existing.vertices=generated.vertices;existing.normals=generated.normals;existing.colors=generated.colors;existing.uv=generated.uv;
            existing.triangles=generated.triangles;existing.bounds=generated.bounds;existing.UploadMeshData(false);
            UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);return existing;
        }
        AssetDatabase.CreateAsset(generated,path);return generated;
    }
    static GameObject Bake(HomeAssets assets,IEnumerable<Vector2Int> cells,string name,string path,string meshPath=null)
    {
        var list=cells.ToList();var mesh=StoreMesh(HomeLandMesh.生成(assets,list,out _),meshPath??Folder+"/"+name+".asset");
        var go=new GameObject(name);
        try
        {
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=assets.地面材质;
            go.AddComponent<MeshCollider>().sharedMesh=mesh;go.AddComponent<HomeSurface>();
            var boundary=new GameObject("边界围挡");boundary.transform.SetParent(go.transform,false);boundary.AddComponent<HomeBoundary>().同步(assets,list);
            return PrefabUtility.SaveAsPrefabAsset(go,path);
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    static GameObject BakeNature(string sourcePath,string name,float height,float width,float pitch=0)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);if(!source)throw new InvalidOperationException("缺少围挡资源 "+sourcePath);
        var go=new GameObject(name);
        try
        {
            var normalized=new GameObject("模型");normalized.transform.SetParent(go.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(normalized.transform,false);
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(pitch,0,0);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            foreach(var script in model.GetComponentsInChildren<MonoBehaviour>(true))if(script)UnityEngine.Object.DestroyImmediate(script);
            var renderers=model.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)throw new InvalidOperationException("围挡模型无渲染器");
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z);
            normalized.transform.localScale=Vector3.one*Mathf.Min(height/bounds.size.y,width/Mathf.Max(bounds.size.x,bounds.size.z));
            return PrefabUtility.SaveAsPrefabAsset(go,Folder+"/"+name+".prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }
    [MenuItem("Tools/家园/烘焙草地与融合示例")]
    public static void BakeAssets()
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var assets=AssetDatabase.LoadAssetAtPath<HomeAssets>("Assets/resources/Home/Assets.asset");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Stone);
        if(!assets||!source)throw new InvalidOperationException("缺少家园资源或源石台");
        assets.地面圆角=2.4f;assets.地面内轮廓=Outline(source,assets.地面圆角);
        var shader=Shader.Find("Cultivation/HomeGround");if(!shader)throw new InvalidOperationException("家园草地 Shader 未导入");
        string matPath=Folder+"/HomeGround.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,matPath);}mat.shader=shader;
        mat.SetTexture("_GrassTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"grass009.bmp"));
        mat.SetTexture("_SoilTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"dirt036.bmp"));
        mat.SetTexture("_RockTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Textures+"dirt022.bmp"));
        mat.SetFloat("_GrassScale",7);mat.SetFloat("_RockScale",3);
        EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);assets.地面材质=mat;
        assets.围挡树木=new[]{
            BakeNature("Assets/Art/Environment/World/Tree/environment_Tree_Green_003_a.FBX","BoundaryTree_0",6f,6f),
            BakeNature("Assets/Art/Environment/World/Tree/environment_Tree_Green_005_b.FBX","BoundaryTree_1",6f,5f)};
        assets.围挡石头=new[]{
            BakeNature("Assets/Art/Environment/Imported/Environment2/Stone/stone_07.prefab","BoundaryRock_0",2.1f,3.1f,-90),
            BakeNature("Assets/Art/Environment/Imported/Environment2/Stone/stone_08.prefab","BoundaryRock_1",1.8f,3.1f,-90)};
        assets.地面=Bake(assets,new[]{Vector2Int.zero},"HomeGroundSurface","Assets/resources/Home/StonePlatform.prefab");
        Bake(assets,new[]{Vector2Int.zero,Vector2Int.right},"HomeGroundMergedPair",Folder+"/HomeGroundMergedPair.prefab");
        EditorUtility.SetDirty(assets);AssetDatabase.SaveAssetIfDirty(assets);
    }
    [MenuItem("Tools/家园/替换当前洞府场景 Ground")]
    public static void ApplyToScene()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying||scene.name!="3C_Testbed")throw new InvalidOperationException("请在非运行状态的洞府场景使用");
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var ground=all.FirstOrDefault(t=>t.name=="Ground");if(!ground)throw new InvalidOperationException("没有找到 Ground");
        var parent=ground.parent;
        Undo.DestroyObjectImmediate(ground.gameObject);
        var replacement=(GameObject)PrefabUtility.InstantiatePrefab(HomeAssets.取().地面,scene);
        Undo.RegisterCreatedObjectUndo(replacement,"替换家园地面");replacement.name="Ground";replacement.transform.SetParent(parent,false);
        replacement.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);replacement.transform.localScale=Vector3.one;
        var boundary=all.FirstOrDefault(t=>t&&t.name=="Boundary");if(boundary)Undo.DestroyObjectImmediate(boundary.gameObject);
        EditorSceneManager.MarkSceneDirty(scene);
    }
    [MenuItem("Tools/家园/将当前扩建烘焙为预制体")]
    static void ExportCurrent()
    {
        var world=UnityEngine.Object.FindObjectOfType<HomeWorld>();if(!world)throw new InvalidOperationException("请先在运行中的洞府扩建地面");
        string path=EditorUtility.SaveFilePanelInProject("导出融合地面","HomeGroundCustom","prefab","选择 Assets 内保存位置");
        if(string.IsNullOrEmpty(path))return;
        Bake(HomeAssets.取(),world.所有地块,Path.GetFileNameWithoutExtension(path),path,Path.ChangeExtension(path,"asset"));
    }
}
