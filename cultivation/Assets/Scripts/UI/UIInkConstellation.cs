using UnityEngine;
using UnityEngine.UI;

/// <summary>实时网格连线，端点跟随悬浮星位，不使用带空槽的整张底图。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkConstellation : MaskableGraphic
{
    public UIInkSkillStar[] Nodes;
    public UIInkHudSkill[] HudNodes;
    public float Progress=1;
    public int SegmentCount {get;private set;}
    protected override void OnPopulateMesh(VertexHelper vh){
        vh.Clear();SegmentCount=0;int count=HudNodes!=null?HudNodes.Length:Nodes!=null?Nodes.Length:0;
        for(int n=0;n+1<count;n++){
            Vector2 a=rectTransform.InverseTransformPoint(HudNodes!=null?HudNodes[n].视觉位置:Nodes[n].视觉位置),b=rectTransform.InverseTransformPoint(HudNodes!=null?HudNodes[n+1].视觉位置:Nodes[n+1].视觉位置);
            Vector2 bend=(a+b)*.5f+(HudNodes!=null?new Vector2(0,n%2==0?-16:16):new Vector2((n%2==0?1:-1)*55,0));
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
    static void Add(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color){
        var delta=b-a;if(delta.sqrMagnitude<.001f)return;var normal=new Vector2(-delta.y,delta.x).normalized*width;int start=vh.currentVertCount;
        vh.AddVert(a-normal,color,Vector2.zero);vh.AddVert(a+normal,color,Vector2.zero);vh.AddVert(b+normal,color,Vector2.zero);vh.AddVert(b-normal,color,Vector2.zero);
        vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
    }
    void LateUpdate(){SetVerticesDirty();}
}
