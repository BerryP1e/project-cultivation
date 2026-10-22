using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>宣纸内沿用原境界墨圈的弧形属性、墨迹悬停、数字缓动与视口渐隐。</summary>
[DefaultExecutionOrder(11000)]
public sealed class UIInkRealmAttributeFlow : MonoBehaviour
{
    public Image 墨圈;
    public UIAttributeList 属性;
    public ScrollRect 属性滚动;
    readonly Dictionary<RectTransform, UIInkReveal[]> rows = new Dictionary<RectTransform, UIInkReveal[]>();
    UIInkViewportFade fade;
    float age;

    void OnEnable()
    {
        age = 0;
        if (属性滚动) 属性滚动.verticalNormalizedPosition = 1;
    }

    void Prepare(RectTransform row)
    {
        var hover = row.GetComponent<UIInkRealmRow>() ?? row.gameObject.AddComponent<UIInkRealmRow>();
        hover.宣纸 = true;
        hover.Initialize(属性.font);
        var reveals = new List<UIInkReveal>();
        foreach (var graphic in row.GetComponentsInChildren<Graphic>())
        {
            if (graphic.transform == row) continue;
            if (graphic is Text text)
            {
                text.color = UIPaperArt.Ink;
                if (text.name == "Value") UIInkNumber.Attach(text);
                reveals.Add(text.GetComponent<UIInkReveal>() ?? text.gameObject.AddComponent<UIInkReveal>());
            }
            var coordinates = graphic.GetComponent<UIInkFadeCoordinates>() ?? graphic.gameObject.AddComponent<UIInkFadeCoordinates>();
            coordinates.Viewport = 属性滚动.viewport;
            graphic.material = fade.Material;
        }
        // 数值仍贴右边线；名称随圆弧进退，不把整行定宽平移出视口。
        var name = (RectTransform)row.Find("Name");
        name.anchorMin = Vector2.zero;
        name.anchorMax = Vector2.one;
        name.offsetMin = new Vector2(4, 0);
        name.offsetMax = new Vector2(-110, 0);
        var value = (RectTransform)row.Find("Value");
        value.anchorMin = new Vector2(1, 0);
        value.anchorMax = Vector2.one;
        value.offsetMin = new Vector2(-110, 0);
        value.offsetMax = new Vector2(-4, 0);
        rows.Add(row, reveals.ToArray());
    }

    void LateUpdate()
    {
        if (!墨圈 || !属性 || !属性滚动 || !属性滚动.viewport || !属性.container) return;
        age += Time.unscaledDeltaTime;
        var viewport = 属性滚动.viewport;
        if (!fade) fade = viewport.GetComponent<UIInkViewportFade>();
        if (!fade) return;
        var circle = 墨圈.rectTransform;
        var center = viewport.InverseTransformPoint(circle.TransformPoint(circle.rect.center));
        float diameter = Mathf.Min(circle.rect.width, circle.rect.height);
        var edge = viewport.InverseTransformPoint(circle.TransformPoint(circle.rect.center + Vector2.right * diameter * .5f));
        float radius = Mathf.Abs(edge.x - center.x) + diameter * .08f;
        int count = 属性.container.childCount;
        float time = UIInkMotion.减少动效 ? 10 : age;
        for (int i = 0; i < count; i++)
        {
            var row = (RectTransform)属性.container.GetChild(i);
            if (!row.gameObject.activeSelf) continue;
            if (!rows.TryGetValue(row, out var reveals)) { Prepare(row); reveals = rows[row]; }
            float dy = viewport.InverseTransformPoint(row.position).y - center.y;
            float arc = center.x + Mathf.Sqrt(Mathf.Max(0, radius * radius - dy * dy));
            float left = Mathf.Max(viewport.rect.xMin + 8, arc + 4);
            float width = Mathf.Max(1, viewport.rect.xMax - 8 - left);
            row.anchorMin = new Vector2(0, 1);
            row.anchorMax = new Vector2(0, 1);
            row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            row.anchoredPosition = new Vector2(left - viewport.rect.xMin + width * row.pivot.x, row.anchoredPosition.y);
            float progress = Mathf.Clamp01((time - (Mathf.Min(count, 12) - 1 - Mathf.Min(i, 11)) * .04f) / .2f);
            foreach (var reveal in reveals) if (reveal.进度 != progress) reveal.进度 = progress;
        }
    }
}

/// <summary>只驱动属性行纵向位置；横向由圆弧负责，避免画布重排时覆盖刚算好的位置。</summary>
public sealed class UIInkRealmVerticalLayout : VerticalLayoutGroup
{
    public override void SetLayoutHorizontal() { }
}
