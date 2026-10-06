using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>右下角独立法宝槽，随既有 HUD 一起隐藏，E 不占用神通 1～6。</summary>
public class TreasureSkillHud : MonoBehaviour, IPointerClickHandler
{
    TreasureCaster caster;
    UIPanelData data;
    Image icon, cooldown, backdrop;
    float clickAge=99,openAt=-1; string clickedTreasure;
    Text label, state;
    public static void Attach(TreasureCaster caster) {
        var canvases=FindObjectsOfType<Canvas>(true); Canvas hud=null;
        foreach(var c in canvases) if(c.name=="HudCanvas") { hud=c; break; }
        if(hud==null) return;
        if(hud.GetComponent<GraphicRaycaster>()==null)hud.gameObject.AddComponent<GraphicRaycaster>();
        var existing=hud.GetComponentInChildren<TreasureSkillHud>(true);
        if(existing != null) { existing.caster=caster; return; }
        var root=UIBuildUtils.CreateImage("TreasureSkillSlot",hud.transform,Color.clear);
        var rect=root.rectTransform; rect.anchorMin=rect.anchorMax=new Vector2(1,0); rect.pivot=new Vector2(1,0);
        rect.anchoredPosition=new Vector2(-32,108); rect.sizeDelta=new Vector2(158,158);
        var view=root.gameObject.AddComponent<TreasureSkillHud>(); view.caster=caster;
        var font=Resources.Load<Font>("Fonts/SimHei") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        view.backdrop=UIBuildUtils.CreateImage("TreasureInkBackdrop",root.transform,new Color(1,1,1,.48f));
        view.backdrop.sprite=InkUITheme.Load("Bag/bag-info-ink");
        view.backdrop.rectTransform.anchorMin=view.backdrop.rectTransform.anchorMax=new Vector2(.5f,.18f);
        view.backdrop.rectTransform.anchoredPosition=Vector2.zero;view.backdrop.rectTransform.sizeDelta=new Vector2(62,140); view.backdrop.rectTransform.localRotation=Quaternion.Euler(0,0,90);
        view.icon=UIBuildUtils.CreateImage("TreasureIcon",root.transform,Color.white); UIBuildUtils.Stretch(view.icon.rectTransform,0); view.icon.preserveAspect=true;view.icon.raycastTarget=true;
        view.cooldown=UIBuildUtils.CreateImage("TreasureCooldown",root.transform,new Color(0,0,0,.66f)); UIBuildUtils.Stretch(view.cooldown.rectTransform,0); view.cooldown.preserveAspect=true;
        view.cooldown.type=Image.Type.Filled; view.cooldown.fillMethod=Image.FillMethod.Radial360; view.cooldown.fillOrigin=2;
        view.state=UIBuildUtils.CreateText("TreasureState",root.transform,font,"",18,TextAnchor.MiddleCenter,Color.white); UIBuildUtils.Stretch(view.state.rectTransform);
        var key=UIBuildUtils.CreateImage("TreasureKeycap",root.transform,new Color(.045f,.065f,.055f,.82f)); key.rectTransform.anchorMin=key.rectTransform.anchorMax=new Vector2(.25f,.16f); key.rectTransform.pivot=new Vector2(.5f,.5f);
        key.rectTransform.anchoredPosition=Vector2.zero; key.rectTransform.sizeDelta=new Vector2(38,38);
        key.sprite=StationInteractor.取圆角(); key.type=Image.Type.Sliced;
        var text=UIBuildUtils.CreateText("KeyE",key.transform,font,"E",24,TextAnchor.MiddleCenter,new Color(.95f,.91f,.75f)); UIBuildUtils.Stretch(text.rectTransform);
        var keyShadow=text.gameObject.AddComponent<Shadow>(); keyShadow.effectColor=new Color(0,0,0,.8f); keyShadow.effectDistance=new Vector2(1,-1);
        view.label=UIBuildUtils.CreateText("TreasureName",root.transform,font,"未装备法宝",15,TextAnchor.MiddleCenter,new Color(.94f,.92f,.83f));
        view.label.rectTransform.anchorMin=new Vector2(0,0); view.label.rectTransform.anchorMax=new Vector2(1,0); view.label.rectTransform.offsetMin=new Vector2(-15,-28); view.label.rectTransform.offsetMax=new Vector2(15,-4);
    }
    void Update() {
        if(caster==null) { Destroy(gameObject); return; }
        if(data==null) data=FindObjectOfType<UIPanelData>();
        var t=data != null ? data.当前法宝 : null;
        icon.sprite=t != null ? t.图标 : null; icon.enabled=t != null && t.图标 != null;
        backdrop.enabled=icon.enabled;
        cooldown.sprite=icon.sprite;
        label.text=t != null ? t.DisplayName : "未装备法宝";
        float left=caster.CooldownRemaining;
        cooldown.enabled=t != null && icon.sprite != null && left>0;
        cooldown.fillAmount=t != null && t.冷却时间>0 ? Mathf.Clamp01(left/t.冷却时间) : 0;
        clickAge+=Time.unscaledDeltaTime; icon.transform.localScale=Vector3.one*Mathf.Lerp(.92f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(clickAge/.16f)));
        if(openAt>=0 && Time.unscaledTime>=openAt){openAt=-1;if(t!=null && t.法宝id==clickedTreasure && !UiEscRegistry.SceneInputBlocked){if(t.法宝id==UIPanelData.镇妖葫id)GourdFormationUI.Open(data);else if(t.法宝id==QingshanSwordTreasure.法宝id)QingshanSwordInfoUI.Open(data);}}
        state.text=caster.CurrentSkillState!=""?caster.CurrentSkillState:caster.CurrentPhase!=TreasureCaster.Phase.Idle ? "收服中" : left>0 ? Mathf.CeilToInt(left).ToString() : "";
    }
    public void OnPointerClick(PointerEventData e) {
        if(e.button!=PointerEventData.InputButton.Right || data==null || data.当前法宝==null || !data.已拥有(data.当前法宝) || UiEscRegistry.SceneInputBlocked)return;
        clickedTreasure=data.当前法宝.法宝id;clickAge=0;icon.transform.localScale=Vector3.one*.92f;openAt=Time.unscaledTime+.12f;
    }
}
