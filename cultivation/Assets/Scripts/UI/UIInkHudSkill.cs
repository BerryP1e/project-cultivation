using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>HUD 使用神通页同款悬浮图标、墨轨道与星粒，不提供装备拖拽。</summary>
public class UIInkHudSkill : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    PlayerHud hud; int index; Image orbit, depth, blot; UIInkStarParticle star;
    Material iconMaterial; RectTransform surface; bool hovered;
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
        depth = UIBuildUtils.CreateImage("HudDepthShadow", surface, new Color(0, 0, 0, .6f)); depth.raycastTarget = false; UIBuildUtils.Stretch(depth.rectTransform, 15);depth.rectTransform.anchoredPosition=Vector2.down*10;
        blot = UIBuildUtils.CreateImage("CooldownInkBloom", surface, new Color(.10f,.17f,.15f,.7f));blot.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4");blot.raycastTarget=false;UIBuildUtils.Stretch(blot.rectTransform,-18);
        if (g.图标 != null) { g.图标.transform.SetParent(surface, false); UIBuildUtils.Stretch(g.图标.rectTransform, 13); g.图标.preserveAspect = true;g.图标.raycastTarget=false;
            var shader=Resources.Load<Shader>("UI/InkUI/Dynamic/InkCooldown");if(shader!=null){iconMaterial=new Material(shader);g.图标.material=iconMaterial;}}
        var rt = UIBuildUtils.CreateRect("HudEquippedStar", surface);rt.sizeDelta=new Vector2(23,23);rt.anchoredPosition=new Vector2(31,30);star=rt.gameObject.AddComponent<UIInkStarParticle>();star.Initialize(index,new Color(.94f,.92f,.72f));
        g.按键文字.rectTransform.anchorMin=g.按键文字.rectTransform.anchorMax=new Vector2(.5f,0);
        g.按键文字.rectTransform.sizeDelta=new Vector2(28,24);g.按键文字.rectTransform.anchoredPosition=new Vector2(0,-8);g.按键文字.alignment=TextAnchor.MiddleCenter;g.按键文字.fontSize=17;g.按键文字.transform.SetAsLastSibling();
        g.冷却文字.fontSize=20;g.冷却文字.transform.SetAsLastSibling();
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
        float phase=index*1.37f;float breathe=UIInkMotion.减少动效?0:Mathf.Sin(Time.unscaledTime*.8f+phase)*2;
        surface.anchoredPosition=Vector2.up*breathe;surface.localScale=Vector3.one*(hovered?1.08f:1);
        orbit.rectTransform.localRotation=Quaternion.Euler(58,0,UIInkMotion.减少动效?phase*30:Time.unscaledTime*9+phase*30);
        orbit.color=Color.Lerp(new Color(.62f,.75f,.64f,.6f),new Color(.18f,.25f,.22f,.38f),墨晕比例);
    }
    public void OnPointerEnter(PointerEventData e){hovered=true;}
    public void OnPointerExit(PointerEventData e){hovered=false;}
    void OnDestroy(){if(iconMaterial!=null)Destroy(iconMaterial);}
}
