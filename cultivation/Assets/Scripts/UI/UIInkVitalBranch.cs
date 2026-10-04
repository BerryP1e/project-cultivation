using UnityEngine;
using UnityEngine.UI;

/// <summary>连续朱红气血与靛蓝灵力，细枝白骨轮廓。进度跟随原 HUD 填充数据。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkVitalBranch : MaskableGraphic
{
    public Image Source;
    public bool Mana;
    float previous = -1;
    void Update() { float f = Source != null ? Source.fillAmount : 1; if (Mathf.Abs(f - previous) > .0005f) { previous = f; SetVerticesDirty(); } }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r = rectTransform.rect; float ratio = Source != null ? Source.fillAmount : 1;
        var light = new Color(.83f, .85f, .78f, .88f);
        var fill = Mana ? new Color(.32f, .48f, .78f, .98f) : new Color(.72f, .10f, .15f, .98f);
        float start = r.xMin + 30, length = r.width - 62;
        for (int i = 0; i < 90; i++)
        {
            float t = i / 90f, next = (i + 1) / 90f;
            Vector2 a = Path(start, length, t), b = Path(start, length, next);
            Stroke(vh, a, b, Mana ? 9 : 11, new Color(.02f, .035f, .045f, .85f));
            Stroke(vh, a + Vector2.up * 5.5f, b + Vector2.up * 5.5f, 1.2f, light);
            Stroke(vh, a - Vector2.up * 5.5f, b - Vector2.up * 5.5f, 1, light * new Color(1, 1, 1, .65f));
            if (t < ratio) { var end = Path(start, length, Mathf.Min(next, ratio)); Stroke(vh, a, end, Mana ? 4 : 7, fill); Stroke(vh, a + Vector2.up, end + Vector2.up, 1, Color.Lerp(fill, Color.white, .38f)); }
        }
        // 分叉有粗细衰减，不按血量分格。蓝色根脉比红色主干更细长。
        for (int i = 0; i < 7; i++)
        {
            float t = .025f + i * .146f + Mathf.Sin(i * 3.7f) * .033f;
            var p = Path(start, length, t); float sign = i % 2 == 0 ? 1 : -1;
            float height = (Mana ? 13 : 9) + Mathf.Sin(i * 2.9f) * 4;
            var q = p + new Vector2(8 + Mathf.Sin(i) * 3, sign * height);
            var tip = q + new Vector2(10 + i % 3 * 3, sign * 4);
            Vector2 branchPoint = p;
            for (int j = 1; j <= 8; j++)
            {
                float k = j / 8f;
                var next = Vector2.Lerp(p, q, k) + new Vector2(Mathf.Sin(k * Mathf.PI) * 3, 0);
                Stroke(vh, branchPoint, next, Mathf.Lerp(1.9f, .7f, k), light); branchPoint = next;
            }
            Stroke(vh, q, tip, .7f, light);
            if (i % 3 != 1) Stroke(vh, q, q + new Vector2(-4, sign * 7), .5f, light);
        }
        var first = Path(start, length, 0); var last = Path(start, length, 1);
        Stroke(vh, first - new Vector2(26, 2), first, 2.4f, light);
        Stroke(vh, last, last + new Vector2(23, 8), 1.4f, light);
    }
    Vector2 Path(float x, float length, float t) => new Vector2(x + length * t,
        Mathf.Sin(t * 19 + (Mana ? .8f : 0)) * (Mana ? 2.8f : 1.4f) + Mathf.Sin(t * 43) * .6f);
    static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = b - a; if (d.sqrMagnitude < .0001f) return;
        Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
        int index = vh.currentVertCount;
        vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero);
        vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
    }
}
