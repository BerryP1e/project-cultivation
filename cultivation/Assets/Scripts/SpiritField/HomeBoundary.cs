using System.Collections.Generic;
using UnityEngine;

/// <summary>空气墙和沿墙树石的统一视图；只增删、移动轮廓变化涉及的实例。</summary>
public sealed class HomeBoundary : MonoBehaviour
{
    public int 最近新增 { get; private set; }
    public int 最近移除 { get; private set; }
    public int 最近移动 { get; private set; }
    public int 最近保留 { get; private set; }
    const float 间距=2.4f;
    readonly Dictionary<string,HomeBoundaryPiece> pieces=new Dictionary<string,HomeBoundaryPiece>();
    struct Desired
    {
        public string key;
        public Vector3 position,scale;
        public Quaternion rotation;
        public GameObject prefab;
        public bool wall;
    }
    static uint Hash(int x,int y)
    {
        unchecked { uint h=(uint)x*73856093u^(uint)y*19349663u^0x63b79a91u;h^=h>>16;h*=0x7feb352du;h^=h>>15;return h; }
    }
    static float Random01(uint h,int shift)=>((h>>shift)&255)/255f;
    static Vector2 Normal(HomeAssets assets,HashSet<Vector2Int> cells,Vector2 p)
    {
        const float e=.03f;
        return new Vector2(HomeLandMesh.距离(assets,cells,p+Vector2.right*e)-HomeLandMesh.距离(assets,cells,p-Vector2.right*e),HomeLandMesh.距离(assets,cells,p+Vector2.up*e)-HomeLandMesh.距离(assets,cells,p-Vector2.up*e)).normalized;
    }
    static string PointKey(Vector2 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000);
    static IEnumerable<Desired> Layout(HomeAssets assets,HashSet<Vector2Int> cells)
    {
        foreach(var edge in HomeLandMesh.边界轮廓(assets,cells))
        {
            var d=edge.b-edge.a;if(d.magnitude<.08f)continue;
            string a=PointKey(edge.a),b=PointKey(edge.b);string key=string.CompareOrdinal(a,b)<0?a+"/"+b:b+"/"+a;
            var center=(edge.a+edge.b)*.5f;
            yield return new Desired{key="墙/"+key,wall=true,position=new Vector3(center.x,4,center.y),rotation=Quaternion.LookRotation(new Vector3(-d.y,0,d.x)),scale=new Vector3(d.magnitude+.04f,10,.25f)};
        }
        if(assets.围挡树木==null||assets.围挡树木.Length==0||assets.围挡石头==null||assets.围挡石头.Length==0)yield break;
        var candidates=new HashSet<Vector2Int>();
        foreach(var cell in cells)
        {
            Vector2 center=new Vector2(cell.x,cell.y)*HomeWorld.网格间距;
            int x0=Mathf.FloorToInt((center.x-24)/间距),x1=Mathf.CeilToInt((center.x+24)/间距);
            int y0=Mathf.FloorToInt((center.y-24)/间距),y1=Mathf.CeilToInt((center.y+24)/间距);
            for(int x=x0;x<=x1;x++)for(int y=y0;y<=y1;y++)candidates.Add(new Vector2Int(x,y));
        }
        foreach(var key in candidates)
        {
            uint h=Hash(key.x,key.y);
            var p=new Vector2(key.x,key.y)*间距+new Vector2(Random01(h,0)-.5f,Random01(h,8)-.5f)*.5f;
            float d=HomeLandMesh.距离(assets,cells,p);
            if(Mathf.Abs(d+HomeLandMesh.行走边距)>1.2f)continue;
            // 投影至与空气墙相同的等值线，再稍向外放，保留内部摆放空间。
            for(int i=0;i<3;i++)p-=Normal(assets,cells,p)*(HomeLandMesh.距离(assets,cells,p)+HomeLandMesh.行走边距);
            var normal=Normal(assets,cells,p);p+=normal*(.35f+Random01(h,16)*.25f);
            d=HomeLandMesh.距离(assets,cells,p);float t=Mathf.Clamp01((d+HomeLandMesh.收边宽度)/HomeLandMesh.收边宽度);
            bool tree=h%5==0;var prefabs=tree?assets.围挡树木:assets.围挡石头;
            var prefab=prefabs[(h/5)%prefabs.Length];if(!prefab)continue;
            float scale=.85f+Random01(h,24)*.3f;
            yield return new Desired{key="景/"+key.x+","+key.y,prefab=prefab,position=new Vector3(p.x,-1.3f*(1-Mathf.Cos(t*Mathf.PI*.5f))-.12f,p.y),rotation=Quaternion.Euler(0,Random01(h,0)*360,0),scale=Vector3.one*scale};
        }
    }
    public void 同步(HomeAssets assets,IEnumerable<Vector2Int> source)
    {
        最近新增=最近移除=最近移动=最近保留=0;
        if(pieces.Count==0)foreach(var p in GetComponentsInChildren<HomeBoundaryPiece>(true))pieces[p.键]=p;
        var wanted=new HashSet<string>();
        foreach(var desired in Layout(assets,new HashSet<Vector2Int>(source)))
        {
            wanted.Add(desired.key);
            if(!pieces.TryGetValue(desired.key,out var piece)||!piece)
            {
                var go=desired.wall?new GameObject("家园边界"):Instantiate(desired.prefab);
                go.transform.SetParent(transform,false);piece=go.AddComponent<HomeBoundaryPiece>();piece.键=desired.key;piece.空气墙=desired.wall;
                if(desired.wall)go.AddComponent<BoxCollider>();
                pieces[desired.key]=piece;最近新增++;
            }
            else if((piece.transform.localPosition-desired.position).sqrMagnitude>.000001f||Quaternion.Angle(piece.transform.localRotation,desired.rotation)>.01f)最近移动++;
            else 最近保留++;
            piece.transform.localPosition=desired.position;piece.transform.localRotation=desired.rotation;
            if(desired.wall){piece.transform.localScale=Vector3.one;piece.GetComponent<BoxCollider>().size=desired.scale;}
            else piece.transform.localScale=desired.scale;
        }
        var removed=new List<string>();
        foreach(var pair in pieces)if(!wanted.Contains(pair.Key))
        {
            if(pair.Value){pair.Value.gameObject.SetActive(false);if(Application.isPlaying)Destroy(pair.Value.gameObject);else DestroyImmediate(pair.Value.gameObject);}
            removed.Add(pair.Key);最近移除++;
        }
        foreach(var key in removed)pieces.Remove(key);
    }
}
