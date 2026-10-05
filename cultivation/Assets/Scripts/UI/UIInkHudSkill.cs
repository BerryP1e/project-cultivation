using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>HUD 使用神通页同款悬浮图标、墨轨道与星粒，不提供装备拖拽。</summary>
public class UIInkHudSkill : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    PlayerHud hud; int index; Image orbit, depth, blot; UIInkStarParticle star;
    Material iconMaterial; RectTransform surface; bool hovered;
    UIInkHudOrbit backOrbit, frontOrbit;
    public bool 使用三维;
    public Vector3 视觉位置=>surface!=null?surface.position:transform.position;
    public Vector2 视差位移 {get;private set;}
    public float 墨晕比例 { get; private set; }
    public void Initialize(PlayerHud source, int slot)
    {
        hud = source; index = slot; var g = hud.技能格们[index];
        g.墨晕冷却 = true;
        if (g.底 == null) g.底 = transform.Find("底")?.GetComponent<Image>();
        if (g.底 == null) g.底 = UIBuildUtils.CreateImage("底", transform, Color.clear);
        g.底.sprite = null; g.底.overrideSprite = null; g.底.color = Color.clear; g.底.raycastTarget = true;
        var shape = GetComponent<InkUIHitShape>(); if (shape != null) shape.enabled = false;
        surface = UIBuildUtils.CreateRect("HudFloatingSurface", transform); UIBuildUtils.Stretch(surface);
        orbit = UIBuildUtils.CreateImage("HudInkOrbit", surface, new Color(.62f, .75f, .64f, .6f));
        orbit.sprite = InkUITheme.Load("Realm/fx-ink-circle"); orbit.raycastTarget = false; UIBuildUtils.Stretch(orbit.rectTransform, -5);
        orbit.enabled=false;
        var back=UIBuildUtils.CreateRect("OrbitBehind",surface);UIBuildUtils.Stretch(back,-6);backOrbit=back.gameObject.AddComponent<UIInkHudOrbit>();backOrbit.raycastTarget=false;backOrbit.序号=index;
        depth = UIBuildUtils.CreateImage("HudDepthShadow", surface, new Color(0, 0, 0, .6f)); depth.raycastTarget = false; UIBuildUtils.Stretch(depth.rectTransform, 15);depth.rectTransform.anchoredPosition=Vector2.down*10;
        blot = UIBuildUtils.CreateImage("CooldownInkBloom", surface, new Color(.10f,.17f,.15f,.7f));blot.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4");blot.raycastTarget=false;UIBuildUtils.Stretch(blot.rectTransform,-18);
        if (g.图标 != null) { g.图标.transform.SetParent(surface, false); UIBuildUtils.Stretch(g.图标.rectTransform, 13); g.图标.preserveAspect = true;g.图标.raycastTarget=false;
            var shader=Resources.Load<Shader>("UI/InkUI/Dynamic/InkCooldown");if(shader!=null){iconMaterial=new Material(shader);g.图标.material=iconMaterial;}}
        var front=UIBuildUtils.CreateRect("OrbitInFront",surface);UIBuildUtils.Stretch(front,-6);frontOrbit=front.gameObject.AddComponent<UIInkHudOrbit>();frontOrbit.前层=true;frontOrbit.序号=index;frontOrbit.raycastTarget=false;
        var rt = UIBuildUtils.CreateRect("HudEquippedStar", surface);rt.sizeDelta=new Vector2(12,12);rt.anchoredPosition=new Vector2(20,15);star=rt.gameObject.AddComponent<UIInkStarParticle>();star.Initialize(index,new Color(.94f,.92f,.72f));
        g.按键文字.rectTransform.anchorMin=g.按键文字.rectTransform.anchorMax=new Vector2(.5f,0);
        g.按键文字.rectTransform.sizeDelta=new Vector2(28,20);g.按键文字.rectTransform.anchoredPosition=new Vector2(0,-9);g.按键文字.alignment=TextAnchor.MiddleCenter;g.按键文字.fontSize=13;g.按键文字.transform.SetAsLastSibling();
        g.冷却文字.fontSize=16;g.冷却文字.transform.SetAsLastSibling();
    }
    void LateUpdate()
    {
        if(hud==null||surface==null)return;var g=hud.技能格们[index];
        var entry=hud.面板数据!=null&&hud.面板数据.主动技能!=null&&index<hud.面板数据.主动技能.Count?hud.面板数据.主动技能[index] as IPanelEntry:null;
        if(g.图标!=null){g.图标.sprite=UIInkAbilityArt.Icon(entry);g.图标.color=Color.white;g.图标.enabled=entry!=null&&g.图标.sprite!=null;}
        depth.sprite=g.图标!=null?g.图标.sprite:null;depth.enabled=depth.sprite!=null;
        墨晕比例=hud.施放器!=null?Mathf.Clamp01(hud.施放器.冷却比例(index)):0;
        if(iconMaterial!=null)iconMaterial.SetFloat("_Cooldown",墨晕比例);
        if(g.冷却遮罩!=null)g.冷却遮罩.gameObject.SetActive(false);
        blot.color=new Color(.10f,.17f,.15f,墨晕比例*.68f);
        blot.rectTransform.localScale=Vector3.one*(1+墨晕比例*.28f);
        star.gameObject.SetActive(entry!=null);star.transform.localScale=Vector3.one*(1-墨晕比例*.35f);
        backOrbit.gameObject.SetActive(!使用三维);frontOrbit.gameObject.SetActive(!使用三维);
        backOrbit.装备=frontOrbit.装备=entry!=null;
        float phase=index*1.37f;float breathe=UIInkMotion.减少动效?0:Mathf.Sin(Time.unscaledTime*.8f+phase)*2;
        var canvas=GetComponentInParent<Canvas>();RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,Input.mousePosition,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var point);
        视差位移=UIInkMotion.减少动效?Vector2.zero:Vector2.ClampMagnitude(point,10)*Mathf.Clamp01(1-point.magnitude/150);
        surface.anchoredPosition=Vector2.up*breathe+视差位移*.5f;surface.localScale=Vector3.one*(hovered?1.08f:1);
        if(使用三维&&g.图标!=null)g.图标.enabled=false;
        orbit.rectTransform.localRotation=Quaternion.Euler(58,0,UIInkMotion.减少动效?phase*30:Time.unscaledTime*9+phase*30);
        orbit.color=Color.Lerp(new Color(.62f,.75f,.64f,.6f),new Color(.18f,.25f,.22f,.38f),墨晕比例);
    }
    public void OnPointerEnter(PointerEventData e){hovered=true;}
    public void OnPointerExit(PointerEventData e){hovered=false;}
    void OnDestroy(){if(iconMaterial!=null)Destroy(iconMaterial);}
}

