using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>拖动整块布阵预览绕中心查看；不改阵型、模型位置或世界相机。</summary>
public class UIInkFormationOrbit : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
{
    public Vector2 查看角度 => new Vector2(yaw, pitch);
    public float 查看距离 => distance;
    public bool 正在查看 => dragging;
    Camera view;
    Transform stage;
    RectTransform area;
    Canvas canvas;
    Vector2 previousPointer;
    float yaw, pitch, distance, initialPitch, initialDistance;
    bool dragging;
    readonly Vector3 focus = new Vector3(0, .7f, 0);

    public void Initialize(Camera camera, Transform previewStage)
    {
        view = camera;
        stage = previewStage;
        area = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();
        var offset = view.transform.localPosition - focus;
        initialDistance = offset.magnitude;
        initialPitch = Mathf.Atan2(offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg;
        复位();
    }

    public void 复位()
    {
        dragging = false;
        yaw = 0;
        pitch = initialPitch;
        distance = initialDistance;
        ApplyView();
    }

    void ApplyView()
    {
        if (view == null || stage == null) return;
        if (area != null && area.rect.height > 1) view.aspect = area.rect.width / area.rect.height;
        var direction = Quaternion.Euler(pitch, yaw, 0) * Vector3.back;
        // Diagonal views take more vertical space. Keep the entire floor and models
        // inside the render target instead of cutting off the nearest corner.
        float framedDistance = distance;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            view.transform.localPosition = focus + direction * framedDistance;
            view.transform.LookAt(stage.TransformPoint(focus));
            bool fits = true;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3((corner & 1) == 0 ? -4.6f : 4.6f,
                    (corner & 2) == 0 ? 0 : 1.9f, (corner & 4) == 0 ? -4.6f : 4.6f);
                var p = view.WorldToViewportPoint(stage.TransformPoint(point));
                fits &= p.z > 0 && p.x >= .08f && p.x <= .92f && p.y >= .10f && p.y <= .94f;
            }
            if (fits) break;
            framedDistance += .35f;
        }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (view == null || UIDragContext.Dragging || e.button != PointerEventData.InputButton.Left) return;
        dragging = true;
        previousPointer = e.position;
        // A drag beginning on a slot must not become an equipment click on release.
        e.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging || UIDragContext.Dragging) return;
        var delta = e.position - previousPointer;
        previousPointer = e.position;
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1;
        yaw = Mathf.Repeat(yaw + delta.x / Mathf.Max(1, area.rect.width * scale) * 180, 360);
        e.eligibleForClick = false;
        ApplyView();
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (dragging) e.eligibleForClick = false;
        dragging = false;
    }

    public void OnScroll(PointerEventData e)
    {
        if (view == null || UIDragContext.Dragging) return;
        float actualDistance = (view.transform.localPosition - focus).magnitude;
        distance = Mathf.Clamp(actualDistance - e.scrollDelta.y * .8f, 15, 36);
        ApplyView();
    }

    void OnDisable() { dragging = false; }
}
