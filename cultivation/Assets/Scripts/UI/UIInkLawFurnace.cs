using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>炉鼎是可逆暂存区；入炉晶体、炉火与精粹使用非缩放时间动画。</summary>
public sealed class UIInkLawFurnace : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    UIInkLawPage page; UIPanelData data; RectTransform area; Image cauldron; Text status;
    Button insert, smelt; UIInkLawFurnaceFx fx; float heat;
    readonly List<Flight> flights = new List<Flight>();
    sealed class Flight { public Image image; public Vector2 from; public float start; }
    public bool 正在熔炼 => heat > 0;
    public void 初始化(UIInkLawPage owner,UIPanelData source,Font font)
    {
        page=owner;data=source;area=(RectTransform)transform;
        var hit=gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
        var effect=UIBuildUtils.CreateRect("FurnaceEnergy",transform);UIBuildUtils.Stretch(effect);fx=effect.gameObject.AddComponent<UIInkLawFurnaceFx>();fx.raycastTarget=false;
        cauldron=UIBuildUtils.CreateImage("Cauldron",transform,Color.white);cauldron.sprite=Resources.Load<Sprite>("Laws/Crafting/law-furnace");cauldron.preserveAspect=true;cauldron.raycastTarget=false;
        UIBuildUtils.Place(cauldron.rectTransform,new Vector2(0,.12f),new Vector2(1,.94f),Vector2.zero,Vector2.zero);
        status=UIBuildUtils.CreateText("FurnaceCount",transform,font,"",17,TextAnchor.MiddleCenter,new Color(.94f,.92f,.78f));status.raycastTarget=false;
        UIBuildUtils.Place(status.rectTransform,Vector2.zero,new Vector2(1,.16f),Vector2.zero,Vector2.zero);
    }
    public void 设置按钮(Button left,Button right){insert=left;smelt=right;Refresh();}
    public void Refresh()
    {
        status.text="炉中 "+data.炉中法则.Count+" 枚";
        if(insert)insert.interactable=!正在熔炼&&data.法则碎片.Any(x=>!data.法则装备.Contains(x.id)&&!data.炉中法则.Contains(x.id));
        if(smelt)smelt.interactable=!正在熔炼&&flights.Count==0&&data.炉中法则.Count>0;
    }
    public void OnDrop(PointerEventData e)
    {
        if(正在熔炼||page.拖拽法则==null)return;var law=page.拖拽法则;
        if(LawRefinementSystem.放入(data,law.id,true)){page.结束拖拽(true);入炉动画(law,e.position);}
    }
    public void OnPointerClick(PointerEventData e)
    {if(e.button==PointerEventData.InputButton.Left&&!正在熔炼&&data.炉中法则.Count>0)LawRefinementSystem.取回全部(data);}
    public void 全部放入()
    {
        if(正在熔炼)return;
        var laws=data.法则碎片.Where(x=>!data.法则装备.Contains(x.id)&&!data.炉中法则.Contains(x.id)).Take(6).ToArray();
        var positions=laws.Select(x=>{var card=page.法则背包.寻找卡片(x);return RectTransformUtility.WorldToScreenPoint(null,card?card.position:page.transform.position);}).ToArray();
        LawRefinementSystem.全部放入(data);for(int i=0;i<laws.Length;i++)入炉动画(laws[i],positions[i]);
    }
    void 入炉动画(LawFragment law,Vector2 screen)
    {
        var image=UIBuildUtils.CreateImage("LawIntoFurnace",page.transform,law.颜色);image.sprite=law.图标;image.preserveAspect=true;image.raycastTarget=false;
        var rt=image.rectTransform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.sizeDelta=Vector2.one*78;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)page.transform,screen,null,out var point);rt.anchoredPosition=point;
        flights.Add(new Flight{image=image,from=point,start=Time.unscaledTime});fx.激发=.45f;Refresh();
    }
    public void 开始熔炼()
    {
        if(正在熔炼||flights.Count>0)return;
        long amount=LawRefinementSystem.熔炼(data);if(amount==0)return;
        heat=1.7f;page.播放精粹飞行(area.TransformPoint(new Vector3(0,area.rect.height*.12f,0)),amount,heat);
        Refresh();ToastUI.提示("获得法则精粹  +"+amount.ToString("N0"));
    }
    public void 洗练动效(){fx.激发=1;}
    void Update()
    {
        bool wasBusy=正在熔炼||flights.Count>0;float dt=Time.unscaledDeltaTime;heat=Mathf.Max(0,heat-dt);fx.激发=Mathf.Max(fx.激发-dt,.0f);fx.热度=heat>0?Mathf.Sin(Mathf.Clamp01(heat/1.7f)*Mathf.PI):0;
        float glow=fx.热度;cauldron.color=Color.Lerp(Color.white,new Color(1,.86f,.51f),glow*.7f);cauldron.rectTransform.localScale=Vector3.one*(1+glow*.035f*Mathf.Sin(Time.unscaledTime*24));
        for(int i=flights.Count-1;i>=0;i--){var f=flights[i];float t=(Time.unscaledTime-f.start)/.75f;
            Vector3 mouth=area.TransformPoint(new Vector3(0,area.rect.height*.12f,0));var target=(Vector2)page.transform.InverseTransformPoint(mouth);
            f.image.rectTransform.anchoredPosition=Vector2.Lerp(f.from,target,Mathf.SmoothStep(0,1,t))+Vector2.up*(Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI)*64);
            f.image.rectTransform.localScale=Vector3.one*Mathf.Lerp(1,.15f,Mathf.Clamp01(t));f.image.rectTransform.localRotation=Quaternion.Euler(0,0,t*120);var c=f.image.color;c.a=1-Mathf.Clamp01((t-.65f)/.35f);f.image.color=c;
            if(t>=1){Destroy(f.image.gameObject);flights.RemoveAt(i);fx.激发=.6f;}}
        if(wasBusy!= (正在熔炼||flights.Count>0))Refresh();
    }
    void OnDisable(){foreach(var f in flights)if(f.image)Destroy(f.image.gameObject);flights.Clear();heat=0;}
}

