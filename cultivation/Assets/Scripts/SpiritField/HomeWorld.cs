using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>保存扩建格，按石台外轮廓合并为连续地面；初始地面直接使用场景烘焙结果。</summary>
public sealed class HomeWorld : MonoBehaviour
{
    public const float 网格间距 = 40f;
    static HomeWorld instance;
    public List<Vector2Int> 扩建地块 = new List<Vector2Int>();
    readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>{Vector2Int.zero};
    readonly List<GameObject> hidden = new List<GameObject>();
    GameObject root;
    HomeBoundary boundary;
    Transform boundaryParent;
    int boundaryScene=-1;
    int sceneHandle = -1;
    public static HomeWorld 取()
    {
        if(instance)return instance;
        var go=new GameObject("家园地面管理");DontDestroyOnLoad(go);
        return instance=go.AddComponent<HomeWorld>();
    }
    void Awake(){instance=this;}
    public static Vector3 中心(Vector2Int cell)=>new Vector3(cell.x*网格间距,0,cell.y*网格间距);
    public IEnumerable<Vector2Int> 所有地块 { get { yield return Vector2Int.zero;foreach(var c in 扩建地块)yield return c; } }
    public bool 已有(Vector2Int cell)=>cells.Contains(cell);
    static readonly Vector2Int[] directions={Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down};
    public Vector3 吸附扩建(Vector3 position)
    {
        float best=float.MaxValue;Vector3 result=position;
        foreach(var c in 所有地块)foreach(var d in directions)
        {
            if(已有(c+d))continue;
            var p=中心(c+d);float distance=(p-position).sqrMagnitude;
            if(distance<best){best=distance;result=p;}
        }
        return result;
    }
    public bool 可扩建(Vector3 position,out string reason)
    {
        reason="";
        if(!摆放校验.当前场景可摆放){reason="只能扩建个人洞府";return false;}
        var cell=new Vector2Int(Mathf.RoundToInt(position.x/网格间距),Mathf.RoundToInt(position.z/网格间距));
        if(Vector3.Distance(position,中心(cell))>.2f){reason="需要对齐家园网格";return false;}
        if(已有(cell)){reason="此处已有地面";return false;}
        bool adjacent=false;foreach(var d in directions)if(已有(cell+d))adjacent=true;
        if(!adjacent){reason="须与已建地面相邻";return false;}
        foreach(var c in Physics.OverlapBox(position+Vector3.up*2,new Vector3(21,2,21)))
        {
            if(c.isTrigger||c is CharacterController||c.GetComponentInParent<HomeSurface>()||c.GetComponentInParent<HomeBoundary>()||c.GetComponentInParent<AirWall>())continue;
            reason="这里已有「"+c.name+"」";return false;
        }
        return true;
    }
    public bool 扩建(Vector3 position)
    {
        if(!可扩建(position,out _))return false;
        var cell=new Vector2Int(Mathf.RoundToInt(position.x/网格间距),Mathf.RoundToInt(position.z/网格间距));
        扩建地块.Add(cell);cells.Add(cell);重建();return true;
    }
    void Update()
    {
        int handle=SceneManager.GetActiveScene().handle;
        if(sceneHandle!=handle){sceneHandle=handle;重建();}
        if(!摆放校验.当前场景可摆放&&SceneManager.GetActiveScene().name!="StartScene"&&SceneManager.GetActiveScene().name!="Transition subtitles")HomeFarmBackground.推进(Time.deltaTime);
    }
    public void 导入(List<Vector2Int> imported)
    {
        扩建地块=new List<Vector2Int>();cells.Clear();cells.Add(Vector2Int.zero);
        if(imported!=null)foreach(var c in imported)if(cells.Add(c))扩建地块.Add(c);
        重建();
    }
    public void 重建()
    {
        foreach(var g in hidden)if(g)g.SetActive(true);hidden.Clear();
        if(root){root.SetActive(false);Destroy(root);}root=null;
        if(boundary&&boundaryScene!=SceneManager.GetActiveScene().handle)
        {
            if(boundaryParent){boundary.transform.SetParent(boundaryParent,true);boundary.同步(HomeAssets.取(),new[]{Vector2Int.zero});}
            else {boundary.gameObject.SetActive(false);Destroy(boundary.gameObject);}
            boundary=null;
        }
        if(!摆放校验.当前场景可摆放)return;
        var assets=HomeAssets.取();if(!assets||!assets.地面||!assets.地面材质||assets.地面内轮廓==null||assets.地面内轮廓.Length<3)return;
        GameObject authored=null;
        foreach(var g in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in g.GetComponentsInChildren<Transform>())
            if(t.name=="Ground"&&t.GetComponent<HomeSurface>())authored=t.gameObject;
        if(!boundary)
        {
            boundary=authored?authored.GetComponentInChildren<HomeBoundary>(true):null;
            boundaryParent=boundary?boundary.transform.parent:null;
            if(!boundary)boundary=new GameObject("边界围挡").AddComponent<HomeBoundary>();
            boundary.transform.SetParent(authored?authored.transform.parent:null,true);
            boundaryScene=SceneManager.GetActiveScene().handle;
        }
        boundary.同步(assets,所有地块);
        if(扩建地块.Count==0&&authored){Physics.SyncTransforms();return;}
        if(authored){hidden.Add(authored);authored.SetActive(false);}
        root=new GameObject("家园融合地面");
        if(扩建地块.Count==0)
        {
            var floor=Instantiate(assets.地面,root.transform);floor.name="Ground";
            var duplicate=floor.GetComponentInChildren<HomeBoundary>();if(duplicate){duplicate.gameObject.SetActive(false);Destroy(duplicate.gameObject);}
            Physics.SyncTransforms();return;
        }
        var mesh=HomeLandMesh.生成(assets,所有地块,out _);
        root.AddComponent<MeshFilter>().sharedMesh=mesh;root.AddComponent<MeshRenderer>().sharedMaterial=assets.地面材质;
        root.AddComponent<MeshCollider>().sharedMesh=mesh;root.AddComponent<HomeSurface>().运行网格=mesh;
        Physics.SyncTransforms();
    }
    public bool 包含(Vector3 p,float margin=0)
    {
        var assets=HomeAssets.取();return assets&&assets.地面内轮廓!=null&&assets.地面内轮廓.Length>=3&&HomeLandMesh.距离(assets,cells,new Vector2(p.x,p.z))<=-HomeLandMesh.行走边距-margin;
    }
}
