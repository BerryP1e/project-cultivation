using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>手绘纵向星图与神通瀑布流。复用原六槽、列表、被动和详情。</summary>
public class UIInkSkillsPage : MonoBehaviour
{
    public UIActiveSkillBar 星图 {get;private set;}
    public UIEntryList 已悟神通 {get;private set;}
    public UIEntryList 生效被动 {get;private set;}
    public UIInkWaterfall 瀑布流 {get;private set;}
    public UIEntryInfo 详情 {get;private set;}
    public int 被动星数 {get;private set;}
    public Material 星点描边材质=>mapMaterial;
    public Vector2 星位锚点(int index)=>positions[index];
    readonly Dictionary<IPanelEntry,UIInkStarParticle> passiveStars=new Dictionary<IPanelEntry,UIInkStarParticle>();
    readonly Vector2[] positions={new Vector2(.30f,.90f),new Vector2(.24f,.73f),new Vector2(.64f,.57f),new Vector2(.52f,.40f),new Vector2(.79f,.25f),new Vector2(.54f,.08f)};
    UIInkSkillStar[] slots;
    Material mapMaterial;
    UIInkConstellation constellation;
    float age,detailAge;
    IPanelEntry selected;
    bool ready;
    static readonly Color Light=new Color(.96f,.95f,.88f);
    public static void 应用(Transform root) {
        if(!UIInkNavigation.启用)return;
        var panel=root.GetComponent<CharacterPanelUI>();if(panel==null || panel.tabs.Count<3)return;
        var page=panel.tabs[2].page;var view=page.GetComponent<UIInkSkillsPage>();if(view==null)view=page.AddComponent<UIInkSkillsPage>();view.Build();
    }
    void Build() {
        if(ready)return;
        if(InkUITheme.Load("SkillsDynamic/fx-star-particle")==null)return;
        星图=GetComponentInChildren<UIActiveSkillBar>(true);
        已悟神通=transform.Find("KnownList").GetComponent<UIEntryList>();生效被动=transform.Find("PassiveList").GetComponent<UIEntryList>();详情=已悟神通.infoTarget;
        ready=true;
        foreach(var motion in GetComponentsInChildren<UIInkMotion>(true))motion.enabled=false;
        foreach(var panel in new Transform[]{星图.transform,已悟神通.transform,生效被动.transform,详情.transform}) {
            var image=panel.GetComponent<Image>();if(image!=null)image.enabled=false;
            var title=panel.Find("Title");if(title!=null)title.gameObject.SetActive(false);
        }
        UIBuildUtils.Place(星图.transform as RectTransform,new Vector2(.005f,.04f),new Vector2(.455f,.97f),Vector2.zero,Vector2.zero);
        foreach(var name in new[]{"InkRing","Hint","InkNumbers"}) {var old=星图.transform.Find(name);if(old!=null)old.gameObject.SetActive(false);}
        foreach(var oldImage in 星图.GetComponentsInChildren<Image>(true)) if(oldImage.name.StartsWith("InkNumber"))oldImage.gameObject.SetActive(false);
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
        UIBuildUtils.Place(已悟神通.transform as RectTransform,new Vector2(.47f,.08f),new Vector2(.73f,.96f),Vector2.zero,Vector2.zero);
        已悟神通.inkCards=false;
        瀑布流=已悟神通.gameObject.AddComponent<UIInkWaterfall>();瀑布流.Initialize(已悟神通);瀑布流.Skip();
        // 保留原始数据组件，但启用状态改由星图卫星表示，不再显示独立列表。
        生效被动.gameObject.SetActive(false);
        UIBuildUtils.Place(详情.transform as RectTransform,new Vector2(.755f,.04f),new Vector2(.995f,.96f),Vector2.zero,Vector2.zero);
        var ink=UIBuildUtils.CreateImage("SkillInfoInk",详情.transform,Color.white);ink.sprite=InkUITheme.Load("Bag/bag-info-ink");ink.raycastTarget=false;ink.transform.SetAsFirstSibling();UIBuildUtils.Stretch(ink.rectTransform);
        LayoutDetail();
        已悟神通.RebuildFromSource();生效被动.RebuildFromSource();
        if(星图.data!=null)星图.data.Changed+=RefreshPassiveStars;
        if(gameObject.activeInHierarchy)Restart();
    }
    static void Shadow(Text t){var s=t.GetComponent<Shadow>();if(s==null)s=t.gameObject.AddComponent<Shadow>();s.effectColor=new Color(0,0,0,.9f);s.effectDistance=new Vector2(1,-1);}
    void LayoutDetail() {
        Place(详情.nameText,.05f,.86f,.95f,.96f,28,TextAnchor.MiddleCenter);
        Place(详情.tierText,.05f,.81f,.50f,.86f,17,TextAnchor.MiddleLeft);
        Place(详情.kindText,.55f,.81f,.95f,.86f,17,TextAnchor.MiddleRight);
        Place(详情.descriptionText,.09f,.16f,.91f,.48f,18,TextAnchor.UpperLeft);
        if(详情.actionButton!=null)UIBuildUtils.Place(详情.actionButton.transform as RectTransform,new Vector2(.14f,.055f),new Vector2(.86f,.12f),Vector2.zero,Vector2.zero);
        if(详情.iconImage!=null) {UIBuildUtils.Place(详情.iconImage.rectTransform,new Vector2(.22f,.51f),new Vector2(.78f,.78f),Vector2.zero,Vector2.zero);详情.iconImage.preserveAspect=true;}
        var art=详情.transform.Find("InkAbilityArtwork") as RectTransform;if(art!=null)UIBuildUtils.Place(art,new Vector2(.09f,.51f),new Vector2(.91f,.78f),Vector2.zero,Vector2.zero);
    }
    static void Place(Text t,float x,float y,float xx,float yy,int size,TextAnchor alignment){if(t==null)return;UIBuildUtils.Place(t.rectTransform,new Vector2(x,y),new Vector2(xx,yy),Vector2.zero,Vector2.zero);t.fontSize=size;t.alignment=alignment;t.color=Light;Shadow(t);}
    void OnEnable(){if(ready)Restart();}
    void Restart(){age=0;detailAge=0;RefreshPassiveStars();}
    void LateUpdate() {
        if(!ready)return;
        age+=Time.unscaledDeltaTime;float t=UIInkMotion.减少动效?10:age;
        constellation.Progress=Mathf.Clamp01(t/.55f);
        for(int i=0;i<slots.Length;i++)slots[i].Refresh(Mathf.Clamp01((t-.1f-i*.06f)/.18f));
        LayoutDetail();
        if(详情.Current!=selected){selected=详情.Current;detailAge=0;}detailAge+=Time.unscaledDeltaTime;
        foreach(var text in 详情.GetComponentsInChildren<Text>(true)){text.color=Light;Shadow(text);var r=text.GetComponent<UIInkReveal>();if(r==null)r=text.gameObject.AddComponent<UIInkReveal>();r.进度=UIInkMotion.减少动效?1:Mathf.Clamp01(detailAge/.18f);}
        if(详情.iconImage!=null)详情.iconImage.enabled=详情.iconImage.sprite!=null;
    }
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
            var starRect=particle.transform as RectTransform;starRect.anchorMin=starRect.anchorMax=positions[被动星数%6];
            float angle=(135+被动星数/6*137.5f)*Mathf.Deg2Rad;starRect.anchoredPosition=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(78+被动星数/6*18);
            particle.gameObject.SetActive(true);被动星数++;
        }
    }
    void OnDestroy(){if(星图!=null && 星图.data!=null)星图.data.Changed-=RefreshPassiveStars;if(mapMaterial!=null)Destroy(mapMaterial);}
}
