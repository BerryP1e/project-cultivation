using System.Collections.Generic;
using UnityEngine;

/// <summary>炼丹结果。给 UI 显示"为什么失败 / 炼出几品"。</summary>
public struct 炼丹结果
{
    /// <summary>受理了吗（材料够不够、灵气够不够）。false = 连试都没试，不扣东西</summary>
    public bool 受理;
    /// <summary>受理的话，炼成了吗</summary>
    public bool 成功;
    /// <summary>这次的成功率（显示给玩家，概率透明）</summary>
    public float 成功率;
    /// <summary>这颗丹的品（1~9，来自丹方本身；只用于显示）</summary>
    public int 品;
    /// <summary>炼出的数量（失败 = 0）</summary>
    public int 数量;
    /// <summary>给玩家看的话</summary>
    public string 文本;

    public static 炼丹结果 拒绝(string 原因)
        => new 炼丹结果 { 受理 = false, 成功 = false, 文本 = 原因 };
}

/// <summary>
/// **炼丹炉** —— 挂机产出（灵田）→ 主动消耗（炼丹）→ 修为提升 的中间那一环。
///
/// ## 规则（用户 2026-10-01 需求原文）
///
/// · 炼丹需要**对应材料**以及**消耗灵气**
/// · 每种丹方有**自己的成功率**；**丹越品（越珍贵）越难炼**
/// · 需要**一味主材 + 多味辅材**
/// · 做几个被动技能 `xx炼丹术` 提升成功率
/// · 做几个被动技能减少炼丹的**灵气消耗**
///
/// ## 「品」是什么（用户 2026-10-01 澄清，别搞错）
///
/// 品是**丹药自己的等级 / 珍贵程度，和境界对应**（炼气破境丹 1 品 … 登仙破境丹 9 品）。
/// **不是"每炉选几品"** —— 所以本类的方法**没有"品"这个参数**：
/// 成功率与耗气直接取丹方上的两个数。
///
/// > 【曾经的错误做法】原来有一组 `最高可炼品` + `品阶衰减` + `品阶加耗`，
/// > 让玩家同一炉里选 1~9 品、选高品就降成功率加耗气 —— 可**产出的物品 id 完全一样**，
/// > 品根本没被记下来，"选品"对结果毫无意义。现在按"品 = 丹本身的属性"重做。
///
/// ## 和别的系统怎么接
///
/// · 材料：走 `QuestDatabase.物品库` + `UIPanelData`（和灵田产物同一套）
/// · 灵气：`PlayerVitals.当前灵气`（需求量级小，不动修为系统）
/// · 产出：同样走 `UIPanelData.给物品`，和任务奖励、灵田产物**完全等价**
/// </summary>
[DisallowMultipleComponent]
public class 炼丹炉 : MonoBehaviour
{
    // ============================================================ 单例

    static 炼丹炉 实例;
    public static 炼丹炉 取()
    {
        if (实例 != null) return 实例;
        实例 = FindObjectOfType<炼丹炉>();
        if (实例 != null) return 实例;
        var go = new GameObject("炼丹炉");
        DontDestroyOnLoad(go);
        实例 = go.AddComponent<炼丹炉>();
        return 实例;
    }

    [Header("被动加成（后续由 xx炼丹术 / 减耗被动 灌进来）")]
    [Tooltip("炼丹成功率**加法**加成（0.1 = 各品成功率 +10%）")]
    [Range(0f, 0.9f)] public float 成功率加成 = 0f;

    [Tooltip("炼丹**灵气消耗减免**（0.2 = 少花 20%）")]
    [Range(0f, 0.9f)] public float 耗气减免 = 0f;

    /// <summary>炼成 / 失败都会广播（参数 = 结果）—— UI 和特效听它</summary>
    public event System.Action<炼丹结果> 炼制完成;

    // ============================================================ 查丹方

