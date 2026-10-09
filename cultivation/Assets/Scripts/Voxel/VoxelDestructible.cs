using System.Collections.Generic;
using UnityEngine;

/// <summary>Per-chunk copy-on-write meshes; updates include adjacent chunk borders.</summary>
public sealed class VoxelDestructible : MonoBehaviour
{
    public VoxelVolumeAsset 数据;
    [Min(1)] public int 每帧最多更新块 = 2;
    [Min(.1f)] public float 每帧软预算毫秒 = 4;
    public bool 自动更新 = true;
    // Ground has no pre-baked chunks. Build directly from its already carved field once.
    public bool 动态初始网格;
    // Adjacent ground patches publish together after all their meshes/colliders are ready.
    public bool 暂缓提交;
    public PhysicMaterial 碰撞材质;
    public int 剩余体素 { get; private set; }
    public int 本次移除 { get; private set; }
    public int 本次影响块 { get; private set; }
    public int 累计重建块 { get; private set; }
    public int 等待块 => pending.Count;
    public int 待提交块 => staged.Count;
    public double 最近批次毫秒 { get; private set; }
    public double 最长批次毫秒 { get; private set; }
    public double 最近修改毫秒 { get; private set; }
    public double 最近网格毫秒 { get; private set; }
    public double 最近碰撞毫秒 { get; private set; }
    byte[] cells;
    float[] distances;
    int buildIndex;
    readonly HashSet<Vector3Int> validChunks=new HashSet<Vector3Int>();
    readonly HashSet<Vector3Int> initialDirty=new HashSet<Vector3Int>();
    public bool 初始化完成=>数据 && buildIndex>=数据.分块坐标.Length;
    readonly Dictionary<Vector3Int, GameObject> chunks = new Dictionary<Vector3Int, GameObject>();
    readonly Dictionary<Vector3Int, Mesh> owned = new Dictionary<Vector3Int, Mesh>();
    [SerializeField] List<Mesh> runtimeMeshes = new List<Mesh>();
    readonly HashSet<Vector3Int> dirty = new HashSet<Vector3Int>();
    readonly Queue<Vector3Int> pending = new Queue<Vector3Int>();
    readonly Dictionary<Vector3Int,Mesh> staged=new Dictionary<Vector3Int,Mesh>();
    const MeshColliderCookingOptions Cooking=MeshColliderCookingOptions.CookForFasterSimulation|MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices|MeshColliderCookingOptions.UseFastMidphase;

    void Awake() { if (数据 && cells==null) 初始化(); }
    void OnEnable() { if(数据 && cells==null)初始化(); }

    public void 初始化()
    {
        开始初始化();while(!初始化完成 && 数据)继续初始化();
    }
    internal void 开始初始化()
    {
        清理();
        if (!数据 || 数据.实体 == null) return;
        cells = (byte[])数据.实体.Clone();
        distances=数据.距离!=null && 数据.距离.Length==cells.Length?(float[])数据.距离.Clone():null;
        剩余体素 = 数据.实体数量;
        buildIndex=0;validChunks.Clear();initialDirty.Clear();foreach(var key in 数据.分块坐标)validChunks.Add(key);
        累计重建块=0; 本次移除=0; 本次影响块=0; 最长批次毫秒=0; 最近批次毫秒=0; 最近修改毫秒=0;
    }
    internal int 继续初始化()
    {
        if(初始化完成 || cells==null)return 0;
        var timer=System.Diagnostics.Stopwatch.StartNew();int count=0;
        do
        {
            int i=buildIndex++;var key=数据.分块坐标[i];bool changed=initialDirty.Remove(key);
            var go = new GameObject("Chunk_" + 数据.分块坐标[i]);
            go.layer=gameObject.layer;
            go.transform.SetParent(transform,false);
            var filter = go.AddComponent<MeshFilter>();
            bool generated=动态初始网格||changed;
            filter.sharedMesh=generated?VoxelChunkMesher.生成(数据,cells,key,distances):数据.预烘焙网格[i];
            if(generated){owned[key]=filter.sharedMesh;runtimeMeshes.Add(filter.sharedMesh);}
            go.AddComponent<MeshRenderer>().sharedMaterial = 数据.材质;
            var collider = go.AddComponent<MeshCollider>();
            collider.cookingOptions=Cooking;
            collider.sharedMaterial=碰撞材质;
            collider.enabled=filter.sharedMesh.vertexCount>0;
            collider.sharedMesh=filter.sharedMesh.vertexCount>0?filter.sharedMesh:null;
            chunks.Add(数据.分块坐标[i],go);
            count++;if(generated)break;
        }while(!初始化完成 && count<4 && timer.Elapsed.TotalMilliseconds<每帧软预算毫秒);
        return 1;
    }

