using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **镇妖塔**：刷怪 → 练级 → 上一层。
///
/// ## 用户的机制要求
///
/// 1. 玩家进入镇妖塔后，塔开始启动**当前层数**的刷怪
/// 2. 打完一层后，**场景里出现一个传送点**（<see cref="通关传送点"/>）：按 F 可选
///    **进入下一层** 或 **出塔**。**不互动它就留在原地** ——
///    <see cref="SpawnZone.重生延迟"/> 秒（默认 10）后新的怪群刷出来，传送点随之消失。
///    也就是说「留在本层」**不再是一个按钮**，而是"什么都不做"
///    （用户 2026-10-02 明确：「清完怪后场景中的传送点会出现，互动后可以选择进入下一层或者出塔；
///     如果不互动传送点的话留在原地 10 秒后新的怪群会刷新」）
/// 3. 所有怪物清理完毕 **10 秒后**会再次刷出这一组怪物（由 <see cref="SpawnZone"/> 自己循环）
/// 4. 玩家死亡时，可以在 **10 秒内**选择**退到塔外**，或**进入上一层**；
///    没有选择 → **自动退出到塔外**（这一条**仍然弹面板** —— 死亡是"必须选一个"的状态，
///    和清完怪那种"不选就是留在原地"不一样）
/// 5. 选择进入上一层 → **满血复活**，塔刷新并进行**等级调整**，继续刷怪
/// 6. 塔的等级**可被记录**（玩家爬到了多少层）；暂定 1000 层，每 10 层对应玩家一个等级
///
/// ## 清完怪为什么用"场景里的传送点"而不是弹窗
///
/// 弹窗是**抢焦点**的：玩家正在跑位 / 打最后一只怪时弹出来，还得先用鼠标点掉。
/// 传送点则是"摆在地上、想走就按 F"—— 而且它天然表达了"这是**离开这一层**的出口"。
/// 实现上塔控只做三件事：**出现 / 消失 / 把两个选项写进那个圈的界面**，
/// 选项的行为走 <see cref="Teleporter.选项接管"/>（「进入下一层」不是一次传送，是塔的逻辑）。
///
/// ## 与塔外死亡流程的分工
///
/// 塔外死亡走 <see cref="PlayerDeathSequence"/> 的原流程（原地消散 → 弹死亡界面 → 点重生回修炼小屋）。
/// **塔里由本组件接管**：<see cref="PlayerDeathSequence.塔接管复活"/> 被置真，
/// 死亡界面不会弹出、点重生也不会把玩家传去修炼小屋 —— 位置与复活都由塔决定。
///
/// ## 挂载
///
/// 挂在镇妖塔场景的**根对象**（或一个空的「塔控」物件）上。
/// <see cref="刷怪区"/> 留空会自动在本场景收集所有 <see cref="SpawnZone"/>。
/// </summary>
[DisallowMultipleComponent]
public class TowerController : MonoBehaviour
{
    /// <summary>
    /// **「从塔里出来过」的标记** —— 在 <see cref="退到塔外"/> 里打。
    /// 任务表用一个 `条件=有标记`（`物品id` 列填这个标记名）的阶段来等它：
    /// 主线「进塔去」就靠它判定 —— **进过塔、并且出来了**才算完成（不是走到塔门就算）。
    /// 标记进存档，所以读档回来也算数。
    /// </summary>
    public const string 出塔标记 = "q_主线_出过塔";

    // ============================================================ 配置

    [Header("层表")]
    [Tooltip("镇妖塔层表（总层数 / 层→等级 / 每层刷哪个组）。留空自动从 Resources 找")]
    public TowerFloorTable 层表;

    [Tooltip("等级补正表。留空自动从 Resources 找")]
    public NpcLevelScale 补正表;

    [Header("刷怪区")]
    [Tooltip("塔里的刷怪区。留空自动收集本场景所有 SpawnZone")]
    public List<SpawnZone> 刷怪区 = new List<SpawnZone>();

    [Header("进出塔")]
    [Tooltip("玩家进塔时的落脚点。留空用场景里的 TeleportTarget_入塔")]
    public Transform 入塔落点;

    [Tooltip("选择「退到塔外」时的去处。**填场景名**（例：Sect）—— 走黑幕过场")]
    public string 塔外场景名 = "Sect";

