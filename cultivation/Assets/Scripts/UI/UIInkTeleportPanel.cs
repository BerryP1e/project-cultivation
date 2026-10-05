using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>传送选择的独立水墨视图；传送、准入与塔内接管仍由 Teleporter 决定。</summary>
public class UIInkTeleportPanel : MonoBehaviour
{
    Teleporter owner;
    Font font;
    Text heading, notice;
    ScrollRect scroll;
    CanvasGroup group;
    float openedAt;
    readonly List<Button> choices = new List<Button>();
    static readonly Color Light = new Color(.94f,.95f,.87f);
    void OnEnable()=>UiEscRegistry.SetSceneInputBlocked(this,true);
    void OnDisable()=>UiEscRegistry.SetSceneInputBlocked(this,false);

    public void 初始化(Teleporter source, Font textFont)
    {
        owner=source; font=textFont;
        var dim=UIBuildUtils.CreateImage("Dim",transform,new Color(.015f,.025f,.025f,.48f));
        UIBuildUtils.Stretch(dim.rectTransform);
        var cancel=dim.gameObject.AddComponent<Button>(); cancel.targetGraphic=dim;
        cancel.transition=Selectable.Transition.None;
        cancel.onClick.AddListener(关闭);
        var panel=UIBuildUtils.CreateImage("TeleportInk",transform,Color.white);
        var rt=panel.rectTransform;
        rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);
        int count=owner.选项!=null ? owner.选项.Length : 0;
        rt.sizeDelta=new Vector2(880,Mathf.Clamp(500+90*(count-1),500,760));
        InkUITheme.Image(panel,"Bag/bag-info-ink",false);
        // 保留背景的射线阻挡：在内容留白处点击不算点击外侧关闭。
        panel.raycastTarget=true;
        group=panel.gameObject.AddComponent<CanvasGroup>();
        heading=Label("Heading",rt,"传送",36,TextAnchor.MiddleCenter);
        Place(heading.rectTransform,.18f,.76f,.82f,.88f);
        notice=Label("Notice",rt,"",22,TextAnchor.MiddleCenter);
        Place(notice.rectTransform,.18f,.63f,.82f,.76f);