/// <summary>实心灰圆与前后分层椭圆轨道。粒子按深度切换层级，而非旋转一张平面贴图。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIInkHudOrbit : MaskableGraphic
{
    public bool 前层, 主体, 装备;
    public int 序号;
    bool previousEquipped;
    void Update(){if(previousEquipped!=装备||(!UIInkMotion.减少动效&&装备))SetVerticesDirty();previousEquipped=装备;}
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;var center=r.center;float radius=Mathf.Min(r.width,r.height)*.36f;
        if(主体){Disc(vh,center,radius*1.35f,new Color(.78f,.63f,.26f,.95f));return;}
        if(!前层)Disc(vh,center,radius,new Color(.48f,.49f,.49f,.9f));
        if(!装备)return;
        float phase=UIInkMotion.减少动效?序号:Time.unscaledTime*.9f+序号;
        for(int i=0;i<64;i++)
        {
            float t=i*Mathf.PI*2/64,next=(i+1)*Mathf.PI*2/64;
            if((Mathf.Sin(t)>=0)!=前层)continue;
            var c=前层?new Color(.77f,.84f,.78f,.85f):new Color(.37f,.44f,.43f,.45f);
            Line(vh,Orbit(center,radius,t),Orbit(center,radius,next),前层?1.4f:.8f,c);
        }
        for(int i=0;i<3;i++)
        {
            float t=phase+i*Mathf.PI*2/3;bool front=Mathf.Sin(t)>=0;if(front!=前层)continue;
            Vector2 p=Orbit(center,radius,t);float s=front?2.4f:1.2f;
            Disc(vh,p,s*2.2f,new Color(.75f,.86f,.83f,.12f));Disc(vh,p,s,new Color(.85f,.93f,.88f,front?.95f:.45f));
        }
    }
    static Vector2 Orbit(Vector2 c,float r,float t)
    {
        var p=new Vector2(Mathf.Cos(t)*r*1.35f,Mathf.Sin(t)*r*.50f);
        float a=-.28f;return c+new Vector2(p.x*Mathf.Cos(a)-p.y*Mathf.Sin(a),p.x*Mathf.Sin(a)+p.y*Mathf.Cos(a));
    }
    public static void Disc(VertexHelper vh,Vector2 p,float radius,Color c)
    {
        int k=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);
        for(int i=0;i<=48;i++){float a=i*Mathf.PI*2/48;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);if(i>0)vh.AddTriangle(k,k+i,k+i+1);}
    }
    public static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
    {
        var d=b-a;if(d.sqrMagnitude<.0001f)return;var n=new Vector2(-d.y,d.x).normalized*width*.5f;int k=vh.currentVertCount;
        vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class UIInkHudChain : MaskableGraphic
{
    public static readonly Vector2[] Positions={new Vector2(40,240),new Vector2(48,162),new Vector2(110,72),new Vector2(207,91),new Vector2(305,76),new Vector2(403,87)};
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var c=new Color(.48f,.49f,.49f,.9f);
        for(int i=0;i<Positions.Length-1;i++)
        {
            var a=Positions[i];var b=Positions[i+1];Vector2 last=a;
            for(int j=1;j<=28;j++){float t=j/28f;var p=Vector2.Lerp(a,b,t)+new Vector2(i<2?-10*Mathf.Sin(t*Mathf.PI):0,i>=2?9*Mathf.Sin(t*Mathf.PI*2):0);UIInkHudOrbit.Line(vh,last,p,4,c);last=p;}
        }
    }
}
