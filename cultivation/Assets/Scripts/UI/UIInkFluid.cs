using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>可复用墨点：有状态二维墨密度流动、阻尼牵引、水痕与点击扩散，命中框及文字不变形。</summary>
public class UIInkFluid : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler, IPointerExitHandler
{
    Image blot,icon,stroke,water;
    Text label;
    Material material;
    UIInkDensitySimulation simulation;
    Vector2 pointerUV=new Vector2(-10,-10),lastUV,dragVelocity,clickUV;
    bool pointerInside,hadUV;
    Vector2 velocity,offset,attraction,releaseFrom,previousPointer;
    float seed,elapsed,releaseTime=1,clickTime=1,streakTime=1,entranceTime;
    bool selected,near,hasPointer;
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
        if(material==null) { var shader=Shader.Find("Cultivation/UI/InkFluid"); if(shader!=null) material=new Material(shader); }
        if(material!=null) { blot.material=material; material.SetFloat("_Seed",seed); }
        if(simulation==null && blot.sprite!=null) simulation=new UIInkDensitySimulation(blot.sprite.texture,seed);
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
        RectTransformUtility.ScreenPointToLocalPointInRectangle(blot.rectTransform,screenPoint,camera,out var inkLocal);
        var rect=blot.rectTransform.rect;
        pointerUV=new Vector2((inkLocal.x-rect.xMin)/rect.width,(inkLocal.y-rect.yMin)/rect.height);
        pointerInside=rect.Contains(inkLocal);
        var delta=hadUV ? pointerUV-lastUV : Vector2.zero;
        dragVelocity=Vector2.ClampMagnitude(delta/Mathf.Max(.001f,Time.unscaledDeltaTime),1);
        if(pointerInside && hadUV && delta.sqrMagnitude>.000002f) 划痕(inkLocal,delta);
        lastUV=pointerUV; hadUV=true;
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
        if(!reduced && simulation!=null) simulation.推进(Time.unscaledDeltaTime,pointerUV,dragVelocity,pointerInside,clickUV,clickTime);
        if(material!=null) {
            material.SetFloat("_Clock",reduced ? 0 : elapsed);
            material.SetFloat("_Deform",reduced ? 0 : .055f);
            material.SetTexture("_InkState",墨密度纹理);
            material.SetFloat("_HasState",墨密度纹理!=null ? 1 : 0);
            material.SetFloat("_Density",(selected ? 1.35f : .85f)+(reduced ? 0 : Mathf.Max(0,1-clickTime/.3f)*.3f));
            material.SetFloat("_Reveal",reduced ? 1 : Mathf.Clamp01(entranceTime/.18f));
            var ripple=material.GetVector("_Ripple"); ripple.z=Mathf.Clamp01(clickTime/.55f); ripple.w=reduced ? 0 : 1; material.SetVector("_Ripple",ripple);
        }
        if(water!=null) water.color=new Color(.76f,.79f,.70f,reduced ? 0 : Mathf.Max(0,1-streakTime/.45f)*.9f);
    }
    void 划痕(Vector2 point,Vector2 direction) {
        if(water==null || UIInkMotion.减少动效) return;
        streakTime=0;
        water.rectTransform.anchoredPosition=point+(blot!=null ? blot.rectTransform.anchoredPosition : Vector2.zero);
        water.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
        water.rectTransform.sizeDelta=new Vector2(Mathf.Clamp(direction.magnitude*1200,40,100),22);
    }
    public void OnPointerMove(PointerEventData e) {
        if(water==null || UIInkMotion.减少动效) return;
        var delta=hasPointer ? e.position-previousPointer : e.delta;
        previousPointer=e.position; hasPointer=true;
        if(delta.sqrMagnitude<1) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(blot.rectTransform,e.position,e.enterEventCamera,out var local);
        if(blot.rectTransform.rect.Contains(local)) 划痕(local,delta/Mathf.Max(1,blot.rectTransform.rect.width));
    }
    public void OnPointerExit(PointerEventData e) { hasPointer=false; }
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
        velocity=offset=attraction=releaseFrom=Vector2.zero; near=hasPointer=hadUV=false;
        if(blot!=null) blot.rectTransform.anchoredPosition=Vector2.zero;
        if(icon!=null) icon.rectTransform.anchoredPosition=Vector2.zero;
    }
    void OnDestroy() { simulation?.释放(); if(material!=null) Destroy(material); }
}
