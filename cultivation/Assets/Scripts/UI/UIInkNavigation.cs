using UnityEngine;
using UnityEngine.UI;

/// <summary>父页面墨点导航。只接管绘制与布局，原按钮事件和页签数据保持原入口。</summary>
[DisallowMultipleComponent]
public class UIInkNavigation : MonoBehaviour
{
    CharacterPanelUI panel;
    static readonly Vector2[] positions = {
        new Vector2(135,-60),new Vector2(78,-158),new Vector2(146,-253),new Vector2(86,-348),
        new Vector2(128,-443),new Vector2(74,-548),new Vector2(122,-649),new Vector2(72,-744)
    };
    static readonly float[] sizes = {100,112,96,106,94,116,98,125};
    public static bool 启用 {
        get {
#if UNITY_EDITOR
            return InkUITheme.Enabled && !UnityEditor.EditorPrefs.GetBool("InkUI.QA.DynamicBefore",false);
#else
            return InkUITheme.Enabled;
#endif
        }
    }
    public static void 应用(Transform root)
    {
        if (!启用 || root == null) return;
        var panel = root.GetComponent<CharacterPanelUI>();
        if (panel == null || panel.tabs.Count != 8 || InkUITheme.Load("Dynamic/nav-ink-blot-1") == null) return;
        var nav = root.GetComponent<UIInkNavigation>();
        if (nav == null) nav=root.gameObject.AddComponent<UIInkNavigation>();
        nav.panel=panel; nav.重排();
        var dim=panel.panelRoot.transform.Find("Dim")?.GetComponent<Image>();
        if(dim!=null) dim.color=new Color(0,0,0,.20f);
        var window=panel.panelRoot.transform.Find("Window") as RectTransform;
        if(window==null) window=root.Find("Window") as RectTransform;
        if(window!=null) {
            var image=window.GetComponent<Image>();
            if(image!=null) image.enabled=false;
            var motion=window.GetComponent<UIInkMotion>();
            if(motion!=null) motion.enabled=false;
            var reveal=window.GetComponent<UIInkReveal>();
            if(reveal!=null) { reveal.进度=1; reveal.enabled=false; }
            var group=window.GetComponent<CanvasGroup>(); if(group!=null) group.alpha=1;
            foreach(var clip in window.GetComponentsInChildren<UIInkClipRect>(true))
                if(clip.面板==reveal) clip.enabled=false;
        }
    }
    void 重排()
    {
        int visible = 0;
        for(int i=0;i<panel.tabs.Count;i++)
        {
            var binding=panel.tabs[i]; var button=binding.button;
            if(button==null || i==(int)CharacterTab.战阵) continue;
            int position=visible++;
            var parentImage=button.transform.parent.GetComponent<Image>();
            if(parentImage!=null) parentImage.enabled=false;
            var layout=button.transform.parent.GetComponent<LayoutGroup>(); if(layout!=null) layout.enabled=false;
            var rt=button.transform as RectTransform;
            rt.anchorMin=rt.anchorMax=new Vector2(0,1); rt.pivot=new Vector2(.5f,.5f);
            rt.anchoredPosition=positions[position]; rt.sizeDelta=new Vector2(146,106);
            var oldMotion=button.GetComponent<UIInkMotion>(); if(oldMotion!=null) oldMotion.enabled=false;
            var oldReveal=button.GetComponent<UIInkReveal>(); if(oldReveal!=null) { oldReveal.进度=1; oldReveal.上浮=0; oldReveal.enabled=false; }
            var hit=button.GetComponent<InkUIHitShape>(); if(hit!=null) hit.enabled=false;
            button.transition=Selectable.Transition.None;
            button.image.sprite=null; button.image.overrideSprite=null; button.image.color=Color.clear;
            button.image.raycastTarget=true;
            var blot=button.transform.Find("InkNavBlot")?.GetComponent<Image>();
            if(blot==null) blot=UIBuildUtils.CreateImage("InkNavBlot",button.transform,Color.white);
            blot.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-"+(i+1)); blot.type=Image.Type.Simple;
            blot.color=InkUITheme.Ink;
            blot.preserveAspect=true; blot.raycastTarget=false; blot.transform.SetAsFirstSibling();
            var br=blot.rectTransform; br.anchorMin=br.anchorMax=new Vector2(.5f,.62f);
            br.anchoredPosition=Vector2.zero; br.sizeDelta=new Vector2(sizes[i],sizes[i]);
            var icon=button.transform.Find("InkIcon")?.GetComponent<Image>();
            if(icon!=null) {
                icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(.5f,.62f);
                icon.rectTransform.pivot=new Vector2(.5f,.5f); icon.rectTransform.anchoredPosition=Vector2.zero;
                icon.rectTransform.sizeDelta=new Vector2(38,38); icon.color=new Color(.94f,.92f,.85f); icon.raycastTarget=false;
            }
            var text=button.GetComponentInChildren<Text>(true);
            if(text!=null) {
                UIBuildUtils.Place(text.rectTransform,new Vector2(0,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,32));
                text.text=((CharacterTab)i).ToString(); text.fontSize=23; text.alignment=TextAnchor.MiddleCenter;
                text.raycastTarget=false;
                var shadow=text.GetComponent<Shadow>(); if(shadow==null) shadow=text.gameObject.AddComponent<Shadow>();
                shadow.effectColor=new Color(0,0,0,.8f); shadow.effectDistance=new Vector2(1,-1);
            }
            var stroke=button.transform.Find("InkNavStroke")?.GetComponent<Image>();
            if(stroke==null) stroke=UIBuildUtils.CreateImage("InkNavStroke",button.transform,InkUITheme.Ink);
            stroke.sprite=InkUITheme.Load("Dynamic/fx-water-streak"); stroke.raycastTarget=false;
            stroke.color=new Color(.94f,.92f,.85f,.85f);
            stroke.rectTransform.anchorMin=stroke.rectTransform.anchorMax=new Vector2(.5f,0);
            stroke.rectTransform.anchoredPosition=new Vector2(0,-2); stroke.rectTransform.sizeDelta=new Vector2(32,5);
            var fluid=button.GetComponent<UIInkFluid>(); if(fluid==null) fluid=button.gameObject.AddComponent<UIInkFluid>();
            fluid.初始化(blot,icon,text,stroke,i); fluid.选中(panel.CurrentTab==(CharacterTab)i);
        }
    }
    public void 刷新状态() {
        if(panel==null) return;
        for(int i=0;i<panel.tabs.Count;i++) {
            var b=panel.tabs[i]; if(b.button==null) continue;
            b.button.image.color=Color.clear;
            b.button.GetComponent<UIInkFluid>()?.选中(panel.CurrentTab==(CharacterTab)i);
        }
    }
}
