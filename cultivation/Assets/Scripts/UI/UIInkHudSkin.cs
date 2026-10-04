using UnityEngine;
using UnityEngine.UI;

/// <summary>运行时 HUD：左下连续气血/灵力，右侧神通星位。业务仍由 PlayerHud 驱动。</summary>
[DisallowMultipleComponent]
public class UIInkHudSkin : MonoBehaviour
{
    public bool 启用 = true;
    bool built;
    void Start() => 刷新();
    public void 刷新()
    {
        if (!启用 || !InkUITheme.Enabled || built) return;
        var hud = GetComponent<PlayerHud>(); if (hud == null) return; built = true;
        foreach (Transform child in transform)
        {
            var rt = child as RectTransform; if (rt == null) continue;
            if (child.name == "修炼") { child.gameObject.SetActive(false); continue; }
            if (child.name == "功法")
            {
                rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
                rt.anchoredPosition = new Vector2(32, 25); rt.sizeDelta = new Vector2(78, 78);
                var bg = child.Find("底")?.GetComponent<Image>();
                if (bg != null) { bg.sprite = InkUITheme.Load("Realm/fx-ink-circle"); bg.color = new Color(.80f,.83f,.72f,.8f); }
            }
            if (child.name.StartsWith("Skill") && int.TryParse(child.name.Substring(5), out int slot))
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1,.5f); rt.pivot = new Vector2(.5f,.5f);
                rt.anchoredPosition = new Vector2(-65, 230-(slot-1)*92); rt.sizeDelta = new Vector2(78,78);
                child.gameObject.AddComponent<UIInkHudSkill>().Initialize(hud, slot-1);
            }
            if (child.name == "气血" || child.name == "灵力")
            {
                bool mana = child.name == "灵力";
                rt.anchorMin = rt.anchorMax = Vector2.zero;rt.pivot=Vector2.zero;
                rt.anchoredPosition=new Vector2(123,mana?27:68);rt.sizeDelta=new Vector2(500,32);
                foreach(var img in child.GetComponentsInChildren<Image>(true))img.enabled=false;
                var brush=UIBuildUtils.CreateRect("ContinuousBranch",child);UIBuildUtils.Stretch(brush);brush.SetAsFirstSibling();
                var branch=brush.gameObject.AddComponent<UIInkVitalBranch>();branch.raycastTarget=false;branch.Mana=mana;branch.Source=mana?hud.灵力填充:hud.气血填充;
                var text=mana?hud.灵力文字:hud.气血文字;
                if(text!=null){text.fontSize=16;text.alignment=TextAnchor.MiddleRight;text.color=new Color(.92f,.93f,.86f);text.rectTransform.anchoredPosition=new Vector2(0,mana?-19:20);text.gameObject.AddComponent<Shadow>().effectColor=new Color(0,0,0,.85f);}
            }
        }
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
