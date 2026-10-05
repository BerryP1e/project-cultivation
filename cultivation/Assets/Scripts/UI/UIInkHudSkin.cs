using UnityEngine;
using UnityEngine.UI;

/// <summary>运行时 HUD：圆形功法、连续细条与左下弧链技能位。业务仍由 PlayerHud 驱动。</summary>
[DisallowMultipleComponent]
public class UIInkHudSkin : MonoBehaviour
{
    public bool 启用 = true;
    bool built;
    Text realmLabel;PlayerHud owner;
    Material realmMaterial;
    void OnDestroy(){if(realmMaterial!=null)Destroy(realmMaterial);}
    void LateUpdate(){if(realmLabel!=null)realmLabel.text=owner.面板数据!=null?owner.面板数据.GetRealmName():"";}
    void Start() => 刷新();
    public void 刷新()
    {
        if (!启用 || !InkUITheme.Enabled || built) return;
        var hud = GetComponent<PlayerHud>(); if (hud == null) return; built = true;owner=hud;
        var skillVisuals=new UIInkHudSkill[6];
        foreach (Transform child in transform)
        {
            var rt = child as RectTransform; if (rt == null) continue;
            if (child.name == "修炼") { child.gameObject.SetActive(false); continue; }
            if (child.name == "功法")
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(78,128); rt.sizeDelta = new Vector2(160,160);
                var bg = child.Find("底")?.GetComponent<Image>();
                if (bg != null) { bg.sprite=InkUITheme.Load("Realm/fx-ink-circle");bg.color=new Color(.81f,.84f,.75f,.95f);bg.preserveAspect=true;var shader=Shader.Find("UI/InkCircle");if(shader!=null){realmMaterial=new Material(shader);bg.material=realmMaterial;} }
                if(hud.功法图标!=null)hud.功法图标.enabled=false;
                var shade=UIBuildUtils.CreateImage("RealmInkDepth",child,new Color(.04f,.07f,.06f,.88f));shade.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4");shade.raycastTarget=false;UIBuildUtils.Stretch(shade.rectTransform,12);shade.transform.SetAsFirstSibling();
                var label=UIBuildUtils.CreateRect("HudRealmName",child);UIBuildUtils.Stretch(label,20);realmLabel=label.gameObject.AddComponent<Text>();realmLabel.font=hud.气血文字.font;realmLabel.fontSize=28;realmLabel.resizeTextForBestFit=true;realmLabel.resizeTextMinSize=20;realmLabel.resizeTextMaxSize=28;realmLabel.alignment=TextAnchor.MiddleCenter;realmLabel.color=new Color(.94f,.93f,.83f);realmLabel.raycastTarget=false;realmLabel.gameObject.AddComponent<Shadow>().effectColor=Color.black;
            }
            if (child.name.StartsWith("Skill") && int.TryParse(child.name.Substring(5), out int slot))
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = new Vector2(.5f,.5f);
                rt.anchoredPosition = UIInkHudChain.Positions[slot-1]; rt.sizeDelta = Vector2.one*72;
                var visual=child.gameObject.AddComponent<UIInkHudSkill>();visual.Initialize(hud,slot-1);skillVisuals[slot-1]=visual;
            }
            if (child.name == "气血" || child.name == "灵力")
            {
                bool mana = child.name == "灵力";
                rt.anchorMin = rt.anchorMax = Vector2.zero;rt.pivot=Vector2.zero;
                rt.anchoredPosition=new Vector2(282,mana?112:159);rt.sizeDelta=new Vector2(380,mana?40:24);
                foreach(var img in child.GetComponentsInChildren<Image>(true))img.enabled=false;
                var brush=UIBuildUtils.CreateRect("ContinuousBranch",child);UIBuildUtils.Stretch(brush);brush.SetAsFirstSibling();
                var branch=brush.gameObject.AddComponent<UIInkVitalBranch>();branch.raycastTarget=false;branch.Mana=mana;branch.Source=mana?hud.灵力填充:hud.气血填充;
                var text=mana?hud.灵力文字:hud.气血文字;
                if(text!=null){text.fontSize=14;text.alignment=TextAnchor.MiddleRight;text.color=new Color(.92f,.93f,.86f,.85f);text.rectTransform.anchoredPosition=new Vector2(0,mana?-13:13);text.gameObject.AddComponent<Shadow>().effectColor=new Color(0,0,0,.85f);}
            }
        }
        var links=UIBuildUtils.CreateRect("HudCurvedLinks",transform);links.anchorMin=links.anchorMax=Vector2.zero;links.pivot=Vector2.zero;links.sizeDelta=new Vector2(610,310);links.SetAsFirstSibling();var chain=links.gameObject.AddComponent<UIInkConstellation>();chain.HudNodes=skillVisuals;chain.raycastTarget=false;
        links.gameObject.AddComponent<UIInkSkillVolume>().InitializeHud(hud,skillVisuals);
        if(hud.信息幕布!=null)
        {
            var detail = (RectTransform)hud.信息幕布.transform;
            detail.sizeDelta = new Vector2(480, 360);
            var bg=hud.信息幕布.GetComponent<Image>();if(bg!=null){bg.sprite=InkUITheme.Load("Dynamic/panel-ink-blob");bg.type=Image.Type.Simple;bg.color=new Color(.07f,.11f,.10f,.96f);bg.raycastTarget=false;}
            if(bg!=null)bg.gameObject.AddComponent<UIInkDialogueBackdrop>();
            foreach(var img in hud.信息幕布.GetComponentsInChildren<Image>(true))if(img!=bg)img.enabled=false;
            foreach(var text in hud.信息幕布.GetComponentsInChildren<Text>(true)){text.color=new Color(.94f,.94f,.87f);var shadow=text.GetComponent<Shadow>()??text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.9f);}
            if (hud.幕布名称 != null)
            {
                hud.幕布名称.fontSize = 23;
                hud.幕布名称.rectTransform.anchoredPosition = new Vector2(44, -30);
                hud.幕布名称.rectTransform.sizeDelta = new Vector2(-145, 32);
            }
            if (hud.幕布品阶 != null)
            {
                hud.幕布品阶.fontSize = 17;
                hud.幕布品阶.rectTransform.anchoredPosition = new Vector2(-44, -36);
            }
            if (hud.幕布正文 != null)
            {
                hud.幕布正文.fontSize = 17;
                hud.幕布正文.rectTransform.offsetMin = new Vector2(44, 32);
                hud.幕布正文.rectTransform.offsetMax = new Vector2(-44, -76);
            }
        }
    }
    public void Refresh() => 刷新();
}
