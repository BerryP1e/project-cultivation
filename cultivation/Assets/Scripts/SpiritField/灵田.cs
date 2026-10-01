using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **洞府灵田** —— 挂机养成的地基。
///
/// ## 它管什么
///
/// · 若干**格子**（<see cref="格数"/>），每格种一株灵植
/// · 灵植按**游戏内时间**生长（时间来自 <see cref="时间管理器"/>）
/// · 成熟后**一键收取**，产物进背包
/// · 灵田**品阶**决定「能种什么」和「产量」
///
/// ## 生长是怎么推进的（关键设计）
///
/// 每帧按 `时间管理器` 的**当天进度增量**累加到每格的 <see cref="灵田格.已生长天数"/>，
/// 并且乘上灵田的**生长倍率**。
///
/// 【为什么用"累加"而不是"记种下的日期"】
/// 记日期的话，中途改「成熟天数」或升级灵田品阶，**已经种下的那一批不会跟着变**，
/// 会出现"同样两株草、一株 3 天熟一株 5 天熟"的怪事。
/// 累加式则天然自洽：升级灵田立刻让所有在长的作物加速。
///
/// ## 为什么它是"挂机"的
///
/// 生长**不需要玩家做任何事** —— 没有浇水、没有施肥。
/// 玩家只要偶尔回来点「一键收取」。这正是需求里"无养护损耗、全天候自动生长"。
///
/// ## 单例与自举
///
/// 走项目惯例：静态 <see cref="实例"/>，没有就由 `场景自举` 补一个。
/// 它**不写进场景**，避免"某个场景忘了挂"。
/// </summary>
[DisallowMultipleComponent]
public class 灵田 : MonoBehaviour
{
    // ============================================================ 单例

    static 灵田 实例;
    public static 灵田 取()
    {
        if (实例 != null) return 实例;
        实例 = FindObjectOfType<灵田>();
        if (实例 != null) return 实例;
        var go = new GameObject("灵田");
        DontDestroyOnLoad(go);
        实例 = go.AddComponent<灵田>();
        return 实例;
    }

    // ============================================================ 配置

    [Header("灵田")]
    [Tooltip("品阶。决定能种什么、产多少、长多快")]
    public 灵田品阶 品阶 = 灵田品阶.一阶下品;

    [Tooltip("格子数上限。后续任务/升级可以扩")]
    [Min(1)] public int 格数 = 6;

    [Header("外观")]
    [Tooltip("灵田在场景里的位置（洞府后山）。由任务或手动摆")]
    public Vector3 田位置 = Vector3.zero;

    [Tooltip("每格在世界里的排布：每行几格")]
    [Min(1)] public int 每行格数 = 3;

    [Tooltip("格与格的间距（米）")]
    public float 格间距 = 1.2f;

    [Tooltip("田块朝向（Y 轴角度）")]
    public float 朝向 = 0f;

    // ============================================================ 运行时

    [Header("状态（只读）")]
    [SerializeField] List<灵田格> 格子 = new List<灵田格>();

    /// <summary>已经生成出来的田块根节点（视觉）</summary>
    GameObject 田根;

    /// <summary>格子的静态访问</summary>
    public IReadOnlyList<灵田格> 所有格 => 格子;

    /// <summary>成熟可收的格数</summary>
    public int 可收格数
    {
        get
        {
            int n = 0;
            foreach (var g in 格子) if (g != null && g.已成熟) n++;
            return n;
        }
    }

    /// <summary>种着东西的格数</summary>
    public int 已种格数
    {
        get
        {
            int n = 0;
            foreach (var g in 格子) if (g != null && !g.是空的) n++;
            return n;
        }
    }

    // ============================================================ 事件

    /// <summary>任何一格的状态变了（种下 / 成熟 / 收走 / 升级）—— UI 和视觉都听它</summary>
    public event System.Action 变化;
    /// <summary>收成了一格：参数 = 产物id、数量</summary>
    public event System.Action<string, int> 收获;

