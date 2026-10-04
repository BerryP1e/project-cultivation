using UnityEngine;
using UnityEngine.UI;

/// <summary>按实际像素宽高生成墨边，正文与长回答都保持一致的晕染宽度。</summary>
[RequireComponent(typeof(Image))]
public class UIInkDialogueBackdrop : MonoBehaviour, ICanvasRaycastFilter
{
    Material ink;
    Image image;
    bool textReady;
    readonly Vector3[] corners = new Vector3[4];
    void Awake()
    {
        image = GetComponent<Image>();
        var shader = Resources.Load<Shader>("UI/InkUI/Dynamic/InkDialogue");
        if (shader == null) return;
        ink = new Material(shader);
        image.sprite = null;
        image.material = ink;
        // 墨底按自身局部坐标裁剪；文字复用 UV1 视口渐隐，点击由下方过滤器限定。
        image.maskable = false;
        UpdateSize();
    }
    void LateUpdate()
    {
        UpdateSize();
        var layout = GetComponent<LayoutElement>();
        var text = GetComponentInChildren<Text>();
        if (layout != null && text != null)
        {
            float height = Mathf.Max(78, text.preferredHeight + 30);
            if (Mathf.Abs(layout.preferredHeight - height) > .5f) layout.preferredHeight = height;
            if (!textReady)
            {
                var fade = GetComponentInParent<UIInkViewportFade>();
                if (fade != null && fade.Material != null)
                {
                    text.material = fade.Material;
                    text.gameObject.AddComponent<UIInkFadeCoordinates>().Viewport = (RectTransform)fade.transform;
                    textReady = true;
                }
            }
        }
    }
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        var scroll = GetComponentInParent<ScrollRect>();
        return scroll == null || scroll.viewport == null ||
            RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, screenPoint, eventCamera);
    }
    void UpdateSize()
    {
        if (ink == null) return;
        var size = image.rectTransform.rect.size;
        ink.SetVector("_RectSize", new Vector4(size.x, size.y, 0, 0));
        var scroll = GetComponentInParent<ScrollRect>();
        if (scroll != null && scroll.viewport != null)
        {
            scroll.viewport.GetWorldCorners(corners);
            Vector3 lo = image.transform.InverseTransformPoint(corners[0]);
            Vector3 hi = image.transform.InverseTransformPoint(corners[2]);
            ink.SetVector("_ClipLocal", new Vector4(lo.x, lo.y, hi.x, hi.y));
        }
    }
    void OnDestroy() { if (ink != null) Destroy(ink); }
}
