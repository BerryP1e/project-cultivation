using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>宣纸、水墨边缘与小型描金纹样；仅生成一次，所有背包实例共享。</summary>
public static class UIPaperArt
{
    public static readonly Color Ink=new Color(.14f,.22f,.21f);
    public static readonly Color Jade=new Color(.26f,.46f,.41f);
    public static readonly Color Ivory=new Color(.94f,.93f,.85f);
    static Sprite paper,tooltip,pattern,header,divider;
    static Sprite Make(int width,int height,System.Func<float,float,Color> pixel,string name)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
        var pixels=new Color[width*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++)pixels[y*width+x]=pixel((float)x/(width-1),(float)y/(height-1));
        texture.SetPixels(pixels);texture.Apply(false,true);var sprite=Sprite.Create(texture,new Rect(0,0,width,height),Vector2.one*.5f,100);sprite.name=name;sprite.hideFlags=HideFlags.HideAndDontSave;return sprite;
    }
    public static Sprite Paper => paper?paper:paper=Resources.Load<Sprite>("UI/InkUI/Bag/inventory-paper-layered");
    static Sprite sortBrush;
    public static Sprite SortBrush {get{if(sortBrush)return sortBrush;var source=Resources.Load<Sprite>("UI/InkUI/Bag/inventory-sort-brush");if(!source)return null;var rect=source.rect;sortBrush=Sprite.Create(source.texture,new Rect(rect.x,rect.y+rect.height*.25f,rect.width,rect.height*.54f),Vector2.one*.5f,100,0,SpriteMeshType.FullRect);return sortBrush;}}
    public static Sprite Tooltip => tooltip?tooltip:tooltip=Make(16,512,(x,y)=>{
        var c=Color.Lerp(new Color(.009f,.017f,.018f,.97f),new Color(.055f,.20f,.23f,.97f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,1,y)));c.a*=Mathf.SmoothStep(0,1,y/.26f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.92f,1,y)));return c;
    },"PaperInventory_Tooltip");
    public static Sprite Header => header?header:header=Make(8,256,(x,y)=>new Color(1,1,1,.82f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,1,y))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.92f,1,y)))),"PaperInventory_QualityFade");
    public static Sprite Divider => divider?divider:divider=Make(256,2,(x,y)=>new Color(1,1,1,.24f*Mathf.SmoothStep(0,1,x/.10f)*Mathf.SmoothStep(0,1,(1-x)/.10f)),"PaperInventory_Divider");
    public static Sprite Pattern => pattern?pattern:pattern=Make(320,320,(x,y)=>{
        var p=new Vector2(x-.5f,y-.5f)*2;float r=p.magnitude,a=Mathf.Atan2(p.y,p.x);
        float rings=Mathf.Min(Mathf.Abs(r-.64f),Mathf.Abs(r-.83f));float petals=Mathf.Abs(r-(.49f+.11f*Mathf.Cos(a*8)));
        float spokes=Mathf.Abs(Mathf.Sin(a*8+r*4))*r;float ink=Mathf.Max(Mathf.Clamp01((.014f-rings)*80),Mathf.Clamp01((.012f-petals)*90));
        ink=Mathf.Max(ink,Mathf.Clamp01((.008f-spokes)*100)*Mathf.SmoothStep(.28f,.5f,r)*(1-Mathf.SmoothStep(.72f,.83f,r)));
        return new Color(.30f,.46f,.40f,ink*.18f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.90f,.96f,r))));
    },"PaperInventory_CloudSeal");
    public static void Place(RectTransform r,float x,float y,float xx,float yy)=>UIBuildUtils.Place(r,new Vector2(x,y),new Vector2(xx,yy),Vector2.zero,Vector2.zero);
    public static Text Label(Transform parent,string name,string text,int size,TextAnchor alignment,Color color)
    {
        var t=UIBuildUtils.CreateText(name,parent,Resources.Load<Font>("Fonts/SimHei")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),text,size,alignment,color);t.raycastTarget=false;return t;
    }
    public static void Button(Button button,bool dark=false)
    {
        foreach(var c in button.GetComponents<UICharacterActionButton>())c.enabled=false;
        foreach(var c in button.GetComponents<UIInkFluid>())c.enabled=false;
        foreach(var c in button.GetComponents<UIInkMotion>())c.enabled=false;
        foreach(var c in button.GetComponents<UIInkActionButton>())c.enabled=false;
        var oldBlot=button.transform.Find("InkActionBlot");if(oldBlot)oldBlot.gameObject.SetActive(false);
        button.transition=Selectable.Transition.None;button.image.sprite=null;button.image.overrideSprite=null;
        var effect=button.GetComponent<UIPaperButton>()??button.gameObject.AddComponent<UIPaperButton>();effect.Dark=dark;effect.Apply();
    }
}

