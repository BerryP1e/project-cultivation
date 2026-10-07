using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>运行时皮肤工具。源图保持原样；裁掉交付画布的透明留白后才做九宫格。</summary>
public static class InkUITheme
{
    public static bool Enabled
    {
        get
        {
#if UNITY_EDITOR
            return !UnityEditor.EditorPrefs.GetBool("InkUI.QA.Before", false);
#else
            return true;
#endif
        }
    }
    public static readonly Color Ink = new Color(.188f, .239f, .216f);
    [Serializable] public class SpriteRect { public string path; public int x, y, width, height; }
    [Serializable] public class Rects { public SpriteRect[] items; }
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    static Dictionary<string, SpriteRect> rects;

    public static Sprite Load(string path)
    {
        if (path == "CommonPanels/toast" || path == "CommonPanels/title-plaque") path = "Dynamic/nav-ink-blot-4";
        if (sprites.TryGetValue(path, out var cached)) return cached;
        var source = Resources.Load<Sprite>("UI/InkUI/" + path);
        if (source == null) return null;
        if (rects == null)
        {
            rects = new Dictionary<string, SpriteRect>();
            var json = Resources.Load<TextAsset>("UI/InkUI/sprite-rects");
            if (json != null)
                foreach (var item in JsonUtility.FromJson<Rects>(json.text).items) rects[item.path] = item;
        }
        if (rects.TryGetValue(path, out var r))
        {
            var border = source.border;
            border.x = Mathf.Min(border.x, r.width * .25f);
            border.z = Mathf.Min(border.z, r.width * .25f);
            border.y = Mathf.Min(border.y, r.height * .25f);
            border.w = Mathf.Min(border.w, r.height * .25f);
            source = Sprite.Create(source.texture, new Rect(r.x, r.y, r.width, r.height),
                new Vector2(.5f, .5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
            source.name = "Ink_" + path;
        }
        sprites[path] = source;
        return source;
    }

    public static void Image(Image image, string path, bool sliced = true, bool resetColor = true)
    {
        if (path == "CommonPanels/toast" || path == "CommonPanels/title-plaque") { NoticeBackground(image); return; }
        if (!Enabled) return;
        if (image == null) return;
        var sprite = Load(path);
        if (sprite == null) return;
        image.sprite = sprite;
        image.overrideSprite = null;
        if (image.type != UnityEngine.UI.Image.Type.Filled)
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = path.StartsWith("Buttons/") ? Mathf.Max(2.6f, 240f / Mathf.Max(40f, image.rectTransform.rect.width * .65f)) : 2f;
        if (resetColor) image.color = Color.white;
        if (path == "CommonPanels/plot-tag" && sprite.texture.isReadable) image.alphaHitTestMinimumThreshold = .12f;
        if (image.type == UnityEngine.UI.Image.Type.Filled) UIInkFill.Attach(image);
    }

    static Sprite noticeSprite;
    public static void NoticeBackground(Image image)
    {
        if (image == null) return;
        var source = Load("Dynamic/nav-ink-blot-4");
        if (source == null) return;
        if (noticeSprite == null)
        {
            var original=source.rect;
            // 墨点源图上下透明留白较多，先收紧绘制范围，避免九宫格把它挤成一条黑线。
            var r=new Rect(original.x+original.width*.05f,original.y+original.height*.18f,original.width*.9f,original.height*.6f);
            noticeSprite=Sprite.Create(source.texture,r,new Vector2(.5f,.5f),source.pixelsPerUnit,0,SpriteMeshType.FullRect,
                new Vector4(r.width*.25f,r.height*.18f,r.width*.25f,r.height*.18f));
            noticeSprite.name="InkNoticeBackdrop";
        }
        image.sprite=noticeSprite;image.overrideSprite=null;image.type=UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier=2;image.color=new Color(.16f,.20f,.18f,.92f);
    }

    public static void Button(Button button, string material = null)
    {
        if (!Enabled) return;
        if (button == null || button.image == null) return;
        // 遮罩上的 Button 仅负责点空白关闭；它不是一枚可换皮的操作按钮。
        if (button.name == "Dim" || button.name.Contains("幕布") || button.name == "暗色底" || button.name == "黑幕") return;
        if (button.name == "InkBagParcel") return;
        if (button.GetComponent<UIInkWaterfallCell>() != null) return;
        if (UIInkNavigation.启用 && !button.name.StartsWith("Tab_") && button.name != "Row"
            && button.GetComponent<UIActiveSkillSlot>() == null && button.image.type != UnityEngine.UI.Image.Type.Filled)
        {
            UIInkActionButton.Apply(button);
            return;
        }
        if (material == null)
        {
            var label = button.GetComponentInChildren<Text>(true);
            string action = label != null ? label.text : button.name;
            material = action.Contains("重生") || action.Contains("转修") || action.Contains("突破") || action.Contains("开炼")
                ? "cinnabar" : action.Contains("装备") || action.Contains("启用") || action.Contains("使用") || action.Contains("确认")
                  || action.Contains("修炼") || action.Contains("闭关") || action.Contains("兑换") || action.Contains("收获") ? "jade" : "ivory";
        }
        Image(button.image, "Buttons/" + material + "-normal");
        button.transition = Selectable.Transition.SpriteSwap;
        if (button.GetComponent<InkUIHitShape>() == null) button.gameObject.AddComponent<InkUIHitShape>();
        button.spriteState = new SpriteState {
            highlightedSprite = Load("Buttons/" + material + "-hover"),
            selectedSprite = Load("Buttons/" + material + "-hover"),
            pressedSprite = Load("Buttons/" + material + "-pressed"),
            disabledSprite = Load("Buttons/" + material + "-disabled")
        };
        foreach (var text in button.GetComponentsInChildren<Text>(true)) text.color = material == "ivory" || !button.interactable ? Ink : new Color(.98f, .95f, .85f);
        UIInkMotion.Attach(button.gameObject, UIInkMotion.Kind.Card);
        UIInkMotion.干笔(button);
    }

    // selectedSprite 是 EventSystem 的键盘焦点状态，不能冒充数据里的“当前选中”。
    public static void Choice(Button button, string prefix, bool selected)
    {
        if (!Enabled) return;
        if (button == null || button.image == null) return;
        string normal = prefix + (selected ? "-selected" : "-normal");
        Image(button.image, normal);
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState {
            highlightedSprite = Load(selected ? normal : prefix + "-hover"),
            selectedSprite = Load(normal), pressedSprite = Load(prefix + "-selected"), disabledSprite = Load(normal)
        };
        UIInkMotion.Attach(button.gameObject, UIInkMotion.Kind.Row)?.选中(selected);
    }

    public static void Scroll(ScrollRect scroll, float titleHeight = 34)
    {
        if (!Enabled) return;
        if (scroll == null || !scroll.vertical) return;
        var bar = scroll.verticalScrollbar;
        if (bar == null) bar = UIBuildUtils.AddVerticalScrollbar(scroll, 16, titleHeight, 8);
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.scrollSensitivity = 42;
        scroll.inertia = true;
        scroll.decelerationRate = .135f;
        ScrollbarStyle(bar);
        if (scroll.viewport != null)
        {
            var hit = scroll.viewport.GetComponent<Image>();
            if (hit == null) hit = scroll.viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
        }
        if (scroll.content != null)
            foreach (var graphic in scroll.content.GetComponentsInChildren<Graphic>(true))
                if (graphic.GetComponent<UIInkScrollFade>() == null) graphic.gameObject.AddComponent<UIInkScrollFade>();
    }
    public static void ScrollbarStyle(Scrollbar bar)
    {
        if(bar==null)return;
        var track=bar.GetComponent<Image>();
        if(track!=null) {track.sprite=Load("Dynamic/fx-ink-blot");track.type=UnityEngine.UI.Image.Type.Simple;track.color=new Color(.22f,.28f,.25f,.55f);}
        var thumb=bar.handleRect!=null ? bar.handleRect.GetComponent<Image>() : null;
        if(thumb!=null) {thumb.sprite=Load("Dynamic/nav-ink-blot-4");thumb.type=UnityEngine.UI.Image.Type.Simple;thumb.color=new Color(.75f,.82f,.74f,.9f);}
        bar.transition=Selectable.Transition.None;
    }
}
