using UnityEngine;
using UnityEngine.UI;

/// <summary>Standalone formation window owned by the equipped Zhenyaohu.</summary>
[DisallowMultipleComponent]
public class GourdFormationUI : MonoBehaviour
{
    UIPanelData data;
    GameObject window;
    float previousTimeScale;
    bool paused;
    readonly System.Collections.Generic.Dictionary<CanvasGroup,Vector3> hiddenHud = new System.Collections.Generic.Dictionary<CanvasGroup,Vector3>();
    public bool IsOpen { get; private set; }
    public GameObject FormationPage { get; private set; }

    public static void Attach(CharacterPanelUI character)
    {
        if (character == null || character.tabs.Count <= 5 || character.tabs[5].page == null) return;
        var view = character.GetComponent<GourdFormationUI>();
        if (view == null) view = character.gameObject.AddComponent<GourdFormationUI>();
        view.data = character.GetComponent<UIPanelData>();
        if (view.data == null) view.data = FindObjectOfType<UIPanelData>();
        var binding = character.tabs[5];
        view.FormationPage = binding.page;
        view.FormationPage.SetActive(false);
        foreach(var list in view.FormationPage.GetComponentsInChildren<UIEntryList>(true)) list.data=view.data;
        foreach(var info in view.FormationPage.GetComponentsInChildren<UIEntryInfo>(true)) info.data=view.data;
        foreach(var bar in view.FormationPage.GetComponentsInChildren<UISpiritFormationBar>(true)) bar.data=view.data;
        foreach(var slot in view.FormationPage.GetComponentsInChildren<UISpiritSlot>(true)) slot.data=view.data;
        view.BuildWindow();
        binding.page = null;
        if (binding.button != null) {
            binding.button.gameObject.SetActive(false);
            Destroy(binding.button.gameObject);
        }
        binding.button = null; binding.background = null;
    }

    void BuildWindow()
    {
        window = new GameObject("镇妖葫战阵窗口", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        window.SetActive(false);
        var canvas = window.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2460;
        var scaler = window.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        var dim = UIBuildUtils.CreateImage("FormationDim",window.transform,new Color(0,.015f,.012f,.48f));
        UIBuildUtils.Stretch(dim.rectTransform); dim.raycastTarget = true;
        var font = Resources.Load<Font>("Fonts/SimHei") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var title = UIBuildUtils.CreateText("Title",window.transform,font,"镇妖葫 · 战阵",32,TextAnchor.MiddleLeft,new Color(.94f,.92f,.82f));
        UIBuildUtils.Place(title.rectTransform,new Vector2(.08f,.89f),new Vector2(.75f,.96f),Vector2.zero,Vector2.zero);
        var close = UIBuildUtils.CreateImage("Close",window.transform,new Color(.05f,.08f,.065f,.85f));
        close.sprite = StationInteractor.取圆角(); close.type = Image.Type.Sliced; close.raycastTarget = true;
        UIBuildUtils.Place(close.rectTransform,new Vector2(.88f,.89f),new Vector2(.93f,.95f),Vector2.zero,Vector2.zero);
        close.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        var closeText = UIBuildUtils.CreateText("Label",close.transform,font,"关闭",22,TextAnchor.MiddleCenter,new Color(.94f,.92f,.82f));
        UIBuildUtils.Stretch(closeText.rectTransform);
        FormationPage.transform.SetParent(window.transform,false);
        UIBuildUtils.Place(FormationPage.transform as RectTransform,new Vector2(.07f,.09f),new Vector2(.93f,.86f),Vector2.zero,Vector2.zero);
        var hint = UIBuildUtils.CreateText("Hint",window.transform,font,"真灵由镇妖葫放出 · 切换法宝时收回    ESC 关闭",18,TextAnchor.MiddleCenter,new Color(.78f,.81f,.75f));
        UIBuildUtils.Place(hint.rectTransform,new Vector2(.15f,.025f),new Vector2(.85f,.065f),Vector2.zero,Vector2.zero);
    }

    public static bool Open(UIPanelData source)
    {
        foreach (var view in FindObjectsOfType<GourdFormationUI>())
            if (view.data == source) return view.TryOpen();
        return false;
    }
    bool HasGourd => data != null && data.当前法宝 != null && data.当前法宝.法宝id == UIPanelData.镇妖葫id && data.已拥有(data.当前法宝);
    public bool TryOpen()
    {
        if (!HasGourd || window == null || IsOpen || UiEscRegistry.SceneInputBlocked) return false;
        previousTimeScale = Time.timeScale; paused = true;
        IsOpen = true; UiEscRegistry.SetSceneInputBlocked(this,true);
        foreach(var canvas in FindObjectsOfType<Canvas>(true)) {
            if(canvas.name!="HudCanvas" && canvas.name!="QuestGuideCanvas" && canvas.name!="ChronicleCanvas")continue;
            var group=canvas.GetComponent<CanvasGroup>();if(group==null)group=canvas.gameObject.AddComponent<CanvasGroup>();
            hiddenHud[group]=new Vector3(group.alpha,group.interactable?1:0,group.blocksRaycasts?1:0);
            group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
        }
        window.SetActive(true); FormationPage.SetActive(true);
        Time.timeScale = 0; Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        return true;
    }
    void Update()
    {
        if (IsOpen && (!HasGourd || Input.GetKeyDown(KeyCode.Escape))) Close();
    }
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false; UIDragContext.End(true);
        if (window != null) window.SetActive(false);
        UiEscRegistry.SetSceneInputBlocked(this,false);
        foreach(var pair in hiddenHud) if(pair.Key!=null) {
            pair.Key.alpha=pair.Value.x;pair.Key.interactable=pair.Value.y>0;pair.Key.blocksRaycasts=pair.Value.z>0;
        }
        hiddenHud.Clear();
        if (paused) { Time.timeScale = previousTimeScale; paused = false; }
        UiEscRegistry.NotifyClosed();
    }
    void OnDisable() => Close();
    void OnDestroy() { Close(); if(window != null) Destroy(window); }
}
