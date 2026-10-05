using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>外观页的水墨列表、透明半身立绘和独立滚动介绍。运行时接入，不重写场景。</summary>
public class UIInkAppearancePage : MonoBehaviour
{
    public AppearanceDefinition Selected { get; private set; }
    public Image Portrait { get; private set; }
    public ScrollRect DescriptionScroll { get; private set; }
    readonly List<GameObject> rows = new List<GameObject>();
    readonly Dictionary<string, Sprite> portraits = new Dictionary<string, Sprite>();
    RectTransform content;
    Text title, description, status, count;
    Button equip;
    Font font;
    玩家外观 actor;
    bool ready;
    static readonly Color Light = new Color(.96f, .95f, .88f);

    public static void 应用(Transform root)
    {
        if (!UIInkNavigation.启用) return;
        var panel = root.GetComponent<CharacterPanelUI>();
        if (panel == null || panel.tabs.Count < 8) return;
        var page = panel.tabs[7].page;
        var view = page.GetComponent<UIInkAppearancePage>();
        if (view == null) view = page.AddComponent<UIInkAppearancePage>();
        view.Build();
    }

    void Build()
    {
        if (ready) return;
        var old = GetComponent<外观页>();
        font = old != null ? old.字体 : null;
        if (font == null) font = Resources.Load<Font>("Fonts/SimHei");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (old != null) old.enabled = false;
        foreach (var motion in GetComponentsInChildren<UIInkMotion>(true)) motion.enabled = false;
        foreach (Transform child in transform) child.gameObject.SetActive(false);
        var oldBackground = GetComponent<Image>();
        if (oldBackground != null) oldBackground.enabled = false;
        ready = true;

        var list = Panel("AppearanceInkList", transform, new Vector2(.025f, .07f), new Vector2(.435f, .94f));
        var heading = Label("OwnedHeading", list, "已拥有外观", 30, TextAnchor.MiddleLeft);
        Place(heading.rectTransform, .17f, .75f, .70f, .86f);
        count = Label("OwnedCount", list, "", 17, TextAnchor.MiddleRight);
        Place(count.rectTransform, .70f, .75f, .85f, .86f);
        var scroll = Scroll("OwnedScroll", list, new Vector2(.09f, .12f), new Vector2(.89f, .73f));
        content = scroll.content;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 28, 28);
        layout.spacing = 30;
        layout.childControlHeight = true; layout.childControlWidth = true;
        layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Portrait = UIBuildUtils.CreateImage("AppearanceHalfPortrait", transform, Color.white);
        Place(Portrait.rectTransform, .47f, .33f, .99f, .985f);
        Portrait.preserveAspect = true; Portrait.raycastTarget = false;

