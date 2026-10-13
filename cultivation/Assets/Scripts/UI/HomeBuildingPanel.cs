using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>三种家园建筑共享仓库交互。输入格选中后从背包放入，仓库条目取回；面板不自行改数量。</summary>
public sealed class HomeBuildingPanel : MonoBehaviour
{
    HomeBuilding building;
    Font font;
    RectTransform left, inventory, recipes;
    Text status, message, inventoryTitle;
    HomeBuildingStorage selected;
    int selectedIndex = -1;
    float nextRefresh;
    string listKey = "";
    public static GameObject 创建(HomeBuilding target)
    {
        var root = new GameObject("HomeBuildingPanel",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1800;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        if (!FindObjectOfType<EventSystem>()) new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
        var panel=root.AddComponent<HomeBuildingPanel>();panel.building=target;panel.Build();return root;
    }
    static void Place(RectTransform rt,float x,float y,float w,float h)
    { rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(w,h); }
    Text Label(Transform parent,string name,string value,float x,float y,float w,float h,int size=23)
    {
        var t=UIBuildUtils.CreateText(name,parent,font,value,size,TextAnchor.MiddleLeft,SpiritFieldPanelStyle.TextColor);Place(t.rectTransform,x,y,w,h);return t;
    }
    Button Btn(Transform parent,string name,string value,float x,float y,float w,float h,UnityEngine.Events.UnityAction action)
    {
        var b=UIBuildUtils.CreateButton(name,parent,font,value,21);Place(b.GetComponent<RectTransform>(),x,y,w,h);SpiritFieldPanelStyle.Button(b);b.onClick.AddListener(action);return b;
    }
    RectTransform Scroll(Transform parent,string name,float x,float y,float w,float h)
    {
        var rt=UIBuildUtils.CreateRect(name,parent);Place(rt,x,y,w,h);
        var view=UIBuildUtils.CreateImage("视口",rt,new Color(0,0,0,.04f));UIBuildUtils.Stretch(view.rectTransform);view.raycastTarget=true;view.gameObject.AddComponent<RectMask2D>();
        var content=UIBuildUtils.CreateRect("内容",view.transform);content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
        var scroll=rt.gameObject.AddComponent<ScrollRect>();scroll.viewport=view.rectTransform;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
        UIBuildUtils.AddVerticalScrollbar(scroll,12,0,4);return content;
    }
    void Build()
    {
        font=Resources.Load<Font>("字体/SourceHanSerifSC-Regular");if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var shade=UIBuildUtils.CreateImage("遮罩",transform,new Color(0,0,0,.5f));UIBuildUtils.Stretch(shade.rectTransform);shade.raycastTarget=true;
        var bg=UIBuildUtils.CreateImage("家园生产窗口",transform,new Color(.025f,.055f,.06f,.94f));Place(bg.rectTransform,0,0,1280,860);bg.raycastTarget=true;SpiritFieldPanelStyle.Frame(bg.transform);
        Label(bg.transform,"标题",building.状态.名,-440,365,320,50,32);
        Btn(bg.transform,"关闭","×",560,365,60,48,Close);
        Btn(bg.transform,"挪动建筑","挪动建筑",380,365,210,48,()=>{if(灵田摆放器.开始挪摆设(building.编号,out _))Close();});
        status=Label(bg.transform,"生产状态","",0,295,1160,48);
        message=Label(bg.transform,"事务提示","",0,-370,1160,50,20);
        var type=building.状态.类型id;
        left=Scroll(bg.transform,"建筑仓储",type=="auto_alchemy"?-395:-280,-32,type=="auto_alchemy"?400:610,580);
        inventoryTitle=Label(bg.transform,"背包标题","背包 · 可放入物品",type=="auto_alchemy"?400:340,220,type=="auto_alchemy"?390:420,42,25);
        inventory=Scroll(bg.transform,"背包",type=="auto_alchemy"?400:340,-32,type=="auto_alchemy"?390:420,440);
        Label(bg.transform,"背包数量操作","点击放入 1 份 · Shift 点击放入一组",type=="auto_alchemy"?400:340,-284,type=="auto_alchemy"?390:420,36,18);
        if(type=="auto_alchemy")
        {
            Label(bg.transform,"丹方标题","已学丹方",0,220,340,42,25);
            recipes=Scroll(bg.transform,"丹方列表",0,-20,340,430);
            Btn(bg.transform,"自动炼制","启停自动炼制",0,-290,320,48,()=>{building.状态.炼丹.自动炼制=!building.状态.炼丹.自动炼制;Invalidate();});
            selected=building.状态.炼丹.主材;selectedIndex=0;
        }
        else selected=type=="animal_pen"?building.状态.兽栏.食槽:building.状态.堆肥.原料;
        Refresh();
    }
    void Close() { var interactor=FindObjectOfType<StationInteractor>();if(interactor && interactor.当前界面==gameObject)interactor.关闭界面();else Destroy(gameObject); }
    void Update()
    {
        if(!building){Destroy(gameObject);return;}if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.3f;Refresh();
    }
    void Invalidate(){listKey="";Refresh();}
    static void Clear(RectTransform content)
    { foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);} }
    void Row(RectTransform content,string name,string value,ref int row,UnityEngine.Events.UnityAction action=null)
    {
        RectTransform rt;
        if(action!=null)rt=Btn(content,name,value,0,0,content.parent.GetComponent<RectTransform>().rect.width-28,55,action).GetComponent<RectTransform>();
        else rt=Label(content,name,value,0,0,content.parent.GetComponent<RectTransform>().rect.width-28,48,23).rectTransform;
        rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.pivot=new Vector2(.5f,1);rt.anchoredPosition=new Vector2(0,-row*65);row++;
        content.sizeDelta=new Vector2(-18,Mathf.Max(440,row*65));
    }
    void StorageRows(HomeBuildingStorage storage,string title,ref int row,bool input,UIPanelData bag)
    {
        Row(left,title,title,ref row);
        for(int i=0;i<storage.格.Count;i++)
        {
            int slot=i;var s=storage.格[i];string text=s.数量>0?灵田.取物品名(s.物品id)+" ×"+s.数量:"空格";
            string mark=selected==storage && (selectedIndex==slot || selectedIndex<0)?"◆ ":"";
            // 炼丹输入格：点击选中，单独的取回按钮防止填料时意外清空。
            if(input && building.状态.类型id=="auto_alchemy")
            {
                Row(left,title+slot,mark+(slot+1)+" · "+text+"　选中",ref row,()=>{selected=storage;selectedIndex=slot;Invalidate();});
                if(s.数量>0)Row(left,"取回"+title+slot,"取回 "+灵田.取物品名(s.物品id),ref row,()=>{building.取回(storage,slot,bag);Invalidate();});
            }
            else Row(left,title+slot,text+(s.数量>0?"　取回":""),ref row,s.数量>0?(UnityEngine.Events.UnityAction)(()=>{building.取回(storage,slot,bag);Invalidate();}):null);
        }
    }
    void Refresh()
    {
        var s=building.状态;if(s==null)return;s.确保建筑状态();
        var bag=FindObjectOfType<UIPanelData>();if(!bag)return;bag.EnsureLists();
        status.text=building.生产状态();message.text=building.提示;
        if(s.类型id=="auto_alchemy" && string.IsNullOrEmpty(message.text))message.text=s.炼丹.上次结果;
        var items=bag.物品.Where(i=>i).Distinct().Where(i=>building.允许存入(selected,i.物品id)||(s.类型id=="animal_pen"&&HomeAnimalCatalog.取().按幼崽(i.物品id)!=null)).ToArray();
        string key;
        // 不使用逐帧增长的小数作为列表键：动物饱食/生产进度仅在状态行更新，避免反复重建和打断点击。
        if(s.类型id=="animal_pen")key=string.Join("|",s.兽栏.动物.Select(a=>a.uid))+JsonUtility.ToJson(s.兽栏.食槽)+JsonUtility.ToJson(s.兽栏.收获篮)+string.Join("|",items.Select(i=>i.物品id+":"+bag.物品数量(i)));
        else if(s.类型id=="compost_bin")key=JsonUtility.ToJson(s.堆肥.原料)+JsonUtility.ToJson(s.堆肥.成品)+string.Join("|",items.Select(i=>i.物品id+":"+bag.物品数量(i)));
        else key=JsonUtility.ToJson(s.炼丹.主材)+JsonUtility.ToJson(s.炼丹.辅材)+JsonUtility.ToJson(s.炼丹.成品)+s.炼丹.丹方id+selectedIndex+(selected==s.炼丹.主材?"main":"aux")+s.炼丹.自动炼制+string.Join("|",items.Select(i=>i.物品id+":"+bag.物品数量(i)))+string.Join("|",灵丹库.全部.Select(d=>炼丹炉.已学会(d.id)));
        if(key==listKey)return;listKey=key;
        Clear(left);Clear(inventory);int row=0;
        if(s.类型id=="animal_pen")
        {
            Row(left,"动物标题","栏内动物",ref row);
            foreach(var a in s.兽栏.动物)
            {
                var animal=a;var d=HomeAnimalCatalog.取().找(a.物种id);if(d==null)continue;
                Row(left,a.uid,d.名+" · 占 "+d.容积+" · 喜爱"+灵田.取物品名(d.喜爱食物id)+"　取出",ref row,()=>{building.取出动物(animal.uid,bag);Invalidate();});
            }
            StorageRows(s.兽栏.食槽,"食槽 · 灵植",ref row,true,bag);StorageRows(s.兽栏.收获篮,"收获篮",ref row,false,bag);
        }
        else if(s.类型id=="compost_bin")
        { StorageRows(s.堆肥.原料,"粪便原料",ref row,true,bag);StorageRows(s.堆肥.成品,"肥料成品",ref row,false,bag);Row(left,"肥效","每次施肥推进成熟时间的 15%",ref row);Row(left,"间隔","每株植物间隔 0.75 天可再次施肥",ref row); }
        else
        {
            Row(left,"启停状态",s.炼丹.自动炼制?"◆ 自动炼制开启":"自动炼制暂停",ref row);
            StorageRows(s.炼丹.主材,"主材 · 一格",ref row,true,bag);StorageRows(s.炼丹.辅材,"辅材 · 三格",ref row,true,bag);StorageRows(s.炼丹.成品,"成品 · 一格",ref row,false,bag);
            inventoryTitle.text=selected==s.炼丹.主材?"背包 → 主材格":"背包 → 辅材格 "+(selectedIndex+1);
            Clear(recipes);int recipeRow=0;
            foreach(var d in 灵丹库.全部.Where(d=>炼丹炉.已学会(d.id)))
            {
                var recipe=d;Row(recipes,d.id,(s.炼丹.丹方id==d.id?"◆ ":"")+d.名+" · "+d.成功率.ToString("P0"),ref recipeRow,()=>{s.炼丹.丹方id=recipe.id;Invalidate();});
                Row(recipes,d.id+"材料",灵田.取物品名(d.主材id)+" ×"+d.主材数量,ref recipeRow);
                foreach(var k in 炼丹炉.解析材料(d.辅材))Row(recipes,d.id+k.Key,灵田.取物品名(k.Key)+" ×"+k.Value,ref recipeRow);
            }
            if(recipeRow==0)Row(recipes,"空丹方","尚未学会丹方",ref recipeRow);
        }
        int itemRow=0;
        foreach(var item in items)
        {
            var it=item;int n=bag.物品数量(it);
            Row(inventory,it.物品id,it.物品名+" ×"+n+"　放入",ref itemRow,()=>{
                if(s.类型id=="animal_pen" && HomeAnimalCatalog.取().按幼崽(it.物品id)!=null)building.放入动物(it,bag);
                else building.存入(selected,it,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?Mathf.Min(bag.物品数量(it),HomeBuildingStorage.堆叠上限):1,bag,selectedIndex);
                Invalidate();
            });
        }
        if(itemRow==0)Row(inventory,"空背包","没有可放入的物品",ref itemRow);
    }
}
