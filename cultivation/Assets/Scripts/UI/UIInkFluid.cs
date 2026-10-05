using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>墨点本体流动、快速鼠标形变和轮廓洇开；命中框及文字不变形。</summary>
public class UIInkFluid : MonoBehaviour, IPointerClickHandler
{
    Image blot,icon,stroke;
    Text label;
    Material material;
    UIInkDensitySimulation simulation;
    Vector2 pointerUV=new Vector2(-10,-10),lastUV,dragVelocity,clickUV;
    bool pointerInside,hadUV;
    Vector2 velocity,offset,attraction,releaseFrom;
    float seed,elapsed,releaseTime=1,clickTime=2,entranceTime,hover;
    bool selected,near;
    public bool 使用密度模拟=true;
    public float 基础浓度=1;
    [Range(0,1)] public float 位移强度=1;
    [Range(0,1)] public float 形变强度=1;
    public RectTransform 渐隐视口;
    public bool 渐隐启用;
    public Vector2 绘制偏移 => offset;
    public Vector2 鼠标牵引 => attraction;
    public float 形变时钟 => material!=null ? material.GetFloat("_Clock") : 0;
    public int 模拟步数 => simulation!=null ? simulation.步数 : 0;
    public Texture 墨密度纹理 => simulation!=null ? simulation.纹理 : null;
#if UNITY_EDITOR
    public Vector2? 调试鼠标;
#endif
    public void 初始化(Image image,Image glyph,Text text,Image underline,int index) {
        blot=image; icon=glyph; label=text; stroke=underline; seed=17.13f+index*31.71f;
        // Navigation resets size before each application. Padding is drawing-only;
        // shader maps the original shape into the middle, leaving room to bleed.
        blot.rectTransform.sizeDelta*=1.4f;
        var oldTrace=transform.Find("InkNavWaterTrace");
        if(oldTrace!=null) { oldTrace.gameObject.SetActive(false); Destroy(oldTrace.gameObject); }
        if(material==null) { var shader=Shader.Find("Cultivation/UI/InkFluid"); if(shader!=null) material=new Material(shader); }
        if(material!=null) { blot.material=material; material.SetFloat("_Seed",seed); }
        if(使用密度模拟 && simulation==null && blot.sprite!=null) simulation=new UIInkDensitySimulation(blot.sprite.texture,seed);
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
        RectTransformUtility.ScreenPointToLocalPointInRectangle(blot.rectTransform,screenPoint,camera,out var inkLocal);
        var rect=blot.rectTransform.rect;
        pointerUV=new Vector2((inkLocal.x-rect.xMin)/rect.width,(inkLocal.y-rect.yMin)/rect.height);
        pointerInside=rect.Contains(inkLocal);
        var delta=hadUV ? pointerUV-lastUV : Vector2.zero;
        var movement=Vector2.ClampMagnitude(delta/Mathf.Max(.001f,Time.unscaledDeltaTime),1);
        dragVelocity=Vector2.Lerp(dragVelocity,movement,1-Mathf.Exp(-Time.unscaledDeltaTime*18));
        lastUV=pointerUV; hadUV=true;
    }
    void Update() {
        if(blot==null) return;
        // One pointer sample per frame, including movement within an unchanged hit target.
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
        var goal=reduced ? Vector2.zero : (Vector2.ClampMagnitude(drift,3)+attraction)*位移强度;
        offset=reduced ? Vector2.zero : Vector2.SmoothDamp(offset,goal,ref velocity,.16f,100,dt);
        blot.rectTransform.anchoredPosition=offset;
        if(icon!=null) icon.rectTransform.anchoredPosition=offset;
        clickTime+=dt; entranceTime+=dt;
        hover=Mathf.MoveTowards(hover,!reduced && pointerInside ? 1 : 0,dt*8);
        if(!reduced && simulation!=null) simulation.推进(Time.unscaledDeltaTime,pointerUV,dragVelocity,pointerInside,clickUV,clickTime);
        if(material!=null) {
            material.SetFloat("_Clock",reduced ? 0 : elapsed);
            material.SetFloat("_Deform",reduced ? 0 : .12f*形变强度);
            material.SetVector("_Pointer",new Vector4(pointerUV.x,pointerUV.y,hover*形变强度,0));
            material.SetVector("_Drag",new Vector4(dragVelocity.x,dragVelocity.y,0,0));
            material.SetFloat("_Bleed",reduced ? 0 : Mathf.SmoothStep(0,1,clickTime/.12f)*(1-Mathf.SmoothStep(.25f,1.1f,clickTime)));
            material.SetTexture("_InkState",墨密度纹理);
            material.SetFloat("_HasState",墨密度纹理!=null ? 1 : 0);
            material.SetFloat("_Density",基础浓度*((selected ? 1.35f : .85f)+(reduced ? 0 : Mathf.Max(0,1-clickTime/.3f)*.3f)));
            material.SetFloat("_Reveal",reduced ? 1 : Mathf.Clamp01(entranceTime/.18f));
            material.SetFloat("_FadeEnabled",渐隐启用 && 渐隐视口!=null ? 1 : 0);
            if(渐隐视口!=null) material.SetVector("_FadeBounds",new Vector4(渐隐视口.rect.yMin,渐隐视口.rect.yMax,82,0));
            var ripple=material.GetVector("_Ripple"); ripple.z=Mathf.Clamp01(clickTime/.55f); ripple.w=reduced ? 0 : 1; material.SetVector("_Ripple",ripple);
        }
    }
    public void OnPointerClick(PointerEventData e) {
        var button=GetComponent<Button>(); if(button!=null && !button.IsInteractable()) return;
        if(material==null || UIInkMotion.减少动效) return;
        clickTime=0;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(blot.rectTransform,e.position,e.pressEventCamera,out var p);
        var rect=blot.rectTransform.rect;
        clickUV=new Vector2((p.x-rect.xMin)/rect.width,(p.y-rect.yMin)/rect.height);
        material.SetVector("_Ripple",new Vector4(clickUV.x,clickUV.y,0,1));
    }
    void OnEnable() { entranceTime=0; }
    void OnDisable() {
        velocity=offset=attraction=releaseFrom=dragVelocity=Vector2.zero; near=hadUV=false; hover=0;
        if(blot!=null) blot.rectTransform.anchoredPosition=Vector2.zero;
        if(icon!=null) icon.rectTransform.anchoredPosition=Vector2.zero;
    }
    void OnDestroy() { simulation?.释放(); if(material!=null) Destroy(material); }
}
