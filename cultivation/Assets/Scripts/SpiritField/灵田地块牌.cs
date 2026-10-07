using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **田块上方的信息牌** —— 走近就能看见，**不需要按 F**。
///
/// 用户 2026-10-01 的要求：
/// > 「我希望走进后就能显示在灵田上方，同时不要是纯文字，**加个底板**。」
///
/// 它只负责**报状态**（第几块田 / 品阶 / 待种植还是长到哪了 / 可收）。
/// 「按 F 能干什么」由 `StationInteractor` 那个**键帽提示框**负责 ——
/// 用户实测反馈过「靠近灵田没有按 F 的提示了」，所以那个提示是开着的
/// （见 <see cref="灵田地块.确保交互"/> 里 `显示靠近提示 = true`），
/// 这里就**不再重复写 [F] 那一行**，省得两块 UI 说同一句话。
///
/// ⚠️ 【尺寸与透明度踩过的坑】第一版做成了 360×130、缩放 0.008（世界 2.9 米宽），
/// 底板 alpha 还按距离乘到最低 0.35 —— 实测截出来是**一大片半透明的淡黄板子浮在地上面**，
/// 又大又虚、字也发白，用户直接说「看起来还是透明的」。
/// 现在：世界宽压到约 1.7 米、底板基础不透明度 0.92、字改成亮色。
/// </summary>
public class 灵田地块牌
{
    GameObject 根;
    Image 底板;
    Text 标题;
    Text 状态;

    static Font 中文字体;

    /// <summary>
    /// 牌子架在田块中心上方多高（米）。
    /// ⚠️ 别压到 `StationInteractor` 那个键帽提示：它挂在**玩家**头顶上方约 2.1 米处，
    ///    玩家站到田边时两者出现在同一片画面里。牌子抬到 1.65 米就错开了（实测截图里重叠过）。
    /// </summary>
    const float 高度 = 1.65f;

    // ============================================================ 建造

    public void 建造(Transform 父)
    {
        根 = new GameObject("信息牌", typeof(Canvas));
        var canvas = 根.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 210;
        var rt = 根.GetComponent<RectTransform>();
        // 480 × 0.004 = 世界 1.92 米宽 —— 刚好和 1.9 米的畦一样宽，不会再压到旁边的地。
        // ⚠️ 宽度要**装得下最长那行字**：上一版是 330 宽 + 24 号字，
        //    而「生灵草 · 幼苗 0% 还需 3.0 天」有 17 个汉字（约 408px）——
        //    文字直接溢出到底板外面去了（截图里能看到字比板子宽）。见下面 状态 的字号。
        rt.sizeDelta = new Vector2(480f, 92f);
        rt.localScale = Vector3.one * 0.004f;

        根.transform.SetParent(父, false);
        根.transform.localPosition = new Vector3(0f, 高度, 0f);

        // ---- 底板 ----
        var 板 = new GameObject("底板", typeof(RectTransform));
        板.transform.SetParent(根.transform, false);
        底板 = 板.AddComponent<Image>();
        底板.color = 底板色(false);
        底板.raycastTarget = false;
        拉伸(板.GetComponent<RectTransform>());
        // 注：这里**不挂 Outline**。上一版给底板挂了淡黄色描边，实测整块板看起来是
        // 一片发黄的半透明片，浅黄标题压在上面**根本读不出来**（用户："面板用的字颜色
        // 不太搭配幕布，有点分不出来"）。边框改由"深色底 + 亮字自己带描边"来表达。

        标题 = 建字("标题", 25, FontStyle.Bold, new Color(1f, 0.99f, 0.94f), -6f, 36f);
        状态 = 建字("状态", 21, FontStyle.Normal, Color.white, -46f, 34f);

        // ★ 字自己带深色描边 —— 这样**不管底板最终渲染成什么颜色**，字都读得出来。
        //   光靠"预期的底板颜色"去配字色是不可靠的（上面那条就是这么翻车的）。
        描边(标题);
        描边(状态);
    }

