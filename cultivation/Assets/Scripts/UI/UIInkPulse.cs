using UnityEngine;
using UnityEngine.UI;

/// <summary>墨晕绘制挂件；优先用交付墨晕图，缺图时用低浓度柔边顶点过渡，绝不抢射线。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkPulse : MaskableGraphic
{
    float elapsed, duration;
    bool ownsBudget;
    Sprite blot;
    public bool 持久;
    public static UIInkPulse Shadow(RectTransform parent)
    {
        var go = new GameObject("InkHoverShadow", typeof(RectTransform), typeof(UIInkPulse), typeof(LayoutElement));
        go.transform.SetParent(parent, false); go.transform.SetAsFirstSibling(); go.GetComponent<LayoutElement>().ignoreLayout = true;
        var shadow = go.GetComponent<UIInkPulse>(); shadow.持久 = true; shadow.raycastTarget = false;
        shadow.blot = InkUITheme.Load("Effects/fx-ink-blot"); shadow.color = new Color(.188f, .239f, .216f, 0);
        shadow.rectTransform.anchorMin = shadow.rectTransform.anchorMax = new Vector2(.5f, .65f);
        shadow.rectTransform.sizeDelta = new Vector2(parent.rect.width * .9f, parent.rect.width * .9f);
        return shadow;
    }
    public void 强度(float value)
    {
        color = new Color(.188f, .239f, .216f, .12f * value);
        rectTransform.localScale = Vector3.one * (1 + .3f * value);
    }
    public override Texture mainTexture => blot != null ? blot.texture : base.mainTexture;
    public static void Emit(RectTransform parent, Vector2 point, float seconds)
    {
        if (parent == null || !parent.gameObject.activeInHierarchy) return;
        if (!UIInkMotion.Acquire()) return;
        var go = new GameObject("InkPulse", typeof(RectTransform), typeof(UIInkPulse), typeof(LayoutElement));
        go.transform.SetParent(parent, false); go.GetComponent<LayoutElement>().ignoreLayout = true;
        var pulse = go.GetComponent<UIInkPulse>(); pulse.ownsBudget = true; pulse.duration = seconds; pulse.raycastTarget = false;
        pulse.color = new Color(.188f, .239f, .216f, .17f); pulse.blot = InkUITheme.Load("Effects/fx-ink-blot");
        var rt = pulse.rectTransform; rt.anchorMin = rt.anchorMax = parent.pivot;
        rt.anchoredPosition = point; rt.sizeDelta = Vector2.one * Mathf.Min(150, Mathf.Max(54, parent.rect.height));
        rt.SetAsLastSibling();
    }
    void Update()
    {
        if (持久) return;
        elapsed += Time.unscaledDeltaTime;
        if (UIInkMotion.减少动效 || elapsed >= duration) { Destroy(gameObject); return; }
        rectTransform.localScale = Vector3.one * Mathf.Lerp(.35f, 1.6f, UIInkMotion.Timing.Cubic(elapsed / duration));
        SetVerticesDirty();
    }
    protected override void OnDestroy() { if (ownsBudget) { UIInkMotion.Release(); ownsBudget = false; } base.OnDestroy(); }
    protected override void OnDisable()
    {
        if (!持久 && ownsBudget) { UIInkMotion.Release(); ownsBudget = false; Destroy(gameObject); }
        base.OnDisable();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float fade = 持久 ? 1 : 1 - Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
        if (blot != null)
        {
            var r = rectTransform.rect; var uv = UnityEngine.Sprites.DataUtility.GetOuterUV(blot);
            var c = color; c.a *= fade;
            vh.AddVert(new Vector3(r.xMin, r.yMin), c, new Vector2(uv.x, uv.y)); vh.AddVert(new Vector3(r.xMin, r.yMax), c, new Vector2(uv.x, uv.w));
            vh.AddVert(new Vector3(r.xMax, r.yMax), c, new Vector2(uv.z, uv.w)); vh.AddVert(new Vector3(r.xMax, r.yMin), c, new Vector2(uv.z, uv.y));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0); return;
        }
        const int segments = 40, rings = 4;
        float radius = rectTransform.rect.width * .5f;
        for (int ring = 0; ring <= rings; ring++)
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments, fraction = ring / (float)rings;
                float irregular = 1 + .045f * Mathf.Sin(angle * 7) + .035f * Mathf.Sin(angle * 11);
                var c = color; c.a *= Mathf.Pow(1 - fraction, 2) * fade;
                vh.AddVert(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * fraction * irregular, c, Vector2.zero);
            }
        for (int ring = 0; ring < rings; ring++)
            for (int i = 0; i < segments; i++)
            { int a = ring * (segments + 1) + i, b = a + segments + 1; vh.AddTriangle(a, b, a + 1); vh.AddTriangle(a + 1, b, b + 1); }
    }
}
