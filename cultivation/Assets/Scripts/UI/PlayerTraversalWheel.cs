using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>按住Tab的十二格行游轮盘。松开只执行当前格一次，空位与中央留白取消。</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(-300)]
public sealed class PlayerTraversalWheel : MonoBehaviour
{
    public KeyCode 打开键 = KeyCode.Tab;
    public const int SectorCount = 12;
    public const int FlightSector = 11, MountSector = 0;
    const float OuterRadius = 200, InnerRadius = 106, Gap = 4;
    sealed class Slot
    {
        public RectTransform root;
        public UITraversalSector paper;
        public CanvasGroup inkGroup;
        public UIInkFluid fluid;
        public Image icon;
        public Text label;
        public float hover;
    }
    PlayerTraversalController controller;
    PlayerController movement;
    GameObject canvasObject;
    RectTransform canvasRect, wheel;
    CanvasGroup group;
    Slot[] slots;
    Text title, subtitle;
    Image centerIcon;
    float opacity;
    Vector2 pressPoint;
    bool pointerMoved, waitingForRelease, wasHeld;
    bool oldCursorVisible;
    CursorLockMode oldCursorLock;
    public bool 已打开 { get; private set; }
    public int 选中扇区 { get; private set; } = -1;
    public GameObject 画布 => canvasObject;
    public RectTransform 轮盘 => wheel;
#if UNITY_EDITOR
    [HideInInspector] public bool 模拟输入, 模拟按住;
    [HideInInspector] public Vector2 模拟鼠标;
#endif

