using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>独立于人物朝向的贴地方向标。只生成一次网格。</summary>
[DisallowMultipleComponent]
public sealed class PlayerAimWheel:MonoBehaviour
{
    public float 半径=.72f;
    PlayerManualAim aim;GameObject visual;Mesh mesh;Material material;
    readonly RaycastHit[] groundHits=new RaycastHit[24];bool visible;
    public void 显示(bool value){visible=value;if(visual)visual.SetActive(value);}
    void Awake()
    {
        aim=GetComponent<PlayerManualAim>();visual=new GameObject("鼠标方向轮盘");visual.layer=2;
        material=new Material(Shader.Find("Sprites/Default")){name="方向轮盘_白玉"};
        var r=visual.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        mesh=new Mesh{name="方向轮盘"};visual.AddComponent<MeshFilter>().sharedMesh=mesh;
        var v=new List<Vector3>();var c=new List<Color>();var t=new List<int>();
        Arc(v,c,t,半径-.012f,半径+.012f,-180,180,72,new Color(1,.96f,.83f,.22f));
        Arc(v,c,t,半径-.023f,半径+.023f,-58,58,32,new Color(1,.98f,.88f,.95f));
        int k=v.Count;v.Add(new Vector3(-.13f,0,半径-.06f));v.Add(new Vector3(0,0,半径+.16f));v.Add(new Vector3(.13f,0,半径-.06f));
        c.Add(Color.white);c.Add(Color.white);c.Add(Color.white);t.Add(k);t.Add(k+1);t.Add(k+2);
        mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(t,0);mesh.RecalculateBounds();visual.SetActive(false);
    }
    static void Arc(List<Vector3> v,List<Color> c,List<int> t,float inner,float outer,float start,float end,int n,Color color)
    {
        int first=v.Count;
        for(int i=0;i<=n;i++){float a=Mathf.Lerp(start,end,(float)i/n)*Mathf.Deg2Rad;var p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));v.Add(p*inner);v.Add(p*outer);c.Add(color);c.Add(color);if(i==n)continue;int k=first+i*2;t.Add(k);t.Add(k+1);t.Add(k+2);t.Add(k+2);t.Add(k+1);t.Add(k+3);}
    }
    void LateUpdate()
    {
        if(!visible||!aim||!aim.鼠标落点有效||!visual)return;
        Vector3 point=transform.position,normal=Vector3.up;float nearest=float.PositiveInfinity;
        int count=Physics.RaycastNonAlloc(point+Vector3.up*2,Vector3.down,groundHits,200,~(1<<2),QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++){var hit=groundHits[i];if(hit.transform.IsChildOf(transform)||hit.collider.GetComponentInParent<NpcInstance>()||hit.normal.y<.4f||hit.distance>=nearest)continue;nearest=hit.distance;point=hit.point;normal=hit.normal;}
        if(float.IsPositiveInfinity(nearest)&&Terrain.activeTerrain){var terrain=Terrain.activeTerrain;point.y=terrain.SampleHeight(point)+terrain.transform.position.y;}
        var forward=Vector3.ProjectOnPlane(aim.鼠标方向,normal);if(forward.sqrMagnitude<.001f)forward=Vector3.ProjectOnPlane(transform.forward,normal);
        visual.transform.SetPositionAndRotation(point+normal*.035f,Quaternion.LookRotation(forward,normal));
    }
    void OnDisable(){显示(false);}
    void OnDestroy(){if(visual)Destroy(visual);if(mesh)Destroy(mesh);if(material)Destroy(material);}
}
