using UnityEngine;
using UnityEngine.UI;

/// <summary>丹炉的蒸气、炉火灵光与出丹飞散微粒；顶点粒子持续运动，无截图帧序列。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkAlchemyFX : MaskableGraphic
{
    public float 热度;
    public float 开盖度;
    public float 爆发时刻=-100;
    public bool 成功;
    protected override void Awake() { base.Awake();raycastTarget=false; }
    void Update() { if(热度>0 || 开盖度>.01f || Time.unscaledTime-爆发时刻<1.6f)SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float now=Time.unscaledTime;var size=rectTransform.rect.size;
        Vector2 mouth=new Vector2(0,size.y*.29f);
        for(int i=0;i<28;i++)
        {
            float phase=Mathf.Repeat(now*(.24f+i%3*.04f)+i*.618f,1);
            float power=Mathf.Max(热度,开盖度*.55f);
            if(power<.01f)break;
            float x=Mathf.Sin(i*2.17f+phase*4)*size.x*(.08f+phase*.06f);
            var p=mouth+new Vector2(x,phase*size.y*.22f);
            float alpha=Mathf.Sin(phase*Mathf.PI)*power*.5f;
            Disc(vh,p,(7+phase*20)*(.7f+i%4*.2f),new Color(.72f,.86f,.79f,alpha));
        }
        for(int i=0;i<32 && 热度>.01f;i++)
        {
            float angle=now*(.8f+i%2*.15f)+i*Mathf.PI*2/32;
            var p=new Vector2(Mathf.Cos(angle)*size.x*.23f, -size.y*.40f+Mathf.Sin(angle)*size.y*.047f);
            Disc(vh,p,4+i%3,new Color(.79f,.87f,.56f,热度*(.5f+.4f*(.5f+.5f*Mathf.Sin(angle*3)))));
        }
        if(热度>.01f)
            for(int layer=0;layer<3;layer++)
                for(int j=0;j<22;j++)
                {
                    float t=j/21f;
                    Vector2 P(float u)=>mouth+new Vector2(Mathf.Sin(u*5-now*1.6f+layer*2)*size.x*(.025f+u*.035f),u*size.y*.24f);
                    Stroke(vh,P(t),P(t+1/21f),1.4f,new Color(.69f,.84f,.74f,热度*.25f*Mathf.Sin(t*Mathf.PI)));
                }
        float burst=now-爆发时刻;
        if(burst>=0 && burst<1.6f)
            for(int i=0;i<42;i++)
            {
                float a=i*2.39996f;
                var p=mouth+new Vector2(Mathf.Cos(a)*burst*(70+i%5*16), Mathf.Sin(a)*burst*(55+i%4*13)+burst*90-burst*burst*85);
                Disc(vh,p,2+i%3,成功?new Color(.91f,.88f,.58f,(1-burst/1.6f)*.8f):new Color(.55f,.62f,.58f,(1-burst/1.6f)*.6f));
            }
    }
    static void Disc(VertexHelper vh,Vector2 p,float radius,Color tint)
    {
        int start=vh.currentVertCount;vh.AddVert(p,tint,Vector2.zero);
        var edge=tint;edge.a=0;
        for(int j=0;j<9;j++){float a=j*Mathf.PI/4;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,edge,Vector2.zero);}
        for(int j=0;j<8;j++)vh.AddTriangle(start,start+j+1,start+j+2);
    }
    static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
    {
        var delta=b-a;var normal=new Vector2(-delta.y,delta.x).normalized*width;
        int n=vh.currentVertCount;vh.AddVert(a-normal,color,Vector2.zero);vh.AddVert(a+normal,color,Vector2.zero);
        vh.AddVert(b+normal,color,Vector2.zero);vh.AddVert(b-normal,color,Vector2.zero);
        vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
    }
}
