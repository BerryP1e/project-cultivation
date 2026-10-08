using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>Reversible, manifest-limited replacements. No source object is deleted or deactivated.</summary>
public sealed class VoxelMapPilot : MonoBehaviour
{
    [Serializable]
    sealed class Replacement
    {
        public Renderer[] renderers;
        public Collider[] colliders;
        public bool[] rendererStates,colliderStates;
        public VoxelDestructible body;
        public Transform source;
        public VoxelVolumeAsset data;
        public Bounds bounds;
        public bool tree,fallen,supportPending,preloaded;
        public Renderer[] foliage;
        public bool[] foliageStates;
        public Mesh[] canopyMeshes;
        public VoxelTreeDebris debris;
        public GameObject preview;
    }
    [Serializable]
    sealed class GroundPlant
    {
        public Vector3 anchor;
        public Renderer[] renderers;
        public Collider[] colliders;
        public bool[] rendererStates,colliderStates;
        public bool removed;
    }
    [SerializeField] List<GroundPlant> plants=new List<GroundPlant>();
    [SerializeField] List<Replacement> replacements = new List<Replacement>();
    [field:SerializeField] public bool 正在替换 { get; private set; }
    public int 替换数量 => replacements.Count;
    public int 已激活数量=>bodies.Count;
    public bool 等待更新
    {
        get
        {
            if(attacks.Count>0 || supportTask!=null || (地面 && 地面.等待更新))return true;
            foreach(var r in replacements)if(r.supportPending || (r.body && (!r.body.初始化完成 || r.body.等待块>0)))return true;
            return false;
        }
    }
    public IReadOnlyList<VoxelDestructible> 体素对象 => bodies;
    [SerializeField] List<VoxelDestructible> bodies = new List<VoxelDestructible>();
    [SerializeField] bool resumeAfterEnable;
    public bool 测试挖掘;
    public double 最近更新毫秒 { get; private set; }
    public string 最近阶段 {get;private set;}
    [field:SerializeField] public VoxelTerrainReplacement 地面 {get;private set;}
    int nextBody,nextWorkStage;
    IEnumerator supportTask;
    readonly Queue<IEnumerator> attacks=new Queue<IEnumerator>();
    bool domainReady;
    static readonly HashSet<VoxelVolumeAsset> warmed=new HashSet<VoxelVolumeAsset>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void 装钩子()
    {
        warmed.Clear();
        SceneManager.sceneLoaded -= 场景加载;
        SceneManager.sceneLoaded += 场景加载;
        for(int i=0;i<SceneManager.sceneCount;i++)安装(SceneManager.GetSceneAt(i));
    }
    static void 场景加载(Scene scene,LoadSceneMode mode) => 安装(scene);
    public static VoxelMapPilot 安装(Scene scene)
    {
        if(!Application.isPlaying || !scene.IsValid() || !scene.isLoaded)return null;
        foreach(var root in scene.GetRootGameObjects())
        {var existing=root.GetComponent<VoxelMapPilot>();if(existing)return existing;}
        var settings=Resources.Load<VoxelMapPilotSettings>("Voxel/MapPilots/"+scene.name);
        if(!settings || !settings.启用试点 || settings.场景路径!=scene.path)return null;
        var go=new GameObject("VoxelMapPilot");SceneManager.MoveGameObjectToScene(go,scene);
        var pilot=go.AddComponent<VoxelMapPilot>();pilot.准备(scene,settings);
        return pilot;
    }
    void 准备(Scene scene,VoxelMapPilotSettings settings)
    {
        foreach(var entry in settings.替换项)
        {
            var source=定位(scene,entry);
            if(!source || !entry.数据 || !允许替换(source,entry))
            {Debug.LogWarning("[体素试点] 跳过不匹配或含交互逻辑的对象："+entry.名称);continue;}
            var renderers=source.GetComponentsInChildren<Renderer>(true);
            var resource=source;for(int i=0;i<entry.资源上溯层数 && resource.parent;i++)resource=resource.parent;
            var colliders=resource.GetComponentsInChildren<Collider>(true);
            var r=new Replacement {source=source,data=entry.数据,bounds=entry.预期包围盒,tree=entry.树木,preloaded=entry.预先加载,
                renderers=renderers,colliders=colliders,rendererStates=new bool[renderers.Length],colliderStates=new bool[colliders.Length]};
            for(int i=0;i<renderers.Length;i++)r.rendererStates[i]=renderers[i].enabled;
            for(int i=0;i<colliders.Length;i++)r.colliderStates[i]=colliders[i].enabled;
            if(entry.树木)
            {
                var foliage=new List<Renderer>();foreach(var renderer in resource.GetComponentsInChildren<Renderer>(true))
                    if(Array.IndexOf(renderers,renderer)<0)foliage.Add(renderer);
                r.foliage=foliage.ToArray();r.canopyMeshes=entry.树冠网格;r.foliageStates=new bool[r.foliage.Length];for(int i=0;i<r.foliage.Length;i++)r.foliageStates[i]=r.foliage[i].enabled;
            }
            replacements.Add(r);
            if(r.data.完整外观)
            {
                r.preview=new GameObject("VoxelPristineSurface");r.preview.layer=source.gameObject.layer;r.preview.transform.SetParent(source,false);
                r.preview.AddComponent<MeshFilter>().sharedMesh=r.data.完整外观;
                var preview=r.preview.AddComponent<MeshRenderer>();preview.sharedMaterial=r.data.材质;
                preview.shadowCastingMode=renderers[0].shadowCastingMode;preview.receiveShadows=renderers[0].receiveShadows;
            }
            VoxelSurfaceProjection.预热(entry.数据);
            if(warmed.Add(entry.数据))for(int i=0;i<entry.数据.分块坐标.Length;i++)if(entry.数据.预烘焙网格[i].vertexCount>0)
            {var warmMesh=VoxelChunkMesher.生成(entry.数据,entry.数据.实体,entry.数据.分块坐标[i]);Destroy(warmMesh);break;}
            if(entry.预先加载)准备实体(r);
        }
        if(settings.地面 && settings.地面.启用)
        {
            foreach(var root in scene.GetRootGameObjects())foreach(var terrain in root.GetComponentsInChildren<Terrain>())
                if(terrain.terrainData==settings.地面.源地形)
                {
                    var ground=gameObject.AddComponent<VoxelTerrainReplacement>();
                    if(ground.初始化(terrain,settings.地面))地面=ground;else Destroy(ground);
                }
        }
        切换试点(true);
        if(地面)准备植被(scene);
        Debug.Log($"[体素环境] {scene.name} 已登记 {replacements.Count} 处岩石/树干，预加载 {bodies.Count} 处；其余受击时加载，可在 P 面板切回。");
    }
    void 准备植被(Scene scene)
    {
        var terrain=Terrain.activeTerrain;if(!terrain || terrain.gameObject.scene!=scene)return;
        foreach(var root in scene.GetRootGameObjects())if(root.name=="野外环境")foreach(Transform category in root.transform)
        {
            if(category.name!="灌木与藤蔓" && category.name!="花草" && category.name!="河岸")continue;
            foreach(Transform plant in category)
            {
                if(plant.name.StartsWith("stone_",StringComparison.OrdinalIgnoreCase))continue;
                var renderers=plant.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                bool solid=false;foreach(var filter in plant.GetComponentsInChildren<MeshFilter>())
                    if(filter.name.StartsWith("stone_",StringComparison.OrdinalIgnoreCase)){solid=true;break;}
                if(solid)continue;
                var anchor=plant.position;anchor.y=terrain.SampleHeight(anchor)+terrain.transform.position.y;
                var p=new GroundPlant {anchor=anchor,renderers=renderers,colliders=plant.GetComponentsInChildren<Collider>()};
                p.rendererStates=new bool[p.renderers.Length];p.colliderStates=new bool[p.colliders.Length];
                for(int i=0;i<p.renderers.Length;i++)p.rendererStates[i]=p.renderers[i].enabled;
                for(int i=0;i<p.colliders.Length;i++)p.colliderStates[i]=p.colliders[i].enabled;plants.Add(p);
            }
        }
    }
    void 切换植被(bool useVoxels)
    {
        foreach(var p in plants)
        {
            bool hidden=useVoxels && p.removed;
            for(int i=0;i<p.renderers.Length;i++)if(p.renderers[i])p.renderers[i].enabled=!hidden && p.rendererStates[i];
            for(int i=0;i<p.colliders.Length;i++)if(p.colliders[i])p.colliders[i].enabled=!hidden && p.colliderStates[i];
        }
    }
    bool 准备实体(Replacement r)
    {
        if(r.body)return true;GameObject child=null;
        try
        {
            child=new GameObject("VoxelReplacement");child.SetActive(false);child.layer=r.source.gameObject.layer;child.transform.SetParent(r.source,false);
            r.body=child.AddComponent<VoxelDestructible>();r.body.数据=r.data;
            if(r.colliders.Length>0)r.body.碰撞材质=r.colliders[0].sharedMaterial;
            r.body.自动更新=false;r.body.每帧最多更新块=1;
            if(r.preloaded)r.body.初始化();else r.body.开始初始化();bodies.Add(r.body);
            return true;
        }
        catch(Exception e){if(child)Destroy(child);r.body=null;Debug.LogException(e);return false;}
    }
    static Transform 定位(Scene scene,VoxelMapPilotSettings.Entry entry)
    {
        Transform result=null;
        foreach(var root in scene.GetRootGameObjects())if(root.name==entry.根名称)
        {if(result)return null;result=root.transform;}
        if(!result || entry.子索引==null || entry.子名称==null || entry.子索引.Length!=entry.子名称.Length)return null;
        for(int i=0;i<entry.子索引.Length;i++)
        {int index=entry.子索引[i];if(index<0 || index>=result.childCount)return null;result=result.GetChild(index);if(result.name!=entry.子名称[i])return null;}
        return result;
    }
    static bool 允许替换(Transform source,VoxelMapPilotSettings.Entry entry)
    {
        if(!source.gameObject.activeInHierarchy || (source.position-entry.预期位置).sqrMagnitude>.0004f ||
           Quaternion.Angle(source.rotation,entry.预期旋转)>.1f || (source.lossyScale-entry.预期缩放).sqrMagnitude>.0001f)return false;
        var filter=source.GetComponent<MeshFilter>();
        var renderer=source.GetComponent<MeshRenderer>();
        if(!filter || !renderer)return false;
        if(filter.sharedMesh!=entry.源网格)
        {
            // Unity substitutes combined meshes for statically batched renderers during scene load.
            if(!renderer.isPartOfStaticBatch || (renderer.bounds.center-entry.预期包围盒.center).sqrMagnitude>.0004f ||
               (renderer.bounds.size-entry.预期包围盒.size).sqrMagnitude>.0004f)return false;
            var materials=renderer.sharedMaterials;
            if(entry.源材质==null || materials.Length!=entry.源材质.Length)return false;
            for(int i=0;i<materials.Length;i++)if(materials[i]!=entry.源材质[i])return false;
        }
        // Do not interfere with LOD controllers, triggers, NPCs or resource scripts.
        foreach(var c in source.GetComponentsInChildren<Component>(true))
        {
            if(!c)return false;
            if(c is Transform || c is MeshFilter || c is MeshRenderer)continue;
            if(c is Animation animation && animation.GetClipCount()==0 && !animation.isPlaying)continue;
            if(c is Collider collider && !collider.isTrigger)continue;
            return false;
        }
        return true;
    }
    public void 切换试点(bool useVoxels)
    {
        正在替换=useVoxels;
        if(地面)地面.切换(useVoxels);
        foreach(var r in replacements)
        {
            if(r.debris && r.debris.已开始)r.debris.gameObject.SetActive(useVoxels);
            切换对象(r,useVoxels);
        }
        切换植被(useVoxels);
        Physics.SyncTransforms();
    }
    static void 切换对象(Replacement r,bool useVoxels)
    {
        bool replaced=useVoxels && r.body && r.body.初始化完成;
        if(r.body)r.body.gameObject.SetActive(replaced);
        if(r.preview)r.preview.SetActive(useVoxels && !replaced);
        for(int i=0;i<r.renderers.Length;i++)if(r.renderers[i])r.renderers[i].enabled=replaced || useVoxels && r.preview?false:r.rendererStates[i];
        for(int i=0;i<r.colliders.Length;i++)if(r.colliders[i])r.colliders[i].enabled=replaced?false:r.colliderStates[i];
        if(r.foliage!=null)for(int i=0;i<r.foliage.Length;i++)if(r.foliage[i])r.foliage[i].enabled=replaced && r.fallen?false:r.foliageStates[i];
        if(replaced && r.fallen && r.debris && !r.debris.已开始)r.debris.开始倒下();
    }
    public int 破坏球(Vector3 center,float radius)
    {
        if(!正在替换)return 0;int removed=0;
        foreach(var r in replacements)
        {
            if(r.bounds.SqrDistance(center)>radius*radius)continue;
            if(!准备实体(r))continue;切换对象(r,true);
            int count=r.body.准备破坏球(center,radius);removed+=count;if(r.tree && count>0)r.supportPending=true;
        }
        if(地面)
        {
            地面.破坏球(center,radius);
            foreach(var plant in plants)if((plant.anchor-center).sqrMagnitude<radius*radius)plant.removed=true;
            切换植被(true);
        }
        return removed;
    }
    public void 排队破坏球(Vector3 center,float radius)
    {if(正在替换 && radius>0)attacks.Enqueue(处理攻击(center,radius));}
    IEnumerator 处理攻击(Vector3 center,float radius)
    {
        foreach(var r in replacements)
        {
            if(r.bounds.SqrDistance(center)>radius*radius)continue;
            if(准备实体(r))
            {
                int count=r.body.准备破坏球(center,radius);
                if(r.tree && count>0)r.supportPending=true;
            }
            yield return null;
        }
        if(地面){var task=地面.分步破坏球(center,radius);while(task.MoveNext())yield return null;}
        foreach(var plant in plants)if((plant.anchor-center).sqrMagnitude<radius*radius)plant.removed=true;
        切换植被(true);
    }
    public bool 可破坏(Collider collider)
    {
        if(!正在替换 || !collider)return false;
        if(collider.GetComponentInParent<VoxelDestructible>() || 地面 && 地面.可破坏(collider))return true;
        foreach(var r in replacements)if(Array.IndexOf(r.colliders,collider)>=0)return true;
        return false;
    }
    public void 还原岩石()
    {
        supportTask=null;attacks.Clear();
        foreach(var r in replacements)
        {
            if(r.debris){r.debris.gameObject.SetActive(false);Destroy(r.debris.gameObject);r.debris=null;}
            if(r.body)
            {
                if(r.preloaded)r.body.初始化();
                else {var body=r.body;r.body=null;bodies.Remove(body);body.gameObject.SetActive(false);Destroy(body.gameObject);}
            }
            r.fallen=false;r.supportPending=false;切换对象(r,正在替换);
        }
        nextBody=0;
        foreach(var plant in plants)plant.removed=false;切换植被(正在替换);
        if(地面)地面.还原();
    }
    void Update()
    {
        if(!正在替换)return;
        if(测试挖掘 && Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0) &&
           !UiEscRegistry.SceneInputBlocked && (!EventSystem.current || !EventSystem.current.IsPointerOverGameObject()))
        {
            var camera=Camera.main;
            if(camera && Physics.Raycast(camera.ScreenPointToRay(Input.mousePosition),out var hit,120,~0,QueryTriggerInteraction.Ignore))
            {bool found=false;foreach(var r in replacements)if(Array.IndexOf(r.colliders,hit.collider)>=0){found=true;break;}
             var body=hit.collider.GetComponentInParent<VoxelDestructible>();if(found || (body && bodies.Contains(body)) || (地面 && 地面.可破坏(hit.collider)))破坏球(hit.point,.45f);}
        }
        更新局部();
    }
    public int 更新局部()
    {
        if(!正在替换)return 0;
        // Spend the existing 4ms budget, rather than stopping after two tiny sample/queue
        // steps. Persist round-robin priority so repeated attacks cannot starve reconstruction.
        var timer=System.Diagnostics.Stopwatch.StartNew();int updated=0,idle=0;
        while(updated<32 && idle<4 && timer.Elapsed.TotalMilliseconds<4){
            int work=0;int stage=nextWorkStage;nextWorkStage=(nextWorkStage+1)%4;
            if(stage==0 && attacks.Count>0){最近阶段="攻击范围";if(!attacks.Peek().MoveNext())attacks.Dequeue();work=1;}
            else if(stage==1 && 地面){work=地面.更新局部(attacks.Count==0);if(work>0)最近阶段="地面/"+地面.最近阶段;}
            else if(stage==2){
                for(int visited=0;visited<bodies.Count;visited++){
                    var body=bodies[nextBody];nextBody=(nextBody+1)%bodies.Count;if(!body)continue;
                    if(!body.初始化完成){
                        最近阶段="准备实体/"+body.数据.来源;work=body.继续初始化();
                        if(body.初始化完成)foreach(var r in replacements)if(r.body==body){切换对象(r,true);break;}
                    }
                    else if(body.等待块>0||body.待提交块>0){最近阶段="实体重建/"+body.数据.来源;work=body.更新局部();}
                    if(work>0)break;
                }
            }
            else if(stage==3){
                if(supportTask==null)foreach(var r in replacements)if(r.supportPending && r.body){
                    r.supportPending=false;
                    supportTask=VoxelTreeSupport.检查(r.body,fallen=>{
                        bool newly=fallen && !r.fallen;r.fallen|=fallen;切换对象(r,正在替换);
                        if(newly && r.debris && r.body.初始化完成)r.debris.开始倒下();
                    },r.fallen?null:(Func<byte[],float[],IEnumerator>)((cells,field)=>VoxelTreeDebris.建立(r.body,cells,field,r.foliage,r.canopyMeshes,d=>r.debris=d)));break;
                }
                if(supportTask!=null){最近阶段="树根连通";if(!supportTask.MoveNext())supportTask=null;work=1;}
            }
            updated+=work;if(work==0)idle++;else idle=0;
        }
        最近更新毫秒=timer.Elapsed.TotalMilliseconds;
        return updated;
    }
    void OnEnable()
    {
        if(!domainReady)
        {foreach(var r in replacements){r.fallen=false;r.supportPending=false;if(r.debris){r.debris.gameObject.SetActive(false);Destroy(r.debris.gameObject);r.debris=null;}}foreach(var p in plants)p.removed=false;domainReady=true;}
        if(resumeAfterEnable && replacements.Count>0)
            切换试点(true);
    }
    void OnDisable() {supportTask=null;resumeAfterEnable=正在替换;切换试点(false);}
    void OnDestroy()
    {
        切换试点(false);
        foreach(var r in replacements)if(r.debris)Destroy(r.debris.gameObject);
        foreach(var r in replacements)if(r.preview)Destroy(r.preview);
        foreach(var body in bodies)if(body)Destroy(body.gameObject);
    }
}
