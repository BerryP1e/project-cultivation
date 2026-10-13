using UnityEngine;
using UnityEngine.UI;

/// <summary>背包卡片与一体化介绍区；继续由原列表和物品使用器管理数据。</summary>
public class UIInkBagPage : MonoBehaviour
{
    public UIInkWaterfall Waterfall {get;private set;}
    public int ParcelFrame => 7;
    public bool AnimationFinished => true;
    UIEntryList list; UIEntryInfo info; Text quantity; bool ready;
    public static void 应用(Transform root)
    {
        if(!UIInkNavigation.启用)return;
        var panel=root.GetComponent<CharacterPanelUI>();if(!panel||panel.tabs.Count==0)return;
        var page=panel.tabs[0].page;
        (page.GetComponent<UIInkBagPage>()??page.AddComponent<UIInkBagPage>()).Build();
    }
    void Build()
    {
        if(ready)return;
        list=GetComponentInChildren<UIEntryList>(true);info=list?list.infoTarget:null;
        if(!list||!info)return;ready=true;
        Place((RectTransform)list.transform,.015f,.105f,.69f,.97f);
        Place((RectTransform)info.transform,.715f,.025f,.995f,.98f);
        foreach(var area in new[]{list.transform,info.transform}) {
            var image=area.GetComponent<Image>();if(image)image.enabled=false;
            var title=area.Find("Title");if(title)title.gameObject.SetActive(false);
            foreach(var motion in area.GetComponents<UIInkMotion>())motion.enabled=false;
        }
        UICharacterBackdrop.Panel(info.transform);
        var old=transform.Find("ItemDesc");if(old)old.gameObject.SetActive(false);
        if(info.descriptionText){info.descriptionText.transform.SetParent(info.transform,false);Place(info.descriptionText.rectTransform,.075f,.17f,.93f,.58f);info.descriptionText.fontSize=21;info.descriptionText.alignment=TextAnchor.UpperLeft;}
        if(info.iconImage){Place(info.iconImage.rectTransform,.38f,.65f,.94f,.87f);info.iconImage.preserveAspect=true;}
        TextPlace(info.nameText,.07f,.89f,.94f,.98f,29,TextAnchor.MiddleLeft);
        TextPlace(info.tierText,.07f,.81f,.45f,.88f,19,TextAnchor.MiddleLeft);
        TextPlace(info.kindText,.07f,.75f,.40f,.81f,18,TextAnchor.MiddleLeft);
        UICharacterBackdrop.Divider(info.transform,"HeaderDivider",.63f);
        quantity=UIBuildUtils.CreateText("BagQuantity",info.transform,list.font,"",20,TextAnchor.MiddleLeft,UICharacterBackdrop.TextColor);
        Place(quantity.rectTransform,.07f,.59f,.94f,.64f);
        if(info.descriptionText){
            var host=UIBuildUtils.CreateRect("BagDescriptionScroll",info.transform);Place(host,.075f,.17f,.93f,.58f);
            var scroll=host.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=30;
            var viewport=UIBuildUtils.CreateRect("Viewport",host);UIBuildUtils.Stretch(viewport);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
            var desc=info.descriptionText;desc.transform.SetParent(viewport,false);desc.rectTransform.anchorMin=new Vector2(0,1);desc.rectTransform.anchorMax=Vector2.one;desc.rectTransform.pivot=new Vector2(.5f,1);desc.rectTransform.sizeDelta=Vector2.zero;desc.rectTransform.anchoredPosition=Vector2.zero;
            desc.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=desc.rectTransform;scroll.viewport=viewport;
            list.SelectionChanged+=entry=>{scroll.StopMovement();scroll.verticalNormalizedPosition=1;};
        }
        if(info.actionButton){Place((RectTransform)info.actionButton.transform,.20f,.025f,.94f,.105f);UICharacterBackdrop.StyleButton(info.actionButton);}
        foreach(var text in info.GetComponentsInChildren<Text>(true))if(text!=info.actionLabel)text.color=UICharacterBackdrop.TextColor;
        Waterfall=list.gameObject.AddComponent<UIInkWaterfall>();Waterfall.OrderedCards=true;Waterfall.CardColumns=6;Waterfall.AllowLoop=false;Waterfall.Initialize(list);Waterfall.Skip();
        var hint=UIBuildUtils.CreateText("BagFooter",transform,list.font,"背 包",20,TextAnchor.MiddleLeft,UICharacterBackdrop.Muted);Place(hint.rectTransform,.025f,.025f,.25f,.08f);
        list.RebuildFromSource();
    }
    static void Place(RectTransform r,float x,float y,float xx,float yy)=>UIBuildUtils.Place(r,new Vector2(x,y),new Vector2(xx,yy),Vector2.zero,Vector2.zero);
    static void TextPlace(Text t,float x,float y,float xx,float yy,int size,TextAnchor align){if(!t)return;Place(t.rectTransform,x,y,xx,yy);t.fontSize=size;t.alignment=align;}
    public void Skip()=>Waterfall?.Skip();
    void Update()
    {
        if(!ready)return;
        quantity.text=info.Current is ItemDefinition item&&list.data?"持有  "+list.data.物品数量(item).ToString("N0"):"";
        if(info.kindText)info.kindText.color=UICharacterBackdrop.Muted;
        if(info.iconImage)info.iconImage.enabled=info.Current?.DisplayIcon!=null;
    }
}
