using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Offline triangle/box voxelization. Source import settings and scenes are never rewritten.</summary>
public sealed class VoxelPrototypeBaker : EditorWindow
{
    const string Folder="Assets/Experiments/Voxel";
    const string DefaultModel="Assets/Art/Environment/Imported/Environment3/nature item/Rock_1.fbx";
    GameObject source;
    float cell=.15f;
    int chunk=8;
    bool fill=true;
    bool smooth=true;
    struct Triangle { public Vector3 a,b,c,na,nb,nc; public Vector2 ua,ub,uc; public Color tint; public Texture2D texture; public Texture sourceTexture; public Vector2 scale,offset; }

    [MenuItem("修仙/实验/体素局部破坏原型")]
    static void Open() { GetWindow<VoxelPrototypeBaker>("体素局部破坏"); }
    void OnGUI()
    {
        if(GUILayout.Button("打开已烘焙的原型场景"))
        {
            var scenes=AssetDatabase.FindAssets("t:Scene",new[]{Folder});
            if(scenes.Length>0 && !EditorApplication.isPlaying && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(AssetDatabase.GUIDToAssetPath(scenes[scenes.Length-1]));
        }
        EditorGUILayout.HelpBox("生成独立测试场景：左侧原模型，右侧分块体素。不会覆盖源模型或游玩场景。最长边统一为6米。",MessageType.Info);
        source=(GameObject)EditorGUILayout.ObjectField("源模型 / Prefab",source,typeof(GameObject),false);
        cell=EditorGUILayout.Slider("体素边长（米）",cell,.06f,.5f);
        chunk=EditorGUILayout.IntSlider("每块边长",chunk,4,16);
        fill=EditorGUILayout.Toggle("填充封闭内部",fill);
        smooth=EditorGUILayout.Toggle("连续平滑表面",smooth);
        EditorGUILayout.HelpBox("岩石可用平滑表面；建筑需要直角硬边时关闭平滑。建筑内部要保留空间时关闭填充。方块模式仍受格子对齐约束，尚非建筑专用破坏方案。薄片/透明树叶需要专门处理。",MessageType.None);
        using(new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if(GUILayout.Button("烘焙并打开独立对照场景"))
            {
                if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                try { 创建(source?source:AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModel),cell,chunk,fill,smooth); }
                catch(Exception e) { Debug.LogException(e); }
            }
    }

    public static string 创建(GameObject model,float cellSize=.15f,int chunkSize=8,bool fillInterior=true,bool smoothSurface=true,bool independentScene=true,bool closeOpenBoundaries=false)
    {
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before baking.");
        if(!model) throw new ArgumentException("Missing source model.");
        if(cellSize<(independentScene?.04f:1e-7f) || chunkSize<4 || chunkSize>16) throw new ArgumentOutOfRangeException("Invalid voxel settings.");
        if(SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Current scene has unsaved edits. Save or cancel before generating the standalone scene.");
        string rootFolder=independentScene?Folder:"Assets/Art/Environment/World/Voxel/Bakes";
        Directory.CreateDirectory(rootFolder);
        string folder=rootFolder+"/"+model.name+"_"+Guid.NewGuid().ToString("N").Substring(0,8);
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var textures=new Dictionary<Texture,Texture2D>();
        var instance=PrefabUtility.IsPartOfPrefabAsset(model)?(GameObject)PrefabUtility.InstantiatePrefab(model):Instantiate(model);
        instance.hideFlags=HideFlags.HideAndDontSave;
        try
        {
            var triangles=提取(instance,textures,out var bounds);
            if(triangles.Count==0) throw new InvalidOperationException("Source has no static mesh triangles.");
            if(closeOpenBoundaries)封闭端口(triangles,bounds);
            float normalize=independentScene?6/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)):1;
            Vector3 anchor=independentScene?new Vector3(bounds.center.x,bounds.min.y,bounds.center.z):Vector3.zero;
            for(int i=0;i<triangles.Count;i++)
            {
                var t=triangles[i]; t.a=(t.a-anchor)*normalize;t.b=(t.b-anchor)*normalize;t.c=(t.c-anchor)*normalize;triangles[i]=t;
            }
            bounds=new Bounds((bounds.center-anchor)*normalize,bounds.size*normalize);
            var data=ScriptableObject.CreateInstance<VoxelVolumeAsset>();
            data.来源=AssetDatabase.GetAssetPath(model); data.格子边长=cellSize;data.分块边长=chunkSize;
            data.平滑表面=smoothSurface;
            data.原点=bounds.min-Vector3.one*cellSize*2;
            data.尺寸=Vector3Int.CeilToInt(bounds.size/cellSize)+Vector3Int.one*4;
            int length=checked(data.尺寸.x*data.尺寸.y*data.尺寸.z);
            if(length>2000000) { DestroyImmediate(data); throw new InvalidOperationException("Prototype limits volume to 2 million cells."); }
            data.实体=new byte[length]; data.颜色=new Color32[length];
            var watch=System.Diagnostics.Stopwatch.StartNew();
            foreach(var t in triangles) 体素化(t,data);
            if(fillInterior) 填充(data);
            if(smoothSurface)
            {
                var points=new Vector3[triangles.Count*3];
                for(int i=0;i<triangles.Count;i++){points[i*3]=triangles[i].a;points[i*3+1]=triangles[i].b;points[i*3+2]=triangles[i].c;}
                VoxelDistanceBaker.烘焙(data,points,fillInterior);
            }
            var sourceTexture=triangles[0].sourceTexture;
            bool projectUV=true;
            foreach(var t in triangles)if(t.sourceTexture!=sourceTexture || t.tint!=triangles[0].tint)projectUV=false;
            if(projectUV)
            {
                data.表面三角点=new Vector3[triangles.Count*3];data.表面三角UV=new Vector2[triangles.Count*3];data.表面三角法线=new Vector3[triangles.Count*3];
                for(int i=0;i<triangles.Count;i++)
                {
                    var t=triangles[i];data.表面三角点[i*3]=t.a;data.表面三角点[i*3+1]=t.b;data.表面三角点[i*3+2]=t.c;
                    data.表面三角UV[i*3]=Vector2.Scale(t.ua,t.scale)+t.offset;
                    data.表面三角UV[i*3+1]=Vector2.Scale(t.ub,t.scale)+t.offset;
                    data.表面三角UV[i*3+2]=Vector2.Scale(t.uc,t.scale)+t.offset;
                    data.表面三角法线[i*3]=t.na;data.表面三角法线[i*3+1]=t.nb;data.表面三角法线[i*3+2]=t.nc;
                }
            }
            var coords=new List<Vector3Int>();var meshes=new List<Mesh>();
            Vector3Int grid=Vector3Int.CeilToInt((Vector3)data.尺寸/chunkSize);
            for(int z=0;z<grid.z;z++) for(int y=0;y<grid.y;y++) for(int x=0;x<grid.x;x++)
            {
                var key=new Vector3Int(x,y,z);var mesh=VoxelChunkMesher.生成(data,data.实体,key);
                bool occupied=false;
                var start=key*chunkSize;var end=Vector3Int.Min(start+Vector3Int.one*chunkSize,data.尺寸);
                for(int iz=start.z;iz<end.z && !occupied;iz++) for(int iy=start.y;iy<end.y && !occupied;iy++) for(int ix=start.x;ix<end.x;ix++)
                    if(data.实体[data.索引(ix,iy,iz)]!=0) { occupied=true;break; }
                if(!occupied && mesh.vertexCount==0) { DestroyImmediate(mesh);continue; }
                coords.Add(key);meshes.Add(mesh);
            }
            var occupiedIndices=new List<int>();for(int i=0;i<data.实体.Length;i++)if(data.实体[i]!=0)occupiedIndices.Add(i);
            data.实体索引=occupiedIndices.ToArray();data.实体数量=occupiedIndices.Count;
            data.分块坐标=coords.ToArray();data.预烘焙网格=meshes.ToArray();
            var detail=new Texture2D(64,64,TextureFormat.RGB24,true) { name="StoneFineDetail",wrapMode=TextureWrapMode.Repeat };
            var pixels=new Color[64*64];var random=new System.Random(197);
            for(int i=0;i<pixels.Length;i++) { float v=.63f+(float)random.NextDouble()*.16f;pixels[i]=new Color(v,v,v); }
            detail.SetPixels(pixels);detail.Apply();
            var sourceMat=model.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;
            bool diffuse=sourceMat && sourceMat.shader && sourceMat.shader.name.IndexOf("Diffuse",StringComparison.OrdinalIgnoreCase)>=0;
            var shader=Shader.Find(diffuse?"Cultivation/VoxelSurfaceDiffuse":"Cultivation/VoxelSurface");
            if(!shader) throw new InvalidOperationException("Voxel shader has not compiled.");
            var mat=new Material(shader) { name="VoxelSurface" };mat.SetTexture("_Detail",detail);data.材质=mat;
            if(sourceMat && sourceMat.HasProperty("_Glossiness"))mat.SetFloat("_Smoothness",sourceMat.GetFloat("_Glossiness"));
            if(sourceMat && sourceMat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",sourceMat.GetFloat("_Metallic"));
            if(projectUV){mat.SetTexture("_SourceTex",sourceTexture);mat.SetColor("_SourceTint",triangles[0].tint);mat.SetFloat("_UseSourceUV",1);}
            string assetPath=folder+"/Volume.asset";
            AssetDatabase.CreateAsset(data,assetPath);
            foreach(var mesh in meshes) AssetDatabase.AddObjectToAsset(mesh,data);
            AssetDatabase.AddObjectToAsset(detail,data);AssetDatabase.AddObjectToAsset(mat,data);
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
            if(!independentScene)
            {
                Debug.Log($"VOXEL_MAP_BAKE source={data.来源} cells={length} solid={data.实体数量} chunks={coords.Count} seconds={watch.Elapsed.TotalSeconds:F2} asset={assetPath}");
                return assetPath;
            }
            // The new scene is created only after a complete successful bake.
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var reference=(GameObject)PrefabUtility.InstantiatePrefab(model,scene);
            reference.name="原模型（对照）";
            reference.transform.position=new Vector3(-3.8f,0,0)-anchor*normalize;
            reference.transform.rotation=Quaternion.identity; reference.transform.localScale=Vector3.one*normalize;
            var voxel=new GameObject("体素模型（左键破坏）");voxel.transform.position=new Vector3(3.8f,0,0);
            var destructible=voxel.AddComponent<VoxelDestructible>();destructible.数据=data;
            destructible.初始化();
            var cameraObject=new GameObject("PrototypeCamera");var camera=cameraObject.AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.17f);camera.nearClipPlane=.05f;camera.farClipPlane=150;
            cameraObject.tag="MainCamera";
            var demo=cameraObject.AddComponent<VoxelPrototypeDemo>();demo.目标=destructible;demo.观察相机=camera;demo.观察中心=new Vector3(0,bounds.size.y*.45f,0);demo.观察距离=17;
            camera.transform.position=new Vector3(8,9,-17);camera.transform.LookAt(demo.观察中心);
            var lightObject=new GameObject("PrototypeLight");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.43f,.45f);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="对照地台";ground.transform.position=new Vector3(0,-.22f,0);ground.transform.localScale=new Vector3(22,.3f,14);
            var groundMat=new Material(Shader.Find("Standard")){name="Ground"};groundMat.color=new Color(.22f,.27f,.26f);
            AssetDatabase.CreateAsset(groundMat,folder+"/Ground.mat");ground.GetComponent<MeshRenderer>().sharedMaterial=groundMat;
            string scenePath=folder+"/VoxelPrototype.unity";EditorSceneManager.SaveScene(scene,scenePath);
            Debug.Log($"VOXEL_BAKE source={data.来源} cells={length} solid={data.实体数量} chunks={coords.Count} seconds={watch.Elapsed.TotalSeconds:F2} scene={scenePath}");
            Selection.activeGameObject=voxel;
            return scenePath;
        }
        finally { DestroyImmediate(instance);foreach(var t in textures.Values) DestroyImmediate(t); }
    }

    static List<Triangle> 提取(GameObject root,Dictionary<Texture,Texture2D> textures,out Bounds bounds)
    {
        var result=new List<Triangle>(); bounds=new Bounds();bool first=true;
        foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
            if(!mesh || !renderer || !renderer.enabled) continue;
            var matrix=root.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            using(var array=MeshUtility.AcquireReadOnlyMeshData(mesh))
            {
                var md=array[0];
                using(var vertices=new NativeArray<Vector3>(md.vertexCount,Allocator.Temp))
                using(var uv=new NativeArray<Vector2>(md.vertexCount,Allocator.Temp))
                using(var normals=new NativeArray<Vector3>(md.vertexCount,Allocator.Temp))
                {
                    md.GetVertices(vertices);
                    if(md.HasVertexAttribute(VertexAttribute.Normal))md.GetNormals(normals);
                    if(md.HasVertexAttribute(VertexAttribute.TexCoord0)) md.GetUVs(0,uv);
                    var materials=renderer.sharedMaterials;
                    for(int s=0;s<md.subMeshCount;s++)
                    {
                        var sub=md.GetSubMesh(s);if(sub.topology!=MeshTopology.Triangles) continue;
                        var material=s<materials.Length?materials[s]:null;
                        Texture texture=material && material.HasProperty("_MainTex")?material.mainTexture:null;
                        if(texture && !textures.ContainsKey(texture)) textures.Add(texture,可读贴图(texture));
                        using(var indices=new NativeArray<int>(sub.indexCount,Allocator.Temp))
                        {
                            md.GetIndices(indices,s,true);
                            for(int i=0;i+2<indices.Length;i+=3)
                            {
                                int ia=indices[i],ib=indices[i+1],ic=indices[i+2];
                                var t=new Triangle { a=matrix.MultiplyPoint3x4(vertices[ia]),b=matrix.MultiplyPoint3x4(vertices[ib]),c=matrix.MultiplyPoint3x4(vertices[ic]),
                                    ua=uv[ia],ub=uv[ib],uc=uv[ic],tint=material && material.HasProperty("_Color")?material.color:Color.gray,
                                    na=matrix.inverse.transpose.MultiplyVector(normals[ia]).normalized,nb=matrix.inverse.transpose.MultiplyVector(normals[ib]).normalized,nc=matrix.inverse.transpose.MultiplyVector(normals[ic]).normalized,
                                    texture=texture?textures[texture]:null,sourceTexture=texture,scale=texture?material.mainTextureScale:Vector2.one,offset=texture?material.mainTextureOffset:Vector2.zero };
                                result.Add(t);
                                if(first) { bounds=new Bounds(t.a,Vector3.zero);first=false; }
                                bounds.Encapsulate(t.a);bounds.Encapsulate(t.b);bounds.Encapsulate(t.c);
                            }
                        }
                    }
                }
            }
        }
        return result;
    }

    static void 封闭端口(List<Triangle> triangles,Bounds bounds)
    {
        float epsilon=Mathf.Max(bounds.size.magnitude*1e-6f,1e-10f);
        var vertices=new Dictionary<Vector3Int,int>();var positions=new List<Vector3>();
        var edges=new Dictionary<(int,int),int>();
        Func<Vector3,int> vertex=p=>{var key=Vector3Int.RoundToInt((p-bounds.center)/epsilon);if(vertices.TryGetValue(key,out int i))return i;i=positions.Count;vertices.Add(key,i);positions.Add(p);return i;};
        Action<int,int> edge=(a,b)=>{var key=a<b?(a,b):(b,a);edges.TryGetValue(key,out int n);edges[key]=n+1;};
        foreach(var t in triangles){int a=vertex(t.a),b=vertex(t.b),c=vertex(t.c);edge(a,b);edge(b,c);edge(c,a);}
        var neighbours=new Dictionary<int,List<int>>();
        Action<int,int> link=(a,b)=>{if(!neighbours.TryGetValue(a,out var list)){list=new List<int>();neighbours.Add(a,list);}list.Add(b);};
        foreach(var pair in edges)if(pair.Value==1){link(pair.Key.Item1,pair.Key.Item2);link(pair.Key.Item2,pair.Key.Item1);}
        var visited=new HashSet<int>();var example=triangles[0];int caps=0;
        foreach(var first in neighbours.Keys)
        {
            if(visited.Contains(first) || neighbours[first].Count!=2)continue;
            var loop=new List<int>();int previous=-1,current=first;bool closed=false;
            for(int guard=0;guard<=neighbours.Count;guard++)
            {
                if(current==first && loop.Count>0){closed=true;break;}
                if(visited.Contains(current) || !neighbours.TryGetValue(current,out var next) || next.Count!=2)break;
                visited.Add(current);loop.Add(current);int following=next[0]==previous?next[1]:next[0];previous=current;current=following;
            }
            if(!closed || loop.Count<3)continue;
            var center=Vector3.zero;foreach(int i in loop)center+=positions[i];center/=loop.Count;
            for(int n=0;n<loop.Count;n++){var t=example;t.a=positions[loop[n]];t.b=positions[loop[(n+1)%loop.Count]];t.c=center;triangles.Add(t);}caps++;
        }
        Debug.Log($"VOXEL_CLOSED_BOUNDARIES loops={caps}");
    }

    static Texture2D 可读贴图(Texture source)
    {
        int width=Mathf.Min(512,source.width),height=Mathf.Min(512,source.height);
        var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        try { Graphics.Blit(source,rt);RenderTexture.active=rt;var copy=new Texture2D(width,height,TextureFormat.RGBA32,false);copy.ReadPixels(new Rect(0,0,width,height),0,0);copy.Apply();copy.wrapMode=source.wrapMode;return copy; }
        finally { RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt); }
    }

    static void 体素化(Triangle t,VoxelVolumeAsset data)
    {
        float step=data.格子边长;
        var min=Vector3Int.Max(Vector3Int.zero,Vector3Int.FloorToInt((Vector3.Min(t.a,Vector3.Min(t.b,t.c))-data.原点)/step));
        var max=Vector3Int.Min(data.尺寸-Vector3Int.one,Vector3Int.FloorToInt((Vector3.Max(t.a,Vector3.Max(t.b,t.c))-data.原点)/step));
        for(int z=min.z;z<=max.z;z++) for(int y=min.y;y<=max.y;y++) for(int x=min.x;x<=max.x;x++)
        {
            var p=data.原点+new Vector3(x+.5f,y+.5f,z+.5f)*step;
            if(!相交(t.a-p,t.b-p,t.c-p,step*.501f)) continue;
            int index=data.索引(x,y,z); data.实体[index]=1;
            var v0=t.b-t.a;var v1=t.c-t.a;var v2=p-t.a;
            float d00=Vector3.Dot(v0,v0),d01=Vector3.Dot(v0,v1),d11=Vector3.Dot(v1,v1),d20=Vector3.Dot(v2,v0),d21=Vector3.Dot(v2,v1);
            float den=d00*d11-d01*d01;
            float b=Mathf.Abs(den)>.00000001f?(d11*d20-d01*d21)/den:0;
            float c=Mathf.Abs(den)>.00000001f?(d00*d21-d01*d20)/den:0;
            float a=Mathf.Max(0,1-b-c);b=Mathf.Max(0,b);c=Mathf.Max(0,c);float sum=a+b+c;
            var uv=Vector2.Scale((t.ua*a+t.ub*b+t.uc*c)/Mathf.Max(sum,.0001f),t.scale)+t.offset;
            data.颜色[index]=t.tint*(t.texture?t.texture.GetPixelBilinear(uv.x,uv.y):Color.white);
        }
    }

    static bool 相交(Vector3 a,Vector3 b,Vector3 c,float half)
    {
        if(分离(Vector3.right,a,b,c,half)||分离(Vector3.up,a,b,c,half)||分离(Vector3.forward,a,b,c,half)) return false;
        var e0=b-a;var e1=c-b;var e2=a-c;
        if(分离(Vector3.Cross(e0,e1),a,b,c,half)) return false;
        return !边分离(e0,a,b,c,half)&&!边分离(e1,a,b,c,half)&&!边分离(e2,a,b,c,half);
    }
    static bool 边分离(Vector3 edge,Vector3 a,Vector3 b,Vector3 c,float half) =>
        分离(Vector3.Cross(edge,Vector3.right),a,b,c,half)||分离(Vector3.Cross(edge,Vector3.up),a,b,c,half)||分离(Vector3.Cross(edge,Vector3.forward),a,b,c,half);
    static bool 分离(Vector3 axis,Vector3 a,Vector3 b,Vector3 c,float half)
    {
        float pa=Vector3.Dot(axis,a),pb=Vector3.Dot(axis,b),pc=Vector3.Dot(axis,c);
        float r=half*(Mathf.Abs(axis.x)+Mathf.Abs(axis.y)+Mathf.Abs(axis.z));
        return Mathf.Min(pa,Mathf.Min(pb,pc))>r || Mathf.Max(pa,Mathf.Max(pb,pc)) < -r;
    }

    static void 填充(VoxelVolumeAsset data)
    {
        var outside=new bool[data.实体.Length];var queue=new Queue<Vector3Int>();
        queue.Enqueue(Vector3Int.zero);outside[0]=true;
        var dirs=new[]{Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        while(queue.Count>0)
        {
            var p=queue.Dequeue();foreach(var d in dirs)
            {
                var n=p+d;if(!data.范围内(n.x,n.y,n.z)) continue;int i=data.索引(n.x,n.y,n.z);
                if(outside[i] || data.实体[i]!=0) continue;outside[i]=true;queue.Enqueue(n);
            }
        }
        // Interior has a distinct cut-surface tint, rather than stretching the exterior UVs into holes.
        Color average=Color.black;int count=0;
        for(int i=0;i<data.实体.Length;i++) if(data.实体[i]!=0) { average+=(Color)data.颜色[i];count++; }
        average=count>0?average/count:Color.gray;
        Color32 inside=Color.Lerp(average,new Color(.35f,.30f,.24f),.25f);
        for(int i=0;i<data.实体.Length;i++) if(!outside[i] && data.实体[i]==0) { data.实体[i]=1;data.颜色[i]=inside; }
    }
}