    // ============================================================ 生命周期

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(this); return; }
        实例 = this;
        补齐格子();
    }

    void Start()
    {
        按点位摆好();
        // 切场景后重新摆一次：灵田是 DontDestroyOnLoad 的，场景换了点位也换了
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= 处理场景加载;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += 处理场景加载;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= 处理场景加载;
    }

    void 处理场景加载(UnityEngine.SceneManagement.Scene 场景, UnityEngine.SceneManagement.LoadSceneMode 模式)
    {
        按点位摆好();
    }

    // 订阅放 OnEnable/OnDisable（成对），并且**先 `-=` 再 `+=`** ——
    // 这样即使 OnEnable 被重复调用也不会叠加订阅。
    // 见 纪年HUD 里记的那个坑：「新的一天」日志打 3 遍，就是订阅叠了三份。
    void OnEnable() => 订阅时间();
    void OnDisable() => 退订时间();

    void 订阅时间()
    {
        var t = 时间管理器.取();
        if (t == null) return;
        t.过了一天 -= 处理过了一天;
        t.过了一天 += 处理过了一天;
    }

    void 退订时间()
    {
        var t = 时间管理器.取();
        if (t != null) t.过了一天 -= 处理过了一天;
    }

    void Update()
    {
        // 「可收」标记要面向相机 —— 每帧一次，很便宜（只是设置几个 rotation）
        灵田外观.每帧(this);

        // 生长按**帧**推进（用同一条时间尺度算出的日内增量），这样种下的那一刻就开始长，
        // 不用等到跨天。跨天只是额外触发一次"刷新提示"。
        if (格子 == null || 格子.Count == 0) return;

        var t = 时间管理器.取();
        if (t == null || t.暂停) return;

        float 增量天 = Time.deltaTime * t.倍率 / Mathf.Max(1f, t.秒每天);
        if (增量天 <= 0f) return;

        推进生长(增量天);
    }

    /// <summary>推进所有在长的格子。参数 = 过了多少游戏日（未乘灵田倍率）</summary>
    public void 推进生长(float 增量天)
    {
        if (增量天 <= 0f) return;
        float 倍率 = 灵田品阶说明.生长倍率(品阶);
        bool 有成熟 = false;

        foreach (var g in 格子)
        {
            if (g == null || g.是空的 || g.已收获) continue;
            if (g.已成熟) continue;                       // 已成熟的不用再长
            float 旧进度 = g.进度;
            g.已生长天数 += 增量天 * 倍率;
            if (!有成熟 && 旧进度 < 1f && g.进度 >= 1f) 有成熟 = true;
        }

        if (有成熟)
        {
            变化?.Invoke();
            灵田外观.通知刷新(this);
        }
    }

    void 处理过了一天(int 天)
    {
        // 跨天时刷新一次（让"还剩几天"之类的提示跟上，也让视觉重新评估阶段）
        变化?.Invoke();
        灵田外观.通知刷新(this);
    }

    // ============================================================ 操作

    /// <summary>这个格子能不能种这种种子</summary>
    public bool 能种(int 格号, string 种子id, out string 原因)
    {
        原因 = "";
        if (格号 < 0 || 格号 >= 格子.Count) { 原因 = "没有这个格子"; return false; }
        var g = 格子[格号];
        if (!g.是空的) { 原因 = "这一格已经种了"; return false; }
        var d = 灵植库.取(种子id);
        if (d == null) { 原因 = "没有这种种子"; return false; }
        if (!灵植库.可种(d, 品阶))
        {
            原因 = $"需要 {灵田品阶说明.中文名(d.品阶)} 及以上的灵田（当前 {灵田品阶说明.中文名(品阶)}）";
            return false;
        }
        if (!string.IsNullOrEmpty(d.种子id) && 数种子(d.种子id) <= 0)
        {
            var 种 = 取物品(d.种子id);
            原因 = "没有「" + (种 != null ? 种.物品名 : d.种子id) + "」了 —— 种子靠任务奖励获得";
            return false;
        }
        return true;
    }

    /// <summary>播种。返回是否成功</summary>
    public bool 播种(int 格号, string 种子id)
    {
        string 原因;
        if (!能种(格号, 种子id, out 原因))
        {
            Debug.LogWarning("[灵田] 种不了：" + 原因);
            return false;
        }
        var g = 格子[格号];
        g.种子id = 种子id;
        g.已生长天数 = 0f;
        g.已收获 = false;

        var d = 灵植库.取(种子id);

        // 扣掉一颗种子（能种() 已经确认够了，这里不会失败）
        if (d != null && !string.IsNullOrEmpty(d.种子id)) 扣种子(d.种子id, 1);
        Debug.Log($"[灵田] 第 {格号 + 1} 格种下「{(d != null ? d.名 : 种子id)}」，"
                  + $"{d?.成熟天数:F0} 天后成熟（灵田生长倍率 ×{灵田品阶说明.生长倍率(品阶):F2}）");

        变化?.Invoke();
        灵田外观.通知刷新(this);
        return true;
    }

    /// <summary>找一个空格子。没有就返回 -1</summary>
    public int 找空格()
    {
        for (int i = 0; i < 格子.Count; i++)
            if (格子[i] != null && 格子[i].是空的) return i;
        return -1;
    }

    /// <summary>收一格。返回实际收到的数量（0 = 没收到）</summary>
    public int 收一格(int 格号, bool 直接进背包 = true)
    {
        if (格号 < 0 || 格号 >= 格子.Count) return 0;
        var g = 格子[格号];
        if (g == null || !g.已成熟) return 0;

        var d = g.植;
        int 量 = 灵植库.掷产量(d, 品阶);
        string 产物 = d != null ? d.产物id : "";

        g.清空();

        if (直接进背包 && !string.IsNullOrEmpty(产物) && 量 > 0)
            进背包(产物, 量);

        收获?.Invoke(产物, 量);
        变化?.Invoke();
        灵田外观.通知刷新(this);

        Debug.Log($"[灵田] 收获「{(d != null ? d.名 : "?")}」×{量}");
        return 量;
    }

    /// <summary>**一键收取**所有成熟的格子。返回 (总株数, 明细文本)</summary>
    public int 一键收取(out string 明细)
    {
        int 总 = 0;
        var 计 = new Dictionary<string, int>();
        for (int i = 0; i < 格子.Count; i++)
        {
            var g = 格子[i];
            if (g == null || !g.已成熟) continue;
            var d = g.植;
            int 量 = 灵植库.掷产量(d, 品阶);
            string 产物 = d != null ? d.产物id : "";
            string 名 = d != null ? d.名 : "灵植";

            g.清空();
            if (!string.IsNullOrEmpty(产物) && 量 > 0) 进背包(产物, 量);

            if (!计.ContainsKey(名)) 计[名] = 0;
            计[名] += 量;
            总 += 量;
            收获?.Invoke(产物, 量);
        }

        var 段 = new List<string>();
        foreach (var kv in 计) 段.Add($"{kv.Key} ×{kv.Value}");
        明细 = 段.Count > 0 ? string.Join("、", 段) : "没有成熟可收的灵植";

        if (总 > 0)
        {
            变化?.Invoke();
            灵田外观.通知刷新(this);
            Debug.Log("[灵田] 一键收取：" + 明细);
        }
        return 总;
    }

    /// <summary>升级灵田品阶（后续任务/贡献消耗用）</summary>
    public bool 升级品阶()
    {
        if (品阶 >= 灵田品阶.三阶上品) return false;
        品阶 = (灵田品阶)((int)品阶 + 1);
        Debug.Log($"[灵田] 品阶提升 → {灵田品阶说明.中文名(品阶)}"
                  + $"（产量 ×{灵田品阶说明.产量倍率(品阶):F2}，生长 ×{灵田品阶说明.生长倍率(品阶):F2}）");
        变化?.Invoke();
        灵田外观.通知刷新(this);
        return true;
    }

    /// <summary>扩格子（后续任务用）</summary>
    public void 扩容(int 加到格数)
    {
        if (加到格数 <= 格数) return;
        格数 = 加到格数;
        补齐格子();
        变化?.Invoke();
        灵田外观.通知刷新(this);
    }

    void 补齐格子()
    {
        if (格子 == null) 格子 = new List<灵田格>();
        while (格子.Count < 格数) 格子.Add(new 灵田格());
        while (格子.Count > 格数) 格子.RemoveAt(格子.Count - 1);
    }

    // ============================================================ 背包接线

    /// <summary>按 id 找物品定义。走项目唯一的物品库（QuestDatabase.物品库）</summary>
    static ItemDefinition 取物品(string 物品id)
    {
        var 库 = QuestDatabase.取();
        return 库 != null ? 库.找物品(物品id) : null;
    }

    static UIPanelData 取面板()
    {
        var 板 = FindObjectOfType<UIPanelData>();
        if (板 != null) 板.EnsureLists();
        return 板;
    }

    /// <summary>背包里有几个这种物品（种子够不够就靠它）</summary>
    static int 数种子(string 物品id)
    {
        var 定义 = 取物品(物品id);
        var 板 = 取面板();
        if (定义 == null || 板 == null) return 0;
        return 板.物品数量(定义);
    }

    /// <summary>从背包扣掉若干个</summary>
    static void 扣种子(string 物品id, int 数量)
    {
        var 定义 = 取物品(物品id);
        var 板 = 取面板();
        if (定义 == null || 板 == null) return;
        if (板.移除物品(定义, 数量)) 板.RaiseChanged();
    }

    /// <summary>
    /// 把产物塞进背包。
    ///
    /// 走项目里**唯一那条正式入口**：`QuestDatabase.物品库` 按 id 找定义 →
    /// `UIPanelData.给物品`。这是任务奖励也用的一条路，所以灵田产出的东西
    /// 和任务发的完全等价（存档、面板、使用器都认得）。
    /// </summary>
    static void 进背包(string 物品id, int 数量)
    {
        if (数量 <= 0 || string.IsNullOrEmpty(物品id)) return;

        var 库 = QuestDatabase.取();
        var 定义 = 库 != null ? 库.找物品(物品id) : null;
        if (定义 == null)
        {
            Debug.LogWarning($"[灵田] 物品库（QuestDatabase.物品库）里没有「{物品id}」——"
                             + "产物不会进背包。先跑菜单「修仙/从配置表生成资产」，"
                             + "或确认物品表里有这个 id");
            return;
        }

        var 面板 = FindObjectOfType<UIPanelData>();
        if (面板 == null)
        {
            Debug.LogWarning("[灵田] 场景里找不到 UIPanelData，产物暂时没地方放");
            return;
        }

        面板.EnsureLists();
        面板.给物品(定义, 数量);
        面板.RaiseChanged();
    }

    // ============================================================ 视觉

    /// <summary>确保田块的视觉已经生成。位置由 <see cref="田位置"/> 决定</summary>
    public void 确保外观()
    {
        if (田根 == null)
        {
            田根 = 灵田外观.建造(this, transform);
        }
        灵田外观.刷新(this, 田根);
    }

    /// <summary>
    /// **按场景里的点位标记自动摆位**（场景加载时调）。
    ///
    /// 标记 = 场景里任一名字为 <see cref="灵田点位名"/> 的物体。
    /// 找不到就保持 <see cref="田位置"/> 不动（开发期手动摆也能用）。
    ///
    /// 【为什么用"按名字找场景物体"而不是序列化引用】
    /// 项目的固定做法（见 `黑幕字幕.找场景物体` / `Teleporter`）——
    /// 场景里的引用会随场景重做而漂移，按名字找是**幂等**的。
    /// </summary>
    public const string 灵田点位名 = "洞府灵田点位";

    public void 按点位摆好()
    {
        var 场景 = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!场景.IsValid() || !场景.isLoaded) return;

        foreach (var go in 场景.GetRootGameObjects())
        {
            var 找到 = 找子物体(go.transform, 灵田点位名);
            if (找到 != null)
            {
                摆到(找到.position, 找到.eulerAngles.y);
                Debug.Log("[灵田] 已按点位「" + 灵田点位名 + "」摆到 " + 找到.position);
                return;
            }
        }
    }

    static Transform 找子物体(Transform 根, string 名)
    {
        if (根.name == 名) return 根;
        for (int i = 0; i < 根.childCount; i++)
        {
            var r = 找子物体(根.GetChild(i), 名);
            if (r != null) return r;
        }
        return null;
    }

    /// <summary>把田块挪到某个位置（任务里"大师兄带你去后山"时用）</summary>
    public void 摆到(Vector3 世界位置, float 朝向角度 = 0f)
    {
        田位置 = 世界位置;
        朝向 = 朝向角度;
        if (田根 != null)
        {
            田根.transform.position = 世界位置;
            田根.transform.rotation = Quaternion.Euler(0f, 朝向角度, 0f);
        }
        else 确保外观();
    }

    // ============================================================ 存档

    public void 导出(out int 品阶值, out int 格数存, out List<string> 格)
    {
        品阶值 = (int)品阶;
        格数存 = 格数;
        格 = new List<string>();
        foreach (var g in 格子) 格.Add(g == null ? "-" : g.导出());
    }

    public void 导入(int 品阶值, int 格数存, List<string> 格)
    {
        品阶 = (灵田品阶)Mathf.Clamp(品阶值, 1, (int)灵田品阶.三阶上品);
        格数 = Mathf.Max(1, 格数存);
        格子 = new List<灵田格>();
        if (格 != null)
            foreach (var s in 格) 格子.Add(灵田格.导入(s));
        补齐格子();
        变化?.Invoke();
        灵田外观.通知刷新(this);
    }

    public void 重置()
    {
        品阶 = 灵田品阶.一阶下品;
        格数 = 6;
        格子 = new List<灵田格>();
        补齐格子();
        变化?.Invoke();
        灵田外观.通知刷新(this);
    }

    // ---- ASCII 别名 ----
    public int GridCount => 格数;
    public int ReadyCount => 可收格数;
    public bool Plant(int index, string seedId) => 播种(index, seedId);
    public int HarvestAll(out string detail) => 一键收取(out detail);
}