public sealed class UIPaperButton : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
{
    public bool Dark,Selected,Brush,分类,物品格,有物品,圆形,装饰,关闭;public Color? BaseColor;public int State {get;private set;}
    bool hover,pressed;Image border;Button button;Text[] labels;UIPaperRelief relief;UIPaperPressOffset[] offsets;UIPaperCloseGlyph closeGlyph;
    float hoverValue,pressValue,selectedValue,hoverVelocity,pressVelocity,selectedVelocity;int updatedFrame=-1;
    public void Apply(){button=GetComponent<Button>();labels=GetComponentsInChildren<Text>(true);if(!border){border=UIBuildUtils.CreateImage("PaperStateStroke",transform,Color.clear);border.raycastTarget=false;UIPaperArt.Place(border.rectTransform,0,0,1,.035f);}Draw();}
    void Draw(){if(!button||!button.image)return;State=!button.interactable?3:pressed?2:hover?1:0;
        if(分类||物品格||圆形)
        {
            if(!relief)relief=GetComponent<UIPaperRelief>()??gameObject.AddComponent<UIPaperRelief>();
            relief.绘制(分类,有物品,Selected,hover&&button.interactable,pressed&&button.interactable,BaseColor??UIPaperArt.Jade,圆形);
            button.image.color=Color.clear;if(border)border.enabled=false;
            if(labels!=null)foreach(var text in labels)if(text)text.color=button.interactable||物品格?UIPaperArt.Ink:new Color(.46f,.50f,.46f,.5f);
            return;
        }
        if(updatedFrame!=Time.frameCount){updatedFrame=Time.frameCount;float delta=Mathf.Min(Time.unscaledDeltaTime,.04f);hoverValue=Mathf.SmoothDamp(hoverValue,hover&&button.interactable?1:0,ref hoverVelocity,.035f,Mathf.Infinity,delta);pressValue=Mathf.SmoothDamp(pressValue,pressed&&button.interactable?1:0,ref pressVelocity,pressed?.018f:.038f,Mathf.Infinity,delta);selectedValue=Mathf.SmoothDamp(selectedValue,Selected?1:0,ref selectedVelocity,.035f,Mathf.Infinity,delta);if(UIInkMotion.减少动效){hoverValue=hover?1:0;pressValue=pressed?1:0;selectedValue=Selected?1:0;}}
        if(关闭&&!closeGlyph){closeGlyph=UIBuildUtils.CreateRect("InkCloseCross",transform).gameObject.AddComponent<UIPaperCloseGlyph>();UIBuildUtils.Stretch(closeGlyph.rectTransform);closeGlyph.raycastTarget=false;offsets=null;}
        if(offsets==null){var graphics=GetComponentsInChildren<Graphic>(true);offsets=new UIPaperPressOffset[graphics.Length];for(int i=0;i<graphics.Length;i++)offsets[i]=graphics[i].GetComponent<UIPaperPressOffset>()??graphics[i].gameObject.AddComponent<UIPaperPressOffset>();}
        foreach(var offset in offsets)if(offset)offset.下沉(2.2f*pressValue);
        float emphasis=Mathf.Max(hoverValue,selectedValue);
        button.image.color=装饰?Color.Lerp(Color.white,new Color(.70f,.83f,.75f),pressValue):Brush?Color.Lerp(Color.Lerp(Color.white,new Color(1.09f,1.09f,1.03f),hoverValue),new Color(.63f,.79f,.78f),pressValue):Dark?new Color(.055f,.12f,.12f,Mathf.Lerp(.65f,.88f,emphasis)+.10f*pressValue):Color.Lerp(BaseColor??new Color(.36f,.56f,.49f,.13f),new Color(.20f,.43f,.38f,.56f),Mathf.Max(pressValue,Mathf.Max(hoverValue*.60f,selectedValue*.40f)));
        if(关闭){button.image.color=Color.clear;closeGlyph.绘制(hoverValue,pressValue,button.interactable);}
        if(border){border.enabled=!装饰&&!关闭;border.color=Color.Lerp(new Color(.40f,.55f,.47f,.26f),new Color(.78f,.66f,.37f,1),Mathf.Max(emphasis,pressValue));}
        if(labels!=null)foreach(var t in labels)if(t)t.color=!button.interactable?new Color(.46f,.50f,.46f,.5f):Dark||Brush?UIPaperArt.Ivory:UIPaperArt.Ink;
    }
    public void OnPointerEnter(PointerEventData e){hover=true;Draw();}public void OnPointerExit(PointerEventData e){hover=pressed=false;Draw();}
    public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)pressed=true;Draw();}public void OnPointerUp(PointerEventData e){pressed=false;Draw();}
    void OnDisable(){hover=pressed=false;hoverValue=pressValue=selectedValue=hoverVelocity=pressVelocity=selectedVelocity=0;updatedFrame=-1;}void LateUpdate(){Draw();}
}

