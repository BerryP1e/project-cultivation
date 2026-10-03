using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 的运行时水墨皮肤。
///
/// PlayerHud 仍然负责数值、冷却、悬停和数据绑定；本组件只替换底图，
/// 因而不会改变快捷键 1–6、冷却竖向填充或气血/灵气/修炼进度的逻辑。
/// </summary>
[DisallowMultipleComponent]
public class UIInkHudSkin : MonoBehaviour
{
    public bool 启用 = true;

    const string 根 = "UI/InkUI";
    const float 九宫倍率 = 2f;
    static readonly Dictionary<string, Sprite> 缓存 = new Dictionary<string, Sprite>();

    void Start() => 刷新();

    public void 刷新()
    {
        if (!启用 || !InkUITheme.Enabled) return;

        var hud = GetComponent<PlayerHud>();
        var root = hud != null ? hud.transform : transform;
        foreach (Transform child in root)
        {
            var rt = child as RectTransform;
            if (rt == null) continue;
            if (child.name == "功法") { rt.anchoredPosition = new Vector2(22, 88); rt.sizeDelta = new Vector2(96, 96); }
            if (child.name.StartsWith("Skill") && int.TryParse(child.name.Substring(5), out int slot))
            { rt.anchoredPosition = new Vector2(132 + (slot - 1) * 74, 104); rt.sizeDelta = new Vector2(68, 68);
              if (child.GetComponent<InkUIHitShape>() == null) child.gameObject.AddComponent<InkUIHitShape>(); }
            if (child.name == "气血" || child.name == "灵力" || child.name == "修炼")
            { rt.anchoredPosition = new Vector2(22, child.name == "气血" ? 64 : child.name == "灵力" ? 42 : 20); rt.sizeDelta = new Vector2(550, 18); }
        }
        foreach (var text in root.GetComponentsInChildren<Text>(true))
            if (text.name == "按键" || text.name == "文字") text.fontSize = 17;
        if (hud != null)
        {
            UIInkNumber.Attach(hud.气血文字); UIInkNumber.Attach(hud.灵力文字);
            UIInkFill.Attach(hud.气血填充); UIInkFill.Attach(hud.灵力填充); UIInkFill.Attach(hud.修炼填充);
            if (hud.幕布名称 != null) hud.幕布名称.color = InkUITheme.Ink;
            if (hud.幕布品阶 != null) hud.幕布品阶.color = InkUITheme.Ink;
            if (hud.幕布正文 != null) hud.幕布正文.color = InkUITheme.Ink;
            if (hud.信息幕布 != null) { var rt = hud.信息幕布.transform as RectTransform; rt.anchoredPosition = new Vector2(22, 210); }
        }
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            if (image == null) continue;
            string name = image.gameObject.name;
            string parent = image.transform.parent != null ? image.transform.parent.name : "";
            if (name == "图标" && parent.StartsWith("Skill"))
            { UIBuildUtils.Stretch(image.rectTransform, 16); image.preserveAspect = true; }

            // 功法印章用独立异形墨环；六枚技能格使用透明区可命中的玉符。
            if (name == "底" && parent == "功法")
            {
                套整图(image, "CultivationAlchemy/ink-ring");
                continue;
            }
            if (name == "底" && parent.StartsWith("Skill"))
            {
                套整图(image, "SkillsPage/skills/slot-empty");
                image.raycastTarget = true;
                continue;
            }

            // 三条 HUD 进度条：底是九宫轨道，填充保持 Filled。
            if (name == "底" && (parent == "气血" || parent == "灵力" || parent == "修炼"))
            {
                套九宫(image, "SkillsPage/status/bar-track");
                continue;
            }
            if (name == "填充" && (parent == "气血" || parent == "灵力" || parent == "修炼"))
            {
                套填充(image);
                continue;
            }

            // 冷却遮罩需要 sprite 才能让 fillAmount 生效，方向仍由 PlayerHud 保持 Vertical/Top。
            if (name == "冷却遮罩")
            {
                套填充(image);
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Vertical;
                image.fillOrigin = (int)Image.OriginVertical.Top;
                continue;
            }

            // 悬停信息幕布是独立纸卷，不把文字和装饰烘焙进底图。
            if (name == "信息幕布")
            {
                套九宫(image, "CommonPanels/dialog-scroll");
                image.raycastTarget = false;
            }
        }
    }

    static void 套九宫(Image image, string path)
    {
        var sprite = 图(path);
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 九宫倍率;
        image.color = Color.white;
    }

    static void 套整图(Image image, string path)
    {
        var sprite = 图(path);
        if (sprite == null) return;
        image.sprite = sprite;
        if (image.type != Image.Type.Filled) image.type = Image.Type.Simple;
        image.color = Color.white;
    }

    static void 套填充(Image image)
    {
        var sprite = 图("SkillsPage/status/bar-fill");
        if (sprite == null) return;
        image.sprite = sprite;
        image.color = Color.white;
        // 调用方会把冷却遮罩重新设回 Filled/Vertical；普通条在这里统一横向。
        if (image.type != Image.Type.Filled) image.type = Image.Type.Filled;
        if (image.fillMethod != Image.FillMethod.Vertical)
        {
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }

    static Sprite 图(string path)
    {
        Sprite sprite;
        if (缓存.TryGetValue(path, out sprite)) return sprite;
        sprite = InkUITheme.Load(path);
        缓存[path] = sprite;
        if (sprite == null) Debug.LogWarning("[UIInkHudSkin] 找不到素材：" + 根 + "/" + path);
        return sprite;
    }

    public void Refresh() => 刷新();
}
