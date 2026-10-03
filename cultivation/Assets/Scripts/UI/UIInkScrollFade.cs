using UnityEngine;
using UnityEngine.UI;

/// <summary>列表两端纸毛边式淡出；仍由原 RectMask2D 决定裁剪和命中。</summary>
public class UIInkScrollFade : BaseMeshEffect
{
    ScrollRect scroll;
    Vector2 previous;
    protected override void OnEnable() { base.OnEnable(); scroll = GetComponentInParent<ScrollRect>(); }
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || scroll == null || scroll.viewport == null || !scroll.vertical) return;
        var viewport = scroll.viewport; var box = viewport.rect;
        UIVertex vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            float y = viewport.InverseTransformPoint(graphic.rectTransform.TransformPoint(vertex.position)).y;
            float fade = Mathf.Clamp01(Mathf.Min(y - box.yMin, box.yMax - y) / 14f);
            var c = vertex.color; c.a = (byte)(c.a * fade); vertex.color = c; vh.SetUIVertex(vertex, i);
        }
    }
    void LateUpdate()
    {
        if (scroll == null || scroll.content == null) return;
        var position = scroll.content.anchoredPosition;
        if (position != previous) { previous = position; graphic.SetVerticesDirty(); }
    }
}
