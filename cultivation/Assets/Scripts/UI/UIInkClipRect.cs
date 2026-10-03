using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>绘制层矩形裁剪；用于展开纸面及离场快照，命中仍走原层级。</summary>
public class UIInkClipRect : BaseMeshEffect
{
    public UIInkReveal 面板;
    public Rect? 固定裁剪;
    float last = -1;
    readonly List<UIVertex> source = new List<UIVertex>();
    readonly List<UIVertex> polygon = new List<UIVertex>(8);
    readonly List<UIVertex> next = new List<UIVertex>(8);
    void LateUpdate()
    {
        if (面板 != null && !Mathf.Approximately(last, 面板.进度)) { last = 面板.进度; graphic.SetVerticesDirty(); }
    }
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        Rect clip;
        if (固定裁剪.HasValue) clip = 固定裁剪.Value;
        else clip = graphic.rectTransform.rect;
        if (面板 != null && 面板.进度 < .9999f)
        {
            var panel = 面板.transform as RectTransform; var r = panel.rect;
            var left = graphic.rectTransform.InverseTransformPoint(panel.TransformPoint(new Vector3(r.xMin, r.yMin)));
            var right = graphic.rectTransform.InverseTransformPoint(panel.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, 面板.进度), r.yMax)));
            var panelClip = Rect.MinMaxRect(left.x, left.y, right.x, right.y);
            clip = 固定裁剪.HasValue ? Rect.MinMaxRect(Mathf.Max(clip.xMin, panelClip.xMin), Mathf.Max(clip.yMin, panelClip.yMin), Mathf.Min(clip.xMax, panelClip.xMax), Mathf.Min(clip.yMax, panelClip.yMax)) : panelClip;
        }
        else if (!固定裁剪.HasValue) return;
        source.Clear(); vh.GetUIVertexStream(source); vh.Clear();
        for (int n = 0; n + 2 < source.Count; n += 3)
        {
            polygon.Clear(); polygon.Add(source[n]); polygon.Add(source[n + 1]); polygon.Add(source[n + 2]);
            Cut(0, clip.xMin, true); Cut(0, clip.xMax, false); Cut(1, clip.yMin, true); Cut(1, clip.yMax, false);
            for (int k = 1; k + 1 < polygon.Count; k++)
            { int i = vh.currentVertCount; vh.AddVert(polygon[0]); vh.AddVert(polygon[k]); vh.AddVert(polygon[k + 1]); vh.AddTriangle(i, i + 1, i + 2); }
        }
    }
    void Cut(int axis, float edge, bool minimum)
    {
        next.Clear();
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
            float av = axis == 0 ? a.position.x : a.position.y, bv = axis == 0 ? b.position.x : b.position.y;
            bool insideA = minimum ? av >= edge : av <= edge, insideB = minimum ? bv >= edge : bv <= edge;
            if (insideA) next.Add(a);
            if (insideA == insideB) continue;
            float t = (edge - av) / (bv - av); var v = a;
            v.position = Vector3.Lerp(a.position, b.position, t); v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t); v.color = Color.Lerp(a.color, b.color, t); next.Add(v);
        }
        polygon.Clear(); polygon.AddRange(next);
    }
}