    public int 破坏球(Vector3 worldCenter, float worldRadius)
        => 修改球(worldCenter,worldRadius,false);
    internal int 准备破坏球(Vector3 worldCenter,float worldRadius)
        => 修改球(worldCenter,worldRadius,true);
    int 修改球(Vector3 worldCenter,float worldRadius,bool preparing)
        => 修改椭球(worldCenter,worldRadius,worldRadius,preparing);
    internal int 准备破坏椭球(Vector3 worldCenter,float worldRadius,float worldDepth)
        => 修改椭球(worldCenter,worldRadius,worldDepth,true);
    int 修改椭球(Vector3 worldCenter,float worldRadius,float worldDepth,bool preparing)
    {
        if(cells == null || worldRadius <= 0 || worldDepth <= 0 || (!preparing && !isActiveAndEnabled)) return 0;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        // Conservative local AABB, followed by exact world-space sphere test; supports scaled instances.
        Vector3 local = transform.InverseTransformPoint(worldCenter);
        var inv = transform.worldToLocalMatrix;
        float band=数据.格子边长*4;
        var scale=transform.lossyScale;
        float maxScale=Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
        float halo=distances!=null?band*maxScale:0;
        Vector3 worldExtent=new Vector3(worldRadius+halo,worldDepth+halo,worldRadius+halo);
        Vector3 extent = new Vector3(
            Mathf.Abs(inv.m00)*worldExtent.x+Mathf.Abs(inv.m01)*worldExtent.y+Mathf.Abs(inv.m02)*worldExtent.z,
            Mathf.Abs(inv.m10)*worldExtent.x+Mathf.Abs(inv.m11)*worldExtent.y+Mathf.Abs(inv.m12)*worldExtent.z,
            Mathf.Abs(inv.m20)*worldExtent.x+Mathf.Abs(inv.m21)*worldExtent.y+Mathf.Abs(inv.m22)*worldExtent.z);
        Vector3 a = (local-extent-数据.原点)/数据.格子边长;
        Vector3 b = (local+extent-数据.原点)/数据.格子边长;
        var min = Vector3Int.Max(Vector3Int.zero,Vector3Int.FloorToInt(a));
        var max = Vector3Int.Min(数据.尺寸-Vector3Int.one,Vector3Int.FloorToInt(b));
        var touched = new HashSet<Vector3Int>();
        int removed=0;
        for(int z=min.z;z<=max.z;z++) for(int y=min.y;y<=max.y;y++) for(int x=min.x;x<=max.x;x++)
        {
            int index=数据.索引(x,y,z);
            var p=数据.原点+new Vector3(x+.5f,y+.5f,z+.5f)*数据.格子边长;
            if(数据.保护下界!=null && 数据.保护下界.Length==数据.尺寸.x*数据.尺寸.z && p.y<=数据.保护下界[x+数据.尺寸.x*z])continue;
            var delta=transform.TransformPoint(p)-worldCenter;
            float toolDistance;
            if(worldDepth==worldRadius)toolDistance=delta.magnitude-worldRadius;
            else{
                // Ellipsoid distance estimate: preserves horizontal radius and vertical depth independently.
                var q=new Vector3(delta.x/worldRadius,delta.y/worldDepth,delta.z/worldRadius);
                float k0=q.magnitude,k1=new Vector3(q.x/worldRadius,q.y/worldDepth,q.z/worldRadius).magnitude;
                toolDistance=k1>1e-8f?k0*(k0-1)/k1:-Mathf.Min(worldRadius,worldDepth);
            }
            if(distances!=null)
            {
                float tool=Mathf.Clamp(toolDistance/Mathf.Max(maxScale,.0001f),-band,band);
                float next=Mathf.Min(distances[index],tool);
                if(next>=distances[index]) continue;
                distances[index]=next;
                if(cells[index]!=0 && next<=0) {cells[index]=0;removed++;}
            }
            else
            {
                if(cells[index]==0 || toolDistance>0) continue;
                cells[index]=0;removed++;
            }
            if(数据.平滑表面)
            {
                // Density filtering, dual cells and normals read a three-sample halo, including diagonals.
                int size=数据.分块边长;
                var lo=Vector3Int.Max(Vector3Int.zero,new Vector3Int(x-3,y-3,z-3));
                var hi=Vector3Int.Min(数据.尺寸-Vector3Int.one,new Vector3Int(x+3,y+3,z+3));
                for(int cz=lo.z/size;cz<=hi.z/size;cz++)for(int cy=lo.y/size;cy<=hi.y/size;cy++)for(int cx=lo.x/size;cx<=hi.x/size;cx++)
                {var key=new Vector3Int(cx,cy,cz);if(validChunks.Contains(key))touched.Add(key);}
                continue;
            }
            标记(x,y,z,touched);
            标记(x+1,y,z,touched); 标记(x-1,y,z,touched);
            标记(x,y+1,z,touched); 标记(x,y-1,z,touched);
            标记(x,y,z+1,touched); 标记(x,y,z-1,touched);
        }
        foreach(var key in touched)排队(key);
        本次移除=removed; 本次影响块=touched.Count; 剩余体素-=removed;
        最近修改毫秒=timer.Elapsed.TotalMilliseconds;
        return removed;
    }

