using UnityEngine;
using UnityEngine.UI;

/// <summary>背包运行时重构。保留原列表、详情、数据与使用按钮，不写场景。</summary>
public class UIInkBagPage : MonoBehaviour
{
    public UIInkWaterfall Waterfall { get; private set; }
    public int ParcelFrame { get; private set; }
    public bool AnimationFinished => elapsed>=1.5f || UIInkMotion.减少动效;
    UIEntryList list;
    UIEntryInfo info;
    Image parcel;
    Sprite[] frames;
    UIInkReveal reveal;
    Text countText;
    float elapsed;
    bool ready;
    readonly System.Collections.Generic.List<UIInkReveal> textReveals=new System.Collections.Generic.List<UIInkReveal>();
    float textAge=1;
    IPanelEntry lastSelection;
    public static void 应用(Transform root)
    {
        if(!UIInkNavigation.启用) return;
#if UNITY_EDITOR
        if(UnityEditor.EditorPrefs.GetBool("InkUI.QA.BagBefore",false)) return;
#endif
        var panel=root.GetComponent<CharacterPanelUI>();
        if(panel==null || panel.tabs.Count==0) return;
        var page=panel.tabs[0].page;
        var bag=page.GetComponent<UIInkBagPage>();
        if(bag==null) bag=page.AddComponent<UIInkBagPage>();
        bag.Build();
    }
    void Build()
    {
        if(ready) return;
        list=GetComponentInChildren<UIEntryList>(true); info=list!=null ? list.infoTarget : null;
        frames=Resources.LoadAll<Sprite>("UI/InkUI/Bag/bag-parcel-8f");
        System.Array.Sort(frames,(a,b)=>string.CompareOrdinal(a.name,b.name));
        if(list==null || info==null || frames.Length!=8) { Debug.LogWarning("[InkBag] requires original list/info and eight parcel frames"); return; }
        ready=true;
        var grid=list.transform as RectTransform;
        UIBuildUtils.Place(grid,new Vector2(.015f,.04f),new Vector2(.74f,.94f),Vector2.zero,Vector2.zero);
        var backing=grid.GetComponent<Image>(); if(backing!=null) { backing.enabled=true; backing.sprite=null; backing.color=Color.clear; backing.raycastTarget=true; }
        var title=grid.Find("Title"); if(title!=null) title.gameObject.SetActive(false);
        var gridMotion=grid.GetComponent<UIInkMotion>(); if(gridMotion!=null) gridMotion.enabled=false;
        var detail=info.transform as RectTransform;
        UIBuildUtils.Place(detail,new Vector2(.77f,.07f),new Vector2(.995f,.93f),Vector2.zero,Vector2.zero);
        var oldDetail=detail.GetComponent<Image>(); if(oldDetail!=null) { oldDetail.enabled=false; }
        var ink=UIBuildUtils.CreateImage("InkDetailBackground",detail,Color.white);
        ink.transform.SetAsFirstSibling(); ink.raycastTarget=false;
        InkUITheme.Image(ink,"Bag/bag-info-ink",false);
        UIBuildUtils.Stretch(ink.rectTransform); ink.rectTransform.offsetMin=new Vector2(-12,-12); ink.rectTransform.offsetMax=new Vector2(12,12);
        var detailTitle=detail.Find("Title"); if(detailTitle!=null) detailTitle.gameObject.SetActive(false);
        var detailMotion=detail.GetComponent<UIInkMotion>(); if(detailMotion!=null) detailMotion.enabled=false;
        var desc=transform.Find("ItemDesc");
        if(info.descriptionText!=null) {
            info.descriptionText.transform.SetParent(detail,false);
            UIBuildUtils.Place(info.descriptionText.rectTransform,new Vector2(0,.24f),new Vector2(1,.53f),new Vector2(26,10),new Vector2(-26,-10));
            info.descriptionText.fontSize=19;
        }
        if(desc!=null) desc.gameObject.SetActive(false);
        if(info.iconImage!=null) {
            UIBuildUtils.Place(info.iconImage.rectTransform,new Vector2(.5f,.75f),new Vector2(.5f,.75f),new Vector2(-64,-64),new Vector2(64,64));
            info.iconImage.preserveAspect=true;
        }
        Place(info.nameText,new Vector2(0,.58f),new Vector2(1,.68f),26);
        if(info.nameText!=null) info.nameText.alignment=TextAnchor.MiddleCenter;
        Place(info.tierText,new Vector2(0,.54f),new Vector2(.55f,.59f),17);
        Place(info.kindText,new Vector2(.55f,.54f),new Vector2(1,.59f),17);
        if(info.actionButton!=null) {
            UIBuildUtils.Place(info.actionButton.transform as RectTransform,new Vector2(.12f,.07f),new Vector2(.88f,.14f),Vector2.zero,Vector2.zero);
            info.actionLabel.fontSize=23;
        }
        countText=UIBuildUtils.CreateText("BagQuantity",detail,list.font,"",18,TextAnchor.MiddleCenter,InkUITheme.Ink);
        Place(countText,new Vector2(0,.17f),new Vector2(1,.22f),18);
        foreach(var text in detail.GetComponentsInChildren<Text>(true)) {
            text.color=new Color(.98f,.96f,.90f);
            var shadow=text.GetComponent<Shadow>(); if(shadow==null) shadow=text.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0,0,0,.9f); shadow.effectDistance=new Vector2(1,-1);
        }
        parcel=UIBuildUtils.CreateImage("InkBagParcel",transform,Color.white); parcel.sprite=frames[0]; parcel.preserveAspect=true;
        parcel.rectTransform.anchorMin=parcel.rectTransform.anchorMax=new Vector2(.36f,.54f);
        parcel.rectTransform.sizeDelta=new Vector2(250,250);
        reveal=parcel.gameObject.AddComponent<UIInkReveal>();
        parcel.raycastTarget=true;
        var parcelClick=parcel.gameObject.AddComponent<Button>(); parcelClick.transition=Selectable.Transition.None; parcelClick.targetGraphic=parcel; parcelClick.onClick.AddListener(Skip);
        Waterfall=grid.gameObject.AddComponent<UIInkWaterfall>(); Waterfall.Initialize(list);
        Waterfall.AnimationLayer=transform;
        foreach(var text in new[]{info.nameText,info.tierText,info.kindText,info.descriptionText})
            if(text!=null) { var effect=text.GetComponent<UIInkReveal>(); if(effect==null) effect=text.gameObject.AddComponent<UIInkReveal>(); textReveals.Add(effect); }
        list.SelectionChanged+=Selection;
        list.RebuildFromSource();
        if(gameObject.activeInHierarchy) StartOpening();
    }
    static void Place(Text text,Vector2 min,Vector2 max,int size)
    { if(text==null) return; UIBuildUtils.Place(text.rectTransform,min,max,new Vector2(24,0),new Vector2(-24,0)); text.fontSize=size; text.color=InkUITheme.Ink; }
    void OnEnable() { if(ready) StartOpening(); }
    void StartOpening()
    {
        parcel.gameObject.SetActive(true); parcel.color=Color.white;
        elapsed=0; ParcelFrame=0; parcel.sprite=frames[0];
        Canvas.ForceUpdateCanvases();
        Waterfall.LaunchPoint=Waterfall.Owner.container.InverseTransformPoint(parcel.transform.position);
        Waterfall.Open();
        if(UIInkMotion.减少动效) Skip();
    }
    public void Skip() { elapsed=10; ParcelFrame=7; if(parcel!=null) { parcel.sprite=frames[7]; parcel.gameObject.SetActive(false); } if(reveal!=null) reveal.进度=1; Waterfall?.Skip(); }
    void Selection(IPanelEntry entry) { if(lastSelection==entry) return; lastSelection=entry; textAge=0; }
    void Update()
    {
        if(!ready) return;
        elapsed+=Time.unscaledDeltaTime;
        textAge+=Time.unscaledDeltaTime;
        foreach(var text in textReveals) text.进度=UIInkMotion.减少动效 ? 1 : Mathf.Clamp01(textAge/.14f);
        if(UIInkMotion.减少动效) Skip();
        reveal.进度=Mathf.Clamp01(elapsed/.18f);
        ParcelFrame=Mathf.Clamp(Mathf.FloorToInt((elapsed-.18f)/.30f*8),0,7); parcel.sprite=frames[ParcelFrame];
        parcel.color=new Color(1,1,1,1-Mathf.SmoothStep(.48f,.78f,elapsed));
        if(elapsed>=.78f) parcel.gameObject.SetActive(false);
        countText.text=info.Current is ItemDefinition item && list.data!=null ? "持有 ×"+list.data.物品数量(item) : "";
        if(info.kindText!=null) info.kindText.color=new Color(.92f,.94f,.86f);
        // Missing data artwork must not become a solid quality-color square.
        if(info.iconImage!=null) info.iconImage.enabled=info.Current!=null && info.Current.DisplayIcon!=null;
    }
    void OnDestroy() { if(list!=null) list.SelectionChanged-=Selection; }
}
