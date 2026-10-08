using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Whole-terrain coverage with lazy, thick solid patches. Original TerrainData is never modified.</summary>
public sealed class VoxelTerrainReplacement : MonoBehaviour
{
    struct Cut {public Vector3 center;public float radius;}
    sealed class Patch
    {
        public Vector2Int key;
        public VoxelVolumeAsset data;
        public float[] heights;
        public readonly List<Cut> cuts=new List<Cut>();
        public Vector3Int grid;
        public int column,cutIndex;
        public VoxelDestructible body;
        public bool ready;
    }
    [SerializeField] Terrain terrain;
    [SerializeField] TerrainCollider terrainCollider;
    [SerializeField] VoxelTerrainSettings settings;
    [SerializeField] TerrainData original,modified;
    readonly Dictionary<Vector2Int,Patch> patches=new Dictionary<Vector2Int,Patch>();
    readonly Queue<Patch> building=new Queue<Patch>();
    readonly List<Patch> prepared=new List<Patch>();
    readonly Dictionary<Vector2Int,List<Cut>> history=new Dictionary<Vector2Int,List<Cut>>();
    [SerializeField] bool active=true;
    public int 活动地块数=>patches.Count;
    public int 待建地块数=>building.Count;
    public bool 等待更新
    {
        get {if(building.Count>0||prepared.Count>0)return true;foreach(var patch in patches.Values)if(patch.body && (patch.body.等待块>0||patch.body.待提交块>0))return true;return false;}
    }
    public double 最近更新毫秒 {get;private set;}
    public string 最近阶段 {get;private set;}
    public TerrainData 原地形=>original;
    public TerrainData 修改地形=>modified;
    public bool 可破坏(Collider collider)=>collider==terrainCollider || (collider && collider.GetComponentInParent<Terrain>()==terrain);
    float step,width;
    public bool 初始化(Terrain source,VoxelTerrainSettings config)
    {
        if(!source || !config || source.terrainData!=config.源地形 ||
           (source.transform.position-config.预期位置).sqrMagnitude>.0004f ||
           Quaternion.Angle(source.transform.rotation,Quaternion.identity)>.01f ||
           (source.transform.lossyScale-Vector3.one).sqrMagnitude>.0001f)return false;
        terrain=source;terrainCollider=source.GetComponent<TerrainCollider>();settings=config;original=source.terrainData;
        step=original.size.x/original.holesResolution;width=step*settings.每区孔洞格数;
        if(Mathf.Abs(original.size.x-original.size.z)>.01f || config.实体厚度<=config.底部承托厚度 || !config.地块材质)return false;
        预热();
        return true;
    }
    void 预热()
    {
        if(!modified){modified=Instantiate(original);modified.name=original.name+"_RuntimeVoxelHoles";modified.enableHolesTextureCompression=false;}
        var data=ScriptableObject.CreateInstance<VoxelVolumeAsset>();
        data.尺寸=new Vector3Int(6,6,6);data.格子边长=step;data.分块边长=8;data.距离=new float[216];data.实体=new byte[216];data.颜色=new Color32[216];
        data.限制水平范围=true;data.水平最大=Vector2.one*step*6;data.地形源=original;
        for(int z=0;z<6;z++)for(int y=0;y<6;y++)for(int x=0;x<6;x++)
        {int i=data.索引(x,y,z);data.距离[i]=(3-y)*step;data.实体[i]=(byte)(y<3?1:0);data.颜色[i]=Color.white;}
        var mesh=VoxelChunkMesher.生成(data,data.实体,Vector3Int.zero);Destroy(mesh);Destroy(data);
    }
    public void 破坏球(Vector3 worldCenter,float worldRadius)
    {
        var task=分步破坏球(worldCenter,worldRadius);while(task.MoveNext()){}
    }
    public System.Collections.IEnumerator 分步破坏球(Vector3 worldCenter,float worldRadius)
    {
        if(!active || !original || worldRadius<=0)yield break;
        var local=terrain.transform.InverseTransformPoint(worldCenter);
        // Include the shared sampling halo so neighbouring patch normals receive the same cut.
        float extent=worldRadius+step*4;
        int count=Mathf.CeilToInt(original.size.x/width);
        int minX=Mathf.Max(0,Mathf.FloorToInt((local.x-extent)/width)),maxX=Mathf.Min(count-1,Mathf.FloorToInt((local.x+extent)/width));
        int minZ=Mathf.Max(0,Mathf.FloorToInt((local.z-extent)/width)),maxZ=Mathf.Min(count-1,Mathf.FloorToInt((local.z+extent)/width));
        for(int z=minZ;z<=maxZ;z++)for(int x=minX;x<=maxX;x++)
        {
            var key=new Vector2Int(x,z);
            var cut=new Cut {center=worldCenter,radius=worldRadius};
            if(!history.TryGetValue(key,out var commands)){commands=new List<Cut>();history.Add(key,commands);}
            commands.Add(cut);
            bool created=false;
            if(!patches.TryGetValue(key,out var patch))
            {
                // A halo-only overlap need not allocate an otherwise untouched patch.
                float cx=Mathf.Clamp(local.x,x*width,(x+1)*width),cz=Mathf.Clamp(local.z,z*width,(z+1)*width);
                if((new Vector2(cx,cz)-new Vector2(local.x,local.z)).sqrMagnitude>worldRadius*worldRadius)continue;
                float height=original.GetInterpolatedHeight(cx/original.size.x,cz/original.size.z);
                if(local.y-worldRadius>height+step || local.y+worldRadius<height-settings.实体厚度)continue;
                patch=new Patch {key=key};patch.cuts.AddRange(commands);patches.Add(key,patch);building.Enqueue(patch);created=true;
            }
            if(patch.body)patch.body.准备破坏球(worldCenter,worldRadius);
            else if(!created)patch.cuts.Add(cut);
            yield return null;
        }
    }
    // Called by the map's shared scheduler; one bounded work unit per invocation.
    public int 更新局部(bool allowPublish=true)
    {
        if(!active || !original)return 0;
        var timer=System.Diagnostics.Stopwatch.StartNew();int work=0;
        if(building.Count>0)
        {
            var patch=building.Peek();
            if(!patch.data){最近阶段="准备";准备(patch);}
            else if(patch.column<patch.data.尺寸.x*patch.data.尺寸.z){最近阶段="采样";采样(patch,64);}
            else if(!patch.body){最近阶段="创建对象";建立实体(patch);}
            else if(patch.cutIndex<patch.cuts.Count){最近阶段="初次切削";var cut=patch.cuts[patch.cutIndex++];patch.body.准备破坏球(cut.center,cut.radius);}
            else if(!patch.body.初始化完成){最近阶段="切削网格";patch.body.继续初始化();}
            else if(patch.body.等待块>0){最近阶段="破坏更新";patch.body.更新局部();}
            else {最近阶段="等待统一切换";prepared.Add(patch);building.Dequeue();}
            work=1;
        }
        else
        {
            foreach(var patch in patches.Values)if(patch.body && patch.body.等待块>0)
            {patch.body.更新局部();work=1;break;}
        }
        if(allowPublish && building.Count==0){
            bool pending=false,changed=prepared.Count>0;
            foreach(var patch in patches.Values)if(patch.body){pending|=patch.body.等待块>0;changed|=patch.body.待提交块>0;}
            if(!pending && changed){最近阶段="统一提交";提交地块();work=1;}
        }
        最近更新毫秒=timer.Elapsed.TotalMilliseconds;return work;
    }
    void 准备(Patch patch)
    {
        int size=settings.每区孔洞格数+4;float minHeight=float.PositiveInfinity,maxHeight=float.NegativeInfinity;
        patch.heights=new float[size*size];
        for(int z=0;z<size;z++)for(int x=0;x<size;x++)
        {
            float px=patch.key.x*width+(x-1.5f)*step,pz=patch.key.y*width+(z-1.5f)*step;
            float h=original.GetInterpolatedHeight(Mathf.Clamp01(px/original.size.x),Mathf.Clamp01(pz/original.size.z));
            patch.heights[x+size*z]=h;minHeight=Mathf.Min(minHeight,h);maxHeight=Mathf.Max(maxHeight,h);
        }
        float bottom=Mathf.Floor((minHeight-settings.实体厚度)/step)*step-step*2;
        var data=ScriptableObject.CreateInstance<VoxelVolumeAsset>();data.name="RuntimeGround_"+patch.key;
        data.格子边长=step;data.分块边长=8;data.平滑表面=true;
        data.原点=new Vector3(patch.key.x*width-step*2,bottom,patch.key.y*width-step*2);
        data.尺寸=new Vector3Int(size,Mathf.CeilToInt((maxHeight-bottom)/step)+3,size);
        int length=checked(data.尺寸.x*data.尺寸.y*data.尺寸.z);
        data.实体=new byte[length];data.距离=new float[length];data.颜色=new Color32[length];data.保护下界=new float[size*size];
        data.限制水平范围=true;data.水平最小=new Vector2(patch.key.x*width,patch.key.y*width);
        data.水平最大=Vector2.Min(data.水平最小+Vector2.one*width,new Vector2(original.size.x,original.size.z));
        data.地形源=original;data.材质=settings.地块材质;patch.data=data;
        patch.grid=Vector3Int.CeilToInt((Vector3)data.尺寸/data.分块边长);
    }
    void 采样(Patch patch,int columns)
    {
        var data=patch.data;int size=data.尺寸.x;
        for(int end=Mathf.Min(size*size,patch.column+columns);patch.column<end;patch.column++)
        {
            int x=patch.column%size,z=patch.column/size;float height=patch.heights[patch.column];
            data.保护下界[patch.column]=height-settings.实体厚度+settings.底部承托厚度;
            for(int y=0;y<data.尺寸.y;y++)
            {
                int i=data.索引(x,y,z);float py=data.原点.y+(y+.5f)*step;
                float distance=Mathf.Min(height-py,py-(height-settings.实体厚度));
                data.距离[i]=Mathf.Clamp(distance,-step*4,step*4);data.实体[i]=(byte)(distance>0?1:0);
                if(distance>0)data.实体数量++;data.颜色[i]=new Color32(77,63,43,255);
            }
        }
    }
    void 建立实体(Patch patch)
    {
        var data=patch.data;int count=patch.grid.x*patch.grid.y*patch.grid.z;
        data.分块坐标=new Vector3Int[count];data.预烘焙网格=new Mesh[count];
        for(int i=0;i<count;i++)data.分块坐标[i]=new Vector3Int(i%patch.grid.x,(i/patch.grid.x)%patch.grid.y,i/(patch.grid.x*patch.grid.y));
        var go=new GameObject("VoxelGround_"+patch.key);go.SetActive(false);go.layer=terrain.gameObject.layer;go.transform.SetParent(terrain.transform,false);
        patch.body=go.AddComponent<VoxelDestructible>();patch.body.数据=data;patch.body.自动更新=false;patch.body.每帧最多更新块=1;patch.body.动态初始网格=true;patch.body.暂缓提交=true;
        if(terrainCollider)patch.body.碰撞材质=terrainCollider.sharedMaterial;
        patch.body.开始初始化();
    }
    void 提交地块()
    {
        // No frame may see top chunks removed while their pit floor or neighbouring
        // patch is still being built. Keep the previous surface until the whole batch is ready.
        foreach(var patch in patches.Values)if(patch.body)patch.body.提交更新();
        if(prepared.Count>0){
            if(!modified)预热();
            int minX=int.MaxValue,minZ=int.MaxValue,maxX=0,maxZ=0,size=settings.每区孔洞格数;
            foreach(var patch in prepared){minX=Mathf.Min(minX,patch.key.x*size);minZ=Mathf.Min(minZ,patch.key.y*size);maxX=Mathf.Max(maxX,(patch.key.x+1)*size);maxZ=Mathf.Max(maxZ,(patch.key.y+1)*size);}
            maxX=Mathf.Min(maxX,modified.holesResolution);maxZ=Mathf.Min(maxZ,modified.holesResolution);
            var holes=modified.GetHoles(minX,minZ,maxX-minX,maxZ-minZ);
            foreach(var patch in prepared){
                for(int z=patch.key.y*size;z<Mathf.Min((patch.key.y+1)*size,maxZ);z++)for(int x=patch.key.x*size;x<Mathf.Min((patch.key.x+1)*size,maxX);x++)holes[z-minZ,x-minX]=false;
                patch.ready=true;patch.body.gameObject.SetActive(true);patch.cuts.Clear();
            }
            // One holes upload and physics synchronization for the batch, after meshes exist.
            modified.SetHoles(minX,minZ,holes);terrain.terrainData=modified;if(terrainCollider)terrainCollider.terrainData=modified;
            prepared.Clear();
        }
        Physics.SyncTransforms();
    }
    public void 切换(bool useVoxels)
    {
        active=useVoxels;if(!terrain || !original)return;
        bool hasReady=false;foreach(var patch in patches.Values)if(patch.ready){hasReady=true;break;}
        terrain.terrainData=useVoxels && modified && hasReady?modified:original;
        if(terrainCollider)terrainCollider.terrainData=terrain.terrainData;
        foreach(var patch in patches.Values)if(patch.body)patch.body.gameObject.SetActive(useVoxels && patch.ready);
        Physics.SyncTransforms();
    }
    public void 还原(bool prepareNext=true)
    {
        if(terrain && original){terrain.terrainData=original;if(terrainCollider)terrainCollider.terrainData=original;}
        foreach(var patch in patches.Values)
        {
            if(patch.body){patch.body.gameObject.SetActive(false);Destroy(patch.body.gameObject);}
            if(patch.data)Destroy(patch.data);
        }
        patches.Clear();building.Clear();prepared.Clear();history.Clear();if(modified)Destroy(modified);modified=null;Physics.SyncTransforms();
        if(prepareNext && terrain && original && settings)预热();
    }
    void OnEnable(){if(terrain && settings)初始化(terrain,settings);}
    void OnDisable(){还原(false);}
    void OnDestroy(){还原(false);}
}
