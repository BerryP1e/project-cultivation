using UnityEngine;
using UnityEngine.UI;

/// <summary>连续细条，进度读取原 Filled Image。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkVitalBranch : MaskableGraphic
{
    public Image Source;
    public bool Mana;
    float previous = -1;
    void Update()
    {
        float value = Source != null ? Source.fillAmount : 1;
        if (Mathf.Abs(value - previous) > .0005f) { previous = value; SetVerticesDirty(); }
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;float ratio=Mathf.Clamp01(Source!=null?Source.fillAmount:1);
        float start=r.xMin+8,length=r.width-22;
        var bone=new Color(.85f,.81f,.78f,.94f);var ink=new Color(.025f,.015f,.025f,.94f);
        for(int i=0;i<96;i++)
        {
            float t=i/96f,n=(i+1)/96f;var a=Path(r,start,length,t);var b=Path(r,start,length,n);
            float width=Mana?3.4f:9+Mathf.Sin(t*12)*1.3f;
            UIInkHudOrbit.Line(vh,a,b,width+4,new Color(.03f,.035f,.04f,.7f));
            if(!Mana)UIInkHudOrbit.Line(vh,a,b,width+2.1f,bone);
            UIInkHudOrbit.Line(vh,a,b,width,ink);
            if(t<ratio){var end=Path(r,start,length,Mathf.Min(n,ratio));var c=Mana?new Color(.12f,.11f,.34f):Color.Lerp(new Color(.36f,.025f,.05f),new Color(.69f,.09f,.12f),.5f+.5f*Mathf.Sin(t*17));UIInkHudOrbit.Line(vh,a,end,width-1,c);}
        }
        // 大尺度、不对称的分叉；枝尖收细，避免规则锯齿与多条平行噪线。
        int count=Mana?7:5;
        for(int i=0;i<count;i++)
        {
            float t=.05f+i*(Mana?.146f:.205f);var p=Path(r,start,length,t);float sign=i%2==0?1:-1;
            var mid=p+new Vector2(7,sign*(Mana?11:5));var tip=p+new Vector2(22+i%3*5,sign*(Mana?19:10));Vector2 last=p;
            for(int j=1;j<=12;j++){float k=j/12f;var q=(1-k)*(1-k)*p+2*(1-k)*k*mid+k*k*tip;UIInkHudOrbit.Line(vh,last,q,Mathf.Lerp(Mana?2.8f:2.2f,.35f,k),i<2||!Mana?bone:new Color(.20f,.18f,.38f,.95f));last=q;}
            if(i%2==0){var fork=Vector2.Lerp(mid,tip,.35f);UIInkHudOrbit.Line(vh,fork,fork+new Vector2(-4,sign*8),.85f,bone);}
        }
    }
    Vector2 Path(Rect r,float start,float length,float t)=>new Vector2(start+length*t,r.center.y+(Mana?Mathf.Sin(t*5.4f)*6+Mathf.Sin(t*18)*1.5f:Mathf.Sin(t*9)*.9f));
    static void Quad(VertexHelper vh, Rect r, Color c)
    {
        int i=vh.currentVertCount;
        vh.AddVert(new Vector2(r.xMin,r.yMin),c,Vector2.zero); vh.AddVert(new Vector2(r.xMin,r.yMax),c,Vector2.zero);
        vh.AddVert(new Vector2(r.xMax,r.yMax),c,Vector2.zero); vh.AddVert(new Vector2(r.xMax,r.yMin),c,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
    }
}
