using UnityEngine;

/// <summary>
/// **场景里的一块田地**：外观 + 信息牌 + 「走近按 F」的交互。
///
/// ## 它不持有数据
///
/// 状态在 <see cref="灵田"/>（跨场景单例）里，按 <see cref="编号"/> 取。
/// 本组件只是"这块地在场景里的那个物件"。
/// 【为什么要分开】灵田是 `DontDestroyOnLoad` 单例（逻辑要跨场景继续长），
/// 而地块物件必须属于当前场景 —— 分开之后，"跨场景继续长"和"外观只在洞府出现"
/// 两件事天然不冲突（踩坑 B57）。
///
/// ## 交互
///
/// · **走近就能看见信息牌**（<see cref="灵田地块牌"/>，带底板，不用按 F）——
///   用户 2026-10-01 要求；地块的 `StationInteractable.显示靠近提示` 因此**关掉**，
///   免得和通用提示框重复。
/// · **按 F** 弹出只针对这一块地的小窗（<see cref="灵田地块界面"/>），
///   里面只有 **种植 / 收获 / 挪动地块 / 升级这块地**。
///
/// ## 面板由地块自己造
///
/// 窗口必须知道"是哪一块地"，而 `StationInteractable.界面预制体` 是**共用预制体**、
/// 装不下这个上下文。所以和 `Teleporter` 一样：**谁的地盘谁造面板**
/// （见 `StationInteractor` 里那条 `灵田地块` 分支）。
/// </summary>
[DisallowMultipleComponent]
public class 灵田地块 : MonoBehaviour
{
    [Tooltip("是第几块地（对应 灵田.状态(编号)）。由 灵田 生成视图时填")]
    public int 编号;

    [Tooltip("玩家离多近才能按 F（米）")]
    public float 交互距离 = 2.6f;

    public 灵田地块状态 状态
    {
        get
        {
            var 田 = 灵田.取();
            return 田 != null ? 田.状态(编号) : null;
        }
    }

    StationInteractable 交互;
    灵田地块外观.视觉 视;
    灵田地块牌 牌;
    GameObject 面板;

    /// <summary>当前打开的窗口（null = 没开）。交给 StationInteractor 记着，ESC 才关得掉</summary>
    public GameObject 面板根 => 面板;

    // ============================================================ 生命周期

    void Awake() => 确保交互();

    void Start()
    {
        if (状态 == null) return;

        视 = 灵田地块外观.建造(this, transform);
        牌 = new 灵田地块牌();
        牌.建造(transform);

        灵田地块外观.刷新(视, 状态);
        牌.刷新(状态, 编号, true);
        同步提示词(true);
    }

    void Update()
    {
        var b = 状态;
        if (b == null || 视 == null) return;

        // 状态一变（种下 / 成熟 / 收回 / 升级），土床材质和作物模型都跟着变。
        // 【这条就是上一轮那个"开拓完模型不变"的教训】：**别只在 Start 刷一次**。
        灵田地块外观.刷新(视, b);
        牌.刷新(b, 编号, true);
        牌.每帧(transform.position, 取玩家());
        同步提示词();
    }

    /// <summary>
    /// 玩家（相机是跟在玩家后上方的，量"走近没走近"必须用玩家自己的位置 ——
    /// 用相机会差出好几米，见 <see cref="灵田地块牌.每帧"/> 的说明）。
    /// 缓存起来，别每帧 `FindObjectOfType`。
    /// </summary>
    Transform 玩家缓存;
    Transform 取玩家()
    {
        if (玩家缓存 != null) return 玩家缓存;
        var 命 = Object.FindObjectOfType<PlayerVitals>();
        if (命 != null) 玩家缓存 = 命.transform;
        return 玩家缓存;
    }

    void OnDestroy() => 牌?.销毁();

    void 确保交互()
    {
        交互 = GetComponent<StationInteractable>();
        if (交互 == null) 交互 = gameObject.AddComponent<StationInteractable>();

        交互.类型 = StationInteractable.StationKind.灵田地块;
        交互.交互距离 = 交互距离;

        // ★ **键帽提示要开着**。用户实测反馈：「靠近灵田没有按 F 互动的提示了」——
        //   上一版把它关了（想着信息牌里已经写了 `[F] 种植`），但那个键帽小框
        //   是项目里统一的"按 F 交互"提示，玩家认的就是它。
        //   现在分工：**信息牌报状态**（第几块田/待种植/长到哪了），
        //   **键帽提示报操作**（`[F] 种植` / `[F] 收获`）。信息牌里不再重复写 [F]。
        交互.显示靠近提示 = true;
        交互.提示最高点 = 1.6f;      // 田很矮，提示压在 1.6 米以内就一直在视野里

        交互.界面预制体 = null;     // 面板由本组件自己造

        同步提示词(true);
    }

    /// <summary>按状态换 `StationInteractable.显示名`（内部用；信息牌是主要显示，这里留个兜底）</summary>
    void 同步提示词(bool 强制 = false)
    {
        if (交互 == null) return;
        var b = 状态;
        string 词 = b == null ? "灵田" : (b.待种植 ? "种植" : (b.已成熟 ? "收获" : "照看"));
        if (强制 || 交互.显示名 != 词) 交互.显示名 = 词;
    }

    // ============================================================ 面板

    public void 开面板()
    {
        if (面板 != null) return;
        面板 = 灵田地块界面.造(this);
        Debug.Log("[灵田] 打开第 " + (编号 + 1) + " 块地的操作窗");
    }

    public void 关面板()
    {
        if (面板 != null) Destroy(面板);
        面板 = null;
    }
}
