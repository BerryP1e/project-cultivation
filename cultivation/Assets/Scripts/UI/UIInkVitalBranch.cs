using UnityEngine;
using UnityEngine.UI;

/// <summary>普通封闭细框，微光与细粒子只跟随实际填充末端。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkVitalBranch : MaskableGraphic
{
    public Image Source;
    public bool Mana;
    float previous=-1,nextRefresh;
    float MotionTime=>UIInkMotion.减少动效?0:Time.unscaledTime;
    public Vector2 填充末端=>Point(Mathf.Clamp01(Source!=null?Source.fillAmount:1));
    Vector2 Point(float t){var r=rectTransform.rect;return new Vector2(r.xMin+8+(r.width-22)*t,r.center.y);}
    void Update()
    {
        float value=Source!=null?Source.fillAmount:1;
        if(Mathf.Abs(value-previous)>.0005f||(!UIInkMotion.减少动效&&Time.unscaledTime>=nextRefresh))
        {previous=value;nextRefresh=Time.unscaledTime+1f/30;SetVerticesDirty();}
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float ratio=Mathf.Clamp01(Source!=null?Source.fillAmount:1);
        var a=Point(0);var b=Point(1);float half=Mana?3.5f:4.5f;
        var track=new Rect(a.x,a.y-half,b.x-a.x,half*2);
        Quad(vh,new Rect(track.x-2,track.y-2,track.width+4,track.height+4),new Color(.015f,.02f,.025f,.8f));
        Quad(vh,track,new Color(.045f,.05f,.07f,.95f));
        Quad(vh,new Rect(track.x,track.y,track.width*ratio,track.height),Mana?new Color(.17f,.25f,.48f,.98f):new Color(.56f,.06f,.09f,.98f));
        Quad(vh,new Rect(track.x,track.y+track.height*.55f,track.width*ratio,track.height*.3f),Mana?new Color(.36f,.48f,.65f,.30f):new Color(.86f,.27f,.27f,.23f));
        // 普通矩形封边；边框与数值无关，不随亮点波动。
        var rim=new Color(.69f,.67f,.62f,.88f);
        UIInkHudOrbit.Line(vh,a+Vector2.up*half,b+Vector2.up*half,1,rim);
        UIInkHudOrbit.Line(vh,a-Vector2.up*half,b-Vector2.up*half,1,rim);
        UIInkHudOrbit.Line(vh,a-Vector2.up*half,a+Vector2.up*half,1,rim);
        UIInkHudOrbit.Line(vh,b-Vector2.up*half,b+Vector2.up*half,1,rim);
        // 普通框配短小的收尖端饰，不恢复枝杈式轮廓。
        for(int side=0;side<2;side++)
        {
            var end=side==0?a:b;float sign=side==0?-1:1;var tip=end+Vector2.right*sign*6;
            UIInkHudOrbit.Line(vh,end+Vector2.up*half,tip,1,rim);UIInkHudOrbit.Line(vh,end-Vector2.up*half,tip,1,rim);
            Diamond(vh,tip,2.2f,1.4f,new Color(.83f,.81f,.74f,.9f));
        }
        if(ratio<=.001f)return;
        var head=填充末端;var tint=Mana?new Color(.77f,.83f,1):new Color(1,.83f,.74f);
        float pulse=UIInkMotion.减少动效?1:.85f+.15f*Mathf.Sin(MotionTime*3.1f);
        for(int i=4;i>=1;i--)UIInkHudOrbit.Disc(vh,head,i*2,new Color(tint.r,tint.g,tint.b,.055f*pulse));
        Diamond(vh,head,1.1f,9*pulse,new Color(tint.r,tint.g,tint.b,1));
        Diamond(vh,head,5,.8f,new Color(tint.r,tint.g,tint.b,.85f));
        if(!UIInkMotion.减少动效)for(int i=0;i<4;i++)
        {
            float life=Mathf.Repeat(MotionTime*(.65f+i*.08f)+i*.381966f,1);
            var p=head+new Vector2(-life*(4+i),Mathf.Sin(i*2.7f)*life*5);
            float alpha=Mathf.Sin(life*Mathf.PI)*(1-life)*.95f;
            UIInkHudOrbit.Disc(vh,p,2,new Color(tint.r,tint.g,tint.b,alpha*.22f));
            UIInkHudOrbit.Disc(vh,p,.85f,new Color(tint.r,tint.g,tint.b,alpha));
        }
    }
    static void Quad(VertexHelper vh,Rect r,Color c)
    {
        int k=vh.currentVertCount;vh.AddVert(new Vector2(r.xMin,r.yMin),c,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),c,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMax),c,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
    }
    static void Diamond(VertexHelper vh,Vector2 p,float width,float height,Color c)
    {
        int k=vh.currentVertCount;vh.AddVert(p+Vector2.left*width,c,Vector2.zero);vh.AddVert(p+Vector2.up*height,c,Vector2.zero);vh.AddVert(p+Vector2.right*width,c,Vector2.zero);vh.AddVert(p+Vector2.down*height,c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
    }
}