    [Tooltip("退到塔外时的落点名（黑幕过场的落点）。**必须填**！\n" +
             "默认 `sect2 to tower` = 宗门里那个**进塔的传送圈**本身（落点与传送圈同名是允许的：\n" +
             "传送要按 F，站在圈上不会自己触发）。\n" +
             "留空 = 落到目标场景的**默认出生点** —— 用户 2026-10-02 报的 bug 就是这个。")]
    public string 塔外落点名 = "sect2 to tower";

    [Header("通关传送点（清完怪出现）")]
    [Tooltip("本层清空时**出现**的那个传送点：玩家走上去按 F，选「进入下一层 / 出塔」。\n" +
             "留空则按 通关传送点名 在场景里找（**含未激活的**）。")]
    public Transform 通关传送点;

    [Tooltip("按名字找通关传送点。它在场景里应当**处于未激活**（清完怪才出现）")]
    public string 通关传送点名 = "tower to sect2";

    [Tooltip("「进入下一层」那一项在圈上显示的名字")]
    public string 下一层选项名 = "进入下一层";

    [Tooltip("「出塔」那一项在圈上显示的名字")]
    public string 出塔选项名 = "出塔（回 Sect）";

    [Header("节奏")]
    [Tooltip("玩家死亡后给多少秒做选择（用户定：10 秒）")]
    [Min(0f)] public float 死亡选择时限 = 10f;

    [Tooltip("进度变化时自动存档（槽位 0 = 自动档）")]
    public bool 自动存档 = true;

    [Header("调试")]
    [Tooltip("勾上把塔的状态变化打进 Console")]
    public bool 打印日志 = true;

    // ============================================================ 状态

    /// <summary>当前层数（1 起）</summary>
    public int 当前层 { get; private set; } = 1;

    /// <summary>历史上爬到的最高层（存档要记的就是它）</summary>
    public int 最高层 { get; private set; } = 1;

    /// <summary>塔是否在运行（刷怪中）</summary>
    public bool 塔运行中 { get; private set; }

    /// <summary>正在等玩家做「下一层 / 出塔」的决定（= 通关传送点已经摆出来了）</summary>
    public bool 等待通关选择 { get; private set; }

    /// <summary>
    /// **本层清过一次怪没有**。用户 2026-10-03 报的坑：清完一层没走、下一波怪一刷，
    /// 出口传送点就被 <see cref="处理一波刷出"/> 收掉了 ⇒ **在塔里再没有出塔的办法**（只能等死）。
    /// 现在口径：**本层清过一次，出口就一直留着**（想去下一层 / 想出去，随时都能走）。
    /// </summary>
    bool 本层清过;

    /// <summary>正在等玩家做「退出 / 上一层」的死亡选择</summary>
    public bool 等待死亡选择 { get; private set; }

    /// <summary>本层清空后是不是已经给过传送点了（同一层只摆一次）</summary>
    bool 本层已问过;

    /// <summary>玩家当前所在层刷完一轮，正在等重刷</summary>
    public bool 等待重刷 { get; private set; }

    /// <summary>通关传送点在场景里能不能被玩家用（找到 + 有 Teleporter）</summary>
    bool 通关传送点可用;

    PlayerVitals 玩家气血;
    PlayerDeathSequence 死亡流程;
    TowerUI 界面;

    /// <summary>
    /// 当前层的**整数**怪等级 —— 报给玩家、算击杀经验用。
    /// 同一级内的 10 层返回同一个值（数值递进由
    /// <see cref="当前楼层内倍率"/> 负责）。
    /// </summary>
    public int 当前怪物等级 => 层表 != null ? 层表.取怪物等级(当前层) : 1;

    /// <summary>
    /// 当前层的**层内数值倍率** —— 同一级内逐层变强的那个倍数。
    /// 第 1 层 ×1.00 … 第 10 层 ×<c>楼层内总量</c>（默认 ×2.5）。
    /// </summary>
    public float 当前楼层内倍率 => 层表 != null ? 层表.取楼层内倍率(当前层) : 1f;

    /// <summary>当前层的等级对应的大境界</summary>
    public int 当前大境界 => NpcLevelScale.等级到大境界(当前怪物等级);

    // ============================================================ 事件（给 UI 用）

    /// <summary>层数变了（参数：新层）</summary>
    public event System.Action<int> 层变化;
    /// <summary>刷怪波次清空（参数：层）</summary>
    public event System.Action<int> 一波清空;
    /// <summary>玩家死亡，开始倒数（参数：剩余秒数）</summary>
    public event System.Action<float> 死亡倒数开始;
    /// <summary>玩家做出死亡选择（参数：true = 进上一层，false = 退出塔外）</summary>
    public event System.Action<bool> 死亡选择完成;