    void Awake()
    {
        movement = GetComponent<PlayerController>();
        controller = GetComponent<PlayerTraversalController>() ?? gameObject.AddComponent<PlayerTraversalController>();
    }
    void Update()
    {
        bool held = Input.GetKey(打开键);
        Vector2 pointer = Input.mousePosition;
#if UNITY_EDITOR
        if (模拟输入) { held = 模拟按住; pointer = 模拟鼠标; }
#endif
        bool pressed = held && !wasHeld; wasHeld = held;
        if (waitingForRelease) { if (!held) waitingForRelease = false; return; }
        if (!movement || !movement.isActiveAndEnabled || (GetComponent<PlayerVitals>()?.IsDead ?? false))
        { if (已打开) 关闭(false); return; }
        if (已打开 && (UiEscRegistry.SceneInputBlockedByOthers(this) || Input.GetKeyDown(KeyCode.Escape)))
        { 关闭(false); waitingForRelease = held; return; }
        if (!已打开 && pressed) 打开(pointer);
        if (已打开)
        {
            更新指针(pointer);
            if (!held) 关闭(true);
        }
    }
    public bool 打开(Vector2 screenPoint)
    {
        if (已打开 || UiEscRegistry.SceneInputBlocked || !movement || !movement.isActiveAndEnabled
            || (GetComponent<PlayerVitals>()?.IsDead ?? false) || (GetComponent<PlayerFlightDodge>()?.闪避中 ?? false)) return false;
        var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (selected && selected.GetComponent<InputField>()?.isFocused == true) return false;
        EnsureView();
        canvasObject.SetActive(true); Canvas.ForceUpdateCanvases();
        pressPoint = screenPoint; pointerMoved = false; 选中扇区 = -1;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out var point);
        var rect = canvasRect.rect; float padding = OuterRadius * 1.06f + 24;
        wheel.anchoredPosition = new Vector2(Mathf.Clamp(point.x, rect.xMin + padding, rect.xMax - padding),
            Mathf.Clamp(point.y, rect.yMin + padding, rect.yMax - padding));
        oldCursorVisible = Cursor.visible; oldCursorLock = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        已打开 = true; canvasObject.SetActive(true);
        group.blocksRaycasts = true;
        UiEscRegistry.SetSceneInputBlocked(this, true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        RefreshLabels();
        return true;
    }
    public static int 扇区位置(Vector2 point)
    {
        if (point.magnitude < InnerRadius) return -1;
        float angle = Mathf.Repeat(90 - Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg, 360);
        float step = 360f / SectorCount, offset = angle % step;
        if (offset < Gap * .5f || offset > step - Gap * .5f) return -1;
        return Mathf.FloorToInt(angle / step);
    }
    public static TraversalAction 扇区动作(int index)
        => index == FlightSector ? TraversalAction.Flight : index == MountSector ? TraversalAction.Mount : TraversalAction.None;
    public void 更新指针(Vector2 screenPoint)
    {
        if (!已打开) return;
        if (!pointerMoved && (screenPoint - pressPoint).sqrMagnitude > 36) pointerMoved = true;
        if (!pointerMoved) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(wheel, screenPoint, null, out var local);
        选中扇区 = 扇区位置(local);
        RefreshLabels();
    }
    public void 关闭(bool execute)
    {
        if (!已打开) return;
        var action = 扇区动作(选中扇区);
        // 请求先检查其他UI，忽略本轮盘自己的输入锁；执行后释放锁。
        if (execute && action != TraversalAction.None) controller.请求(action, this);
        已打开 = false;
        group.blocksRaycasts = false;
        UiEscRegistry.SetSceneInputBlocked(this, false); UiEscRegistry.NotifyClosed();
        if (!UiEscRegistry.SceneInputBlockedByOthers(this))
        { Cursor.visible = oldCursorVisible; Cursor.lockState = oldCursorLock; }
    }
    void OnApplicationFocus(bool focused)
    {
        if (!focused && 已打开) { 关闭(false); waitingForRelease = true; }
    }
    void OnDisable()
    {
        关闭(false); UiEscRegistry.SetSceneInputBlocked(this, false);
        if (canvasObject) canvasObject.SetActive(false);
    }
    void OnDestroy() { if (canvasObject) Destroy(canvasObject); }
    void LateUpdate()
    {
        if (!canvasObject || !canvasObject.activeSelf) return;
        float dt = Mathf.Min(.05f, Time.unscaledDeltaTime), blend = 1 - Mathf.Exp(-dt * 18);
        opacity = Mathf.Lerp(opacity, 已打开 ? 1 : 0, blend); group.alpha = opacity;
        if (!已打开 && opacity < .015f) { canvasObject.SetActive(false); opacity = 0; return; }
        bool reduced = UIInkMotion.减少动效;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            slot.hover = Mathf.Lerp(slot.hover, 已打开 && 选中扇区 == i ? 1 : 0, blend);
            slot.root.localScale = Vector3.one * (1 + slot.hover * .055f);
            bool populated = 扇区动作(i) != TraversalAction.None;
            bool available = populated && controller.可用(扇区动作(i), out _);
            slot.paper.color = Color.Lerp(new Color(.78f, .80f, .75f, populated ? (available ? .8f : .38f) : .32f),
                new Color(.88f, .88f, .80f, .92f), slot.hover);
            slot.inkGroup.alpha = slot.hover * .7f;
            slot.fluid.enabled = !reduced && slot.hover > .01f;
            slot.icon.color = available ? Color.white : new Color(.65f, .69f, .65f, .65f);
            slot.label.color = available ? InkUITheme.Ink : new Color(.36f, .41f, .38f, .85f);
        }
    }
    void RefreshLabels()
    {
        if (slots == null) return;
        var data = controller.数据;
        var flight = data ? data.取御风神通() : null;
        var mount = data ? data.当前坐骑 : null;
        slots[FlightSector].icon.sprite = flight && flight.图标 ? flight.图标 : InkUITheme.Load("Icons/icon-divine-ability");
        slots[MountSector].icon.sprite = InkUITheme.Load("Icons/icon-mount");
        var action = 扇区动作(选中扇区);
        if (action == TraversalAction.None)
        {
            title.text = 选中扇区 < 0 ? "行　游" : "";
            subtitle.text = ""; centerIcon.enabled = false; return;
        }
        bool available = controller.可用(action, out string reason);
        bool active = action == TraversalAction.Flight ? (controller.御风?.御风中 ?? false) : (controller.骑手?.已骑稳 ?? false);
        title.text = action == TraversalAction.Flight ? "凭虚御风" : mount ? mount.DisplayName : "坐骑";
        subtitle.text = available ? (active ? (action == TraversalAction.Flight ? "落地" : "下坐骑") : (action == TraversalAction.Flight ? "御风" : "骑乘")) : reason;
        centerIcon.sprite = slots[选中扇区].icon.sprite; centerIcon.enabled = centerIcon.sprite;
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f; rect.sizeDelta = size;
        return rect;
    }
    static Image Picture(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = Rect(name, parent, size); rect.anchoredPosition = position;
        var image = rect.gameObject.AddComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
        return image;
    }
    static Text Label(string name, Transform parent, Font font, string text, int size, Vector2 dimensions, Vector2 position)
    {
        var rect = Rect(name, parent, dimensions); rect.anchoredPosition = position;
        var label = rect.gameObject.AddComponent<Text>(); label.font = font; label.text = text; label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter; label.color = InkUITheme.Ink; label.raycastTarget = false;
        return label;
    }
    void EnsureView()
    {
        if (canvasObject) return;
        canvasObject = new GameObject("TraversalWheelCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        group = canvasObject.GetComponent<CanvasGroup>(); group.alpha = 0;
        canvasRect = (RectTransform)canvasObject.transform;
        var veil = Picture("Veil", canvasRect, Vector2.zero, Vector2.zero);
        veil.rectTransform.anchorMin = Vector2.zero; veil.rectTransform.anchorMax = Vector2.one;
        veil.color = new Color(.07f, .1f, .085f, .2f); veil.raycastTarget = true;
        wheel = Rect("行游轮盘", canvasRect, Vector2.one * 440);
        var font = Resources.Load<Font>("字体/SourceHanSerifSC-Regular") ?? Resources.Load<Font>("Fonts/SimHei") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        slots = new Slot[SectorCount];
        for (int i = 0; i < slots.Length; i++)
        {
            var root = Rect("Sector_" + i, wheel, Vector2.one * 440);
            var paper = root.gameObject.AddComponent<UITraversalSector>(); paper.index = i; paper.raycastTarget = false;
            var maskRect = Rect("InkMask", root, Vector2.one * 440);
            var arc = maskRect.gameObject.AddComponent<UITraversalSector>(); arc.index = i; arc.raycastTarget = false;
            var mask = maskRect.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var inkGroup = maskRect.gameObject.AddComponent<CanvasGroup>(); inkGroup.alpha = 0;
            float angle = (90 - (i + .5f) * 360 / SectorCount) * Mathf.Deg2Rad;
            var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 150;
            var inkRoot = Rect("InkFlow", maskRect, Vector2.one * 140); inkRoot.anchoredPosition = point;
            var ink = Picture("InkBlot", inkRoot, Vector2.one * 140, Vector2.zero);
            ink.sprite = InkUITheme.Load("Dynamic/nav-ink-blot-4") ?? InkUITheme.Load("Effects/fx-ink-blot");
            var fluid = inkRoot.gameObject.AddComponent<UIInkFluid>(); fluid.使用密度模拟 = false;
            fluid.位移强度 = .35f; fluid.形变强度 = .5f; fluid.基础浓度 = .8f;
            fluid.初始化(ink, null, null, null, i); fluid.选中(true); fluid.enabled = false;
            var icon = Picture("Icon", root, Vector2.one * 34, point + Vector2.up * 9);
            var text = Label("Label", root, font, i == FlightSector ? "御风" : i == MountSector ? "坐骑" : "", 14, new Vector2(70, 25), point - Vector2.up * 24);
            icon.enabled = 扇区动作(i) != TraversalAction.None;
            slots[i] = new Slot { root = root, paper = paper, inkGroup = inkGroup, fluid = fluid, icon = icon, label = text };
        }
        centerIcon = Picture("SelectedIcon", wheel, Vector2.one * 42, new Vector2(0, 33));
        title = Label("SelectedName", wheel, font, "行　游", 21, new Vector2(170, 36), new Vector2(0, -13));
        subtitle = Label("SelectedState", wheel, font, "", 14, new Vector2(170, 26), new Vector2(0, -46));
        subtitle.color = new Color(.79f, .84f, .78f); title.color = new Color(.95f, .94f, .85f);
        canvasObject.SetActive(false);
    }
}