        var list=UIBuildUtils.CreateRect("Destinations",rt);
        Place(list,.16f,.27f,.84f,.63f);
        scroll=list.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal=false; scroll.vertical=true;
        scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=UIBuildUtils.CreateImage("Viewport",list,Color.clear);
        UIBuildUtils.Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<RectMask2D>().softness=new Vector2Int(0,20);
        scroll.viewport=viewport.rectTransform;
        scroll.content=UIBuildUtils.CreateRect("Content",viewport.transform);
        scroll.content.anchorMin=new Vector2(0,1); scroll.content.anchorMax=Vector2.one;
        scroll.content.pivot=new Vector2(.5f,1); scroll.content.sizeDelta=Vector2.zero;
        var layout=scroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding=new RectOffset(24,24,22,22); layout.spacing=18;
        layout.childControlWidth=true; layout.childControlHeight=true;
        layout.childForceExpandWidth=true; layout.childForceExpandHeight=false;
        scroll.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        InkUITheme.Scroll(scroll,0);
        var options=owner.选项 ?? new Teleporter.传送选项[0];
        for(int i=0;i<options.Length;i++)
        {
            int index=i;
            var row=UIBuildUtils.CreateImage("Destination_"+i,scroll.content,Color.clear);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=94;
            var button=row.gameObject.AddComponent<Button>(); button.targetGraphic=row;
            var name=Label("Name",row.transform,"",24,TextAnchor.MiddleCenter);
            Place(name.rectTransform,.14f,.30f,.86f,.97f);
            name.resizeTextForBestFit=true; name.resizeTextMinSize=18; name.resizeTextMaxSize=24;
            var state=Label("State",row.transform,"",17,TextAnchor.MiddleCenter);
            Place(state.rectTransform,.14f,.02f,.86f,.30f);
            Style(button);
            button.onClick.AddListener(()=>owner.执行(index));
            choices.Add(button);
            foreach(var graphic in row.GetComponentsInChildren<Graphic>(true))
                graphic.gameObject.AddComponent<UIInkScrollFade>();
        }
        var close=UIBuildUtils.CreateImage("Cancel",rt,Color.clear);
        Place(close.rectTransform,.34f,.14f,.66f,.23f);
        var closeButton=close.gameObject.AddComponent<Button>(); closeButton.targetGraphic=close;
        var closeText=Label("Label",close.transform,"暂不前往",22,TextAnchor.MiddleCenter);
        UIBuildUtils.Stretch(closeText.rectTransform); Style(closeButton);
        closeButton.onClick.AddListener(关闭);
        var hint=Label("CloseHint",rt,"ESC / F 关闭",15,TextAnchor.MiddleCenter);
        Place(hint.rectTransform,.32f,.09f,.68f,.14f);
        刷新();
    }

    public void 刷新()
    {
        if(owner==null || heading==null) return;
        bool duplicate=owner.选项!=null && owner.选项.Length==1 && owner.选项[0]!=null && owner.标题==owner.选项[0].名称;
        heading.text=string.IsNullOrEmpty(owner.标题) || owner.标题=="是否传送？" || duplicate ? "传送" : owner.标题;
        bool allowed=owner.准入通过;
        notice.text=!allowed ? owner.取未开放原因() : choices.Count==0 ? "暂无可前往的地点" : "选择前往的地点";
        notice.color=allowed ? Light : new Color(.95f,.73f,.48f);
        for(int i=0;i<choices.Count;i++)
        {
            var option=owner.选项[i];
            bool enabled=option!=null && !option.暂未开放 && allowed;
            var button=choices[i]; button.interactable=enabled;
            var name=button.transform.Find("Name").GetComponent<Text>();
            var state=button.transform.Find("State").GetComponent<Text>();
            name.text=option==null ? "暂无地点" : option.名称;
            state.text=option==null || option.暂未开放 ? "尚未开放" : !allowed ? "境界不足" : "点击前往";
            name.color=enabled ? Light : new Color(.62f,.66f,.62f);
            state.color=enabled ? new Color(.73f,.82f,.75f) : new Color(.62f,.66f,.62f);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.verticalNormalizedPosition=1;
        openedAt=Time.unscaledTime;
        if(group!=null) group.alpha=0;
    }

    void Update()
    {
        if(group!=null) group.alpha=Mathf.SmoothStep(0,1,(Time.unscaledTime-openedAt)/.22f);
        // StationInteractor 统一处理 F/ESC；独立调用开面板时也可以关闭。
        if(!StationInteractor.有界面打开 &&
            (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F))) 关闭();
    }
    void 关闭()
    {
        var interactor=FindObjectOfType<StationInteractor>();
        if(interactor!=null && StationInteractor.有界面打开) interactor.关闭界面();
        else if(owner!=null) owner.关面板();
    }
    Text Label(string name,Transform parent,string text,int size,TextAnchor alignment)
    {
        var label=UIBuildUtils.CreateText(name,parent,font,text,size,alignment,Light);
        label.raycastTarget=false; label.horizontalOverflow=HorizontalWrapMode.Wrap;
        label.verticalOverflow=VerticalWrapMode.Truncate;
        var shadow=label.gameObject.AddComponent<Shadow>();
        shadow.effectColor=new Color(0,0,0,.85f); shadow.effectDistance=new Vector2(1,-1);
        return label;
    }
    static void Style(Button button)
    {
        UIInkActionButton.Apply(button);
        button.GetComponent<UIInkActionButton>().设置绘制倍率(new Vector2(.90f,1.45f));
        var fluid=button.GetComponent<UIInkFluid>();
        fluid.位移强度=.12f; fluid.形变强度=.22f;
    }
    static void Place(RectTransform rt,float x0,float y0,float x1,float y1)
    { UIBuildUtils.Place(rt,new Vector2(x0,y0),new Vector2(x1,y1),Vector2.zero,Vector2.zero); }
}
