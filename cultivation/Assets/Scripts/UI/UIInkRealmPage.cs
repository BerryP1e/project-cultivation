using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>境界展示运行时布局；仅表现，不改变修炼、功法或场景。</summary>
public class UIInkRealmPage : MonoBehaviour
{
    public Image 墨圈 { get; private set; }
    public UIRealmBar 境界条 { get; private set; }
    public UIAttributeList 属性 { get; private set; }
    public ScrollRect 属性滚动 { get; private set; }
    readonly List<UIInkReveal> reveals = new List<UIInkReveal>();
    RectTransform gongfa, circle;
    CanvasGroup center;
    UIInkReveal bottomReveal;
    RectTransform bottomInk;
    bool ready;
    float age;
    Material ringMaterial;
    static readonly Color Light = new Color(.96f,.95f,.88f);
    public const float 绘圈时长=1.56f;
    public static float 绘圈进度(float seconds){
        if(seconds<.18f)return Mathf.Clamp01(seconds/.18f)*.08f;
        if(seconds<.50f)return .08f;
        return .08f+.92f*Mathf.Pow(Mathf.Clamp01((seconds-.50f)/(绘圈时长-.50f)),2.2f);
    }

    public static void 应用(Transform root)
    {
        if (!UIInkNavigation.启用) return;
        var panel=root.GetComponent<CharacterPanelUI>();
        if(panel==null || panel.tabs.Count<2) return;
        var page=panel.tabs[1].page;
        var view=page.GetComponent<UIInkRealmPage>();
        if(view==null) view=page.AddComponent<UIInkRealmPage>();
        view.Build();
    }
    void Build()
    {
        if(ready) return;
        var sprite=Resources.Load<Sprite>("UI/InkUI/Realm/fx-ink-circle");
        属性=GetComponentInChildren<UIAttributeList>(true);
        境界条=GetComponentInChildren<UIRealmBar>(true);
        gongfa=transform.Find("GongFaShow") as RectTransform;
        if(sprite==null || 属性==null || 境界条==null || gongfa==null) return;
        ready=true;
        foreach(var image in new[]{GetComponent<Image>(),属性.GetComponent<Image>(),gongfa.GetComponent<Image>(),境界条.GetComponent<Image>()}) if(image!=null) image.enabled=false;
        foreach(var motion in GetComponentsInChildren<UIInkMotion>(true)) motion.enabled=false;
        foreach(var title in new[]{transform.Find("AttrList/Title"),gongfa.Find("Title"),境界条.transform.Find("Title")}) if(title!=null) title.gameObject.SetActive(false);

        circle=UIBuildUtils.CreateRect("RealmCircle",transform);
        circle.anchorMin=circle.anchorMax=new Vector2(.18f,.64f);
        circle.sizeDelta=new Vector2(400,400);
        墨圈=UIBuildUtils.CreateImage("BrushRing",circle, new Color(.71f,.76f,.69f));
        UIBuildUtils.Stretch(墨圈.rectTransform); 墨圈.sprite=sprite; 墨圈.raycastTarget=false;
        墨圈.type=Image.Type.Filled; 墨圈.fillMethod=Image.FillMethod.Radial360; 墨圈.fillOrigin=(int)Image.Origin360.Left; 墨圈.fillClockwise=true;
        ringMaterial=new Material(Shader.Find("UI/InkCircle")); 墨圈.material=ringMaterial;
        gongfa.SetParent(circle,false);
        UIBuildUtils.Place(gongfa,new Vector2(.13f,.15f),new Vector2(.87f,.74f),Vector2.zero,Vector2.zero);
        center=gongfa.gameObject.GetComponent<CanvasGroup>(); if(center==null) center=gongfa.gameObject.AddComponent<CanvasGroup>();
        var info=gongfa.GetComponent<UIEntryInfo>();
        Place(info.nameText,0,.57f,1,.78f,25,TextAnchor.MiddleCenter);
        Place(info.tierText,0,.41f,1,.55f,17,TextAnchor.MiddleCenter);
        Place(info.descriptionText,.06f,.02f,.94f,.39f,17,TextAnchor.UpperCenter);
        境界条.realmNameText.transform.SetParent(gongfa,false);
        Place(境界条.realmNameText,0,.81f,1,1,33,TextAnchor.MiddleCenter);

        UIBuildUtils.Place(属性.transform as RectTransform,new Vector2(.30f,.38f),new Vector2(.77f,.94f),Vector2.zero,Vector2.zero);
        属性滚动=属性.GetComponent<ScrollRect>();
        属性.rowHeight=36; 属性.fontSize=20;
        var layout=属性.container.GetComponent<VerticalLayoutGroup>(); if(layout!=null) layout.padding=new RectOffset(6,90,6,6);
        if(属性滚动!=null) {
            属性滚动.horizontal=false; 属性滚动.scrollSensitivity=28;
            var vp=属性滚动.viewport;
            var backdrop=vp.GetComponent<Image>(); if(backdrop!=null) {backdrop.sprite=null;backdrop.color=Color.clear;}
            vp.gameObject.AddComponent<UIInkViewportFade>().FadeWidth=32;
            InkUITheme.ScrollbarStyle(属性滚动.verticalScrollbar);
        }
        属性.Rebuild();
        var bottom=境界条.transform as RectTransform;
        UIBuildUtils.Place(bottom,new Vector2(.055f,.07f),new Vector2(.92f,.33f),Vector2.zero,Vector2.zero);
        境界条.Refresh(); // 先让原组件建立信息文字，再布局，避免 Start 重新覆盖。
        var ink=UIBuildUtils.CreateImage("RealmInfoInk",bottom,Color.white); ink.transform.SetAsFirstSibling(); ink.raycastTarget=false;
        ink.sprite=Resources.Load<Sprite>("UI/InkUI/Bag/bag-info-ink"); UIBuildUtils.Stretch(ink.rectTransform);
        ink.rectTransform.localEulerAngles=new Vector3(0,0,90);
        ink.rectTransform.anchorMin=ink.rectTransform.anchorMax=new Vector2(.5f,.5f);
        ink.rectTransform.sizeDelta=new Vector2(270,1030);
        bottomReveal=ink.gameObject.AddComponent<UIInkReveal>();
        bottomInk=ink.rectTransform;
        Place(境界条.infoText,.05f,.04f,.95f,.63f,18,TextAnchor.UpperLeft);
        Place(境界条.expText,.05f,.72f,.55f,.91f,20,TextAnchor.MiddleLeft);
        Place(境界条.percentText,.70f,.72f,.95f,.91f,20,TextAnchor.MiddleRight);
        var track=境界条.progressFill.transform.parent as RectTransform;
        UIBuildUtils.Place(track,new Vector2(.05f,.65f),new Vector2(.95f,.69f),Vector2.zero,Vector2.zero);
        var oldTrack=track.GetComponent<Image>(); if(oldTrack!=null) {oldTrack.sprite=null;oldTrack.color=new Color(.5f,.6f,.53f,.25f);}
        境界条.progressFill.color=new Color(.67f,.79f,.70f);
        foreach(var text in bottom.GetComponentsInChildren<Text>(true)) {
            Style(text); var reveal=text.gameObject.AddComponent<UIInkReveal>(); reveals.Add(reveal);
        }
        foreach(var text in gongfa.GetComponentsInChildren<Text>(true)) Style(text);
        if(gameObject.activeInHierarchy) Restart();
    }
    static void Style(Text t) {
        if(t==null)return; t.color=Light;
        var s=t.GetComponent<Shadow>(); if(s==null)s=t.gameObject.AddComponent<Shadow>(); s.effectColor=new Color(0,0,0,.95f);s.effectDistance=new Vector2(1,-1);
    }
    static void Place(Text t,float x,float y,float xx,float yy,int size,TextAnchor align) {
        if(t==null)return; UIBuildUtils.Place(t.rectTransform,new Vector2(x,y),new Vector2(xx,yy),Vector2.zero,Vector2.zero);t.fontSize=size;t.alignment=align;Style(t);
    }
    void OnEnable(){if(ready)Restart();}
    void Restart(){ age=0; if(属性滚动!=null) 属性滚动.verticalNormalizedPosition=1; }
    void LateUpdate()
    {
        if(!ready)return;
        age+=Time.unscaledDeltaTime;
        var bottom=(RectTransform)境界条.transform;
        bottomInk.sizeDelta=new Vector2(bottom.rect.height*1.05f,bottom.rect.width*1.10f);
        float t=UIInkMotion.减少动效 ? 10 : age;
        墨圈.fillAmount=绘圈进度(t);
        circle.localEulerAngles=new Vector3(0,0,t>绘圈时长 && t<绘圈时长+.22f ? Mathf.Sin((t-绘圈时长)*36)*(1-(t-绘圈时长)/.22f)*.65f:0);
        center.alpha=Mathf.Clamp01((t-绘圈时长)/.2f);
        var gi=gongfa.GetComponent<UIEntryInfo>(); if(gi!=null && gi.tierText!=null) gi.tierText.color=Light;
        bottomReveal.进度=Mathf.Clamp01((t-.14f)/.26f);
        foreach(var r in reveals) r.进度=Mathf.Clamp01((t-.4f)/.2f);
        if(属性滚动==null)return;
        var viewport=属性滚动.viewport;
        var fade=viewport.GetComponent<UIInkViewportFade>();
        int n=属性.container.childCount;
        for(int i=0;i<n;i++) {
            var row=属性.container.GetChild(i) as RectTransform; if(!row.gameObject.activeSelf)continue;
            var relative=viewport.InverseTransformPoint(row.position);
            var ringCenter=viewport.InverseTransformPoint(circle.position);
            var ringEdge=viewport.InverseTransformPoint(circle.TransformPoint(new Vector3(circle.rect.width*.5f,0,0)));
            float radius=Mathf.Abs(ringEdge.x-ringCenter.x)+32;
            float dy=relative.y-ringCenter.y;
            float arcX=ringCenter.x+Mathf.Sqrt(Mathf.Max(0,radius*radius-dy*dy));
            float left=Mathf.Max(viewport.rect.xMin+12,arcX+8);
            row.anchoredPosition=new Vector2(row.rect.width*row.pivot.x+left-viewport.rect.xMin,row.anchoredPosition.y);
            var hover=row.GetComponent<UIInkRealmRow>(); if(hover==null) hover=row.gameObject.AddComponent<UIInkRealmRow>();
            hover.Initialize(属性.font);
            foreach(var text in row.GetComponentsInChildren<Text>()) {
                Style(text); UIInkNumber.Attach(text.name=="Value" ? text : null);
                if(text.GetComponent<UIInkFadeCoordinates>()==null)text.gameObject.AddComponent<UIInkFadeCoordinates>().Viewport=viewport;
                text.material=fade.Material;
                var r=text.GetComponent<UIInkReveal>();if(r==null)r=text.gameObject.AddComponent<UIInkReveal>();
                r.进度=Mathf.Clamp01((t-(Mathf.Min(n,12)-1-Mathf.Min(i,11))*.04f)/.2f);
            }
        }
    }
    void OnDestroy(){if(ringMaterial!=null)Destroy(ringMaterial);}
}