        var info = Panel("AppearanceInkIntroduction", transform, new Vector2(.48f, .025f), new Vector2(.995f, .355f));
        title = Label("AppearanceName", info, "", 29, TextAnchor.MiddleLeft);
        Place(title.rectTransform, .10f, .65f, .70f, .90f);
        status = Label("AppearanceStatus", info, "", 17, TextAnchor.MiddleRight);
        Place(status.rectTransform, .68f, .65f, .89f, .90f);
        DescriptionScroll = Scroll("IntroductionScroll", info, new Vector2(.10f, .28f), new Vector2(.90f, .65f));
        description = Label("AppearanceDescription", DescriptionScroll.content, "", 20, TextAnchor.UpperLeft);
        var descriptionLayout = DescriptionScroll.content.gameObject.AddComponent<VerticalLayoutGroup>();
        descriptionLayout.padding = new RectOffset(4, 16, 4, 4);
        descriptionLayout.childControlHeight = true; descriptionLayout.childControlWidth = true;
        descriptionLayout.childForceExpandHeight = false; descriptionLayout.childForceExpandWidth = true;
        DescriptionScroll.content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        equip = Action("EquipAppearance", info, "换上此装", new Vector2(.30f, .07f), new Vector2(.70f, .23f));
        equip.onClick.AddListener(() => {
            if (actor == null) actor = FindObjectOfType<玩家外观>();
            if (actor != null && Selected != null && actor.已拥有(Selected)) actor.装备(Selected);
        });
        if (gameObject.activeInHierarchy) Refresh();
    }

    void OnEnable()
    {
        玩家外观.外观变化 += Changed;
        if (ready) Refresh();
    }
    void OnDisable() { 玩家外观.外观变化 -= Changed; }
    void Changed(AppearanceDefinition appearance) { if (ready) Refresh(); }

    public void Refresh()
    {
        if (!ready) return;
        actor = FindObjectOfType<玩家外观>();
        var database = AppearanceDatabase.取();
        foreach (var row in rows) { row.SetActive(false); Destroy(row); }
        rows.Clear();
        if (database != null && actor != null)
            foreach (var appearance in database.全部)
                if (appearance != null && actor.已拥有(appearance)) AddRow(appearance);
        count.text = rows.Count + " 件";
        if (Selected == null || actor == null || !actor.已拥有(Selected))
            Selected = actor != null ? actor.当前外观 : null;
        if (Selected == null && database != null)
            foreach (var a in database.全部) if (actor != null && actor.已拥有(a)) { Selected = a; break; }
        ShowSelection();
    }

    void AddRow(AppearanceDefinition appearance)
    {
        var rt = UIBuildUtils.CreateRect("AppearanceRow_" + appearance.id, content);
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
        var rowImage = rt.gameObject.AddComponent<Image>();
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = rowImage;
        var label = Label("Name", rt, appearance.DisplayName, 27, TextAnchor.MiddleLeft);
        Place(label.rectTransform, .20f, .35f, .87f, .88f);
        var owned = Label("State", rt, actor.当前外观 == appearance ? "已穿戴" : "已拥有 · 点击查看", 16, TextAnchor.MiddleLeft);
        Place(owned.rectTransform, .20f, .05f, .87f, .37f);
        StyleAction(button);
        button.onClick.AddListener(() => { Selected = appearance; ShowSelection(); });
        rows.Add(rt.gameObject);
        foreach (var graphic in rt.GetComponentsInChildren<Graphic>(true)) graphic.gameObject.AddComponent<UIInkScrollFade>();
    }

    void ShowSelection()
    {
        string id = Selected != null ? Selected.id : "";
        if (!portraits.TryGetValue(id, out var sprite))
        {
            sprite = string.IsNullOrEmpty(id) ? null : Resources.Load<Sprite>("UI/InkUI/Appearance/portrait-" + id);
            if (sprite != null) portraits[id] = sprite;
        }
        Portrait.sprite = sprite; Portrait.enabled = sprite != null;
        title.text = Selected != null ? Selected.DisplayName : "暂无外观";
        description.text = Selected != null ? Selected.介绍 : "获得外观后可在此查看和穿戴。";
        bool worn = actor != null && actor.当前外观 == Selected;
        status.text = worn ? "当前穿戴" : "";
        equip.interactable = Selected != null && actor != null && actor.已拥有(Selected) && !worn;
        equip.GetComponentInChildren<Text>().text = worn ? "已穿戴" : "换上此装";
        LayoutRebuilder.ForceRebuildLayoutImmediate(DescriptionScroll.content);
        DescriptionScroll.verticalNormalizedPosition = 1;
        foreach (var row in rows)
        {
            var fluid = row.GetComponent<UIInkFluid>();
            if (fluid != null) fluid.选中(row.name == "AppearanceRow_" + id);
        }
    }

    RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var image = UIBuildUtils.CreateImage(name, parent, Color.white);
        UIBuildUtils.Place(image.rectTransform, min, max, Vector2.zero, Vector2.zero);
        InkUITheme.Image(image, "Bag/bag-info-ink", false);
        image.raycastTarget = false;
        return image.rectTransform;
    }
    Text Label(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        var text = UIBuildUtils.CreateText(name, parent, font, value, size, alignment, Light);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .8f); shadow.effectDistance = new Vector2(1, -1);
        return text;
    }
    ScrollRect Scroll(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rt = UIBuildUtils.CreateRect(name, parent);
        UIBuildUtils.Place(rt, min, max, Vector2.zero, Vector2.zero);
        var scroll = rt.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = UIBuildUtils.CreateImage("Viewport", rt, Color.clear);
        UIBuildUtils.Stretch(viewport.rectTransform);
        viewport.rectTransform.offsetMax = new Vector2(-20, 0);
        viewport.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, 14);
        scroll.viewport = viewport.rectTransform;
        scroll.content = UIBuildUtils.CreateRect("Content", viewport.transform);
        scroll.content.anchorMin = new Vector2(0, 1); scroll.content.anchorMax = Vector2.one;
        scroll.content.pivot = new Vector2(.5f, 1); scroll.content.sizeDelta = Vector2.zero;
        InkUITheme.Scroll(scroll, 0);
        return scroll;
    }
    Button Action(string name, Transform parent, string value, Vector2 min, Vector2 max)
    {
        var image = UIBuildUtils.CreateImage(name, parent, Color.clear);
        UIBuildUtils.Place(image.rectTransform, min, max, Vector2.zero, Vector2.zero);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var text = Label("Label", image.transform, value, 22, TextAnchor.MiddleCenter);
        UIBuildUtils.Stretch(text.rectTransform);
        StyleAction(button);
        return button;
    }
    static void StyleAction(Button button)
    {
        UIInkActionButton.Apply(button);
        button.GetComponent<UIInkActionButton>().设置绘制倍率(new Vector2(.84f,1.45f));
        var fluid=button.GetComponent<UIInkFluid>();
        fluid.位移强度=.10f;
        fluid.形变强度=.15f;
    }
    static void Place(RectTransform rt, float x0, float y0, float x1, float y1)
    { UIBuildUtils.Place(rt, new Vector2(x0, y0), new Vector2(x1, y1), Vector2.zero, Vector2.zero); }
}
