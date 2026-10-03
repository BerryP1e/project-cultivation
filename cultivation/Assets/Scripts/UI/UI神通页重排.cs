using UnityEngine;
using UnityEngine.UI;

/// <summary>只在运行时调整布局，保留角色父页和共享数据，不保存场景。</summary>
public static class UI神通页重排
{
    public static void 应用(Transform root)
    {
        var window = Find(root, "Window") as RectTransform;
        if (window == null) return;
        window.sizeDelta = new Vector2(1780, 970);
        InkUITheme.Image(window.GetComponent<Image>(), "Skeleton/parent-curtain-9slice-source");
        var side = Find(window, "Sidebar") as RectTransform;
        if (side != null)
        {
            UIBuildUtils.Place(side, Vector2.zero, new Vector2(0, 1), new Vector2(24, 28), new Vector2(236, -28));
            InkUITheme.Image(side.GetComponent<Image>(), "Skeleton/parent-navigation-rail");
            int index = 0;
            foreach (Transform tab in side)
            {
                if (!tab.name.StartsWith("Tab_")) continue;
                var rt = tab as RectTransform;
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, 1);
                rt.sizeDelta = new Vector2(-16, 70); rt.anchoredPosition = new Vector2(0, -52 - index++ * 96);
            }
        }
        var content = window.Find("Content") as RectTransform;
        if (content != null) { content.offsetMin = new Vector2(250, 30); content.offsetMax = new Vector2(-30, -30); }
        var footer = Find(window, "HintBar"); if (footer != null) footer.gameObject.SetActive(false);
        foreach (var bar in root.GetComponentsInChildren<UIActiveSkillBar>(true)) LayoutBar(bar);
        foreach (var list in root.GetComponentsInChildren<UIEntryList>(true))
        {
            InkUITheme.Scroll(list.GetComponent<ScrollRect>());
            if (list.source == ListSource.战阵成员) list.rowHeight = 56;
        }
        foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true)) InkUITheme.Scroll(scroll);
        var page = Find(root, "Page_神通"); if (page == null) return;
        Place(Find(page, "ActiveSkillBar") as RectTransform, 0, .49f, .69f, 1);
        Place(Find(page, "KnownList") as RectTransform, 0, 0, .38f, .48f);
        Place(Find(page, "PassiveList") as RectTransform, .38f, 0, .69f, .48f);
        Place(Find(page, "Info") as RectTransform, .69f, 0, 1, 1);
        var known = Find(page, "KnownList").GetComponent<UIEntryList>();
        known.inkCards = true; known.rowHeight = 158;
        var vertical = known.container.GetComponent<VerticalLayoutGroup>();
        if (vertical != null) { vertical.enabled = false; Object.DestroyImmediate(vertical); }
        var grid = known.container.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = known.container.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
        grid.cellSize = new Vector2(164, 158); grid.spacing = new Vector2(8, 8); grid.padding = new RectOffset(12, 12, 12, 12);
        Title(known.transform, "已悟神通"); known.RebuildFromSource();
        var passive = Find(page, "PassiveList").GetComponent<UIEntryList>();
        passive.rowHeight = 86; passive.infoTarget = known.infoTarget;
        Title(passive.transform, "生效中的被动神通"); passive.RebuildFromSource();
        LayoutDetail(Find(page, "Info").GetComponent<UIEntryInfo>());
    }

    public static void LayoutBar(UIActiveSkillBar bar)
    {
        var ring = bar.transform.Find("InkRing") as RectTransform;
        if (ring == null)
        {
            ring = UIBuildUtils.CreateRect("InkRing", bar.transform);
            ring.gameObject.AddComponent<Image>().raycastTarget = false; ring.SetAsFirstSibling();
        }
        ring.anchorMin = ring.anchorMax = new Vector2(.5f, .55f); ring.sizeDelta = new Vector2(380, 380);
        InkUITheme.Image(ring.GetComponent<Image>(), "SkillsPage/skills/active-ring", false);
        var obsolete = bar.GetComponent<Image>(); if (obsolete != null) obsolete.enabled = false;
        var numbers = bar.transform.Find("InkNumbers") as RectTransform;
        if (numbers == null)
        {
            numbers = UIBuildUtils.CreateRect("InkNumbers", bar.transform);
            UIBuildUtils.Stretch(numbers);
        }
        foreach (var slot in bar.slots)
        {
            if (slot == null) continue;
            float angle = slot.index * Mathf.PI / 3;
            var pos = new Vector2(Mathf.Sin(angle) * 143, Mathf.Cos(angle) * 143);
            var srt = slot.transform as RectTransform;
            srt.anchorMin = srt.anchorMax = new Vector2(.5f, .55f); srt.pivot = new Vector2(.5f, .5f);
            srt.sizeDelta = new Vector2(94, 94); srt.anchoredPosition = pos;
            if (slot.button != null) slot.button.transition = Selectable.Transition.ColorTint;
            if (slot.background != null) slot.background.overrideSprite = null;
            if (slot.icon != null) UIBuildUtils.Stretch(slot.icon.rectTransform, 22);
            if (slot.GetComponent<InkUIHitShape>() == null) slot.gameObject.AddComponent<InkUIHitShape>();
            var oldHover = slot.GetComponent<InkUIHoverMotion>();
            if (oldHover != null) oldHover.enabled = false;
            UIInkMotion.Attach(slot.gameObject, UIInkMotion.Kind.Slot, slot.icon != null ? slot.icon.transform : null);
            if (slot.label != null)
            {
                var label = slot.label.rectTransform;
                label.SetParent(bar.transform, false);
                label.anchorMin = label.anchorMax = new Vector2(.5f, .55f);
                label.anchoredPosition = pos + new Vector2(0, -62); label.sizeDelta = new Vector2(132, 26); slot.label.fontSize = 16;
            }
            if (slot.clearButton != null)
            {
                var clear = slot.clearButton.transform as RectTransform;
                clear.SetParent(bar.transform, false);
                clear.anchorMin = clear.anchorMax = new Vector2(.5f, .55f);
                clear.anchoredPosition = pos + new Vector2(36, 34); clear.sizeDelta = new Vector2(28, 28);
            }
            // 编号有独立的不透明底托，避免墨字与玉符花纹混在一起。
            var badge = numbers.Find("InkSlotNumberBadge_" + slot.index) as RectTransform;
            if (badge == null)
            {
                badge = UIBuildUtils.CreateImage("InkSlotNumberBadge_" + slot.index, numbers, new Color(.46f, .36f, .20f)).rectTransform;
                var paper = UIBuildUtils.CreateImage("Paper", badge, new Color(.98f, .96f, .88f));
                UIBuildUtils.Stretch(paper.rectTransform, 1.5f);
            }
            badge.anchorMin = badge.anchorMax = new Vector2(.5f, .55f); badge.pivot = new Vector2(.5f, .5f);
            badge.anchoredPosition = pos + new Vector2(54, 0); badge.sizeDelta = new Vector2(42, 40);
            var number = numbers.Find("InkSlotNumber_" + slot.index)?.GetComponent<Text>();
            if (number == null) number = slot.transform.Find("编号")?.GetComponent<Text>();
            if (number == null) number = UIBuildUtils.CreateText("编号", slot.transform,
                bar.GetComponentInChildren<Text>(true).font, (slot.index + 1).ToString(), 22, TextAnchor.MiddleCenter, InkUITheme.Ink);
            var nrt = number.rectTransform;
            number.transform.SetParent(numbers, false); number.name = "InkSlotNumber_" + slot.index;
            number.text = (slot.index + 1).ToString(); number.color = InkUITheme.Ink; number.fontStyle = FontStyle.Bold; number.transform.SetAsLastSibling();
            number.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); number.fontSize = 24;
            foreach (var font in Resources.FindObjectsOfTypeAll<Font>())
                if (font != null && font.name.ToLowerInvariant().Contains("simhei")) { number.font = font; break; }
            number.enabled = true; number.gameObject.SetActive(true); number.raycastTarget = false;
            nrt.anchorMin = nrt.anchorMax = new Vector2(.5f, .55f); nrt.pivot = new Vector2(.5f, .5f);
            nrt.anchoredPosition = pos + new Vector2(54, 0); nrt.sizeDelta = new Vector2(42, 40);
        }
        numbers.SetAsLastSibling();
        var hint = bar.transform.Find("Hint")?.GetComponent<Text>();
        if (hint != null)
        {
            hint.text = "主动技能栏\n神通 · 法宝 · 灵阵共用"; hint.fontSize = 19; hint.alignment = TextAnchor.MiddleCenter;
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, .55f);
            hint.rectTransform.pivot = new Vector2(.5f, .5f); hint.rectTransform.anchoredPosition = Vector2.zero;
            hint.rectTransform.sizeDelta = new Vector2(180, 70);
        }
        bar.Refresh();
    }

    static void LayoutDetail(UIEntryInfo info)
    {
        if (info == null) return;
        InkUITheme.Image(info.GetComponent<Image>(), "SkillsPage/skills/panel-detail");
        var title = info.transform.Find("Title"); if (title != null) title.gameObject.SetActive(false);
        Top(info.nameText?.rectTransform, 70, 52, 72); if (info.nameText != null) info.nameText.fontSize = 34;
        Top(info.tierText?.rectTransform, 126, 32, 72); Top(info.kindText?.rectTransform, 166, 28, 72);
        if (info.descriptionText != null)
        {
            UIBuildUtils.Place(info.descriptionText.rectTransform, Vector2.zero, Vector2.one, new Vector2(72, 142), new Vector2(-72, -238));
            info.descriptionText.fontSize = 21; info.descriptionText.alignment = TextAnchor.UpperLeft;
        }
        if (info.actionButton != null)
            UIBuildUtils.Place(info.actionButton.transform as RectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(40, 48), new Vector2(-40, 110));
        if (info.actionLabel != null) info.actionLabel.fontSize = 25;
        UIInkAbilityArt.RefreshDetail(info);
    }
    static void Top(RectTransform rt, float y, float height, float padding)
    {
        if (rt != null) UIBuildUtils.Place(rt, new Vector2(0, 1), Vector2.one, new Vector2(padding, -y-height), new Vector2(-padding, -y));
    }
    static void Title(Transform root, string text)
    {
        var t = root.Find("Title")?.GetComponent<Text>(); if (t == null) return;
        t.text = text; t.fontSize = 24; t.alignment = TextAnchor.MiddleCenter; Top(t.rectTransform, 6, 32, 20);
    }
    static void Place(RectTransform rt, float x, float y, float xx, float yy)
    {
        if (rt != null) UIBuildUtils.Place(rt, new Vector2(x, y), new Vector2(xx, yy), new Vector2(6, 6), new Vector2(-6, -6));
    }
    static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }
}
