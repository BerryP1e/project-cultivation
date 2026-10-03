using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// **进任何场景自动补齐"每个场景都必须有"的东西** —— 用户 2026-09-27 要的"一劳永逸"。
///
/// 为什么要有它（用户的抱怨）：
///   「每次搞点什么新的东西，一去其他的场景又不行，一看，哦又是什么东西没有同步过来」
///   根因是：这些东西原本**靠手工在每个场景里配**，而场景有 5 个 ⇒ 加新东西必然漏。
///
/// 现在改成**运行时保证**：进入任何游戏场景后，先检查缺什么，缺的就补上。
/// 于是"忘了在某个场景里配"这件事**不再可能发生** —— 不用再去 5 个场景各配一遍。
///
/// 现有做法（<c>SceneRigSyncer</c>，把 Player/相机/UI 从模板场景整体搬过去）**已经不跑了**：
/// 它是整体覆盖式的，而各场景早就各自调过（古古镇的相机角度、宗门外的野外布置…），
/// 一跑就把别人的改动冲掉。这里改成**只补缺、不动已有的**。
///
/// 与 <c>场景一致性体检</c> 的分工：
///   · 本类 = **运行时自动补**（玩家永远遇不到"缺东西"的场景）
///   · 体检 = **编辑器里报告**（自举清单漏了什么，一眼看出来）
///   ★ 新增"每场景必须有"的东西时，**两边都要登记**。
/// </summary>
public static class 场景自举
{
    /// <summary>非游戏场景（主菜单 / 过场），不补东西</summary>
    static readonly string[] 跳过场景 = { "StartScene", "Transition subtitles" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void 装钩子()
    {
        SceneManager.sceneLoaded -= 场景加载后;
        SceneManager.sceneLoaded += 场景加载后;
        // 第一个场景的 sceneLoaded 可能在本方法之前就派发过了，所以自己也跑一次
        补当前场景(SceneManager.GetActiveScene());
    }

    static void 场景加载后(Scene 场景, LoadSceneMode 模式) => 补当前场景(场景);

    static void 补当前场景(Scene 场景)
    {
        if (!场景.IsValid() || !场景.isLoaded) return;

        // ★ 场景标记要在**任何提前返回之前**维护：它是"我现在在哪个场景"的唯一真相，
        //   连主菜单/过场也该把它翻过去（否则从洞府回主菜单，洞府那几段对话还开着）。
        补场景标记(场景);

        for (int i = 0; i < 跳过场景.Length; i++)
            if (场景.name == 跳过场景[i]) return;

        补单例(场景);
        补玩家与相机(场景);
        查装配(场景);
    }

    // ---------------------------------------------------------------- 场景标记

    /// <summary>
    /// **场景标记的前缀**：对话表 / 任务表的 `需要标记`（`排除标记` 同理）里可以写
    /// <c>场景_3C_Testbed</c> 这种标记，用来**按场景开关一段内容**。
    ///
    /// 【为什么要有它】踩过的坑（2026-10-02 用户实测报的"还没进洞府，大师兄就把洞府那一幕演完了"）：
    ///   对话选段只看 `(npcId, 分段, 需要标记, 优先)`，**完全不看场景** ——
    ///   而大师兄是任务生成、被「飞到」停在宗门传送点站在玩家旁边的。
    ///   于是 `q_main_004` 阶段16 一打上 `接取加标记=q_主线_到洞府`，
    ///   阶段17/18 的洞府台词（`dlg_act5_dongfu_*` / `dlg_act5_lingtian_*`）条件就全满足了，
    ///   玩家在传送点按两下 F 就把「这里就是我的洞府吗」「这里便是你的灵田了」听完，
    ///   接着阶段19「NPC到位」（坐标空 → 以玩家脚下为基准）当场完成、
    ///   阶段20 的「灵田不是白来的…」心法**在宗门就传了** —— 等真走进洞府只剩「开一块田」。
    ///
    /// 【口径】标记集合是**全局**的，所以进新场景时必须把别的 `场景_*` 摘掉，
    ///   否则"进过一次洞府"会永久生效（那就等于没门禁）。
    /// </summary>
    public const string 场景标记前缀 = "场景_";

    /// <summary>上一次写进去的场景标记（切场景时拿它对比，避免每帧刷日志）</summary>
    static string 上次场景标记 = "";

    static void 补场景标记(Scene 场景)
    {
        string 该有 = 场景标记前缀 + 场景.name;
        if (上次场景标记 == 该有) return;

        var 全部 = 对话标记.全部标记();
        for (int i = 0; i < 全部.Length; i++)
        {
            string m = 全部[i];
            if (string.IsNullOrEmpty(m)) continue;
            if (!m.StartsWith(场景标记前缀, System.StringComparison.Ordinal)) continue;
            if (m == 该有) continue;
            对话标记.移除(m);           // 上一个场景的，摘掉
        }
        对话标记.添加(该有);
        上次场景标记 = 该有;
        Debug.Log("[场景自举] 场景标记 → " + 该有);
    }

    // ---------------------------------------------------------------- 装配检查

    /// <summary>
    /// 查**补不了但必须有**的装配，缺了就**明确报错**（不静默）。
    ///
    /// 为什么不直接运行时补（踩过的坑 2026-09-27）：
    ///   `跨场景数据` 靠 `OnDestroy` **在旧场景销毁前**把数据拍成快照。
    ///   如果等新场景 `Awake` 才发现缺、临时补一个，那它已经错过了"拍快照"的时机
    ///   （旧的 `OnDestroy` 早跑过了）→ **补了也没用，数据照样丢**。
    ///   而且它必须和 `UIPanelData` 同物件（`GetComponent<UIPanelData>()`）。
    ///   所以这一项只能**编辑器里装配好** —— 由 `CharacterPanelBuilder` 生成，
    ///   这里负责在缺的时候喊出来。
    /// </summary>
    static void 查装配(Scene 场景)
    {
        var 面板 = 找组件<UIPanelData>(场景);
        if (面板 == null) return;                       // 本来就没面板的场景不管

        bool 有 = 面板.GetComponent<跨场景数据>() != null;
        if (有) return;

        if (报过缺接力) return;
        报过缺接力 = true;
        Debug.LogError("[场景自举] 「" + 场景.name + "」的「" + 面板.gameObject.name +
            "」缺 跨场景数据 组件 —— **切场景时背包/已学功法/神通会全丢**。\n" +
            "  它靠 OnDestroy 在旧场景销毁前拍快照，所以运行时补不了。\n" +
            "  修法：跑一次菜单「修仙/构建角色面板 UI」（builder 现在会带上它），" +
            "或用「修仙/体检/场景一致性（并补齐缺失）」。");
    }

    static bool 报过缺接力;

    static T 找组件<T>(Scene 场景) where T : Component
    {
        foreach (var g in 场景.GetRootGameObjects())
        {
            var c = g.GetComponentInChildren<T>(true);
            if (c != null) return c;
        }
        return null;
    }

    // ---------------------------------------------------------------- 单例

    /// <summary>
    /// 场景里没有这个服务就建一个（带 <c>DontDestroyOnLoad</c> 的走各自 Awake 的约定）。
    /// 只补**能在运行时安全凭空造出来**的 —— 需要美术/UI 装配的（UIPanelData / 跨场景数据）
    /// 一律不补，那属于场景装配，缺了应该由"场景一致性体检"报出来让人补。
    /// </summary>
    static void 补单例(Scene 场景)
    {
        if (!场景内有<任务管理器>(场景))
        {
            var go = new GameObject("任务管理器");
            SceneManager.MoveGameObjectToScene(go, 场景);
            go.AddComponent<任务管理器>();
            Debug.Log("[场景自举] 「" + 场景.name + "」没有任务管理器 → 已自动补上");
        }

        // ---- 纪年 / 灵田（2026-10-01 新增）----
        //
        // 【为什么这两个也要自举】它们都是**全局进度**：
        // 时间在任何场景都在走，灵田在任何场景都在长。
        // 只在某个场景手挂的话，切到别的场景就停了 —— 那正是
        // 「场景一致性」那一类反复发作的 bug（见 docs/ai/踩坑总库.md）。
        //
        // 两者都**自带** DontDestroyOnLoad（在各自 Awake 里设），所以只在第一个场景
        // 建一次、之后跨场景都活着。这里**不能**再调 DontDestroyOnLoad —— 本方法是 static。
        if (!场景内有<时间管理器>(场景) && 时间管理器.取() == null)
        {
            var go = new GameObject("时间管理器");
            SceneManager.MoveGameObjectToScene(go, 场景);
            go.AddComponent<时间管理器>();
            Debug.Log("[场景自举] 「" + 场景.name + "」补上 时间管理器（纪年/修炼机会）");
        }

        if (!场景内有<灵田>(场景) && 灵田.取() == null)
        {
            var go = new GameObject("灵田");
            SceneManager.MoveGameObjectToScene(go, 场景);
            go.AddComponent<灵田>();
            Debug.Log("[场景自举] 「" + 场景.name + "」补上 灵田");
        }
    }

    // ---------------------------------------------------------------- 玩家 / 相机

    static void 补玩家与相机(Scene 场景)
    {
        // ---- 玩家：演出锁（过场/对话期间锁操作）----
        var 玩家 = 找玩家(场景);
        if (玩家 != null)
        {
            确保组件<演出锁>(玩家, 场景.name);
            // 玩家外观：有皮肤系统的场景才需要；缺了就补（它 Start 里会按规则选一件）
            确保组件<玩家外观>(玩家, 场景.name);
            // 主动神通开关：**技能槽空着就把 ActiveSkillCaster 关掉**（口径统一）。
            // 2026-09-29 补进来 —— 以前只有 village / Sect 手挂了它，
            // 3C_Testbed / Sect_Wilderness / Demon-Suppressing Tower 一直缺；
            // 它不在自举名单里，所以这个漂移永远抹不平。见 工作日志 §27。
            确保组件<主动神通开关>(玩家, 场景.name);
        }

        // ---- 主相机：机位旋转工具（临时调角度用，可随时摘掉）----
        //
        // ★ 2026-10-01：**这里必须顺手把「显示角度」关掉。**
        //
        // 【踩过的坑】本方法每次进场景都会 `AddComponent<CameraYawRotator>()`
        // （只要主相机上没有），而那个脚本的字段默认值会被**原样当成配置** ——
        // 于是「显示角度」永远是默认的 true，屏幕左上角一直挂着
        // 「机位 yaw = 45.0°（【 左转 / 】 右转）」那行提示。
        // 用户要求在场景里把它关掉时，**改场景根本没用** —— 运行时又被这里补回来了。
        // 实测：场景文件里存的是 0，运行时读出来却是 True。
        //
        // 所以补组件这件事不能只看「有没有」，凡是**「补」出来的**都要显式设成
        // 我们想要的初始状态，不能指望脚本默认值。
        var 相机 = 找主相机(场景);
        if (相机 != null)
        {
            var 补出来的 = 确保组件<CameraYawRotator>(相机, 场景.name);
            // 「补」和「已有」都设一遍：玩家在场景里手改过就尊重场景值，
            // 但**我们不希望它默认开着**，所以统一在这一层关掉提示。
            foreach (var r in 相机.GetComponents<CameraYawRotator>())
                if (r != null && r.显示角度) r.显示角度 = false;
            if (补出来的 != null) 补出来的.显示角度 = false;

            // ★ 全局调色后处理（Built-in 管线，自写 OnRenderImage）。
            //   放在这里补，是为了**每个场景都自动有** ——
            //   写进场景反而会漂移（某个场景忘了挂就没有统一调色）。
            //   组件已经存在（手动挂过、调过参数）时不动它。
            //
            // ⚠️ **顺序就是后处理链的顺序**（同一相机上多个 `OnRenderImage` 按组件顺序调用）：
            //   目标链 = **描边 → 辉光 → 调色**。
            //   所以 `GameGlobalOutline` 必须在这两行**之前**补：后补的排在后面执行。
            确保组件<GameGlobalOutline>(相机, 场景.name);

            确保组件<GameGlobalGrade>(相机, 场景.name);

            // ★ 辉光（Bloom）后处理。**必须在调色之前跑**，否则辉光不会被调色、
            //   会和画面脱节。顺序靠 `[DefaultExecutionOrder(-100)]` 保证
            //   （见 GameGlobalBloom 的注释）。
            //   同样在这里自举补，不写进场景。
            确保组件<GameGlobalBloom>(相机, 场景.name);

            // ★ 画面基线实时预览（F2 切档）。纯调试工具，定下基线后可以删。
            确保组件<画面基线预览>(相机, 场景.name);

            // ★ 纪年 HUD（右上角）+ 灵田界面（F4）+ 任务引导（左侧主线追踪）。
            //   同样自举补 —— UI 是「每个场景一份」的重灾区（见 场景一致性与踩坑规律）。
            //   三个都是 ScreenSpaceOverlay 自搭 Canvas，挂相机上只是为了有个不销毁的宿主。
            确保组件<纪年HUD>(相机, 场景.name);
            确保组件<灵田界面>(相机, 场景.name);
            确保组件<任务引导>(相机, 场景.name);

            // ★ 日常循环（第四阶段）：四件事的状态机（收获 / 炼制 / 打坐 / 入塔）。
            确保组件<日常循环>(相机, 场景.name);
        }

        // ---- 功德堂兑换（宗门里那栋"功德堂"建筑）----
        // 同上：那栋建筑在 Sect.scene 里，`StationInteractable` 上的"界面预制体"是空 ——
        // 不碰场景，运行时补一个自己画面的组件上去。
        补功德堂(场景);

        // ---- HUD 层：把"层级比 HUD 还低"的全屏面板抬上来 ----
        //
        // 常驻 HUD（纪年 1520 / 任务引导 1500）现在都压在**面板层之下**，
        // 但场景里的 `CharacterUI`（角色面板）是 **sortingOrder = 0** —— 比 HUD 还低，
        // 于是打开角色面板时左边那块追踪牌会盖在它上面（用户 2026-10-02 实测报过这个）。
        // 照老规矩**不碰场景文件**，运行时抬上来。
        补HUD层(场景);
    }

    /// <summary>角色面板在所有游玩场景里都是 `sortingOrder = 0`（场景里的默认值）—— 抬到 HUD 之上</summary>
    static void 补HUD层(Scene 场景)
    {
        foreach (var 根 in 场景.GetRootGameObjects())
            foreach (var c in 根.GetComponentsInChildren<Canvas>(true))
            {
                if (c == null || c.name != 角色面板物体名) continue;

                // ★ 顺带把「水墨换皮」补上（同样**不碰场景文件**）：
                //   角色面板是编辑器生成的、**序列化进 5 个游玩场景**，重新生成会把运行时 UI 列表一起重排
                //   （踩坑 A8：写脏过 +19787 行）⇒ 所以这一版走"运行时按节点名换素材"。
                if (c.GetComponent<UIInkSkin>() == null)
                {
                    c.gameObject.AddComponent<UIInkSkin>();
                    Debug.Log("[场景自举] 「" + 场景.name + "」的「" + c.name + "」缺 UIInkSkin → 已自动补上（UI 水墨换皮）");
                }

                if (c.sortingOrder >= HUD层下限) return;      // 已经比 HUD 高了，不动
                int 旧 = c.sortingOrder;
                c.sortingOrder = 角色面板层级;
                Debug.Log("[场景自举] 「" + 场景.name + "」的「" + c.name + "」sortingOrder "
                    + 旧 + " → " + 角色面板层级 + "（它是全屏面板，必须能盖住 HUD）");
                return;
            }
    }

    const string 角色面板物体名 = "CharacterUI";

    /// <summary>HUD 层下限：任务引导 1500（最低的常驻 HUD）—— 1400 是判定阈值</summary>
    const int HUD层下限 = 1400;

    /// <summary>角色面板层级：高于 HUD 层，低于对话(2600)与暂停菜单(3000)</summary>
    const int 角色面板层级 = 2450;

    /// <summary>给宗门的"功德堂"建筑补上兑换面板（按名字找，找不到就什么都不做）</summary>
    static void 补功德堂(Scene 场景)
    {
        foreach (var 根 in 场景.GetRootGameObjects())
            foreach (var t in 根.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != 功德堂物体名) continue;
                if (t.GetComponent<StationInteractable>() == null) continue;
                if (t.GetComponent<功德堂兑换>() == null)
                {
                    t.gameObject.AddComponent<功德堂兑换>();
                    Debug.Log("[场景自举] 「" + 场景.name + "」的「" + t.name + "」缺 功德堂兑换 → 已自动补上");
                }
                return;
            }
    }

