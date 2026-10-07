using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>两处宗门兑换共用细线窗口。物品效果决定分类，价格由物品表维护。</summary>
[DisallowMultipleComponent]
public class 功德堂兑换 : MonoBehaviour
{
    public bool 传法阁;
    public Font 字体;
    public bool 打印日志;
    readonly List<ItemDefinition> 上架的=new List<ItemDefinition>();
    readonly List<Button> 卡片=new List<Button>(),分类按钮=new List<Button>();
    Canvas 画布;RectTransform 面板,内容;
    Text 贡献文本,提示文本,名称,品阶,介绍,价格,库存,分类标题;
    Image 大图;Button 兑换按钮;ScrollRect 滚动;ItemDefinition 选中;int 分类;float 下一次刷新;
    static readonly Color 金=new Color(.72f,.64f,.43f),字=new Color(.91f,.89f,.8f),淡字=new Color(.59f,.64f,.6f);
    public GameObject 面板根=>面板!=null?面板.gameObject:null;
    public bool 面板已开=>面板!=null && 面板.gameObject.activeSelf;
    string 堂名=>传法阁?"传法阁":"功德堂";
    string[] 分类名=>传法阁?new[]{"全部典藏","功法秘籍","主动神通","被动神通","御兽契","炼丹丹方","法宝"}:new[]{"全部供物","灵植种子","洞府用具"};
    void Awake(){宗门贡献.变化+=贡献变化;}
    void OnDestroy(){宗门贡献.变化-=贡献变化;UiEscRegistry.SetSceneInputBlocked(this,false);if(画布!=null)Destroy(画布.gameObject);}
    void Update()
    {
        if(画布==null)return;
        if(!面板已开){画布.gameObject.SetActive(false);UiEscRegistry.SetSceneInputBlocked(this,false);return;}
        if(Time.unscaledTime>=下一次刷新){下一次刷新=Time.unscaledTime+.3f;刷新详情();}
        if(Input.GetKeyDown(KeyCode.Escape))关面板();
    }
    void 贡献变化(int _){if(面板已开)刷新详情();}
    public static bool 是传法物品(ItemDefinition i)=>i!=null && (i.使用效果 is 学功法效果 || i.使用效果 is 学主动神通效果
        || i.使用效果 is 学被动神通效果 || i.使用效果 is 学坐骑效果 || i.使用效果 is 学丹方效果 || i.使用效果 is 获得法宝效果);
    public List<ItemDefinition> 取兑换目录()
    {
        var list=new List<ItemDefinition>();var db=QuestDatabase.取();if(db==null || db.物品库==null)return list;
        var ids=new HashSet<string>();foreach(var i in db.物品库)
            if(i!=null && i.兑换消耗贡献>0 && 是传法物品(i)==传法阁 && ids.Add(i.物品id))list.Add(i);
        list.Sort((a,b)=>{int c=a.兑换消耗贡献.CompareTo(b.兑换消耗贡献);return c!=0?c:string.CompareOrdinal(a.物品id,b.物品id);});return list;
    }
    public void 开面板(){if(面板==null)建面板();画布.gameObject.SetActive(true);面板.gameObject.SetActive(true);UiEscRegistry.SetSceneInputBlocked(this,true);建目录();}
    public void 关面板(){if(面板!=null)面板.gameObject.SetActive(false);if(画布!=null)画布.gameObject.SetActive(false);UiEscRegistry.SetSceneInputBlocked(this,false);UiEscRegistry.记录关闭();}
    public bool 兑换(ItemDefinition i)
    {
        if(i==null || !取兑换目录().Contains(i))return false;
        var panel=玩家背包();if(panel==null){提示("未找到背包，未扣除贡献");return false;}
        if(!宗门贡献.扣(i.兑换消耗贡献,"兑换「"+i.DisplayName+"」")){提示("宗门贡献不足");return false;}
        panel.给物品(i);MainQuestTutorial.记录兑换(传法阁);提示("已将「"+i.DisplayName+"」放入背包");刷新详情();return true;
    }
    bool 属于分类(ItemDefinition i)
    {
        if(分类==0)return true;if(!传法阁)return 分类==1?i.物品id.StartsWith("item_seed_"):!i.物品id.StartsWith("item_seed_");
        return 分类==1?i.使用效果 is 学功法效果:分类==2?i.使用效果 is 学主动神通效果:分类==3?i.使用效果 is 学被动神通效果:分类==4?i.使用效果 is 学坐骑效果:分类==5?i.使用效果 is 学丹方效果:i.使用效果 is 获得法宝效果;
    }
    void 建目录()
    {
        上架的.Clear();foreach(var i in 取兑换目录())if(属于分类(i))上架的.Add(i);
        foreach(var b in 卡片){b.transform.SetParent(null,false);Destroy(b.gameObject);}卡片.Clear();
        foreach(var item in 上架的)
        {
            var b=按钮("物品_"+item.物品id,内容,"",20);卡片.Add(b);
            var icon=图("图标",b.transform,Color.white);icon.sprite=item.图标;icon.preserveAspect=true;放(icon.rectTransform,0,.22f,.27f,.85f,12,0,-2,0);
            文("名称",b.transform,item.DisplayName,22,字,TextAnchor.MiddleLeft,.3f,.46f,.97f,.91f);
            文("价格",b.transform,item.兑换消耗贡献+" 贡献",19,金,TextAnchor.MiddleLeft,.3f,.14f,.96f,.43f);
            b.onClick.AddListener(()=>{选中=item;刷新详情();});
        }
        分类标题.text=分类名[分类]+"  /  "+上架的.Count;
        if(选中==null || !上架的.Contains(选中))选中=上架的.Count>0?上架的[0]:null;
        for(int n=0;n<分类按钮.Count;n++)分类按钮[n].GetComponent<Image>().color=n==分类?new Color(.21f,.25f,.23f,.95f):new Color(.08f,.12f,.12f,.75f);
        Canvas.ForceUpdateCanvases();滚动.verticalNormalizedPosition=1;刷新详情();
    }
    void 刷新详情()
    {
        if(贡献文本==null)return;贡献文本.text="宗门贡献  "+宗门贡献.当前;
        foreach(var b in 卡片)b.GetComponent<Image>().color=选中!=null && b.name=="物品_"+选中.物品id?new Color(.2f,.26f,.23f,.98f):new Color(.07f,.11f,.12f,.86f);
        大图.sprite=选中!=null?选中.图标:null;大图.enabled=大图.sprite!=null;
        名称.text=选中!=null?选中.DisplayName:"暂无可兑换物品";品阶.text=选中!=null?选中.品阶.ToString():"";介绍.text=选中!=null?选中.介绍:"";
        价格.text=选中!=null?"兑换所需   "+选中.兑换消耗贡献+" 贡献":"";
        var panel=玩家背包();int count=0;if(panel!=null && panel.物品!=null && 选中!=null)foreach(var i in panel.物品)if(i!=null && i.物品id==选中.物品id)count++;
        库存.text=选中!=null?"背包中已有  "+count+" 件":"";兑换按钮.interactable=选中!=null && 宗门贡献.够(选中.兑换消耗贡献);
        兑换按钮.GetComponentInChildren<Text>().text=选中==null?"兑换":兑换按钮.interactable?"兑换 · 一份":"贡献不足";
    }
    void 提示(string s){if(提示文本!=null)提示文本.text=s;}
    static UIPanelData 玩家背包()
    {
        var player=GameObject.Find("Player");
        if(player!=null){var data=player.GetComponent<UIPanelData>();if(data!=null)return data;}
        var cultivation=FindObjectOfType<PlayerCultivation>();
        if(cultivation!=null && cultivation.面板数据!=null)return cultivation.面板数据;
        foreach(var data in FindObjectsOfType<UIPanelData>())if(data.isActiveAndEnabled)return data;
        return null;
    }
    void 建面板()
    {
        if(字体==null)foreach(var f in Resources.FindObjectsOfTypeAll<Font>())if(f.name.ToLowerInvariant().Contains("simhei")){字体=f;break;}
        var root=new GameObject(传法阁?"TeachingHallCanvas":"MeritHallCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        画布=root.GetComponent<Canvas>();画布.renderMode=RenderMode.ScreenSpaceOverlay;画布.sortingOrder=2600;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var shade=图("背景压暗",root.transform,new Color(0f,0f,0f,.5f));放(shade.rectTransform,0,0,1,1);shade.raycastTarget=true;
        var bg=图("堂阁周边素材",root.transform,Color.white);bg.sprite=Resources.Load<Sprite>("UI/SectExchange/"+(传法阁?"teaching-hall":"merit-hall"));放(bg.rectTransform,0,0,1,1);bg.preserveAspect=true;
        面板=图("细线窗口",root.transform,new Color(.025f,.055f,.06f,.18f)).rectTransform;面板.anchorMin=面板.anchorMax=new Vector2(.5f,.5f);面板.sizeDelta=new Vector2(1460,850);面板.anchoredPosition=Vector2.zero;
        框(面板);文("标题",面板,堂名,42,字,TextAnchor.MiddleLeft,.035f,.89f,.36f,.965f);
        文("副标题",面板,传法阁?"承先贤之道 · 阅诸法典藏":"积寸功 · 济修行",19,淡字,TextAnchor.MiddleLeft,.035f,.845f,.4f,.89f);
        贡献文本=文("贡献",面板,"",24,金,TextAnchor.MiddleRight,.55f,.895f,.89f,.95f);
        var close=按钮("关闭",面板,"×",30);放(close.transform as RectTransform,.93f,.89f,.975f,.958f);close.onClick.AddListener(关面板);线(面板,.035f,.824f,.965f,.824f);
        for(int n=0;n<分类名.Length;n++){int k=n;var b=按钮("分类_"+n,面板,分类名[n],23);放(b.transform as RectTransform,.035f,.72f-n*.085f,.185f,.79f-n*.085f);b.onClick.AddListener(()=>{分类=k;建目录();提示("");});分类按钮.Add(b);}
        分类标题=文("目录标题",面板,"",20,淡字,TextAnchor.MiddleLeft,.215f,.76f,.65f,.815f);
        var area=UIBuildUtils.CreateRect("目录滚动",面板);放(area,.215f,.11f,.66f,.75f);
        var view=图("视口",area,new Color(0,0,0,.01f)).rectTransform;放(view,0,0,1,1);view.gameObject.AddComponent<RectMask2D>();
        内容=UIBuildUtils.CreateRect("内容",view);内容.anchorMin=new Vector2(0,1);内容.anchorMax=Vector2.one;内容.pivot=new Vector2(.5f,1);内容.sizeDelta=Vector2.zero;
        var grid=内容.gameObject.AddComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=2;grid.cellSize=new Vector2(305,120);grid.spacing=new Vector2(14,14);grid.padding=new RectOffset(0,0,0,6);
        内容.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        滚动=area.gameObject.AddComponent<ScrollRect>();滚动.viewport=view;滚动.content=内容;滚动.horizontal=false;滚动.movementType=ScrollRect.MovementType.Clamped;滚动.scrollSensitivity=32;
        var detail=图("物品详情",面板,new Color(.07f,.1f,.1f,.8f)).rectTransform;放(detail,.69f,.11f,.965f,.79f);框(detail);
        大图=图("成品图标",detail,Color.white);大图.preserveAspect=true;放(大图.rectTransform,.28f,.67f,.72f,.96f);
        名称=文("物品名",detail,"",25,字,TextAnchor.MiddleCenter,.07f,.55f,.93f,.68f);品阶=文("品阶",detail,"",18,金,TextAnchor.MiddleCenter,.07f,.49f,.93f,.55f);
        var introView=图("介绍视口",detail,new Color(0,0,0,.01f)).rectTransform;放(introView,.09f,.24f,.91f,.46f);introView.gameObject.AddComponent<RectMask2D>();introView.GetComponent<Image>().raycastTarget=true;
        介绍=UIBuildUtils.CreateText("介绍",introView,字体,"",21,TextAnchor.UpperLeft,字);
        var intro=介绍.rectTransform;intro.anchorMin=new Vector2(0,1);intro.anchorMax=Vector2.one;intro.pivot=new Vector2(.5f,1);intro.sizeDelta=Vector2.zero;intro.anchoredPosition=Vector2.zero;
        intro.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var introScroll=introView.gameObject.AddComponent<ScrollRect>();introScroll.viewport=introView;introScroll.content=intro;introScroll.horizontal=false;introScroll.movementType=ScrollRect.MovementType.Clamped;introScroll.scrollSensitivity=24;
        库存=文("背包数量",detail,"",18,淡字,TextAnchor.MiddleLeft,.09f,.17f,.91f,.24f);
        价格=文("兑换价格",detail,"",20,金,TextAnchor.MiddleLeft,.09f,.1f,.91f,.17f);
        兑换按钮=按钮("兑换",detail,"兑换 · 一份",24);放(兑换按钮.transform as RectTransform,.09f,.025f,.91f,.105f);兑换按钮.onClick.AddListener(()=>兑换(选中));
        线(面板,.215f,.085f,.965f,.085f);提示文本=文("提示",面板,"",20,金,TextAnchor.MiddleLeft,.35f,.025f,.87f,.075f);
        文("关闭提示",面板,"ESC  返回",18,淡字,TextAnchor.MiddleLeft,.215f,.025f,.32f,.075f);
    }
    Image 图(string n,Transform p,Color c)=>UIBuildUtils.CreateImage(n,p,c);
    Text 文(string n,Transform p,string s,int size,Color c,TextAnchor a,float x,float y,float X,float Y)
    {var t=UIBuildUtils.CreateText(n,p,字体,s,size,a,c);t.verticalOverflow=VerticalWrapMode.Truncate;放(t.rectTransform,x,y,X,Y);return t;}
    Button 按钮(string n,Transform p,string s,int size)
    {
        var b=UIBuildUtils.CreateButton(n,p,字体,s,size);var image=b.GetComponent<Image>();image.raycastTarget=true;image.color=new Color(.08f,.12f,.12f,.85f);
        b.GetComponentInChildren<Text>().color=字;b.GetComponentInChildren<Text>().verticalOverflow=VerticalWrapMode.Truncate;
        var colors=b.colors;colors.highlightedColor=new Color(1.3f,1.3f,1.2f);colors.pressedColor=new Color(.7f,.8f,.75f);colors.disabledColor=new Color(.45f,.45f,.45f);b.colors=colors;框(b.transform);return b;
    }
    static void 放(RectTransform r,float x,float y,float X,float Y,float l=0,float b=0,float rr=0,float t=0)=>UIBuildUtils.Place(r,new Vector2(x,y),new Vector2(X,Y),new Vector2(l,b),new Vector2(rr,t));
    static void 线(Transform p,float x,float y,float X,float Y){var i=UIBuildUtils.CreateImage("细金线",p,new Color(金.r,金.g,金.b,.5f));放(i.rectTransform,x,y,X,Y);i.rectTransform.sizeDelta+=new Vector2(X==x?1:0,Y==y?1:0);}
    static void 框(Transform p){var f=UIBuildUtils.CreateRect("纹饰细框",p);UIBuildUtils.Stretch(f,1);var g=f.gameObject.AddComponent<UISectFineFrame>();g.color=new Color(金.r,金.g,金.b,.65f);g.raycastTarget=false;}
    public void Open()=>开面板();public void Close()=>关面板();public GameObject PanelRoot=>面板根;
}
