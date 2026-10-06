using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>法宝、坐骑共用原型布局：可拖动的真 3D 展示、简介、持有图标和装备操作。</summary>
public class UIEquipmentShowcasePage : MonoBehaviour
{
    public IPanelEntry Selected { get; private set; }
    UIPanelData data;
    UIMountPage preview;
    Font font;
    Text title, description, status;
    Button equip, previous, next;
    readonly List<Button> icons=new List<Button>();
    readonly List<IPanelEntry> owned=new List<IPanelEntry>();
    bool mounts, built, subscribed;
    int first;
    public static void 应用(Transform root) {
        var panel=root.GetComponent<CharacterPanelUI>();
        if(panel == null || panel.tabs.Count<7 || !UIInkNavigation.启用) return;
        foreach(int index in new[]{3,6}) {
            var page=panel.tabs[index].page;
            var view=page.GetComponent<UIEquipmentShowcasePage>();
            if(view == null) view=page.AddComponent<UIEquipmentShowcasePage>();
            view.Build(index==6,root.GetComponent<UIPanelData>());
        }
    }
    void Build(bool isMount,UIPanelData source) {
        if(built) return;
        mounts=isMount; data=source;
        if(data == null) data=GetComponentInParent<UIPanelData>();
        font=Resources.Load<Font>("Fonts/SimHei") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var oldPreview=GetComponent<UIMountPage>(); if(oldPreview != null) oldPreview.enabled=false;
        foreach(var old in GetComponentsInChildren<UIEntryList>(true)) old.enabled=false;
        foreach(Transform child in transform) child.gameObject.SetActive(false);
        var background=GetComponent<Image>(); if(background != null) background.enabled=false;
        var body=UIBuildUtils.CreateRect("EquipmentShowcase",transform); Place(body,.025f,.025f,.985f,.975f);
        title=Label("EquipmentName",body,"",30); Place(title.rectTransform,.20f,.90f,.80f,.98f);
        var imageGo=new GameObject("Equipment3D",typeof(RectTransform),typeof(RawImage));
        imageGo.transform.SetParent(body,false); var image=imageGo.GetComponent<RawImage>(); Place(image.rectTransform,.15f,.34f,.85f,.89f);
        preview=body.gameObject.AddComponent<UIMountPage>(); preview.预览图=image; preview.data=data;
        preview.正交展示=true; preview.背景色=Color.clear; preview.取景留白=1.18f; preview.贴图边长=1024;
        description=Label("EquipmentIntroduction",body,"",22); Place(description.rectTransform,.13f,.265f,.87f,.335f);
        description.resizeTextForBestFit=true; description.resizeTextMinSize=16; description.resizeTextMaxSize=22;
        for(int i=0;i<5;i++) {
            int local=i;
            var button=UIBuildUtils.CreateButton("OwnedEquipment_"+i,body,font,"",20);
            Place(button.GetComponent<RectTransform>(),.23f+i*.11f,.13f,.32f+i*.11f,.24f);
            button.image.color=Color.clear;
            button.transition=Selectable.Transition.None;
            var icon=UIBuildUtils.CreateImage("EquipmentIcon",button.transform,Color.white); icon.preserveAspect=true;
            Place(icon.rectTransform,.10f,.12f,.90f,.90f);
            button.onClick.AddListener(()=> { int n=first+local; if(n<owned.Count) Select(owned[n]); }); icons.Add(button);
        }
        previous=Action("PreviousEquipment",body,"‹",.14f,.15f,.21f,.22f);
        next=Action("NextEquipment",body,"›",.79f,.15f,.86f,.22f);
        previous.onClick.AddListener(()=> { first=Mathf.Max(0,first-5); Refresh(); });
        next.onClick.AddListener(()=> { first=Mathf.Min(Mathf.Max(0,owned.Count-1),first+5); Refresh(); });
        equip=Action("EquipSelectedEquipment",body,mounts?"装备选中坐骑":"装备选中法宝",.34f,.015f,.66f,.09f);
        equip.onClick.AddListener(()=> {
            if(data == null) return;
            if(Selected is MountDefinition m) { if(data.当前坐骑 == m) data.取消装备坐骑(); else data.设置当前坐骑(m); }
            else if(Selected is TreasureDefinition t) data.设置当前法宝(data.当前法宝 == t ? null : t);
        });
        status=Label("EquipmentStatus",body,"",16); Place(status.rectTransform,.18f,.095f,.82f,.13f);
        built=true; Subscribe(); Refresh();
    }
    Text Label(string name,Transform parent,string value,int size) {
        var t=UIBuildUtils.CreateText(name,parent,font,value,size,TextAnchor.MiddleCenter,new Color(.96f,.94f,.84f));
        t.verticalOverflow=VerticalWrapMode.Truncate; return t;
    }
    Button Action(string name,Transform parent,string value,float x0,float y0,float x1,float y1) {
        var b=UIBuildUtils.CreateButton(name,parent,font,value,22); Place(b.GetComponent<RectTransform>(),x0,y0,x1,y1);
        InkUITheme.Button(b); return b;
    }
    static void Place(RectTransform r,float x0,float y0,float x1,float y1) { r.anchorMin=new Vector2(x0,y0); r.anchorMax=new Vector2(x1,y1); r.offsetMin=r.offsetMax=Vector2.zero; }
    void Subscribe() { if(!subscribed && data != null) { data.Changed+=Refresh; subscribed=true; } }
    void OnEnable() { if(built) { Subscribe(); Refresh(); } }
    void OnDisable() { if(subscribed && data != null) data.Changed-=Refresh; subscribed=false; }
    public void Refresh() {
        if(!built || data == null) return;
        data.EnsureLists(); owned.Clear();
        if(mounts) foreach(var m in data.坐骑) { if(m != null && data.已学坐骑.Contains(m.坐骑id)) owned.Add(m); }
        else foreach(var t in data.法宝) { if(data.已拥有(t)) owned.Add(t); }
        first=Mathf.Clamp(first,0,Mathf.Max(0,owned.Count-1));
        if(Selected == null || !owned.Contains(Selected)) Select(owned.Count>0 ? owned[0] : null);
        for(int i=0;i<icons.Count;i++) {
            var b=icons[i]; int n=first+i; b.gameObject.SetActive(n<owned.Count);
            if(n>=owned.Count) continue;
            int shown=Mathf.Min(5,owned.Count-first); float left=.5f-shown*.055f;
            Place(b.GetComponent<RectTransform>(),left+i*.11f,.13f,left+.09f+i*.11f,.24f);
            b.transform.Find("EquipmentIcon").GetComponent<Image>().sprite=owned[n].DisplayIcon;
            b.image.color=Color.clear;
            b.transform.Find("EquipmentIcon").GetComponent<Image>().color=owned[n]==Selected ? Color.white : new Color(1,1,1,.65f);
        }
        previous.interactable=first>0; next.interactable=first+5<owned.Count;
        bool equipped=Selected is MountDefinition m2 ? data.当前坐骑==m2 : Selected is TreasureDefinition t2 && data.当前法宝==t2;
        equip.interactable=Selected != null;
        equip.GetComponentInChildren<Text>().text=equipped ? "取消装备" : mounts ? "装备选中坐骑" : "装备选中法宝";
        status.text=Selected == null ? mounts ? "尚未学会驾驭坐骑" : "尚未拥有法宝" : equipped ? mounts?"已装备 · Shift 乘骑":"已装备 · E 施放" : "左右拖动查看模型";
    }
    void Select(IPanelEntry entry) {
        bool changed=Selected!=entry; Selected=entry;
        title.text=entry != null ? entry.DisplayName : mounts?"坐骑":"法宝";
        description.text=entry != null ? entry.DisplayDescription : "";
        if(changed || entry==null) preview.Refresh(entry);
        if(built && changed) Refresh();
    }
}