    /// <summary>所有可炼的丹方</summary>
    public List<灵丹定义> 全部丹方()
    {
        var 出 = new List<灵丹定义>();
        foreach (var d in 灵丹库.全部) if (d != null && d.是丹方) 出.Add(d);
        return 出;
    }

    public 灵丹定义 取丹方(string id) => 灵丹库.取(id);

    // ============================================================ 「学会了吗」
    //
    // 用户 2026-10-02 的口径：
    //   「获得的丹方（**只要炼出来过就能学会**，不显示没学过的丹方，
    //     只要材料足够就会亮起，不然就是暗下去的）」
    //
    // ⇒ 两种来源都算"学会"：① 任务里大师兄给的（阶段24 的 `完成加标记` 打 `丹方_<id>`）；
    //   ② 自己炼成功过一次（`炼制()` 成功时自动补上这个标记）。
    // 标记走 `对话标记`（存档里本来就有这一份，不用动存档版本）。

    /// <summary>「已学会这个丹方」的标记名</summary>
    public static string 丹方标记(string 丹方id)
        => string.IsNullOrEmpty(丹方id) ? "" : "丹方_" + 丹方id;

    /// <summary>「炼成过某个丹方」的标记前缀（<see cref="丹方标记"/> 就是这个前缀 + 丹方id）</summary>
    public const string 丹方标记前缀 = "丹方_";

    /// <summary>
    /// **「真的炼成过一次」的标记** —— 只有 <see cref="炼制"/> **成功**时才打。
    ///
    /// 【为什么要和 <see cref="丹方标记"/> 分开】踩过的坑（2026-10-02，用户实测报的
    /// "大师兄引我往丹房去，我到了丹房外，任务直接跳过了炼丹那一步"）：
    /// `丹方_` 这个标记**有两处会写** —— ① `炼制()` 成功时；② 任务表**阶段24 的 `完成加标记`**
    /// （大师兄"把这几张一阶丹方留给你"⇒ 一次打上 4 个）。于是拿 `丹方_` 当"玩家炼过丹"的判据时，
    /// 阶段24 一完成，**4 个"炼过丹"就凭空成立了** ⇒ 阶段27 一进去就完成 → 直接跳到 28。
    ///
    /// **通用教训**：拿某个标记当"玩法验真"的判据之前，**先查这个标记有没有别人也在写**。
    /// （同类：`丹方_` 表示"学会"，`炼成_` 才表示"练成过"。）
    /// </summary>
    public const string 炼成标记前缀 = "炼成_";

    /// <summary>「真的炼成过一次」的标记（只有 <see cref="炼制"/> 成功时打）</summary>
    public static string 炼成标记(string 丹方id)
        => string.IsNullOrEmpty(丹方id) ? "" : 炼成标记前缀 + 丹方id;

    /// <summary>这个丹方学会了没有（任务给过 / 自己炼出来过）</summary>
    public static bool 已学会(string 丹方id) => 对话标记.具备(丹方标记(丹方id));

    /// <summary>
    /// **已经炼成过几种丹药** —— 数 <see cref="炼成标记前缀"/>（`炼成_`）的个数。
    ///
    /// 【为什么数标记，不另开一个计数器】`炼制()` 只在**成功**时打这个标记（失败不打），
    /// 而 `对话标记` 是**进存档**的（`SaveData.对话标记`）⇒ 天然满足
    /// "炼成过、而且读档之后还算数"。另开一个 `static int` 会在重进 Play / 读档时归零，
    /// 于是"炼完丹主线不推进"这种坑会在读档后又冒出来一次。
    ///
    /// ⚠️ **不能数 `丹方_`** —— 那个标记任务表阶段24 也会打（见 <see cref="炼成标记前缀"/> 的说明）。
    ///
    /// 用途：`任务条件.炼过丹`（主线阶段27「走近丹房」= 炼成任意一种丹药）。
    /// </summary>
    public static int 炼成过的种类数
    {
        get
        {
            var 全部 = 对话标记.全部标记();
            int n = 0;
            for (int i = 0; i < 全部.Length; i++)
                if (!string.IsNullOrEmpty(全部[i])
                    && 全部[i].StartsWith(炼成标记前缀, System.StringComparison.Ordinal)) n++;
            return n;
        }
    }

