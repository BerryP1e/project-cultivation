using UnityEngine;

/// <summary>玉符的八边形命中区：保留中心可点击，透明四角不截获射线。</summary>
public class InkUIHitShape : MonoBehaviour, ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        var rt = transform as RectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPoint, eventCamera, out var p)) return false;
        var r = rt.rect;
        if (r.width <= 0 || r.height <= 0) return false;
        float x = Mathf.Abs((p.x - r.center.x) / (r.width * .5f));
        float y = Mathf.Abs((p.y - r.center.y) / (r.height * .5f));
        return x <= 1 && y <= 1 && x + y <= 1.55f;
    }
}
