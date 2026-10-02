using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **运行时对话库**：把 `Assets/Data/Generated/DialogueDefinition/` 里生成出来的对话资产收集成一份，
/// 放到 <c>Assets/resources/对话/对话库.asset</c> —— 因为生成的资产不在 Resources 下，
/// 运行时只能靠这份库来查（这是项目里既有的做法：运行时资源一律走 Resources）。
///
/// 菜单：**修仙/对话系统/收集对话资产**（`修仙/从配置表生成资产` 跑完会自动跟着跑一次）。
///
/// ## 「树」是什么（2026-10-02 加）
///
/// `分段` 是**每个 NPC 一套全局序列**：一个 NPC 身上可以挂**多棵独立的树**，
/// 靠 id 前缀区分 —— 大师兄身上就有 `dlg_act3_dashi`（招募 1~22 段）、
/// `dlg_act4_dongfu_guide`（传送点）、`dlg_act5_lingtian`（灵田 1~4 段）等好几棵。
///
/// 不加"树"过滤就会串台：实测阶段18 的灵田树演完第 1 段后，
/// `当前分段 + 1 = 2` 取到的是 **`dlg_act3_dashi_2`（招募台词）** ——
/// 因为灵田树自己的第 2 段和招募树的第 2 段**段号撞了**，而排序时 id 靠前的赢。
/// 所以：**续段只在当前这棵树里找**（见 <see cref="取树"/>）。
/// </summary>
[CreateAssetMenu(fileName = "对话库", menuName = "修仙/对话库", order = 7)]
public class DialogueDatabase : ScriptableObject
{
    [Tooltip("由「修仙/对话系统/收集对话资产」自动填充，不用手拖")]
    public List<DialogueDefinition> 全部 = new List<DialogueDefinition>();

    static DialogueDatabase 缓存;

    /// <summary>取对话库（Resources/对话/对话库）。没有就返回 null，调用方要判空</summary>
    public static DialogueDatabase 取()
    {
        if (缓存 == null) 缓存 = Resources.Load<DialogueDatabase>("对话/对话库");
        return 缓存;
    }

    /// <summary>换了库（重新收集）之后调一下，免得还拿着旧的</summary>
    public static void 清缓存() => 缓存 = null;

    /// <summary>
    /// 这条对话属于哪一棵树：取 id **最后一个 `_` 之前**的部分。
    /// `dlg_act5_lingtian_3` → `dlg_act5_lingtian`；`dlg_act4_dongfu_guide` → `dlg_act4_dongfu`。
    ///
    /// 【为什么必须靠前缀】一个 NPC 上多棵树共用同一套段号，只有 id 前缀能区分它们。
    /// 对话表里同一棵树的 id 都是 `前缀_段号` 这个写法（`dlg_act3_dashi_1..22`），所以前缀是可靠的。
    /// </summary>
    public static string 取树(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        int i = id.LastIndexOf('_');
        return i > 0 ? id.Substring(0, i) : id;
    }

    /// <summary>
    /// 某个 NPC 的某一段里，**现在满足条件的**所有候选，按「优先」从大到小排。
    /// npcId 为空的通用段对任何 NPC 都算候选（这样可以写"所有村民都会说的话"）。
    /// 给了 <paramref name="树"/> 就只在那一棵树里找（续段用）。
    /// </summary>
    public List<DialogueDefinition> 候选(string npcId, int 分段) => 候选(npcId, 分段, null);

    public List<DialogueDefinition> 候选(string npcId, int 分段, string 树)
    {
        var 结果 = new List<DialogueDefinition>();
        for (int i = 0; i < 全部.Count; i++)
        {
            var d = 全部[i];
            if (d == null || d.分段 != 分段) continue;
            if (!string.IsNullOrEmpty(树) && 取树(d.id) != 树) continue;   // ★ 锁在树里
            if (!string.IsNullOrEmpty(d.npcId) && d.npcId != npcId) continue;
            if (!对话条件.满足(d)) continue;
            结果.Add(d);
        }
        结果.Sort((a, b) =>
        {
            int c = b.优先.CompareTo(a.优先);
            if (c != 0) return c;
            // ★ 同等优先时：**NPC 专属压过通用**（通用段是"谁都能说"的兜底，
            //   否则村民自己写的第 2 段会被 对话表里 npcId 空的第 2 段顶掉 —— 实测踩过）
            bool a通用 = string.IsNullOrEmpty(a.npcId);
            bool b通用 = string.IsNullOrEmpty(b.npcId);
            if (a通用 != b通用) return a通用 ? 1 : -1;
            // ★ 再比「具体程度」：**带条件的**（任务专属回答）压过无条件的。
            //   实测踩过：任务回答和默认回答都写 优先=10，平手后按 id 排，"dlg_chengnan_01" 比
            //   "dlg_chengnan_01b" 短就赢了 —— 结果任务回答永远出不来。
            bool a有 = !string.IsNullOrWhiteSpace(a.需要标记);
            bool b有 = !string.IsNullOrWhiteSpace(b.需要标记);
            if (a有 != b有) return a有 ? -1 : 1;
            return string.CompareOrdinal(a.id, b.id);   // 保证顺序稳定
        });
        return 结果;
    }

