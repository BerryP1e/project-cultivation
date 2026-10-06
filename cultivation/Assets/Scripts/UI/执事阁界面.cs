using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class 执事阁界面 : MonoBehaviour
{
    public Font 字体;
    Canvas 画布; RectTransform 面板, 内容; ScrollRect 滚动;
    Text 贡献, 标题, 详情, 奖励, 状态, 提示;
    Button 行动, 追踪, 放弃; Image 图标;
    readonly List<Button> 分类按钮 = new List<Button>();
    readonly List<Button> 行 = new List<Button>();
    readonly List<宗门委托> 显示任务 = new List<宗门委托>();
    宗门委托 选中; int 分类; float 下次刷新;
    static readonly Color 金 = new Color(.72f,.64f,.43f), 字 = new Color(.91f,.89f,.8f), 淡 = new Color(.64f,.69f,.65f);
    static readonly string[] 分类名 = { "全部委托", "物品提交", "妖魔悬赏", "已接委托" };
    public GameObject 面板根 => 面板 != null ? 面板.gameObject : null;
    public bool 面板已开 => 面板 != null && 面板.gameObject.activeSelf;
    void Awake() { 宗门任务.变化 += 重绘; }
    void OnDestroy() { 宗门任务.变化 -= 重绘; UiEscRegistry.SetSceneInputBlocked(this,false); if(画布 != null) Destroy(画布.gameObject); }
    void Update()
    {
        if (画布 == null) return;
        if (!面板已开) { 画布.gameObject.SetActive(false); UiEscRegistry.SetSceneInputBlocked(this,false); return; }
        if (Time.unscaledTime >= 下次刷新) { 下次刷新 = Time.unscaledTime + .3f; 刷详情(); }
        if (Input.GetKeyDown(KeyCode.Escape)) 关面板();
    }
    public void 开面板() { if (面板 == null) 建界面(); 画布.gameObject.SetActive(true); 面板.gameObject.SetActive(true); UiEscRegistry.SetSceneInputBlocked(this,true); 列任务(); }
    public void 关面板() { if (面板 != null) 面板.gameObject.SetActive(false); if (画布 != null) 画布.gameObject.SetActive(false); UiEscRegistry.SetSceneInputBlocked(this,false); UiEscRegistry.记录关闭(); }
    void 重绘() { if (面板已开) 列任务(); }
    void 列任务()
    {
        foreach (var b in 行) { b.transform.SetParent(null,false); Destroy(b.gameObject); } 行.Clear(); 显示任务.Clear();
        foreach (var q in 宗门任务.全部)
        {
            if (分类 == 1 && q.是悬赏 || 分类 == 2 && !q.是悬赏 || 分类 == 3 && 宗门任务.记录(q.id) == null) continue;
            显示任务.Add(q);
            var b = 按钮("委托_" + q.id,内容,"",22); 行.Add(b);
            文("名称",b.transform,q.名称,23,字,.04f,.48f,.96f,.9f);
            文("类型",b.transform,q.是悬赏 ? "悬赏 · Lv"+q.等级 : "提交 · "+q.目标介绍,18,淡,.04f,.12f,.73f,.44f);
            文("奖励",b.transform,q.奖励贡献+" 贡献",18,金,.74f,.12f,.96f,.44f);
            b.onClick.AddListener(()=>{选中=q;刷详情();});
        }
        if (选中 == null || !显示任务.Contains(选中)) 选中 = 显示任务.FirstOrDefault();
        for(int i=0;i<分类按钮.Count;i++) 分类按钮[i].GetComponent<Image>().color=i==分类 ? new Color(.21f,.25f,.23f,.95f) : new Color(.08f,.12f,.12f,.75f);
        Canvas.ForceUpdateCanvases(); 滚动.verticalNormalizedPosition=1; 刷详情();
    }
    void 刷详情()
    {
        if (贡献 == null) return; 贡献.text="宗门贡献  "+宗门贡献.当前;
        foreach (var b in 行) b.GetComponent<Image>().color=选中 != null && b.name=="委托_"+选中.id ? new Color(.2f,.26f,.23f,.95f) : new Color(.06f,.09f,.09f,.8f);
        var q=选中; var r=宗门任务.记录(q?.id); bool complete=宗门任务.可交付(q);
        标题.text=q?.名称 ?? "暂无委托"; 图标.sprite=q?.需求物品?.图标;
        if(q != null && q.是悬赏) 图标.sprite=PanelDatabase.取()?.神通?.FirstOrDefault(a=>a.神通id=="ability_xiao_jianzhen")?.图标;
        图标.enabled=图标.sprite != null;
        奖励.text=q != null ? "完成奖励   "+q.奖励贡献+" 宗门贡献" : "";
        if(q==null){详情.text="当前分类没有委托。";状态.text="";}
        else if(q.是悬赏)
        {
            int total=q.妖魔列表().Count, remaining=r?.剩余妖魔.Count ?? total;
            详情.text=q.说明+"\n\n悬赏妖群\n"+q.目标介绍+"\n\n妖魔等级   Lv"+TowerFloorTable库.取().取怪物等级(q.对应塔层)+
                "\n妖魔数量   "+total+" 只\n地点   宗门野外刷怪区\n\n清剿后回执事阁领取贡献。";
            状态.text=r==null ? "未接取 · 同时可接一项悬赏" : complete ? "清剿完成 · 可领取悬赏" : "清剿进度   "+(total-remaining)+" / "+total;
        }
        else {详情.text=q.说明+"\n\n提交需求\n"+q.目标介绍+"\n\n提交时扣除对应物品，再发放宗门贡献。";状态.text="背包持有   "+宗门任务.持有数(q)+" / "+q.数量+(r==null ? " · 未接取" : complete ? " · 可提交" : " · 收集中");}
        行动.interactable=q != null && (r == null ? !q.是悬赏 || 宗门任务.当前悬赏==null : complete);
        行动.GetComponentInChildren<Text>().text=r==null ? "接取委托" : complete ? (q.是悬赏 ? "领取悬赏" : "提交物品") : "委托进行中";
        追踪.interactable=放弃.interactable=r != null;
        追踪.GetComponentInChildren<Text>().text=宗门任务.当前追踪==q && r!=null ? "正在追踪" : "追踪委托";
    }
    void 执行()
    {
        if(选中==null)return; string message;
        if(宗门任务.记录(选中.id)==null) 宗门任务.接取(选中.id,out message); else 宗门任务.交付(选中.id,out message);
        提示.text=message; 刷详情();
    }
    void 建界面()
    {
        if(字体==null) 字体=Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f=>f.name.ToLowerInvariant().Contains("simhei"));
        var root=new GameObject("SectTaskHallCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        画布=root.GetComponent<Canvas>();画布.renderMode=RenderMode.ScreenSpaceOverlay;画布.sortingOrder=2600;
        var s=root.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.matchWidthOrHeight=.5f;
        var shade=图("背景压暗",root.transform,new Color(0,0,0,.5f));放(shade.rectTransform,0,0,1,1);shade.raycastTarget=true;
        var art=图("堂阁周边素材",root.transform,Color.white);art.sprite=Resources.Load<Sprite>("UI/SectExchange/teaching-hall");art.preserveAspect=true;放(art.rectTransform,0,0,1,1);
        面板=图("细线窗口",root.transform,new Color(.025f,.055f,.06f,.18f)).rectTransform;面板.anchorMin=面板.anchorMax=new Vector2(.5f,.5f);面板.sizeDelta=new Vector2(1460,850);框(面板);
        文("标题",面板,"执事阁",42,字,.035f,.89f,.36f,.965f);文("副标题",面板,"领宗门之托 · 积护道之功",19,淡,.035f,.845f,.6f,.89f);
        贡献=文("贡献",面板,"",22,金,.64f,.89f,.925f,.965f);
        var close=按钮("关闭",面板,"×",30);放(close.transform as RectTransform,.93f,.89f,.975f,.958f);close.onClick.AddListener(关面板);
        for(int i=0;i<分类名.Length;i++){int k=i;var b=按钮("分类_"+i,面板,分类名[i],23);放(b.transform as RectTransform,.035f,.72f-i*.09f,.185f,.79f-i*.09f);b.onClick.AddListener(()=>{分类=k;列任务();提示.text="";});分类按钮.Add(b);}
        文("须知",面板,"委托可重复接取\n悬赏同时限一项\n奖励在执事阁领取",18,淡,.04f,.23f,.18f,.43f);
        var scrollRoot=UIBuildUtils.CreateRect("委托滚动",面板);放(scrollRoot,.22f,.14f,.595f,.8f);
        var view=图("视口",scrollRoot,Color.clear);放(view.rectTransform,0,0,1,1);view.raycastTarget=true;view.gameObject.AddComponent<RectMask2D>();
        内容=UIBuildUtils.CreateRect("委托列表",view.transform);内容.anchorMin=new Vector2(0,1);内容.anchorMax=Vector2.one;内容.pivot=new Vector2(.5f,1);内容.sizeDelta=Vector2.zero;
        var layout=内容.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=14;layout.childControlHeight=false;layout.childControlWidth=true;layout.childForceExpandHeight=false;
        var fit=内容.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        滚动=scrollRoot.gameObject.AddComponent<ScrollRect>();滚动.viewport=view.rectTransform;滚动.content=内容;滚动.horizontal=false;滚动.movementType=ScrollRect.MovementType.Clamped;
        var detail=图("委托详情",面板,new Color(.045f,.065f,.06f,.8f)).rectTransform;放(detail,.63f,.14f,.965f,.8f);框(detail);
        图标=图("图标",detail,Color.white);图标.preserveAspect=true;放(图标.rectTransform,.4f,.79f,.6f,.95f);
        标题=文("任务名称",detail,"",25,字,.06f,.71f,.94f,.79f);标题.alignment=TextAnchor.MiddleCenter;
        var introRoot=UIBuildUtils.CreateRect("介绍滚动",detail);放(introRoot,.07f,.27f,.93f,.69f);
        var introView=图("介绍视口",introRoot,Color.clear);放(introView.rectTransform,0,0,1,1);introView.raycastTarget=true;introView.gameObject.AddComponent<RectMask2D>();
        详情=文("介绍",introView.transform,"",20,字,0,0,1,1);详情.alignment=TextAnchor.UpperLeft;详情.rectTransform.anchorMin=new Vector2(0,1);详情.rectTransform.anchorMax=Vector2.one;详情.rectTransform.pivot=new Vector2(.5f,1);
        var introFit=详情.gameObject.AddComponent<ContentSizeFitter>();introFit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var introScroll=introRoot.gameObject.AddComponent<ScrollRect>();introScroll.content=详情.rectTransform;introScroll.viewport=introView.rectTransform;introScroll.horizontal=false;introScroll.movementType=ScrollRect.MovementType.Clamped;
        状态=文("状态",detail,"",18,淡,.07f,.2f,.93f,.27f);奖励=文("奖励",detail,"",20,金,.07f,.135f,.93f,.2f);
        行动=按钮("接取或交付",detail,"接取委托",23);放(行动.transform as RectTransform,.07f,.04f,.93f,.125f);行动.onClick.AddListener(执行);
        追踪=按钮("追踪",面板,"追踪委托",19);放(追踪.transform as RectTransform,.22f,.06f,.395f,.12f);追踪.onClick.AddListener(()=>{if(选中!=null)宗门任务.追踪(选中.id);});
        放弃=按钮("放弃",面板,"放弃委托",19);放(放弃.transform as RectTransform,.42f,.06f,.595f,.12f);放弃.onClick.AddListener(()=>{if(选中!=null && 宗门任务.放弃(选中.id))提示.text="已放弃委托，物品未扣除";});
        提示=文("反馈",面板,"",19,金,.22f,.015f,.62f,.055f);文("关闭提示",面板,"ESC  返回",18,淡,.64f,.07f,.83f,.115f);
    }
    Button 按钮(string name,Transform parent,string label,int size)
    {
        var image=图(name,parent,new Color(.07f,.11f,.11f,.85f));image.raycastTarget=true;框(image.rectTransform);
        var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
        var text=文("文字",b.transform,label,size,字,.05f,.05f,.95f,.95f);text.alignment=TextAnchor.MiddleCenter;
        if(parent==内容){var l=b.gameObject.AddComponent<LayoutElement>();l.preferredHeight=116;image.rectTransform.sizeDelta=new Vector2(0,116);}
        return b;
    }
    static Image 图(string name,Transform parent,Color color)=>UIBuildUtils.CreateImage(name,parent,color);
    Text 文(string name,Transform parent,string value,int size,Color color,float x,float y,float X,float Y)
    {
        var text=UIBuildUtils.CreateText(name,parent,字体,value,size,TextAnchor.MiddleLeft,color);text.verticalOverflow=VerticalWrapMode.Truncate;放(text.rectTransform,x,y,X,Y);return text;
    }
    static void 放(RectTransform r,float x,float y,float X,float Y){r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(X,Y);r.offsetMin=r.offsetMax=Vector2.zero;}
    static void 框(RectTransform r){var t=UIBuildUtils.CreateRect("回纹细框",r);放(t,0,0,1,1);var g=t.gameObject.AddComponent<UISectFineFrame>();g.color=金;g.raycastTarget=false;}
}
