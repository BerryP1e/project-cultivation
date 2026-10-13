using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>法则背包三列等距卡片，按境界/名称筛选，只绑定视口附近的卡片。</summary>
public sealed class UIInkLawGrid : MonoBehaviour
{
    UIInkLawPage page; Font font; ScrollRect scroll; RectTransform viewport,content;
    InputField search; Button realm; int tier=-1;
    readonly List<LawFragment> source=new List<LawFragment>(),filtered=new List<LawFragment>();
    readonly List<Card> cards=new List<Card>();
    UIInkViewportFade fade;
    sealed class Card {public Button button;public Image icon,edge;public Text name,state;public LawFragment law;public UIInkLawItemDrag drag;public UICharacterCard surface;}
    public int 筛选数量=>filtered.Count;
    public int 卡片池数=>cards.Count;
    public void 初始化(UIInkLawPage owner,Font textFont)
    {
        page=owner;font=textFont;
        realm=UIBuildUtils.CreateButton("LawRealmFilter",transform,font,"境界 · 全部",18);
        Place((RectTransform)realm.transform,0,.925f,.48f,1);UICharacterBackdrop.StyleButton(realm);
        realm.onClick.AddListener(()=>设置筛选((tier+2)%10-1,search.text));
        var input=UIBuildUtils.CreateImage("LawNameFilter",transform,new Color(.08f,.14f,.16f,.6f));Place(input.rectTransform,.51f,.925f,1,1);
        search=input.gameObject.AddComponent<InputField>();
        var text=UIBuildUtils.CreateText("Text",input.transform,font,"",18,TextAnchor.MiddleLeft,UICharacterBackdrop.TextColor);UIBuildUtils.Stretch(text.rectTransform,10);search.textComponent=text;
        var placeholder=UIBuildUtils.CreateText("Placeholder",input.transform,font,"搜索名称",18,TextAnchor.MiddleLeft,UICharacterBackdrop.Muted);UIBuildUtils.Stretch(placeholder.rectTransform,10);search.placeholder=placeholder;
        search.onValueChanged.AddListener(value=>设置筛选(tier,value));
        var host=UIBuildUtils.CreateRect("LawGridScroll",transform);Place(host,0,0,1,.90f);
        scroll=host.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=30;scroll.movementType=ScrollRect.MovementType.Clamped;
        viewport=UIBuildUtils.CreateRect("Viewport",host);UIBuildUtils.Stretch(viewport);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        fade=viewport.gameObject.AddComponent<UIInkViewportFade>();fade.FadeWidth=36;
        content=UIBuildUtils.CreateRect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;content.anchoredPosition=Vector2.zero;
        scroll.viewport=viewport;scroll.content=content;UIBuildUtils.AddVerticalScrollbar(scroll);
        viewport.gameObject.AddComponent<UIInkLawInventoryDrop>().初始化(page);
    }
    static void Place(RectTransform r,float x,float y,float xx,float yy)=>UIBuildUtils.Place(r,new Vector2(x,y),new Vector2(xx,yy),Vector2.zero,Vector2.zero);
    public void 设置(IEnumerable<LawFragment> laws){source.Clear();source.AddRange(laws);Filter(false);}
    public void 设置筛选(int realmIndex,string name){tier=Mathf.Clamp(realmIndex,-1,8);search.SetTextWithoutNotify(name??"");Filter(true);}
    void Filter(bool reset)
    {
        filtered.Clear();filtered.AddRange(source.Where(x=>(tier<0||x.境界==tier)&&(string.IsNullOrEmpty(search.text)||x.名称.IndexOf(search.text,System.StringComparison.OrdinalIgnoreCase)>=0)));
        realm.GetComponentInChildren<Text>().text="境界 · "+(tier<0?"全部":LawFragmentCatalog.取().境界[tier].境界名);
        if(reset){scroll.StopMovement();content.anchoredPosition=Vector2.zero;}
        Layout();
    }
    Card Create()
    {
        var c=new Card();c.button=UIBuildUtils.CreateButton("LawGridCard",content,font,"",15);c.button.transition=Selectable.Transition.None;c.button.image.sprite=null;c.button.image.color=new Color(.08f,.14f,.16f,.58f);
        c.name=c.button.GetComponentInChildren<Text>();c.name.color=UICharacterBackdrop.TextColor;c.name.fontSize=15;Place(c.name.rectTransform,.04f,.035f,.96f,.24f);
        c.icon=UIBuildUtils.CreateImage("Icon",c.button.transform,Color.white);c.icon.preserveAspect=true;Place(c.icon.rectTransform,.10f,.22f,.90f,.95f);
        c.state=UIBuildUtils.CreateText("State",c.button.transform,font,"",13,TextAnchor.UpperRight,UICharacterBackdrop.Muted);Place(c.state.rectTransform,.05f,.73f,.94f,.96f);
        c.edge=UIBuildUtils.CreateImage("QualityEdge",c.button.transform,new Color(.8f,.79f,.57f,.6f));Place(c.edge.rectTransform,0,.01f,1,.035f);
        foreach(var g in c.button.GetComponentsInChildren<Graphic>())if(g!=c.button.image){g.raycastTarget=false;g.material=fade.Material;g.gameObject.AddComponent<UIInkFadeCoordinates>().Viewport=viewport;}
        c.surface=c.button.gameObject.AddComponent<UICharacterCard>();c.surface.初始化(c.button.image);c.surface.渐隐(viewport);c.edge.gameObject.SetActive(false);
        c.drag=c.button.gameObject.AddComponent<UIInkLawItemDrag>();c.button.onClick.AddListener(()=>page.选择(c.law));return c;
    }
    void Layout()
    {
        if(!viewport||viewport.rect.width<1)return;
        float width=(viewport.rect.width-24)/3,height=width*1.13f,pitch=height+14;
        content.sizeDelta=new Vector2(0,Mathf.Max(viewport.rect.height,Mathf.CeilToInt(filtered.Count/3f)*pitch+24));
        int first=Mathf.Max(0,Mathf.FloorToInt(content.anchoredPosition.y/pitch)-1)*3;
        int count=Mathf.Min(filtered.Count-first,(Mathf.CeilToInt(viewport.rect.height/pitch)+3)*3);count=Mathf.Max(0,count);
        while(cards.Count<count)cards.Add(Create());
        for(int i=0;i<cards.Count;i++) {
            var c=cards[i];c.button.gameObject.SetActive(i<count);if(i>=count)continue;
            int index=first+i;var law=filtered[index];c.law=law;c.name.text=law.名称;c.icon.sprite=law.图标;c.icon.color=law.颜色;
            c.state.text=page.是否已装备(law)?"已装备":page.是否在炉中(law)?"炉中":"";
            c.surface.绑定(law.颜色,page.当前选择==law);c.surface.渐隐(viewport);
            c.drag.初始化(page,law,scroll);
            var rt=(RectTransform)c.button.transform;rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.sizeDelta=new Vector2(width-8,height);rt.anchoredPosition=new Vector2(8+index%3*width,-12-index/3*pitch);
        }
    }
    public Transform 寻找卡片(LawFragment law)=>cards.FirstOrDefault(c=>c.law==law&&c.button.gameObject.activeInHierarchy)?.button.transform;
    void LateUpdate(){if(page.拖拽法则==null)Layout();}
}
