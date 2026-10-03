using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>共用水墨动效。只动绘制层；业务开关、命中框、布局和文字字号不动。</summary>
[DisallowMultipleComponent]
public class UIInkMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public enum Kind { Panel, Tab, Row, Card, Slot }
    public Kind 类型;
    public Transform 图标;
    public float 错开;
    CanvasGroup group;
    UIInkReveal reveal;
    Coroutine entrance;
    Coroutine tabFade;
    bool tabReserved;
    Image previousTab;
    Vector3 iconPosition, iconScale;
    Quaternion iconRotation;
    bool hovered, pressed, reserved, started, selected;
    float lift;
    float appearance = 1;
    int pulseFrame = -1;
    Transform capturedIcon;
    Image shortStroke;
    UIInkPulse shadow;
    Image dropOutline;

    public static class Timing
    {
        public const float Press = .06f, Hover = .14f, Appear = .18f, Close = .18f;
        public const float Panel = .26f, Ripple = .25f, Slot = .22f, Number = .20f, Fill = .25f;
        public static float Cubic(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);
        public static float Quad(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 2);
        public static float InOut(float t) => t < .5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
    }
    public static bool 减少动效
    {
        get => PlayerPrefs.GetInt("InkUI.ReduceMotion", 0) != 0;
        set { PlayerPrefs.SetInt("InkUI.ReduceMotion", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static int 运动数 { get; private set; }
    public static int 峰值 { get; private set; }
    internal static bool Acquire()
    {
        if (减少动效 || 运动数 >= 6) return false;
        运动数++; 峰值 = Mathf.Max(峰值, 运动数); return true;
    }
    internal static void Release() { 运动数 = Mathf.Max(0, 运动数 - 1); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetBudget() { 运动数 = 0; 峰值 = 0; }

    public static UIInkMotion Attach(GameObject target, Kind kind, Transform icon = null, float delay = 0)
    {
        if (!InkUITheme.Enabled || target == null) return null;
        var motion = target.GetComponent<UIInkMotion>();
        if (motion == null) motion = target.AddComponent<UIInkMotion>();
        motion.类型 = kind; motion.图标 = icon; motion.错开 = delay;
        motion.CaptureIcon();
        return motion;
    }
    void CaptureIcon()
    {
        if (图标 == null || capturedIcon == 图标) return;
        capturedIcon = 图标;
        iconPosition = 图标 is RectTransform rt ? (Vector3)rt.anchoredPosition : 图标.localPosition;
        iconScale = 图标.localScale; iconRotation = 图标.localRotation;
    }
    bool HasEntrance => 类型 == Kind.Panel || 错开 >= 0 && (类型 == Kind.Row || 类型 == Kind.Card && 图标 != null);
    void Start() { started = true; CaptureIcon(); if (HasEntrance) 落笔(this); }
    void OnEnable() { if (started && HasEntrance) 落笔(this); }
    void OnDisable()
    {
        if (entrance != null) StopCoroutine(entrance);
        if (tabFade != null) StopCoroutine(tabFade);
        CleanupTab();
        tabFade = null;
        entrance = null;
        if (reserved) { Release(); reserved = false; }
        hovered = pressed = false; lift = 0;
        appearance = 1;
        if (图标 != null) { if (图标 is RectTransform rt) rt.anchoredPosition = iconPosition; else 图标.localPosition = iconPosition; 图标.localScale = iconScale; 图标.localRotation = iconRotation; }
        if (group != null) group.alpha = 1;
        if (reveal != null) reveal.进度 = 1;
    }
    public static void 落笔(UIInkMotion motion)
    {
        if (motion == null || !motion.isActiveAndEnabled) return;
        if (motion.entrance != null) motion.StopCoroutine(motion.entrance);
        if (motion.reserved) { Release(); motion.reserved = false; }
        motion.entrance = motion.StartCoroutine(motion.Enter());
    }
    IEnumerator Enter()
    {
        if (group == null) { group = GetComponent<CanvasGroup>(); if (group == null) group = gameObject.AddComponent<CanvasGroup>(); }
        if (减少动效) { group.alpha = 1; yield break; }
        group.alpha = 0;
        if (错开 > 0) yield return new WaitForSecondsRealtime(错开);
        if (类型 == Kind.Panel && GetComponent<Image>() != null)
        {
            reveal = GetComponent<UIInkReveal>();
            if (reveal == null) reveal = gameObject.AddComponent<UIInkReveal>();
            reveal.进度 = 0;
            foreach (var child in GetComponentsInChildren<Graphic>(true))
            {
                var clip = child.GetComponent<UIInkClipRect>();
                if (clip == null) clip = child.gameObject.AddComponent<UIInkClipRect>();
                clip.面板 = reveal;
            }
        }
        // 主面板等待绘制名额；行和卡片直接显示，避免让操作排队。
        bool acquired = Acquire();
        while (!acquired && 类型 == Kind.Panel && !减少动效) { yield return null; acquired = Acquire(); }
        if (!acquired) { group.alpha = 1; if (reveal != null) reveal.进度 = 1; yield break; }
        reserved = true;
        float duration = 类型 == Kind.Panel ? Timing.Panel : Timing.Appear;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (减少动效) break;
            group.alpha = Timing.Cubic(elapsed / Timing.Appear);
            if (类型 == Kind.Card && 图标 != null) appearance = Mathf.Lerp(.96f, 1, Timing.Cubic(elapsed / Timing.Appear));
            if (reveal != null) reveal.进度 = Timing.InOut(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = 1; appearance = 1; if (reveal != null) reveal.进度 = 1;
        Release(); reserved = false; entrance = null;
    }
    public static void 晕开(RectTransform target, Vector2? localPoint = null, float duration = Timing.Ripple)
    {
        if (target == null || !target.gameObject.activeInHierarchy || !InkUITheme.Enabled || 减少动效) return;
        UIInkPulse.Emit(target, localPoint ?? Vector2.zero, duration);
    }
    public void 选中(bool value)
    {
        if (selected == value) return;
        selected = value;
        if (类型 == Kind.Tab && isActiveAndEnabled)
        {
            if (tabFade != null) StopCoroutine(tabFade);
            CleanupTab();
            tabFade = StartCoroutine(CrossFadeTab());
        }
        if (value && !hovered) 晕开(transform as RectTransform);
    }
    IEnumerator CrossFadeTab()
    {
        var image = GetComponent<Image>(); var text = GetComponentInChildren<Text>();
        if (image == null || !Acquire()) yield break;
        tabReserved = true;
        Image old = null;
        if (image.color.a > .01f)
        {
            old = UIBuildUtils.CreateImage("InkTabPrevious", transform, image.color);
            previousTab = old;
            old.sprite = image.overrideSprite; old.type = image.type; old.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
            UIBuildUtils.Stretch(old.rectTransform); old.transform.SetAsFirstSibling();
        }
        try
        {
            // 状态刷新由原控制器完成，本组件仅交叉淡入绘制。
            yield return null;
            for (float elapsed = 0; elapsed < Timing.Appear; elapsed += Time.unscaledDeltaTime)
            {
                if (减少动效) break;
                float t = Timing.Cubic(elapsed / Timing.Appear);
                image.canvasRenderer.SetAlpha(t); if (text != null) text.canvasRenderer.SetAlpha(t);
                if (old != null) { var c = old.color; c.a = 1 - t; old.color = c; }
                yield return null;
            }
        }
        finally
        {
            CleanupTab();
        }
    }
    void CleanupTab()
    {
        var image = GetComponent<Image>(); var text = GetComponentInChildren<Text>();
        if (image != null) image.canvasRenderer.SetAlpha(1); if (text != null) text.canvasRenderer.SetAlpha(1);
        if (previousTab != null) Destroy(previousTab.gameObject);
        previousTab = null;
        if (tabReserved) { Release(); tabReserved = false; }
        tabFade = null;
    }
    public static void 干笔(Button button)
    {
        // 没有飞白遮罩时不伪造素材；保留禁用按钮的纸底与原业务禁用状态。
        if (button == null) return;
        var mask = InkUITheme.Load("Effects/fx-dry-brush-mask");
        if (mask == null) return;
        var child = button.transform.Find("InkDryBrush");
        if (child == null)
        {
            var image = UIBuildUtils.CreateImage("InkDryBrush", button.transform, new Color(1, 1, 1, .35f));
            UIBuildUtils.Stretch(image.rectTransform); image.sprite = mask; child = image.transform;
        }
        child.gameObject.SetActive(!button.interactable);
    }
    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        if (类型 == Kind.Slot && UIDragContext.Dragging)
        {
            if (dropOutline == null)
            {
                dropOutline = UIBuildUtils.CreateImage("InkDropOutline", transform, new Color(.19f, .24f, .22f, .3f));
                UIBuildUtils.Stretch(dropOutline.rectTransform); InkUITheme.Image(dropOutline, "Parts/active-slot-frame", false, false);
            }
            dropOutline.gameObject.SetActive(true);
        }
    }
    public void OnPointerExit(PointerEventData e) { hovered = pressed = false; if (dropOutline != null) dropOutline.gameObject.SetActive(false); }
    public void OnPointerDown(PointerEventData e) { if (CanInteract()) pressed = true; }
    public void OnPointerUp(PointerEventData e) { pressed = false; }
    bool CanInteract() { var b = GetComponent<Button>(); return b == null || b.IsInteractable(); }
    public void OnPointerClick(PointerEventData e)
    {
        if (!CanInteract() || pulseFrame == Time.frameCount) return;
        pulseFrame = Time.frameCount;
        var rt = transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var p)) 晕开(rt, p);
    }
    void LateUpdate()
    {
        if (类型 == Kind.Tab)
        {
            if (reveal == null) { reveal = GetComponent<UIInkReveal>(); if (reveal == null) reveal = gameObject.AddComponent<UIInkReveal>(); }
            reveal.上浮 = selected && !减少动效 ? 2 : 0;
        }
        if (类型 == Kind.Row)
        {
            if (shortStroke == null)
            {
                shortStroke = UIBuildUtils.CreateImage("InkShortStroke", transform, InkUITheme.Ink);
                var rt = shortStroke.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0, .5f);
                rt.pivot = new Vector2(0, .5f); rt.anchoredPosition = new Vector2(4, 0); rt.sizeDelta = new Vector2(10, 2);
            }
            float width = selected ? 22 : hovered ? 16 : 10;
            float previous = shortStroke.rectTransform.sizeDelta.x;
            if (!减少动效 && !Mathf.Approximately(previous, width) && !reserved) reserved = Acquire();
            float next = 减少动效 ? width : reserved ? Mathf.MoveTowards(previous, width, Time.unscaledDeltaTime * 50) : previous;
            shortStroke.rectTransform.sizeDelta = new Vector2(next, 2);
            if (reserved && entrance == null && Mathf.Approximately(next, width)) { Release(); reserved = false; }
            shortStroke.color = new Color(.188f, .239f, .216f, selected ? .75f : hovered ? .4f : .15f);
        }
        if (图标 == null || 类型 == Kind.Panel || 类型 == Kind.Row || 类型 == Kind.Tab) return;
        if (shadow == null) shadow = UIInkPulse.Shadow(transform as RectTransform);
        float goal = 减少动效 || !CanInteract() ? 0 : pressed ? -1 : hovered ? 3 : 0;
        if (!reserved && !Mathf.Approximately(lift, goal)) reserved = Acquire();
        if (!reserved) { lift = 0; }
        else lift = Mathf.MoveTowards(lift, goal, Time.unscaledDeltaTime * (pressed ? 4 / Timing.Press : 4 / Timing.Hover));
        if (图标 is RectTransform visualRect) visualRect.anchoredPosition = (Vector2)iconPosition + Vector2.up * lift;
        else 图标.localPosition = iconPosition + Vector3.up * lift;
        shadow.强度(减少动效 ? 0 : Mathf.Max(0, lift) / 3);
        图标.localRotation = iconRotation * Quaternion.Euler(0, 0, hovered && !减少动效 ? 1.5f : 0);
        图标.localScale = iconScale * (类型 == Kind.Slot && hovered && !减少动效 ? 1.08f : appearance);
        if (reserved && entrance == null && Mathf.Approximately(lift, goal)) { Release(); reserved = false; }
    }
    /// <summary>业务立即关闭；只复制当前可见绘制对象做短暂离场，不复制脚本/按钮。</summary>
    public static void 提笔(Transform source)
    {
        if (source == null || !source.gameObject.activeInHierarchy || !InkUITheme.Enabled || 减少动效) return;
        bool initialized = false;
        foreach (var motion in source.GetComponentsInChildren<UIInkMotion>()) if (motion.started) { initialized = true; break; }
        if (!initialized || !Acquire()) return;
        var canvas = source.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay) { Release(); return; }
        var ghost = new GameObject("InkCloseVisual", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(UIInkCloseVisual));
        var copyCanvas = ghost.GetComponent<Canvas>(); copyCanvas.renderMode = RenderMode.ScreenSpaceOverlay; copyCanvas.sortingOrder = canvas.sortingOrder;
        var group = ghost.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        var holder = ghost.transform as RectTransform;
        var corners = new Vector3[4];
        foreach (var graphic in source.GetComponentsInChildren<Graphic>())
        {
            if (!graphic.enabled || graphic.canvasRenderer.cull || graphic.color.a <= .001f) continue;
            graphic.rectTransform.GetWorldCorners(corners);
            // 已被父列表裁掉的对象不复制，避免离场时把整库展开。
            bool hidden = false;
            Rect? clipBounds = null;
            for (var ancestor = graphic.transform.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var mask = ancestor.GetComponent<RectMask2D>();
                if (mask == null || !mask.isActiveAndEnabled) continue;
                var box = new Vector3[4]; mask.rectTransform.GetWorldCorners(box);
                if (corners[2].y <= box[0].y || corners[0].y >= box[2].y) { hidden = true; break; }
                var bounds = new Rect(box[0], box[2] - box[0]);
                if (clipBounds.HasValue)
                {
                    var old = clipBounds.Value;
                    bounds = Rect.MinMaxRect(Mathf.Max(old.xMin, bounds.xMin), Mathf.Max(old.yMin, bounds.yMin), Mathf.Min(old.xMax, bounds.xMax), Mathf.Min(old.yMax, bounds.yMax));
                }
                clipBounds = bounds;
            }
            if (hidden) continue;
            Graphic copy = null;
            if (graphic is Text text)
            {
                var t = new GameObject(text.name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                t.font = text.font; t.text = text.text; t.fontSize = Mathf.RoundToInt(text.fontSize * canvas.scaleFactor);
                t.fontStyle = text.fontStyle; t.alignment = text.alignment; t.lineSpacing = text.lineSpacing;
                t.horizontalOverflow = text.horizontalOverflow; t.verticalOverflow = text.verticalOverflow;
                copy = t;
            }
            else if (graphic is Image image)
            {
                var i = new GameObject(image.name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                i.sprite = image.overrideSprite; i.type = image.type; i.preserveAspect = image.preserveAspect;
                i.fillMethod = image.fillMethod; i.fillOrigin = image.fillOrigin; i.fillClockwise = image.fillClockwise; i.fillAmount = image.fillAmount;
                i.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier / Mathf.Max(.01f, canvas.scaleFactor); copy = i;
                if (image.name == "Window" || image.name == "主面板" || image.name == "内容框" || image.name == "面板")
                {
                    var effect = i.gameObject.AddComponent<UIInkReveal>();
                    var sourceReveal = image.GetComponent<UIInkReveal>();
                    effect.进度 = sourceReveal != null ? sourceReveal.进度 : 1;
                }
            }
            if (copy == null) continue;
            copy.transform.SetParent(holder, false);
            var copyColor = graphic.color;
            foreach (var sourceGroup in graphic.GetComponentsInParent<CanvasGroup>())
            { copyColor.a *= sourceGroup.alpha; if (sourceGroup.ignoreParentGroups) break; }
            copy.color = copyColor; copy.raycastTarget = false;
            var rt = copy.rectTransform; rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
            rt.anchoredPosition = corners[0]; rt.sizeDelta = corners[2] - corners[0];
            if (clipBounds.HasValue)
            {
                var box = clipBounds.Value;
                copy.gameObject.AddComponent<UIInkClipRect>().固定裁剪 = new Rect(box.position - (Vector2)corners[0], box.size);
            }
        }
        ghost.GetComponent<UIInkCloseVisual>().Begin();
    }
}