    // ============================================================ 生命周期

    void Awake()
    {
        层表 = 层表 != null ? 层表 : TowerFloorTable库.取();
        补正表 = 补正表 != null ? 补正表 : NpcLevelScale库.取();
        找入塔落点();
        找通关传送点();

        if (刷怪区 == null) 刷怪区 = new List<SpawnZone>();
        if (刷怪区.Count == 0) 刷怪区.AddRange(FindObjectsOfType<SpawnZone>());
        刷怪区.RemoveAll(z => z == null);
    }

    void Start()
    {
        取玩家();
        挂事件();
        隐藏通关传送点();          // 开局没清怪，出口不该摆在外面

        if (层表 == null)
            Debug.LogError("[镇妖塔] 找不到 TowerFloorTable —— 跑一次「修仙/从配置表生成资产」"
                           + "（会生成并收进 Resources/Data）", this);
        if (刷怪区.Count == 0)
            Debug.LogWarning("[镇妖塔] 场景里一个 SpawnZone 都没有，不会刷怪", this);
        if (!通关传送点可用)
            Debug.LogWarning("[镇妖塔] 没有可用的「通关传送点」（名字「" + 通关传送点名 + "」）。"
                             + "清完怪不会有出口，只能等 " + 重生延迟兜底().ToString("0.#") + " 秒自动重刷"
                             + " —— 玩家就永远上不了下一层了", this);

        // 进塔即从存档里的层数继续（没存过就是第 1 层）
        当前层 = Mathf.Clamp(TowerProgress.当前层, 1, 层表 != null ? 层表.有效总层数 : 1);
        最高层 = Mathf.Clamp(Mathf.Max(TowerProgress.最高层, 当前层), 1,
                            层表 != null ? 层表.有效总层数 : 1);

        Debug.Log("[镇妖塔] 启动：第 " + 当前层 + " 层，怪等级 " + 当前怪物等级
                  + "（大境界 " + 当前大境界 + "），刷怪区 " + 刷怪区.Count + " 片"
                  + (层表 != null ? "｜" + 层表.有效总层数 + " 层制" : ""), this);

        启动塔();
    }

    void OnDestroy()
    {
        if (玩家气血 != null) 玩家气血.死亡 -= 处理玩家死亡;
        if (死亡流程 != null) 死亡流程.塔接管复活 = false;
        for (int i = 0; i < 刷怪区.Count; i++)
            if (刷怪区[i] != null)
            {
                刷怪区[i].清空 -= 处理一波清空;
                刷怪区[i].刷出了 -= 处理一波刷出;
            }
    }

    void 取玩家()
    {
        if (玩家气血 == null) 玩家气血 = FindObjectOfType<PlayerVitals>();
        if (死亡流程 == null)
        {
            if (玩家气血 != null) 死亡流程 = 玩家气血.GetComponent<PlayerDeathSequence>();
            if (死亡流程 == null) 死亡流程 = FindObjectOfType<PlayerDeathSequence>();
        }
        if (界面 == null) 界面 = GetComponent<TowerUI>();
        if (界面 == null) 界面 = FindObjectOfType<TowerUI>();
    }

    void 挂事件()
    {
        if (玩家气血 != null) 玩家气血.死亡 += 处理玩家死亡;
        else Debug.LogWarning("[镇妖塔] 场景里找不到 PlayerVitals —— 玩家死亡机制不会生效", this);

        // 接管复活：塔里死亡不让 PlayerDeathSequence 弹它的死亡界面 / 传去修炼小屋
        if (死亡流程 != null) 死亡流程.塔接管复活 = true;

        for (int i = 0; i < 刷怪区.Count; i++)
            if (刷怪区[i] != null)
            {
                刷怪区[i].清空 += 处理一波清空;
                刷怪区[i].刷出了 += 处理一波刷出;
            }
    }

    void 找入塔落点()
    {
        if (入塔落点 != null) return;
        var go = GameObject.Find("TeleportTarget_入塔");
        if (go != null) 入塔落点 = go.transform;
    }

