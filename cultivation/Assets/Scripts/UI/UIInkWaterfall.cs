using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>错列瀑布流：可选循环，仅创建可见范围的单元，数据仍由 UIEntryList 管理。</summary>
public class UIInkWaterfall : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public UIEntryList Owner { get; private set; }
    public int EntryCount => entries.Count;
    public int PoolCount => cells.Count;
    public float ScrollOffset => offset;
    public bool AllowLoop = true;
    public float MaxScrollOffset => viewport==null ? 0 : Mathf.Max(0,ContentHeight-viewport.rect.height);
    public bool Opening { get; private set; }
    public Vector2 LaunchPoint;
    public Transform AnimationLayer;
    int Columns=3;
    float Pitch=180;
    public int VisibleColumns => Columns;
    public float ItemScale { get; private set; }=1;
    public float BackdropScale { get; private set; }=1;
    readonly List<IPanelEntry> entries=new List<IPanelEntry>();
    readonly Dictionary<IPanelEntry,int> quantities=new Dictionary<IPanelEntry,int>();
    readonly List<UIInkWaterfallCell> cells=new List<UIInkWaterfallCell>();
    RectTransform viewport,content;
    Scrollbar scrollbar;
    Text empty;
    RawImage flow;
    UIInkViewportFade fade;
    Vector2 previousDrag;
    float offset,speed,openingAge;
    bool dragging,settingBar;
    public void Initialize(UIEntryList owner)
    {
        Owner=owner;
        var scroll=owner.GetComponent<ScrollRect>();
        viewport=scroll.viewport; content=scroll.content;
        fade=viewport.gameObject.AddComponent<UIInkViewportFade>();
        var area=viewport.GetComponent<Image>(); if(area==null) area=viewport.gameObject.AddComponent<Image>(); area.color=Color.clear; area.raycastTarget=true;
        if(scroll.verticalScrollbar==null) UIBuildUtils.AddVerticalScrollbar(scroll);
        scrollbar=scroll.verticalScrollbar; scroll.enabled=false;
        InkUITheme.ScrollbarStyle(scrollbar);
        if(scrollbar!=null) {
            scrollbar.onValueChanged.RemoveListener(BarChanged); scrollbar.onValueChanged.AddListener(BarChanged);
        }
        foreach(var layout in content.GetComponents<Behaviour>())
            if(layout is LayoutGroup || layout is ContentSizeFitter) layout.enabled=false;
        for(int i=content.childCount-1;i>=0;i--) { var old=content.GetChild(i); old.gameObject.SetActive(false); Destroy(old.gameObject); }
        content.anchorMin=Vector2.zero; content.anchorMax=Vector2.one;
        content.offsetMin=content.offsetMax=Vector2.zero; content.pivot=new Vector2(.5f,.5f);
        UIBuildUtils.Stretch(viewport); viewport.offsetMin=new Vector2(6,8); viewport.offsetMax=new Vector2(-22,-8);
        empty=UIBuildUtils.CreateText("Empty",viewport,owner.font,owner.source==ListSource.神通 ? "尚未掌握神通" : owner.source==ListSource.战阵成员 ? "尚未获得真灵" : "包裹中暂无物品",21,TextAnchor.MiddleCenter,new Color(.9f,.9f,.83f));
        UIBuildUtils.Stretch(empty.rectTransform); empty.raycastTarget=false;
        var overlay=UIBuildUtils.CreateRect("InkScrollFlow",viewport);
        overlay.anchorMin=new Vector2(.9f,0); overlay.anchorMax=Vector2.one; overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        flow=overlay.gameObject.AddComponent<RawImage>();
        flow.texture=Resources.Load<Texture2D>("UI/InkUI/Bag/fx-ink-flow-loop"); flow.raycastTarget=false; flow.color=new Color(.82f,.85f,.79f,0);
        owner.inkWaterfall=this;
    }
    public void SetEntries(IList<IPanelEntry> source)
    {
        entries.Clear(); quantities.Clear();
        if(source!=null) foreach(var entry in source) if(entry!=null) {
            if(quantities.TryGetValue(entry,out int count)) quantities[entry]=count+1;
            else { quantities.Add(entry,1); entries.Add(entry); }
        }
        empty.gameObject.SetActive(entries.Count==0);
        if(scrollbar!=null) scrollbar.gameObject.SetActive(entries.Count>0);
        if(entries.Count==0) offset=0;
        if(Owner.Selected==null || !entries.Contains(Owner.Selected)) Owner.Select(entries.Count>0 ? entries[0] : null);
        Refresh(true);
    }
    float Cycle => Mathf.Max(1,Mathf.CeilToInt(entries.Count/(float)Columns))*Pitch;
    public int Quantity(IPanelEntry entry) => entry!=null && quantities.TryGetValue(entry,out int value) ? value : 0;
    float ContentHeight => Cycle+(Columns-1)*25+36*ItemScale;
    bool Looping => AllowLoop && viewport!=null && Cycle>viewport.rect.height;
    bool Scrollable => Looping || MaxScrollOffset>0;
    public void Open()
    {
        openingAge=0; Opening=!UIInkMotion.减少动效; speed=0; Refresh(true);
    }
    public void Skip() { Opening=false; openingAge=10; Refresh(false); }
    public void ScrollBy(float pixels) { offset=Scrollable ? offset+pixels : 0; Refresh(false); }
    public void OnScroll(PointerEventData e)
    { Skip(); ScrollBy(-e.scrollDelta.y*78); speed=-e.scrollDelta.y*210; }
    public void OnBeginDrag(PointerEventData e)
    { Skip(); dragging=true; speed=0; previousDrag=Local(e.position,e.pressEventCamera); }
    public void OnDrag(PointerEventData e)
    {
        var point=Local(e.position,e.pressEventCamera); float delta=point.y-previousDrag.y;
        ScrollBy(delta); speed=delta/Mathf.Max(.001f,Time.unscaledDeltaTime); previousDrag=point;
    }
    public void OnEndDrag(PointerEventData e) { dragging=false; }
    public void OnDrop(PointerEventData e){
        if(Owner.source!=ListSource.神通 || UIDragContext.OriginSlot<0 || UIDragContext.OriginData!=Owner.data)return;
        int slot=UIDragContext.OriginSlot;
        if(slot<Owner.data.主动技能.Count && Owner.data.主动技能[slot]==UIDragContext.Entry as UnityEngine.Object){Owner.data.ClearSlot(slot);UIDragContext.End(true);UIInkPulse.Emit(transform as RectTransform,Vector2.zero,.25f);}
    }
    Vector2 Local(Vector2 screen,Camera camera)
    { RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport,screen,camera,out var result); return result; }
    void BarChanged(float value)
    { if(settingBar) return; Skip(); offset=(1-value)*(Looping ? Cycle : MaxScrollOffset); speed=0; Refresh(false); }
    void LateUpdate()
    {
        if(Owner==null) return;
        if(UIDragContext.Dragging && (Owner.source==ListSource.神通 || Owner.source==ListSource.战阵成员))return;
        float dt=Mathf.Min(Time.unscaledDeltaTime,.1f); openingAge+=dt;
        if(UIInkMotion.减少动效) Opening=false;
        if(Opening && openingAge>1.5f) Opening=false;
        if(!dragging && Mathf.Abs(speed)>.1f) { if(Scrollable) offset+=speed*dt; speed*=Mathf.Exp(-dt*7); }
        if(Looping && Mathf.Abs(offset)>100000) offset=Mathf.Repeat(offset,Cycle);
        Refresh(false);
        if(flow!=null) {
            var uv=flow.uvRect; uv.y=Time.unscaledTime*.12f; flow.uvRect=uv;
            flow.color=new Color(.82f,.85f,.79f,UIInkMotion.减少动效 ? 0 : Mathf.Min(.12f,Mathf.Abs(speed)/1800));
        }
    }
    void Refresh(bool rebind)
    {
        if(viewport==null) return;
        float desired=entries.Count<=4 ? 280 : entries.Count<=12 ? 240 : entries.Count<=30 ? 210 : entries.Count<=80 ? 180 : 160;
        if(Owner.source==ListSource.神通 || Owner.source==ListSource.战阵成员)desired=170;
        Columns=Mathf.Clamp(Mathf.FloorToInt(viewport.rect.width/desired),1,Mathf.Min(7,Mathf.Max(1,entries.Count)));
        if(entries.Count<10) Columns=Mathf.Min(Columns,Mathf.Max(1,Mathf.CeilToInt(Mathf.Sqrt(entries.Count))));
        ItemScale=Mathf.Clamp((viewport.rect.width/Columns-20)/160,.88f,1.55f);
        BackdropScale=entries.Count<=9 ? 1 : entries.Count<=20 ? .93f : entries.Count<=40 ? .82f : .70f;
        Pitch=180*ItemScale;
        int rows=Mathf.CeilToInt(viewport.rect.height/Pitch)+3;
        int count=entries.Count==0 ? 0 : rows*Columns;
        while(cells.Count<count) {
            var cell=UIInkWaterfallCell.Create(content,Owner); cell.FadeMaterial=fade.Material;
            foreach(var graphic in cell.GetComponentsInChildren<Graphic>()) {
                var coords=graphic.gameObject.AddComponent<UIInkFadeCoordinates>(); coords.Viewport=viewport;
            }
            cell.GetComponent<UIInkFluid>().渐隐视口=viewport;
            cells.Add(cell);
        }
        float width=Mathf.Max(100,(viewport.rect.width-18)/Columns);
        if(!Looping) {
            offset=Mathf.Clamp(offset,0,MaxScrollOffset);
            if(offset<=0 && speed<0 || offset>=MaxScrollOffset && speed>0) speed=0;
        }
        int start=Scrollable ? Mathf.FloorToInt(offset/Pitch)-1 : 0;
        int dataRows=Mathf.Max(1,Mathf.CeilToInt(entries.Count/(float)Columns));
        for(int i=0;i<cells.Count;i++) {
            var cell=cells[i]; if(i>=count) { cell.gameObject.SetActive(false); continue; }
            cell.FadeMaterial=fade.Material;
            int row=start+i/Columns,col=i%Columns;
            int wrapped=Looping ? ((row%dataRows)+dataRows)%dataRows : row;
            int index=wrapped*Columns+col;
            if(index<0 || index>=entries.Count || !Looping && row>=dataRows) { cell.gameObject.SetActive(false); continue; }
            cell.gameObject.SetActive(true);
            if(rebind || cell.Entry!=entries[index]) cell.Bind(entries[index]);
            cell.Rect.sizeDelta=new Vector2(width-14,140*ItemScale);
            cell.Layout(ItemScale,BackdropScale);
            float top=Scrollable ? viewport.rect.height*.5f : Mathf.Min(viewport.rect.height*.5f,ContentHeight*.5f);
            Vector2 target=new Vector2(9+col*width+width*.5f,top-row*Pitch+offset-Pitch*.5f-col*25);
            target.x-=viewport.rect.width*.5f;
            target+=cell.Scatter;
            float progress=UIInkMotion.减少动效 || !Opening ? 1 : Mathf.Clamp01((openingAge-.48f-i*.025f)/.34f);
            float t=UIInkMotion.Timing.Cubic(progress);
            Vector2 position=Vector2.Lerp(LaunchPoint,target,t); position.y+=Mathf.Sin(t*Mathf.PI)*120;
            var parent=progress<1 && AnimationLayer!=null ? AnimationLayer : content;
            if(cell.transform.parent!=parent) cell.transform.SetParent(parent,false);
            cell.Rect.position=content.TransformPoint(position); cell.SetArrival(progress);
            cell.UpdateQuantity();
        }
        if(scrollbar!=null && entries.Count>0) {
            settingBar=true; scrollbar.size=Mathf.Clamp01(viewport.rect.height/(Looping ? Cycle : ContentHeight));
            scrollbar.SetValueWithoutNotify(Looping ? 1-Mathf.Repeat(offset,Cycle)/Cycle : MaxScrollOffset>0 ? 1-offset/MaxScrollOffset : 1); settingBar=false;
        }
    }
    void OnDisable() { dragging=false; speed=0; }
    void OnDestroy() { if(scrollbar!=null) scrollbar.onValueChanged.RemoveListener(BarChanged); }
}

