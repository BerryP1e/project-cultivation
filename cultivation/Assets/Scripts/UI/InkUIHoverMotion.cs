using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>暂停时也能工作的轻微浮起和透视倾斜；不改变命中框和布局。</summary>
public class InkUIHoverMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public Transform visual;
    bool hovered;
    Vector2 tilt;
    public void OnPointerEnter(PointerEventData e) { hovered = true; OnPointerMove(e); }
    public void OnPointerExit(PointerEventData e) { hovered = false; tilt = Vector2.zero; }
    public void OnPointerMove(PointerEventData e)
    {
        var rt = transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.enterEventCamera, out var p))
            tilt = new Vector2(-p.y / Mathf.Max(1, rt.rect.height), p.x / Mathf.Max(1, rt.rect.width)) * 10;
    }
    void LateUpdate()
    {
        if (visual == null) return;
        float t = 1 - Mathf.Exp(-18 * Time.unscaledDeltaTime);
        visual.localScale = Vector3.Lerp(visual.localScale, Vector3.one * (hovered ? 1.055f : 1), t);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, hovered ? Quaternion.Euler(tilt.x, tilt.y, 0) : Quaternion.identity, t);
    }
    void OnDisable() { hovered = false; if (visual != null) { visual.localScale = Vector3.one; visual.localRotation = Quaternion.identity; } }
}