    /// <summary>炼成过任意一种丹药没有（= <see cref="炼成过的种类数"/> &gt; 0）</summary>
    public static bool 炼成过任意 => 炼成过的种类数 > 0;

    /// <summary>**已经学会**的丹方 —— `炼丹界面` 只列这些，没学会的不显示</summary>
    public List<灵丹定义> 已学会的丹方()
    {
        var 出 = new List<灵丹定义>();
        foreach (var d in 全部丹方()) if (d != null && 已学会(d.id)) 出.Add(d);
        return 出;
    }

    /// <summary>实际成功率（含被动加成）</summary>
    public float 实际成功率(灵丹定义 丹方)
        => 丹方 == null ? 0f : Mathf.Clamp01(丹方.成功率 * (1f + Mathf.Clamp(成功率加成, 0f, 0.9f)));

    /// <summary>实际灵气消耗（含被动减免）</summary>
    public int 实际耗气(灵丹定义 丹方)
        => 丹方 == null ? 0 : Mathf.Max(1, Mathf.RoundToInt(丹方.灵气消耗 * (1f - Mathf.Clamp(耗气减免, 0f, 0.9f))));

    // ============================================================ 材料

    /// <summary>解析 `物品id:数量|物品id:数量`</summary>
    public static List<KeyValuePair<string, int>> 解析材料(string 串)
    {
        var 出 = new List<KeyValuePair<string, int>>();
        if (string.IsNullOrWhiteSpace(串)) return 出;
        foreach (var 段 in 串.Split('|'))
        {
            if (string.IsNullOrWhiteSpace(段)) continue;
            var kv = 段.Split(':');
            string id = kv[0].Trim();
            int n = 1;
            if (kv.Length >= 2) int.TryParse(kv[1].Trim(), out n);
            if (n <= 0) n = 1;
            if (!string.IsNullOrEmpty(id)) 出.Add(new KeyValuePair<string, int>(id, n));
        }
        return 出;
    }

    /// <summary>列出这个丹方要的全部材料（主材在前）</summary>
    public List<KeyValuePair<string, int>> 全部材料(灵丹定义 丹方)
    {
        var 出 = new List<KeyValuePair<string, int>>();
        if (丹方 == null) return 出;
        if (!string.IsNullOrEmpty(丹方.主材id))
            出.Add(new KeyValuePair<string, int>(丹方.主材id, Mathf.Max(1, 丹方.主材数量)));
        出.AddRange(解析材料(丹方.辅材));
        return 出;
    }

    /// <summary>材料够不够。不够时 <paramref name="缺什么"/> 给出人话</summary>
    public bool 材料够(灵丹定义 丹方, out string 缺什么)
    {
        缺什么 = "";
        var 板 = 取面板();
        if (板 == null) { 缺什么 = "找不到背包（UIPanelData）"; return false; }

        foreach (var kv in 全部材料(丹方))
        {
            var 定义 = 取物品(kv.Key);
            if (定义 == null) { 缺什么 = "物品库里没有「" + kv.Key + "」"; return false; }
            int 有 = 板.物品数量(定义);
            if (有 < kv.Value)
            {
                缺什么 = $"「{定义.物品名}」不够：有 {有}，需要 {kv.Value}";
                return false;
            }
        }
        return true;
    }

    // ============================================================ 炼制

    /// <summary>能不能炼（不实际消耗）。给 UI 判按钮可用性</summary>
    public bool 能炼(灵丹定义 丹方, out string 原因)
    {
        原因 = "";
        if (丹方 == null || !丹方.是丹方) { 原因 = "这不是一个丹方"; return false; }

        string 缺;
        if (!材料够(丹方, out 缺)) { 原因 = 缺; return false; }

        var 命 = 取玩家();
        int 需 = 实际耗气(丹方);
        if (命 != null && 命.当前灵气 < 需) { 原因 = $"灵气不足：有 {命.当前灵气:F0}，需要 {需}"; return false; }
        return true;
    }