/// <summary>固定命中单元；只牵引、放大图标，不移动名称或按钮。</summary>
public class UIInkWaterfallCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public IPanelEntry Entry { get; private set; }
    public Vector2 Scatter { get; private set; }
    public RectTransform Rect => transform as RectTransform;
    UIEntryList owner;
    Image icon,shadow;
    Text nameLabel,tier,quantity;
    CanvasGroup group;
    Vector2 velocity,shift;
    float scale=1;
    bool hovered;
    int lastQuantity=-1;
    bool lastSelected;
    UIInkFluid ink;
    float displayScale=-1;
    float backdropScale=-1;
    public Material FadeMaterial;
    Graphic[] visuals;
    UIInkSpiritCloud cloud;
    bool landed;
    public static UIInkWaterfallCell Create(RectTransform parent,UIEntryList owner)
    {
        var rt=UIBuildUtils.CreateRect("InkWaterfallCell",parent);
        var hit=rt.gameObject.AddComponent<Image>(); hit.color=Color.clear; hit.raycastTarget=true;
        var button=rt.gameObject.AddComponent<Button>(); button.transition=Selectable.Transition.None; button.targetGraphic=hit;
        var cell=rt.gameObject.AddComponent<UIInkWaterfallCell>(); cell.owner=owner;
        cell.group=rt.gameObject.AddComponent<CanvasGroup>();
        cell.shadow=UIBuildUtils.CreateImage("InkItemShadow",rt,new Color(.68f,.74f,.69f,.26f));
        cell.shadow.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4");
        cell.shadow.rectTransform.sizeDelta=new Vector2(108,108); cell.shadow.rectTransform.anchoredPosition=new Vector2(0,18);
        cell.icon=UIBuildUtils.CreateImage("ItemIcon",rt,Color.white); cell.icon.preserveAspect=true;
        cell.icon.rectTransform.sizeDelta=new Vector2(72,72); cell.icon.rectTransform.anchoredPosition=new Vector2(0,18);
        cell.nameLabel=UIBuildUtils.CreateText("Name",rt,owner.font,"",17,TextAnchor.MiddleCenter,new Color(.96f,.94f,.86f));
        cell.nameLabel.rectTransform.sizeDelta=new Vector2(118,48); cell.nameLabel.rectTransform.anchoredPosition=new Vector2(0,-60); cell.nameLabel.fontSize=16;
        cell.tier=UIBuildUtils.CreateText("Tier",rt,owner.font,"",13,TextAnchor.MiddleCenter,new Color(.82f,.86f,.79f));
        cell.tier.rectTransform.sizeDelta=new Vector2(140,20); cell.tier.rectTransform.anchoredPosition=new Vector2(0,-25);
        cell.quantity=UIBuildUtils.CreateText("Quantity",rt,owner.font,"",16,TextAnchor.MiddleRight,new Color(.98f,.96f,.87f));
        cell.quantity.rectTransform.sizeDelta=new Vector2(60,22); cell.quantity.rectTransform.anchoredPosition=new Vector2(28,48);
        foreach(var graphic in rt.GetComponentsInChildren<Graphic>()) if(graphic!=hit) graphic.raycastTarget=false;
        foreach(var text in rt.GetComponentsInChildren<Text>()) { var outline=text.gameObject.AddComponent<Shadow>(); outline.effectColor=new Color(0,0,0,.85f); outline.effectDistance=new Vector2(1,-1); }
        cell.visuals=new Graphic[]{cell.shadow,cell.icon,cell.nameLabel,cell.tier,cell.quantity};
        if(owner.source==ListSource.战阵成员){var particle=UIBuildUtils.CreateRect("SpiritCloud",cell.icon.transform);UIBuildUtils.Stretch(particle);cell.cloud=particle.gameObject.AddComponent<UIInkSpiritCloud>();cell.visuals=new Graphic[]{cell.shadow,cell.cloud,cell.nameLabel,cell.tier,cell.quantity};cell.tier.enabled=false;cell.quantity.enabled=false;cell.shadow.enabled=false;}
        cell.ink=rt.gameObject.AddComponent<UIInkFluid>(); cell.ink.使用密度模拟=false; cell.ink.基础浓度=2.2f;
        cell.ink.初始化(cell.shadow,null,null,null,parent.childCount);
        cell.ink.选中(false);
        button.onClick.AddListener(()=> { cell.owner.Select(cell.Entry); UIInkPulse.Emit(rt,Vector2.zero,.25f); });
        return cell;
    }
    public void Bind(IPanelEntry entry)
    {
        Entry=entry; nameLabel.text=entry.DisplayName;
        uint hash=2166136261; foreach(char letter in entry.DisplayName) hash=(hash^letter)*16777619;
        shadow.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-"+(hash%8+1));
        float a=(hash%1000)/999f,b=((hash/1000)%1000)/999f;
        Scatter=new Vector2((a-.5f)*28,(b-.5f)*36);
        displayScale=-1;
        icon.rectTransform.localRotation=Quaternion.Euler(0,0,(b-.5f)*8);
        shadow.rectTransform.localRotation=Quaternion.Euler(0,0,(a-.5f)*26);
        tier.text=entry.DisplayTier+" · "+(entry is ItemDefinition item ? (item.可使用 ? "可使用" : "不可使用") : UIEntryRow.TagOf(entry));
        icon.sprite=owner.source==ListSource.神通 ? UIInkAbilityArt.Icon(entry) : entry.DisplayIcon; icon.color=Color.white;
        if(icon.sprite==null) { icon.enabled=false; }
        else icon.enabled=true;
        if(cloud!=null){icon.enabled=false;cloud.Initialize(entry);nameLabel.enabled=false;}
        shift=velocity=Vector2.zero; hovered=false; scale=1; lastQuantity=-1; lastSelected=Entry!=owner.Selected; UpdateQuantity();
    }
    public void UpdateQuantity()
    {
        int count=Entry is ItemDefinition ? owner.inkWaterfall.Quantity(Entry) : 0;
        if(lastQuantity!=count) { quantity.text=count>0 ? "×"+count : ""; lastQuantity=count; }
        bool selected=Entry==owner.Selected;
        if(lastSelected!=selected) { ink.选中(selected); lastSelected=selected; }
    }
    public void SetArrival(float progress) {
        group.alpha=progress>0 ? 1 : 0; group.interactable=progress>.9f; group.blocksRaycasts=progress>.9f;
        bool arrived=progress>=1;
        ink.渐隐启用=arrived;
        if(landed!=arrived || arrived && visuals[1].material!=FadeMaterial) {
            landed=arrived;
            foreach(var graphic in visuals) if(graphic!=shadow) graphic.material=arrived ? FadeMaterial : null;
        }
    }
    public void Layout(float value,float density)
    {
        if(Mathf.Approximately(displayScale,value) && Mathf.Approximately(backdropScale,density)) return; displayScale=value; backdropScale=density;
        uint hash=2166136261; foreach(char letter in Entry.DisplayName) hash=(hash^letter)*16777619;
        float variation=.88f+(hash%1000)/999f*.24f;
        icon.rectTransform.sizeDelta=Vector2.one*72*value*variation;
        shadow.rectTransform.sizeDelta=new Vector2(260,400)*value*variation*density;
        icon.rectTransform.anchoredPosition=new Vector2(0,18*value);
        nameLabel.rectTransform.sizeDelta=new Vector2(Mathf.Min(Rect.rect.width-8,160*value),48*value);
        nameLabel.rectTransform.anchoredPosition=new Vector2(0,-60*value);
        tier.rectTransform.anchoredPosition=new Vector2(0,-94*value);
        tier.rectTransform.sizeDelta=new Vector2(180*value,20*value);
        quantity.rectTransform.anchoredPosition=new Vector2(48*value,58*value);
        nameLabel.fontSize=Mathf.RoundToInt(Mathf.Clamp(18*value,18,24));
        tier.fontSize=Mathf.RoundToInt(Mathf.Clamp(14*value,14,18));
        quantity.fontSize=Mathf.RoundToInt(Mathf.Clamp(17*value,17,22));
    }
    public void OnPointerEnter(PointerEventData e) { hovered=true; }
    public void OnPointerExit(PointerEventData e) { hovered=false; }
    public void OnBeginDrag(PointerEventData e) {
        if(Entry is ActiveDivineAbility || owner.source==ListSource.战阵成员 && Entry is NpcDefinition) {
            var canvas=GetComponentInParent<Canvas>();
            UIDragContext.Begin(Entry,canvas.rootCanvas.transform,owner.font);UIDragContext.Move(e.position);
            if(Entry is NpcDefinition)UIDragContext.ApplySpiritGhost();else UIDragContext.ApplyInkGhost();
        } else owner.inkWaterfall.OnBeginDrag(e);
    }
    public void OnDrag(PointerEventData e) {if(UIDragContext.Dragging)UIDragContext.Move(e.position);else owner.inkWaterfall.OnDrag(e);}
    public void OnEndDrag(PointerEventData e) {if(UIDragContext.Dragging)UIDragContext.End();else owner.inkWaterfall.OnEndDrag(e);}
    void OnDisable(){hovered=false;if(UIDragContext.Entry==Entry)UIDragContext.End();}
    void LateUpdate()
    {
        var canvas=GetComponentInParent<Canvas>();
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect,Input.mousePosition,camera,out var local);
        float distance=(local-new Vector2(0,18)).magnitude;
        var goal=UIInkMotion.减少动效 ? Vector2.zero : Vector2.ClampMagnitude(local-new Vector2(0,18),6)*Mathf.Clamp01(1-distance/110);
        shift=Vector2.SmoothDamp(shift,goal,ref velocity,.1f,100,Time.unscaledDeltaTime);
        icon.rectTransform.anchoredPosition=new Vector2(0,18*Mathf.Max(1,displayScale))+shift;
        float hoverScale=owner.source==ListSource.神通 ? 1.1f : 1.15f;
        scale=Mathf.MoveTowards(scale,!UIInkMotion.减少动效 && hovered ? hoverScale : 1,Time.unscaledDeltaTime*(.15f/.14f));
        icon.rectTransform.localScale=Vector3.one*scale;
        if(owner.source==ListSource.神通) tier.color=new Color(.82f,.86f,.79f,hovered ? 1 : .55f);
        if(cloud!=null)nameLabel.enabled=hovered || Entry==owner.Selected;
    }
}
