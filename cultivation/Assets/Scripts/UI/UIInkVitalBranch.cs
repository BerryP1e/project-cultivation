using UnityEngine;
using UnityEngine.UI;

/// <summary>两端封口的枝脉条，光尖与粒子只跟随当前填充末端。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkVitalBranch : MaskableGraphic
{
    public Image Source;
    public bool Mana;
    float previous = -1;
    float nextRefresh;
    float MotionTime=>UIInkMotion.减少动效?0:Time.unscaledTime;
    public Vector2 填充末端=>Path(rectTransform.rect,rectTransform.rect.xMin+8,rectTransform.rect.width-22,Mathf.Clamp01(Source!=null?Source.fillAmount:1));
    void Update()
    {
        float value = Source != null ? Source.fillAmount : 1;
        if (Mathf.Abs(value - previous) > .0005f || (!UIInkMotion.减少动效 && Time.unscaledTime>=nextRefresh))
        { previous=value;nextRefresh=Time.unscaledTime+1f/30;SetVerticesDirty(); }
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
            UIInkHudOrbit.Line(vh,a,b,width+2.1f,Mana?new Color(.54f,.55f,.65f,.68f):bone);
            UIInkHudOrbit.Line(vh,a,b,width,ink);
            if(t<ratio){var end=Path(r,start,length,Mathf.Min(n,ratio));var c=Mana?new Color(.12f,.11f,.34f):Color.Lerp(new Color(.36f,.025f,.05f),new Color(.69f,.09f,.12f),.5f+.5f*Mathf.Sin(t*17));UIInkHudOrbit.Line(vh,a,end,width-1,c);}
        }
        // 大尺度、不对称的分叉；枝尖收细，避免规则锯齿与多条平行噪线。
        int count=Mana?7:2;
        for(int i=0;i<count;i++)
        {
            float t=Mana?.05f+i*.146f:.03f+i*.92f;var p=Path(r,start,length,t);float sign=i%2==0?1:-1;
            var mid=p+new Vector2(7,sign*(Mana?11:5));var tip=p+new Vector2(22+i%3*5,sign*(Mana?19:10));Vector2 last=p;
            for(int j=1;j<=12;j++){float k=j/12f;var q=(1-k)*(1-k)*p+2*(1-k)*k*mid+k*k*tip;UIInkHudOrbit.Line(vh,last,q,Mathf.Lerp(Mana?2.8f:2.2f,.35f,k),i<2||!Mana?bone:new Color(.20f,.18f,.38f,.95f));last=q;}
            if(i%2==0){var fork=Vector2.Lerp(mid,tip,.35f);UIInkHudOrbit.Line(vh,fork,fork+new Vector2(-4,sign*8),.85f,bone);}
        }
        // 首尾封口，端饰沿主干向外收尖，避免左右漏成一条开口槽。
        for(int side=0;side<2;side++)
        {
            var p=Path(r,start,length,side);float half=(Mana?3.4f:9+Mathf.Sin(side*12)*1.3f)*.5f+1;float sign=side==0?-1:1;
            var c=Mana?new Color(.72f,.71f,.79f,.8f):bone;
            UIInkHudOrbit.Line(vh,p+Vector2.up*half,p-Vector2.up*half,1.4f,c);
            var tip=p+Vector2.right*sign*7;
            UIInkHudOrbit.Line(vh,p+Vector2.up*half,tip,1.1f,c);UIInkHudOrbit.Line(vh,p-Vector2.up*half,tip,1.1f,c);
            UIInkHudOrbit.Line(vh,tip,tip+new Vector2(sign*4,3),.65f,c);
        }
        if(ratio<=.001f)return;
        var head=填充末端;var tint=Mana?new Color(.77f,.83f,1):new Color(1,.83f,.74f);
        float pulse=UIInkMotion.减少动效?1:.85f+.15f*Mathf.Sin(MotionTime*3.1f);
        // 纵向小光尖标记真实数值边界，不在全条撒光点。
        for(int i=4;i>=1;i--)UIInkHudOrbit.Disc(vh,head,i*1.8f,new Color(tint.r,tint.g,tint.b,.026f*pulse));
        Diamond(vh,head,1.1f,7*pulse,new Color(tint.r,tint.g,tint.b,.86f));
        Diamond(vh,head,4.5f,.6f,new Color(tint.r,tint.g,tint.b,.55f));
        if(!UIInkMotion.减少动效)
        for(int i=0;i<4;i++)
        {
            float life=Mathf.Repeat(MotionTime*(.65f+i*.08f)+i*.381966f,1);
            var p=head+new Vector2(-life*(4+i),Mathf.Sin(i*2.7f)*life*5);
            float alpha=Mathf.Sin(life*Mathf.PI)*(1-life)*.48f;
            UIInkHudOrbit.Disc(vh,p,1.5f,new Color(tint.r,tint.g,tint.b,alpha*.12f));
            UIInkHudOrbit.Disc(vh,p,.45f,new Color(tint.r,tint.g,tint.b,alpha));
        }
    }
    Vector2 Path(Rect r,float start,float length,float t)=>new Vector2(start+length*t,r.center.y+(Mana?Mathf.Sin(t*5.4f)*6+Mathf.Sin(t*18)*1.5f:Mathf.Sin(t*9)*.9f)+Mathf.Sin(t*13-MotionTime*1.7f)*(Mana?.65f:.2f)*Mathf.Sin(t*Mathf.PI));
    static void Diamond(VertexHelper vh,Vector2 p,float width,float height,Color c)
    {
        int k=vh.currentVertCount;vh.AddVert(p+Vector2.left*width,c,Vector2.zero);vh.AddVert(p+Vector2.up*height,c,Vector2.zero);vh.AddVert(p+Vector2.right*width,c,Vector2.zero);vh.AddVert(p+Vector2.down*height,c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
    }
}
