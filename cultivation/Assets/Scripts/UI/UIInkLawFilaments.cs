using UnityEngine;
using UnityEngine.UI;

/// <summary>单份 UI 网格绘制流线与软边粒子；不会为每个光点创建对象。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIInkLawFilaments : MaskableGraphic
{
    public int 境界;
    public float 清晰度=1;
    public int 粒子数 {get;private set;}
    float nextRefresh;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();粒子数=0;float time=UIInkMotion.减少动效?0:Time.unscaledTime;
        float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.39f;
        for(int s=0;s<2+境界/3;s++)
        {
            var last=Point(radius,s,0,time);
            for(int j=1;j<=32;j++){float u=j/32f;var p=Point(radius,s,u,time);float alpha=Mathf.Sin(u*Mathf.PI)*.62f*清晰度;Line(vh,last,p,.55f,new Color(.69f,.87f,.79f,alpha));last=p;}
        }
        if(UIInkMotion.减少动效)return;
        for(int i=0;i<6+境界*2;i++)
        {
            float u=Mathf.Repeat(time*(.09f+i%3*.014f)+i*.618034f,1);var p=Point(radius,i%3,u,time);
            float alpha=Mathf.Clamp01(u/.15f)*Mathf.Clamp01((1-u)/.2f)*清晰度;
            Dot(vh,p,(1.1f+i%3*.4f)*(清晰度>.8f?1.2f:1),new Color(.90f,.97f,.79f,alpha));粒子数++;
        }
    }
    Vector2 Point(float r,int s,float u,float t)
    {float a=u*Mathf.PI*1.7f+s*2.1f+t*(s%2==0?.12f:-.16f)+境界*.31f;float drift=Mathf.Sin(a*3+t*.5f+s)*.04f;return new Vector2(Mathf.Cos(a)*r*(1+drift),Mathf.Sin(a)*r*(.68f+drift));}
    static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
    {var d=b-a;if(d.sqrMagnitude<.001f)return;var n=new Vector2(-d.y,d.x).normalized*width;int k=vh.currentVertCount;vh.AddVert(a-n,tint,Vector2.zero);vh.AddVert(a+n,tint,Vector2.zero);vh.AddVert(b+n,tint,Vector2.zero);vh.AddVert(b-n,tint,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);}
    static void Dot(VertexHelper vh,Vector2 p,float radius,Color tint)
    {const int n=10;int k=vh.currentVertCount;vh.AddVert(p,tint,Vector2.zero);var edge=tint;edge.a=0;for(int j=0;j<=n;j++){float a=j*2*Mathf.PI/n;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,edge,Vector2.zero);if(j>0)vh.AddTriangle(k,k+j,k+j+1);}}
    void LateUpdate(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+1f/30;SetVerticesDirty();}
}
