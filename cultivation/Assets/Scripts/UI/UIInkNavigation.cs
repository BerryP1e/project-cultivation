using UnityEngine;
using UnityEngine.UI;

/// <summary>常驻右下角背包、装备、坐骑导航，独立于可关闭的角色页面。</summary>
[DisallowMultipleComponent]
public class UIInkNavigation : MonoBehaviour
{
    CharacterPanelUI panel;RectTransform dock;readonly CharacterTab[] visible={CharacterTab.背包,CharacterTab.神通,CharacterTab.坐骑};
    public static bool 启用 {
        get {
#if UNITY_EDITOR
            return InkUITheme.Enabled&&!UnityEditor.EditorPrefs.GetBool("InkUI.QA.DynamicBefore",false);
#else
            return InkUITheme.Enabled;
#endif
        }
    }
    public static void 应用(Transform root)
    {
        if(!启用||!root)return;var panel=root.GetComponent<CharacterPanelUI>();if(!panel||panel.tabs.Count<8)return;
        var nav=root.GetComponent<UIInkNavigation>()??root.gameObject.AddComponent<UIInkNavigation>();nav.panel=panel;nav.Build();
    }
    void Build()
    {
        panel.defaultTab=CharacterTab.背包;
        if(dock){刷新状态();return;}
        if(!dock){dock=UIBuildUtils.CreateRect("CharacterNavigationDock",transform);dock.anchorMin=dock.anchorMax=new Vector2(1,0);dock.pivot=Vector2.one;dock.anchoredPosition=new Vector2(-212,148);dock.sizeDelta=new Vector2(264,88);var group=dock.gameObject.AddComponent<Canvas>();group.overrideSorting=true;group.sortingOrder=GetComponentInParent<Canvas>().sortingOrder+10;dock.gameObject.AddComponent<GraphicRaycaster>();}
        for(int i=0;i<panel.tabs.Count;i++){
            var button=panel.tabs[i].button;if(!button)continue;int index=System.Array.IndexOf(visible,(CharacterTab)i);
            if(index<0){button.gameObject.SetActive(false);continue;}
            var icon=button.transform.Find("InkIcon")?.GetComponent<Image>();
            button.transform.SetParent(dock,false);button.gameObject.SetActive(true);var rt=(RectTransform)button.transform;UIPaperArt.Place(rt,(float)index/visible.Length,0,(float)(index+1)/visible.Length,1);rt.offsetMin=new Vector2(3,0);rt.offsetMax=new Vector2(-3,0);
            foreach(Transform child in button.transform)child.gameObject.SetActive(false);
            var text=button.GetComponentInChildren<Text>(true);if(text){text.gameObject.SetActive(true);text.text=visible[index]==CharacterTab.神通?"装备":visible[index].ToString();text.fontSize=18;text.alignment=TextAnchor.MiddleCenter;UIPaperArt.Place(text.rectTransform,0,.04f,1,.36f);var shadow=text.GetComponent<Shadow>();if(shadow)shadow.enabled=false;}
            if(!icon)icon=UIBuildUtils.CreateImage("InkIcon",button.transform,Color.white);icon.gameObject.SetActive(true);string art=visible[index]==CharacterTab.背包?"bag":visible[index]==CharacterTab.神通?"equipment":"mount";icon.sprite=Resources.Load<Sprite>("UI/InkUI/Navigation/nav-"+art);icon.color=Color.white;icon.preserveAspect=true;icon.raycastTarget=false;UIPaperArt.Place(icon.rectTransform,.28f,.40f,.72f,.91f);
            var oldMotion=button.GetComponent<UIInkMotion>();if(oldMotion)oldMotion.enabled=false;
            var oldReveal=button.GetComponent<UIInkReveal>();if(oldReveal){oldReveal.进度=1;oldReveal.enabled=false;}
            var hit=button.GetComponent<InkUIHitShape>();if(hit)hit.enabled=false;
            button.transition=Selectable.Transition.None;button.image.sprite=null;button.image.overrideSprite=null;button.image.color=Color.clear;button.image.raycastTarget=true;
            var blot=button.transform.Find("InkNavBlot")?.GetComponent<Image>()??UIBuildUtils.CreateImage("InkNavBlot",button.transform,InkUITheme.Ink);
            blot.gameObject.SetActive(true);blot.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-"+(i+1));blot.preserveAspect=true;blot.raycastTarget=false;blot.transform.SetAsFirstSibling();blot.rectTransform.anchorMin=blot.rectTransform.anchorMax=new Vector2(.5f,.62f);blot.rectTransform.anchoredPosition=Vector2.zero;blot.rectTransform.sizeDelta=Vector2.one*82;
            icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(.5f,.62f);icon.rectTransform.pivot=new Vector2(.5f,.5f);icon.rectTransform.sizeDelta=Vector2.one*40;icon.rectTransform.anchoredPosition=Vector2.zero;
            var stroke=button.transform.Find("InkNavStroke")?.GetComponent<Image>()??UIBuildUtils.CreateImage("InkNavStroke",button.transform,UIPaperArt.Ivory);
            stroke.gameObject.SetActive(true);stroke.sprite=InkUITheme.Load("Dynamic/fx-water-streak");stroke.raycastTarget=false;UIPaperArt.Place(stroke.rectTransform,.30f,0,.70f,.045f);
            var fluid=button.GetComponent<UIInkFluid>()??button.gameObject.AddComponent<UIInkFluid>();fluid.enabled=true;fluid.初始化(blot,icon,text,stroke,i);fluid.选中(false);
            var tab=visible[index];button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{if(!panel.IsOpen)panel.SetOpen(true);panel.ShowTab(tab);});
        }
        var sidebar=panel.panelRoot.transform.Find("Window/Sidebar");if(sidebar)sidebar.gameObject.SetActive(false);
        var window=panel.panelRoot.transform.Find("Window");if(window){var background=window.GetComponent<Image>();if(background)background.enabled=false;var motion=window.GetComponent<UIInkMotion>();if(motion)motion.enabled=false;var reveal=window.GetComponent<UIInkReveal>();if(reveal){reveal.进度=1;reveal.enabled=false;}var group=window.GetComponent<CanvasGroup>();if(group)group.alpha=1;foreach(var clip in window.GetComponentsInChildren<UIInkClipRect>(true))if(clip.面板==reveal)clip.enabled=false;}
        if(!GetComponent<UIPaperCursor>())gameObject.AddComponent<UIPaperCursor>();dock.SetAsLastSibling();刷新状态();
    }
    public void 刷新状态(){if(!panel)return;if(dock)dock.gameObject.SetActive(!panel.IsOpen&&!panel.过渡中);foreach(var tab in visible){var button=panel.tabs[(int)tab].button;if(!button)continue;button.image.color=Color.clear;button.GetComponent<UIInkFluid>()?.选中(panel.IsOpen&&(panel.CurrentTab==tab||tab==CharacterTab.神通&&panel.CurrentTab==CharacterTab.法则));}}
    void LateUpdate(){刷新状态();}
}
