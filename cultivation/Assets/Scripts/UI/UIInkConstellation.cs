using UnityEngine;
using UnityEngine.UI;

/// <summary>端点跟随星位；神通页以游离细线和流动墨粒子连接，HUD 保留轻量连线。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkConstellation : MaskableGraphic
{
    public UIInkSkillStar[] Nodes;
    public UIInkHudSkill[] HudNodes;
    public float Progress=1;
    public int SegmentCount {get;private set;}
    public int 游离粒子数 {get;private set;}
    float nextRefresh;
    protected override void OnPopulateMesh(VertexHelper vh){
        vh.Clear();SegmentCount=0;游离粒子数=0;int count=HudNodes!=null?HudNodes.Length:Nodes!=null?Nodes.Length:0;
        for(int n=0;n+1<count;n++){
            Vector2 a=rectTransform.InverseTransformPoint(HudNodes!=null?HudNodes[n].视觉位置:Nodes[n].视觉位置),b=rectTransform.InverseTransformPoint(HudNodes!=null?HudNodes[n+1].视觉位置:Nodes[n+1].视觉位置);
            Vector2 bend=(a+b)*.5f+(HudNodes!=null?new Vector2(0,n%2==0?-16:16):new Vector2((n%2==0?1:-1)*55,0));
            if(HudNodes==null){DrawFlow(vh,a,b,bend,n,count);continue;}
            Vector2 last=a;
            for(int i=1;i<=28;i++){
                float t=i/28f;if((n+t)/(count-1)>Progress)break;
                Vector2 p=(1-t)*(1-t)*a+2*(1-t)*t*bend+t*t*b;
                float flicker=UIInkMotion.减少动效?1:.8f+.2f*Mathf.Sin(Time.unscaledTime*1.1f+n+t*5);
                Add(vh,last,p,4,new Color(.55f,.77f,.70f,.18f*flicker));
                Add(vh,last,p,1.2f+.4f*Mathf.Sin(t*30+n),new Color(.80f,.92f,.84f,.88f*flicker));last=p;SegmentCount++;
            }
        }
    }
    void DrawFlow(VertexHelper vh, Vector2 a, Vector2 b, Vector2 bend, int link, int count)
    {
        float time=UIInkMotion.减少动效?0:Time.unscaledTime;
        var delta=b-a;if(delta.sqrMagnitude<.01f)return;
        var normal=new Vector2(-delta.y,delta.x).normalized;
        for(int strand=0;strand<4;strand++)
        {
            Vector2 last=a;
            for(int j=1;j<=28;j++)
            {
                float t=j/28f;if((link+t)/(count-1)>Progress)break;
                var p=FlowPoint(a,b,bend,normal,t,time,link,strand);
                float fade=.45f+.55f*Mathf.Sin(Mathf.PI*t);
                var tint=strand%2==0?new Color(.69f,.85f,.77f,.64f*fade):new Color(.80f,.75f,.56f,.52f*fade);
                if(strand==0)Add(vh,last,p,2.3f,new Color(.62f,.82f,.74f,.09f));
                Add(vh,last,p,strand==0?.72f:.45f,tint);last=p;SegmentCount++;
            }
        }
        if(UIInkMotion.减少动效)return;
        for(int particle=0;particle<18;particle++)
        {
            float phase=particle*.618034f+link*.271f;
            float t=Mathf.Repeat(time*(.11f+particle%3*.018f)+phase,1);
            if((link+t)/(count-1)>Progress)continue;
            float fade=Mathf.Clamp01(t/.14f)*Mathf.Clamp01((1-t)/.18f);
            var p=FlowPoint(a,b,bend,normal,t,time,link,particle%4)+normal*Mathf.Sin(time*.9f+phase*8)*3.5f;
            float radius=1.7f+(particle%4)*.35f;
            var tint=particle%4==0?new Color(.97f,.89f,.64f,.95f*fade):new Color(.77f,.96f,.88f,.9f*fade);
            Disc(vh,p,radius*2.4f,new Color(tint.r,tint.g,tint.b,tint.a*.16f));
            Disc(vh,p,radius,tint);游离粒子数++;
        }
    }
    static Vector2 FlowPoint(Vector2 a,Vector2 b,Vector2 bend,Vector2 normal,float t,float time,int link,int strand)
    {
        var p=(1-t)*(1-t)*a+2*(1-t)*t*bend+t*t*b;
        float drift=Mathf.Sin(t*Mathf.PI)*((strand-1.5f)*2.4f+Mathf.Sin(t*8+time*(.6f+strand*.13f)+link*1.4f+strand)*5);
        return p+normal*drift;
    }
    static void Disc(VertexHelper vh,Vector2 center,float radius,Color tint)
    {
        const int slices=10;int start=vh.currentVertCount;
        vh.AddVert(center,tint,Vector2.zero);var edge=tint;edge.a=0;
        for(int i=0;i<=slices;i++){float a=i*Mathf.PI*2/slices;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,edge,Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1);}
    }
    static void Add(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color){
        var delta=b-a;if(delta.sqrMagnitude<.001f)return;var normal=new Vector2(-delta.y,delta.x).normalized*width;int start=vh.currentVertCount;
        vh.AddVert(a-normal,color,Vector2.zero);vh.AddVert(a+normal,color,Vector2.zero);vh.AddVert(b+normal,color,Vector2.zero);vh.AddVert(b-normal,color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
    }
    void LateUpdate(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+1f/30;SetVerticesDirty();}
}
