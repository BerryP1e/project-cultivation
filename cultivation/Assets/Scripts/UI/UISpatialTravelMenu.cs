using UnityEngine;
using UnityEngine.UI;

/// <summary>空间腾挪子选项；任务目标与实际已访地点分区，ESC 关闭并释放输入。</summary>
[DefaultExecutionOrder(-310)]
public sealed class UISpatialTravelMenu : MonoBehaviour
{
    PlayerTraversalController owner;
    Font font;
    Text notice;
    bool oldVisible;
    CursorLockMode oldLock;
    public static UISpatialTravelMenu Open(PlayerTraversalController source, PlayerTraversalWheel wheel = null)
    {
        foreach (var existing in FindObjectsOfType<UISpatialTravelMenu>()) existing.Close();
        var root = new GameObject("空间腾挪_目的地", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 1450;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var menu = root.AddComponent<UISpatialTravelMenu>(); menu.owner = source;
        if (wheel) { menu.oldVisible = wheel.原光标可见; menu.oldLock = wheel.原光标锁定; }
        menu.Build(); return menu;
    }
    void OnEnable()
    {
        oldVisible = Cursor.visible; oldLock = Cursor.lockState;
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        UiEscRegistry.SetSceneInputBlocked(this, true);
    }
    void Build()
    {
        font = Resources.Load<Font>("字体/SourceHanSerifSC-Regular") ?? Resources.Load<Font>("Fonts/SimHei") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var dim = UIBuildUtils.CreateImage("Dim", transform, new Color(.01f, .02f, .025f, .45f)); UIBuildUtils.Stretch(dim.rectTransform);
        dim.raycastTarget = true; var cancel = dim.gameObject.AddComponent<Button>(); cancel.transition = Selectable.Transition.None; cancel.onClick.AddListener(Close);
        var backdrop = UIBuildUtils.CreateImage("云墨子菜单", transform, new Color(.09f, .13f, .13f, .94f));
        backdrop.rectTransform.anchorMin = backdrop.rectTransform.anchorMax = Vector2.one * .5f; backdrop.rectTransform.sizeDelta = new Vector2(720, 760);
        backdrop.gameObject.AddComponent<UIInkDialogueBackdrop>(); backdrop.raycastTarget = true;
        var title = Label("标题", backdrop.transform, "空间腾挪", 32); Place(title.rectTransform, .1f, .86f, .9f, .96f);
        notice = Label("提示", backdrop.transform, "选择前往的地点", 18); Place(notice.rectTransform, .1f, .81f, .9f, .86f);
        bool questAvailable = SpatialTravel.TryQuest(out var quest);
        var task = Choice(backdrop.transform, "当前委托目标", questAvailable ? quest.名称 : "暂无可前往的委托目标", questAvailable, () => ChooseQuest());
        Place(task.GetComponent<RectTransform>(), .12f, .68f, .88f, .79f);
        var heading = Label("已访标题", backdrop.transform, "已访传送点", 22); Place(heading.rectTransform, .12f, .59f, .88f, .66f);
        var list = UIBuildUtils.CreateRect("已访列表", backdrop.transform); Place(list, .1f, .16f, .9f, .58f);
        var scroll = list.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = UIBuildUtils.CreateImage("Viewport", list, Color.clear); UIBuildUtils.Stretch(viewport.rectTransform); viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 16); scroll.viewport = viewport.rectTransform;
        var content = UIBuildUtils.CreateRect("Content", viewport.transform); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero; scroll.content = content;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 20; layout.padding = new RectOffset(20, 20, 16, 16);
        layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var points = SpatialTravel.Copy(owner.数据.已访传送点);
        foreach (var point in points)
        {
            var row = Choice(content, point.名称, SaveApplier.取场景显示名(point.场景), true, () => Choose(point));
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
        }
        if (points.Count == 0) { var empty = Label("无记录", content, "尚未访问传送点", 20); empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 72; }
        var close = Choice(backdrop.transform, "返回", "", true, Close); Place(close.GetComponent<RectTransform>(), .32f, .05f, .68f, .13f);
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); scroll.verticalNormalizedPosition = 1;
    }
    void ChooseQuest()
    {
        if (!SpatialTravel.TryQuest(out var point)) { notice.text = "当前委托目标已改变"; return; }
        Choose(point);
    }
    public bool Choose(TeleportBookmark point)
    {
        if (!owner || UiEscRegistry.SceneInputBlockedByOthers(this)) return false;
        if (!owner.可用(TraversalAction.SpaceShift, out var reason)) { notice.text = reason; return false; }
        if (!SpatialTravel.Travel(owner, point, this)) { notice.text = "目的地暂不可到达"; return false; }
        Close(); return true;
    }
    Button Choice(Transform parent, string title, string subtitle, bool enabled, UnityEngine.Events.UnityAction action)
    {
        var image = UIBuildUtils.CreateImage(title, parent, Color.clear); var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var text = Label("名称", image.transform, title, 22); Place(text.rectTransform, 0, string.IsNullOrEmpty(subtitle) ? 0 : .3f, 1, 1);
        if (!string.IsNullOrEmpty(subtitle)) { var sub = Label("地点", image.transform, subtitle, 15); Place(sub.rectTransform, 0, 0, 1, .3f); }
        UIInkActionButton.Apply(button); button.GetComponent<UIInkActionButton>().设置绘制倍率(new Vector2(.8f, 1.2f));
        button.interactable = enabled; button.onClick.AddListener(action); return button;
    }
    Text Label(string name, Transform parent, string value, int size) => UIBuildUtils.CreateText(name, parent, font, value, size, TextAnchor.MiddleCenter, new Color(.95f, .95f, .87f));
    static void Place(RectTransform rect, float x0, float y0, float x1, float y1) => UIBuildUtils.Place(rect, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
    void Update()
    {
        if (!owner || !owner.可用(TraversalAction.SpaceShift, out _) || (owner.GetComponent<PlayerVitals>()?.IsDead ?? true)
            || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) Close();
    }
    public void Close() { gameObject.SetActive(false); UiEscRegistry.NotifyClosed(); Destroy(gameObject); }
    void OnDisable()
    {
        UiEscRegistry.SetSceneInputBlocked(this, false);
        if (!UiEscRegistry.SceneInputBlockedByOthers(this)) { Cursor.visible = oldVisible; Cursor.lockState = oldLock; }
    }
}
