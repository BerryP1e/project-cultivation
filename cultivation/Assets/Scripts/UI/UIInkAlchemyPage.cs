using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>丹房：竹筒丹方、周围散落的材料、可开盖投料的丹炉及一次结算的炼制演出。</summary>
public class UIInkAlchemyPage : MonoBehaviour
{
    public bool 已打开 => root!=null && root.gameObject.activeSelf;
    public bool 正在炼制 { get; private set; }
    public string 选中丹方 => selected!=null ? selected.id : "";
    public float 炉盖开度 {get;private set;}
    public Dictionary<string,int> 已投材料 => slots.Where(s=>s.item!=null && s.count>0).GroupBy(s=>s.item.物品id).ToDictionary(g=>g.Key,g=>g.Sum(s=>s.count));
    class Slot {public ItemDefinition item;public int count;public Text label;public Image icon;}
    class Material {public ItemDefinition item;public RectTransform rect;public Text count;}
    Font font;
    RectTransform root,stage,lid,mouth,scrollReveal,scrollCanvas;
    Image dragIcon,resultIcon;
    Image bambooTube,bambooCap;
    Text recipeText,recipeTitle,hint,mana,startText,resultText;
    CanvasGroup scrollWords;
    UIInkAlchemyFX fx;
    Button start;
    ScrollRect leftMaterials,rightMaterials,recipes,description;
    readonly List<Slot> slots=new List<Slot>();
    readonly List<Material> materials=new List<Material>();
    readonly Dictionary<string,Button> recipeButtons=new Dictionary<string,Button>();
    readonly Dictionary<Canvas,bool> hudStates=new Dictionary<Canvas,bool>();
    灵丹定义 selected;
    ItemDefinition dragging;
    Vector2 dragOrigin,lidBase;
    float lidTarget,refreshAt,scrollAt=-100;
    string inventoryKey="",recipeKey="";
    bool animatingDrop,heating;
    static readonly Color Light=new Color(.95f,.95f,.87f);
    static readonly Color Muted=new Color(.70f,.79f,.72f);

