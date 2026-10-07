using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>Scroll, drag, click and keyboard all select the same central save slot.</summary>
public sealed class MainMenuSaveCarousel : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IScrollHandler
{
    MainMenuUI menu;
    ScrollRect scroll;
    RectTransform content;
    CanvasGroup group;
    Button confirm;
    Text confirmLabel;
    int selected;
    float quietUntil, appearance;
    bool dragging;
    const float Step=128;
    public int Selected => selected;

    public static MainMenuSaveCarousel Build(MainMenuUI menu)
    {
        var panel=menu.存档面板.GetComponent<RectTransform>();
        MainMenuPresentation.Place(panel,new Vector2(990,550),new Vector2(650,650));
        panel.GetComponent<Image>().color=Color.clear;
        var frame=panel.Find("FineFrame"); if(frame!=null) frame.gameObject.SetActive(false);
        var carousel=panel.gameObject.AddComponent<MainMenuSaveCarousel>(); carousel.menu=menu;
        carousel.group=panel.gameObject.AddComponent<CanvasGroup>();
        if(menu.面板标题!=null) {
            MainMenuPresentation.Place(menu.面板标题.rectTransform,new Vector2(325,574),new Vector2(440,42));
            menu.面板标题.alignment=TextAnchor.MiddleCenter;
        }
        var hint=panel.Find("ModeHint")?.GetComponent<Text>();
        if(hint!=null) { MainMenuPresentation.Place(hint.rectTransform,new Vector2(325,92),new Vector2(480,32)); hint.alignment=TextAnchor.MiddleCenter; }
        MainMenuPresentation.Place(menu.关闭按钮.GetComponent<RectTransform>(),new Vector2(568,568),new Vector2(40,40));
        var view=UIBuildUtils.CreateImage("CarouselViewport",panel,Color.clear);
        MainMenuPresentation.Place(view.rectTransform,new Vector2(325,330),new Vector2(510,370));
        view.raycastTarget=true; view.gameObject.AddComponent<RectMask2D>();
        carousel.scroll=view.gameObject.AddComponent<ScrollRect>();
        carousel.scroll.viewport=view.rectTransform; carousel.scroll.horizontal=false;
        carousel.scroll.movementType=ScrollRect.MovementType.Clamped;
        carousel.scroll.scrollSensitivity=46; carousel.scroll.decelerationRate=.09f;
        carousel.content=(RectTransform)menu.槽位容器; carousel.content.SetParent(view.transform,false);
        carousel.content.anchorMin=new Vector2(0,1); carousel.content.anchorMax=Vector2.one;
        carousel.content.pivot=new Vector2(.5f,1); carousel.content.sizeDelta=new Vector2(0,0);
        carousel.content.anchoredPosition=Vector2.zero;
        var layout=carousel.content.GetComponent<VerticalLayoutGroup>();
        layout.spacing=12; layout.padding=new RectOffset(12,12,127,127);
        layout.childForceExpandHeight=false;
        var fitter=carousel.content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        carousel.scroll.content=carousel.content;
        // Event forwarding keeps wheel/drag ownership on the ScrollRect itself.
        view.gameObject.AddComponent<MainMenuCarouselInput>().target=carousel;
        carousel.confirm=UIBuildUtils.CreateButton("ConfirmSave",panel,menu.字体,"进入此存档",25);
        MainMenuPresentation.Place(carousel.confirm.GetComponent<RectTransform>(),new Vector2(325,44),new Vector2(250,52));
        UIInkActionButton.Apply(carousel.confirm);
        carousel.confirmLabel=carousel.confirm.GetComponentInChildren<Text>();
        carousel.confirm.onClick.AddListener(()=>menu.确认中央存档(carousel.selected));
        for(int i=0;i<2;i++) {
            var marker=UIBuildUtils.CreateText("SelectionMarker"+i,panel,menu.字体,i==0?"›":"‹",38,TextAnchor.MiddleCenter,new Color(.8f,.9f,.72f));
            MainMenuPresentation.Place(marker.rectTransform,new Vector2(i==0?48:602,330),new Vector2(30,52));
        }
        return carousel;
    }
    public void Refresh(bool newGame)
    {
        selected=Mathf.Clamp(selected,0,content.childCount-1);
        confirmLabel.text=newGame?"踏入仙途":"继续争渡";
        Canvas.ForceUpdateCanvases(); content.anchoredPosition=new Vector2(0,selected*Step);
        UpdateRows();
    }
    public void Select(int index) { selected=index; quietUntil=0; scroll.StopMovement(); }
    void OnEnable() { appearance=0; if(group!=null) group.alpha=0; }
    public void OnBeginDrag(PointerEventData e) { dragging=true; }
    public void OnEndDrag(PointerEventData e) { dragging=false; quietUntil=Time.unscaledTime+.15f; }
    public void OnScroll(PointerEventData e) { quietUntil=Time.unscaledTime+.16f; }
    void Update()
    {
        if(content==null) return;
        appearance=Mathf.MoveTowards(appearance,1,Time.unscaledDeltaTime*2.5f);
        group.alpha=Mathf.SmoothStep(0,1,appearance);
        transform.localScale=Vector3.one*Mathf.Lerp(.94f,1,Mathf.SmoothStep(0,1,appearance));
        if(Input.GetKeyDown(KeyCode.Escape)) { menu.关闭面板(); return; }
        if(Input.GetKeyDown(KeyCode.UpArrow)) Select(Mathf.Max(0,selected-1));
        if(Input.GetKeyDown(KeyCode.DownArrow)) Select(Mathf.Min(content.childCount-1,selected+1));
        if(dragging || Time.unscaledTime<quietUntil || Mathf.Abs(scroll.velocity.y)>35)
            selected=Mathf.Clamp(Mathf.RoundToInt(content.anchoredPosition.y/Step),0,content.childCount-1);
        else {
            scroll.StopMovement();
            content.anchoredPosition=Vector2.Lerp(content.anchoredPosition,new Vector2(0,selected*Step),1-Mathf.Exp(-Time.unscaledDeltaTime*13));
        }
        UpdateRows();
    }
    void UpdateRows()
    {
        for(int i=0;i<content.childCount;i++) {
            var row=content.GetChild(i); float distance=Mathf.Abs(content.anchoredPosition.y-i*Step)/Step;
            var cg=row.GetComponent<CanvasGroup>(); if(cg==null) cg=row.gameObject.AddComponent<CanvasGroup>();
            cg.alpha=Mathf.Lerp(1,.18f,Mathf.Clamp01(distance/1.5f));
            row.localScale=Vector3.one*Mathf.Lerp(1,.9f,Mathf.Clamp01(distance));
            var image=row.GetComponent<Image>();
            image.color=i==selected?new Color(.08f,.20f,.20f,.94f):new Color(.025f,.065f,.07f,.70f);
        }
        confirm.interactable=menu.中央存档可用(selected);
    }
}