public class UIInkRealmRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    Image blot,stroke; float hover; bool inside;
    public void Initialize(Font font) {
        if(blot!=null)return;
        var hit=gameObject.GetComponent<Image>();if(hit==null)hit=gameObject.AddComponent<Image>();hit.color=Color.clear;
        blot=UIBuildUtils.CreateImage("AttributeInk",transform,new Color(.38f,.45f,.39f,.4f));
        blot.sprite=Resources.Load<Sprite>("UI/InkUI/Dynamic/nav-ink-blot-4");blot.raycastTarget=false;blot.transform.SetAsFirstSibling();UIBuildUtils.Stretch(blot.rectTransform);
        stroke=UIBuildUtils.CreateImage("AttributeStroke",transform,new Color(.77f,.81f,.72f));stroke.raycastTarget=false;
        stroke.rectTransform.anchorMin=stroke.rectTransform.anchorMax=new Vector2(0,.5f);stroke.rectTransform.pivot=new Vector2(1,.5f);stroke.rectTransform.anchoredPosition=new Vector2(-7,0);
    }
    public void OnPointerEnter(PointerEventData e){inside=true;}
    public void OnPointerExit(PointerEventData e){inside=false;}
    void OnDisable(){inside=false;hover=0;}
    void Update(){if(blot==null)return;hover=Mathf.MoveTowards(hover,inside?1:0,Time.unscaledDeltaTime/.12f);blot.color=new Color(.18f,.25f,.20f,.25f+hover*.45f);stroke.rectTransform.sizeDelta=new Vector2(5+hover*18,1.5f);}
}