    /// <summary>
    /// 找通关传送点（**含未激活的** —— 它平时就该是关着的，清完怪才出现）。
    ///
    /// ⚠️ 不能用 `GameObject.Find`：它**看不见未激活的物体**，
    ///    而这个圈在场景里正是"未激活"状态，`Find` 永远返回 null。
    /// </summary>
    void 找通关传送点()
    {
        通关传送点可用 = false;
        if (通关传送点 == null && !string.IsNullOrEmpty(通关传送点名))
        {
            var 场景 = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var 根 in 场景.GetRootGameObjects())
            {
                foreach (var t in 根.GetComponentsInChildren<Transform>(true))
                    if (t.name == 通关传送点名) { 通关传送点 = t; break; }
                if (通关传送点 != null) break;
            }
        }
        if (通关传送点 == null) return;
        通关传送点可用 = 通关传送点.GetComponent<Teleporter>() != null;
        if (!通关传送点可用)
            Debug.LogWarning("[镇妖塔] 通关传送点「" + 通关传送点.name + "」上没有 Teleporter 组件，"
                             + "玩家不能用它上楼", this);
    }

    // ============================================================ 开关塔

    /// <summary>启动塔：按当前层刷怪</summary>
    public void 启动塔()
    {
        塔运行中 = true;
        等待通关选择 = false;
        等待死亡选择 = false;
        本层清过 = false;
        等待重刷 = false;
        本层已问过 = false;
        隐藏通关传送点();
        刷新刷怪区();
    }

    /// <summary>停塔：停刷 + 清场</summary>
    public void 停止塔(bool 清掉怪 = true)
    {
        塔运行中 = false;
        隐藏通关传送点();
        for (int i = 0; i < 刷怪区.Count; i++)
            if (刷怪区[i] != null) 刷怪区[i].停止刷怪(清掉怪);
    }

    /// <summary>把当前层的配置（组 + 等级 + 数量）灌进每个刷怪区</summary>
    void 刷新刷怪区()
    {
        if (层表 == null) return;
        string 组 = 层表.取刷怪组(当前层);
        // ★ 三个数分工不同：
        //   · 整数等级   → 报给玩家 / **算击杀经验**
        //   · 强度倍率   → **怪的实际数值**（大境界门槛 × 层内递进，从第 1 层算起）
        //   · 数量倍率   → 越深怪越多
        int 整数等级 = 层表.取怪物等级(当前层);
        float 强度 = 层表.取怪强度倍率(当前层);
        float 数量倍 = 层表.取数量倍率(当前层);

        if (string.IsNullOrEmpty(组))
            Debug.LogWarning("[镇妖塔] 第 " + 当前层 + " 层没有配刷怪组（镇妖塔层表里这一层落在哪个分段？）", this);

        for (int i = 0; i < 刷怪区.Count; i++)
        {
            var z = 刷怪区[i];
            if (z == null) continue;
            z.补正表 = 补正表;
            z.设为刷怪组(组, 整数等级, 数量倍, 强度);
            z.立刻重刷();
        }
    }

    // ============================================================ 换层

    /// <summary>进入第 <paramref name="层"/> 层</summary>
    public void 进入层(int 层)
    {
        if (层表 == null) return;

        int 新层 = 层表.夹层(层);
        当前层 = 新层;
        if (当前层 > 最高层) 最高层 = 当前层;

        等待通关选择 = false;
        等待死亡选择 = false;
        本层清过 = false;
        本层已问过 = false;
        等待重刷 = false;

        隐藏通关传送点();

        塔运行中 = true;
        刷新刷怪区();

        if (打印日志)
            Debug.Log("[镇妖塔] 进入第 " + 当前层 + " 层｜怪等级 " + 当前怪物等级
                      + "（大境界 " + 当前大境界 + "）｜组 " + 层表.取刷怪组(当前层), this);

        层变化?.Invoke(当前层);
        记录进度();
    }

    /// <summary>下一层（到底了就留在顶层）</summary>
    public void 下一层()
    {
        if (等待死亡选择 || (玩家气血 != null && 玩家气血.已死亡)) return;
        进入层(当前层 + 1);
    }

    /// <summary>
    /// 留在这一层（**立刻**重刷本层）。
    ///
    /// ⚠️ 现在**没有界面会调它了**：清完怪不互动传送点就是"留在本层"，
    ///    重刷由 <see cref="SpawnZone"/> 自己按 <see cref="SpawnZone.重生延迟"/> 循环。
    ///    留着它是给**调试 / 外部**用的（想在原地立刻再打一波时调）。
    /// </summary>
    public void 留在本层()
    {
        等待通关选择 = false;
        本层已问过 = false;
        隐藏通关传送点();

        塔运行中 = true;
        刷新刷怪区();
        if (打印日志) Debug.Log("[镇妖塔] 留在第 " + 当前层 + " 层，立刻重刷怪", this);
    }

    /// <summary>
    /// 退到塔外（走黑幕过场切场景）。
    ///
    /// ★ **落点必须填**（`塔外落点名`）：留空的话 `黑幕字幕.开始场景过渡` 拿不到落点，
    ///   玩家会被放进目标场景的**默认出生点** —— 用户 2026-10-02 报的就是这个：
    ///   「死亡后传送出塔，但是没有传送到塔外的那个进塔的传送点，而是出现在了宗门场景的出生点」。
    ///   这里配的是 `sect2 to tower`（= 宗门里那个**进塔的传送圈**本身，
    ///   落点与传送圈同名是**允许的**：传送要按 F，落在圈上不会自己再触发）。
    /// </summary>
    public void 退到塔外()
    {
        停止塔();
        记录进度();

        // ★ 留一个"进来过、并且出来了"的标记（2026-10-02）：主线的「进塔去」那一阶段靠它判定
        //   （用户要求「进塔后再出来」才算，不是走到塔门就算）。标记进存档，读档也算数。
        对话标记.添加(出塔标记);

        if (打印日志) Debug.Log("[镇妖塔] 退出塔外 → 场景「" + 塔外场景名 + "」", this);

        if (string.IsNullOrEmpty(塔外场景名))
        {
            Debug.LogWarning("[镇妖塔] 没配「塔外场景名」，只停塔不切场景", this);
            return;
        }

        string 落点 = 塔外落点名;
        // ⚠️ 这里**只能**判"填没填"，**不能**顺手判"目标场景里有没有这个落点物体" ——
        //    落点物体在**目标场景**里，而现在还在塔里，`找场景物体` 一定找不到，
        //    会打出一条永远出现、且是假警报的 Warning（实测踩过）。
        //    真要报"找不到落点"，那是切场景之后的事，由 `黑幕字幕.推进场景过渡` 自己报。
        if (string.IsNullOrEmpty(落点))
            Debug.LogWarning("[镇妖塔] 没配「塔外落点名」—— 玩家会落在「" + 塔外场景名
                             + "」的**默认出生点**，而不是塔门口的那个传送点", this);

        var 显示名 = 塔外场景名;
        // 唯一正路：黑幕字幕.开始场景过渡（它负责黑幕 / 摆位 / 钉进度）
        黑幕字幕.开始场景过渡(塔外场景名, 显示名, Vector3.zero, 落点);
    }

    // ============================================================ 清空一波

    /// <summary>
    /// **本层清空** —— 把通关传送点摆出来（用户 2026-10-02 改的机制）。
    ///
    /// 不再弹「下一层 / 留在本层」的窗口：
    ///   · 想上楼 / 出塔 → 走到传送点按 F（选项由 <see cref="显示通关传送点"/> 写进那个圈）
    ///   · 什么都不做 → 留在原地，<see cref="SpawnZone"/> 按 `重生延迟` 自己重刷
    ///     （重刷时 <see cref="处理一波刷出"/> 会把传送点收掉）
    /// </summary>
    void 处理一波清空(SpawnZone 区)
    {
        if (!塔运行中 || 等待死亡选择) return;

        一波清空?.Invoke(当前层);

        // ⚠️ **塔里现在不发任何奖励**（用户 2026-10-02 明确：「打塔暂时是没有奖励的，之后可能会设置，目前没有」）。
        //    曾经在这里给过宗门贡献（`宗门贡献.加(max(1, 当前层/5+1))`），已按用户口径删掉 ——
        //    塔现在只给「击杀掉修炼次数」（那条在 `PlayerCultivation.记录击杀` 里，是另一套东西，用户没让动）。
        //    以后要加塔奖励（贡献 / 灵石 / 内丹）时，挂在这里最合适：位置就是"一波清空"那一刻。

        if (本层已问过) return;
        本层已问过 = true;
        等待通关选择 = true;
        本层清过 = true;
        等待重刷 = true;
        显示通关传送点();

        if (打印日志)
            Debug.Log("[镇妖塔] 第 " + 当前层 + " 层已清空 —— 通关传送点已出现"
                      + "（按 F 选「" + 下一层选项名 + " / " + 出塔选项名 + "」；"
                      + "不互动就 " + 重生延迟兜底().ToString("0.#") + " 秒后重刷本层）", this);
    }

    /// <summary>
    /// **新一波刷出来了** —— 只在**本层还没清过**的时候收掉通关传送点。
    /// （清过一次之后就一直留着：用户 2026-10-03 报的"留在本层后再也没有出塔的办法"就是这么来的）
    ///
    /// 为什么挂这个事件而不是自己计时：重刷的时机**只有 `SpawnZone` 知道**
    /// （它有 `重生延迟` / `首刷延迟` / 立刻重刷好几条路），塔自己再算一遍必然对不上。
    /// </summary>
    void 处理一波刷出(SpawnZone 区, List<NpcInstance> 怪)
    {
        本层已问过 = false;
        等待通关选择 = false;
        等待重刷 = false;
        if (!本层清过) 隐藏通关传送点();   // ★ 清过一次就一直留着出口（用户 2026-10-03：选留在本层之后再没有出塔的办法）
    }

    /// <summary>
    /// 把通关传送点**摆出来**，并把它那两个选项写成「进入下一层 / 出塔」。
    ///
    /// 为什么要在这里**重写 `选项`** 而不是在场景里配好：
    ///   「进入下一层」不是一次传送，是塔的逻辑（<see cref="下一层"/>），
    ///   场景数据表达不了 —— 所以选项由塔控现写、行为由
    ///   <see cref="Teleporter.选项接管"/> 接走。场景里那份只是"没塔控时的兜底长相"。
    /// </summary>
    void 显示通关传送点()
    {
        找通关传送点();
        if (!通关传送点可用) return;

        var 传送 = 通关传送点.GetComponent<Teleporter>();
        bool 顶层 = 层表 != null && 当前层 >= 层表.有效总层数;

        传送.选项 = new[]
        {
            new Teleporter.传送选项 { 名称 = 下一层选项名, 暂未开放 = 顶层 },
            new Teleporter.传送选项 { 名称 = 出塔选项名, 场景 = 塔外场景名, 落点 = 塔外落点名 },
        };
        传送.选项接管 = 处理通关选项;
        传送.重建面板();                 // 选项变了，旧面板（如果搭过）要作废

        if (!通关传送点.gameObject.activeSelf) 通关传送点.gameObject.SetActive(true);
        传送.刷新交互标记();              // 刚激活才轮到它 Awake 补交互标记，这里再兜一次

        if (打印日志)
            Debug.Log("[镇妖塔] 通关传送点已出现：" + 通关传送点.name + " @"
                      + 通关传送点.position.ToString("F2")
                      + "（" + 下一层选项名 + (顶层 ? " · 已至顶层" : "") + " / " + 出塔选项名 + "）", this);
    }

    /// <summary>收掉通关传送点（还开着面板的话一起收）</summary>
    void 隐藏通关传送点()
    {
        if (通关传送点 == null) return;
        var 传送 = 通关传送点.GetComponent<Teleporter>();
        if (传送 != null) 传送.关面板();     // 面板是**独立根对象**，不跟着圈一起隐藏
        if (通关传送点.gameObject.activeSelf) 通关传送点.gameObject.SetActive(false);
    }

    /// <summary>
    /// 通关传送点上两个选项的行为（0 = 进入下一层，1 = 出塔）。
    /// 返回 true = 我处理了，圈不用自己去传送。
    /// </summary>
    bool 处理通关选项(int 序号)
    {
        if (序号 == 0)
        {
            if (层表 != null && 当前层 >= 层表.有效总层数)
            {
                if (打印日志) Debug.Log("[镇妖塔] 已至顶层（第 " + 当前层 + " 层），不再往上", this);
                return true;
            }
            下一层();
            return true;
        }
        if (序号 == 1)
        {
            退到塔外();
            return true;
        }
        return false;
    }

    float 重生延迟兜底()
    {
        for (int i = 0; i < 刷怪区.Count; i++)
            if (刷怪区[i] != null) return Mathf.Max(0f, 刷怪区[i].重生延迟);
        return 10f;
    }

    // ============================================================ 玩家死亡

    void 处理玩家死亡(PlayerVitals 谁)
    {
        if (!塔运行中) return;                    // 塔外死亡不归塔管
        if (等待死亡选择) return;

        停止塔(清掉怪: false);                     // 先别清场：玩家还在原地消散演出
        等待死亡选择 = true;

        // 顺便把通关传送点收掉（玩家已经死了，那个选择没意义了）
        等待通关选择 = false;
        隐藏通关传送点();

        if (打印日志) Debug.Log("[镇妖塔] 玩家在第 " + 当前层 + " 层倒下 —— "
                               + 死亡选择时限 + (当前层 > 1 ? " 秒内选择：出塔 / 返回上一层" : " 秒内选择：出塔"), this);

        死亡倒数开始?.Invoke(死亡选择时限);
        StartCoroutine(死亡选择倒计时());
    }

    IEnumerator 死亡选择倒计时()
    {
        if (界面 != null) 界面.显示死亡选择(当前层, 死亡选择时限);

        float 剩 = Mathf.Max(0f, 死亡选择时限);
        while (剩 > 0f && 等待死亡选择)
        {
            剩 -= Time.unscaledDeltaTime;
            if (界面 != null) 界面.更新死亡倒数(Mathf.Max(0f, 剩));
            yield return null;
        }

        if (!等待死亡选择) yield break;

        // 没选择 → 自动退出到塔外
        if (打印日志) Debug.Log("[镇妖塔] 玩家超时未选择 —— 自动退出塔外", this);
        执行死亡选择(进上一层: false);
    }

    /// <summary>
    /// 玩家做出死亡选择。
    /// · <c>true</c> = 进入上一层：满血复活 + 塔刷新 + 等级调整
    /// · <c>false</c> = 退出塔外
    /// </summary>
    public void 执行死亡选择(bool 进上一层)
    {
        if (!等待死亡选择) return;
        等待死亡选择 = false;
        // 第一层没有可退的楼层；也拦住外部调用或旧按钮回调。
        进上一层 = 进上一层 && 当前层 > 1;

        if (界面 != null) 界面.隐藏死亡选择();
        死亡选择完成?.Invoke(进上一层);

        if (!进上一层)
        {
            // 【注意】先复活再切场景：
            //   不复活的话 `PlayerVitals.已死亡` 会一直是 true，
            //   新场景里的所有敌对 NPC 第一行决策就是「玩家已死 → 待机」，
            //   表现为「全场 NPC 集体发呆」（`踩坑总库.md` §15.10 专门记过这个坑）
            复活玩家();
            退到塔外();
            return;
        }

        // 进上一层：满血复活 + 刷新 + 等级调整
        复活玩家();
        进入层(当前层 - 1);

        if (打印日志) Debug.Log("[镇妖塔] 死亡后选择进入上一层 → 第 " + 当前层
                               + " 层（怪等级 " + 当前怪物等级 + "），已满血复活", this);
    }

    /// <summary>
    /// **塔里的复活**。
    ///
    /// 【必须走 PlayerDeathSequence.执行重生，不能只调 回满()】
    /// 死亡流程一共改了三样东西：气血归零、**藏模型 + 关操作组件**、`死亡流程中` 置真。
    /// <c>PlayerVitals.回满()</c> 只处理**第一样**。
    /// 只调它的话，玩家会「满血但透明、且走不动」——
    /// 而且 `死亡流程中` 永远卡在 true，**下一次死亡会被 `处理死亡` 开头挡掉**
    /// （实测：塔里死亡选「进入上一层」后主角变透明、第二次死亡完全没反应）。
    ///
    /// `执行重生()` 才是「还回模型 + 开回操作 + 清死亡标记 + 清 死亡流程中」那一步。
    /// 位置不用管：塔里复活是**原地站起来**（`重生位置` 在死亡时已经记成本身位置，
    /// 因为塔场景没有 `cultivation room`，`找重生点()` 找不到就退化成原地）。
    ///
    /// 复活之后再补一次重生保护 —— 顺序很重要：
    /// 先让 `执行重生()` 把 `保护剩余` 设成默认值，再用 <see cref="PlayerDeathSequence.给重生保护"/>
    /// 把它抬到我们想要的时长。反过来的话会被 `执行重生()` 覆盖掉。
    /// </summary>
    void 复活玩家()
    {
        if (玩家气血 == null || 死亡流程 == null) 取玩家();

        if (死亡流程 != null)
        {
            死亡流程.执行重生();                 // 还回模型/操作 + 清死亡标记 + 清 死亡流程中
            死亡流程.给重生保护();                // 再补一次保护（免得刚站起来又被同一波怪秒）
        }
        else
        {
            // 兜底：场景里没有死亡流程组件时，至少把血回满、把死亡标记清掉
            if (玩家气血 != null) 玩家气血.回满();
            Debug.LogWarning("[镇妖塔] 找不到 PlayerDeathSequence —— 只回满了气血，"
                             + "模型/操作组件不会被恢复", this);
        }
    }

    // ============================================================ 存档

    /// <summary>把当前层数记进内存并落盘</summary>
    void 记录进度()
    {
        TowerProgress.记录(当前层, 最高层);
        if (自动存档) TowerProgress.落盘();
    }

    /// <summary>外部改层（调试面板 / 测试用）</summary>
    public void 设置层(int 层)
    {
        if (层表 == null) { 当前层 = Mathf.Max(1, 层); return; }
        进入层(层);
    }

    // ============================================================ 查询（给 UI / 调试）

    /// <summary>本层还剩几只怪</summary>
    public int 本层存活数
    {
        get
        {
            int n = 0;
            for (int i = 0; i < 刷怪区.Count; i++) if (刷怪区[i] != null) n += 刷怪区[i].存活数;
            return n;
        }
    }

    /// <summary>一行状态摘要（日志 / 调试面板用）</summary>
    public string 状态摘要()
    {
        if (层表 == null) return "（无层表）";
        return "第 " + 当前层 + "/" + 层表.有效总层数 + " 层｜怪等级 " + 当前怪物等级
               + "（大境界 " + 当前大境界 + "）｜存活 " + 本层存活数
               + "｜" + (塔运行中 ? "刷怪中" : "已停")
               + (等待通关选择 ? "｜等通关选择" : "")
               + (等待死亡选择 ? "｜等死亡选择" : "");
    }

    // ---- ASCII 别名 ----
    public int CurrentFloor => 当前层;
    public int HighestFloor => 最高层;
    public bool IsRunning => 塔运行中;
    public void EnterFloor(int f) => 进入层(f);
    public void NextFloor() => 下一层();
    public void StayOnFloor() => 留在本层();
    public void ExitTower() => 退到塔外();
}