    /// <summary>
    /// **炼一炉。**
    ///
    /// 受理后**材料与灵气都会扣掉**（不论成败）—— 这是"失败也有代价"的常规做法，
    /// 也符合需求里"炼丹是主动玩法、消耗资源"的定位。
    ///
    /// ⚠️ **不再有"选品"参数**：品是**丹药自己的属性**（1~9，对应境界），不是每炉选的档位。
    /// 见 <see cref="灵丹定义.品"/> 的说明。
    /// </summary>
    public 炼丹结果 炼制(string 丹方id)
    {
        var 丹方 = 取丹方(丹方id);
        string 原因;
        if (!能炼(丹方, out 原因)) return 炼丹结果.拒绝(原因);

        // ---- 扣材料 ----
        foreach (var kv in 全部材料(丹方)) 扣物品(kv.Key, kv.Value);

        // ---- 扣灵气 ----
        int 耗 = 实际耗气(丹方);
        var 命 = 取玩家();
        if (命 != null) 命.当前灵气 = Mathf.Max(0f, 命.当前灵气 - 耗);

        // ---- 判定 ----
        float 概率 = 实际成功率(丹方);
        bool 成功 = UnityEngine.Random.value < 概率;
        int 品 = 丹方.品;

        var 结果 = new 炼丹结果
        {
            受理 = true, 成功 = 成功, 成功率 = 概率, 品 = 品,
            数量 = 0,
        };

        if (成功)
        {
            int 量 = 丹方.掷产量();
            var 产出定义 = 取物品(丹方.id);
            if (产出定义 != null)
            {
                var 板 = 取面板();
                板.给物品(产出定义, 量);
                板.RaiseChanged();
                结果.数量 = 量;
                结果.文本 = $"炼成「{丹方.名}」{品} 品 ×{量}（成功率 {概率:P0}）";
            }
            else
            {
                结果.成功 = false;
                结果.文本 = "炼成了，但物品库里没有「" + 丹方.id + "」——产出丢失";
            }

            // ★ 炼出来过 = 学会这个丹方（用户口径）⇒ 补上标记，`炼丹界面` 靠它决定列不列出来
            对话标记.添加(丹方标记(丹方.id));
            // ★ 另外单独记一条"真的炼成过一次"：`丹方_` 那个标记**任务表阶段24 也会打**
            //   （一次给 4 张丹方），拿它当"玩家炼过丹"的判据会被任务奖励污染，见 炼成标记前缀。
            对话标记.添加(炼成标记(丹方.id));
        }
        else
        {
            结果.文本 = $"炼丹失败（成功率 {概率:P0}），材料与灵气已耗去";
        }

        Debug.Log($"[炼丹] {丹方.名} {品}品 耗气{耗} 成功率{概率:P0} → {(结果.成功 ? "成功 ×" + 结果.数量 : "失败")}");
        炼制完成?.Invoke(结果);
        return 结果;
    }

    // ============================================================ 背包接线

    static ItemDefinition 取物品(string id)
    {
        var 库 = QuestDatabase.取();
        return 库 != null ? 库.找物品(id) : null;
    }

    static UIPanelData 取面板()
    {
        var 板 = FindObjectOfType<UIPanelData>();
        if (板 != null) 板.EnsureLists();
        return 板;
    }

    static PlayerVitals 取玩家() => FindObjectOfType<PlayerVitals>();

    static void 扣物品(string id, int 数量)
    {
        var 定义 = 取物品(id);
        var 板 = 取面板();
        if (定义 == null || 板 == null) return;
        if (板.移除物品(定义, 数量)) 板.RaiseChanged();
    }

    // ---- ASCII 别名 ----
    public 炼丹结果 Craft(string recipeId) => 炼制(recipeId);
    public List<灵丹定义> AllRecipes() => 全部丹方();
}