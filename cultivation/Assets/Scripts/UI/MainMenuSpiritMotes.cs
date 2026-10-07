using UnityEngine;
using UnityEngine.UI;

/// <summary>One UI mesh for drifting spirit motes; no per-frame object allocation.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MainMenuSpiritMotes : MaskableGraphic
{
    protected override void Awake() { base.Awake(); raycastTarget=false; }
    void Update() { SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var rect=rectTransform.rect; float t=Time.unscaledTime;
        for(int i=0;i<64;i++) {
            float seed=i*137.53f;
            float x=Mathf.Repeat(seed+t*(3+i%5),1920)/1920*rect.width+rect.xMin;
            float y=Mathf.Repeat(seed*.713f+t*(5+i%7),1080)/1080*rect.height+rect.yMin;
            float alpha=(.18f+.22f*Mathf.Sin(t*.7f+i))*Mathf.Clamp01((x-rect.xMin-330)/220);
            float r=1+i%3; int b=vh.currentVertCount;
            Color c=new Color(.54f,.63f,.54f,alpha);
            vh.AddVert(new Vector3(x-r,y),c,Vector2.zero); vh.AddVert(new Vector3(x,y+r),c,Vector2.zero);
            vh.AddVert(new Vector3(x+r,y),c,Vector2.zero); vh.AddVert(new Vector3(x,y-r),c,Vector2.zero);
            vh.AddTriangle(b,b+1,b+2); vh.AddTriangle(b,b+2,b+3);
        }
        for(int i=0;i<110;i++) {
            float seed=i*.618034f;
            float life=Mathf.Repeat(seed+t/(7+i%6),1);
            float alpha=Mathf.SmoothStep(0,1,life*8)*(1-Mathf.SmoothStep(.83f,1,life));
            for(int tail=0;tail<4;tail++) {
                float u=Mathf.Max(0,life-tail*.005f);
                float radius=365*Mathf.Pow(1-u,.68f);
                float angle=seed*37+u*9+t*.09f;
                float x=(990+Mathf.Cos(angle)*radius)/1920*rect.width+rect.xMin;
                float y=(550+Mathf.Sin(angle)*radius)/1080*rect.height+rect.yMin;
                float r=(1.2f+i%4*.5f)*(1-tail*.14f); int b=vh.currentVertCount;
                Color c=i%5==0?new Color(.77f,.76f,.64f,alpha*.65f/(1+tail)):new Color(.54f,.66f,.56f,alpha*.65f/(1+tail));
                vh.AddVert(new Vector3(x-r,y),c,Vector2.zero); vh.AddVert(new Vector3(x,y+r),c,Vector2.zero);
                vh.AddVert(new Vector3(x+r,y),c,Vector2.zero); vh.AddVert(new Vector3(x,y-r),c,Vector2.zero);
                vh.AddTriangle(b,b+1,b+2); vh.AddTriangle(b,b+2,b+3);
            }
        }
    }
}
