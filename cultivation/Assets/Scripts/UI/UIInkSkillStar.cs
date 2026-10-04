using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>星位固定命中，描线与高亮粒子随装备更新，粒子最多牵引 5 像素。</summary>
public class UIInkSkillStar : MonoBehaviour, ICanvasRaycastFilter, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    UIActiveSkillSlot slot;Material material;UIInkReveal reveal;
    UIInkStarParticle particle;Object previous;bool hovered;
    RectTransform surface;Image orbit,shadow,center;Vector2 shift,velocity;float phase;
    public Vector3 视觉位置=>surface!=null ? surface.position : transform.position;
    public Vector2 视差位移=>shift;
    public void Initialize(UIActiveSkillSlot original,int index,Material tint){
        slot=original;material=tint;phase=index*1.37f;
        slot.background.sprite=null;slot.background.alphaHitTestMinimumThreshold=0;
        surface=UIBuildUtils.CreateRect("FloatingSurface",transform);UIBuildUtils.Stretch(surface);
        orbit=UIBuildUtils.CreateImage("Orbit",surface,new Color(.62f,.75f,.64f,.5f));orbit.sprite=InkUITheme.Load("Realm/fx-ink-circle");orbit.material=material;orbit.raycastTarget=false;UIBuildUtils.Stretch(orbit.rectTransform,-10);
        reveal=orbit.gameObject.AddComponent<UIInkReveal>();
        shadow=UIBuildUtils.CreateImage("DepthShadow",surface,new Color(0,0,0,.55f));shadow.raycastTarget=false;UIBuildUtils.Stretch(shadow.rectTransform,14);shadow.rectTransform.anchoredPosition=new Vector2(0,-12);
        center=UIBuildUtils.CreateImage("EmptyStar",surface,new Color(.76f,.83f,.74f));center.sprite=InkUITheme.Load("SkillsDynamic/fx-star-particle");center.raycastTarget=false;center.rectTransform.sizeDelta=new Vector2(24,24);
        if(slot.icon!=null){slot.icon.transform.SetParent(surface,false);UIBuildUtils.Stretch(slot.icon.rectTransform,14);slot.icon.raycastTarget=false;}
        var rt=UIBuildUtils.CreateRect("EquippedStarParticle",surface);rt.anchorMin=rt.anchorMax=Vector2.one;rt.sizeDelta=new Vector2(34,34);rt.anchoredPosition=new Vector2(-8,-8);
        particle=rt.gameObject.AddComponent<UIInkStarParticle>();particle.Initialize(index,new Color(.94f,.92f,.72f));
    }
    public void Refresh(float entrance){
        slot.background.sprite=null;slot.background.overrideSprite=null;slot.background.material=null;slot.background.color=Color.clear;slot.background.raycastTarget=true;
        orbit.color=hovered?new Color(.89f,.96f,.82f,.75f):new Color(.52f,.64f,.55f,.45f);reveal.进度=entrance;
        if(slot.label!=null)slot.label.color=new Color(.96f,.95f,.88f);
        particle.gameObject.SetActive(slot.Content!=null);
        center.gameObject.SetActive(slot.Content==null);
        shadow.sprite=slot.icon!=null?slot.icon.sprite:null;shadow.enabled=shadow.sprite!=null;
        if(slot.icon!=null)slot.icon.color=new Color(1,1,1,UIDragContext.OriginData==slot.data && UIDragContext.OriginSlot==slot.index ?.25f:1);
        if(previous!=slot.Content){if(slot.Content!=null)UIInkPulse.Emit(transform as RectTransform,Vector2.zero,.3f);previous=slot.Content;}
    }
    void LateUpdate(){
        if(surface==null)return;var canvas=GetComponentInParent<Canvas>();var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform,Input.mousePosition,camera,out var point);
        Vector2 goal=UIInkMotion.减少动效?Vector2.zero:Vector2.ClampMagnitude(point,12)*Mathf.Clamp01(1-point.magnitude/160);
        shift=Vector2.SmoothDamp(shift,goal,ref velocity,.12f,120,Time.unscaledDeltaTime);
        float breathe=UIInkMotion.减少动效?0:Mathf.Sin(Time.unscaledTime*.8f+phase)*2.5f;
        surface.anchoredPosition=shift*.5f+Vector2.up*breathe;surface.localRotation=Quaternion.Euler(-shift.y*.8f,shift.x*.8f,0);
        surface.localScale=Vector3.one*(hovered && !UIInkMotion.减少动效?1.08f:1);
        orbit.rectTransform.localRotation=Quaternion.Euler(58,0,UIInkMotion.减少动效?phase*30:Time.unscaledTime*9+phase*30);
        shadow.rectTransform.anchoredPosition=new Vector2(-shift.x*.4f,-12-Mathf.Abs(breathe));
    }
    public bool IsRaycastLocationValid(Vector2 screen,Camera cam){RectTransformUtility.ScreenPointToLocalPointInRectangle(transform as RectTransform,screen,cam,out var p);var rect=((RectTransform)transform).rect;return p.sqrMagnitude<=rect.width*rect.width*.25f;}
    public void OnPointerEnter(PointerEventData e){hovered=true;}
    public void OnPointerExit(PointerEventData e){hovered=false;}
    public void OnBeginDrag(PointerEventData e){if(slot.Content==null)return;var canvas=GetComponentInParent<Canvas>();UIDragContext.BeginFromSlot(slot,canvas.rootCanvas.transform,slot.label!=null?slot.label.font:null);UIDragContext.ApplyInkGhost();UIDragContext.Move(e.position);}
    public void OnDrag(PointerEventData e){if(UIDragContext.Dragging)UIDragContext.Move(e.position);}
    public void OnEndDrag(PointerEventData e){if(UIDragContext.Dragging)UIDragContext.End();}
    void OnDisable(){hovered=false;}
}
