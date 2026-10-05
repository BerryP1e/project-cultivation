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
        vh.Clear(); var r = rectTransform.rect;
        Quad(vh, r, new Color(.08f,.085f,.09f,.65f));
        r.width *= Mathf.Clamp01(Source != null ? Source.fillAmount : 1);
        Quad(vh, r, Mana ? new Color(.02f,.62f,.84f) : new Color(.88f,.09f,.15f));
    }
    static void Quad(VertexHelper vh, Rect r, Color c)
    {
        int i=vh.currentVertCount;
        vh.AddVert(new Vector2(r.xMin,r.yMin),c,Vector2.zero); vh.AddVert(new Vector2(r.xMin,r.yMax),c,Vector2.zero);
        vh.AddVert(new Vector2(r.xMax,r.yMax),c,Vector2.zero); vh.AddVert(new Vector2(r.xMax,r.yMin),c,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
    }
}
