using UnityEngine;
using UnityEngine.UI;

/// <summary>条前端渐隐笔锋；交付笔锋素材存在时使用它，否则为柔边单笔几何。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkBrushTip : MaskableGraphic
{
    public Image 填充;
    Sprite brush;
    public override Texture mainTexture => brush != null ? brush.texture : base.mainTexture;
    protected override void Awake() { base.Awake(); raycastTarget = false; brush = InkUITheme.Load("Dynamic/fx-brush-tip") ?? InkUITheme.Load("Effects/fx-brush-tip"); }
    void LateUpdate()
    {
        if (填充 == null) return;
        bool show = 填充.fillAmount > .001f && 填充.fillAmount < .999f;
        canvasRenderer.SetAlpha(show ? 1 : 0);
        var parent = 填充.rectTransform;
        rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(填充.fillAmount, .5f);
        rectTransform.pivot = new Vector2(.5f, .5f); rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(18, parent.rect.height);
        if (color != 填充.color) color = 填充.color;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r = rectTransform.rect;
        var uv = brush != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(brush) : new Vector4(0, 0, 1, 1);
        if (brush != null)
        {
            vh.AddVert(new Vector3(r.xMin, r.yMin), color, new Vector2(uv.x, uv.y));
            vh.AddVert(new Vector3(r.xMin, r.yMax), color, new Vector2(uv.x, uv.w));
            vh.AddVert(new Vector3(r.xMax, r.yMax), color, new Vector2(uv.z, uv.w));
            vh.AddVert(new Vector3(r.xMax, r.yMin), color, new Vector2(uv.z, uv.y));
            vh.AddTriangle(0, 1, 2); vh.AddTriangle(2, 3, 0); return;
        }
        var clear = color; clear.a = 0;
        vh.AddVert(new Vector3(r.xMin, r.yMin), color, new Vector2(uv.x, uv.y));
        vh.AddVert(new Vector3(r.xMin, r.yMax), color, new Vector2(uv.x, uv.w));
        vh.AddVert(new Vector3(r.xMax, r.center.y), clear, new Vector2(uv.z, (uv.y + uv.w) * .5f));
        vh.AddTriangle(0, 1, 2);
    }
}
