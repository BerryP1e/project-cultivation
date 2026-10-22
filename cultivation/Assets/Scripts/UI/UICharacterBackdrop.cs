using UnityEngine;
using UnityEngine.UI;

/// <summary>角色页面共用的渐变幕布。波纹使用非缩放时间，暂停菜单里仍自然流动。</summary>
public sealed class UICharacterBackdrop : MonoBehaviour
{
    Material owned;
    Sprite whiteUv;
    static Sprite rounded;
    Vector2 pointer;
    bool innerPanel;
    UIInkDensitySimulation simulation;
    Texture2D inkSeed;
    Vector2 lastUv,dragVelocity;
    bool hadPointer;
    public int 墨流步数=>simulation!=null?simulation.步数:0;
    public Texture 墨流纹理=>simulation?.纹理;
#if UNITY_EDITOR
    public Vector2? 调试鼠标;
#endif
    public static readonly Color TextColor = new Color(.94f,.95f,.90f);
    public static readonly Color Muted = new Color(.69f,.78f,.77f);
    public static void 应用(Transform root)
    {
        var panel=root.GetComponent<CharacterPanelUI>();
        if(!panel || !panel.panelRoot || !UIInkNavigation.启用) return;
        var window=panel.panelRoot.transform.Find("Window") as RectTransform;
        if(!window) return;
        var backdrop=window.Find("UnifiedCurtain");
        if(!backdrop) {
            var image=UIBuildUtils.CreateImage("UnifiedCurtain",window,Color.white);
            UIBuildUtils.Stretch(image.rectTransform);
            // 云雾过渡越出原窗口，四边不会落成一块硬切矩形。
            image.rectTransform.offsetMin=new Vector2(-54,-54);image.rectTransform.offsetMax=new Vector2(54,54);image.raycastTarget=false;
            image.transform.SetAsFirstSibling();image.gameObject.AddComponent<UICharacterBackdrop>();
        }
        var dim=panel.panelRoot.transform.Find("Dim")?.GetComponent<Image>();
        if(dim)dim.color=new Color(.035f,.037f,.028f,.48f);
        var hint=window.Find("HintBar");
        if(hint){var old=hint.GetComponent<Image>();if(old)old.enabled=false;Panel(hint);}
        foreach(var button in window.GetComponentsInChildren<Button>(true))
            if(!button.name.StartsWith("Tab_") && button.GetComponent<UIActiveSkillSlot>()==null
                && !button.name.StartsWith("LawSlot_") && button.name!="Row" && button.GetComponent<UIInkWaterfallCell>()==null
                && button.GetComponent<UIInkLawItemDrag>()==null && button.GetComponent<UIInkPassiveStar>()==null && !button.name.StartsWith("OwnedEquipment_") && !button.name.StartsWith("AppearanceRow_")) StyleButton(button);
    }
    void Awake()
    {
        var shader=Resources.Load<Shader>("UI/InkUI/Dynamic/CharacterCurtain");
        if(!shader)return;
        // 无 sprite 的 Image 只提供常量 UV，必须显式提供完整矩形才能绘制渐变。
        whiteUv=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f);
        owned=new Material(shader);GetComponent<Image>().sprite=whiteUv;GetComponent<Image>().material=owned;
    }
    void Update()
    {
        if(!owned)return;owned.SetFloat("_Clock",UIInkMotion.减少动效?0:Time.unscaledTime);
        var size=((RectTransform)transform).rect.size;owned.SetVector("_Size",new Vector4(size.x,size.y,0,0));owned.SetFloat("_PanelMode",innerPanel?1:0);
        Vector2 screen=Input.mousePosition;
#if UNITY_EDITOR
        screen=调试鼠标??screen;
#endif
        var canvas=GetComponentInParent<Canvas>();var camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        var rect=(RectTransform)transform;RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,camera,out var point);
        var uv=new Vector2((point.x-rect.rect.xMin)/rect.rect.width,(point.y-rect.rect.yMin)/rect.rect.height);
        bool inside=rect.rect.Contains(point);float dt=Mathf.Min(.05f,Time.unscaledDeltaTime);
        var delta=hadPointer?uv-lastUv:Vector2.zero;lastUv=uv;hadPointer=true;
        dragVelocity=Vector2.Lerp(dragVelocity,Vector2.ClampMagnitude(delta/Mathf.Max(.001f,dt),1.2f),1-Mathf.Exp(-dt*16));
        if(UIInkMotion.减少动效)dragVelocity=Vector2.zero;
        var goal=UIInkMotion.减少动效?Vector2.zero:uv-Vector2.one*.5f;
        pointer=Vector2.Lerp(pointer,goal,1-Mathf.Exp(-Time.unscaledDeltaTime*4));owned.SetVector("_Pointer",pointer);
        owned.SetVector("_Cursor",new Vector4(uv.x,uv.y,inside&&!UIInkMotion.减少动效?1:0,0));owned.SetVector("_Drag",dragVelocity);
        if(!innerPanel&&!UIInkMotion.减少动效){
            if(simulation==null){
                inkSeed=new Texture2D(128,72,TextureFormat.RGBA32,false,true);var colors=new Color[128*72];
                for(int y=0;y<72;y++)for(int x=0;x<128;x++)colors[y*128+x]=new Color(.10f,.12f,.11f,.24f+.65f*Mathf.PerlinNoise(x*.045f,y*.07f));
                inkSeed.SetPixels(colors);inkSeed.Apply();simulation=new UIInkDensitySimulation(inkSeed,17.31f,256,144);
            }
            simulation.推进(Time.unscaledDeltaTime,uv,dragVelocity,inside,uv,2);
            owned.SetTexture("_InkState",simulation.纹理);owned.SetFloat("_HasState",simulation.纹理?1:0);
        }
        if(UIInkMotion.减少动效)owned.SetFloat("_HasState",0);
    }
    void OnDisable(){hadPointer=false;dragVelocity=Vector2.zero;}
    void OnDestroy(){simulation?.释放();if(inkSeed)Destroy(inkSeed);if(owned)Destroy(owned);if(whiteUv)Destroy(whiteUv);}
    public static Sprite RoundedButton
    {
        get {
            if(rounded)return rounded;
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="CharacterButton_Runtime",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                var q=new Vector2(Mathf.Abs(x-31.5f)-18.5f,Mathf.Abs(y-31.5f)-18.5f);
                float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-12;
                float alpha=Mathf.Clamp01(.5f-distance);
                float edge=Mathf.Clamp01(1+distance);
                pixels[y*size+x]=Color.Lerp(new Color(.69f,.67f,.58f,.84f*alpha),new Color(.86f,.84f,.75f,.96f*alpha),edge);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);
            rounded=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(16,16,16,16));rounded.name="CharacterButton_Rounded";rounded.hideFlags=HideFlags.HideAndDontSave;return rounded;
        }
    }
    public static Image Panel(Transform host,string name="SoftPanel")
    {
        var old=host.Find(name)?.GetComponent<Image>();
        var image=old?old:UIBuildUtils.CreateImage(name,host,new Color(.10f,.16f,.18f,.30f));
        image.raycastTarget=false;image.transform.SetAsFirstSibling();
        UIBuildUtils.Stretch(image.rectTransform);
        var effect=image.GetComponent<UICharacterBackdrop>();if(!effect)effect=image.gameObject.AddComponent<UICharacterBackdrop>();effect.innerPanel=true;
        // 使用同一云雾材质作为弱分区，底缘比顶部更宽地消散。
        image.color=new Color(1,1,1,.30f);
        return image;
    }
    public static void StyleButton(Button button)
    {
        if(!button||!button.image)return;
        if(button.GetComponent<UIPaperButton>())return;
        if(!button.GetComponent<UICharacterActionButton>())button.gameObject.AddComponent<UICharacterActionButton>();
        var action=button.GetComponent<UIInkActionButton>();if(action)action.enabled=false;
        var fluid=button.GetComponent<UIInkFluid>();if(fluid)fluid.enabled=false;
        var motion=button.GetComponent<UIInkMotion>();if(motion)motion.enabled=false;
        var hit=button.GetComponent<InkUIHitShape>();if(hit)hit.enabled=false;
        var blot=button.transform.Find("InkActionBlot");if(blot)blot.gameObject.SetActive(false);
        button.transition=Selectable.Transition.ColorTint;
        button.image.overrideSprite=null;
        button.image.sprite=RoundedButton;
        button.image.type=Image.Type.Sliced;button.image.pixelsPerUnitMultiplier=1f;
        button.image.color=Color.white;
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.98f,.85f);colors.pressedColor=new Color(.72f,.81f,.79f);colors.disabledColor=new Color(.7f,.75f,.74f,.5f);button.colors=colors;
        foreach(var text in button.GetComponentsInChildren<Text>(true)) {
            text.color=new Color(.12f,.22f,.23f);var shadow=text.GetComponent<Shadow>();if(shadow)shadow.enabled=false;
        }
    }
    public static Image Divider(Transform parent,string name,float y)
    {
        var image=UIBuildUtils.CreateImage(name,parent,new Color(.71f,.82f,.80f,.25f));
        image.raycastTarget=false;UIBuildUtils.Place(image.rectTransform,new Vector2(.04f,y),new Vector2(.96f,y),new Vector2(0,-.5f),new Vector2(0,.5f));return image;
    }
}