/// <summary>纸面上的轻墨斜叉；不加厚玉框，46单位命中区内只画细笔触。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UIPaperCloseGlyph : MaskableGraphic
{
    public float Hover {get;private set;}public float Press {get;private set;}bool available=true;
    public void 绘制(float hover,float press,bool enabled){if(Mathf.Abs(Hover-hover)<.0001f&&Mathf.Abs(Press-press)<.0001f&&available==enabled)return;Hover=hover;Press=press;available=enabled;SetVerticesDirty();}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();float size=1+.12f*Hover-.10f*Press;var center=rectTransform.rect.center;
        var ink=Color.Lerp(new Color(.25f,.39f,.34f,.70f),new Color(.10f,.29f,.24f,1),Hover);ink=Color.Lerp(ink,new Color(.08f,.20f,.17f,1),Press);if(!available)ink.a*=.35f;
        Stroke(vh,center+new Vector2(-8.5f,-8)*size,center+new Vector2(8,8.5f)*size,.55f,.80f,ink);
        Stroke(vh,center+new Vector2(-8,8)*size,center+new Vector2(8.5f,-8.5f)*size,.75f,.45f,ink);
    }
    static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float wa,float wb,Color ink)
    {
        var n=new Vector2(-(b-a).y,(b-a).x).normalized;int first=vh.currentVertCount;
        float[] from={-wa-.65f,-wa,wa,wa+.65f},to={-wb-.65f,-wb,wb,wb+.65f};
        for(int i=0;i<4;i++){var c=ink;if(i==0||i==3)c.a=0;vh.AddVert(a+n*from[i],c,Vector2.zero);vh.AddVert(b+n*to[i],c,Vector2.zero);}
        for(int i=0;i<3;i++){int j=first+i*2;vh.AddTriangle(j,j+1,j+2);vh.AddTriangle(j+2,j+1,j+3);}
    }
}

public sealed class UIPaperCursor : MonoBehaviour
{
    Texture2D normal,pressed;public Texture2D CurrentTexture {get;private set;}public bool Pressed {get;private set;}
    Canvas overlay;RawImage glyph;bool focused=true,ownsVisibility,previousVisible;public Vector2 DisplaySize=>new Vector2(594f/882*30,30);
    // 两态的共同实图边界，只裁显示UV，不缩小或重采样1254像素源图。
    static readonly Rect ContentUV=new Rect(343f/1254,194f/1254,594f/1254,882f/1254);
    static readonly Vector2 Hotspot=new Vector2(.6f,.55f);
    void Awake(){normal=Resources.Load<Texture2D>("UI/InkUI/Bag/cursor-cloud-normal");pressed=Resources.Load<Texture2D>("UI/InkUI/Bag/cursor-cloud-pressed");var go=new GameObject("PaperCursorOverlay_Runtime",typeof(RectTransform),typeof(Canvas));overlay=go.GetComponent<Canvas>();overlay.renderMode=RenderMode.ScreenSpaceOverlay;overlay.sortingOrder=32760;overlay.pixelPerfect=true;var image=new GameObject("GeneratedCursor",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(go.transform,false);glyph=image.GetComponent<RawImage>();glyph.raycastTarget=false;glyph.uvRect=ContentUV;glyph.rectTransform.anchorMin=glyph.rectTransform.anchorMax=Vector2.zero;glyph.rectTransform.pivot=new Vector2(0,1);glyph.rectTransform.sizeDelta=DisplaySize;Apply(false);}
    void LateUpdate(){bool held=Input.GetMouseButton(0);Vector2 position=Input.mousePosition;var module=EventSystem.current?EventSystem.current.currentInputModule as StandaloneInputModule:null;if(module&&module.inputOverride){held=module.inputOverride.GetMouseButton(0);position=module.inputOverride.mousePosition;}if(held!=Pressed)Apply(held);if(glyph){glyph.rectTransform.anchoredPosition=new Vector2(Mathf.Round(position.x-Hotspot.x),Mathf.Round(position.y+Hotspot.y));glyph.enabled=focused&&Cursor.lockState==CursorLockMode.None;}if(focused&&normal){OwnVisibility();Cursor.visible=false;}}
    void OwnVisibility(){if(ownsVisibility)return;previousVisible=Cursor.visible;ownsVisibility=true;Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);Cursor.visible=false;}
    void Apply(bool held){Pressed=held;CurrentTexture=held&&pressed?pressed:normal;if(glyph)glyph.texture=CurrentTexture;}
    void ReleaseVisibility(){if(!ownsVisibility)return;Cursor.visible=previousVisible;ownsVisibility=false;}
    void OnApplicationFocus(bool value){focused=value;if(!value){Apply(false);ReleaseVisibility();if(glyph)glyph.enabled=false;}}
    void OnDisable(){ReleaseVisibility();if(overlay)overlay.gameObject.SetActive(false);}
    void OnEnable(){if(overlay)overlay.gameObject.SetActive(true);if(normal)Apply(false);}
    void OnDestroy(){ReleaseVisibility();if(overlay)Destroy(overlay.gameObject);}
}