/// <summary>
/// **塔库的运行时取用口**（`Assets/resources/塔/塔库.asset`，由
/// 「修仙/从配置表生成资产」自动收集）。带缓存。
/// </summary>
public static class TowerDatabase库
{
    static TowerDatabase 缓存;
    static bool 已警告;

    public static TowerDatabase 取()
    {
        if (缓存 != null) return 缓存;

        缓存 = Resources.Load<TowerDatabase>("塔/塔库");
        if (缓存 != null) return 缓存;

        if (!已警告)
        {
            已警告 = true;
            Debug.LogWarning("[塔库] 读不到「塔/塔库」资产 —— 跑一次「修仙/从配置表生成资产」"
                             + "（它会在末尾自动收集塔库与 NPC库）");
        }
        return null;
    }

    public static void 清缓存() { 缓存 = null; 已警告 = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置静态() { 清缓存(); }
}

/// <summary>
/// **镇妖塔层表的运行时取用口**（走塔库，带缓存）。
/// </summary>
public static class TowerFloorTable库
{
    static TowerFloorTable 缓存;

    public static TowerFloorTable 取()
    {
        if (缓存 != null) return 缓存;
        var 库 = TowerDatabase库.取();
        if (库 != null && 库.层表 != null) 缓存 = 库.层表;
        return 缓存;
    }

    public static void 清缓存() { 缓存 = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置静态() { 清缓存(); }
}

/// <summary>
/// **NPC库的运行时取用口**（`Assets/resources/NPC数据/NPC库.asset`）。
/// 刷怪时 id → 定义走它。
/// </summary>
public static class NpcDatabase库
{
    static NpcDatabase 缓存;
    static bool 已警告;

    public static NpcDatabase 取()
    {
        if (缓存 != null) return 缓存;

        缓存 = Resources.Load<NpcDatabase>("NPC数据/NPC库");
        if (缓存 != null) return 缓存;

        if (!已警告)
        {
            已警告 = true;
            Debug.LogWarning("[NPC库] 读不到「NPC数据/NPC库」资产 —— 跑一次「修仙/从配置表生成资产」");
        }
        return null;
    }

    public static void 清缓存() { 缓存 = null; 已警告 = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置静态() { 清缓存(); }
}
