using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>绕仪轨下缘向右上展开的法则收藏带；按弧长滚动，只创建可见卡片。</summary>
public sealed class UIInkLawArc : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter
{
    public int 收藏数量 => entries.Count;
    public int 卡片池数 => cells.Count;
    public float 滚动位置 => offset;
    public float 最大滚动 => Mathf.Max(0, (entries.Count - 1) * Pitch - (length - 140));
    public Scrollbar 滚动条 { get; private set; }
    const float Pitch = 152;
    UIInkLawPage page; Font font; RectTransform area;
    readonly List<LawFragment> entries = new List<LawFragment>();
    readonly List<Cell> cells = new List<Cell>();
    readonly Vector2[] path = new Vector2[97];
    readonly float[] distances = new float[97];
    Vector2 lastSize; float length, offset, velocity; bool dragging;
    sealed class Cell { public Button button; public Image image; public Text label; public CanvasGroup group; public UIInkFluid fluid; public UIInkLawItemDrag drag; public LawFragment law; }
    public void 初始化(UIInkLawPage owner, Font textFont)
    {
        page = owner; font = textFont; area = (RectTransform)transform;
        var host = UIBuildUtils.CreateRect("ArcScrollbar", transform); UIBuildUtils.Place(host, new Vector2(.975f,.30f),new Vector2(.99f,.85f),Vector2.zero,Vector2.zero);
        var barImage = host.gameObject.AddComponent<Image>(); barImage.color = new Color(.5f,.6f,.56f,.2f);
        滚动条 = host.gameObject.AddComponent<Scrollbar>(); 滚动条.direction = Scrollbar.Direction.BottomToTop;
        var slide = UIBuildUtils.CreateRect("Sliding Area",host);UIBuildUtils.Stretch(slide,2);
        var handle = UIBuildUtils.CreateImage("Handle",slide,Color.white);UIBuildUtils.Stretch(handle.rectTransform);handle.raycastTarget=true;
        滚动条.handleRect=handle.rectTransform;滚动条.targetGraphic=handle;InkUITheme.ScrollbarStyle(滚动条);
        滚动条.onValueChanged.AddListener(value=> {offset=(1-value)*最大滚动;velocity=0;Layout();});
    }
    public void 设置(List<LawFragment> source) { entries.Clear();entries.AddRange(source);offset=Mathf.Clamp(offset,0,最大滚动);Layout(); }
    void BuildPath()
    {
        if(lastSize==area.rect.size&&length>0)return;lastSize=area.rect.size;length=0;
        for(int i=0;i<path.Length;i++) {float a=Mathf.Lerp(-110,-6,i/96f)*Mathf.Deg2Rad;path[i]=new Vector2((.30f+.61f*Mathf.Cos(a)-area.pivot.x)*lastSize.x,(.89f+.54f*Mathf.Sin(a)-area.pivot.y)*lastSize.y);if(i>0)length+=Vector2.Distance(path[i-1],path[i]);distances[i]=length;}
    }
    public Vector2 弧上位置(float distance)
    {
        BuildPath();distance=Mathf.Clamp(distance,0,length);
        for(int i=1;i<distances.Length;i++)if(distances[i]>=distance)return Vector2.Lerp(path[i-1],path[i],Mathf.InverseLerp(distances[i-1],distances[i],distance));return path[96];
    }
    public Vector2 切线(Vector2 screen, Camera camera)
    {
        BuildPath();RectTransformUtility.ScreenPointToLocalPointInRectangle(area,screen,camera,out var local);int closest=0;float best=float.PositiveInfinity;
        for(int i=0;i<path.Length;i++){float distance=(path[i]-local).sqrMagnitude;if(distance<best){best=distance;closest=i;}}
        return (path[Mathf.Min(96,closest+1)]-path[Mathf.Max(0,closest-1)]).normalized;
    }
    public bool IsRaycastLocationValid(Vector2 screen, Camera camera)
    {
        BuildPath();RectTransformUtility.ScreenPointToLocalPointInRectangle(area,screen,camera,out var local);
        if(local.x>area.rect.xMax-38&&local.y>area.rect.height*(-.2f)&&local.y<area.rect.height*.36f)return true;
        for(int i=0;i<path.Length;i++)if((path[i]-local).sqrMagnitude<100*100)return true;return false;
    }
    Cell NewCell()
    {
        var cell=new Cell();cell.button=UIBuildUtils.CreateButton("LawArcCard",transform,font,"",17);cell.button.transition=Selectable.Transition.None;cell.button.image.color=Color.clear;
        var rect=(RectTransform)cell.button.transform;rect.anchorMin=rect.anchorMax=area.pivot;rect.sizeDelta=new Vector2(130,136);cell.group=cell.button.gameObject.AddComponent<CanvasGroup>();
        var blot=UIBuildUtils.CreateImage("LawCardInk",rect,new Color(.38f,.42f,.39f));blot.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4");blot.preserveAspect=true;blot.rectTransform.sizeDelta=new Vector2(108,100);blot.rectTransform.anchoredPosition=new Vector2(0,13);
        cell.image=UIBuildUtils.CreateImage("Icon",rect,Color.white);cell.image.rectTransform.sizeDelta=new Vector2(83,83);cell.image.rectTransform.anchoredPosition=new Vector2(0,13);cell.image.preserveAspect=true;
        cell.label=cell.button.GetComponentInChildren<Text>();cell.label.rectTransform.anchorMin=cell.label.rectTransform.anchorMax=new Vector2(.5f,.5f);cell.label.rectTransform.sizeDelta=new Vector2(148,36);cell.label.rectTransform.anchoredPosition=new Vector2(0,-51);cell.label.color=new Color(.96f,.94f,.86f);
        var shadow=cell.label.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.85f);shadow.effectDistance=new Vector2(1,-1);
        cell.fluid=cell.button.gameObject.AddComponent<UIInkFluid>();cell.fluid.使用密度模拟=false;cell.fluid.基础浓度=2.2f;cell.fluid.初始化(blot,null,null,null,cells.Count);
        cell.drag=cell.button.gameObject.AddComponent<UIInkLawItemDrag>();cell.button.onClick.AddListener(()=>{if(cell.law!=null)page.选择(cell.law);});return cell;
    }
    void Layout()
    {
        if(!area)return;BuildPath();if(length<1)return;offset=Mathf.Clamp(offset,0,最大滚动);
        int count=entries.Count==0?0:Mathf.Min(entries.Count,Mathf.CeilToInt(length/Pitch)+2);
        while(cells.Count<count)cells.Add(NewCell());
        float margin=最大滚动<=0?(length-(entries.Count-1)*Pitch)*.5f:70;
        int first=最大滚动>0?Mathf.Max(0,Mathf.FloorToInt((offset-margin)/Pitch)):0;
        for(int i=0;i<cells.Count;i++)
        {
            var cell=cells[i];int index=first+i;float distance=margin+index*Pitch-offset;bool visible=i<count&&index<entries.Count&&distance>=-25&&distance<=length+25;
            cell.button.gameObject.SetActive(visible);if(!visible)continue;var law=entries[index];
            if(cell.law!=law){cell.law=law;cell.button.name="LawItem_"+law.id;cell.label.text=law.名称;UIInkLawArt.应用(cell.image,law);cell.drag.初始化(page,law,this);}
            ((RectTransform)cell.button.transform).anchoredPosition=弧上位置(distance);
            cell.group.alpha=Mathf.Clamp01((distance+25)/75)*Mathf.Clamp01((length+25-distance)/75);cell.group.blocksRaycasts=cell.group.alpha>.4f;cell.fluid.选中(page.当前选择==law);
        }
        滚动条.gameObject.SetActive(最大滚动>0);滚动条.size=Mathf.Clamp01(length/Mathf.Max(length,entries.Count*Pitch));滚动条.SetValueWithoutNotify(最大滚动>0?1-offset/最大滚动:1);
    }
    public void OnScroll(PointerEventData e){offset-=e.scrollDelta.y*90;velocity=-e.scrollDelta.y*180;Layout();}
    public void OnBeginDrag(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;dragging=true;velocity=0;}
    public void OnDrag(PointerEventData e){if(e.button!=PointerEventData.InputButton.Left)return;float delta=Vector2.Dot(e.delta,切线(e.position,e.pressEventCamera))/GetComponentInParent<Canvas>().scaleFactor;offset-=delta;velocity=-delta/Mathf.Max(.001f,Time.unscaledDeltaTime);Layout();}
    public void OnEndDrag(PointerEventData e){dragging=false;}
    void LateUpdate(){if(page.拖拽法则!=null)return;float dt=Mathf.Min(.1f,Time.unscaledDeltaTime);if(!dragging){offset+=velocity*dt;velocity*=Mathf.Exp(-dt*7);if(offset<=0&&velocity<0||offset>=最大滚动&&velocity>0)velocity=0;}Layout();}
    void OnDisable(){dragging=false;velocity=0;}
}
