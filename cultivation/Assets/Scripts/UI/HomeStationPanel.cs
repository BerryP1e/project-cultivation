using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>基站任务与四格仓库面板，交易统一交给基站，沿用灵田的细框风格。</summary>
public sealed class HomeStationPanel : MonoBehaviour
{
    HomePuppetStation station;
    Font font;
    Text status,taskText;
    readonly Text[] slotTexts=new Text[4];
    RectTransform inventory;
    float nextRefresh;
    string inventoryKey="";
    public static GameObject 创建(HomePuppetStation target)
    {
        var root=new GameObject("HomeStationPanel",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1800;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var panel=root.AddComponent<HomeStationPanel>();panel.station=target;panel.Build();return root;
    }
    void Position(RectTransform rt,float x,float y,float w,float h)
    {
        rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(w,h);
    }
    Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size=24)
    {
        var t=UIBuildUtils.CreateText(name,parent,font,value,size,TextAnchor.MiddleLeft,SpiritFieldPanelStyle.TextColor);Position(t.rectTransform,x,y,w,h);return t;
    }
    Button Button(Transform parent,string name,string value,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
    {
        var b=UIBuildUtils.CreateButton(name,parent,font,value,23);Position(b.GetComponent<RectTransform>(),x,y,w,h);SpiritFieldPanelStyle.Button(b);b.onClick.AddListener(action);return b;
    }
    void Build()
    {
        font=Resources.Load<Font>("字体/SourceHanSerifSC-Regular");
        if(!font)font=FindObjectsOfType<Text>(true).Select(t=>t.font).FirstOrDefault(f=>f);
        if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var shade=UIBuildUtils.CreateImage("遮罩",transform,new Color(0,0,0,.5f));UIBuildUtils.Stretch(shade.rectTransform);shade.raycastTarget=true;
        var background=UIBuildUtils.CreateImage("基站窗口",transform,new Color(.055f,.085f,.085f,.95f));Position(background.rectTransform,0,0,1060,750);background.raycastTarget=true;SpiritFieldPanelStyle.Frame(background.transform);
        Label(background.transform,"标题","傀儡基站",-400,315,190,55,32);
        Button(background.transform,"关闭","×",460,315,65,55,()=>{var interactor=FindObjectOfType<StationInteractor>();if(interactor)interactor.关闭界面();else Destroy(gameObject);});
        var task=Button(background.transform,"种田任务","",-245,235,410,62,()=>{station.设置任务(!station.状态.种田任务);Refresh();});taskText=task.GetComponentInChildren<Text>();
        Button(background.transform,"重新尝试","重新调度",255,235,400,62,()=>{station.重新尝试();Refresh();});
        status=Label(background.transform,"工作状态","",0,152,925,90);status.horizontalOverflow=HorizontalWrapMode.Wrap;
        Label(background.transform,"仓库标题","基站仓库 · 四格",-258,72,440,42,25);
        Label(background.transform,"背包标题","背包物品",258,72,440,42,25);
        for(int i=0;i<4;i++)
        {
            int slot=i;var b=Button(background.transform,"仓库格"+i,"",-255,-5-i*86,430,70,()=>{station.取回背包物品(slot);Refresh();});slotTexts[i]=b.GetComponentInChildren<Text>();
        }
        var scrollRoot=UIBuildUtils.CreateRect("背包滚动列表",background.transform);Position(scrollRoot,255,-115,440,345);
        var viewport=UIBuildUtils.CreateImage("视口",scrollRoot,new Color(0,0,0,.05f));UIBuildUtils.Stretch(viewport.rectTransform);viewport.gameObject.AddComponent<RectMask2D>();viewport.raycastTarget=true;
        inventory=UIBuildUtils.CreateRect("物品内容",viewport.transform);inventory.anchorMin=new Vector2(0,1);inventory.anchorMax=new Vector2(1,1);inventory.pivot=new Vector2(.5f,1);inventory.anchoredPosition=Vector2.zero;
        var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport.rectTransform;scroll.content=inventory;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        UIBuildUtils.AddVerticalScrollbar(scroll,12,0,4);
        Label(background.transform,"仓库规则","种子从基站取出，收获存回基站",0,-325,700,34,20);
        Refresh();
    }
    void Update()
    {
        if(!station){Destroy(gameObject);return;}
        if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.25f;Refresh();
    }
    void Refresh()
    {
        if(!station)return;station.状态.确保();
        taskText.text=station.状态.种田任务?"种田任务：已启用":"种田任务：未启用";
        status.text=string.IsNullOrEmpty(station.错误)?"傀儡 · "+station.当前作业:station.错误;status.color=string.IsNullOrEmpty(station.错误)?SpiritFieldPanelStyle.TextColor:new Color(1,.4f,.3f);
        for(int i=0;i<4;i++)
        {
            var s=station.状态.仓库[i];slotTexts[i].text=string.IsNullOrEmpty(s.物品id)?"空格":灵田.取物品名(s.物品id)+"  ×"+s.数量+(station.状态.携带种子==s.物品id?" · 种子已带出":"")+"　取回";
        }
        var panel=FindObjectOfType<UIPanelData>();if(!panel)return;panel.EnsureLists();
        var items=panel.物品.Where(it=>it).Distinct().ToArray();
        string key=string.Join("|",items.Select(it=>it.物品id+":"+panel.物品数量(it)));
        if(key==inventoryKey)return;inventoryKey=key;
        foreach(Transform t in inventory){t.gameObject.SetActive(false);Destroy(t.gameObject);}
        inventory.sizeDelta=new Vector2(-18,Mathf.Max(345,items.Length*70));
        for(int i=0;i<items.Length;i++)
        {
            var item=items[i];int count=panel.物品数量(item);
            var b=Button(inventory,"存入_"+item.物品id,item.物品名+" ×"+count+"　存入",0,0,415,60,()=>{station.存入背包物品(item,Input.GetKey(KeyCode.LeftShift)?panel.物品数量(item):1);Refresh();});
            var rt=b.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-i*70);
        }
    }
}
