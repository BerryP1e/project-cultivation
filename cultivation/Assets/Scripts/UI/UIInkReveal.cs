using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>逐三角形裁出纸面宽度；不改 Image.type，不移动/缩放子文字。</summary>
public class UIInkReveal : BaseMeshEffect
{
    float progress = 1;
    float offsetY;
    public float 上浮 { get => offsetY; set { offsetY = value; if (graphic != null) graphic.SetVerticesDirty(); } }
    public float 进度 { get => progress; set { progress = Mathf.Clamp01(value); if (graphic != null) graphic.SetVerticesDirty(); } }
    readonly List<UIVertex> vertices = new List<UIVertex>();
    readonly List<UIVertex> polygon = new List<UIVertex>(4);
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || progress >= .9999f && Mathf.Approximately(offsetY, 0)) return;
        vertices.Clear(); vh.GetUIVertexStream(vertices); vh.Clear();
        float edge = Mathf.Lerp(graphic.rectTransform.rect.xMin, graphic.rectTransform.rect.xMax, progress);
        for (int n = 0; n + 2 < vertices.Count; n += 3)
        {
            polygon.Clear();
            for (int k = 0; k < 3; k++)
            {
                var a = vertices[n + k]; var b = vertices[n + (k + 1) % 3];
                a.position.y += offsetY; b.position.y += offsetY;
                bool insideA = a.position.x <= edge, insideB = b.position.x <= edge;
                if (insideA) polygon.Add(a);
                if (insideA == insideB) continue;
                float t = (edge - a.position.x) / (b.position.x - a.position.x);
                var p = a; p.position = Vector3.Lerp(a.position, b.position, t); p.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
                p.color = Color.Lerp(a.color, b.color, t); polygon.Add(p);
            }
            for (int k = 1; k + 1 < polygon.Count; k++)
            {
                int start = vh.currentVertCount; vh.AddVert(polygon[0]); vh.AddVert(polygon[k]); vh.AddVert(polygon[k + 1]); vh.AddTriangle(start, start + 1, start + 2);
            }
        }
    }
}
