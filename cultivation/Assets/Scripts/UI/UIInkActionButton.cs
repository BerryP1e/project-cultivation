using UnityEngine;
using UnityEngine.UI;

/// <summary>操作按钮共用墨点标签。保留原按钮命中框、文案、回调和禁用逻辑。</summary>
public class UIInkActionButton : MonoBehaviour
{
    Button button;
    Image blot;
    UIInkFluid fluid;
    Vector2 size;
    Vector2 drawingScale=new Vector2(1.45f,2.8f);
    public void 设置绘制倍率(Vector2 value)
    {
        drawingScale=value;
        if(blot!=null) blot.rectTransform.sizeDelta=Vector2.Scale(size,drawingScale)*1.4f;
    }
    public static void Apply(Button target)
    {
        var effect=target.GetComponent<UIInkActionButton>();
        if(effect==null) effect=target.gameObject.AddComponent<UIInkActionButton>();
        effect.Build(target);
    }
    void Build(Button target)
    {
        button=target;
        var oldMotion=GetComponent<UIInkMotion>(); if(oldMotion!=null) oldMotion.enabled=false;
        var oldHit=GetComponent<InkUIHitShape>(); if(oldHit!=null) oldHit.enabled=false;
        button.transition=Selectable.Transition.None;
        button.image.overrideSprite=null; button.image.sprite=null; button.image.color=Color.clear;
        button.image.alphaHitTestMinimumThreshold=0; button.image.raycastTarget=true;
        if(blot==null) {
            blot=UIBuildUtils.CreateImage("InkActionBlot",transform,InkUITheme.Ink);
            blot.transform.SetAsFirstSibling(); blot.raycastTarget=false;
            blot.sprite=InkUITheme.Load("Dynamic/nav-ink-blot-4");
            fluid=gameObject.AddComponent<UIInkFluid>();
            // Lightweight shared silhouette deformation: hundreds of action buttons
            // need not allocate the persistent density solver used by navigation.
            fluid.使用密度模拟=false;
            var label=button.GetComponentInChildren<Text>(true);
            size=((RectTransform)transform).rect.size;
            blot.rectTransform.anchorMin=blot.rectTransform.anchorMax=new Vector2(.5f,.5f);
            blot.rectTransform.sizeDelta=new Vector2(size.x*1.45f,size.y*2.8f);
            fluid.初始化(blot,null,label,null,button.name.Length);
            fluid.选中(true);
        }
        foreach(var text in button.GetComponentsInChildren<Text>(true)) {
            text.color=new Color(.98f,.96f,.90f); text.raycastTarget=false;
            var shadow=text.GetComponent<Shadow>(); if(shadow==null) shadow=text.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(0,0,0,.7f); shadow.effectDistance=new Vector2(1,-1);
        }
    }
    void LateUpdate()
    {
        if(button==null || blot==null) return;
        var current=((RectTransform)transform).rect.size;
        if(current!=size) { size=current; blot.rectTransform.sizeDelta=Vector2.Scale(size,drawingScale)*1.4f; }
        bool enabledAction=button.IsInteractable();
        fluid.基础浓度=enabledAction ? 2.2f : .7f;
        blot.color=enabledAction ? InkUITheme.Ink : new Color(.43f,.46f,.43f);
    }
}
