using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>共用宣纸布局：四类行囊或双环装备舞台，以及面板外信息卡。</summary>
public class UIInkBagPage : MonoBehaviour
{
    public UIInkWaterfall Waterfall=>null;public int ParcelFrame=>7;public bool AnimationFinished=>true;
    public UIInkLawPage Equipment=>panel&&panel.tabs.Count>4?panel.tabs[4].page.GetComponent<UIInkLawPage>():null;public int Category {get;private set;}
    public UIAttributeList Attributes {get;private set;}public ScrollRect AttributeScroll {get;private set;}
    public int EntryCount=>entries.Count;public int VisibleCellCount=>cells.Count;
    public IReadOnlyList<IPanelEntry> Entries=>entries;
    public bool EquipmentMode {get;private set;}public UIPaperEquipmentOrbit Orbit {get;private set;}
    UIPanelData data;CharacterPanelUI panel;UIEntryInfo info;Font font;RectTransform sheet,grid,tooltip,itemActions,realmSeal,sortButton;ScrollRect scroll;Scrollbar inventoryBar;CanvasGroup actionGroup;float actionAlpha;
    const int InventoryColumns=5,InventoryRows=5;const float CellGap=7;
    Text realm,gongfa,nameLabel,countLabel,description,tier;Image tooltipHeader,tooltipIcon,realmRing,portrait;Button lawAction;float realmAge;Material realmMaterial,paperMaterial;CanvasGroup realmText,gongfaText;
    readonly Vector3[] paperCorners=new Vector3[4];
    readonly List<IPanelEntry> entries=new List<IPanelEntry>();readonly List<UIPaperInventoryCell> cells=new List<UIPaperInventoryCell>();readonly List<Button> filters=new List<Button>();
    bool built,hovering,pinned;IPanelEntry shown;float nextStats,leaveTime;Text equipmentEmpty;
    static readonly string[] BagNames={"学习","使用","草药","其他"},EquipmentNames={"神通","法则","武器"};
    static readonly string[] BagIcons={"learning","usable","herbs","misc"},EquipmentIcons={"learning","misc","equipment"};
    string[] Names=>EquipmentMode?EquipmentNames:BagNames;string[] CategoryIcons=>EquipmentMode?EquipmentIcons:BagIcons;
    int FilterValue(int index)=>EquipmentMode?index:index+1;
    public static void 应用(Transform root)
    {
        if(!UIInkNavigation.启用)return;var p=root.GetComponent<CharacterPanelUI>();if(!p||p.tabs.Count==0)return;
        var view=p.tabs[0].page.GetComponent<UIInkBagPage>()??p.tabs[0].page.AddComponent<UIInkBagPage>();view.Build(p,root.GetComponent<UIPanelData>());view.Presentation();
    }
    void Build(CharacterPanelUI owner,UIPanelData source)
    {
        if(built||!source)return;panel=owner;data=source;font=Resources.Load<Font>("Fonts/SimHei")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Category=EquipmentMode?0:1;
        var oldList=GetComponentsInChildren<UIEntryList>(true).FirstOrDefault(l=>l.name=="KnownList")??GetComponentInChildren<UIEntryList>(true);info=oldList?oldList.infoTarget:null;
        foreach(Transform child in transform)child.gameObject.SetActive(false);if(oldList)oldList.enabled=false;
        var image=GetComponent<Image>();if(image)image.enabled=false;
        transform.SetParent(panel.panelRoot.transform,false);UIBuildUtils.Stretch((RectTransform)transform);built=true;
        var paper=UIBuildUtils.CreateImage("InventoryPaper",transform,Color.clear);paper.raycastTarget=true;sheet=paper.rectTransform;UIPaperArt.Place(sheet,.635f,0,1,1);
        // 边缘按接近原图的宽高比整张显示；纸面向右延展，不用九宫格挤窄左边缘。
        var edge=UIBuildUtils.CreateImage("LayeredPaperEdge",sheet,Color.white);edge.sprite=UIPaperArt.Paper;edge.raycastTarget=false;UIPaperArt.Place(edge.rectTransform,-.53f,0,.47f,1);
        var surface=UIBuildUtils.CreateRect("CleanPaperSurface",sheet).gameObject.AddComponent<RawImage>();surface.texture=UIPaperArt.Paper.texture;surface.uvRect=new Rect(.999f,0,.001f,1);surface.raycastTarget=false;UIPaperArt.Place(surface.rectTransform,.469f,0,1,1);
        var paperShader=Resources.Load<Shader>("UI/InkUI/Bag/PaperWash");if(paperShader){paperMaterial=new Material(paperShader);edge.material=surface.material=paperMaterial;}
        var title=Label(sheet,"InventoryTitle",EquipmentMode?"装\n备":"行\n囊",38,.18f,.78f,.29f,.975f);title.alignment=TextAnchor.UpperLeft;title.lineSpacing=1.25f;
        var close=UIBuildUtils.CreateButton("PaperClose",sheet,font,"",26);var closeRect=(RectTransform)close.transform;closeRect.anchorMin=closeRect.anchorMax=new Vector2(.925f,.963f);closeRect.sizeDelta=Vector2.one*46;closeRect.anchoredPosition=Vector2.zero;UIPaperArt.Button(close);close.GetComponent<UIPaperButton>().关闭=true;close.onClick.AddListener(()=>panel.SetOpen(false));
        if(EquipmentMode){var orbit=UIBuildUtils.CreateRect("EquipmentOrbit",sheet);UIPaperArt.Place(orbit,.11f,.585f,.99f,.94f);Orbit=orbit.gameObject.AddComponent<UIPaperEquipmentOrbit>();Orbit.Initialize(this,data,font);}
        else {
        var pattern=UIBuildUtils.CreateImage("CharacterCloudSeal",sheet,Color.white);pattern.sprite=UIPaperArt.Pattern;pattern.raycastTarget=false;pattern.preserveAspect=true;UIPaperArt.Place(pattern.rectTransform,.35f,.57f,.99f,.98f);
        portrait=UIBuildUtils.CreateImage("CurrentOutfitPortrait",sheet,Color.white);portrait.preserveAspect=true;portrait.raycastTarget=false;UIPaperArt.Place(portrait.rectTransform,.36f,.55f,.98f,.98f);portrait.gameObject.AddComponent<UIPaperPortraitFade>();sheet.gameObject.AddComponent<UIPaperPortraitDrop>().Owner=this;
        realmSeal=UIBuildUtils.CreateRect("RealmInkSeal",sheet);UIPaperArt.Place(realmSeal,.18f,.61f,.42f,.77f);
        realmRing=UIBuildUtils.CreateImage("BrushRing",realmSeal,new Color(.30f,.43f,.36f));realmRing.sprite=Resources.Load<Sprite>("UI/InkUI/Realm/fx-ink-circle");realmRing.preserveAspect=true;UIBuildUtils.Stretch(realmRing.rectTransform);realmRing.type=Image.Type.Filled;realmRing.fillMethod=Image.FillMethod.Radial360;realmRing.fillOrigin=(int)Image.Origin360.Left;realmMaterial=new Material(Shader.Find("UI/InkCircle"));realmRing.material=realmMaterial;
        realm=Label(realmSeal,"RealmSummary","",22,.10f,.43f,.90f,.75f);realm.resizeTextForBestFit=true;realm.resizeTextMinSize=12;realm.resizeTextMaxSize=22;
        gongfa=Label(realmSeal,"CurrentGongfa","",17,.10f,.22f,.90f,.44f);gongfa.alignment=TextAnchor.MiddleCenter;gongfa.resizeTextForBestFit=true;gongfa.resizeTextMinSize=10;gongfa.resizeTextMaxSize=17;
        realmText=realm.gameObject.AddComponent<CanvasGroup>();gongfaText=gongfa.gameObject.AddComponent<CanvasGroup>();realmText.alpha=gongfaText.alpha=0;
        BuildAttributes();
        }
        if(!EquipmentMode)BuildAppearanceEntry();
        for(int i=0;i<Names.Length;i++)
        {
            int category=FilterValue(i);var b=UIBuildUtils.CreateButton("Category_"+category,sheet,font,Names[i],16);
            UIPaperArt.Place((RectTransform)b.transform,.18f+i*.65f/Names.Length,.54f,.18f+(i+1)*.65f/Names.Length,.574f);
            var label=b.GetComponentInChildren<Text>();label.alignment=TextAnchor.MiddleLeft;UIPaperArt.Place(label.rectTransform,.44f,0,.97f,1);
            var icon=UIBuildUtils.CreateImage("CategoryIcon",b.transform,Color.white);icon.sprite=Resources.Load<Sprite>("UI/InkUI/Bag/Categories/"+CategoryIcons[i]);icon.preserveAspect=true;icon.raycastTarget=false;UIPaperArt.Place(icon.rectTransform,.10f,.13f,.39f,.87f);
            UIPaperArt.Button(b);b.GetComponent<UIPaperButton>().分类=true;b.GetComponent<UIPaperButton>().BaseColor=new Color(.36f,.58f,.50f,.28f);b.onClick.AddListener(()=>SetCategory(category));filters.Add(b);
        }
        var host=UIBuildUtils.CreateRect("InventoryScroll",sheet);UIPaperArt.Place(host,.18f,.105f,.96f,.53f);var hit=host.gameObject.AddComponent<Image>();hit.color=Color.clear;scroll=host.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=36;scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=UIBuildUtils.CreateRect("Viewport",host);UIBuildUtils.Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();scroll.viewport=viewport;
        grid=UIBuildUtils.CreateRect("InventoryGrid",viewport);grid.anchorMin=new Vector2(0,1);grid.anchorMax=Vector2.one;grid.pivot=new Vector2(.5f,1);grid.sizeDelta=Vector2.zero;scroll.content=grid;
        var layout=grid.gameObject.AddComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=InventoryColumns;layout.spacing=Vector2.one*CellGap;layout.childAlignment=TextAnchor.UpperLeft;grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        BuildInventoryScrollbar();
        if(EquipmentMode){viewport.gameObject.AddComponent<UIPaperEquipmentReturnDrop>().Owner=Orbit;equipmentEmpty=Label(viewport,"EquipmentEmpty","尚未获得武器",18,.04f,.45f,.96f,.58f);equipmentEmpty.color=new Color(.24f,.37f,.31f,.65f);equipmentEmpty.gameObject.SetActive(false);}
        else {var sort=UIBuildUtils.CreateButton("InventorySort",sheet,font,"一键整理",18);sortButton=(RectTransform)sort.transform;UIPaperArt.Place(sortButton,.27f,.035f,.73f,.086f);UIPaperArt.Button(sort);sort.GetComponent<UIPaperButton>().Brush=true;sort.image.sprite=UIPaperArt.SortBrush;sort.image.type=Image.Type.Simple;sort.onClick.AddListener(SortInventory);}
        BuildTooltip();panel.TabChanged+=TabChanged;data.Changed+=Refresh;玩家外观.外观变化+=AppearanceChanged;Refresh();RefreshStats();RefreshPortrait();
    }
    public void BuildEquipment(CharacterPanelUI owner,UIPanelData source){if(built)return;EquipmentMode=true;Build(owner,source);}
    void BuildInventoryScrollbar()
    {
        var track=UIBuildUtils.CreateImage("InventoryScrollbar",scroll.transform,Color.clear);track.raycastTarget=true;var r=track.rectTransform;r.anchorMin=new Vector2(1,0);r.anchorMax=Vector2.one;r.sizeDelta=new Vector2(22,-8);
        var rail=UIBuildUtils.CreateImage("InkTrack",track.transform,new Color(.22f,.38f,.31f,.16f));rail.raycastTarget=false;UIPaperArt.Place(rail.rectTransform,.46f,0,.54f,1);
        var area=UIBuildUtils.CreateRect("SlidingArea",track.transform);UIBuildUtils.Stretch(area);
        var handle=UIBuildUtils.CreateImage("Handle",area,Color.clear);handle.raycastTarget=true;UIBuildUtils.Stretch(handle.rectTransform);
        var thumb=UIBuildUtils.CreateImage("InkThumb",handle.transform,new Color(.23f,.43f,.35f,.78f));thumb.raycastTarget=false;UIPaperArt.Place(thumb.rectTransform,.38f,0,.62f,1);
        inventoryBar=track.gameObject.AddComponent<Scrollbar>();inventoryBar.direction=Scrollbar.Direction.BottomToTop;inventoryBar.handleRect=handle.rectTransform;inventoryBar.targetGraphic=thumb;
        var colors=inventoryBar.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.72f,.87f,.77f,1);colors.pressedColor=new Color(.48f,.67f,.56f,1);colors.disabledColor=new Color(1,1,1,.16f);colors.fadeDuration=.07f;inventoryBar.colors=colors;
        scroll.verticalScrollbar=inventoryBar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
    }
    void BuildAttributes()
    {
        var host=UIBuildUtils.CreateRect("RealmAttributes",sheet);UIPaperArt.Place(host,.18f,.585f,.96f,.77f);host.gameObject.AddComponent<Image>().color=Color.clear;
        AttributeScroll=host.gameObject.AddComponent<ScrollRect>();AttributeScroll.horizontal=false;AttributeScroll.scrollSensitivity=32;AttributeScroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=UIBuildUtils.CreateRect("Viewport",host);UIBuildUtils.Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
        viewport.gameObject.AddComponent<UIInkViewportFade>().FadeWidth=14;
        var content=UIBuildUtils.CreateRect("Attributes",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<UIInkRealmVerticalLayout>();layout.childControlHeight=true;layout.childControlWidth=false;layout.childForceExpandHeight=layout.childForceExpandWidth=false;layout.spacing=2;layout.padding=new RectOffset(0,0,12,12);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        Attributes=host.gameObject.AddComponent<UIAttributeList>();Attributes.container=content;Attributes.font=font;Attributes.source=data.玩家属性;Attributes.hideZero=false;Attributes.fontSize=17;Attributes.rowHeight=25;Attributes.Rebuild();AttributeScroll.viewport=viewport;AttributeScroll.content=content;
        var bar=UIBuildUtils.AddVerticalScrollbar(AttributeScroll,5,0,4);viewport.offsetMax=Vector2.zero;bar.GetComponent<Image>().color=new Color(.27f,.48f,.41f,.14f);bar.handleRect.GetComponent<Image>().color=new Color(.27f,.48f,.41f,.52f);
        var flow=host.gameObject.AddComponent<UIInkRealmAttributeFlow>();flow.墨圈=realmRing;flow.属性=Attributes;flow.属性滚动=AttributeScroll;
    }
    void BuildAppearanceEntry()
    {
        var button=UIBuildUtils.CreateButton("AppearanceEntry",sheet,font,"外观",15);var rect=(RectTransform)button.transform;
        rect.anchorMin=rect.anchorMax=new Vector2(.925f,.865f);rect.sizeDelta=Vector2.one*66;rect.anchoredPosition=Vector2.zero;
        var label=button.GetComponentInChildren<Text>();UIPaperArt.Place(label.rectTransform,.12f,.15f,.88f,.43f);
        var icon=UIBuildUtils.CreateImage("AppearanceIcon",button.transform,Color.white);icon.sprite=Resources.Load<Sprite>("UI/InkUI/Bag/Categories/appearance");icon.preserveAspect=true;icon.raycastTarget=false;UIPaperArt.Place(icon.rectTransform,.29f,.43f,.71f,.84f);
        UIPaperArt.Button(button);button.GetComponent<UIPaperButton>().圆形=true;button.gameObject.AddComponent<UIPaperRoundHit>();
        button.onClick.AddListener(()=>panel.ShowTab(CharacterTab.外观));
        var old=panel.tabs[(int)CharacterTab.外观].button;if(old)old.gameObject.SetActive(false);
    }
    void AppearanceChanged(AppearanceDefinition appearance)=>RefreshPortrait();
    void RefreshPortrait(){if(EquipmentMode){if(Orbit)Orbit.SyncPortrait();return;}var actor=FindObjectOfType<玩家外观>();string id=actor&&actor.当前外观?actor.当前外观.id:"app_player";portrait.sprite=Resources.Load<Sprite>("UI/InkUI/Appearance/portrait-"+id);portrait.enabled=portrait.sprite;}
    Text Label(Transform p,string n,string s,int size,float x,float y,float xx,float yy){var t=UIPaperArt.Label(p,n,s,size,TextAnchor.MiddleCenter,UIPaperArt.Ink);UIPaperArt.Place(t.rectTransform,x,y,xx,yy);return t;}
    void BuildTooltip()
    {
        var image=UIBuildUtils.CreateImage("InventoryTooltip",transform,Color.white);image.sprite=UIPaperArt.Tooltip;image.raycastTarget=true;tooltip=image.rectTransform;UIPaperArt.Place(tooltip,.365f,.127f,.538f,.717f);
        tooltipHeader=UIBuildUtils.CreateImage("QualityHeader",tooltip,Color.white);tooltipHeader.sprite=UIPaperArt.Header;UIBuildUtils.Stretch(tooltipHeader.rectTransform);tooltipHeader.raycastTarget=false;
        nameLabel=Label(tooltip,"ItemName","",22,0,0,1,1);nameLabel.alignment=TextAnchor.UpperLeft;UIBuildUtils.Place(nameLabel.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(24,-90),new Vector2(-24,-32));
        var nameDivider=UIBuildUtils.CreateImage("NameDivider",tooltip,UIPaperArt.Ivory);nameDivider.sprite=UIPaperArt.Divider;nameDivider.raycastTarget=false;
        countLabel=Label(tooltip,"OwnedCount","",16,0,0,1,1);countLabel.alignment=TextAnchor.UpperLeft;
        tooltipIcon=UIBuildUtils.CreateImage("ItemIcon",tooltip,Color.white);tooltipIcon.preserveAspect=true;tooltipIcon.raycastTarget=false;tooltipIcon.rectTransform.anchorMin=tooltipIcon.rectTransform.anchorMax=Vector2.one;tooltipIcon.rectTransform.sizeDelta=Vector2.one*104;tooltipIcon.rectTransform.anchoredPosition=new Vector2(-76,-120);
        tier=Label(tooltip,"ItemTier","",16,0,0,1,1);tier.alignment=TextAnchor.UpperLeft;UIBuildUtils.Place(tier.rectTransform,new Vector2(0,1),new Vector2(.52f,1),new Vector2(24,-148),new Vector2(0,-124));
        var divider=UIBuildUtils.CreateImage("HeaderDivider",tooltip,UIPaperArt.Ivory);divider.sprite=UIPaperArt.Divider;divider.raycastTarget=false;UIBuildUtils.Place(divider.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(24,-193),new Vector2(-24,-192));
        var area=UIBuildUtils.CreateRect("DescriptionScroll",tooltip);UIBuildUtils.Place(area,Vector2.zero,Vector2.one,new Vector2(24,88),new Vector2(-24,-212));var scrollView=area.gameObject.AddComponent<ScrollRect>();scrollView.horizontal=false;scrollView.scrollSensitivity=28;
        var view=UIBuildUtils.CreateRect("Viewport",area);UIBuildUtils.Stretch(view);view.gameObject.AddComponent<RectMask2D>();view.gameObject.AddComponent<Image>().color=Color.clear;
        description=UIPaperArt.Label(view,"Description","",18,TextAnchor.UpperLeft,UIPaperArt.Ivory);description.rectTransform.anchorMin=new Vector2(0,1);description.rectTransform.anchorMax=Vector2.one;description.rectTransform.pivot=new Vector2(.5f,1);description.rectTransform.sizeDelta=Vector2.zero;description.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scrollView.content=description.rectTransform;scrollView.viewport=view;
        itemActions=UIBuildUtils.CreateRect("InventoryItemAction",transform);itemActions.anchorMin=itemActions.anchorMax=Vector2.zero;itemActions.pivot=new Vector2(0,.5f);itemActions.sizeDelta=new Vector2(104,36);actionGroup=itemActions.gameObject.AddComponent<CanvasGroup>();actionGroup.alpha=0;
        if(info){info.transform.SetParent(tooltip,false);info.gameObject.SetActive(true);UIPaperArt.Place((RectTransform)info.transform,0,0,1,1);foreach(Transform c in info.transform)c.gameObject.SetActive(false);var bg=info.GetComponent<Image>();if(bg)bg.enabled=false;
            if(info.actionButton){info.actionButton.transform.SetParent(itemActions,false);UIBuildUtils.Stretch((RectTransform)info.actionButton.transform);UIPaperArt.Button(info.actionButton,true);}info.data=data;}
        lawAction=UIBuildUtils.CreateButton("LawEquipAction",itemActions,font,"装备法则",16);UIBuildUtils.Stretch((RectTransform)lawAction.transform);UIPaperArt.Button(lawAction,true);lawAction.onClick.AddListener(()=>{if(shown is WeaponDefinition weapon){data.装备武器(data.当前武器==weapon?null:weapon);return;}if(shown is LawFragment law){int index=data.法则装备.IndexOf(law.id);if(index>=0)LawFragmentSystem.卸下(data,index);else EquipLaw(law);}});lawAction.gameObject.SetActive(false);itemActions.gameObject.SetActive(false);
        tooltip.gameObject.SetActive(false);
    }
    public static int Classify(ItemDefinition item)
    {
        if(item==null)return 4;string id=(item.物品id??"").ToLowerInvariant(),name=item.物品名??"";
        if(灵植库.全部.Any(p=>p.种子id==item.物品id||p.产物id==item.物品id))return 3;
        if(id.Contains("seed")||id.Contains("herb")||name.Contains("种子")||name.Contains("灵草")||name.Contains("草药"))return 3;
        if(item.使用效果 is 学功法效果||item.使用效果 is 学主动神通效果||item.使用效果 is 学被动神通效果||item.使用效果 is 学丹方效果||item.使用效果 is 学坐骑效果)return 1;
        return item.可使用?2:4;
    }
    public void SetCategory(int index){Category=Mathf.Clamp(index,EquipmentMode?0:1,EquipmentMode?2:4);pinned=false;HideTooltip();Refresh();scroll.StopMovement();Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=1;}
    public void SortInventory()
    {
        data.物品.Sort((a,b)=>{if(!a)return b?1:0;if(!b)return -1;int category=Classify(a).CompareTo(Classify(b));if(category!=0)return category;int quality=b.品阶.CompareTo(a.品阶);return quality!=0?quality:StringComparer.Ordinal.Compare(a.DisplayName,b.DisplayName);});
        data.法则碎片.Sort((a,b)=>{if(a==null)return b==null?0:1;if(b==null)return -1;int level=b.境界.CompareTo(a.境界);return level!=0?level:b.评分.CompareTo(a.评分);});
        data.RaiseChanged();scroll.verticalNormalizedPosition=1;ToastUI.提示("已按分类、品阶与名称整理");
    }
    void Refresh()
    {
        if(!built)return;entries.Clear();
        if(EquipmentMode){if(Category==0)entries.AddRange(data.GetAbilities().Distinct().OrderBy(e=>e is PassiveDivineAbility?1:0).ThenByDescending(e=>e.DisplayTier).ThenBy(e=>e.DisplayName,StringComparer.Ordinal));if(Category==1)entries.AddRange(data.法则碎片.Where(l=>l!=null&&!data.炉中法则.Contains(l.id)).OrderByDescending(l=>l.境界).ThenByDescending(l=>l.评分).Cast<IPanelEntry>());if(Category==2)entries.AddRange(data.已获得武器.Where(w=>w).Cast<IPanelEntry>());if(equipmentEmpty)equipmentEmpty.gameObject.SetActive(Category==2&&entries.Count==0);}
        else entries.AddRange(data.GetItems().OfType<ItemDefinition>().Distinct().Where(i=>Classify(i)==Category).OrderByDescending(i=>i.品阶).ThenBy(i=>i.DisplayName,StringComparer.Ordinal));
        int total=Mathf.Max(InventoryColumns*InventoryRows,Mathf.CeilToInt((float)entries.Count/InventoryColumns)*InventoryColumns);for(int i=cells.Count;i<total;i++){var b=UIBuildUtils.CreateButton("InventoryCell_"+i,grid,font,"",18);var cell=b.gameObject.AddComponent<UIPaperInventoryCell>();cell.Initialize(this);cells.Add(cell);}
        for(int i=0;i<cells.Count;i++){cells[i].gameObject.SetActive(i<total);if(i<total)cells[i].Bind(i<entries.Count?entries[i]:null,data);}
        for(int i=0;i<filters.Count;i++){filters[i].GetComponent<UIPaperButton>().Selected=FilterValue(i)==Category;filters[i].GetComponentInChildren<Text>().text=Names[i];}
        if(shown!=null){if(shown is ItemDefinition item&&data.物品数量(item)<=0||shown is LawFragment law&&!data.法则碎片.Contains(law))HideTooltip();else {bool wasHovering=hovering;ShowTooltip(shown,false);hovering=wasHovering;}}
    }
    void RefreshStats(){if(!data||!realm)return;realm.text=data.GetRealmName();gongfa.text=data.当前功法?data.当前功法.DisplayName:"尚未修习功法";}
    public void ShowTooltip(IPanelEntry entry,bool pin)
    {
        if(entry==null){if(!pinned)HideTooltip();return;}
        bool same=ReferenceEquals(shown,entry);if(!pin&&pinned&&!same)return;
        if(pin)pinned=!(pinned&&same);else if(!same)pinned=false;
        shown=entry;hovering=true;tooltip.gameObject.SetActive(true);tooltip.SetAsLastSibling();nameLabel.text=entry.DisplayName;tooltipIcon.sprite=UIInkAbilityArt.Icon(entry);tooltipIcon.enabled=tooltipIcon.sprite;tooltipIcon.color=entry is LawFragment l?l.颜色:Color.white;
        tooltipHeader.color=Color.Lerp(new Color(.055f,.20f,.23f),UIEntryRow.TierColor(entry.DisplayTier),.36f);countLabel.text=entry is ItemDefinition item?"持有  "+data.物品数量(item):entry is LawFragment law&&data.法则装备.Contains(law.id)?"已装备":"持有  1";tier.text=entry.DisplayTier.ToString();description.text=entry.DisplayDescription;
        if(entry is WeaponDefinition weapon)countLabel.text=data.当前武器==weapon?"已拥有 · 已装备":"已拥有";
        if(entry is ActiveDivineAbility active){countLabel.text=data.IsEquipped(active)?"已掌握 · 已装备":"已掌握";description.text+="\n\n灵力消耗  "+active.消耗灵力.ToString("0.#")+"\n冷却  "+active.冷却时间.ToString("0.#")+" 秒";}
        if(entry is PassiveDivineAbility passive){countLabel.text=data.IsPassiveEnabled(passive)?"已掌握 · 已启用":"已掌握 · 未启用";description.text+="\n\n被动神通 · 维持消耗  "+passive.维持消耗灵力.ToString("0.#");}
        foreach(var t in new[]{nameLabel,countLabel,tier,description})t.color=UIPaperArt.Ivory;
        if(info){info.Show(entry);if(info.actionButton)UIPaperArt.Button(info.actionButton,true);}
        if(entry is LawFragment fragment)lawAction.GetComponentInChildren<Text>().text=data.法则装备.Contains(fragment.id)?"卸下法则":"装备法则";
        if(entry is WeaponDefinition w)lawAction.GetComponentInChildren<Text>().text=data.当前武器==w?"卸下武器":"装备武器";
        // 卡片随真实描述收紧；长文保留内部滚动，避免短物品也留下半屏空黑底。
        float titleHeight=Mathf.Clamp(nameLabel.preferredHeight,28,58),extra=titleHeight-28;
        UIBuildUtils.Place((RectTransform)tooltip.Find("NameDivider"),new Vector2(0,1),Vector2.one,new Vector2(24,-68-extra),new Vector2(-24,-67-extra));
        UIBuildUtils.Place(countLabel.rectTransform,new Vector2(0,1),new Vector2(.52f,1),new Vector2(24,-102-extra),new Vector2(0,-78-extra));
        tooltipIcon.rectTransform.anchoredPosition=new Vector2(-76,-128-extra);UIBuildUtils.Place(tier.rectTransform,new Vector2(0,1),new Vector2(.52f,1),new Vector2(24,-164-extra),new Vector2(0,-140-extra));
        UIBuildUtils.Place((RectTransform)tooltip.Find("HeaderDivider"),new Vector2(0,1),Vector2.one,new Vector2(24,-193-extra),new Vector2(-24,-192-extra));
        ((RectTransform)tooltip.Find("DescriptionScroll")).offsetMax=new Vector2(-24,-212-extra);
        float height=Mathf.Clamp(description.preferredHeight+300+extra,380+extra,((RectTransform)transform).rect.height*.68f);
        var min=tooltip.anchorMin;min.y=tooltip.anchorMax.y-height/((RectTransform)transform).rect.height;tooltip.anchorMin=min;
        if(pin&&!same)actionAlpha=0;UpdateItemAction();
    }
    public void LeaveCell(){hovering=false;leaveTime=Time.unscaledTime+.35f;}
    public bool 已选中(IPanelEntry entry)=>entry!=null&&pinned&&ReferenceEquals(shown,entry);
    public void HideTooltip(){shown=null;pinned=hovering=false;if(tooltip)tooltip.gameObject.SetActive(false);if(itemActions){itemActions.gameObject.SetActive(false);actionGroup.alpha=actionAlpha=0;}}
    public void BeginDrag(IPanelEntry e,PointerEventData pointer){HideTooltip();if(EquipmentMode){Orbit.BeginDrag(e,-1,false,pointer);return;}if(e is LawFragment law)Equipment.开始拖拽(law,-1,pointer);}
    public void EquipLaw(LawFragment law){if(law==null||!data.法则碎片.Contains(law))return;if(data.法则装备.Contains(law.id)){ToastUI.提示("该法则已装备");return;}int index=data.法则装备.FindIndex(string.IsNullOrEmpty);if(index<0){ToastUI.提示("六个法则槽已满，请拖到装备页的法则槽调整");return;}LawFragmentSystem.装备(data,law.id,index);}
    public void DropOnPortrait(){var law=UIDragContext.Entry as LawFragment;if(law==null)return;Equipment.结束拖拽(true);EquipLaw(law);}
    public void Drag(PointerEventData e){if(EquipmentMode)Orbit.Drag(e);else Equipment.拖动(e);}public void EndDrag(){if(EquipmentMode)Orbit.EndDrag();else Equipment.结束拖拽();}
    void TabChanged(CharacterTab tab){Presentation();HideTooltip();if(EquipmentMode&&tab==CharacterTab.法则)SetCategory(1);}
    void Presentation(){if(!built)return;bool paper=panel.IsOpen&&(panel.CurrentTab==CharacterTab.背包||panel.CurrentTab==CharacterTab.神通||panel.CurrentTab==CharacterTab.法则);var window=panel.panelRoot.transform.Find("Window");if(window)window.gameObject.SetActive(!paper);var dim=panel.panelRoot.transform.Find("Dim")?.GetComponent<Image>();if(dim){dim.sprite=null;dim.material=null;dim.color=new Color(.015f,.035f,.033f,paper?.57f:.48f);}}
    void Update(){if(!built)return;if(paperMaterial){sheet.GetWorldCorners(paperCorners);paperMaterial.SetVector("_PaperBounds",new Vector4(paperCorners[0].x,paperCorners[0].y,paperCorners[2].x-paperCorners[0].x,paperCorners[2].y-paperCorners[0].y));}realmAge+=Time.unscaledDeltaTime;if(realmRing)realmRing.fillAmount=UIInkMotion.减少动效?1:UIInkRealmPage.绘圈进度(realmAge);LayoutInventory();if(Time.unscaledTime>nextStats){nextStats=Time.unscaledTime+.5f;RefreshStats();}if(tooltip.gameObject.activeSelf&&!pinned&&!hovering&&Time.unscaledTime>leaveTime&&!RectTransformUtility.RectangleContainsScreenPoint(tooltip,Input.mousePosition,null)&&!UIDragContext.Dragging)HideTooltip();}
    void LayoutInventory()
    {
        var layout=grid.GetComponent<GridLayoutGroup>();float width=((RectTransform)scroll.transform).rect.width;
        // 保持原六列布局的格子尺寸，只删右侧一列；空出的整列用于细滚动条。
        float size=Mathf.Min((width-CellGap*5)/6,(scroll.viewport.rect.height-CellGap*(InventoryRows-1))/InventoryRows);
        if(size<=0)return;if(Mathf.Abs(layout.cellSize.x-size)>.2f)layout.cellSize=Vector2.one*size;
        scroll.viewport.offsetMax=new Vector2(-size-CellGap,0);((RectTransform)inventoryBar.transform).anchoredPosition=new Vector2(-size*.5f,0);
        float right=.18f+(InventoryColumns*size+(InventoryColumns-1)*CellGap)/sheet.rect.width;for(int i=0;i<filters.Count;i++){var rt=(RectTransform)filters[i].transform;UIPaperArt.Place(rt,Mathf.Lerp(.18f,right,(float)i/filters.Count),.54f,Mathf.Lerp(.18f,right,(float)(i+1)/filters.Count),.574f);rt.offsetMin=new Vector2(i==0?0:2,0);rt.offsetMax=new Vector2(i==filters.Count-1?0:-2,0);}
        if(!EquipmentMode&&realmSeal&&AttributeScroll){float shift=.96f-right;UIPaperArt.Place(realmSeal,.15f-shift,.59f,.45f-shift,.79f);float rail=.96f-size*.5f/sheet.rect.width;float left=.18f-shift;
            // 名称沿圆弧；数值末端距下方滚动条中线12单位，右侧栏宽按实际视口计算。
            UIPaperArt.Place((RectTransform)AttributeScroll.transform,left,.585f,rail,.77f);}
        if(sortButton){float center=(.18f+right)*.5f;UIPaperArt.Place(sortButton,center-.23f,.035f,center+.23f,.086f);}
        inventoryBar.interactable=grid.rect.height>scroll.viewport.rect.height+.5f;
    }
    public void Skip(){}
    void LateUpdate(){if(built&&realmText&&gongfaText)realmText.alpha=gongfaText.alpha=UIInkMotion.减少动效?1:Mathf.Clamp01((realmAge-UIInkRealmPage.绘圈时长)/.2f);if(built)UpdateItemAction();}
    void UpdateItemAction()
    {
        if(!itemActions)return;var cell=pinned?cells.FirstOrDefault(c=>c.gameObject.activeInHierarchy&&ReferenceEquals(c.Entry,shown)):null;
        bool visible=cell&&RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,cell.transform.position,null);
        bool actionable=shown is ItemDefinition item&&item.可使用||EquipmentMode&&(shown is ActiveDivineAbility||shown is PassiveDivineAbility);
        bool usable=visible&&actionable&&info&&info.actionButton&&info.actionButton.interactable;
        bool law=visible&&(shown is LawFragment||shown is WeaponDefinition);if(info&&info.actionButton)info.actionButton.gameObject.SetActive(usable);lawAction.gameObject.SetActive(law);
        if(!usable&&!law){itemActions.gameObject.SetActive(false);actionGroup.alpha=actionAlpha=0;return;}
        var corners=paperCorners;((RectTransform)cell.transform).GetWorldCorners(corners);var left=transform.InverseTransformPoint(corners[0]);var right=transform.InverseTransformPoint(corners[2]);sheet.GetWorldCorners(corners);float max=transform.InverseTransformPoint(corners[2]).x-12;
        float x=right.x+8;if(x+itemActions.rect.width>max)x=left.x-itemActions.rect.width-8;
        itemActions.localPosition=new Vector3(x,(left.y+right.y)*.5f,0);itemActions.gameObject.SetActive(true);itemActions.SetAsLastSibling();actionAlpha=UIInkMotion.减少动效?1:Mathf.MoveTowards(actionAlpha,1,Time.unscaledDeltaTime/.08f);actionGroup.alpha=actionAlpha;actionGroup.blocksRaycasts=actionGroup.interactable=true;
    }
    void OnEnable(){realmAge=0;if(built){if(realmText)realmText.alpha=gongfaText.alpha=0;if(AttributeScroll)AttributeScroll.verticalNormalizedPosition=1;Refresh();Presentation();RefreshPortrait();}}
    void OnDisable(){HideTooltip();if(Equipment)Equipment.结束拖拽(true);}
    void OnDestroy(){if(panel)panel.TabChanged-=TabChanged;if(data)data.Changed-=Refresh;玩家外观.外观变化-=AppearanceChanged;if(realmMaterial)Destroy(realmMaterial);if(paperMaterial)Destroy(paperMaterial);}
}

