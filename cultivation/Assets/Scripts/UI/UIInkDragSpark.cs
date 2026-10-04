using UnityEngine;
using UnityEngine.UI;

/// <summary>独立于拖影倾斜层的跟手光点，暂停时仍持续发射短命粒子。</summary>
public class UIInkDragSpark : MonoBehaviour
{
    struct Mote {public RectTransform rect;public Image image;public Vector3 position;public Vector2 velocity;public float life;}
    readonly Mote[] motes=new Mote[24];RectTransform core;float clock;int next;
    public int 粒子数=>motes.Length;
    public void Initialize(){
        var sprite=InkUITheme.Load("SkillsDynamic/fx-star-particle");
        var image=UIBuildUtils.CreateImage("CursorStar",transform,new Color(.7f,1,1));image.sprite=sprite;core=image.rectTransform;core.sizeDelta=Vector2.one*64;
        var spot=UIBuildUtils.CreateRect("CursorLightCore",transform);spot.sizeDelta=Vector2.one*32;spot.gameObject.AddComponent<UIInkCursorCore>().raycastTarget=false;
        for(int i=0;i<motes.Length;i++){var mote=UIBuildUtils.CreateImage("LightMote",transform,new Color(.7f,1,1));mote.sprite=sprite;mote.rectTransform.sizeDelta=Vector2.one*28;mote.gameObject.SetActive(false);motes[i]=new Mote{rect=mote.rectTransform,image=mote};}
    }
    public void Follow(Vector2 screen){var parent=transform.parent as RectTransform;var canvas=GetComponentInParent<Canvas>();RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,out var point);((RectTransform)transform).anchoredPosition=point;}
    void Update(){
        float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);clock+=dt;core.localScale=Vector3.one*(UIInkMotion.减少动效?1:1+.12f*Mathf.Sin(Time.unscaledTime*8));
        if(!UIInkMotion.减少动效 && clock>=.025f){clock=0;var m=motes[next];m.rect.gameObject.SetActive(true);m.position=core.position;float angle=next*2.39996f;m.velocity=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*35;m.life=.45f;motes[next]=m;next=(next+1)%motes.Length;}
        for(int i=0;i<motes.Length;i++){var m=motes[i];if(m.life<=0)continue;m.life-=dt;m.position+=(Vector3)(m.velocity*dt);m.rect.position=m.position;m.image.color=new Color(.55f,.92f,1,Mathf.Clamp01(m.life/.45f));m.rect.localScale=Vector3.one*Mathf.Max(.2f,m.life/.45f);if(m.life<=0)m.rect.gameObject.SetActive(false);motes[i]=m;}
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public class UIInkCursorCore : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh){
        vh.Clear();vh.AddVert(Vector3.zero,Color.white,Vector2.zero);
        for(int ring=0;ring<2;ring++)for(int i=0;i<24;i++){float angle=i*Mathf.PI/12;vh.AddVert(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*(ring==0?4:16),ring==0?new Color(.95f,1,.9f,1):new Color(.4f,1,1,0),Vector2.zero);}
        for(int i=0;i<24;i++){int a=1+i,b=1+(i+1)%24;vh.AddTriangle(0,a,b);vh.AddTriangle(a,a+24,b);vh.AddTriangle(b,a+24,b+24);}
    }
}