    /// <summary>宗门里"功德堂"那栋建筑的对象名（它的 `StationInteractable.显示名` = 功德堂）</summary>
    const string 功德堂物体名 = "environment_Building_luoxiaguan_001_d";

    /// <summary>确保有该组件；**返回补出来的那个**（已有则返回 null）</summary>
    static T 确保组件<T>(GameObject go, string 场景名) where T : Component
    {
        var 已有 = go.GetComponent<T>();
        if (已有 != null) return null;
        var 新 = go.AddComponent<T>();
        Debug.Log("[场景自举] 「" + 场景名 + "」的「" + go.name + "」缺 " + typeof(T).Name + " → 已自动补上");
        return 新;
    }

    static bool 场景内有<T>(Scene 场景) where T : Component
    {
        foreach (var g in 场景.GetRootGameObjects())
            if (g.GetComponentInChildren<T>(true) != null) return true;
        return false;
    }

    static GameObject 找玩家(Scene 场景)
    {
        foreach (var g in 场景.GetRootGameObjects())
        {
            if (g.name == "Player") return g;
            if (g.GetComponentInChildren<PlayerVitals>(true) != null) return g;
        }
        return null;
    }

    static GameObject 找主相机(Scene 场景)
    {
        foreach (var g in 场景.GetRootGameObjects())
        {
            if (g.name == "Main Camera") return g;
            var cam = g.GetComponentInChildren<Camera>(true);
            if (cam != null && cam.CompareTag("MainCamera")) return cam.gameObject;
        }
        // 兜底：场景里任意一台相机
        var 任意 = Object.FindObjectOfType<Camera>();
        return 任意 != null ? 任意.gameObject : null;
    }
}
