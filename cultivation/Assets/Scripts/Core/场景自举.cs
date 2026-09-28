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
        for (int i = 0; i < 跳过场景.Length; i++)
            if (场景.name == 跳过场景[i]) return;

        补单例(场景);
        补玩家与相机(场景);
        查装配(场景);
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
        var 相机 = 找主相机(场景);
        if (相机 != null)
        {
            确保组件<CameraYawRotator>(相机, 场景.name);
        }
    }

    static void 确保组件<T>(GameObject go, string 场景名) where T : Component
    {
        if (go.GetComponent<T>() != null) return;
        go.AddComponent<T>();
        Debug.Log("[场景自举] 「" + 场景名 + "」的「" + go.name + "」缺 " + typeof(T).Name + " → 已自动补上");
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
