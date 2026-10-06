using UnityEngine;
using UnityEngine.UI;

/// <summary>细边框与回纹角，由网格绘制避免贴图拉伸。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UISectFineFrame : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        Line(vh,new Vector2(r.xMin+16,r.yMin),new Vector2(r.xMax-16,r.yMin));
        Line(vh,new Vector2(r.xMin+16,r.yMax),new Vector2(r.xMax-16,r.yMax));
        Line(vh,new Vector2(r.xMin,r.yMin+16),new Vector2(r.xMin,r.yMax-16));
        Line(vh,new Vector2(r.xMax,r.yMin+16),new Vector2(r.xMax,r.yMax-16));
        foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1})
        {
            var p=new Vector2(sx<0?r.xMin:r.xMax,sy<0?r.yMin:r.yMax);var d=new Vector2(-sx,-sy);
            Vector2 P(float x,float y)=>p+new Vector2(d.x*x,d.y*y);
            Line(vh,P(0,16),P(0,0));Line(vh,P(0,0),P(16,0));
            Line(vh,P(5,18),P(5,5));Line(vh,P(5,5),P(18,5));Line(vh,P(10,5),P(10,10));Line(vh,P(10,10),P(5,10));
        }
    }
    void Line(VertexHelper vh,Vector2 a,Vector2 b)
    {
        var n=new Vector2(-(b-a).y,(b-a).x).normalized*.5f;int i=vh.currentVertCount;
        vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
    }
}
