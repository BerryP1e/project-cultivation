using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 【神通页重排】把生成器摆好的节点**改成目标原型图的样子**（运行时做，不动场景、不改生成器）。
///
/// ## 为什么要单独一个类
///
/// `UIInkSkin` 只管"换图"；**改布局是另一件事**：原型图（`ui-rework-2026-10-03/screen-studies/
/// skills-scroll-passives-v2.png`）里这些块的位置/尺寸/层次跟生成器摆的不一样：
///
/// | 原型图 | 生成器摆的 | 这里怎么改 |
/// |---|---|---|
/// | 大幕布几乎铺满屏、内含山水 | `Window` 居中 1440×860、素纸 | 换成 `parent-curtain-9slice-source`，尺寸放到 1780×1000 |
/// | 左导航是一条**完整立板** + 选中项有牌 | `Sidebar` 170 宽纯色 + 8 个纯色页签 | 换 `left-parent-navigation-panel`，页签换 `nav-tab-active/inactive`（**认当前页**） |
/// | 六槽环：大环 + 编号 1–6 + 中心"主动技能栏" | 六个纯色方块 | 环换 `active-ring`、槽换 `active-slot-frame`，**补编号 1–6** |
/// | 已悟神通是**卡片**网格 | `KnownList` 是 34 高的竖列表 | 行底换 `ability-library-card`，容器 **VerticalLayoutGroup → GridLayoutGroup** |
/// | 生效被动是**带立绘圆图的行** | 普通列表行 | 行底换 `enabled-passive-row` |
/// | 右侧详情**通高**、有品阶牌 | `Info` 只占右下 0.44 | 换 `ability-detail-panel`，**锚点拉到通高** |
///
/// ⚠️ 全在运行时做：面板节点**序列化在 5 个游玩场景里**，重新生成 + 存场景会重排运行时 UI 列表
/// （踩坑 A8：写脏过 +19787 行）。
///
/// ## 幂等
///
/// 每步都先看"是不是已经改过了"（比如 Text 编号已经存在就不再建）。可以反复调用。
/// </summary>
public static class UI神通页重排
{
    const string 资源根 = "UI/InkUI";
    const float 九宫倍率 = 2f;

    /// <summary>对整棵面板做重排（只在"神通页"那部分生效，其它页只换底图）</summary>
    public static void 应用(Transform 面板根)
    {
        if (面板根 == null) return;

        var 页 = 找(面板根, "Page_神通");
        if (页 == null) { Debug.LogWarning("[神通页重排] 找不到 Page_神通"); return; }

        放大幕布(面板根);
        做左导航(面板根);
        做六槽环(页);
        做已悟网格(页);
        做被动行(页);
        做右侧详情(页);

        Debug.Log("[神通页重排] 完成（幕布/左导航/六槽环+编号/卡片网格/被动行/通高详情）");
    }

    // ---------------------------------------------------------------- 大幕布

