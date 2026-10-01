using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **镇妖塔**：刷怪 → 练级 → 上一层。
///
/// ## 用户的机制要求（2026-09-30）
///
/// 1. 玩家进入镇妖塔后，塔开始启动**当前层数**的刷怪
/// 2. 玩家打完一层后，可以选择**进入下一层**，或者**留在这一层**
/// 3. 所有怪物清理完毕 **10 秒后**会再次刷出这一组怪物
/// 4. 玩家死亡时，可以在 **10 秒内**选择**退到塔外**，或**进入上一层**；
///    没有选择 → **自动退出到塔外**
/// 5. 选择进入上一层 → **满血复活**，塔刷新并进行**等级调整**，继续刷怪
/// 6. 塔的等级**可被记录**（玩家爬到了多少层）；暂定 1000 层，每 10 层对应玩家一个等级
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

    [Tooltip("退到塔外时的落点名（黑幕过场的落点）。留空 = 用默认落点")]
    public string 塔外落点名 = "";

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

    /// <summary>正在等玩家做「下一层 / 留在本层」的选择</summary>
    public bool 等待通关选择 { get; private set; }

    /// <summary>正在等玩家做「退出 / 上一层」的死亡选择</summary>
    public bool 等待死亡选择 { get; private set; }

    /// <summary>上一层清空后是不是已经给过选择了（同一层只问一次）</summary>
    bool 本层已问过;

    /// <summary>玩家当前所在层刷完一轮，正在等重刷</summary>
    public bool 等待重刷 { get; private set; }

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

        if (刷怪区 == null) 刷怪区 = new List<SpawnZone>();
        if (刷怪区.Count == 0) 刷怪区.AddRange(FindObjectsOfType<SpawnZone>());
        刷怪区.RemoveAll(z => z == null);
    }

    void Start()
    {
        取玩家();
        挂事件();

        if (层表 == null)
            Debug.LogError("[镇妖塔] 找不到 TowerFloorTable —— 跑一次「修仙/从配置表生成资产」"
                           + "（会生成并收进 Resources/Data）", this);
        if (刷怪区.Count == 0)
            Debug.LogWarning("[镇妖塔] 场景里一个 SpawnZone 都没有，不会刷怪", this);

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
            if (刷怪区[i] != null) 刷怪区[i].清空 -= 处理一波清空;
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
            if (刷怪区[i] != null) 刷怪区[i].清空 += 处理一波清空;
    }

    void 找入塔落点()
    {
        if (入塔落点 != null) return;
        var go = GameObject.Find("TeleportTarget_入塔");
        if (go != null) 入塔落点 = go.transform;
    }

    // ============================================================ 开关塔

    /// <summary>启动塔：按当前层刷怪</summary>
    public void 启动塔()
    {
        塔运行中 = true;
        等待通关选择 = false;
        等待死亡选择 = false;
        等待重刷 = false;
        本层已问过 = false;
        刷新刷怪区();
    }

    /// <summary>停塔：停刷 + 清场</summary>
    public void 停止塔(bool 清掉怪 = true)
    {
        塔运行中 = false;
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
        本层已问过 = false;
        等待重刷 = false;

        if (界面 != null) 界面.隐藏通关选择();

        塔运行中 = true;
        刷新刷怪区();

        if (打印日志)
            Debug.Log("[镇妖塔] 进入第 " + 当前层 + " 层｜怪等级 " + 当前怪物等级
                      + "（大境界 " + 当前大境界 + "）｜组 " + 层表.取刷怪组(当前层), this);

        层变化?.Invoke(当前层);
        记录进度();
    }

    /// <summary>下一层（到底了就留在顶层）</summary>
    public void 下一层() => 进入层(当前层 + 1);

    /// <summary>留在这一层（重新刷本层）</summary>
    public void 留在本层()
    {
        等待通关选择 = false;
        本层已问过 = false;
        if (界面 != null) 界面.隐藏通关选择();

        塔运行中 = true;
        刷新刷怪区();
        if (打印日志) Debug.Log("[镇妖塔] 留在第 " + 当前层 + " 层，重新刷怪", this);
    }

    /// <summary>退到塔外（走黑幕过场切场景）</summary>
    public void 退到塔外()
    {
        停止塔();
        记录进度();

        if (打印日志) Debug.Log("[镇妖塔] 退出塔外 → 场景「" + 塔外场景名 + "」", this);

        if (string.IsNullOrEmpty(塔外场景名))
        {
            Debug.LogWarning("[镇妖塔] 没配「塔外场景名」，只停塔不切场景", this);
            return;
        }

        var 落点 = string.IsNullOrEmpty(塔外落点名) ? null : 塔外落点名;
        var 显示名 = 塔外场景名;
        // 唯一正路：黑幕字幕.开始场景过渡（它负责黑幕 / 摆位 / 钉进度）
        黑幕字幕.开始场景过渡(塔外场景名, 显示名, Vector3.zero, 落点);
    }

    // ============================================================ 清空一波

    void 处理一波清空(SpawnZone 区)
    {
        if (!塔运行中 || 等待死亡选择) return;

        一波清空?.Invoke(当前层);

        // 清空后先等重生延迟再重刷（SpawnZone 自己会重刷）；
        // 这里只在**第一次**清空时问玩家要不要上楼
        if (!本层已问过)
        {
            本层已问过 = true;
            等待通关选择 = true;
            等待重刷 = true;
            if (界面 != null) 界面.显示通关选择(当前层, 当前层 >= 层表.有效总层数);
            if (打印日志)
                Debug.Log("[镇妖塔] 第 " + 当前层 + " 层已清空 —— 等玩家选择「下一层 / 留在本层」"
                          + "（不选就 10 秒后按留在本层继续）", this);
            if (界面 == null) StartCoroutine(无界面自动留层());
        }
    }

    /// <summary>
    /// 【兜底】还没搭 UI 时，清空后自动留在本层。
    /// 否则塔会卡在「等一个永远不会有人按的选择」上 —— 调试时很容易误判成塔坏了。
    /// </summary>
    IEnumerator 无界面自动留层()
    {
        yield return new WaitForSeconds(重生延迟兜底());
        if (等待通关选择) 留在本层();
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

        // 顺便把「留在本层」的选择收掉（玩家已经死了，那个选择没意义了）
        if (等待通关选择) { 等待通关选择 = false; if (界面 != null) 界面.隐藏通关选择(); }

        if (打印日志) Debug.Log("[镇妖塔] 玩家在第 " + 当前层 + " 层倒下 —— "
                               + 死亡选择时限 + " 秒内选择：退出塔外 / 进入上一层", this);

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

        if (界面 != null) { 界面.隐藏死亡选择(); 界面.隐藏通关选择(); }
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
        进入层(当前层 + 1);

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
