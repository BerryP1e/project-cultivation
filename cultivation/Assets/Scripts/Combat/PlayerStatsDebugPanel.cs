using UnityEngine;

/// <summary>
/// 临时调试面板（IMGUI 实现，不依赖任何 prefab / Canvas）。
///
/// 用途：把玩家的【全部数值】都做成可实时拖动/输入，方便测试。
/// 打开方式：默认 F1 键。改动即刻生效（走 PlayerCombatStats 的调试数值覆盖）。
///
/// 面板关闭时如果还开着「调试覆盖」，数值依然生效；点【恢复常规结算】才会关掉。
/// </summary>
public class PlayerStatsDebugPanel : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("显示/隐藏调试面板的按键")]
    public KeyCode 开关按键 = KeyCode.F1;

    [Tooltip("进入游戏时是否默认显示。★ 默认关（用户 2026-09-26：一进游戏就糊一大块面板，F1 自己开）")]
    public bool 启动时显示 = false;

    [Header("引用")]
    [Tooltip("留空则自动在自身找 PlayerCombatStats")]
    public PlayerCombatStats 玩家战斗属性;

    [Tooltip("留空则自动在自身找 PlayerVitals（「不扣血」开关要用它）")]
    public PlayerVitals 玩家气血;

    [Header("外观")]
    public float 面板宽度 = 380f;
    public float 面板高度 = 640f;
    public int 字号 = 13;

    bool 显示;
    Vector2 滚动;
    GUIStyle 标题样式, 行样式;
    string[] 编辑缓存;

    const float 标签宽 = 130f;

    void Awake()
    {
        if (玩家战斗属性 == null) 玩家战斗属性 = GetComponent<PlayerCombatStats>();
        if (玩家气血 == null) 玩家气血 = GetComponent<PlayerVitals>();
        显示 = 启动时显示;
        编辑缓存 = new string[AttributeUtil.Count];
    }

    void Update()
    {
        if (Input.GetKeyDown(开关按键)) 显示 = !显示;
    }

    void EnsureStyles()
    {
        if (标题样式 != null) return;
        标题样式 = new GUIStyle(GUI.skin.label) { fontSize = 字号 + 2, fontStyle = FontStyle.Bold };
        行样式 = new GUIStyle(GUI.skin.label) { fontSize = 字号 };
    }

    void OnGUI()
    {
        if (!显示 || 玩家战斗属性 == null) return;
        EnsureStyles();

        // 用屏幕高度，而不是只用 面板高度 这个字段。
        // 字段是序列化的，改默认值不会影响场景里已有的组件，所以加上
        // 「召唤 NPC」之后面板总内容超过了 640，召唤按钮正好被挤出窗口
        // 下边缘 —— 表现就是"看不到召唤按钮"。
        float 高 = Mathf.Max(面板高度, Screen.height - 24f);
        var rect = new Rect(12f, 12f, 面板宽度, 高);
        GUILayout.BeginArea(rect, GUI.skin.box);

        GUILayout.Label("玩家数值调试  (" + 开关按键 + " 开关)", 标题样式);
        GUILayout.Space(4f);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("从当前值同步")) { 玩家战斗属性.同步调试数值(); 清空缓存(); }
        if (GUILayout.Button("恢复常规结算")) { 玩家战斗属性.关闭调试数值(); 清空缓存(); }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("载入初始默认值")) { 载入默认(); }
        if (GUILayout.Button("全部清零")) { 全部清零(); }
        GUILayout.EndHorizontal();

        GUILayout.Space(4f);

        // 这里原来是一个 Toggle，直接改 使用调试数值 这个 bool 字段。
        // 问题是：那个字段是裸的，改它【不会触发 Recalculate()】，
        // 所以勾上以后 当前属性 还是旧的，要等别的事件才刷新 —— 表现就是"勾了没反应"。
        // 换成按钮，走 UseDebugStats 属性（内部会 Recalculate），再补一次重算兜底。
        bool 已在用调试值 = 玩家战斗属性.使用调试数值;
        var 原色 = GUI.backgroundColor;
        GUI.backgroundColor = 已在用调试值 ? new Color(1f, 0.75f, 0.4f) : new Color(0.55f, 1f, 0.6f);
        if (GUILayout.Button(已在用调试值 ? "✔ 修改已应用（点此重新应用）" : "应用修改到角色", GUILayout.Height(28f)))
        {
            玩家战斗属性.UseDebugStats = true;     // 会 Recalculate
            // 【不要在这里调 同步调试数值()】—— 它会把 调试数值 覆盖成「基础属性+功法」，
            // 于是刚改的数值（比如攻速 2）会立刻被顶回默认值（1）。那个按钮单独留着。
            玩家战斗属性.Recalculate();            // 双保险
            清空缓存();
            Debug.Log("[调试面板] 已把修改后的数值应用到角色身上");
        }
        GUI.backgroundColor = 原色;

        GUILayout.Label(已在用调试值
            ? "当前：使用调试数值（覆盖 基础属性 + 功法）"
            : "当前：使用常规结算", 行样式);

        // 无敌开关放在这里而不是面板底部 —— 面板内容很长，
        // 放底下会被挤出窗口下边缘（召唤按钮当初就踩过这个坑，见上面 GUILayout 高度的注释）
        画无敌开关();
        GUILayout.Space(4f);

        // 召唤区放在「应用修改到角色」正下方 —— 之前放在属性列表底下，
        // 面板内容超高，按钮被挤出窗口下边缘，压根看不见。
        画召唤区();
        GUILayout.Space(4f);
        画物品区();
        GUILayout.Space(4f);

        // ---- 独立字段 ----
        GUILayout.Label("—— 修炼相关 ——", 行样式);
        GUILayout.Label("境界：" + (玩家战斗属性.玩家属性 != null && 玩家战斗属性.玩家属性.境界 != null
                    ? 玩家战斗属性.玩家属性.境界.境界名 : "未设定") + "（暂不可调试）", 行样式);
        玩家战斗属性.调试神识 = 行("神识", 玩家战斗属性.调试神识);
        玩家战斗属性.调试吐纳速度 = 行("吐纳速度", 玩家战斗属性.调试吐纳速度);
        同步独立字段();

        GUILayout.Space(6f);
        GUILayout.Label("—— 26 项战斗属性 ——", 行样式);

        // 属性列表占「窗口高 - 700」：上面约 230、召唤区约 150、物品区约 190、底部一行约 20。
        // 加 160 的下限，免得窗口被拖得很矮时这个滚动区变成负数、整个布局崩掉。
        滚动 = GUILayout.BeginScrollView(滚动, GUILayout.Height(Mathf.Max(160f, rect.height - 700f)));
        for (int i = 0; i < AttributeUtil.Count; i++)
        {
            var t = (AttributeType)i;
            玩家战斗属性.调试数值[t] = 行(AttributeUtil.GetDisplayName(t), 玩家战斗属性.调试数值[t]);
        }
        GUILayout.EndScrollView();

        GUILayout.Space(4f);
        GUILayout.Label("当前生效值：" + 玩家战斗属性.当前属性.ToReadableString(), 行样式);

        GUILayout.EndArea();
        if (玩家战斗属性.使用调试数值) 玩家战斗属性.Recalculate();
    }

    /// <summary>
    /// **「不扣血」开关**（立刻生效，点一下就能用）。
    ///
    /// 写的是 <see cref="PlayerVitals.调试无敌"/>，而 <c>受到伤害()</c> 第一行就是
    /// `if (伤害 &lt;= 0f || 已死亡 || 无敌) return 0f;` —— 所以**当帧就生效**，
    /// 不用等任何重算。
    ///
    /// 【为什么挂在 PlayerVitals 上而不是面板自己的字段】
    /// 面板的字段是**场景序列化**的：面板关了它还在、甚至在编辑器里被存进场景。
    /// 而 `PlayerVitals` 是运行时组件，重进 Play 就回到默认值 ——
    /// 「重开一局不该还开着无敌」这件事就自动成立了。
    ///
    /// 【为什么不去写 无敌 本身】`无敌` 现在是只读的并集（`调试无敌 ‖ 保护中`），
    /// 详见 <see cref="PlayerVitals.无敌"/>。这样塔里的重生保护到期时
    /// 只关它自己那一份，**不会把调试开关一起关掉**。
    /// </summary>
    void 画无敌开关()
    {
        if (玩家气血 == null) 玩家气血 = GetComponent<PlayerVitals>();
        if (玩家气血 == null)
        {
            GUILayout.Label("—— 不扣血 —— 找不到 PlayerVitals，用不了", 行样式);
            return;
        }

        GUILayout.Space(6f);
        bool 开 = 玩家气血.调试无敌;
        var 旧色 = GUI.backgroundColor;
        GUI.backgroundColor = 开 ? new Color(1f, 0.55f, 0.5f) : new Color(0.55f, 1f, 0.6f);

        if (GUILayout.Button(开 ? "✔ 不扣血：开（点此关闭）" : "不扣血（无敌）", GUILayout.Height(30f)))
        {
            玩家气血.调试无敌 = !开;
            Debug.Log("[调试面板] 不扣血：" + (玩家气血.调试无敌 ? "开" : "关")
                      + "（当前 无敌=" + 玩家气血.无敌
                      + "，其中重生保护=" + 玩家气血.保护中 + "）");
        }
        GUI.backgroundColor = 旧色;

        // 把两个来源都显示出来 —— 只显示一个「无敌：开」的话，
        // 分不清是调试开关开着、还是重生保护还没到期
        GUILayout.Label(玩家气血.无敌
            ? "当前：无敌（调试开关 " + (开 ? "开" : "关") + " ｜ 重生保护 " + (玩家气血.保护中 ? "开" : "关") + "）"
            : "当前：正常受伤", 行样式);
    }

    /// <summary>第一次打开面板时，把当前实际值灌进调试数值，避免从 0 开始</summary>
    void 同步独立字段()
    {
        if (玩家战斗属性.调试神识 == 0f && 玩家战斗属性.调试吐纳速度 == 0f && !独立字段已同步)
        {
            玩家战斗属性.调试神识 = PlayerDefaultStats.默认神识;
            玩家战斗属性.调试吐纳速度 = PlayerDefaultStats.默认吐纳速度;
            独立字段已同步 = true;
        }
    }
    bool 独立字段已同步;

    /// <summary>一行：标签 + 输入框，返回解析后的值</summary>
    float 行(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, 行样式, GUILayout.Width(标签宽));
        string cached = 编辑缓存[(int)FindIndex(label)];
        if (cached == null || !float.TryParse(cached, out float parsed) || !Mathf.Approximately(parsed, value))
            编辑缓存[(int)FindIndex(label)] = value.ToString("0.####");
        string text = GUILayout.TextField(编辑缓存[(int)FindIndex(label)], GUILayout.Width(90f));
        if (text != 编辑缓存[(int)FindIndex(label)])
        {
            编辑缓存[(int)FindIndex(label)] = text;
            if (float.TryParse(text, out float v)) value = v;
        }
        if (AttributeUtil.IsPercent((AttributeType)FindIndex(label)))
            GUILayout.Label("(" + (value * 100f).ToString("0.#") + "%)", 行样式);
        GUILayout.EndHorizontal();
        return value;
    }

    static int FindIndex(string displayName)
    {
        for (int i = 0; i < AttributeUtil.Count; i++)
            if (AttributeUtil.GetDisplayName((AttributeType)i) == displayName) return i;
        return 0;
    }

    void 清空缓存()
    {
        if (编辑缓存 == null) return;
        for (int i = 0; i < 编辑缓存.Length; i++) 编辑缓存[i] = null;
    }

    void 载入默认()
    {
        var d = PlayerDefaultStats.Create();
        for (int i = 0; i < AttributeUtil.Count; i++)
            玩家战斗属性.调试数值[(AttributeType)i] = d[(AttributeType)i];
        玩家战斗属性.调试神识 = PlayerDefaultStats.默认神识;
        玩家战斗属性.调试吐纳速度 = PlayerDefaultStats.默认吐纳速度;
        玩家战斗属性.使用调试数值 = true;
        清空缓存();
    }

    void 全部清零()
    {
        玩家战斗属性.调试数值.Clear();
        玩家战斗属性.调试神识 = 0f;
        玩家战斗属性.调试吐纳速度 = 0f;
        玩家战斗属性.使用调试数值 = true;
        清空缓存();
    }

    // ---- ASCII 别名 ----
    public bool Visible { get => 显示; set => 显示 = value; }
    public void Toggle() => 显示 = !显示;
    // ================================================================
    // NPC 召唤（调试用）
    //
    // 列表直接从 Resources 加载 —— resources/NPC 本身就是 Resources 目录，
    // 所以 Resources.LoadAll<GameObject>("NPC") 能一次拿到全部 261 个 NPC prefab，
    // 不用在 Inspector 里接任何引用。
    // ================================================================

    GameObject[] 全部NPC;
    string[] 全部NPC名;
    /// <summary>每个 NPC 有没有可用的 AI（见 <see cref="NpcAiBase.选脚本"/>）</summary>
    bool[] 全部NPC有AI;
    /// <summary>每个 NPC 是不是**专属物种 AI**（`NpcAi&lt;模型名&gt;`，而不是类型默认的 Animal/Demon/Human）</summary>
    bool[] 全部NPC是专属;
    Vector2 召唤滚动;
    int 选中NPC = -1;

    [Tooltip("召唤列表的筛选词（按 prefab 名 / 定义 id / 中文名 过滤）")]
    public string 召唤搜索 = "";

    [Tooltip("★ 只列「有 AI」的 NPC（用户 2026-09-29 要求：只放进去做好了 ai 的 npc 和 animal）。\n" +
             "关掉就能看到全部（含中立、没 AI 的）")]
    public bool 只看有AI = true;

    void 加载NPC列表()
    {
        if (全部NPC != null) return;
        var 全部 = Resources.LoadAll<GameObject>("NPC");
        var 列表 = new System.Collections.Generic.List<GameObject>();
        foreach (var g in 全部)
        {
            if (g == null) continue;
            // 只留我们组装好的 prefab：必须有 Animator 且接了控制器。
            // 原始 FBX 也躺在 Resources 里（跟 prefab 同名或差个 _01），
            // 不过滤的话列表会成对重复，而且召唤出来的 FBX 不会动。
            var an = g.GetComponent<Animator>();
            if (an == null || an.runtimeAnimatorController == null) continue;
            if (g.GetComponentInChildren<SkinnedMeshRenderer>(true) == null) continue;
            列表.Add(g);
        }
        列表.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        全部NPC = 列表.ToArray();
        全部NPC名 = new string[全部NPC.Length];
        全部NPC有AI = new bool[全部NPC.Length];
        全部NPC是专属 = new bool[全部NPC.Length];
        for (int i = 0; i < 全部NPC.Length; i++)
        {
            全部NPC名[i] = 全部NPC[i].name;
            var 实例 = 全部NPC[i].GetComponent<NpcInstance>();
            全部NPC有AI[i] = NpcAiBase.选脚本(实例) != null;
            全部NPC是专属[i] = NpcAiBase.找物种脚本(全部NPC[i].name) != null
                            || (实例 != null && (NpcAiBase.找物种脚本(实例.定义 != null ? 实例.定义.名字 : null) != null
                                              || NpcAiBase.找物种脚本(实例.定义 != null ? 实例.定义.id : null) != null));
        }
    }

    /// <summary>这一条要不要显示（搜索词 + 「只看有 AI」）</summary>
    bool 召唤该显示(int i, string 净搜索)
    {
        if (只看有AI && !全部NPC有AI[i]) return false;
        if (净搜索.Length == 0) return true;
        if (全部NPC名[i] != null && 全部NPC名[i].IndexOf(净搜索, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        var 实例 = 全部NPC[i].GetComponent<NpcInstance>();
        if (实例 == null) return false;
        if (实例.定义 == null) return false;
        return (实例.定义.id ?? "").IndexOf(净搜索, System.StringComparison.OrdinalIgnoreCase) >= 0
            || (实例.定义.名字 ?? "").IndexOf(净搜索, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void 画召唤区()
    {
        加载NPC列表();

        int 有AI数 = 0, 专属数 = 0;
        for (int i = 0; i < 全部NPC.Length; i++) { if (全部NPC有AI[i]) 有AI数++; if (全部NPC是专属[i]) 专属数++; }

        GUILayout.Space(6f);
        GUILayout.Label("—— 召唤 NPC ——", 行样式);
        GUILayout.Label("共 " + 全部NPC.Length + " 个（有 AI " + 有AI数 + "，其中专属物种 AI " + 专属数 + "）"
            + "  当前选中：" + (选中NPC >= 0 ? 全部NPC名[选中NPC] : "（未选）"), 行样式);

        // 搜索框：261 个 NPC 纯滚动找起来很累（用户 2026-09-29 要求：跟背包塞物品那边一样能搜）
        GUILayout.BeginHorizontal();
        GUILayout.Label("筛选", 行样式, GUILayout.Width(36f));
        召唤搜索 = GUILayout.TextField(召唤搜索 ?? "");
        if (GUILayout.Button("清", GUILayout.Width(30f))) 召唤搜索 = "";
        GUILayout.EndHorizontal();
        只看有AI = GUILayout.Toggle(只看有AI, " 只列有 AI 的（★ = 专属物种 AI）", 行样式);

        string 净搜索 = (召唤搜索 ?? "").Trim();

        召唤滚动 = GUILayout.BeginScrollView(召唤滚动, GUILayout.Height(110f));
        int 显示数 = 0;
        for (int i = 0; i < 全部NPC名.Length; i++)
        {
            if (!召唤该显示(i, 净搜索)) continue;
            显示数++;
            var 原 = GUI.backgroundColor;
            if (i == 选中NPC) GUI.backgroundColor = new Color(1f, 0.85f, 0.45f);
            if (GUILayout.Button((全部NPC是专属[i] ? "★ " : "· ") + 全部NPC名[i]))
            {
                选中NPC = i;
                Debug.Log("[调试面板] 选中 NPC：" + 全部NPC名[i]
                    + (全部NPC是专属[i] ? "（专属物种 AI）" : (全部NPC有AI[i] ? "（类型默认 AI）" : "（**没有 AI**）")));
            }
            GUI.backgroundColor = 原;
        }
        if (显示数 == 0) GUILayout.Label("（没有匹配的，检查筛选词 / 关掉「只列有 AI 的」）", 行样式);
        GUILayout.EndScrollView();

        GUILayout.Space(4f);
        var 旧色 = GUI.backgroundColor;
        GUI.backgroundColor = 选中NPC >= 0 ? new Color(0.55f, 1f, 0.6f) : new Color(0.6f, 0.6f, 0.6f);
        GUI.enabled = 选中NPC >= 0;
        if (GUILayout.Button("召唤选中 NPC", GUILayout.Height(30f)))
        {
            var prefab = 全部NPC[选中NPC];
            // 召在玩家正前方 2 米
            var 玩家 = 玩家战斗属性 != null ? 玩家战斗属性.transform : null;
            Vector3 位 = 玩家 != null ? 玩家.position + 玩家.forward * 2f : Vector3.zero;
            var go = Instantiate(prefab, 位, Quaternion.identity);
            go.name = prefab.name + "_召唤";
            Debug.Log("[调试面板] 已召唤 " + prefab.name + " 到 " + 位
                + (全部NPC有AI[选中NPC] ? "" : " —— **注意：这个 NPC 没有 AI，召唤出来不会动**"));
        }
        GUI.enabled = true;
        GUI.backgroundColor = 旧色;
    }

    // ================================================================
    // 物品（调试用）：把任意物品直接塞进玩家背包
    //
    // 列表来源：`QuestDatabase.物品库` —— 那是 `修仙/从配置表生成资产` 结束时
    // 由 `QuestDatabaseBuilder` 收集好的（Generated 下的物品表资产 + resources 下的），
    // 运行时通过 `Assets/resources/任务/任务库.asset` 读得到。
    // 所以**物品表里改了什么、这里就有什么**，不需要另外维护一份清单。
    // ================================================================

    Vector2 物品滚动;
    string 物品搜索 = "";
    ItemDefinition 选中物品;

    void 画物品区()
    {
        var 全部 = 取全部物品();

        GUILayout.Space(6f);
        GUILayout.Label("—— 给背包塞物品 ——", 行样式);
        if (全部.Count == 0)
        {
            GUILayout.Label("物品库是空的：先跑 修仙/从配置表生成资产", 行样式);
            return;
        }

        var 面板 = 取面板();
        int 已有 = 选中物品 != null && 面板 != null ? 面板.物品数量(选中物品) : 0;
        GUILayout.Label("共 " + 全部.Count + " 件，当前选中：" +
            (选中物品 != null ? 选中物品.DisplayName + "（背包里已有 " + 已有 + "）" : "（未选）"), 行样式);

        // 搜索框：物品到几十件以后，纯滚动找起来很累，可以直接筛
        GUILayout.BeginHorizontal();
        GUILayout.Label("筛选", 行样式, GUILayout.Width(36f));
        物品搜索 = GUILayout.TextField(物品搜索 ?? "");
        if (GUILayout.Button("清", GUILayout.Width(30f))) 物品搜索 = "";
        GUILayout.EndHorizontal();

        物品滚动 = GUILayout.BeginScrollView(物品滚动, GUILayout.Height(150f));
        int 显示数 = 0;
        foreach (var it in 全部)
        {
            if (!string.IsNullOrEmpty(物品搜索) &&
                it.DisplayName.IndexOf(物品搜索, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                (it.物品id ?? "").IndexOf(物品搜索, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            显示数++;

            var 原 = GUI.backgroundColor;
            if (it == 选中物品) GUI.backgroundColor = new Color(1f, 0.85f, 0.45f);
            // 名字后面带上「能否使用」，一眼能看出哪些是学习物品
            string 标 = it.DisplayName + (it.可使用 ? "  [可用]" : "");
            if (GUILayout.Button(标)) 选中物品 = it;
            GUI.backgroundColor = 原;
        }
        if (显示数 == 0) GUILayout.Label("（没有匹配的物品）", 行样式);
        GUILayout.EndScrollView();

        GUILayout.Space(4f);
        var 旧色 = GUI.backgroundColor;
        GUI.enabled = 选中物品 != null && 面板 != null;
        GUI.backgroundColor = GUI.enabled ? new Color(0.55f, 1f, 0.6f) : new Color(0.6f, 0.6f, 0.6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("塞 1 个", GUILayout.Height(28f))) 塞物品(选中物品, 1, 面板);
        if (GUILayout.Button("塞 10 个", GUILayout.Height(28f))) 塞物品(选中物品, 10, 面板);
        GUILayout.EndHorizontal();
        GUI.backgroundColor = 旧色;
        GUI.enabled = true;

        if (面板 == null)
            GUILayout.Label("场景里找不到 UIPanelData（角色面板数据），塞不了", 行样式);
    }

    /// <summary>物品库（缓存一次）</summary>
    System.Collections.Generic.List<ItemDefinition> 物品缓存;

    System.Collections.Generic.List<ItemDefinition> 取全部物品()
    {
        if (物品缓存 != null && 物品缓存.Count > 0) return 物品缓存;
        var 出 = new System.Collections.Generic.List<ItemDefinition>();
        var 库 = QuestDatabase.取();
        if (库 != null && 库.物品库 != null)
            foreach (var it in 库.物品库)
                if (it != null && !出.Contains(it)) 出.Add(it);
        出.Sort((a, b) => string.CompareOrdinal(a.DisplayName, b.DisplayName));
        物品缓存 = 出;
        return 物品缓存;
    }

    static UIPanelData 取面板()
    {
        // 面板数据挂在 CharacterUI 上，不在 Player 身上，所以要全场景找
        return FindObjectOfType<UIPanelData>();
    }

    static void 塞物品(ItemDefinition 物品, int 数量, UIPanelData 面板)
    {
        if (物品 == null || 面板 == null) return;
        面板.EnsureLists();
        面板.给物品(物品, 数量);
        Debug.Log("[调试面板] 往背包塞了 " + 物品.DisplayName + " ×" + 数量
            + "（现在共 " + 面板.物品数量(物品) + " 个）");
    }
}