    void 标记(int x,int y,int z,HashSet<Vector3Int> touched)
    {
        if(!数据.范围内(x,y,z)) return;
        var key=new Vector3Int(x/数据.分块边长,y/数据.分块边长,z/数据.分块边长);
        if(validChunks.Contains(key)) touched.Add(key);
    }
    void 排队(Vector3Int key)
    {
        if(!chunks.ContainsKey(key)){initialDirty.Add(key);return;}
        // A second strike can invalidate prepared geometry before publication.
        if(staged.TryGetValue(key,out var stale)){释放(stale);staged.Remove(key);}
        if(dirty.Add(key))pending.Enqueue(key);
    }

    internal void 移除断枝(List<int> indices,int start,int count)
    {
        var touched=new HashSet<Vector3Int>();int size=数据.分块边长;
        for(int n=start;n<start+count;n++)
        {
            int i=indices[n];if(cells[i]==0)continue;
            cells[i]=0;剩余体素--;if(distances!=null)distances[i]=-数据.格子边长;
            int x=i%数据.尺寸.x,y=(i/数据.尺寸.x)%数据.尺寸.y,z=i/(数据.尺寸.x*数据.尺寸.y);
            var lo=Vector3Int.Max(Vector3Int.zero,new Vector3Int(x-3,y-3,z-3));
            var hi=Vector3Int.Min(数据.尺寸-Vector3Int.one,new Vector3Int(x+3,y+3,z+3));
            for(int cz=lo.z/size;cz<=hi.z/size;cz++)for(int cy=lo.y/size;cy<=hi.y/size;cy++)for(int cx=lo.x/size;cx<=hi.x/size;cx++)
            {var key=new Vector3Int(cx,cy,cz);if(validChunks.Contains(key))touched.Add(key);}
        }
        foreach(var key in touched)排队(key);
    }

    void Update() { if(自动更新)更新局部(); }

    public int 更新局部()
    {
        if(pending.Count==0){if(!暂缓提交 && staged.Count>0){提交更新();return 1;}return 0;}
        var timer=System.Diagnostics.Stopwatch.StartNew();
        最近网格毫秒=0;最近碰撞毫秒=0;
        int count=0;
        while(pending.Count>0 && count<每帧最多更新块)
        {
            var key=pending.Peek();
            var go=chunks[key];
            var stage=System.Diagnostics.Stopwatch.StartNew();
            var mesh=VoxelChunkMesher.生成(数据,cells,key,distances);
            最近网格毫秒+=stage.Elapsed.TotalMilliseconds;stage.Restart();
            // Prepare physics while the old visible/collidable chunk remains intact.
            if(mesh.vertexCount>0)Physics.BakeMesh(mesh.GetInstanceID(),false,Cooking);
            最近碰撞毫秒+=stage.Elapsed.TotalMilliseconds;
            staged.Add(key,mesh);pending.Dequeue();dirty.Remove(key);
            count++; 累计重建块++;
            if(timer.Elapsed.TotalMilliseconds>=每帧软预算毫秒) break;
        }
        if(pending.Count==0 && !暂缓提交)提交更新();
        最近批次毫秒=timer.Elapsed.TotalMilliseconds;
        最长批次毫秒=System.Math.Max(最长批次毫秒,最近批次毫秒);
        return count;
    }

    public void 提交更新()
    {
        if(pending.Count>0)return;
        foreach(var pair in staged){
            var key=pair.Key;var mesh=pair.Value;var go=chunks[key];var collider=go.GetComponent<MeshCollider>();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            collider.sharedMesh=null;collider.enabled=mesh.vertexCount>0;
            if(mesh.vertexCount>0)collider.sharedMesh=mesh;
            if(owned.TryGetValue(key,out var previous)){runtimeMeshes.Remove(previous);释放(previous);}
            owned[key]=mesh;runtimeMeshes.Add(mesh);
        }
        staged.Clear();
    }

    public Mesh 取块网格(Vector3Int key) => chunks[key].GetComponent<MeshFilter>().sharedMesh;
    public bool 取实体(int x,int y,int z) => cells!=null && 数据.范围内(x,y,z) && cells[数据.索引(x,y,z)]!=0;
    public float[] 复制距离() => distances!=null?(float[])distances.Clone():null;

    void 清理()
    {
        var meshes=new HashSet<Mesh>(runtimeMeshes);
        foreach(var mesh in owned.Values)meshes.Add(mesh);
        foreach(var mesh in staged.Values)meshes.Add(mesh);
        // Editor preview chunks are serialized too; remove them on first Play initialization.
        for(int i=transform.childCount-1;i>=0;i--)
        {
            var child=transform.GetChild(i).gameObject;
            if(!child.name.StartsWith("Chunk_",System.StringComparison.Ordinal)) continue;
            child.SetActive(false); 释放(child);
        }
        foreach(var mesh in meshes) if(mesh)释放(mesh);
        chunks.Clear(); owned.Clear(); pending.Clear(); dirty.Clear();staged.Clear();
        runtimeMeshes.Clear();
    }
    static void 释放(Object obj) { if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
    void OnDestroy() { 清理(); }
}