    static void 描边(Text t)
    {
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.95f);
        o.effectDistance = new Vector2(1.6f, -1.6f);
    }

    static Color 底板色(bool 成熟)
        => 成熟 ? new Color(0.16f, 0.11f, 0.02f, 0.95f)
                : new Color(0.05f, 0.06f, 0.08f, 0.95f);

    Text 建字(string 名, int 字号, FontStyle 风格, Color 色, float y, float 高)
    {
        var go = new GameObject(名, typeof(RectTransform));
        go.transform.SetParent(根.transform, false);
        var t = go.AddComponent<Text>();
        t.font = 取字体();
        t.fontSize = 字号;
        t.fontStyle = 风格;
        t.color = 色;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;   // 不换行，免得两行字互相压
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-14f, 高);
        return t;
    }

    // ============================================================ 刷新

    public void 刷新(灵田地块状态 b, int 编号, bool 可交互)
    {
        if (根 == null || b == null) return;

        标题.text = "第 " + (编号 + 1) + " 块田 · " + 灵田品阶说明.中文名(b.品阶);

        if (b.待种植)
        {
            状态.text = "待种植";
        }
        else
        {
            var d = b.植;
            string 名 = d != null ? d.名 : b.作物id;
            // 【字要短】底板只有 1.92 米宽，太长会溢出到板子外面（踩过）。
            状态.text = b.已成熟
                ? 名 + " 已成熟 · 可收！"
                : 名 + " " + b.阶段名 + " " + (b.进度 * 100f).ToString("0") + "% · 剩"
                  + b.剩余天数.ToString("0.0") + "天";
        }

        底板.color = 底板色(b.已成熟);
    }

    /// <summary>
    /// 每帧：面向相机 + 按**玩家**离得多近决定显不显示。
    ///
    /// ⚠️ 【距离要按玩家算，不能按相机算】第一版用 `Camera.main` 的位置量距离 ——
    /// 而相机是跟在玩家后上方好几米的，实测玩家已经站到田边（离田 2 米）时，
    /// 相机离田还有 6~8 米，牌子直接被判成"太远"**一次都不显示**。
    /// "走近就显示"里的"近"指的是**玩家**。
    /// </summary>
    public void 每帧(Vector3 田位置, Transform 玩家)
    {
        if (根 == null) return;
        var 相 = Camera.main;
        if (相 == null) return;

        根.transform.rotation = Quaternion.LookRotation(
            根.transform.position - 相.transform.position, 相.transform.up);

        // 找不到玩家就照常显示（宁可能看见，也不要整片田一块牌子都没有）
        var 参照 = 玩家 != null ? 玩家.position : 相.transform.position;
        float 近 = Vector2.Distance(new Vector2(田位置.x, 田位置.z), new Vector2(参照.x, 参照.z));

        // ★ **离远就整个关掉，而不是留一块半透明的板子**。
        //   【这条就是用户那句"放下去之后怎么还是透明的"的真正原因】
        //   上一版有个"最低不透明度 0.35"的下限 —— 玩家一离远，每块田上方都留着一片
        //   35% 不透明的深色板子浮在半空，看上去就是"地还是透明的"（实测 alpha = 0.350）。
        //   正确做法是**要么清楚地显示、要么完全不显示**，没有"半吊子"这一档。
        const float 额外显示距离 = 2.5f;
        bool 该显示 = 近 <= 额外显示距离 + 3.2f;      // 玩家离田 5.7 米以内都看得见
        if (根.activeSelf != 该显示) 根.SetActive(该显示);
        if (!该显示) return;

        var c = 底板.color;
        底板.color = InkUITheme.Enabled ? new Color(.16f,.20f,.18f,.92f) : new Color(c.r, c.g, c.b, 0.95f);
        if (InkUITheme.Enabled && 底板.sprite == null) InkUITheme.Image(底板, "CommonPanels/toast");
        foreach (var tx in 根.GetComponentsInChildren<Text>(true))
            tx.color = InkUITheme.Enabled ? new Color(.96f,.95f,.89f) : new Color(tx.color.r, tx.color.g, tx.color.b, 1f);
    }

    public void 销毁()
    {
        if (根 != null) Object.Destroy(根);
        根 = null;
    }

    static void 拉伸(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static Font 取字体()
    {
        if (中文字体 != null) return 中文字体;
#if UNITY_EDITOR
        中文字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
        if (中文字体 != null) return 中文字体;
#endif
        foreach (var x in Resources.FindObjectsOfTypeAll<Font>())
            if (x != null && x.name.ToLowerInvariant().Contains("simhei")) { 中文字体 = x; return x; }
        中文字体 = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return 中文字体;
    }
}
