using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **洞府灵田 —— 地块的管理者（数据 + 生长 + 摆放校验）**。
///
/// ## 它管什么
///
/// · 一份**地块表**（<see cref="地块"/>，每块地自己的品阶/作物/生长量/**位置与朝向**）
/// · 按游戏内时间推进所有地块的生长（时间来自 <see cref="时间管理器"/>）
/// · **能不能摆在某处 / 摆下去 / 挪动** —— 用户要求的"在洞府里有地面、有空间的地方任意摆放"
/// · 播种 / 收获 / 升级 的校验与执行
/// · 进场景时把地块视图搭出来（视图是纯表现，见 <see cref="灵田地块"/>）
///
/// ## 和上一版的区别（2026-10-01 第二次重构）
///
/// 上一版是**固定 12 块地**（按场景点位算网格），其中 4 块熟地 8 块荒地。
/// 用户要求改成**玩家自己摆**：
/// > 「现在的灵田是固定位置的，我希望个人洞府是能够自定义的……
/// >   在背包中找到灵田开拓令……可以在整个洞府中有地面的有空间的地方任意的摆放。」
/// > 「把待开发的灵田的模型转变为**待种植**的灵田模型。」
///
/// ⇒ 所以：**没有"荒地"了**，地块是玩家用 `item_lingtian_kaituo`（灵田开拓令）摆下来的，
/// 摆下来**就是待种植的熟地**；"开拓"这个动作 = 把地摆下来。
///
/// ## ⚠️ 数据与视图是分开的（这条很重要）
///
/// 灵田是 `DontDestroyOnLoad` 单例（**逻辑要跨场景继续长**），
/// 但地块视图必须属于当前场景 —— 否则切场景时那一片土床会跟着玩家留在宗门里
/// （用户报过的「Sect 里有个棕色的片状物」，踩坑 B57）。
/// 所以：**状态**跨场景活着，**视图**每次进场景重建，且**只有在允许摆放的场景才建**。
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

    // ============================================================ 配置 / 常量

    /// <summary>升阶消耗的道具 id（**宗门贡献处兑换**；换兑换物只改这一行）</summary>
    public const string 升阶令id = "item_lingtian_ling";

    /// <summary>开一块新地消耗的道具 id（**从背包里用它在洞府摆一块地**）</summary>
    public const string 开拓令id = "item_lingtian_kaituo";

    [Header("允许摆放的场景")]
    [Tooltip("只有在这些场景里才能用「灵田开拓令」摆地、也才会生成地块视图。\n" +
             "用户要求：「不在洞府场景无法使用开拓令」。\n" +
             "⚠️ 改名场景时记得改这里。")]
    public string[] 允许摆放的场景 = { "3C_Testbed" };

    [Header("摆放校验")]
    [Tooltip("除了地面之外，还检查有没有别的东西挡住（树/岩石/建筑）。\n" +
             "万一某个场景里误判太多（比如地面上有一层看不见的触发体），把它关掉就只判\"压住已有地块\"")]
    public bool 检查场景障碍 = true;

    [Tooltip("地面最大坡度（度）。比这陡的地方不许摆")]
    [Range(0f, 45f)] public float 最大地面坡度 = 15f;

    [Tooltip("地面往下探多远算探到地（米）")]
    public float 探地距离 = 6f;

    // ============================================================ 状态

    [Header("状态（只读，进存档）")]
    [SerializeField] List<灵田地块状态> 地块 = new List<灵田地块状态>();

    /// <summary>所有地块（只读）。下标 = 地块编号</summary>
    public IReadOnlyList<灵田地块状态> 所有地块 => 地块;

    public int 总块数 => 地块.Count;

    /// <summary>**待种植**（空着）的块数</summary>
    public int 待种数
    {
        get
        {
            int n = 0;
            foreach (var b in 地块) if (b != null && b.待种植) n++;
            return n;
        }
    }

    /// <summary>种着东西的地块数</summary>
    public int 已种数
    {
        get
        {
            int n = 0;
            foreach (var b in 地块) if (b != null && !b.待种植) n++;
            return n;
        }
    }

    /// <summary>能收的地块数</summary>
    public int 可收数
    {
        get
        {
            int n = 0;
            foreach (var b in 地块) if (b != null && b.已成熟) n++;
            return n;
        }
    }

    public 灵田地块状态 状态(int i) => (i >= 0 && i < 地块.Count) ? 地块[i] : null;

    /// <summary>当前场景允不允许摆放 / 显示地块</summary>
    public bool 当前场景可摆放
    {
        get
        {
            var 场景 = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!场景.IsValid() || !场景.isLoaded) return false;
            if (允许摆放的场景 == null) return false;
            foreach (var n in 允许摆放的场景)
                if (n == 场景.name) return true;
            return false;
        }
    }

    // ============================================================ 事件

    /// <summary>任何一块地的状态变了（摆下 / 种下 / 成熟 / 收走 / 挪动 / 升级）</summary>
    public event System.Action 变化;

    /// <summary>收成了一块地：参数 = 产物id、数量</summary>
    public event System.Action<string, int> 收获;

    void 通知变化() => 变化?.Invoke();

    // ============================================================ 生命周期

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(this); return; }
        实例 = this;
        if (地块 == null) 地块 = new List<灵田地块状态>();
    }

    void Start()
    {
        重建视图();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= 处理场景加载;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += 处理场景加载;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= 处理场景加载;
    }

    void 处理场景加载(UnityEngine.SceneManagement.Scene 场景, UnityEngine.SceneManagement.LoadSceneMode 模式)
        => 重建视图();

    void Update()
    {
        var t = 时间管理器.取();
        if (t == null || t.暂停) return;
        if (地块 == null || 地块.Count == 0) return;

        // ★ 增量取「时间管理器总天数」的**帧间差**（踩坑 B58），不是拿现实时间反算。
        //   `推进()` 允许一次跨好几天（推进天数 / 大倍率 / 读档补时间）。
        float 现在 = t.总天数;
        float 增量天 = 上次总天数 < 0f ? 0f : 现在 - 上次总天数;
        上次总天数 = 现在;
        if (增量天 <= 0f) return;

        推进生长(增量天);
    }

    float 上次总天数 = -1f;

    /// <summary>推进所有在长的地块。参数 = 过了多少游戏日（未乘品阶倍率）</summary>
    public void 推进生长(float 增量天)
    {
        if (增量天 <= 0f) return;
        bool 有变化 = false;

        foreach (var b in 地块)
        {
            if (b == null || b.待种植 || b.已成熟) continue;
            float 旧进度 = b.进度;
            b.已生长天数 += 增量天 * 灵田品阶说明.生长倍率(b.品阶);
            if (旧进度 < 1f && b.进度 >= 1f) 有变化 = true;      // 刚好熟了一块
        }

        if (有变化) 通知变化();
    }

    // ============================================================ 摆放

    /// <summary>
    /// 这个位置能不能摆一块地。
    /// 三条：**在允许的场景里** / **底下是够平的地面** / **没压住已有地块或场景里的东西**。
    /// </summary>
    public bool 位置可用(Vector3 位置, int 朝向档, out string 原因, int 忽略地块 = -1)
    {
        原因 = "";

        if (!当前场景可摆放)
        {
            原因 = "只能在个人洞府里开垦灵田";
            return false;
        }

        // ---- 地面：往下探，要有地、而且够平 ----
        RaycastHit 地;
        var 起点 = 位置 + Vector3.up * 3f;
        if (!Physics.Raycast(起点, Vector3.down, out 地, 探地距离 + 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            原因 = "这里没有地面";
            return false;
        }
        if (Vector3.Angle(地.normal, Vector3.up) > 最大地面坡度)
        {
            原因 = "这里地面太陡（" + Vector3.Angle(地.normal, Vector3.up).ToString("0") + "°）";
            return false;
        }
        if (Mathf.Abs(地.point.y - 位置.y) > 0.6f)
        {
            原因 = "这里和地面差太多";
            return false;
        }

        // ---- 压住已有地块？----
        var 我 = new Vector2(位置.x, 位置.z);
        for (int i = 0; i < 地块.Count; i++)
        {
            if (i == 忽略地块) continue;
            var b = 地块[i];
            if (b == null) continue;
            if (灵田规格.压住(我, 朝向档, new Vector2(b.位置.x, b.位置.z), b.朝向档))
            {
                原因 = "压住第 " + (i + 1) + " 块地了";
                return false;
            }
        }

        // ---- 压住场景里的东西？----
        // 【怎么区分"地面"和"障碍"】不用图层：**地面就是刚才那根向下射线打到的那个碰撞体**，
        // 把它（和玩家自己）排除掉，剩下的算障碍。这样不用给场景配图层就能用。
        //
        // ⚠️ 【这里用的是"床本身"的半边长，**不含占用边距**】
        //    占用边距（0.35）是给**别的灵田**留的过道，不该拿来判定场景物件 ——
        //    用户要的是"有地面有空间的地方任意摆放"，而实测用含边距的 1.30 米盒子去撞，
        //    连旁边一根细木桩（Npc_WoodenStake）都会把整块地判成"被占着"，根本摆不下去。
        //    场景障碍只在**真的插进畦里**时才算冲突。
        if (检查场景障碍)
        {
            float 半床 = 灵田规格.床边长 * 0.5f;
            var 中心 = new Vector3(位置.x, 地.point.y + 0.5f, 位置.z);
            var 半 = new Vector3(半床, 0.5f, 半床);
            var 撞 = Physics.OverlapBox(中心, 半, Quaternion.Euler(0f, 灵田规格.档转角度(朝向档), 0f),
                                        ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in 撞)
            {
                if (c == null || c == 地.collider) continue;
                if (c is CharacterController) continue;                  // 玩家自己
                if (c.transform.IsChildOf(transform)) continue;          // 自己的东西
                原因 = "这里被「" + c.name + "」占着";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 摆下一块新地（**道具由调用方扣**，这里只管加数据 + 建视图）。
    /// 返回新地块的编号；失败返回 -1。
    /// </summary>
    public int 放置(Vector3 位置, int 朝向档, 灵田品阶 品阶 = 灵田品阶.一阶下品)
    {
        string 原因;
        if (!位置可用(位置, 朝向档, out 原因))
        {
            Debug.LogWarning("[灵田] 摆不了：" + 原因);
            return -1;
        }

        var b = new 灵田地块状态 { 位置 = 位置, 朝向档 = 朝向档, 品阶 = 品阶 };
        地块.Add(b);
        Debug.Log("[灵田] 摆下第 " + 地块.Count + " 块地（待种植）@ " + 位置.ToString("F2")
                  + "  朝向 " + 灵田规格.档转角度(朝向档) + "°");
        重建视图();
        通知变化();
        return 地块.Count - 1;
    }

    /// <summary>
    /// 把第 i 块地挪到新位置（用户选的是"挪动**不退道具也不多花**"）。
    /// </summary>
    public bool 挪动(int i, Vector3 位置, int 朝向档)
    {
        var b = 状态(i);
        if (b == null) return false;

        string 原因;
        if (!位置可用(位置, 朝向档, out 原因, i))
        {
            Debug.LogWarning("[灵田] 挪不了：" + 原因);
            return false;
        }

        b.位置 = 位置;
        b.朝向档 = 朝向档;
        Debug.Log("[灵田] 第 " + (i + 1) + " 块地挪到 " + 位置.ToString("F2"));
        重建视图();
        通知变化();
        return true;
    }

    // ============================================================ 播种 / 收获

    public bool 能种(int i, string 灵植id, out string 原因)
    {
        原因 = "";
        var b = 状态(i);
        if (b == null) { 原因 = "没有这块地"; return false; }
        if (!b.待种植) { 原因 = "这块地已经种了"; return false; }

        var d = 灵植库.取(灵植id);
        if (d == null) { 原因 = "没有这种灵植"; return false; }

        if (!灵植库.可种(d, b.品阶))
        {
            原因 = "需要 " + 灵田品阶说明.中文名(d.品阶) + " 及以上的地"
                 + "（这块地是 " + 灵田品阶说明.中文名(b.品阶) + "）";
            return false;
        }

        if (!string.IsNullOrEmpty(d.种子id) && 数物品(d.种子id) <= 0)
        {
            原因 = "没有「" + 取物品名(d.种子id) + "」了 —— 种子靠任务奖励获得";
            return false;
        }
        return true;
    }

    public bool 播种(int i, string 灵植id)
    {
        string 原因;
        if (!能种(i, 灵植id, out 原因))
        {
            Debug.LogWarning("[灵田] 种不了：" + 原因);
            return false;
        }

        var b = 状态(i);
        var d = 灵植库.取(灵植id);
        b.作物id = 灵植id;
        b.已生长天数 = 0f;
        if (d != null && !string.IsNullOrEmpty(d.种子id)) 扣物品(d.种子id, 1);

        Debug.Log("[灵田] 第 " + (i + 1) + " 块地种下「" + (d != null ? d.名 : 灵植id) + "」，"
                  + (d != null ? d.成熟天数.ToString("F0") : "?") + " 天后成熟");
        通知变化();
        return true;
    }

    /// <summary>收第 i 块地。返回实际收到几株（0 = 没收到）</summary>
    public int 收一块(int i)
    {
        var b = 状态(i);
        if (b == null || !b.已成熟) return 0;

        var d = b.植;
        int 量 = 灵植库.掷产量(d, b.品阶);
        string 产物 = d != null ? d.产物id : "";
        string 名 = d != null ? d.名 : "灵植";

        b.清空作物();
        if (!string.IsNullOrEmpty(产物) && 量 > 0) 进背包(产物, 量);

        收获?.Invoke(产物, 量);
        通知变化();
        Debug.Log("[灵田] 第 " + (i + 1) + " 块地收获「" + 名 + "」×" + 量);
        return 量;
    }

    /// <summary>一键收掉所有成熟的地块</summary>
    public int 一键收取(out string 明细)
    {
        int 总 = 0;
        var 计 = new Dictionary<string, int>();

        for (int i = 0; i < 地块.Count; i++)
        {
            var b = 地块[i];
            if (b == null || !b.已成熟) continue;

            var d = b.植;
            int 量 = 灵植库.掷产量(d, b.品阶);
            string 产物 = d != null ? d.产物id : "";
            string 名 = d != null ? d.名 : "灵植";

            b.清空作物();
            if (!string.IsNullOrEmpty(产物) && 量 > 0) 进背包(产物, 量);

            if (!计.ContainsKey(名)) 计[名] = 0;
            计[名] += 量;
            总 += 量;
            收获?.Invoke(产物, 量);
        }

        var 段 = new List<string>();
        foreach (var kv in 计) 段.Add(kv.Key + " ×" + kv.Value);
        明细 = 段.Count > 0 ? string.Join("、", 段) : "没有成熟可收的灵植";

        if (总 > 0)
        {
            通知变化();
            Debug.Log("[灵田] 一键收取：" + 明细);
        }
        return 总;
    }

    // ============================================================ 升级（每块地各升各的）

    public bool 能升级(int i, out string 原因)
    {
        原因 = "";
        var b = 状态(i);
        if (b == null) { 原因 = "没有这块地"; return false; }
        if (b.已满阶) { 原因 = "已经是最高品阶（" + 灵田品阶说明.中文名(b.品阶) + "）"; return false; }

        int 需等级 = 灵田地块状态.需要境界等级(b.品阶);
        var 修 = FindObjectOfType<PlayerCultivation>();
        int 等级 = 修 != null ? 修.等级 : 1;
        if (等级 < 需等级)
        {
            原因 = "境界不足：需要 " + 需等级 + " 级（当前 " + 等级 + " 级）";
            return false;
        }

        int 需张 = 灵田地块状态.升阶令张数(b.品阶);
        int 有 = 数物品(升阶令id);
        if (有 < 需张)
        {
            原因 = "材料不足：" + 取物品名(升阶令id) + " " + 有 + "/" + 需张;
            return false;
        }
        return true;
    }

    public bool 升级(int i)
    {
        string 原因;
        if (!能升级(i, out 原因))
        {
            Debug.LogWarning("[灵田] 升不了：" + 原因);
            return false;
        }

        var b = 状态(i);
        扣物品(升阶令id, 灵田地块状态.升阶令张数(b.品阶));
        b.品阶 = (灵田品阶)((int)b.品阶 + 1);

        // ★ 到「一阶上品」时附赠赤焰芝种子：赤焰芝要一阶上品才种得下
        string 附 = "";
        if (b.品阶 == 灵田品阶.一阶上品)
        {
            var 种 = 取物品("item_seed_chiyan");
            if (种 != null) { 进背包("item_seed_chiyan", 2); 附 = "；附赠 " + 种.物品名 + "×2"; }
        }

        Debug.Log("[灵田] 第 " + (i + 1) + " 块地升到 " + 灵田品阶说明.中文名(b.品阶)
                  + "（产量 ×" + 灵田品阶说明.产量倍率(b.品阶).ToString("F2")
                  + "，生长 ×" + 灵田品阶说明.生长倍率(b.品阶).ToString("F2") + "）" + 附);
        通知变化();
        return true;
    }

    /// <summary>这块地升级要什么（给界面显示一行）</summary>
    public string 升级需求文本(int i)
    {
        var b = 状态(i);
        if (b == null) return "";
        if (b.已满阶) return "已是最高品阶（" + 灵田品阶说明.中文名(b.品阶) + "）";
        return "升到 " + 灵田品阶说明.中文名((灵田品阶)((int)b.品阶 + 1))
             + "：需境界 " + 灵田地块状态.需要境界等级(b.品阶) + " 级 + "
             + 取物品名(升阶令id) + " ×" + 灵田地块状态.升阶令张数(b.品阶);
    }

    // ============================================================ 场景视图

    GameObject 地块根;

    /// <summary>把地块视图**在本场景重建一遍**（进场景、摆完、挪完、读档后都调它）</summary>
    public void 重建视图()
    {
        if (地块根 != null) Object.Destroy(地块根);
        地块根 = null;

        if (!当前场景可摆放) return;          // 宗门/村庄/塔里**一块地都不建**

        地块根 = new GameObject("灵田地块根");
        var 父 = 地块根.transform;

        for (int i = 0; i < 地块.Count; i++)
        {
            var b = 地块[i];
            if (b == null) continue;

            var go = new GameObject("田" + (i + 1));
            go.transform.SetParent(父, false);
            go.transform.position = b.位置;
            go.transform.rotation = Quaternion.Euler(0f, b.朝向角度, 0f);

            var 块 = go.AddComponent<灵田地块>();
            块.编号 = i;
        }
    }

    // ============================================================ 背包小工具

    static ItemDefinition 取物品(string 物品id)
    {
        var 库 = QuestDatabase.取();
        return 库 != null ? 库.找物品(物品id) : null;
    }

    /// <summary>物品的显示名（界面上要写"缺XX"时用；找不到就退回 id）</summary>
    public static string 取物品名(string 物品id)
    {
        var d = 取物品(物品id);
        return d != null ? d.物品名 : 物品id;
    }

    static UIPanelData 取面板()
    {
        var 板 = FindObjectOfType<UIPanelData>();
        if (板 != null) 板.EnsureLists();
        return 板;
    }

    public static int 数物品(string 物品id)
    {
        var 定义 = 取物品(物品id);
        var 板 = 取面板();
        if (定义 == null || 板 == null) return 0;
        return 板.物品数量(定义);
    }

    /// <summary>往背包里放一件（**摆放取消时把开拓令还回去**用的）</summary>
    public static void 给道具(string 物品id, int 数量 = 1) => 进背包(物品id, 数量);

    /// <summary>从背包扣一件，成功返回 true</summary>
    public static bool 扣道具(string 物品id, int 数量 = 1)
    {
        var 定义 = 取物品(物品id);
        var 板 = 取面板();
        if (定义 == null || 板 == null) return false;
        if (!板.移除物品(定义, 数量)) return false;
        板.RaiseChanged();
        return true;
    }

    static void 扣物品(string 物品id, int 数量)
    {
        var 定义 = 取物品(物品id);
        var 板 = 取面板();
        if (定义 == null || 板 == null) return;
        if (板.移除物品(定义, 数量)) 板.RaiseChanged();
    }

    static void 进背包(string 物品id, int 数量)
    {
        if (数量 <= 0 || string.IsNullOrEmpty(物品id)) return;

        var 定义 = 取物品(物品id);
        if (定义 == null)
        {
            Debug.LogWarning("[灵田] 物品库里没有「" + 物品id + "」——产物不会进背包。"
                             + "先跑菜单「修仙/从配置表生成资产」");
            return;
        }

        var 面板 = 取面板();
        if (面板 == null) return;
        面板.给物品(定义, 数量);
        面板.RaiseChanged();
    }

    // ============================================================ 存档
    //
    // ⚠️ `灵田地块状态` **本身就是存档类型**（见 `SaveData.灵田地块`），
    //    所以这里不用再做字符串打包 —— JsonUtility 直接吃 `List<灵田地块状态>`
    //    （它的字段全是 string / float / int / Vector3，都在 JsonUtility 支持范围内）。

    /// <summary>导出所有地块（**引用拷贝**，写进存档对象用）</summary>
    public List<灵田地块状态> 导出()
    {
        var 出 = new List<灵田地块状态>();
        foreach (var b in 地块) if (b != null) 出.Add(b);
        return 出;
    }

    /// <summary>用存档覆盖地块表</summary>
    public void 导入(List<灵田地块状态> 存档)
    {
        地块 = new List<灵田地块状态>();
        if (存档 != null)
            foreach (var b in 存档)
                if (b != null)
                {
                    // 作物定义没了（改过灵植库）→ 当作待种植，免得留一个永远长不出来的僵尸
                    if (!string.IsNullOrEmpty(b.作物id) && 灵植库.取(b.作物id) == null) b.清空作物();
                    b.朝向档 = Mathf.Clamp(b.朝向档, 0, 灵田规格.朝向档数 - 1);
                    地块.Add(b);
                }

        上次总天数 = -1f;          // 读档后重新对齐，别把"读档"当成"过了很多天"
        重建视图();
        通知变化();
    }
}
