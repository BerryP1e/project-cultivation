using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

/// <summary>Real large-area casts, checked every frame for missing ground and stale staged geometry.</summary>
public static class VoxelTerrainStreamingChecks
{
    public static string Result {get;private set;}="Not run";
    [MenuItem("修仙/体素/验证大范围破坏无缺面（Play）")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("请进入宗门野外 Play");
        var pilot=Object.FindObjectOfType<VoxelMapPilot>();if(!pilot||!pilot.地面)throw new InvalidOperationException("没有体素地面");
        pilot.StartCoroutine(Guard(Check()));
    }
    static IEnumerator Guard(IEnumerator check)
    {
        while(true){bool next=false;Exception error=null;try{next=check.MoveNext();}catch(Exception e){error=e;}
            if(error!=null){(check as IDisposable)?.Dispose();Result="FAIL "+error;Debug.LogError(Result);yield break;}
            if(!next){Result="PASS";Debug.Log("VOXEL_TERRAIN_STREAMING_PASS");yield break;}yield return check.Current;}
    }
    static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
    public static IEnumerator Check()
    {
        Result="Running";
        var pilot=Object.FindObjectOfType<VoxelMapPilot>();var ground=pilot.地面;var terrain=Terrain.activeTerrain;
        var vitals=Object.FindObjectOfType<PlayerVitals>();var player=vitals.gameObject;
        var movement=player.GetComponent<PlayerController>();var cc=player.GetComponent<CharacterController>();
        var data=Object.FindObjectOfType<UIPanelData>();var aim=player.GetComponent<PlayerManualAim>();var loader=player.GetComponent<PlayerAbilityLoader>();
        var position=player.transform.position;var rotation=player.transform.rotation;
        bool moved=movement.enabled,collided=cc.enabled,piloted=pilot.enabled,regen=vitals.自动回复;
        float mana=vitals.当前灵气,health=vitals.当前气血;var slots=data.主动技能;var treasure=data.当前法宝;var gongfa=data.当前功法;
        var owned=new List<string>(data.已拥有法宝);int kills=data.青山剑有效击杀;
        var source=ground.原地形;var sourceHoles=source.GetHoles(0,0,source.holesResolution,source.holesResolution);
        Vector3 point=terrain.transform.position+new Vector3(103.125f,0,175);
        point.y=source.GetInterpolatedHeight(103.125f/source.size.x,175/source.size.z)+terrain.transform.position.y;
        try{
            Require(aim&&aim.可接收输入,"玩家当前不能操作");
            movement.enabled=false;cc.enabled=false;vitals.自动回复=false;pilot.enabled=false;pilot.切换试点(true);pilot.还原岩石();
            player.GetComponent<NpcTargeting>().ClearLock();data.当前功法=null;loader.Refresh();player.transform.position=point-Vector3.forward*8;
            foreach(string id in new[]{"ability_fentian_yanshu","giant","overlap"}){
                player.GetComponent<PlayerAnimationController>()?.停止动作();vitals.当前灵气=vitals.灵气上限;
                if(id=="ability_fentian_yanshu"){
                    var skill=AssetDatabase.LoadAssetAtPath<ActiveDivineAbility>("Assets/Data/Generated/ActiveDivineAbility/"+id+".asset");
                    data.主动技能=new List<Object>{skill,null,null,null,null,null};Require(aim.确认施放(0,point),"焚天施放失败");
                }else if(id=="giant"){
                    data.当前法宝=AssetDatabase.LoadAssetAtPath<TreasureDefinition>("Assets/Data/Generated/TreasureDefinition/treasure_qingshan_sword.asset");
                    if(!data.已拥有法宝.Contains(QingshanSwordTreasure.法宝id))data.已拥有法宝.Add(QingshanSwordTreasure.法宝id);
                    data.青山剑有效击杀=100;yield return null;Require(aim.确认施放(-2,point),"巨剑施放失败");
                }else{
                    // Add another cut after some meshes have already been prepared. Their
                    // stale versions must be discarded, never published over the newer SDF.
                    pilot.排队破坏球(point-Vector3.up,4);
                    int prepareFrames=0;
                    while(!Object.FindObjectsOfType<VoxelDestructible>().Any(b=>b.数据.地形源&&b.待提交块>0)&&prepareFrames++<1000){
                        pilot.更新局部();Physics.SyncTransforms();CheckFloor(point);yield return null;
                    }
                    Require(prepareFrames<1000,"重叠测试未产生待提交网格");
                    pilot.排队破坏球(point+new Vector3(.6f,-1.4f,.4f),3.2f);
                }
                bool sawWork=false;int frames=0;double cpu=0,peak=0;float until=Time.realtimeSinceStartup+25;
                do{
                    int units=pilot.更新局部();Require(units<=32,"调度工作上限异常");cpu+=pilot.最近更新毫秒;peak=Math.Max(peak,pilot.最近更新毫秒);
                    sawWork|=pilot.等待更新||units>0;Physics.SyncTransforms();CheckFloor(point);frames++;
                    yield return null;
                }while((!sawWork||pilot.等待更新)&&Time.realtimeSinceStartup<until);
                Require(sawWork&&!pilot.等待更新,"大范围破坏没有完成 "+id);
                Debug.Log("TERRAIN_STREAM "+id+" frames="+frames+" cpu="+cpu+" peak="+peak+" missingGroundFrames=0 patches="+ground.活动地块数);
            }
            foreach(var body in Object.FindObjectsOfType<VoxelDestructible>().Where(b=>b.数据.地形源)){
                Require(body.待提交块==0&&body.等待块==0,"有遗留待提交网格");
                var volume=body.数据;var cells=new byte[volume.实体.Length];var field=body.复制距离();
                for(int z=0;z<volume.尺寸.z;z++)for(int y=0;y<volume.尺寸.y;y++)for(int x=0;x<volume.尺寸.x;x++)cells[volume.索引(x,y,z)]=(byte)(body.取实体(x,y,z)?1:0);
                foreach(var key in volume.分块坐标){
                    var expected=VoxelChunkMesher.生成(volume,cells,key,field);
                    try{
                        var actual=body.取块网格(key);
                        Require(actual.vertices.SequenceEqual(expected.vertices)&&actual.normals.SequenceEqual(expected.normals)&&actual.triangles.SequenceEqual(expected.triangles)&&actual.colors32.SequenceEqual(expected.colors32),"提交了过期网格或表面算法变了 "+key);
                    }finally{Object.Destroy(expected);}
                }
                foreach(var f in body.GetComponentsInChildren<MeshFilter>()){
                    var collider=f.GetComponent<MeshCollider>();
                    // On activation Unity may auto-bind an empty MeshFilter back to a
                    // null MeshCollider. Disabling that collider is what prevents ghosts.
                    Require(f.sharedMesh.vertexCount>0?collider.enabled&&collider.sharedMesh==f.sharedMesh:!collider.enabled,"渲染/碰撞未同步 "+f.transform.parent.name+"/"+f.name);
                }
                yield return null;
            }
            Require(sourceHoles.Cast<bool>().SequenceEqual(source.GetHoles(0,0,source.holesResolution,source.holesResolution).Cast<bool>()),"源TerrainData被修改");
            // Cancel a partially prepared batch and ensure toggling/restore leaves no holes.
            pilot.排队破坏球(point+Vector3.right*8,4);pilot.更新局部();pilot.切换试点(false);Require(terrain.terrainData==source,"切回原地形失败");
            pilot.还原岩石();pilot.切换试点(true);CheckFloor(point);Require(!pilot.等待更新&&ground.活动地块数==0,"还原未取消正在生成的批次");
            Result="PASS";Debug.Log("VOXEL_TERRAIN_STREAMING_PASS actual fire+giant, repeated overlap, old surface retained, exact final meshes, restore");
        }finally{
            pilot.还原岩石();data.主动技能=slots;data.当前法宝=treasure;data.当前功法=gongfa;data.已拥有法宝=owned;data.青山剑有效击杀=kills;
            player.GetComponent<PlayerAnimationController>()?.停止动作();player.transform.SetPositionAndRotation(position,rotation);
            movement.enabled=moved;cc.enabled=collided;vitals.自动回复=regen;vitals.当前灵气=mana;vitals.当前气血=health;loader.Refresh();pilot.enabled=piloted;
        }
    }
    static void CheckFloor(Vector3 point)
    {
        for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++){
            bool hit=false;foreach(var h in Physics.RaycastAll(point+new Vector3(x*.65f,8,z*.65f),Vector3.down,20,~0,QueryTriggerInteraction.Ignore))
                if(h.collider is TerrainCollider||h.collider.GetComponentInParent<VoxelDestructible>()?.数据.地形源){hit=true;break;}
            Require(hit,"地面临时缺面："+x+","+z);
        }
    }
}