    public void 初始化(Font textFont)
    {
        font=textFont;
        var canvas=new GameObject("AlchemyCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvas.transform.SetParent(transform,false);
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder=2650;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var dim=UIBuildUtils.CreateImage("AlchemyDim",canvas.transform,new Color(.01f,.025f,.02f,.65f));
        UIBuildUtils.Stretch(dim.rectTransform);dim.raycastTarget=true;root=dim.rectTransform;
        Label("Title",root,"丹房",36,TextAnchor.MiddleLeft,.075f,.91f,.3f,.97f);
        mana=Label("Mana",root,"",21,TextAnchor.MiddleRight,.58f,.91f,.89f,.97f);
        var close=Action("Close",root,"关闭",.90f,.92f,.975f,.97f);close.onClick.AddListener(请求关闭);

        // 展开宽度的遮罩裁剪完整竹简，不把竹简整幅横向拉扁。
        scrollReveal=UIBuildUtils.CreateRect("RecipeUnrollMask",root);
        scrollReveal.anchorMin=scrollReveal.anchorMax=new Vector2(.055f,.60f);
        scrollReveal.pivot=new Vector2(0,0);scrollReveal.sizeDelta=new Vector2(815,310);
        scrollReveal.gameObject.AddComponent<RectMask2D>();
        scrollCanvas=UIBuildUtils.CreateRect("RecipeScroll",scrollReveal);
        scrollCanvas.anchorMin=scrollCanvas.anchorMax=Vector2.zero;scrollCanvas.pivot=Vector2.zero;
        scrollCanvas.sizeDelta=new Vector2(815,310);
        var paper=UIBuildUtils.CreateImage("ScrollArtwork",scrollCanvas,Color.white);UIBuildUtils.Stretch(paper.rectTransform);
        paper.sprite=Asset("recipe-bamboo");paper.raycastTarget=false;
        recipeTitle=Label("RecipeTitle",scrollCanvas,"丹方",28,TextAnchor.MiddleLeft,.10f,.70f,.87f,.85f);
        recipeTitle.color=InkUITheme.Ink;
        description=Scroll("RecipeDescription",scrollCanvas,.10f,.15f,.87f,.69f);
        recipeText=Label("RecipeRequirements",description.content,"选中丹方，查看所需药材。",21,TextAnchor.UpperLeft,0,0,1,1);
        recipeText.color=InkUITheme.Ink;
        var textLayout=description.content.gameObject.AddComponent<VerticalLayoutGroup>();
        textLayout.childControlHeight=true;textLayout.childControlWidth=true;textLayout.childForceExpandHeight=false;
        description.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        scrollWords=scrollCanvas.gameObject.AddComponent<CanvasGroup>();
        scrollAt=Time.unscaledTime;
        bambooTube=UIBuildUtils.CreateImage("OpeningRecipeTube",root,Color.white);
        bambooTube.sprite=Asset("recipe-bamboo-tube-open");bambooTube.preserveAspect=true;
        bambooTube.rectTransform.anchorMin=bambooTube.rectTransform.anchorMax=new Vector2(.07f,.75f);
        bambooTube.rectTransform.sizeDelta=new Vector2(180,225);bambooTube.enabled=false;
        bambooCap=UIBuildUtils.CreateImage("OpeningRecipeCap",bambooTube.transform,Color.white);
        bambooCap.sprite=Asset("recipe-bamboo-cap");bambooCap.preserveAspect=true;
        bambooCap.rectTransform.anchorMin=bambooCap.rectTransform.anchorMax=new Vector2(.64f,.74f);
        bambooCap.rectTransform.sizeDelta=new Vector2(75,62);bambooCap.enabled=false;

        Label("KnownRecipes",root,"已学丹方",27,TextAnchor.MiddleLeft,.55f,.85f,.88f,.9f);
        recipes=Scroll("Recipes",root,.55f,.60f,.91f,.85f);
        var grid=recipes.content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize=new Vector2(185,112);grid.spacing=new Vector2(20,18);grid.padding=new RectOffset(18,18,18,18);
        grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;
        recipes.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;

        stage=UIBuildUtils.CreateRect("CauldronStage",root);
        stage.anchorMin=stage.anchorMax=new Vector2(.5f,.34f);stage.sizeDelta=new Vector2(820,540);
        var shadow=UIBuildUtils.CreateImage("CauldronInkShadow",stage,new Color(.08f,.14f,.10f,.65f));
        shadow.sprite=InkUITheme.Load("Dynamic/fx-ink-blot");Place(shadow.rectTransform,.10f,-.025f,.9f,.19f);
        var body=UIBuildUtils.CreateImage("CauldronBody",stage,Color.white);body.sprite=Asset("cauldron-body");
        UIBuildUtils.Stretch(body.rectTransform);body.preserveAspect=true;
        mouth=UIBuildUtils.CreateRect("MouthDropTarget",stage);Place(mouth,.265f,.67f,.735f,.88f);
        var lidImage=UIBuildUtils.CreateImage("CauldronLid",stage,Color.white);lidImage.sprite=Asset("cauldron-lid");
        lid=lidImage.rectTransform;lid.anchorMin=lid.anchorMax=new Vector2(.5f,.765f);
        lid.sizeDelta=new Vector2(565,185);lidBase=lid.anchoredPosition;lidImage.preserveAspect=true;
        for(int i=0;i<5;i++)
        {
            int index=i;Vector2 p=i==0 ? new Vector2(.5f,.43f) : new Vector2(i<3 ? .32f : .68f,(i==1 || i==3) ? .48f:.29f);
            var slotRoot=UIBuildUtils.CreateRect("IngredientSlot_"+i,stage);slotRoot.anchorMin=slotRoot.anchorMax=p;
            slotRoot.sizeDelta=new Vector2(i==0?132:116,92);
            var ink=UIBuildUtils.CreateImage("Ink",slotRoot,new Color(.08f,.13f,.11f,.86f));ink.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-2");UIBuildUtils.Stretch(ink.rectTransform);
            var button=ink.gameObject.AddComponent<Button>();button.targetGraphic=ink;ink.raycastTarget=true;button.transition=Selectable.Transition.None;
            button.onClick.AddListener(()=>取回(index));
            var icon=UIBuildUtils.CreateImage("Icon",slotRoot,Color.white);Place(icon.rectTransform,.3f,.35f,.7f,.90f);icon.preserveAspect=true;
            var text=Label("SlotLabel",slotRoot,i==0?"主材":"辅材",17,TextAnchor.MiddleCenter,.02f,0,.98f,.4f);
            slots.Add(new Slot{label=text,icon=icon});
        }
        var fxRect=UIBuildUtils.CreateRect("AlchemyParticles",stage);UIBuildUtils.Stretch(fxRect);
        fx=fxRect.gameObject.AddComponent<UIInkAlchemyFX>();
        resultIcon=UIBuildUtils.CreateImage("ResultPill",stage,Color.white);
        resultIcon.rectTransform.anchorMin=resultIcon.rectTransform.anchorMax=new Vector2(.5f,.79f);
        resultIcon.rectTransform.sizeDelta=new Vector2(95,95);
        resultIcon.preserveAspect=true;resultIcon.enabled=false;
        resultText=Label("ResultLabel",stage,"",24,TextAnchor.MiddleCenter,.12f,.91f,.88f,1.1f);

        Label("MaterialHeading",root,"可用药材",24,TextAnchor.MiddleLeft,.065f,.535f,.25f,.59f);
        Label("DragHint",root,"拖入炉口 · 点击也可投料",17,TextAnchor.MiddleRight,.73f,.535f,.93f,.59f);
        leftMaterials=Scroll("LeftMaterials",root,.055f,.095f,.275f,.535f);
        rightMaterials=Scroll("RightMaterials",root,.725f,.095f,.945f,.535f);
        start=Action("Craft",root,"开炼",.435f,.055f,.565f,.115f);startText=start.GetComponentInChildren<Text>();start.onClick.AddListener(开炼);
        var clear=Action("Clear",root,"取回药材",.32f,.055f,.415f,.11f);clear.onClick.AddListener(清空投料);
        hint=Label("Status",root,"",20,TextAnchor.MiddleCenter,.285f,.005f,.715f,.055f);
        隐藏HUD();刷新内容();
    }

    public void 打开() { if(root!=null){root.gameObject.SetActive(true);隐藏HUD();刷新内容();} }
    public void 关闭()
    {
        StopAllCoroutines();animatingDrop=false;dragging=null;正在炼制=false;heating=false;
        if(dragIcon!=null)Destroy(dragIcon.gameObject);
        if(fx!=null){fx.热度=0;fx.开盖度=0;}
        lidTarget=0;炉盖开度=0;
        if(lid!=null){lid.anchoredPosition=lidBase;lid.localRotation=Quaternion.identity;}
        if(root!=null)root.gameObject.SetActive(false);
        foreach(var kv in hudStates)if(kv.Key!=null)kv.Key.enabled=kv.Value;
        hudStates.Clear();
    }
    void 隐藏HUD()
    {
        foreach(var canvas in FindObjectsOfType<Canvas>(true))
            if(canvas.name=="HudCanvas" || canvas.name=="QuestGuideCanvas" || canvas.name=="ChronicleCanvas")
            {if(!hudStates.ContainsKey(canvas))hudStates[canvas]=canvas.enabled;canvas.enabled=false;}
    }
    void 请求关闭()
    {
        UiEscRegistry.记录关闭();
        var interactor=FindObjectOfType<StationInteractor>();
        if(StationInteractor.有界面打开 && interactor!=null)interactor.关闭界面();
        else 关闭();
    }
    void OnDisable() { if(root!=null)关闭(); }
    void Update()
    {
        if(!已打开)return;
        float dt=Time.unscaledDeltaTime;
        float width=815*Mathf.SmoothStep(0,1,(Time.unscaledTime-scrollAt)/.48f);
        scrollReveal.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Max(8,width));
        scrollWords.alpha=Mathf.Clamp01((Time.unscaledTime-scrollAt-.12f)/.28f);
        float bambooAge=Time.unscaledTime-scrollAt;
        bool showingTube=selected!=null && bambooAge<.65f;
        bambooTube.enabled=showingTube;bambooCap.enabled=showingTube;
        if(showingTube)
        {
            float capLift=Mathf.SmoothStep(0,1,bambooAge/.32f);
            bambooCap.rectTransform.anchoredPosition=new Vector2(14*capLift,44*capLift);
            bambooCap.rectTransform.localRotation=Quaternion.Euler(0,0,-13*capLift);
            float alpha=1-Mathf.Clamp01((bambooAge-.35f)/.3f);
            bambooTube.color=new Color(1,1,1,alpha);bambooCap.color=bambooTube.color;
        }
        炉盖开度=Mathf.MoveTowards(炉盖开度,lidTarget,dt*5);
        float lift=Mathf.SmoothStep(0,1,炉盖开度);
        lid.anchoredPosition=lidBase+new Vector2(35*lift,100*lift);
        lid.localRotation=Quaternion.Euler(0,0,-9*lift);
        fx.开盖度=lift;
        fx.热度=heating?.72f+.18f*Mathf.Sin(Time.unscaledTime*4):0;
        if(heating && !UIInkMotion.减少动效)
        {
            float t=Time.unscaledTime;
            stage.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*7)*.28f);
        }
        else stage.localRotation=Quaternion.identity;
        if(Time.unscaledTime>=refreshAt && dragging==null && !animatingDrop && !正在炼制)
        {refreshAt=Time.unscaledTime+.4f;刷新内容();}
        if(!StationInteractor.有界面打开 && Input.GetKeyDown(KeyCode.Escape))请求关闭();
    }

    void 刷新内容()
    {
        var furnace=炼丹炉.取();var data=FindObjectOfType<UIPanelData>();
        var known=furnace.已学会的丹方();string key=string.Join("|",known.Select(d=>d.id));
        if(key!=recipeKey || (known.Count>0 && recipeButtons.Count==0))
        {
            recipeKey=key;foreach(Transform child in recipes.content){child.gameObject.SetActive(false);Destroy(child.gameObject);}recipeButtons.Clear();
            foreach(var d in known)
            {
                var button=Action("Recipe_"+d.id,recipes.content,d.名,0,0,1,1);
                button.gameObject.AddComponent<LayoutElement>();
                var title=button.GetComponentInChildren<Text>();Place(title.rectTransform,.05f,.02f,.95f,.35f);title.fontSize=19;
                var icon=UIBuildUtils.CreateImage("RecipeIcon",button.transform,Color.white);Place(icon.rectTransform,.29f,.36f,.71f,.98f);
                icon.sprite=Asset("recipe-bamboo-tube");icon.preserveAspect=true;
                button.onClick.AddListener(()=>选择丹方(d.id));recipeButtons[d.id]=button;
                foreach(var g in button.GetComponentsInChildren<Graphic>())g.gameObject.AddComponent<UIInkScrollFade>();
            }
        }
        var usable=new HashSet<string>(furnace.全部丹方().SelectMany(d=>furnace.全部材料(d)).Select(k=>k.Key));
        var items=data!=null ? data.物品.Where(d=>d!=null && usable.Contains(d.物品id)).GroupBy(d=>d.物品id).Select(g=>g.First()).OrderBy(d=>d.物品id).ToList() : new List<ItemDefinition>();
        string itemKey=string.Join("|",items.Select(d=>d.物品id));
        if(itemKey!=inventoryKey || (items.Count>0 && materials.Count==0))
        {
            inventoryKey=itemKey;materials.Clear();
            foreach(var s in new[]{leftMaterials,rightMaterials})foreach(Transform child in s.content){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            for(int i=0;i<items.Count;i++)AddMaterial(items[i],i);
            SizeMaterials(leftMaterials);SizeMaterials(rightMaterials);
        }
        foreach(var row in materials)
        {int count=data!=null?data.物品数量(row.item)-暂存数量(row.item.物品id):0;row.count.text="×"+Mathf.Max(0,count);row.rect.GetComponent<CanvasGroup>().alpha=count>0?1:.36f;}
        foreach(var kv in recipeButtons){bool can=furnace.能炼(furnace.取丹方(kv.Key),out var unused);kv.Value.interactable=!正在炼制;kv.Value.GetComponent<UIInkFluid>().选中(selected!=null && kv.Key==selected.id);kv.Value.GetComponentInChildren<Text>().color=can?Light:Muted;}
        for(int i=0;i<slots.Count;i++)
        {
            var slot=slots[i];slot.icon.enabled=slot.item!=null;slot.icon.sprite=slot.item!=null?Icon(slot.item):null;
            slot.label.text=slot.item!=null ? slot.item.DisplayName+" ×"+slot.count : i==0?"主材":"辅材";
        }
        var vitals=FindObjectOfType<PlayerVitals>();mana.text=vitals!=null ? $"灵气 {vitals.当前灵气:F0} / {vitals.灵气上限:F0}" : "";
        var match=匹配丹方();bool ready=match!=null && furnace.能炼(match,out var reason);
        start.interactable=!正在炼制 && !animatingDrop && ready;startText.text=正在炼制?"炼制中…":"开炼";
        if(!正在炼制 && string.IsNullOrEmpty(hint.text))hint.text=known.Count==0 ? "尚未学会丹方" : items.Count==0 ? "背包中暂无可炼丹的药材" : "";
    }

    void AddMaterial(ItemDefinition item,int index)
    {
        var scroll=index%2==0?leftMaterials:rightMaterials;int n=index/2;
        var rt=UIBuildUtils.CreateRect("Material_"+item.物品id,scroll.content);
        rt.anchorMin=rt.anchorMax=new Vector2(.5f,1);rt.sizeDelta=new Vector2(155,140);
        // 每两行偏移错落，列表仍留足图标与文字空间，更多材料自然向下延伸。
        rt.anchoredPosition=new Vector2(Mathf.Sin(index*2.17f)*80,-85-n*145);
        var ink=UIBuildUtils.CreateImage("MaterialInk",rt,new Color(.13f,.20f,.16f,.9f));ink.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-"+(index%4+1));UIBuildUtils.Stretch(ink.rectTransform);
        var hit=rt.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        rt.gameObject.AddComponent<CanvasGroup>();
        var icon=UIBuildUtils.CreateImage("MaterialIcon",rt,Color.white);icon.sprite=Icon(item);icon.preserveAspect=true;Place(icon.rectTransform,.15f,.32f,.85f,.94f);
        Label("Name",rt,item.DisplayName,20,TextAnchor.MiddleCenter,.02f,.07f,.98f,.31f);
        var count=Label("Count",rt,"",17,TextAnchor.MiddleRight,.61f,.73f,.95f,.97f);
        var drag=rt.gameObject.AddComponent<UIInkAlchemyDrag>();drag.视图=this;drag.材料=item;
        materials.Add(new Material{item=item,rect=rt,count=count});
        foreach(var g in rt.GetComponentsInChildren<Graphic>())g.gameObject.AddComponent<UIInkScrollFade>();
    }
    void SizeMaterials(ScrollRect scroll)
    {
        int count=materials.Count(m=>m.rect.parent==scroll.content);
        scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(scroll.viewport.rect.height,32+count*145));
    }
    public void 选择丹方(string id)
    {
        if(正在炼制 || animatingDrop || dragging!=null || !炼丹炉.已学会(id))return;
        selected=炼丹炉.取().取丹方(id);if(selected==null)return;
        清空投料();recipeTitle.text=selected.名+" · "+selected.品+"品";
        var sb=new StringBuilder();var furnace=炼丹炉.取();int index=0;
        foreach(var kv in furnace.全部材料(selected))
        {var d=QuestDatabase.取().找物品(kv.Key);sb.AppendLine((index++==0?"主材  ":"辅材  ")+(d!=null?d.DisplayName:kv.Key)+" ×"+kv.Value);}
        sb.AppendLine();sb.AppendLine($"成丹率 {furnace.实际成功率(selected):P0}   耗灵气 {furnace.实际耗气(selected)}");sb.AppendLine(selected.说明);
        recipeText.text=sb.ToString();scrollAt=Time.unscaledTime;
        LayoutRebuilder.ForceRebuildLayoutImmediate(description.content);description.verticalNormalizedPosition=1;刷新内容();
    }
    int 暂存数量(string id)=>slots.Where(s=>s.item!=null && s.item.物品id==id).Sum(s=>s.count);
    bool CanStage(ItemDefinition item,out int index)
    {
        index=-1;if(item==null || 正在炼制 || animatingDrop)return false;
        var data=FindObjectOfType<UIPanelData>();if(data==null || data.物品数量(item)<=暂存数量(item.物品id)){hint.text="这味药材已经全部投入炉中";return false;}
        if(selected!=null)
        {
            var requirements=炼丹炉.取().全部材料(selected);index=requirements.FindIndex(k=>k.Key==item.物品id);
            if(index<0 || index>=5){hint.text="此丹方不需要这味药材";return false;}
            if(暂存数量(item.物品id)>=requirements.Where(k=>k.Key==item.物品id).Sum(k=>k.Value)){hint.text="这味药材已足量";return false;}
        }
        else
        {index=slots.FindIndex(s=>s.item==item);if(index<0)index=slots.FindIndex(s=>s.item==null);}
        if(index<0){hint.text="炉中材料位置已满";return false;}return true;
    }
    public bool 开始拖动(ItemDefinition item,RectTransform origin,Vector2 screen)
    {
        if(dragging!=null || !CanStage(item,out var index))return false;
        dragging=item;dragOrigin=root.InverseTransformPoint(origin.position);
        CreateGhost(item);拖动(screen);return true;
    }
    void CreateGhost(ItemDefinition item)
    {
        dragIcon=UIBuildUtils.CreateImage("DraggedIngredient",root,Color.white);
        dragIcon.sprite=Icon(item);dragIcon.preserveAspect=true;dragIcon.raycastTarget=false;
        dragIcon.rectTransform.anchorMin=dragIcon.rectTransform.anchorMax=new Vector2(.5f,.5f);
        dragIcon.rectTransform.sizeDelta=new Vector2(105,105);
    }
    bool NearMouth(Vector2 screen,float margin)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(mouth,screen,null,out var point);
        var r=mouth.rect;return Mathf.Abs(point.x-r.center.x)<r.width*.5f+margin && Mathf.Abs(point.y-r.center.y)<r.height*.5f+margin;
    }
    public void 拖动(Vector2 screen)
    {
        if(dragging==null || dragIcon==null)return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root,screen,null,out var point);
        dragIcon.rectTransform.anchoredPosition=point;
        lidTarget=NearMouth(screen,65)?1:0;
    }
    public void 结束拖动(Vector2 screen)
    {
        if(dragging==null)return;
        var item=dragging;dragging=null;
        int slotIndex=-1;
        bool accepted=NearMouth(screen,0) && CanStage(item,out slotIndex);
        if(accepted){slots[slotIndex].item=item;slots[slotIndex].count++;hint.text="已投入 "+item.DisplayName;}
        else if(!NearMouth(screen,0))hint.text="未放入炉口，药材已归位";
        StartCoroutine(投料演出(accepted));
    }
    public void 点击投料(ItemDefinition item,RectTransform origin)
    {
        if(!CanStage(item,out var index) || dragging!=null)return;
        slots[index].item=item;slots[index].count++;CreateGhost(item);
        dragIcon.rectTransform.anchoredPosition=root.InverseTransformPoint(origin.position);dragOrigin=dragIcon.rectTransform.anchoredPosition;
        hint.text="已投入 "+item.DisplayName;StartCoroutine(投料演出(true));
    }
    IEnumerator 投料演出(bool accepted)
    {
        animatingDrop=true;lidTarget=accepted?1:0;start.interactable=false;
        Vector2 from=dragIcon.rectTransform.anchoredPosition;
        Vector2 end=accepted?(Vector2)root.InverseTransformPoint(mouth.position):dragOrigin;
        if(accepted)yield return new WaitForSecondsRealtime(.16f);
        float age=0;
        while(age<.34f && dragIcon!=null)
        {
            age+=Time.unscaledDeltaTime;float p=Mathf.Clamp01(age/.34f);float ease=1-Mathf.Pow(1-p,3);
            dragIcon.rectTransform.anchoredPosition=Vector2.Lerp(from,end,ease)+Vector2.up*Mathf.Sin(p*Mathf.PI)*45;
            dragIcon.rectTransform.localScale=Vector3.one*(accepted?Mathf.Lerp(1,.12f,p):1);
            var c=dragIcon.color;c.a=accepted?1-p:1;dragIcon.color=c;yield return null;
        }
        if(dragIcon!=null)Destroy(dragIcon.gameObject);dragIcon=null;
        if(accepted){fx.成功=true;fx.爆发时刻=Time.unscaledTime;}
        lidTarget=0;animatingDrop=false;刷新内容();
    }
    public void 清空投料()
    {
        if(正在炼制 || animatingDrop || dragging!=null)return;
        foreach(var s in slots){s.item=null;s.count=0;}hint.text="";resultIcon.enabled=false;resultText.text="";刷新内容();
    }
    void 取回(int index)
    {
        if(正在炼制 || animatingDrop || dragging!=null)return;
        var s=slots[index];if(s.count>0){s.count--;hint.text="取回 "+s.item.DisplayName;if(s.count==0)s.item=null;刷新内容();}
    }
    灵丹定义 匹配丹方()
    {
        var staged=已投材料;
        foreach(var d in 炼丹炉.取().已学会的丹方())
        {
            if(selected!=null && selected.id!=d.id)continue;
            var expected=炼丹炉.取().全部材料(d).GroupBy(k=>k.Key).ToDictionary(g=>g.Key,g=>g.Sum(k=>k.Value));
            if(staged.Count==expected.Count && expected.All(k=>staged.TryGetValue(k.Key,out int n)&&n==k.Value))return d;
        }
        return null;
    }
    public void 开炼()
    {
        if(正在炼制 || animatingDrop || dragging!=null)return;
        var recipe=匹配丹方();if(recipe==null){hint.text="投料还未与已学丹方相符";return;}
        if(!炼丹炉.取().能炼(recipe,out var reason)){hint.text=reason;return;}
        StartCoroutine(炼制演出(recipe));
    }
    IEnumerator 炼制演出(灵丹定义 recipe)
    {
        正在炼制=true;heating=true;lidTarget=0;resultIcon.enabled=false;resultText.text="";刷新内容();
        float age=0;
        while(age<3.2f){age+=Time.unscaledDeltaTime;hint.text="炉火温养  "+Mathf.Min(99,Mathf.FloorToInt(age/3.2f*100))+"%";yield return null;}
        // 整段演出只在这里调用一次业务结算；关闭/销毁会停止协程，未完成不扣材料。
        heating=false;
        var result=炼丹炉.取().炼制(recipe.id);
        fx.热度=0;fx.成功=result.成功;fx.爆发时刻=Time.unscaledTime;lidTarget=result.受理?1:0;
        if(result.受理)foreach(var s in slots){s.item=null;s.count=0;}
        hint.text=result.文本;
        if(result.成功)
        {
            var item=QuestDatabase.取().找物品(recipe.id);resultIcon.sprite=item!=null?Icon(item):null;
            resultIcon.enabled=resultIcon.sprite!=null;resultText.text=recipe.名+" ×"+result.数量;
            float t=0;while(t<.65f){t+=Time.unscaledDeltaTime;float p=Mathf.Clamp01(t/.65f);resultIcon.rectTransform.localScale=Vector3.one*Mathf.SmoothStep(.15f,1,p);resultIcon.rectTransform.anchoredPosition=new Vector2(0,45*Mathf.SmoothStep(0,1,p));yield return null;}
        }
        else resultText.text=result.受理?"炉火熄散":"材料不足";
        yield return new WaitForSecondsRealtime(.65f);
        lidTarget=0;正在炼制=false;刷新内容();
    }
    Sprite Icon(ItemDefinition item)
    { if(item.图标!=null)return item.图标;return Asset("material-"+item.物品id) ?? Asset("pill"); }
    static Sprite Asset(string name)=>Resources.Load<Sprite>("UI/InkUI/AlchemyInteractive/"+name);
    Text Label(string name,Transform parent,string value,int size,TextAnchor align,float x0,float y0,float x1,float y1)
    {
        var text=UIBuildUtils.CreateText(name,parent,font,value,size,align,Light);Place(text.rectTransform,x0,y0,x1,y1);
        text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        var shade=text.gameObject.AddComponent<Shadow>();shade.effectColor=new Color(0,0,0,.7f);shade.effectDistance=new Vector2(1,-1);return text;
    }
    Button Action(string name,Transform parent,string value,float x0,float y0,float x1,float y1)
    {
        var image=UIBuildUtils.CreateImage(name,parent,Color.clear);Place(image.rectTransform,x0,y0,x1,y1);
        var button=image.gameObject.AddComponent<Button>();button.targetGraphic=image;
        Label("Label",image.transform,value,23,TextAnchor.MiddleCenter,0,0,1,1);
        UIInkActionButton.Apply(button);button.GetComponent<UIInkActionButton>().设置绘制倍率(new Vector2(.91f,1.5f));
        var fluid=button.GetComponent<UIInkFluid>();fluid.位移强度=.10f;fluid.形变强度=.20f;return button;
    }
    ScrollRect Scroll(string name,Transform parent,float x0,float y0,float x1,float y1)
    {
        var rt=UIBuildUtils.CreateRect(name,parent);Place(rt,x0,y0,x1,y1);
        var scroll=rt.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
        var viewport=UIBuildUtils.CreateImage("Viewport",rt,Color.clear);UIBuildUtils.Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<RectMask2D>().softness=new Vector2Int(0,24);scroll.viewport=viewport.rectTransform;
        scroll.content=UIBuildUtils.CreateRect("Content",viewport.transform);scroll.content.anchorMin=new Vector2(0,1);scroll.content.anchorMax=Vector2.one;
        scroll.content.pivot=new Vector2(.5f,1);scroll.content.sizeDelta=Vector2.zero;
        InkUITheme.Scroll(scroll,0);return scroll;
    }
    static void Place(RectTransform rt,float x0,float y0,float x1,float y1)
    {UIBuildUtils.Place(rt,new Vector2(x0,y0),new Vector2(x1,y1),Vector2.zero,Vector2.zero);}
}
