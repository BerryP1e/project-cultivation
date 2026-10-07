using UnityEngine;
using UnityEngine.UI;

/// <summary>灵田沿用堂阁界面的细线边框与半透明深色底。</summary>
public static class SpiritFieldPanelStyle
{
    public static readonly Color TextColor = new Color(.91f,.89f,.8f);
    public static void Frame(Transform parent)
    {
        var rt=UIBuildUtils.CreateRect("纹饰细框",parent); UIBuildUtils.Stretch(rt,1);
        var frame=rt.gameObject.AddComponent<UISectFineFrame>();
        frame.color=new Color(.72f,.64f,.43f,.65f);frame.raycastTarget=false;
    }
    public static void Button(Button button, bool selected=false)
    {
        var image=button.GetComponent<Image>();image.sprite=null;image.type=Image.Type.Simple;
        image.color=selected?new Color(.21f,.27f,.23f,.95f):new Color(.07f,.11f,.11f,.82f);
        foreach(var text in button.GetComponentsInChildren<Text>())text.color=TextColor;
        var colors=button.colors; colors.highlightedColor=new Color(1.25f,1.25f,1.15f);colors.pressedColor=new Color(.7f,.8f,.75f);colors.disabledColor=new Color(.45f,.45f,.45f);button.colors=colors;
        if(button.transform.Find("纹饰细框")==null)Frame(button.transform);
    }
}
