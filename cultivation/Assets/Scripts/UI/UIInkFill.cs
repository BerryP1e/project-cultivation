using UnityEngine;
using UnityEngine.UI;

/// <summary>进度显示缓动；保持 Filled 与原方向，冷却遮罩不接入。</summary>
[DefaultExecutionOrder(10000)]
public class UIInkFill : MonoBehaviour
{
    Image image;
    float desired, output, start, elapsed;
    bool initialized, ownsBudget;
    public static void Attach(Image image)
    {
        if (image == null || !InkUITheme.Enabled || image.type != Image.Type.Filled || image.name.Contains("冷却")) return;
        if (image.GetComponent<UIInkFill>() == null) image.gameObject.AddComponent<UIInkFill>();
    }
    void Awake()
    {
        image = GetComponent<Image>();
        if (image.fillMethod == Image.FillMethod.Horizontal && image.fillOrigin == (int)Image.OriginHorizontal.Left)
        {
            var go = new GameObject("InkBrushTip", typeof(RectTransform), typeof(UIInkBrushTip));
            go.transform.SetParent(transform, false); go.GetComponent<UIInkBrushTip>().填充 = image;
        }
    }
    void OnDisable()
    {
        if (image != null && initialized) image.fillAmount = desired;
        if (ownsBudget) { UIInkMotion.Release(); ownsBudget = false; }
        initialized = false;
    }
    void LateUpdate()
    {
        if (image == null || image.type != Image.Type.Filled) return;
        float raw = image.fillAmount;
        if (!initialized) { desired = output = raw; initialized = true; return; }
        if (!Mathf.Approximately(raw, output) && !Mathf.Approximately(raw, desired))
        {
            start = output; desired = raw; elapsed = 0;
            if (!ownsBudget) ownsBudget = UIInkMotion.Acquire();
        }
        if (!ownsBudget || UIInkMotion.减少动效) { image.fillAmount = output = desired; if (ownsBudget) { UIInkMotion.Release(); ownsBudget = false; } return; }
        elapsed += Time.unscaledDeltaTime;
        output = Mathf.Lerp(start, desired, UIInkMotion.Timing.Cubic(elapsed / UIInkMotion.Timing.Fill)); image.fillAmount = output;
        if (elapsed >= UIInkMotion.Timing.Fill)
        {
            UIInkMotion.Release(); ownsBudget = false; image.fillAmount = output = desired;
            if (desired >= .999f && start < .999f) UIInkMotion.晕开(transform.parent as RectTransform, null, UIInkMotion.Timing.Slot);
        }
    }
}