/// <summary>低开销UI网格：炉口灵气旋线、吸入微光与熔炼时上升精粹。</summary>
public sealed class UIInkLawFurnaceFx : MaskableGraphic
{
    public float 激发,热度;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;float time=Time.unscaledTime;var center=new Vector2(r.center.x,r.center.y+r.height*.12f);float strength=.25f+激发*.5f+热度;
        for(int strand=0;strand<3;strand++)for(int j=0;j<48;j++){
            float a=(j/48f*5.2f+time*(strand%2==0?1:-.8f)+strand*2),b=a+5.2f/48;float radius=r.width*(.34f+strand*.035f);
            var from=center+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.28f);var to=center+new Vector2(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius*.28f);
            Line(vh,from,to,.6f+热度*.9f,new Color(.76f,.85f,.66f,Mathf.Sin(j/48f*Mathf.PI)*strength*.55f));}
        for(int i=0;i<42;i++){
            float p=Mathf.Repeat(time*(.24f+热度*.35f)+i*.618f,1),a=i*2.4f+time*.6f;float radius=Mathf.Lerp(r.width*.55f,4,p);
            var point=center+new Vector2(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius*.45f+(热度>0?p*r.height*.35f:0));
            float size=(1.3f+Mathf.Sin(i)*.4f)*(1+热度);var c=new Color(.86f,.93f,.70f,Mathf.Sin(p*Mathf.PI)*strength);
            Quad(vh,point,size*2,new Color(c.r,c.g,c.b,c.a*.12f));Quad(vh,point,size,c);}
    }
    static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width;var verts=new UIVertex[4];var ps=new[]{a-n,a+n,b+n,b-n};for(int i=0;i<4;i++){verts[i]=UIVertex.simpleVert;verts[i].position=ps[i];verts[i].color=c;}vh.AddUIVertexQuad(verts);}
    static void Quad(VertexHelper vh,Vector2 p,float size,Color c){var verts=new UIVertex[4];var ps=new[]{p+new Vector2(-size,0),p+new Vector2(0,size),p+new Vector2(size,0),p+new Vector2(0,-size)};for(int i=0;i<4;i++){verts[i]=UIVertex.simpleVert;verts[i].position=ps[i];verts[i].color=c;}vh.AddUIVertexQuad(verts);}
    void Update(){if(!UIInkMotion.减少动效)SetVerticesDirty();}
}
