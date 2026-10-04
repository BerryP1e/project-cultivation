using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>可复用墨点：确定性游走 + 阻尼牵引 + 贴图边缘形变，命中框及文字不变形。</summary>
public class UIInkFluid : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler, IPointerExitHandler
{
    Image blot,icon,stroke,water;
    Text label;
    Material material;
    Vector2 velocity,offset,attraction,releaseFrom,previousPointer;
    float seed,elapsed,releaseTime=1,clickTime=1,streakTime=1,entranceTime;
    bool selected,near,hasPointer;
    public Vector2 绘制偏移 => offset;
    public Vector2 鼠标牵引 => attraction;
    public float 形变时钟 => material!=null ? material.GetFloat("_Clock") : 0;
#if UNITY_EDITOR
    public Vector2? 调试鼠标;
#endif
    public void 初始化(Image image,Image glyph,Text text,Image underline,int index) {
        blot=image; icon=glyph; label=text; stroke=underline; seed=17.13f+index*31.71f;
        if(material==null) { var shader=Shader.Find("Cultivation/UI/InkFluid"); if(shader!=null) material=new Material(shader); }
        if(material!=null) { blot.material=material; material.SetFloat("_Seed",seed); }
        if(water==null) {
            water=UIBuildUtils.CreateImage("InkNavWaterTrace",transform,InkUITheme.Ink);
            water.sprite=InkUITheme.Load("Dynamic/fx-water-streak"); water.raycastTarget=false;
            water.rectTransform.anchorMin=water.rectTransform.anchorMax=new Vector2(.5f,.62f);
            water.rectTransform.sizeDelta=new Vector2(72,18);
        }
        entranceTime=0;
    }
    public void 选中(bool value) {
        selected=value;
        if(label!=null) label.color=value ? new Color(.98f,.96f,.90f) : new Color(.76f,.79f,.72f);
        if(blot!=null) blot.color=value ? InkUITheme.Ink : new Color(.38f,.42f,.39f);
        if(stroke!=null) stroke.enabled=value;
    }
    public void 鼠标位置(Vector2 screenPoint) {
        if(blot==null) return;
        var canvas=GetComponentInParent<Canvas>();
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var rt=transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,screenPoint,camera,out var local);
        local-=new Vector2(0,rt.rect.height*.12f);
        float distance=local.magnitude;
        bool nowNear=distance<112;
        if(nowNear) { attraction=Vector2.ClampMagnitude(local,8)*Mathf.Pow(1-distance/112,2); releaseTime=0; }
        else if(near) { releaseFrom=attraction; releaseTime=0; }
        near=nowNear;
    }
    void Update() {
        if(blot==null) return;
#if UNITY_EDITOR
        鼠标位置(调试鼠标 ?? (Vector2)Input.mousePosition);
#else
        鼠标位置(Input.mousePosition);
#endif
    }
    void LateUpdate() {
        if(blot==null) return;
        float dt=Mathf.Min(Time.unscaledDeltaTime,.05f); elapsed+=dt;
        bool reduced=UIInkMotion.减少动效;
        if(!near) { releaseTime+=dt; attraction=releaseFrom*(1-UIInkMotion.Timing.Cubic(releaseTime/.22f)); }
        var drift=new Vector2(Mathf.PerlinNoise(seed,elapsed/(4+seed%3))-.5f,Mathf.PerlinNoise(seed+22,elapsed/(5+seed%2))-.5f)*6;
        var goal=reduced ? Vector2.zero : Vector2.ClampMagnitude(drift,3)+attraction;
        offset=reduced ? Vector2.zero : Vector2.SmoothDamp(offset,goal,ref velocity,.16f,100,dt);
        blot.rectTransform.anchoredPosition=offset;
        if(icon!=null) icon.rectTransform.anchoredPosition=offset;
        clickTime+=dt; streakTime+=dt; entranceTime+=dt;
        if(material!=null) {
            material.SetFloat("_Clock",reduced ? 0 : elapsed);
            material.SetFloat("_Deform",reduced ? 0 : .018f);
            material.SetFloat("_Density",(selected ? 1.35f : .85f)+(reduced ? 0 : Mathf.Max(0,1-clickTime/.3f)*.3f));
            material.SetFloat("_Reveal",reduced ? 1 : Mathf.Clamp01(entranceTime/.18f));
            var ripple=material.GetVector("_Ripple"); ripple.z=Mathf.Clamp01(clickTime/.3f); ripple.w=reduced ? 0 : 1; material.SetVector("_Ripple",ripple);
        }
        if(water!=null) water.color=new Color(.188f,.239f,.216f,reduced ? 0 : Mathf.Max(0,1-streakTime/.25f)*.48f);
    }
    public void OnPointerMove(PointerEventData e) {
        if(water==null || UIInkMotion.减少动效) return;
        var delta=hasPointer ? e.position-previousPointer : e.delta;
        previousPointer=e.position; hasPointer=true;
        if(delta.sqrMagnitude<1) return;
        streakTime=0; water.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        water.rectTransform.sizeDelta=new Vector2(Mathf.Clamp(delta.magnitude*4,35,95),14);
    }
    public void OnPointerExit(PointerEventData e) { hasPointer=false; }
    public void OnPointerClick(PointerEventData e) {
        var button=GetComponent<Button>(); if(button!=null && !button.IsInteractable()) return;
        if(material==null || UIInkMotion.减少动效) return;
        clickTime=0;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(blot.rectTransform,e.position,e.pressEventCamera,out var p);
        var rect=blot.rectTransform.rect;
        material.SetVector("_Ripple",new Vector4((p.x-rect.xMin)/rect.width,(p.y-rect.yMin)/rect.height,0,1));
    }
    void OnEnable() { entranceTime=0; }
    void OnDisable() {
        velocity=offset=attraction=releaseFrom=Vector2.zero; near=hasPointer=false;
        if(blot!=null) blot.rectTransform.anchoredPosition=Vector2.zero;
        if(icon!=null) icon.rectTransform.anchoredPosition=Vector2.zero;
    }
    void OnDestroy() { if(material!=null) Destroy(material); }
}