    /// <summary>取这一段该显示哪条（顺序：当前阶段点名的 → 树内优先值最大的；都没有就 null）</summary>
    public DialogueDefinition 取段(string npcId, int 分段) => 取段(npcId, 分段, null);

    public DialogueDefinition 取段(string npcId, int 分段, string 树)
    {
        // ★ **当前主线阶段"点名"的那条对话，压过按 `优先` 抢**（2026-10-02 修，用户实测报的坑）：
        //
        //   现象：`q_main_004` 阶段16「引导使用传送点」要求 `dlg_act4_dongfu_guide`（段1、优先 34），
        //   但同一 (大师兄, 段1) 里还有阶段18 的 `dlg_act5_lingtian_1`（优先 41）—— 阶段16 的
        //   `接取加标记=q_主线_到洞府` 一打上，两条**同时满足条件**，于是按 F 出来的是**灵田台词**，
        //   阶段16 永远完不成 ⇒ 玩家反复按 F 看到同一段（截图实测确认）。
        //
        //   口径：只在"阶段点名的那条对话的 (npcId, 分段) **正好等于**这次要取的段"时才接管；
        //   若这次带了树过滤（续段），还要它属于同一棵树。
        var 点名 = 当前阶段点名的对话(npcId, 分段);
        if (点名 != null && (string.IsNullOrEmpty(树) || 取树(点名.id) == 树)) return 点名;

        var c = 候选(npcId, 分段, 树);
        return c.Count > 0 ? c[0] : null;
    }