public sealed class UIPaperPortraitFade : BaseMeshEffect
{
    public override void ModifyMesh(VertexHelper mesh){if(!IsActive()||mesh.currentVertCount==0)return;UIVertex v=default;float bottom=float.MaxValue,top=float.MinValue;for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref v,i);bottom=Mathf.Min(bottom,v.position.y);top=Mathf.Max(top,v.position.y);}for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref v,i);float y=Mathf.InverseLerp(bottom,top,v.position.y);var color=v.color;color.a=(byte)(color.a*Mathf.Lerp(.08f,.88f,y));v.color=color;mesh.SetUIVertex(v,i);}}
}

public sealed class UIPaperPortraitDrop : MonoBehaviour,IDropHandler
{
    public UIInkBagPage Owner;
    public void OnDrop(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Owner.DropOnPortrait();}
}

public sealed class UIPaperInventoryCell : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
{
    UIInkBagPage owner;Image icon;Text count,fallback;UIPaperButton effect;public IPanelEntry Entry {get;private set;}
    public void Initialize(UIInkBagPage page){owner=page;var button=GetComponent<Button>();foreach(Transform c in transform)c.gameObject.SetActive(false);UIPaperArt.Button(button);button.onClick.AddListener(()=>owner.ShowTooltip(Entry,true));icon=UIBuildUtils.CreateImage("ItemIcon",transform,Color.white);icon.preserveAspect=true;icon.raycastTarget=false;UIPaperArt.Place(icon.rectTransform,.10f,.10f,.90f,.90f);fallback=UIPaperArt.Label(transform,"ItemNameFallback","",14,TextAnchor.MiddleCenter,UIPaperArt.Ink);UIPaperArt.Place(fallback.rectTransform,.10f,.22f,.90f,.86f);fallback.resizeTextForBestFit=true;fallback.resizeTextMinSize=10;fallback.resizeTextMaxSize=14;count=UIPaperArt.Label(transform,"Quantity","",17,TextAnchor.LowerRight,UIPaperArt.Ink);UIPaperArt.Place(count.rectTransform,.20f,.04f,.95f,.29f);}
    public void Bind(IPanelEntry entry,UIPanelData data){Entry=entry;icon.sprite=entry?.DisplayIcon;icon.enabled=icon.sprite;icon.color=entry is LawFragment law?law.颜色:Color.white;fallback.text=entry?.DisplayName;fallback.gameObject.SetActive(entry!=null&&!icon.sprite);count.text=entry is ItemDefinition item?data.物品数量(item).ToString():entry is LawFragment l&&data.法则装备.Contains(l.id)||entry is WeaponDefinition w&&data.当前武器==w?"已装备":"";var b=GetComponent<Button>();b.interactable=entry!=null;effect=b.GetComponent<UIPaperButton>();effect.物品格=true;effect.有物品=entry!=null;effect.Selected=owner.已选中(entry);effect.BaseColor=entry!=null?Color.Lerp(new Color(.68f,.76f,.70f),UIEntryRow.TierColor(entry.DisplayTier),.52f):new Color(.61f,.69f,.63f);}
    void Update(){if(effect)effect.Selected=owner.已选中(Entry);}
    public void OnPointerEnter(PointerEventData e){if(Entry!=null)owner.ShowTooltip(Entry,false);}public void OnPointerExit(PointerEventData e)=>owner.LeaveCell();
    public void OnBeginDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)owner.BeginDrag(Entry,e);}public void OnDrag(PointerEventData e){owner.Drag(e);}public void OnEndDrag(PointerEventData e){owner.EndDrag();}
}
