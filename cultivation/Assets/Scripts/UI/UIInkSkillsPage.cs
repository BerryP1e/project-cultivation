using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>神通四区：掌握列表、六槽装备、独立3D表现、属性与介绍。</summary>
public class UIInkSkillsPage : MonoBehaviour
{
    public UIActiveSkillBar 星图 {get;private set;}
    public UIEntryList 已悟神通 {get;private set;}
    public UIEntryList 生效被动 {get;private set;}
    public UIInkWaterfall 瀑布流 {get;private set;}
    public UIEntryInfo 详情 {get;private set;}
    public UIAbilityPreview 神通预览 {get;private set;}
    public int 被动星数 {get;private set;}
    public Material 星点描边材质=>mapMaterial;
    public Vector2 星位锚点(int index)=>positions[index];
    readonly Dictionary<IPanelEntry,UIInkStarParticle> passiveStars=new Dictionary<IPanelEntry,UIInkStarParticle>();
    readonly Dictionary<IPanelEntry,Vector2> passivePositions=new Dictionary<IPanelEntry,Vector2>();
    readonly System.Random starRandom=new System.Random();
    public bool 星位空白有效(Vector2 normalized,IPanelEntry ignored=null){
        var bounds=((RectTransform)星图.transform).rect;var point=Vector2.Scale(normalized,bounds.size);
        foreach(var slot in positions){var delta=point-Vector2.Scale(slot,bounds.size);if(Mathf.Abs(delta.x)<83 && delta.y>-78 && delta.y<65)return false;}
        foreach(var pair in passivePositions)if(pair.Key!=ignored && 星图.data.IsPassiveEnabled(pair.Key as PassiveDivineAbility) && Vector2.Distance(point,Vector2.Scale(pair.Value,bounds.size))<65)return false;
        return normalized.x>=.06f && normalized.x<=.92f && normalized.y>=.09f && normalized.y<=.91f;
    }
    Vector2 AllocateStar(IPanelEntry entry){
        if(passivePositions.TryGetValue(entry,out var old) && 星位空白有效(old,entry))return old;
        for(int i=0;i<600;i++){var p=new Vector2(.06f+(float)starRandom.NextDouble()*.86f,.09f+(float)starRandom.NextDouble()*.82f);if(星位空白有效(p,entry)){passivePositions[entry]=p;return p;}}
        // 极密星图扩大画布时才需要更多空间；优先网格寻找仍未占据的空白。
        for(float y=.09f;y<=.91f;y+=.045f)for(float x=.06f;x<=.92f;x+=.045f){var p=new Vector2(x,y);if(星位空白有效(p,entry)){passivePositions[entry]=p;return p;}}
        return old;
    }
    readonly Vector2[] positions={new Vector2(.30f,.90f),new Vector2(.24f,.73f),new Vector2(.64f,.57f),new Vector2(.52f,.40f),new Vector2(.79f,.25f),new Vector2(.54f,.08f)};
    UIInkSkillStar[] slots;
    Material mapMaterial;
    UIInkConstellation constellation;
    float age;
    IPanelEntry selected;RectTransform propertyRows;bool ready;
    public static void 应用(Transform root)
    {
        if(!UIInkNavigation.启用)return;
        var panel=root.GetComponent<CharacterPanelUI>();if(!panel||panel.tabs.Count<3)return;
        var page=panel.tabs[2].page;(page.GetComponent<UIInkSkillsPage>()??page.AddComponent<UIInkSkillsPage>()).Build();
    }
    void Build()
    {
        if(ready)return;
        星图=GetComponentInChildren<UIActiveSkillBar>(true);已悟神通=transform.Find("KnownList")?.GetComponent<UIEntryList>();生效被动=transform.Find("PassiveList")?.GetComponent<UIEntryList>();
        if(!星图||!已悟神通)return;详情=已悟神通.infoTarget;if(!详情)return;ready=true;
        foreach(var motion in GetComponentsInChildren<UIInkMotion>(true))motion.enabled=false;
        foreach(var area in new[]{星图.transform,已悟神通.transform,详情.transform}){var image=area.GetComponent<Image>();if(image)image.enabled=false;var title=area.Find("Title");if(title)title.gameObject.SetActive(false);}
        Place((RectTransform)已悟神通.transform,.005f,.09f,.24f,.96f);已悟神通.inkCards=false;
        瀑布流=已悟神通.gameObject.AddComponent<UIInkWaterfall>();瀑布流.OrderedCards=true;瀑布流.CardColumns=3;瀑布流.AllowLoop=false;瀑布流.Initialize(已悟神通);瀑布流.Skip();
        if(生效被动)生效被动.gameObject.SetActive(false);
        Place((RectTransform)星图.transform,.245f,.055f,.48f,.97f);
        foreach(var name in new[]{"InkRing","Hint","InkNumbers"}){var old=星图.transform.Find(name);if(old)old.gameObject.SetActive(false);}
        mapMaterial=new Material(Shader.Find("UI/InkCircle"));
        var graph=UIBuildUtils.CreateRect("LiveConstellation",星图.transform);graph.SetAsFirstSibling();UIBuildUtils.Stretch(graph);constellation=graph.gameObject.AddComponent<UIInkConstellation>();constellation.raycastTarget=false;constellation.material=mapMaterial;
        for(int i=0;i<16;i++){
            var rt=UIBuildUtils.CreateRect("AmbientStar",星图.transform);uint seed=(uint)(i*7919+313);
            rt.anchorMin=rt.anchorMax=new Vector2(.06f+(seed%101)/115f,.05f+(seed/101%97)/110f);rt.sizeDelta=Vector2.one*(12+i%3*3);
            rt.gameObject.AddComponent<UIInkStarParticle>().Initialize(i,new Color(.74f,.79f,.7f));
        }
        slots=new UIInkSkillStar[星图.slots.Count];
        for(int i=0;i<星图.slots.Count;i++) {
            var slot=星图.slots[i];var rt=slot.transform as RectTransform;
            rt.anchorMin=rt.anchorMax=positions[i];rt.sizeDelta=new Vector2(104,104);rt.anchoredPosition=Vector2.zero;
            var oldShape=slot.GetComponent<InkUIHitShape>();if(oldShape!=null)oldShape.enabled=false;
            slot.button.transition=Selectable.Transition.None;
            if(slot.icon!=null){UIBuildUtils.Stretch(slot.icon.rectTransform,14);slot.icon.preserveAspect=true;}
            if(slot.label!=null){slot.label.transform.SetParent(rt,false);UIBuildUtils.Place(slot.label.rectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-100,-32),new Vector2(100,-8));slot.label.fontSize=18;}
            if(slot.clearButton!=null){slot.clearButton.transform.SetParent(rt,false);var clear=slot.clearButton.transform as RectTransform;clear.anchorMin=clear.anchorMax=Vector2.one;clear.sizeDelta=new Vector2(24,24);clear.anchoredPosition=new Vector2(0,0);InkUITheme.Button(slot.clearButton);}
            var number=UIBuildUtils.CreateText("StarNumber",rt,已悟神通.font,(i+1).ToString(),22,TextAnchor.MiddleCenter,Light);number.raycastTarget=false;number.rectTransform.sizeDelta=new Vector2(28,28);number.rectTransform.anchoredPosition=new Vector2(-62,0);Shadow(number);
            slots[i]=rt.gameObject.AddComponent<UIInkSkillStar>();slots[i].Initialize(slot,i,mapMaterial);
        }
        constellation.Nodes=slots;
        星图.gameObject.AddComponent<UIInkSkillVolume>().Initialize(星图,slots);
        var previewRT=UIBuildUtils.CreateRect("Ability3D",transform);Place(previewRT,.485f,.07f,.725f,.96f);var image3D=previewRT.gameObject.AddComponent<RawImage>();神通预览=previewRT.gameObject.AddComponent<UIAbilityPreview>();神通预览.初始化(image3D);
        Place((RectTransform)详情.transform,.745f,.02f,.998f,.98f);UICharacterBackdrop.Panel(详情.transform);
        详情.独立属性展示=true;
        TextPlace(详情.nameText,.07f,.92f,.94f,.995f,28,TextAnchor.MiddleLeft);
        TextPlace(详情.tierText,.07f,.85f,.48f,.91f,19,TextAnchor.MiddleLeft);TextPlace(详情.kindText,.55f,.85f,.94f,.91f,18,TextAnchor.MiddleRight);
        if(!详情.iconImage)详情.iconImage=UIBuildUtils.CreateImage("SkillIcon",详情.transform,Color.white);
        Place(详情.iconImage.rectTransform,.18f,.62f,.82f,.83f);详情.iconImage.preserveAspect=true;详情.iconImage.gameObject.SetActive(true);
        var art=详情.transform.Find("InkAbilityArtwork");if(art)art.gameObject.SetActive(false);
        UICharacterBackdrop.Divider(详情.transform,"SkillHeaderDivider",.605f);
        propertyRows=UIBuildUtils.CreateRect("SkillProperties",详情.transform);Place(propertyRows,.065f,.445f,.94f,.59f);
        var host=UIBuildUtils.CreateRect("SkillDescriptionScroll",详情.transform);Place(host,.075f,.145f,.94f,.425f);
        var scroll=host.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=30;
        var viewport=UIBuildUtils.CreateRect("Viewport",host);UIBuildUtils.Stretch(viewport);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        if(详情.descriptionText){var desc=详情.descriptionText;desc.transform.SetParent(viewport,false);desc.rectTransform.anchorMin=new Vector2(0,1);desc.rectTransform.anchorMax=Vector2.one;desc.rectTransform.pivot=new Vector2(.5f,1);desc.rectTransform.sizeDelta=Vector2.zero;desc.rectTransform.anchoredPosition=Vector2.zero;desc.fontSize=20;desc.alignment=TextAnchor.UpperLeft;desc.color=UICharacterBackdrop.TextColor;desc.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=desc.rectTransform;scroll.viewport=viewport;}
        if(详情.actionButton){Place((RectTransform)详情.actionButton.transform,.14f,.025f,.94f,.105f);UICharacterBackdrop.StyleButton(详情.actionButton);}
        foreach(var text in 详情.GetComponentsInChildren<Text>(true))if(text!=详情.actionLabel)text.color=UICharacterBackdrop.TextColor;
        星图.Refresh();已悟神通.RebuildFromSource();详情.Show(详情.Current);已悟神通.SelectionChanged+=Select;
        if(星图.data)星图.data.Changed+=RefreshPassiveStars;RefreshPassiveStars();
        Select(详情.Current);
    }
    static void Place(RectTransform r,float x,float y,float xx,float yy)=>UIBuildUtils.Place(r,new Vector2(x,y),new Vector2(xx,yy),Vector2.zero,Vector2.zero);
    static void TextPlace(Text t,float x,float y,float xx,float yy,int size,TextAnchor align){if(!t)return;Place(t.rectTransform,x,y,xx,yy);t.fontSize=size;t.alignment=align;}
    void Select(IPanelEntry entry)
    {
        selected=entry;神通预览.显示(entry);
        if(详情.iconImage){详情.iconImage.gameObject.SetActive(true);详情.iconImage.enabled=详情.iconImage.sprite!=null;详情.iconImage.color=Color.white;}
        foreach(Transform child in propertyRows){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        if(entry is ActiveDivineAbility active){Property("灵力消耗",active.消耗灵力.ToString("0.#"),.67f);Property("冷却",active.冷却时间.ToString("0.#")+" 秒",.34f);Property("攻击",active.伤害属性.ToString(),.01f);}
        else if(entry is PassiveDivineAbility passive){Property("类型","被动神通",.67f);Property("维持消耗",passive.维持消耗灵力.ToString("0.#"),.34f);}
        var scroll=详情.descriptionText?详情.descriptionText.GetComponentInParent<ScrollRect>():null;if(scroll){scroll.StopMovement();scroll.verticalNormalizedPosition=1;}
    }
    void Property(string name,string value,float y)
    {
        var row=UIBuildUtils.CreateRect("PropertyRow",propertyRows);Place(row,0,y,1,y+.29f);row.gameObject.AddComponent<Image>().color=new Color(.49f,.60f,.61f,.14f);
        var a=UIBuildUtils.CreateText("Name",row,已悟神通.font,name,18,TextAnchor.MiddleLeft,UICharacterBackdrop.TextColor);Place(a.rectTransform,.03f,0,.60f,1);
        var b=UIBuildUtils.CreateText("Value",row,已悟神通.font,value,18,TextAnchor.MiddleRight,UICharacterBackdrop.TextColor);Place(b.rectTransform,.60f,0,.97f,1);
    }
    void OnEnable(){if(ready){age=0;RefreshPassiveStars();}}
    void LateUpdate(){if(!ready)return;age+=Time.unscaledDeltaTime;constellation.Progress=UIInkMotion.减少动效?1:Mathf.Clamp01(age/.55f);for(int i=0;i<slots.Length;i++)slots[i].Refresh(UIInkMotion.减少动效?1:Mathf.Clamp01((age-.1f-i*.06f)/.18f));if(详情.Current!=selected)Select(详情.Current);}
    void RefreshPassiveStars(){
        if(星图.data==null)return;
        foreach(var pair in passiveStars)pair.Value.gameObject.SetActive(false);被动星数=0;
        foreach(var entry in 星图.data.GetPassiveAbilities()) {
            if(!(entry is PassiveDivineAbility))continue;
            if(!passiveStars.TryGetValue(entry,out var particle)){
                var rt=UIBuildUtils.CreateRect("PassiveStar",星图.transform);rt.sizeDelta=new Vector2(42,42);
                var hit=rt.gameObject.AddComponent<Image>();hit.color=Color.clear;var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;
                var keep=entry;button.onClick.AddListener(()=>已悟神通.Select(keep));particle=rt.gameObject.AddComponent<UIInkStarParticle>();particle.Initialize(被动星数,new Color(.58f,1f,1f));
                rt.gameObject.AddComponent<UIInkPassiveStar>().Initialize(this,keep);
                passiveStars.Add(entry,particle);
            }
            var starRect=particle.transform as RectTransform;starRect.anchorMin=starRect.anchorMax=AllocateStar(entry);starRect.anchoredPosition=Vector2.zero;
            particle.gameObject.SetActive(true);被动星数++;
        }
    }

    void OnDestroy(){if(已悟神通)已悟神通.SelectionChanged-=Select;if(星图&&星图.data)星图.data.Changed-=RefreshPassiveStars;if(mapMaterial)Destroy(mapMaterial);}
    static void Shadow(Text t){var shadow=t.GetComponent<Shadow>();if(!shadow)shadow=t.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.8f);shadow.effectDistance=new Vector2(1,-1);}
    static readonly Color Light=new Color(.96f,.95f,.88f);
}
