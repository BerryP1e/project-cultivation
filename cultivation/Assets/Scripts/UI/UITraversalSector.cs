using UnityEngine;
using UnityEngine.UI;

/// <summary>真实环形扇区网格，同时供底色和选中水墨遮罩使用；不生成贴图。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UITraversalSector : MaskableGraphic
{
    public int index, count = 12;
    public float innerRadius = 106, outerRadius = 200, gapDegrees = 4;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        const int steps = 18;
        float span = 360f / count;
        float start = 90 - index * span - gapDegrees * .5f, end = start - span + gapDegrees;
        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.Lerp(start, end, i / (float)steps) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var inside = direction * innerRadius; var outside = direction * outerRadius;
            vh.AddVert(inside, color, inside / (outerRadius * 2) + Vector2.one * .5f);
            vh.AddVert(outside, color, outside / (outerRadius * 2) + Vector2.one * .5f);
        }
        for (int i = 0; i < steps; i++)
        { int a = i * 2; vh.AddTriangle(a, a + 1, a + 2); vh.AddTriangle(a + 2, a + 1, a + 3); }
    }
}
