using System.Collections.Generic;
using Cultivation.WorldAuthoring;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>洞府拓境直接修改工坊地形；每次约20×20米，约原洞府面积四分之一。</summary>
public sealed class HomeWorld : MonoBehaviour
{
    public const float 网格间距=10f;
    public const float 扩建边长=20f;
    static HomeWorld instance;
    public List<Vector2Int> 扩建地块=new List<Vector2Int>();
    readonly HashSet<Vector2Int> cells=new HashSet<Vector2Int>();
    WorldMapLoader loader;
    int sceneHandle=-1;
    public static HomeWorld 取(){if(instance)return instance;var root=new GameObject("家园地面管理");DontDestroyOnLoad(root);return instance=root.AddComponent<HomeWorld>();}
    void Awake(){instance=this;}
    public static Vector3 中心(Vector2Int cell)=>new Vector3(cell.x*网格间距,0,cell.y*网格间距);
    public IEnumerable<Vector2Int> 所有地块{get{yield return Vector2Int.zero;foreach(var cell in 扩建地块)yield return cell;}}
    public bool 已有(Vector2Int cell)=>cells.Contains(cell);
    void FindMap(){if(loader&&loader.gameObject.scene==SceneManager.GetActiveScene())return;loader=null;foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects()){var candidate=root.GetComponentInChildren<WorldMapLoader>();if(candidate&&candidate.homeMap){loader=candidate;break;}}}
    public Vector3 吸附扩建(Vector3 position)
    {
        FindMap();if(!loader||loader.Data==null)return position;
        var centre=new Vector2Int(Mathf.RoundToInt(position.x/网格间距),Mathf.RoundToInt(position.z/网格间距));float best=float.MaxValue;Vector3 result=中心(centre);
        for(int z=-4;z<=4;z++)for(int x=-4;x<=4;x++){var p=中心(centre+new Vector2Int(x,z));p.y=loader.Data.spawn.y;float distance=(p-position).sqrMagnitude;if(distance>=best||!可扩建(p,out _))continue;best=distance;result=p;}
        result.y=loader.Data.spawn.y;return result;
    }
    bool TerrainAllows(Vector3 position,out string reason)
    {
        reason="";if(!loader||loader.Data==null){reason="洞府地图尚未加载";return false;}var data=loader.Data;
        if(Mathf.Abs(position.x)+11>=data.Width*.5f||Mathf.Abs(position.z)+11>=data.Width*.5f){reason="已到地图边界";return false;}
        int covered=0,total=0;bool connected=false;
        for(float z=-10;z<=10;z+=1)for(float x=-10;x<=10;x+=1)
        {
            if(!InPatch(x,z))continue;total++;bool ground=data.HasGround(position.x+x,position.z+z);if(ground)covered++;
            if(!connected&&(ground||data.HasGround(position.x+x+data.cellSize,position.z+z)||data.HasGround(position.x+x-data.cellSize,position.z+z)||data.HasGround(position.x+x,position.z+z+data.cellSize)||data.HasGround(position.x+x,position.z+z-data.cellSize)))connected=true;
        }
        if(covered>total*.4f){reason="此处已有太多地面，请移到洞府边缘";return false;}if(!connected){reason="新地须与已有地面相连";return false;}return true;
    }
    public bool 可扩建(Vector3 position,out string reason)
    {
        reason="";if(!摆放校验.当前场景可摆放){reason="只能扩建个人洞府";return false;}FindMap();if(!TerrainAllows(position,out reason))return false;
        var cell=new Vector2Int(Mathf.RoundToInt(position.x/网格间距),Mathf.RoundToInt(position.z/网格间距));if((new Vector2(position.x,position.z)-new Vector2(cell.x,cell.y)*网格间距).sqrMagnitude>.04f){reason="需要对齐扩建预览";return false;}
        foreach(var collider in Physics.OverlapBox(position+Vector3.up*2,new Vector3(9.5f,2,9.5f)))
        {
            if(collider.isTrigger||collider is CharacterController||collider.gameObject.layer==28||collider.GetComponentInParent<HomeSurface>()||collider.GetComponentInParent<AirWall>())continue;
            reason="这里已有「"+collider.name+"」";return false;
        }
        return true;
    }
    static bool InPatch(float x,float z){float qx=Mathf.Max(Mathf.Abs(x)-7,0),qz=Mathf.Max(Mathf.Abs(z)-7,0);return qx*qx+qz*qz<=9;}
    void Paint(Vector2Int cell)
    {
        var data=loader.Data;var p=中心(cell);float target=data.Sample(data.height,0,0);
        for(int z=1;z<data.resolution-1;z++)for(int x=1;x<data.resolution-1;x++)
        {
            var point=data.Position(x,z);bool inside=InPatch(point.x-p.x,point.z-p.z);
            // 相邻拓境地块连接成连续地基，四个圆角不能在中间留下小洞和岩壁。
            if(!inside)foreach(var other in cells){var q=中心(other);var delta=q-p;
                if(Mathf.Abs(delta.z)<.01f&&Mathf.Abs(delta.x)<=扩建边长+.01f&&Mathf.Abs(point.z-p.z)<=10
                    &&point.x>=Mathf.Min(p.x,q.x)&&point.x<=Mathf.Max(p.x,q.x)){inside=true;break;}
                if(Mathf.Abs(delta.x)<.01f&&Mathf.Abs(delta.z)<=扩建边长+.01f&&Mathf.Abs(point.x-p.x)<=10
                    &&point.z>=Mathf.Min(p.z,q.z)&&point.z<=Mathf.Max(p.z,q.z)){inside=true;break;}
            }
            if(!inside)continue;int i=data.Index(x,z);if(data.land[i]>.5f)continue;data.land[i]=1;data.height[i]=data.dryHeight[i]=target;data.water[i]=0;data.waterHeight[i]=0;
        }
    }
    public bool 扩建(Vector3 position){if(!可扩建(position,out _))return false;var cell=new Vector2Int(Mathf.RoundToInt(position.x/网格间距),Mathf.RoundToInt(position.z/网格间距));if(cells.Add(cell))扩建地块.Add(cell);Paint(cell);loader.Rebuild();Physics.SyncTransforms();return true;}
    void Update(){int handle=SceneManager.GetActiveScene().handle;if(sceneHandle!=handle){sceneHandle=handle;loader=null;重建();}if(!摆放校验.当前场景可摆放&&SceneManager.GetActiveScene().name!="StartScene"&&SceneManager.GetActiveScene().name!="Transition subtitles")HomeFarmBackground.推进(Time.deltaTime);}
    public void 导入(List<Vector2Int> imported){扩建地块=new List<Vector2Int>();cells.Clear();if(imported!=null)foreach(var cell in imported)if(cells.Add(cell))扩建地块.Add(cell);重建();}
    public void 重建(){if(!摆放校验.当前场景可摆放)return;FindMap();if(!loader||loader.Data==null)return;loader.ResetTerrainData();foreach(var cell in 扩建地块)Paint(cell);loader.Rebuild();Physics.SyncTransforms();}
    public bool 包含(Vector3 p,float margin=0)
    {
        FindMap();if(!loader||loader.Data==null)return false;var data=loader.Data;if(!data.HasGround(p.x,p.z)||data.IsWet(p.x,p.z))return false;
        // 占地是方形，必须覆盖四角；圆周检查会允许大建筑角部悬在云海上。
        for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++){float px=p.x+x*(margin+.25f),pz=p.z+z*(margin+.25f);if(!data.HasGround(px,pz)||data.IsWet(px,pz))return false;}return true;
    }
}
