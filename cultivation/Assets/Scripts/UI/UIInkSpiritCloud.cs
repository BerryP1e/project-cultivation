using UnityEngine;
using UnityEngine.UI;

/// <summary>实时软边墨粒子，颜色和形状由真灵标识确定，不使用具象图标。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkSpiritCloud : MaskableGraphic
{
    float seed;
    public override Texture mainTexture=>Texture2D.whiteTexture;
    public void Initialize(IPanelEntry entry){uint hash=2166136261;foreach(char c in entry.DisplayName)hash=(hash^c)*16777619;seed=hash%1024;
        var palette=new[]{new Color(.50f,.87f,.77f),new Color(.82f,.60f,.94f),new Color(.95f,.68f,.37f),new Color(.51f,.77f,.97f),new Color(.93f,.49f,.52f),new Color(.78f,.87f,.48f)};color=palette[hash%6];raycastTarget=false;SetAllDirty();}
    void Update(){if(!UIInkMotion.减少动效)SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh){
        vh.Clear();float time=UIInkMotion.减少动效?0:Time.unscaledTime;var rect=rectTransform.rect;float size=Mathf.Min(rect.width,rect.height);
        for(int i=0;i<16;i++){
            float phase=seed+i*2.4f,angle=phase+time*(.12f+i%3*.025f),radius=size*(.06f+(i%5)*.043f);Vector2 center=rect.center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)*.76f)*radius;
            float half=size*(.10f+(i%3)*.037f)*(1+.10f*Mathf.Sin(time*1.6f+phase));var tint=color;tint.a=.25f+.22f*(1+Mathf.Sin(time*1.1f+phase))*.5f;int k=vh.currentVertCount;
            vh.AddVert(center,tint,Vector2.one*.5f);var edge=tint;edge.a=0;
            for(int j=0;j<=16;j++){float a=j*Mathf.PI/8,shape=1+.19f*Mathf.Sin(a*3+phase);vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*half*shape,edge,Vector2.one*.5f);if(j>0)vh.AddTriangle(k,k+j,k+j+1);}
        }
    }
}
