using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>浮世万象装备后的编排窗口，拥有自己的暂停与输入锁。</summary>
public sealed class UIInkSpiritArrayPage : MonoBehaviour
{
    UIPanelData data;
    SpiritArrayDefinition definition;
    SpiritArrayProgram draft;
    SpiritArrayNode selected;
    readonly List<SpiritArrayDefinition> definitions = new List<SpiritArrayDefinition>();
    readonly List<Button> programTabs=new List<Button>();
    RectTransform rows, details;
    Font font;
    Text status, summary;
    InputField nameField;
    bool dirty, ready;
    GameObject window;
    float previousTimeScale;
    bool opened;
    readonly Dictionary<CanvasGroup,Vector3> hiddenHud=new Dictionary<CanvasGroup,Vector3>();
    public bool IsOpen=>opened;
    public SpiritArrayProgram Draft => draft;
    public static bool Open(UIPanelData source)
    {
        if(!SpiritArrayTalisman.Equipped(source)||UiEscRegistry.SceneInputBlocked)return false;
        UIInkSpiritArrayPage view=null;
        foreach(var existing in FindObjectsOfType<UIInkSpiritArrayPage>(true))if(existing.data==source){view=existing;break;}
        if(!view){
            var window=new GameObject("浮世万象编排窗口",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));window.SetActive(false);
            var canvas=window.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=2460;
            var scaler=window.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var dim=UIBuildUtils.CreateImage("ArrayDim",window.transform,new Color(0,.02f,.01f,.55f));UIBuildUtils.Stretch(dim.rectTransform);dim.raycastTarget=true;
            var rt=UIBuildUtils.CreateRect("SpiritArrayEditor",window.transform);Place(rt,.05f,.065f,.95f,.94f);
            view=rt.gameObject.AddComponent<UIInkSpiritArrayPage>();view.data=source;view.window=window;view.font=Resources.Load<Font>("Fonts/SimHei")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        view.previousTimeScale=Time.timeScale;view.opened=true;view.window.SetActive(true);view.LoadEditor();
        UiEscRegistry.SetSceneInputBlocked(view,true);
        foreach(var canvas in FindObjectsOfType<Canvas>(true))if(canvas.name=="HudCanvas"||canvas.name=="QuestGuideCanvas"||canvas.name=="ChronicleCanvas"){
            var group=canvas.GetComponent<CanvasGroup>();if(!group)group=canvas.gameObject.AddComponent<CanvasGroup>();view.hiddenHud[group]=new Vector3(group.alpha,group.interactable?1:0,group.blocksRaycasts?1:0);group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
        }
        Time.timeScale=0;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;return true;
    }
    void LoadEditor()
    {
        if(!data)data = FindObjectOfType<UIPanelData>();
        if (!data) return;
        data.EnsureLists(); definitions.Clear();
        foreach (var d in data.灵阵) if (d && d.默认编排 != null) definitions.Add(d);
        definitions.Sort((a,b)=>string.CompareOrdinal(a.灵阵id,b.灵阵id));
        if (!ready) Build();
        if (definitions.Count > 0) SelectProgram(Mathf.Max(0,definitions.FindIndex(d=>d.灵阵id==data.当前灵阵编排)));
    }
    void Build()
    {
        ready = true;
        var parentCanvas=transform.parent.GetComponentInParent<Canvas>(true);
        var canvas=gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=(parentCanvas?parentCanvas.sortingOrder:2450)+1;
        gameObject.AddComponent<GraphicRaycaster>();
        var bg = gameObject.AddComponent<Image>(); bg.color = new Color(.10f,.15f,.14f,1); bg.raycastTarget = true;
        Label(transform, "浮世万象 · 灵阵编排", .025f,.935f,.55f,.99f, 30);
        Button(transform, "关闭 · ESC", .83f,.94f,.98f,.99f, Close);
        for (int i = 0; i < 6; i++) { int index = i;
            programTabs.Add(Button(transform, "编排 " + (i+1), .025f+i*.13f,.867f,.145f+i*.13f,.922f, () => SelectProgram(index)));
        }
        nameField = Input(transform, "阵名", .025f,.80f,.39f,.852f, "", text => { if (draft != null) { draft.名称 = text; Changed(false); } });
        Button(transform, "保存编排", .40f,.80f,.54f,.852f, () => { if (SaveDraft()) SetStatus("已保存编排；游戏存档会保存这些设置"); });
        Button(transform, "在主序列末尾添加", .55f,.80f,.78f,.852f, () => { selected = null; Refresh(); });
        Button(transform, "恢复此槽默认", .79f,.80f,.98f,.852f, () => { draft = definition.默认编排.Copy(); selected = null; Changed(); });
        Label(transform,"节点库 · 点击添加",.025f,.742f,.235f,.79f,21);
        int libraryIndex=0;
        foreach (SpiritArrayOp op in SpiritArrayRules.可用节点) {
            int index = libraryIndex++;
            Button(transform,SpiritArrayRules.Name(op),.025f,.692f-index*.048f,.235f,.735f-index*.048f, () => Add(op));
        }
        Label(transform,$"前摇 {SpiritArrayRules.前摇:0.##} 秒\n间距、起点距离在节点页调整。\n逆向打包仅向后展开，不反转子弹。",.025f,.13f,.235f,.20f,15);
        rows = Scroll(transform, .25f,.21f,.635f,.79f);
        details = Scroll(transform,.65f,.21f,.98f,.79f);details.name="NodeParameters";
        summary = Label(transform,"",.25f,.135f,.98f,.20f,17);
        status = Label(transform,"",.025f,.075f,.98f,.13f,17);
        Button(transform,"设为当前施放阵列 · E 释放",.45f,.012f,.98f,.068f,UseCurrent);
    }
    public void SelectProgram(int index)
    {
        if (index < 0 || index >= definitions.Count || !SaveDraft()) return;
        definition = definitions[index]; draft = SpiritArrayRules.Get(data, definition).Copy();
        draft.id = definition.灵阵id; selected = null; dirty = false;
        nameField.SetTextWithoutNotify(draft.名称); Refresh(); SetStatus("选中「"+draft.名称+"」；修改后保存或装备");
    }
    public void Add(SpiritArrayOp op)
    {
        if (draft == null) return;
        int Count(List<SpiritArrayNode> nodes){int count=nodes.Count;foreach(var node in nodes)count+=Count(node.子阵);return count;}
        if(Count(draft.序列)>=SpiritArrayRules.最大节点){SetStatus("每套编排最多 64 个节点");return;}
        var target = draft.序列; int at = target.Count;
        if (selected != null) {
            if (SpiritArrayRules.Container(selected.操作)) { target = selected.子阵; at = target.Count; }
            else { target = Parent(draft.序列, selected); at = target.IndexOf(selected)+1; }
        }
        var node = SpiritArrayNode.Create(op); target.Insert(at,node); selected = node; Changed();
    }
    static List<SpiritArrayNode> Parent(List<SpiritArrayNode> nodes, SpiritArrayNode node)
    {
        if(nodes==null)return null;
        if (nodes.Contains(node)) return nodes;
        foreach (var n in nodes) { var found = Parent(n.子阵,node); if (found != null) return found; }
        return null;
    }
    void Changed(bool refresh = true) { dirty = true; if (refresh) Refresh(); else UpdateSummary(); }
    void Refresh()
    {
        Clear(rows); Clear(details);
        if (draft == null) return;
        int count = 0;
        void Draw(List<SpiritArrayNode> nodes,int depth) {
            if(nodes==null)return;
            foreach (var n in nodes) {
                var row = UIBuildUtils.CreateButton("ArrayNode",rows,font,new string('　',depth)+SpiritArrayRules.Name(n.操作)+(SpiritArrayRules.Container(n.操作)?"  ▸":""),18);
                row.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
                row.image.color = n == selected ? new Color(.35f,.55f,.46f) : new Color(.20f,.28f,.25f);
                row.GetComponentInChildren<Text>().color = Color.white;
                row.onClick.AddListener(() => { selected = n; Refresh(); }); count++;
                Draw(n.子阵,depth+1);
            }
        }
        Draw(draft.序列,0);
        if (selected == null) DetailLabel("主序列\n\n从左侧选择节点添加。\n先放「生成灵弹」，后面接穿阵修饰，或使用多重与打包组织序列。",21,180);
        else {
            var n = selected;
            DetailLabel(SpiritArrayRules.Name(n.操作),25,45);
            DetailLabel(SpiritArrayRules.Describe(n.操作),18,100);
            if(n.操作 == SpiritArrayOp.生成灵弹 || n.操作 == SpiritArrayOp.强化 || n.操作 == SpiritArrayOp.加速 || n.操作 == SpiritArrayOp.分裂 || n.操作 == SpiritArrayOp.追踪) {
                string label = n.操作 == SpiritArrayOp.分裂 ? "数量（2～5）" : n.操作 == SpiritArrayOp.追踪 ? "转向速度 °/秒" : "倍率";
                DetailNumber(label,n.参数,v => n.参数=v);
            }
            if(n.操作 == SpiritArrayOp.生成灵弹) {
                DetailNumber("启动延迟（0～5 秒）",n.延迟,v=>n.延迟=v);
                DetailButton(n.反向?"发射方向：反向":"发射方向：正向",()=>{n.反向=!n.反向;Changed();});
            }
            if(n.操作 == SpiritArrayOp.多重施法 || n.操作 == SpiritArrayOp.反向多重 || n.操作 == SpiritArrayOp.分裂)
                DetailNumber("外倾角（±75°）",n.外倾角,v=>n.外倾角=v);
            string spacing=SpiritArrayRules.Pack(n.操作)?"入口到首个子阵间距（0～20 米）":"到下一个节点间距（0～20 米）";
            DetailNumber(spacing,SpiritArrayRules.Step(n),v=>{n.节点间距=v;n.自定义间距=true;},"NextSpacing");
            if(n.操作==SpiritArrayOp.身前布阵||n.操作==SpiritArrayOp.目标头顶)
                DetailNumber(n.操作==SpiritArrayOp.身前布阵?"距角色距离（0～50 米）":"距锁定目标距离 · 向上（0～50 米）",SpiritArrayRules.OriginDistance(n),v=>{n.起点距离=v;n.自定义起点距离=true;},"OriginDistance");
            var actions=UIBuildUtils.CreateRect("NodeActions",details);actions.gameObject.AddComponent<LayoutElement>().preferredHeight=48;
            Button(actions,"上移",0,0,.28f,1,()=>Move(-1));
            Button(actions,"下移",.31f,0,.59f,1,()=>Move(1));
            Button(actions,"删除节点",.62f,0,1,1,()=>{Parent(draft.序列,n).Remove(n);selected=null;Changed();});
        }
        UpdateSummary();
        for(int i=0;i<programTabs.Count;i++){
            var tab=programTabs[i];tab.image.color=i<definitions.Count&&definitions[i]==definition?new Color(.40f,.54f,.45f):new Color(.23f,.34f,.29f);
            tab.GetComponentInChildren<Text>().text="编排 "+(i+1)+(i<definitions.Count&&definitions[i].灵阵id==data.当前灵阵编排?" · 当前":"");
        }
    }
    void Move(int direction)
    {
        var list = Parent(draft.序列,selected); int a=list.IndexOf(selected),b=a+direction;
        if(b<0 || b>=list.Count)return; var other=list[b];list[b]=selected;list[a]=other;Changed();
    }
    void UpdateSummary()
    {
        if(!summary || draft==null)return;
        string validity=SpiritArrayRules.Validate(draft,out string error)?"可施放":"待完善："+error;
        summary.text=(dirty?"未保存 · ":"")+validity+"\n起手灵力 "+SpiritArrayCaster.FirstCost(draft.序列).ToString("0.#")+"；修饰阵按实际穿阵数量另扣。";
    }
    public bool SaveDraft()
    {
        if(!dirty || draft==null)return true;
        if(!SpiritArrayRules.Validate(draft,out string error)){SetStatus(error);return false;}
        if(string.IsNullOrWhiteSpace(draft.名称))draft.名称="灵阵编排";
        draft.名称=draft.名称.Substring(0,Mathf.Min(24,draft.名称.Length));
        SpiritArrayRules.Store(data,draft);dirty=false;UpdateSummary();return true;
    }
    public void UseCurrent()
    {
        if(!SaveDraft() || !definition)return;
        data.当前灵阵编排=definition.灵阵id;data.RaiseChanged();Refresh();SetStatus("当前阵列「"+draft.名称+"」；关闭后按 E 施放");
    }
    void Update(){if(opened&&(!SpiritArrayTalisman.Equipped(data)||UnityEngine.Input.GetKeyDown(KeyCode.Escape)))Close();}
    public void Close(){
        if(!opened)return;
        SaveDraft();opened=false;window.SetActive(false);UiEscRegistry.SetSceneInputBlocked(this,false);Time.timeScale=previousTimeScale;
        foreach(var pair in hiddenHud)if(pair.Key){pair.Key.alpha=pair.Value.x;pair.Key.interactable=pair.Value.y>0;pair.Key.blocksRaycasts=pair.Value.z>0;}hiddenHud.Clear();UiEscRegistry.NotifyClosed();
    }
    void SetStatus(string message) { if(status)status.text=message; }
    void OnDisable()
    {
        if(dirty && !SaveDraft())data?.ShowHint("灵阵编排不完整，未覆盖上次保存的方案");
        if(opened)Close();
    }
    void DetailLabel(string text,int size,float height)
    {
        var label=UIBuildUtils.CreateText("DetailLabel",details,font,text,size,TextAnchor.UpperLeft,new Color(.9f,.92f,.84f));label.raycastTarget=false;label.supportRichText=false;label.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
    }
    void DetailButton(string text,Action action){var button=Button(details,text,0,0,1,1,action);button.gameObject.AddComponent<LayoutElement>().preferredHeight=44;}
    void DetailNumber(string title,float value,Action<float> assign,string id="Parameter")
    {
        var holder=UIBuildUtils.CreateRect("ParameterRow",details);holder.gameObject.AddComponent<LayoutElement>().preferredHeight=82;
        Label(holder,title,0,.65f,1,1,17);
        Input(holder,id,0,0,1,.62f,value.ToString("0.##",CultureInfo.InvariantCulture),text=>{
            if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float parsed) && !float.IsNaN(parsed) && !float.IsInfinity(parsed)){assign(parsed);Changed(false);}
            else SetStatus("请输入有效数字");
        });
    }
    InputField Input(Transform parent,string id,float x,float y,float right,float top,string initial,Action<string> change)
    {
        var rt=UIBuildUtils.CreateRect(id,parent);Place(rt,x,y,right,top);var image=rt.gameObject.AddComponent<Image>();image.color=new Color(.86f,.88f,.81f);image.raycastTarget=true;
        var input=rt.gameObject.AddComponent<InputField>();input.targetGraphic=image;input.textComponent=UIBuildUtils.CreateText("Value",rt,font,initial,19,TextAnchor.MiddleLeft,new Color(.1f,.15f,.13f));
        UIBuildUtils.Stretch(input.textComponent.rectTransform,8);input.textComponent.supportRichText=false;input.text=initial;input.characterLimit=32;
        input.onEndEdit.AddListener(text=>change(text));return input;
    }
    Text Label(Transform parent,string text,float x,float y,float right,float top,int size)
    {
        var label=UIBuildUtils.CreateText("Label",parent,font,text,size,TextAnchor.UpperLeft,new Color(.9f,.92f,.84f));
        Place(label.transform,x,y,right,top);label.raycastTarget=false;label.supportRichText=false;return label;
    }
    Button Button(Transform parent,string text,float x,float y,float right,float top,Action click)
    {
        var button=UIBuildUtils.CreateButton("Action",parent,font,text,18);Place(button.transform,x,y,right,top);
        button.image.color=new Color(.23f,.34f,.29f);button.image.raycastTarget=true;
        button.GetComponentInChildren<Text>().color=new Color(.94f,.94f,.86f);
        button.onClick.AddListener(()=>click());
        return button;
    }
    RectTransform Scroll(Transform parent,float x,float y,float right,float top)
    {
        var viewport=UIBuildUtils.CreateRect("NodeScroll",parent);Place(viewport,x,y,right,top);
        viewport.gameObject.AddComponent<Image>().color=new Color(.07f,.11f,.10f,.6f);viewport.gameObject.AddComponent<RectMask2D>();
        var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;
        var content=UIBuildUtils.CreateRect("Nodes",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=6;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childControlWidth=true;layout.childForceExpandWidth=true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=content;return content;
    }
    static void Place(Transform transform,float x,float y,float right,float top)
    {
        var rt=(RectTransform)transform;rt.anchorMin=new Vector2(x,y);rt.anchorMax=new Vector2(right,top);rt.offsetMin=rt.offsetMax=Vector2.zero;
    }
    static void Clear(Transform parent) {foreach(Transform child in parent){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
}