    DialogueDefinition 找到(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < 全部.Count; i++)
            if (全部[i] != null && 全部[i].id == id) return 全部[i];
        return null;
    }

    /// <summary>
    /// **这一阶段点名的那段对话，此刻能不能出来** —— 条件（标记 / 场景门禁）都算上。
    ///
    /// 两个调用方（都是 2026-10-02 那一类"跨场景剧情泄漏"的收尾）：
    ///   · <c>任务引导.解析目标</c>：**不该**把「！」顶在一个现在说不了话的人身上；
    ///   · <c>任务管理器.确保对话NPC在场</c>：**不该**把那个 NPC 凭空补到玩家跟前。
    ///
    /// 例：`q_main_004` 阶段17 要跟大师兄说 `dlg_act5_dongfu_1`，而那段被
    /// `场景_3C_Testbed` 锁在洞府里 —— 玩家还在宗门传送点时，引导就该去指"洞府传送点"
    /// （阶段17 的 `引导坐标`），而不是旁边的大师兄。
    ///
    /// ⚠️ **库里查不到这个 id 时返回 true**（= 保持老行为）：那是"表写错了/没生成"，
    ///    不该顺带把引导和补人一起关掉 —— 那样反而更难查。
    /// </summary>
    public bool 阶段对话能出(QuestDefinition 阶段)
    {
        if (阶段 == null || string.IsNullOrWhiteSpace(阶段.对话id)) return true;
        var d = 找到(阶段.对话id);
        if (d == null) return true;
        return 对话条件.满足(d);
    }

    /// <summary>
    /// 当前主线阶段点名的那条对话 —— 分两种情况（这是 2026-10-02 把"这一类"一次修掉的第二条）：
    ///
    /// ① **点名就是这一段** ⇒ 直接用它（阶段16 点名 `dlg_act4_dongfu_guide`，段1）。
    /// ② **点名的是同一棵树里更靠后的段**，而这次要的是这棵树的**入口段** ⇒ 用这棵树的入口段开场。
    ///    例：阶段13 点名 `dlg_act3_dashi_21`（21 段），而入口是 `dlg_act3_dashi_ready`（段1），
    ///    它的回答「仙人，我准备好了」**跳转到 21** —— 玩家这样才能走到那一段。
    ///    不加这条时，按 F 会被同段优先更高的 `dlg_act4_dashi_return` 抢走，阶段13 永远完不成
    ///    （用户实测：反复按 F 都是同一段）。
    /// </summary>
    DialogueDefinition 当前阶段点名的对话(string npcId, int 分段)
    {
        var 任务 = 任务管理器.实例;
        if (任务 == null) return null;

        DialogueDefinition 点名 = null;
        string 最好任务 = null;
        var 在跑 = 任务.进行中的阶段();
        for (int i = 0; i < 在跑.Count; i++)
        {
            var q = 在跑[i];
            if (q == null || q.类型 != 任务类型.主线) continue;
            if (q.条件 != 任务条件.对话 || string.IsNullOrWhiteSpace(q.对话id)) continue;
            if (最好任务 != null && string.CompareOrdinal(q.任务id, 最好任务) <= 0) continue;

            var d = 找到(q.对话id);
            if (d == null) continue;
            if (!string.IsNullOrEmpty(d.npcId) && d.npcId != npcId) continue;

            点名 = d;
            最好任务 = q.任务id;
        }
        if (点名 == null) return null;

        // ★★ 「点名」只是**优先**，不能绕过条件（2026-10-02 第二轮，用户实测报的）。
        //
        //   现象：把洞府那几段用 `场景_3C_Testbed` 门禁锁上之后，**玩家在宗门传送点还是能
        //   听完整套洞府台词** —— 因为阶段17「点名」了 `dlg_act5_dongfu_1`，
        //   而下面第 ① 条是**直接 return 点名**，连 `对话条件.满足` 都没看。
        //   （引导那一路是按 `满足` 判的，所以当时已经改对了、"！"指传送点，
        //     可对话本身照样出得来 —— 实测日志：引导=洞府传送点，取段=dlg_act5_dongfu_1。）
        //
        //   口径：点名的那段**自己条件不满足**时，就当作"没有点名"，
        //   回到普通的「同段里挑优先最高的」逻辑（那里本来就会过滤条件）。
        if (!对话条件.满足(点名)) return null;

        // ① 正好点名这一段
        if (点名.分段 == 分段) return 点名;

        // ② 点名更靠后的段：这次要的是这棵树的入口段 → 用入口段开场（顺着回答/跳转就能走到点名那段）
        string 树 = 取树(点名.id);
        if (分段 < 点名.分段 && 分段 == 最小分段(npcId, 树))
        {
            var 入口 = 候选(npcId, 分段, 树);
            if (入口.Count > 0) return 入口[0];
        }
        return null;
    }

    /// <summary>这个 NPC 最小的分段号（对话入口），没有就 0</summary>
    public int 最小分段(string npcId)
    {
        int m = 0;
        for (int i = 0; i < 全部.Count; i++)
        {
            var d = 全部[i];
            if (d == null) continue;
            if (!string.IsNullOrEmpty(d.npcId) && d.npcId != npcId) continue;
            if (m == 0 || d.分段 < m) m = d.分段;
        }
        return m;
    }

    /// <summary>这个 NPC **某一棵树**里最小的分段号（= 那棵树的入口段），没有就 0</summary>
    public int 最小分段(string npcId, string 树)
    {
        int m = 0;
        for (int i = 0; i < 全部.Count; i++)
        {
            var d = 全部[i];
            if (d == null) continue;
            if (!string.IsNullOrEmpty(d.npcId) && d.npcId != npcId) continue;
            if (!string.IsNullOrEmpty(树) && 取树(d.id) != 树) continue;
            if (m == 0 || d.分段 < m) m = d.分段;
        }
        return m;
    }

    /// <summary>这个 NPC 最大的分段号（用来判断"还有没有下一段"）。⚠️ 是**所有树**里最大的，别单独拿它判续段，见 <see cref="取树"/></summary>
    public int 最大分段(string npcId)
    {
        int m = 0;
        for (int i = 0; i < 全部.Count; i++)
        {
            var d = 全部[i];
            if (d == null) continue;
            if (!string.IsNullOrEmpty(d.npcId) && d.npcId != npcId) continue;
            if (d.分段 > m) m = d.分段;
        }
        return m;
    }

    /// <summary>这个 NPC **某一棵树**里最大的分段号</summary>
    public int 最大分段(string npcId, string 树)
    {
        int m = 0;
        for (int i = 0; i < 全部.Count; i++)
        {
            var d = 全部[i];
            if (d == null) continue;
            if (!string.IsNullOrEmpty(d.npcId) && d.npcId != npcId) continue;
            if (!string.IsNullOrEmpty(树) && 取树(d.id) != 树) continue;
            if (d.分段 > m) m = d.分段;
        }
        return m;
    }

    /// <summary>某个 NPC 有没有对话（没写对话的 NPC 就不该弹框）</summary>
    public bool 有对话(string npcId) => 最小分段(npcId) > 0;

    /// <summary>
    /// 某一段对话是谁说的（找不到返回空串）。
    ///
    /// 任务系统用它回答"这一阶段到底要跟谁说话" —— `条件=对话` 的阶段常常**只填了
    /// `对话id`**（说话的人写在对话表的 `npcId` 列里），光看任务表那一行是不知道找谁的。
    /// 目前两处用：`任务引导`（头顶感叹号指谁）与 `任务管理器`（那个人不在场就补出来）。
    /// </summary>
    public string 取NpcId(string 对话id)
    {
        if (string.IsNullOrWhiteSpace(对话id)) return "";
        for (int i = 0; i < 全部.Count; i++)
        {
            var d = 全部[i];
            if (d != null && d.id == 对话id) return d.npcId;
        }
        return "";
    }
}