    static void 放大幕布(Transform 根)
    {
        var w = 找(根, "Window");
        if (w == null) return;
        var img = w.GetComponent<Image>();
        if (img != null)
        {
            var sp = 图("Skeleton/parent-curtain-9slice-source");
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 九宫倍率;
                img.color = Color.white;
            }
        }
        var rt = w as RectTransform;
        if (rt != null && rt.sizeDelta.x < 1700f)
        {
            rt.sizeDelta = new Vector2(1780f, 1000f);     // 原型图里幕布几乎铺满
        }
    }

    // ---------------------------------------------------------------- 左导航

    static void 做左导航(Transform 根)
    {
        var 侧 = 找(根, "Sidebar");
        if (侧 == null) return;
        var img = 侧.GetComponent<Image>();
        if (img != null)
        {
            var sp = 图("BigPieces/left-parent-navigation-panel");
            if (sp != null)
            {
                img.sprite = sp;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 九宫倍率;
                img.color = Color.white;
            }
        }
        // 页签：整条板已经有了，页签只当"选中牌"用
        var 当前 = 当前页(根);
        foreach (var t in 侧.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Tab_")) continue;
            var i = t.GetComponent<Image>();
            if (i == null) continue;
            string 名 = t.name.Substring(4);
            bool 选中 = (名 == 当前);
            var sp = 图(选中 ? "Skeleton/nav-tab-active" : "Skeleton/nav-tab-inactive");
            if (sp == null) continue;
            i.sprite = sp;
            i.type = Image.Type.Sliced;
            i.pixelsPerUnitMultiplier = 九宫倍率;
            i.color = 选中 ? Color.white : new Color(1f, 1f, 1f, 0.92f);
            // 选中项把文字压深、未选稍淡
            var 字 = t.GetComponentInChildren<Text>(true);
            if (字 != null) 字.color = 选中 ? new Color(0.13f, 0.20f, 0.16f) : new Color(0.30f, 0.33f, 0.30f);
        }
    }

    /// <summary>当前是哪个页（看哪个 `Page_*` 是 activeSelf）</summary>
    static string 当前页(Transform 根)
    {
        foreach (var t in 根.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("Page_") && t.gameObject.activeSelf) return t.name.Substring(5);
        return "";
    }

    // ---------------------------------------------------------------- 六槽环

    static void 做六槽环(Transform 页)
    {
        var 栏 = 找(页, "ActiveSkillBar");
        if (栏 == null) return;

        var 栏i = 栏.GetComponent<Image>();
        if (栏i != null)
        {
            var sp = 图("SkillsPage/skills/active-ring");
            if (sp != null) { 栏i.sprite = sp; 栏i.type = Image.Type.Simple; 栏i.color = Color.white; }
        }

        int n = 0;
        foreach (var t in 栏.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Slot_")) continue;
            n++;
            var i = t.GetComponent<Image>();
            if (i != null)
            {
                var sp = 图("Parts/active-slot-frame");
                if (sp != null) { i.sprite = sp; i.type = Image.Type.Simple; i.color = Color.white; }
            }
            // 编号 1–6：原型图里**常显**
            if (t.Find("编号") == null)
            {
                var go = new GameObject("编号", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(t, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 2f);
                rt.sizeDelta = new Vector2(60f, 34f);
                var tx = go.GetComponent<Text>();
                tx.text = n.ToString();
                tx.font = 字体(t);
                tx.fontSize = 26;
                tx.fontStyle = FontStyle.Bold;
                tx.alignment = TextAnchor.MiddleCenter;
                tx.color = new Color(0.35f, 0.27f, 0.12f);
                tx.raycastTarget = false;
            }
        }
    }

    // ---------------------------------------------------------------- 已悟神通（列表 → 卡片网格）

    static void 做已悟网格(Transform 页)
    {
        var 库 = 找(页, "KnownList");
        if (库 == null) return;
        var sp = 图("Parts/ability-library-card");
        if (sp != null) 给所有行换底(库, sp);

        // 容器：VerticalLayoutGroup → GridLayoutGroup（原型图是卡片网格）
        var 视口 = 找(库, "Viewport");
        var 内容 = 视口 != null ? 找(视口, "Content") : null;
        if (内容 == null) return;

        var 竖 = 内容.GetComponent<VerticalLayoutGroup>();
        if (竖 != null) Object.DestroyImmediate(竖);
        var 网 = 内容.GetComponent<GridLayoutGroup>();
        if (网 == null) 网 = 内容.gameObject.AddComponent<GridLayoutGroup>();
        网.cellSize = new Vector2(168f, 128f);
        网.spacing = new Vector2(8f, 8f);
        网.padding = new RectOffset(8, 8, 8, 8);
        网.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        网.constraintCount = 4;

        var 适配 = 内容.GetComponent<ContentSizeFitter>();
        if (适配 != null) 适配.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // ---------------------------------------------------------------- 生效被动（换行底）

    static void 做被动行(Transform 页)
    {
        var 被 = 找(页, "PassiveList");
        if (被 == null) return;
        var sp = 图("Parts/enabled-passive-row");
        if (sp != null) 给所有行换底(被, sp);
    }

    // ---------------------------------------------------------------- 右侧详情（通高）

    static void 做右侧详情(Transform 页)
    {
        var 详情 = 找(页, "Info");
        if (详情 == null) return;
        var rt = 详情 as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.735f, 0f);      // 原型图：右侧通高
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        var i = 详情.GetComponent<Image>();
        if (i != null)
        {
            var sp = 图("BigPieces/ability-detail-panel");
            if (sp != null)
            {
                i.sprite = sp;
                i.type = Image.Type.Sliced;
                i.pixelsPerUnitMultiplier = 九宫倍率;
                i.color = Color.white;
            }
        }
    }

    // ---------------------------------------------------------------- 小工具

    static void 给所有行换底(Transform 容器, Sprite sp)
    {
        var 视口 = 找(容器, "Viewport");
        var 内容 = 视口 != null ? 找(视口, "Content") : null;
        if (内容 == null) return;
        foreach (var t in 内容.GetComponentsInChildren<Transform>(true))
        {
            if (t == 内容) continue;
            var i = t.GetComponent<Image>();
            if (i == null) continue;
            // 只给"行根"换底：行根通常是 Content 的直接子物体
            if (t.parent != 内容) continue;
            i.sprite = sp;
            i.type = Image.Type.Sliced;
            i.pixelsPerUnitMultiplier = 九宫倍率;
            i.color = Color.white;
        }
    }

    static Transform 找(Transform 根, string 名)
    {
        if (根 == null) return null;
        if (根.name == 名) return 根;
        foreach (var t in 根.GetComponentsInChildren<Transform>(true))
            if (t.name == 名) return t;
        return null;
    }

    static Font 字体(Transform 谁)
    {
        var t = 谁.GetComponentInChildren<Text>(true);
        return t != null && t.font != null ? t.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static readonly Dictionary<string, Sprite> 缓存 = new Dictionary<string, Sprite>();
    static Sprite 图(string 相对)
    {
        Sprite sp;
        if (缓存.TryGetValue(相对, out sp)) return sp;
        sp = Resources.Load<Sprite>(资源根 + "/" + 相对);
        if (sp == null) Debug.LogWarning("[神通页重排] 取不到素材：" + 资源根 + "/" + 相对);
        缓存[相对] = sp;
        return sp;
    }
}
