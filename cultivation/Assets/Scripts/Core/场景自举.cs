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
