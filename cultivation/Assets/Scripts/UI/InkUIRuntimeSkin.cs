using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>运行时生成界面的共享换皮。只处理登记过的画布及新增节点，不改业务事件。</summary>
[DisallowMultipleComponent]
public class InkUIRuntimeSkin : MonoBehaviour
{
    static readonly HashSet<string> CanvasNames = new HashSet<string> {
        "CultivationCanvas", "AlchemyCanvas", "灵田地块界面", "灵田总览", "功德堂Canvas", "对话界面",
        "PauseMenuCanvas", "MenuCanvas", "DeathScreenCanvas", "起名界面", "ToastCanvas", "TowerCanvas", "传送面板",
        "QuestGuideCanvas", "ChronicleCanvas", "StationHint", "StationUIPlaceholder"
    };
    readonly HashSet<int> handled = new HashSet<int>();
    float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!InkUITheme.Enabled) return;
        var host = new GameObject("InkUIRuntimeRegistry");
        DontDestroyOnLoad(host);
        host.AddComponent<InkUIRuntimeSkin>().StartCoroutine(WatchCanvases());
    }
    static IEnumerator WatchCanvases()
    {
        var delay = new WaitForSecondsRealtime(.5f);
        while (true)
        {
            foreach (var canvas in Object.FindObjectsOfType<Canvas>(true)) Ensure(canvas.transform);
            yield return delay;
        }
    }
    public static void Ensure(Transform parent)
    {
        if (!Application.isPlaying || !InkUITheme.Enabled || parent == null) return;
        var canvas = parent.GetComponentInParent<Canvas>(true);
        if (canvas == null || !CanvasNames.Contains(canvas.name)) return;
        if (canvas.GetComponent<InkUIRuntimeSkin>() == null) canvas.gameObject.AddComponent<InkUIRuntimeSkin>();
    }
    void Start()
    {
        if (GetComponent<Canvas>() == null) return;
        if (name == "灵田地块界面")
        {
            var panel = transform.Find("面板") as RectTransform;
            if (panel != null && panel.Find("InkContent") == null)
            {
                panel.sizeDelta = new Vector2(640, 740);
                var children = new List<RectTransform>();
                foreach (Transform child in panel) if (child is RectTransform rt) children.Add(rt);
                var content = UIBuildUtils.CreateRect("InkContent", panel);
                content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
                content.offsetMin = new Vector2(48, 54); content.offsetMax = new Vector2(-48, -70);
                foreach (var child in children) child.SetParent(content, false);
            }
        }
        if (name == "ChronicleCanvas")
        {
            var chronicle = GetComponentInParent<纪年HUD>();
            if (chronicle != null) { chronicle.日期色 = InkUITheme.Ink; chronicle.机会色 = new Color(.19f, .38f, .38f); chronicle.跨天色 = new Color(.65f, .33f, .13f); }
            var row = transform.Find("右上行") as RectTransform;
            if (row != null) InkUITheme.Image(row.gameObject.AddComponent<Image>(), "CommonPanels/toast");
        }
        Refresh();
        if (name == "PauseMenuCanvas" || name == "DeathScreenCanvas" || name == "MenuCanvas" || name == "起名界面")
            UIInkMotion.Attach(gameObject, UIInkMotion.Kind.Panel);
    }
    void Update()
    {
        if (GetComponent<Canvas>() == null || Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + .25f;
        Refresh();
    }
    public void Refresh()
    {
        if (!InkUITheme.Enabled) return;
        // 对话和传送已使用独立墨边布局，避免登记器重新套回旧纸卷/金框。
        if (GetComponent<DialogueUI>() != null || GetComponent<UIInkTeleportPanel>() != null) return;
        string canvas = name;
        foreach (var image in GetComponentsInChildren<Image>(true))
        {
            if (!handled.Add(image.GetInstanceID())) continue;
            string n = image.name;
            string path = Map(canvas, image);
            if (path != null) InkUITheme.Image(image, path, !path.EndsWith("furnace-stage"));
            if (path != null && (image.name == "主面板" || image.name == "内容框" || image.name == "面板" || image.name == "功德堂" || image.name == "对话框" || image.name == "底板" || image.name == "SavePanel"))
                UIInkMotion.Attach(image.gameObject, UIInkMotion.Kind.Panel);
        }
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (!handled.Add(button.GetInstanceID())) continue;
            if (button.name.StartsWith("页签_") || button.name.StartsWith("功法行") || button.name.Contains("材格") || button.name == "预览方块") continue;
            if (button.name.StartsWith("回答")) InkUITheme.Choice(button, "CommonPanels/dialog-choice", false);
            else if (button.name.StartsWith("丹方")) InkUITheme.Choice(button, "CultivationAlchemy/recipe-row", false);
            else InkUITheme.Button(button);
        }
        foreach (var scroll in GetComponentsInChildren<ScrollRect>(true))
            if (handled.Add(scroll.GetInstanceID())) InkUITheme.Scroll(scroll);
        bool paper = canvas != "DeathScreenCanvas" && canvas != "PauseMenuCanvas" && canvas != "起名界面" && canvas != "MenuCanvas" && canvas != "TowerCanvas";
        foreach (var text in GetComponentsInChildren<Text>(true))
        {
            if (!handled.Add(text.GetInstanceID())) continue;
            if (text.name.Contains("余额") || text.name.Contains("贡献") || text.name == "灵气数值" || text.name == "进度文字") UIInkNumber.Attach(text);
            if (text.name == "塔层HUD") { Backdrop(text, "CommonPanels/tower-floor-bar"); text.color = new Color(.98f, .95f, .85f); }
            if ((canvas == "PauseMenuCanvas" || canvas == "DeathScreenCanvas" || canvas == "起名界面" || canvas == "StationUIPlaceholder") && text.name == "标题")
            { Backdrop(text, "CommonPanels/title-plaque"); text.color = InkUITheme.Ink; }
            if (!paper) continue;
            if (text.name.Contains("立绘") || text.transform.parent.name.Contains("立绘")) continue;
            if (text.GetComponentInParent<Button>(true) != null) continue;
            if (text.color.maxColorComponent > .55f) text.color = InkUITheme.Ink;
            var outline = text.GetComponent<Outline>(); if (outline != null) outline.enabled = false;
        }
    }
    static void Backdrop(Text text, string path)
    {
        var old = text.transform.parent.Find("InkBackdrop_" + text.name);
        if (old != null) return;
        var image = UIBuildUtils.CreateImage("InkBackdrop_" + text.name, text.transform.parent, Color.white);
        var source = text.rectTransform; var rt = image.rectTransform;
        rt.anchorMin = source.anchorMin; rt.anchorMax = source.anchorMax; rt.pivot = source.pivot;
        rt.sizeDelta = source.sizeDelta + new Vector2(24, 24); rt.anchoredPosition = source.anchoredPosition;
        InkUITheme.Image(image, path); rt.SetSiblingIndex(text.transform.GetSiblingIndex()); image.raycastTarget = false;
    }
    static string Map(string canvas, Image image)
    {
        string n = image.name;
        if (n.Contains("幕布") || n == "暗色底" || n == "黑幕" || n == "死亡选择") return null;
        if (n.Contains("填充") || n == "Fill") return "SkillsPage/status/bar-fill";
        if (n.Contains("进度底") || n == "Track") return "SkillsPage/status/bar-track";
        if (canvas == "CultivationCanvas")
        {
            if (n == "主面板") return "CultivationAlchemy/cultivation-frame";
            if (n == "功法信息") return "SkillsPage/common/panel-inner";
            if (n.StartsWith("功法行")) return "CultivationAlchemy/gongfa-card";
            if (n.StartsWith("页签_")) return "SkillsPage/navigation/tab-normal";
            if (n == "突破所需物品信息" || n == "当前学会的功法" || n == "转修后境界预估") return "SkillsPage/common/panel-inner";
        }
        if (canvas == "AlchemyCanvas")
        {
            if (n == "内容框") return "SkillsPage/common/panel-sheet";
            if (n == "成品预览底") return "CultivationAlchemy/furnace-stage";
            if (n == "信息板底" || n == "丹方板底" || n == "背包板底") return "SkillsPage/common/panel-inner";
            if (n.Contains("材格")) return "CultivationAlchemy/material-slot-empty";
        }
        if (canvas == "灵田地块界面" && n == "面板") return "CommonPanels/plot-tag";
        if (canvas == "灵田总览" && n == "面板") return "SkillsPage/common/panel-sheet";
        if (canvas == "功德堂Canvas" && n == "功德堂") return "SkillsPage/common/panel-sheet";
        if (canvas == "对话界面")
        {
            if (n == "对话框") return "CommonPanels/dialog-scroll";
            if (n == "名字底") return "CommonPanels/dialog-nameplate";
        }
        if (canvas == "传送面板" && n == "底板") return "CommonPanels/teleport-plate";
        if (canvas == "ToastCanvas" && n == "底") return "CommonPanels/toast";
        if (canvas == "MenuCanvas" && n == "SavePanel") return "SkillsPage/common/panel-sheet";
        if (canvas == "MenuCanvas" && n.StartsWith("Slot")) return "SkillsPage/skills/row-normal";
        if (canvas == "QuestGuideCanvas" && n == "主线追踪") return "CommonPanels/dialog-scroll";
        if (canvas == "StationHint" && n == "底") return "CommonPanels/toast";
        return null;
    }
}
