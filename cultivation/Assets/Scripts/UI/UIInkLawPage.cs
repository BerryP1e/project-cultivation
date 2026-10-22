using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>御风人物、六位倾斜仪轨与拖拽装备；收藏独立于背包。</summary>
public sealed class UIInkLawPage : MonoBehaviour
{
    public Camera 预览相机 => camera3d;
    public float 环角度 { get; private set; }
    public LawFragment 当前选择 { get; private set; }
    public int 当前槽位 { get; private set; }
    public LawFragment 拖拽法则 { get; private set; }
    public int 拖拽来源槽 { get; private set; } = -1;
    public UIInkLawPreview 人物预览 { get; private set; }
    public UIInkLawArc 收获弧带 { get; private set; }
    public UIInkLawFurnace 炉鼎 { get; private set; }
    public UIInkLawEssenceStream 精粹流 { get; private set; }
    public Image 精粹图标 { get; private set; }
    public bool 背包已打开 {get;private set;}
    public bool 熔炼已打开 => furnacePanel && furnacePanel.gameObject.activeSelf;
    public UIInkLawGrid 法则背包 {get;private set;}
    RectTransform overview,summaryRows,furnacePanel; Transform details; Button openBag;
    float essencePulse;
    public const float 预览放大倍数 = 1.45f;
    UIPanelData data; Font font; bool built; float nextRender, lastRender, orbitSpeed, speedDamping; bool orbitHeld;
    RectTransform board; RawImage preview;
    Text nameText, scoreText, realmText, empty; Image icon;
    RectTransform detailRows; ScrollRect detailScroll;
    Button refine; Text refineCost, essence;
    readonly List<RectTransform> attributeViews = new List<RectTransform>();
    Transform stage, ring; Camera camera3d; RenderTexture target;
    readonly List<Mesh> meshes = new List<Mesh>();
    readonly List<Material> materials = new List<Material>();
    readonly List<Button> slotButtons = new List<Button>();
    readonly List<Transform> slotModels = new List<Transform>();
    readonly List<SpriteRenderer> slotIcons = new List<SpriteRenderer>();
    readonly List<UIInkLawGlyph> slotArt = new List<UIInkLawGlyph>();
    readonly List<Transform[]> slotRings = new List<Transform[]>();
    readonly List<LineRenderer> strands = new List<LineRenderer>();
    readonly Vector2[] pointerShift = new Vector2[6];
    static readonly Color Ivory = new Color(.95f, .94f, .85f);
    public static void 应用(Transform root)
    {
        var panel = root.GetComponent<CharacterPanelUI>(); if (!panel || panel.tabs.Count < 5 || !UIInkNavigation.启用) return;
        var page = panel.tabs[4].page; if (!page) return;
        var nav = panel.tabs[4].button ? panel.tabs[4].button.transform.Find("InkIcon")?.GetComponent<Image>() : null;
        if (nav && LawFragmentCatalog.取()) { nav.sprite = LawFragmentCatalog.取().境界[0].图标; nav.color = Ivory; nav.preserveAspect = true; }
        (page.GetComponent<UIInkLawPage>() ?? page.AddComponent<UIInkLawPage>()).Build(root.GetComponent<UIPanelData>());
    }
    void Build(UIPanelData source)
    {
        if (built || !source || !LawFragmentCatalog.取()) return; data = source; data.EnsureLists();
        font = Resources.Load<Font>("Fonts/SimHei") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (var old in GetComponentsInChildren<UIEntryList>(true)) old.enabled = false;
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var oldImage = GetComponent<Image>(); if (oldImage) oldImage.enabled = false;
        var workspace = Backdrop("LawWorkspace", .005f, .08f, .797f, .97f);
        workspace.gameObject.SetActive(false);
        board = UIBuildUtils.CreateRect("LawOrbit", transform); Place(board, .005f, .43f, .63f, 1.10f);
        Label("LawTitle", transform, "法 则", 29, .30f, .94f, .70f, .99f);
        preview = UIBuildUtils.CreateRect("Law3D", board).gameObject.AddComponent<RawImage>(); Place(preview.rectTransform, .02f, .035f, .98f, .87f); preview.raycastTarget = true; preview.gameObject.AddComponent<UIInkLawDrag>().初始化(this);
        var owned = UIBuildUtils.CreateRect("LawInventory",transform);Place(owned,0,0,.80f,1);var hit=owned.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        owned.gameObject.AddComponent<UIInkLawInventoryDrop>().初始化(this);收获弧带=owned.gameObject.AddComponent<UIInkLawArc>();收获弧带.初始化(this,font);owned.gameObject.SetActive(false);
        Label("InventoryTitle",owned,"收 获 法 则",23,.79f,.89f,.995f,.97f);
        empty=Label("EmptyLaw",owned,"暂无未装备法则",18,.25f,.32f,.68f,.42f);empty.raycastTarget=false;
        var info = UIBuildUtils.CreateRect("LawDetails", transform);details=info; Place(info, .75f, .025f, .999f, .985f);
        var infoInk = UIBuildUtils.CreateImage("LawInfoInk", info, Color.white); infoInk.sprite = InkUITheme.Load("Bag/bag-info-ink"); infoInk.raycastTarget = false; UIBuildUtils.Stretch(infoInk.rectTransform);infoInk.gameObject.SetActive(false);UICharacterBackdrop.Panel(info);
        // 墨底毛边留在文字区外，避免长数值贴着透明边缘。
        infoInk.rectTransform.offsetMin = new Vector2(-24, -12); infoInk.rectTransform.offsetMax = new Vector2(24, 12);
        icon = UIBuildUtils.CreateImage("LawIcon", info, Color.white); icon.preserveAspect = true; Place(icon.rectTransform, .52f, .80f, .94f, .96f);
        nameText = Label("LawName", info, "", 25, .07f, .92f, .94f, .99f); scoreText = Label("LawScore", info, "", 17, .07f, .85f, .53f, .92f);
        realmText = Label("LawRealm", info, "", 18, .07f, .79f, .55f, .85f);
        var detailRT = UIBuildUtils.CreateRect("DetailScroll", info); Place(detailRT, .035f, .18f, .97f, .76f);UICharacterBackdrop.Divider(info,"LawHeaderDivider",.77f); detailScroll = detailRT.gameObject.AddComponent<ScrollRect>(); detailScroll.horizontal = false; detailScroll.scrollSensitivity = 28;
        var detailViewport = UIBuildUtils.CreateRect("Viewport", detailRT); Place(detailViewport, 0, 0, .93f, 1); var ih = detailViewport.gameObject.AddComponent<Image>(); ih.color = Color.clear; detailViewport.gameObject.AddComponent<RectMask2D>();
        detailRows = UIBuildUtils.CreateRect("LawAttributes", detailViewport); detailRows.anchorMin = new Vector2(0, 1); detailRows.anchorMax = Vector2.one; detailRows.pivot = new Vector2(.5f, 1); detailRows.offsetMin = detailRows.offsetMax = Vector2.zero;
        var layout = detailRows.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(6, 3, 4, 8); layout.spacing = 7; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        detailRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; detailScroll.content = detailRows; detailScroll.viewport = detailViewport;
        var barRT = UIBuildUtils.CreateRect("DetailScrollbar", detailRT); Place(barRT, .96f, 0, 1, 1); barRT.gameObject.AddComponent<Image>();
        var bar = barRT.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
        var handle = UIBuildUtils.CreateImage("Handle", barRT, Color.white); UIBuildUtils.Stretch(handle.rectTransform); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle;
        detailScroll.verticalScrollbar = bar; detailScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; InkUITheme.ScrollbarStyle(bar);
        foreach(var title in new[]{nameText,scoreText,realmText}){title.alignment=TextAnchor.MiddleLeft;title.resizeTextForBestFit=true;title.resizeTextMinSize=13;title.resizeTextMaxSize=title.fontSize;}
        refine=InkButton("RefineLaw",info,"法则洗练",.52f,.07f,.97f,.14f);refine.onClick.AddListener(洗练);
        refineCost=Label("RefineCost",info,"",16,.06f,.035f,.94f,.085f);
        furnacePanel=UIBuildUtils.CreateRect("LawSmeltingPanel",transform);Place(furnacePanel,.29f,.025f,.735f,.36f);UICharacterBackdrop.Panel(furnacePanel).color=new Color(.055f,.10f,.12f,.93f);
        var furnaceHost=UIBuildUtils.CreateRect("LawFurnace",furnacePanel);Place(furnaceHost,.58f,.08f,.97f,.98f);
        炉鼎=furnaceHost.gameObject.AddComponent<UIInkLawFurnace>();炉鼎.初始化(this,data,font);
        var insert=InkButton("InsertAllLaws",furnacePanel,"一键放入",.04f,.14f,.29f,.34f);insert.onClick.AddListener(()=>炉鼎.全部放入());
        var smelt=InkButton("SmeltLaws",furnacePanel,"开始熔炼",.32f,.14f,.57f,.34f);smelt.onClick.AddListener(()=>炉鼎.开始熔炼());
        炉鼎.设置按钮(insert,smelt);
        var closeFurnace=InkButton("CloseLawFurnace",furnacePanel,"收起",.04f,.72f,.22f,.92f);closeFurnace.onClick.AddListener(()=>furnacePanel.gameObject.SetActive(false));
        var smeltEntry=InkButton("OpenLawFurnace",info,"熔炼法则",.03f,.07f,.49f,.14f);smeltEntry.onClick.AddListener(()=>furnacePanel.gameObject.SetActive(true));
        essence=Label("LawEssence",transform,"",18,.093f,.875f,.36f,.927f);essence.alignment=TextAnchor.MiddleLeft;essence.raycastTarget=false;
        精粹图标=UIBuildUtils.CreateImage("EssenceIcon",transform,Color.white);精粹图标.sprite=Resources.Load<Sprite>("Laws/Crafting/law-essence");精粹图标.preserveAspect=true;精粹图标.raycastTarget=false;Place(精粹图标.rectTransform,.043f,.875f,.086f,.927f);
        var stream=UIBuildUtils.CreateRect("EssenceStream",transform);UIBuildUtils.Stretch(stream);
        精粹流=stream.gameObject.AddComponent<UIInkLawEssenceStream>();精粹流.初始化(精粹图标.sprite,精粹图标.rectTransform,()=>essencePulse=.4f);
        BuildOverview();BuildStage(); built = true; data.Changed += Refresh; Refresh();切换背包(false);
    }
    void BuildOverview()
    {
        overview=UIBuildUtils.CreateRect("LawOverview",transform);Place(overview,.015f,.13f,.285f,.85f);UICharacterBackdrop.Panel(overview);
        var heading=Label("LawOverviewTitle",overview,"法则增益",24,.06f,.91f,.94f,1);heading.alignment=TextAnchor.MiddleLeft;
        var host=UIBuildUtils.CreateRect("LawSummaryScroll",overview);Place(host,.03f,.02f,.97f,.89f);
        var scroll=host.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.scrollSensitivity=28;
        var viewport=UIBuildUtils.CreateRect("Viewport",host);UIBuildUtils.Stretch(viewport);viewport.gameObject.AddComponent<Image>().color=Color.clear;viewport.gameObject.AddComponent<RectMask2D>();
        summaryRows=UIBuildUtils.CreateRect("Rows",viewport);summaryRows.anchorMin=new Vector2(0,1);summaryRows.anchorMax=Vector2.one;summaryRows.pivot=new Vector2(.5f,1);summaryRows.sizeDelta=Vector2.zero;
        var layout=summaryRows.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=5;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;
        summaryRows.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=summaryRows;scroll.viewport=viewport;
        openBag=InkButton("OpenLawInventory",transform,"法则背包",.025f,.025f,.275f,.105f);openBag.onClick.AddListener(()=>切换背包(true));
        var list=UIBuildUtils.CreateRect("LawGrid",transform);Place(list,.005f,.12f,.275f,.855f);法则背包=list.gameObject.AddComponent<UIInkLawGrid>();法则背包.初始化(this,font);
        var back=InkButton("BackToLawOverview",list,"返回总览",.03f,-.12f,.97f,-.025f);back.onClick.AddListener(()=>返回上层());
    }
    void 刷新汇总()
    {
        foreach(Transform child in summaryRows){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        var percent=new AttributeSet();var flat=new AttributeSet();AttributeSet basis=null;
        var stats=FindObjectOfType<PlayerCombatStats>();
        if(stats&&!stats.使用调试数值){basis=new AttributeSet();if(stats.玩家属性)basis.Add(stats.玩家属性.基础属性);if(stats.当前功法&&stats.功法等级>0)basis.Add(stats.当前功法.GetTotalBonus(stats.功法等级));}
        LawFragmentSystem.汇总(data,percent,flat,basis);
        int count=0;
        for(int i=0;i<AttributeUtil.Count;i++) {
            var a=(AttributeType)i;if(Mathf.Approximately(percent[a],0)&&Mathf.Approximately(flat[a],0))continue;
            var row=UIBuildUtils.CreateRect("LawBonusRow",summaryRows);row.gameObject.AddComponent<LayoutElement>().preferredHeight=38;
            var name=Label("Name",row,AttributeUtil.GetDisplayName(a),17,.03f,0,.54f,1);name.alignment=TextAnchor.MiddleLeft;
            string value=(percent[a]!=0?"+"+(percent[a]*100).ToString("0.#")+"%":"")+(percent[a]!=0&&flat[a]!=0?" / ":"")+(flat[a]!=0?"+"+显示固定值(a,flat[a]):"");
            var number=Label("Value",row,value,17,.54f,0,.97f,1);number.alignment=TextAnchor.MiddleRight;number.resizeTextForBestFit=true;number.resizeTextMinSize=13;number.resizeTextMaxSize=17;
            row.gameObject.AddComponent<Image>().color=new Color(.43f,.56f,.57f,count++%2==0?.12f:.03f);
        }
        if(count==0){var hint=Label("NoLawBonus",summaryRows,"装备法则后\n显示其带来的增益",19,0,0,1,1);hint.gameObject.AddComponent<LayoutElement>().preferredHeight=80;}
    }
    static string 显示固定值(AttributeType a,float value){var text=AttributeUtil.Format(a,value);return text=="0"&&value!=0?value.ToString("0.#####"):text;}
    public bool 是否已装备(LawFragment law)=>law!=null&&data.法则装备.Contains(law.id);
    public bool 是否在炉中(LawFragment law)=>law!=null&&data.炉中法则.Contains(law.id);
    public void 切换背包(bool open)
    {
        if(!built)return;结束拖拽(true);背包已打开=open;
        overview.gameObject.SetActive(!open);openBag.gameObject.SetActive(!open);法则背包.gameObject.SetActive(open);details.gameObject.SetActive(open);furnacePanel.gameObject.SetActive(false);
        Place(board,open?.28f:.30f,.04f,open?.745f:.995f,.95f);Place(preview.rectTransform,0,0,1,1);
        // 装备总览与背包使用同一舞台，仅调整取景留白，不重建人物和六槽。
        camera3d.fieldOfView=open?37:32;RefreshDetails();
    }
    public bool 返回上层()
    {
        if(熔炼已打开){furnacePanel.gameObject.SetActive(false);return true;}
        if(!背包已打开)return false;切换背包(false);return true;
    }
    RectTransform Backdrop(string id, float x0, float y0, float x1, float y1) { var image = UIBuildUtils.CreateImage(id, transform, new Color(.30f,.37f,.34f,.38f)); image.sprite = InkUITheme.Load("SkillsPage/common/panel-inner");image.type=Image.Type.Sliced; Place(image.rectTransform, x0, y0, x1, y1); return image.rectTransform; }
    Text Label(string id, Transform p, string s, int size, float x0, float y0, float x1, float y1) { var t = UIBuildUtils.CreateText(id, p, font, s, size, TextAnchor.MiddleCenter, Ivory);var shadow=t.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.8f);shadow.effectDistance=new Vector2(1,-1);Place(t.rectTransform, x0, y0, x1, y1); return t; }
    Button InkButton(string id,Transform p,string text,float x0,float y0,float x1,float y1){var b=UIBuildUtils.CreateButton(id,p,font,text,22);Place((RectTransform)b.transform,x0,y0,x1,y1);UICharacterBackdrop.StyleButton(b);return b;}
    void 洗练(){var law=当前选择;if(LawRefinementSystem.洗练(data,law)){炉鼎.洗练动效();ToastUI.提示(law.名称+" · 主属性精进");}}
    public void 播放精粹飞行(Vector3 mouth,long amount,float duration){精粹流.发射(mouth,amount,duration);}
    void Update(){if(!精粹图标)return;essencePulse=Mathf.Max(0,essencePulse-Time.unscaledDeltaTime);精粹图标.rectTransform.localScale=Vector3.one*(1+.18f*Mathf.Sin(essencePulse/.4f*Mathf.PI));}
    static void Place(RectTransform r, float x0, float y0, float x1, float y1) => UIBuildUtils.Place(r, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero);
    void BuildStage()
    {
        stage = new GameObject("LawPreview_Runtime").transform; stage.position = new Vector3(15000, 15000, 15000);
        var cam = new GameObject("LawCamera", typeof(Camera)); cam.transform.SetParent(stage, false); camera3d = cam.GetComponent<Camera>(); cam.transform.localPosition = new Vector3(0, 3.6f, -7.8f); cam.transform.LookAt(stage.position + Vector3.up * 1.4f);
        // 改取景角度，让人物、仪轨、槽位与粒子按同一比例放大；点击位置仍从相机投影获得。
        camera3d.enabled = false; camera3d.fieldOfView = 2 * Mathf.Atan(Mathf.Tan(37 * Mathf.Deg2Rad * .5f) / 预览放大倍数) * Mathf.Rad2Deg; camera3d.cullingMask = 1 << 31; camera3d.clearFlags = CameraClearFlags.SolidColor; camera3d.backgroundColor = Color.clear; camera3d.nearClipPlane = .1f; camera3d.farClipPlane = 30; camera3d.allowHDR = false;
        ring = new GameObject("LawRing").transform; ring.SetParent(stage, false); ring.localPosition = Vector3.up * 1.15f;
        var ink = new Material(Shader.Find("Sprites/Default")); materials.Add(ink);
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("WanderingStrand_" + i, typeof(LineRenderer)); go.layer = 31; go.transform.SetParent(ring, false); var line = go.GetComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 72; line.widthMultiplier = i == 0 ? .018f : .01f; line.sharedMaterial = ink;
            var c = i % 2 == 0 ? new Color(.72f, .67f, .46f, .7f) : new Color(.43f, .63f, .56f, .65f); var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(c.a, .18f), new GradientAlphaKey(c.a, .78f), new GradientAlphaKey(0, 1) }); line.colorGradient = gradient; strands.Add(line);
        }
        var actor = FindObjectOfType<PlayerController>(); var appearance = actor ? actor.GetComponent<玩家外观>() : null; var source = appearance ? appearance.玩家视觉 : actor ? actor.transform.Find("Player_Visual") : null;
        人物预览 = stage.gameObject.AddComponent<UIInkLawPreview>(); 人物预览.初始化(source, ring);
        var torus = CreateRing(); meshes.Add(torus); var ringMaterial = new Material(Shader.Find("UI/InkStarVolume")); materials.Add(ringMaterial);
        for (int i = 0; i < data.法则装备.Count; i++)
        {
            int slot = i; var node = new GameObject("LawSlot_" + i).transform; node.SetParent(ring, false); slotModels.Add(node); var pair = new Transform[2];
            for (int n = 0; n < 2; n++) { var go = new GameObject("OrbitalRing_" + n, typeof(MeshFilter), typeof(MeshRenderer)); go.layer = 31; go.transform.SetParent(node, false); go.GetComponent<MeshFilter>().sharedMesh = torus; go.GetComponent<MeshRenderer>().sharedMaterial = ringMaterial; pair[n] = go.transform; } slotRings.Add(pair);
            var glyph = new GameObject("LawGlyph", typeof(SpriteRenderer)); glyph.layer = 31; glyph.transform.SetParent(node, false); var renderer=glyph.GetComponent<SpriteRenderer>();slotIcons.Add(renderer);var art=node.gameObject.AddComponent<UIInkLawGlyph>();art.初始化(renderer);slotArt.Add(art);
            var b = UIBuildUtils.CreateButton("LawSlot_" + i, preview.transform, font, "", 19); b.image.color = Color.clear; b.transition = Selectable.Transition.None; b.onClick.AddListener(() => 选择槽(slot)); b.gameObject.AddComponent<UIInkLawSlot>().初始化(this, i);
            var text = b.GetComponentInChildren<Text>(); text.color = Ivory; text.raycastTarget = false; text.gameObject.AddComponent<Shadow>().effectColor = Color.black; slotButtons.Add(b);
        }
        stage.gameObject.SetActive(gameObject.activeInHierarchy);
    }
    public static Mesh CreateRing()
    {
        const int slices = 64, tube = 6; var v = new Vector3[(slices + 1) * (tube + 1)]; var normals = new Vector3[v.Length]; var colors = new Color[v.Length]; var triangles = new int[slices * tube * 6]; int t = 0;
        for (int i = 0; i <= slices; i++) for (int j = 0; j <= tube; j++) { float a = i * Mathf.PI * 2 / slices, b = j * Mathf.PI * 2 / tube; int k = i * (tube + 1) + j; var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); normals[k] = radial * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b); v[k] = radial * .47f + normals[k] * .012f; colors[k] = new Color(.61f, .69f, .51f, .85f); if (i == slices || j == tube) continue; int next = k + tube + 1; triangles[t++] = k; triangles[t++] = next; triangles[t++] = k + 1; triangles[t++] = k + 1; triangles[t++] = next; triangles[t++] = next + 1; }
        var mesh = new Mesh { name = "LawCrossingTorus" }; mesh.vertices = v; mesh.normals = normals; mesh.colors = colors; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
    }
    public void 旋转(float degrees) { 环角度 = Mathf.Repeat(环角度 + degrees, 360); }
    public void 开始转环() { orbitHeld = true; }
    public void 结束转环() { orbitHeld = false; }
    public float 自动转速 => orbitSpeed;
    public LawFragment 取槽(int slot) => LawFragmentSystem.取槽(data, slot);
    public void 选择槽(int slot) { 当前槽位 = slot; 当前选择 = 取槽(slot);切换背包(true); RefreshDetails(); }
    public void 选择(LawFragment law) { 当前选择 = law; RefreshDetails(); }
    public void 开始拖拽(LawFragment law, int slot, PointerEventData e)
    {
        if (law == null || !data.法则碎片.Contains(law)) return; 当前选择 = law; 拖拽法则 = law; 拖拽来源槽 = slot;
        UIDragContext.Begin(law, GetComponentInParent<Canvas>().rootCanvas.transform, font); UIDragContext.ApplyInkGhost(); var group = UIDragContext.Ghost.GetComponent<CanvasGroup>(); if (!group) group = UIDragContext.Ghost.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        foreach (var graphic in UIDragContext.Ghost.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false; var ghostIcon = UIDragContext.Ghost.transform.Find("Icon")?.GetComponent<Image>(); if (ghostIcon) ghostIcon.color = law.颜色; UIDragContext.Move(e.position); RefreshDetails();
    }
    public void 拖动(PointerEventData e) { if (拖拽法则 != null) UIDragContext.Move(e.position); }
    public void 结束拖拽(bool accepted = false) { if (拖拽法则 != null) UIDragContext.End(accepted); 拖拽法则 = null; 拖拽来源槽 = -1; }
    public void 放到槽(int slot) { if (拖拽法则 == null || !ReferenceEquals(UIDragContext.Entry, 拖拽法则)) return; var law = 拖拽法则; 结束拖拽(true); 当前槽位 = slot; LawFragmentSystem.装备(data, law.id, slot); }
    public void 放回收获() { if (拖拽法则 == null || 拖拽来源槽 < 0 || !ReferenceEquals(UIDragContext.Entry, 拖拽法则)) return; int slot = 拖拽来源槽; 结束拖拽(true); LawFragmentSystem.卸下(data, slot); }
    public void Refresh()
    {
        if (!built || !data) return; if (当前选择 != null && !data.法则碎片.Contains(当前选择)) 当前选择 = null;
        var inventory = data.法则碎片.Where(x => !data.法则装备.Contains(x.id)&&!data.炉中法则.Contains(x.id)).OrderByDescending(x => x.评分).ToList(); empty.gameObject.SetActive(inventory.Count == 0);
        essence.text="法则精粹  "+data.法则精粹.ToString("N0");炉鼎.Refresh();法则背包.设置(data.法则碎片.OrderByDescending(x=>x.评分));刷新汇总();
        if (当前选择 == null && inventory.Count > 0) 当前选择 = inventory[0];收获弧带.设置(inventory); RefreshDetails();
    }
    void RefreshDetails()
    {
        nameText.text = 当前选择 == null ? "" : 当前选择.名称; icon.sprite = 当前选择?.图标; icon.enabled = 当前选择 != null; icon.color = 当前选择?.颜色 ?? Color.white;
        scoreText.text = 当前选择 == null ? "" : "评分 " + 当前选择.评分 + " · 品质 " + (当前选择.品质 * 100).ToString("0") + "%";
        UIInkLawArt.应用(icon,当前选择,true);
        realmText.text = 当前选择 == null ? "" : LawFragmentCatalog.取().境界[当前选择.境界].境界名 + "期法则";
        refine.interactable=LawRefinementSystem.可洗练(当前选择)&&!data.炉中法则.Contains(当前选择.id)&&data.法则精粹>=LawRefinementSystem.洗练消耗(当前选择);
        refineCost.text=当前选择==null?"":!LawRefinementSystem.可洗练(当前选择)?"主属性已圆满":"精粹 "+LawRefinementSystem.洗练消耗(当前选择).ToString("N0");
        foreach (var view in attributeViews) view.gameObject.SetActive(false);
        int index = 0;
        if (当前选择 != null) foreach (bool main in new[] { true, false })
        {
            var rolls = 当前选择.属性.Where(x => x.主属性 == main).ToArray(); if (rolls.Length == 0) continue;
            var header = AttributeView(index++, true); header.GetComponentInChildren<Text>().text = main ? "主属性" : "副属性";
            foreach (var roll in rolls)
            {
                var row = AttributeView(index++, false);
                row.Find("Name").GetComponent<Text>().text = AttributeUtil.GetDisplayName(roll.属性);
                row.Find("Value").GetComponent<Text>().text = roll.大增幅 ? "+" + (roll.数值 * 100).ToString("0.#") + "% 增幅" : "+" + 显示固定值(roll.属性, roll.数值);
                var progress=row.Find("MainProgress").GetComponent<Image>();progress.gameObject.SetActive(roll.主属性);if(roll.主属性)Place(progress.rectTransform,.03f,.02f,Mathf.Lerp(.03f,.98f,LawRefinementSystem.主属性进度(当前选择,roll)),.055f);
            }
        }
        detailScroll.StopMovement(); detailScroll.verticalNormalizedPosition = 1;
    }
    RectTransform AttributeView(int index, bool heading)
    {
        // 主副数量随境界变化，复用内容节点；悬停直接共用境界页属性行效果。
        RectTransform view;
        if (index < attributeViews.Count && (attributeViews[index].GetComponent<UIInkRealmRow>() == null) != heading)
        { Destroy(attributeViews[index].gameObject); attributeViews[index] = null; }
        if (index < attributeViews.Count && attributeViews[index]) view = attributeViews[index];
        else
        {
            view = UIBuildUtils.CreateRect(heading ? "LawAttributeHeading" : "LawAttributeRow", detailRows);
            var element = view.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = heading ? 29 : 34;
            if (heading)
            { var title = UIBuildUtils.CreateText("Title", view, font, "", 19, TextAnchor.MiddleLeft, new Color(.82f, .84f, .72f)); UIBuildUtils.Stretch(title.rectTransform); }
            else
            {
                view.gameObject.AddComponent<UIInkRealmRow>().Initialize(font);
                var name = UIBuildUtils.CreateText("Name", view, font, "", 16, TextAnchor.MiddleLeft, Ivory); Place(name.rectTransform, .03f, .07f, .56f, .93f);
                var value = UIBuildUtils.CreateText("Value", view, font, "", 16, TextAnchor.MiddleRight, new Color(.79f, .85f, .75f)); Place(value.rectTransform, .56f, .07f, .98f, .93f);
                foreach(var text in new[]{name,value}){text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=16;text.horizontalOverflow=HorizontalWrapMode.Overflow;}
                var progress=UIBuildUtils.CreateImage("MainProgress",view,new Color(.75f,.81f,.55f,.5f));progress.raycastTarget=false;
            }
            foreach (var text in view.GetComponentsInChildren<Text>()) { text.raycastTarget = false; text.gameObject.AddComponent<Shadow>().effectColor = Color.black; }
            if (index < attributeViews.Count) attributeViews[index] = view; else attributeViews.Add(view);
        }
        view.SetSiblingIndex(index); view.gameObject.SetActive(true); return view;
    }
    void LateUpdate()
    {
        if (!built || board.rect.width < 1 || Time.unscaledTime < nextRender) return; float delta = Mathf.Min(.1f, Time.unscaledTime - lastRender); lastRender = Time.unscaledTime; nextRender = lastRender + 1f / 30;
        bool reduced = UIInkMotion.减少动效; float time = reduced ? 0 : Time.unscaledTime;
        bool held = orbitHeld || 拖拽法则 != null;
        orbitSpeed = reduced ? 0 : Mathf.SmoothDamp(orbitSpeed, held ? 0 : 7, ref speedDamping, held ? .16f : .65f, Mathf.Infinity, delta);
        环角度 = Mathf.Repeat(环角度 + orbitSpeed * delta, 360);
        ring.localRotation = Quaternion.Euler(-18 + Mathf.Sin(time * .35f) * 2, 0, -9 + Mathf.Sin(time * .3f) * 2);
        var rect = preview.rectTransform.rect; camera3d.aspect = rect.width / rect.height;
        // 窄背包展示区以横向留白决定视野，不能照搬宽总览的固定FOV裁掉左右法则。
        float vertical=背包已打开?37:32;
        camera3d.fieldOfView=2*Mathf.Atan(Mathf.Tan(vertical*Mathf.Deg2Rad*.5f)*Mathf.Max(1,1f/camera3d.aspect))*Mathf.Rad2Deg;
        float scale = GetComponentInParent<Canvas>().scaleFactor; int width = Mathf.Clamp(Mathf.RoundToInt(rect.width * scale), 256, 1024), height = Mathf.Clamp(Mathf.RoundToInt(rect.height * scale), 256, 1024);
        if (!target || target.width != width || target.height != height) { if (target) { target.Release(); Destroy(target); } target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 }; target.Create(); camera3d.targetTexture = target; preview.texture = target; }
        for (int s = 0; s < strands.Count; s++) for (int j = 0; j < 72; j++) { float angle = 环角度 * Mathf.Deg2Rad + s * 1.35f + j / 71f * (s == 0 ? 5.6f : 3.5f) + time * (s % 2 == 0 ? .10f : -.16f); float drift = Mathf.Sin(angle * 3 + time * .7f + s) * .025f; strands[s].SetPosition(j, new Vector3(Mathf.Sin(angle) * (2.05f + s * .012f + drift), Mathf.Sin(angle * 2 + time * .5f + s) * (.022f + s * .016f), Mathf.Cos(angle) * (1.28f + s * .013f + drift))); }
        for (int i = 0; i < slotModels.Count; i++)
        {
            var law = 取槽(i); float angle = (环角度 + i * 360f / slotModels.Count) * Mathf.Deg2Rad; var node = slotModels[i]; node.localPosition = new Vector3(Mathf.Sin(angle) * 2.05f, Mathf.Sin(time * 1.1f + i) * .035f, Mathf.Cos(angle) * 1.28f);
            var br = slotButtons[i].transform as RectTransform; RectTransformUtility.ScreenPointToLocalPointInRectangle(br, Input.mousePosition, null, out var local); float weight = Mathf.Clamp01(1 - local.magnitude / 160); var shift = reduced ? Vector2.zero : Vector2.ClampMagnitude(local * .1f, 12) * weight; pointerShift[i] = Vector2.Lerp(pointerShift[i], shift, 1 - Mathf.Exp(-delta * 12));
            node.position += (camera3d.transform.right * pointerShift[i].x + camera3d.transform.up * pointerShift[i].y) * .004f; node.rotation = camera3d.transform.rotation * Quaternion.Euler(-pointerShift[i].y * .8f, pointerShift[i].x * .8f, 0);
            for (int n = 0; n < 2; n++) { slotRings[i][n].localRotation = Quaternion.Euler(58 + n * 48, 25 + n * 55, time * (n == 0 ? 15 : -12) + i * 37); slotRings[i][n].localScale = Vector3.one * (n == 0 ? 1 : 1.12f); }
            var glyph = slotIcons[i]; glyph.sprite = law?.图标 ?? LawFragmentCatalog.取().境界[0].图标; float size = glyph.sprite ? Mathf.Max(glyph.sprite.bounds.size.x, glyph.sprite.bounds.size.y) : 1; glyph.transform.localScale = Vector3.one * ((law != null ? .66f : .40f) / Mathf.Max(.001f, size)); glyph.color = law?.颜色 ?? new Color(.62f, .68f, .61f, .33f); if (拖拽来源槽 == i) { var c = glyph.color; c.a *= .25f; glyph.color = c; }
            slotArt[i].设置(law,拖拽来源槽==i?.25f:1);
            var point = camera3d.WorldToViewportPoint(node.position); br.anchorMin = br.anchorMax = new Vector2(point.x, point.y); br.sizeDelta = Vector2.one * (point.z < 8 ? 94 : 78) * 预览放大倍数; br.anchoredPosition = Vector2.zero; var text = slotButtons[i].GetComponentInChildren<Text>(); text.text = (i + 1) + "\n" + (law?.名称 ?? "空 槽"); text.color = Ivory; Place(text.rectTransform, -.15f, -.46f, 1.15f, -.04f);
        }
        人物预览.推进(delta, 环角度, reduced); camera3d.Render();
    }
    void OnEnable() { if (built) { 切换背包(false);stage.gameObject.SetActive(true); lastRender = Time.unscaledTime; orbitSpeed = speedDamping = 0; Refresh(); } }
    void OnDisable() { if(furnacePanel)furnacePanel.gameObject.SetActive(false);结束拖拽(true); orbitHeld = false; orbitSpeed = speedDamping = 0; essencePulse=0; if(精粹图标)精粹图标.rectTransform.localScale=Vector3.one; if (stage) stage.gameObject.SetActive(false); }
    void OnDestroy() { if (data) data.Changed -= Refresh; if (stage) Destroy(stage.gameObject); if (target) { target.Release(); Destroy(target); } foreach (var m in meshes) Destroy(m); foreach (var m in materials) Destroy(m); }
}
public sealed class UIInkLawDrag : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler
{
    UIInkLawPage page; public void 初始化(UIInkLawPage owner) { page = owner; }
    public void OnDrag(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) page.旋转(-e.delta.x * .3f); }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) page.开始转环(); }
    public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) page.结束转环(); }
    public void OnBeginDrag(PointerEventData e) { OnPointerDown(e); }
    public void OnEndDrag(PointerEventData e) { OnPointerUp(e); }
}
