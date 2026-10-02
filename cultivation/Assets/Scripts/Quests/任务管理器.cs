using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **任务管理器**：管三类任务（主线/支线/悬赏）的进度。
///
/// 一个任务 = 同一 <see cref="QuestDefinition.任务id"/> 下的 N 个**阶段**，按 <c>阶段</c> 升序推进；
/// 当前阶段完成才会把下一阶段设为当前（用户要求："只有当子任务完成时，才会触发下一个"）。
///
/// 完成条件四种，全部由这里统一检查：
///   · <b>提交物品</b>：背包里有够数量 → 交掉（扣物品）算完成
///   · <b>击杀</b>：目标 NPC 死掉（留空 = 任意 NPC 死一个；靠 <c>NpcInstance.Died</c> 事件 + 轮询兜底）
///   · <b>到达</b>：玩家进半径（每 0.25 秒查一次）
///   · <b>对话</b>：对话系统说完某一段时调 <see cref="通知对话"/>
///
/// 阶段开始时可以**调度 NPC**（播动画 / 攻击玩家 / 销毁），完成时**发物品奖励**，
/// 并且把 <see cref="QuestDefinition.接取加标记"/> / <see cref="QuestDefinition.完成加标记"/>
/// 写进 <see cref="对话标记"/> —— 对话表靠这两个标记就能长出新回答（和对话系统打通）。
/// </summary>
[DisallowMultipleComponent]
public class 任务管理器 : MonoBehaviour
{
    public static 任务管理器 实例 { get; private set; }

    /// <summary>某个阶段被设为当前（接取 / 推进到它）</summary>
    public static event Action<QuestDefinition> 阶段开始;
    /// <summary>某个阶段完成</summary>
    public static event Action<QuestDefinition> 阶段完成;
    /// <summary>整条任务（所有阶段）完成</summary>
    public static event Action<string> 任务全部完成;

    [Tooltip("背包数据。留空自动找场景里的 UIPanelData")]
    public UIPanelData 面板;

    [Tooltip("任务库。留空自动 Resources/任务/任务库")]
    public QuestDatabase 库;

    [Tooltip("检查「提交物品 / 到达」的间隔（秒）")]
    public float 检查间隔 = 0.25f;

    /// <summary>`条件=境界等级` 用（只找一次；找不到就留空，别的条件不受影响）</summary>
    PlayerCultivation 修行;

    readonly Dictionary<string, int> 当前阶段 = new Dictionary<string, int>();

    [Tooltip("演出用的等待条件靠它计时：任务id → 这一阶段是什么时候开始的")]
    readonly Dictionary<string, float> 阶段开始时间 = new Dictionary<string, float>();

    /// <summary>
    /// 阻塞式演出（黑幕白字 / 强制对话）正在进行中。
    /// 这期间**不推进任何阶段** —— 否则「条件=无」的演出阶段会立刻算完成，
    /// 任务直接走到下一阶段（比如「切换场景」），演出被切走甚至随旧场景一起销毁。
    /// </summary>
    /// <summary>
    /// 阻塞式演出（黑幕白字 / 强制对话）正在进行中。
    /// 这期间**不推进任何阶段** —— 否则「条件=无」的演出阶段会立刻算完成，
    /// 任务直接走到下一阶段（比如「切换场景」），演出被切走甚至随旧场景一起销毁。
    ///
    /// ⚠ 必须是 **static**：黑幕是跨场景的（`DontDestroyOnLoad`），而 任务管理器 会随场景重建。
    ///   如果它是实例字段，切场景后新的管理器读到 `false`，黑幕还盖着阶段就自己推进了。
    /// </summary>
    static bool 演出中;

    /// <summary>外部（黑幕自动收幕）通知：演出结束，放行阶段推进</summary>
    public static void 清演出中() { 演出中 = false; }

    /// <summary>"自称在演出、实际没有"持续了多久 —— 超过阈值就把 `演出中` 清掉（见自愈那段）</summary>
    float 空演出计时;

    /// <summary>接手过来的阶段，动作要等场景激活完的第一帧才发（见 导入进度 的注释）</summary>
    readonly List<string> 待发动作 = new List<string>();

    /// <summary>
    /// **跨场景**待补发的动作。切场景时旧的管理器会随旧场景销毁，实例字段带不过去，
    /// 所以用静态队列存着，由新场景的管理器在第一帧取出来执行。
    /// （四幕「切到宗门 → 移动玩家到落点」就靠它；之前依赖 导入进度 发动作，实测发不出来。）
    /// </summary>
    static readonly List<string> 跨场景待发动作 = new List<string>();
    readonly HashSet<string> 已完成任务 = new HashSet<string>();
    readonly List<NpcInstance> 已订阅 = new List<NpcInstance>();
    float 计时;

    /// <summary>
    /// 已经**执行过动作**的 `任务id#阶段`。
    ///
    /// 切场景那一帧会有三条路径碰到同一个阶段：`进入阶段()` 自己、`导入进度` 的待发动作、
    /// 以及跨场景队列。实测「生成NPC」因此跑两遍 —— 后一个大师兄顶掉前一个，
    /// 症状就是「到宗门后大师兄的对话不出现」。
    ///
    /// 判据必须用**独立**的一份记录：早先我拿 `阶段开始时间` 当判据是错的 ——
    /// `导入进度` 会给所有接手阶段写那个时间戳（「等待秒数」条件要靠它计时），
    /// 于是本该补发的动作全被当成"已执行"跳过了，主线直接卡死不推进。
    ///
    /// 用静态是因为 `进入阶段()`（旧场景）执行动作、队列（新场景）还要再判一次。
    /// </summary>
    static readonly HashSet<string> 已执行动作 = new HashSet<string>();

    static string 动作键(string 任务id, int 阶段) => 任务id + "#" + 阶段;

    /// <summary>
    /// 跨场景带过去的「等待秒数」剩余量：`任务id#剩余秒`。
    ///
    /// 为什么需要：`导入进度` 会把 `阶段开始时间` 重置成当前时间，于是一旦在等待期间
    /// 切场景（例如四幕第 10 阶段「等候大师兄禀报」等 60 秒时走进传送门），
    /// 计时就被清零重来 —— 玩家看到的是「剧情卡住不动」。
    /// 切场景前把剩余量记下来，新场景导回时按剩余量续上。
    /// </summary>
    /// <summary>跨场景带过去的「等待秒数」剩余量：任务id → 还剩多少秒</summary>
    static readonly Dictionary<string, float> 剩余等待 = new Dictionary<string, float>();

    /// <summary>
    /// **落点待执行状态**（四幕：切到宗门后把主角放到大师兄旁边）。
    ///
    /// 为什么是静态而不是协程：`切换场景` 这个动作是在**旧场景**的 任务管理器 上跑的，
    /// `LoadSceneAsync` 一完成旧场景就卸载，挂在该实例上的协程随之被销毁 ——
    /// 实测「协程入口日志有、协程体内日志一条都没有」，玩家因此停在场景初始摆放处
    /// `(-83.6, 18.13, 89.9)`（= P1 bug）。
    ///
    /// 改成静态坐标后由**活着的**管理器在 Update 里每帧按，跨场景也不会被销毁。
    /// 持续若干帧是为了压过场景自身的出生/摆放逻辑（它可能晚几帧才跑完）。
    /// </summary>
    static Vector3 落点目标;
    static float 落点截止时刻;
    /// <summary>登记落点的时刻（用来识别「接管 0 秒」= 立刻放置一次；见 处理落点）</summary>
    static float 落点登记时刻;
    static bool 落点报过;
    static bool 落点已执行;

    /// <summary>
    /// 落点流程是否还在进行（登记后为 true，接管结束或超时放权后为 false）。
    /// 「条件=落点结束」靠它判定 —— 用来让下一阶段等到主角真的被放到落点。
    /// </summary>
    static bool 落点进行中;

    /// <summary>落点流程是否还在接管（黑幕字幕的场景过渡要等它摆好才收幕）</summary>
    public static bool 落点进行中_公开 => 落点进行中;
    static int 落点起始帧 = -1;

    /// <summary>偏离这么大（米）才纠正。给足余量，避免把玩家"粘"在落点上走不动。</summary>
    const float 落点容差 = 1.0f;

    /// <summary>
    /// 登记落点。**只负责把玩家放到位，不负责按住他** ——
    /// 之前每帧硬写 position + 反复开关 CharacterController，表现是
    /// 「一直被卡在落点上、往外跑又被拉回来」，所以改成：
    ///   · 就绪后放置一次；
    ///   · 只在玩家偏离超过 落点容差 时才纠正（用来压过场景晚几帧的出生/摆放逻辑）；
    ///   · 用时间封顶（默认 1.2 秒），到点立即放权。
    /// </summary>
    public static void 请求落点(Vector3 位, float 接管秒 = 1.2f)
    {
        落点目标 = 位;
        // ⚠️ 不要用 Mathf.Max 抬下限：「接管 0 秒」是**合法且有意义的**用法 ——
        //    意思是"立刻放置一次就走"（传送落点就靠它）。
        //    原来写 Mathf.Max(0.1f, 接管秒) 会把 0 抬成 0.1，
        //    于是 处理落点 里的"到点放权"会先于"放置"成立 → 玩家永远不被摆到落点。
        落点截止时刻 = Time.time + Mathf.Max(0f, 接管秒);
        落点登记时刻 = Time.time;
        落点报过 = false;
        落点已执行 = false;
        落点进行中 = true;
        落点起始帧 = Time.frameCount;
        Debug.Log("[任务] 已登记落点：" + 位.ToString("F3") + "（接管 " + 接管秒.ToString("F2") + " 秒）");
    }

    /// <summary>
    /// 找玩家：优先按名字找（文档建议），再退到按 PlayerVitals 组件找。
    /// 不能只依赖 物品使用器.取玩家物体() 的静态缓存 —— 它跨场景可能还指向旧场景的玩家。
    /// </summary>
    static GameObject 找玩家物体()
    {
        var go = GameObject.Find("Player");
        if (go != null && go.activeInHierarchy) return go;
        return 物品使用器.取玩家物体();
    }

    /// <summary>每帧处理落点；由 Update 调用</summary>
    void 处理落点()
    {
        if (落点起始帧 < 0) return;                                  // 没有待处理落点
        if (落点起始帧 == Time.frameCount) return;                   // 登记当帧不动，让场景先摆完

        // ★ 「接管 0 秒」= **立刻放置一次就走**（传送落点就是这么用的）。
        //   踩过的坑（2026-09-27）：原来无条件先判 `Time.time >= 落点截止时刻`，
        //   而 0 秒接管会让「截止时刻 = 现在」→ 立刻成立 →
        //   `落点起始帧 = -1` 直接放弃，**位置从来没被应用过**。
        //   表现：传送到个人洞府后玩家站在 (0,0,0)，而不是落点。
        if (落点截止时刻 <= 落点登记时刻 + 0.0001f)
        {
            var 玩家一次 = 找玩家物体();
            if (玩家一次 != null)
            {
                var cc0 = 玩家一次.GetComponent<CharacterController>();
                if (cc0 != null) cc0.enabled = false;
                玩家一次.transform.position = 落点目标;
                if (cc0 != null) cc0.enabled = true;
                Debug.Log("[任务] 落点（0 秒接管，立刻放置）：" + 落点目标.ToString("F3")
                    + " → 实际 " + 玩家一次.transform.position.ToString("F3"));
            }
            落点起始帧 = -1;
            落点进行中 = false;
            return;
        }

        // 时间到就放权：不管放没放成功，都不能把玩家一直按着
        if (Time.time >= 落点截止时刻)
        {
            if (落点已执行) Debug.Log("[任务] 落点接管结束，交还操作");
            落点起始帧 = -1;
            落点进行中 = false;
            return;
        }

        var 玩家 = 找玩家物体();
        if (玩家 == null)
        {
            if (!落点报过) { 落点报过 = true; Debug.LogWarning("[任务] 落点：暂时取不到玩家，等它就绪"); }
            return;
        }

        var 当前 = 玩家.transform.position;
        float 偏离 = Vector3.Distance(new Vector3(当前.x, 0f, 当前.z),
                                      new Vector3(落点目标.x, 0f, 落点目标.z));
        bool 第一次 = !落点已执行;
        if (!第一次 && 偏离 <= 落点容差) return;      // 已经在落点上，别动他

        var cc = 玩家.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        玩家.transform.position = 落点目标;
        if (cc != null) cc.enabled = true;
        落点已执行 = true;

        if (!落点报过)
        {
            落点报过 = true;
            Debug.Log("[任务] 调度：玩家移动到 " + 落点目标.ToString("F3") + " → " + 玩家.name
                + " 实际 " + 玩家.transform.position.ToString("F3")
                + (第一次 ? "（首次放置）" : "（偏离 " + 偏离.ToString("F2") + "m，纠正）"));
        }
    }

    public QuestDatabase 取库() => 库 != null ? 库 : (库 = QuestDatabase.取());

    void Awake() { 实例 = this; }

    void Start()
    {
        if (面板 == null) 面板 = FindObjectOfType<UIPanelData>();
        取库();
        订阅死亡();
        应用存档();          // ★ 先把存档套到角色/面板上（**必须在 恢复存档进度 之前**）
        恢复存档进度();      // ★ 读档进来的主线进度（必须在 Awake 之后才能恢复）
        自动接取();
    }

    /// <summary>
    /// 把主菜单选中的存档套到角色和面板上。
    ///
    /// ★ 为什么要在这里、而不是靠场景里的 <c>SaveApplier</c>：
    ///   `SaveApplier.cs` 的注释写着"挂在 Player 上，Start 里跑一次"，
    ///   但它**没有挂在任何场景里**（Village / Sect 都查过，0 个实例）——
    ///   于是 `SaveSystem.应用到角色` 从来没被调用过：
    ///     ① 读档进来的修为/背包/位置 全都没恢复；
    ///     ② 新档清空面板的逻辑也不执行 → **场景里序列化的那 8 件物品一直在**，
    ///        表现就是"新建角色背包里还是有东西"。
    ///   任务管理器在游戏场景里一定有，所以放这里最稳（也不用去改场景）。
    ///
    /// ★★ **只在本局第一次进游戏场景时跑一次**（用户 2026-09-27 报的严重 bug）。
    ///
    /// 踩过的坑（表现：从宗门传送到个人洞府后，**背包东西全丢、学会的神通全没**）：
    ///   这个方法原来在**每个场景**的 Start 里都跑，而它内部：
    ///     ① `是新档` 判定 = 存档里背包/法宝/灵阵/坐骑/真灵**全空** → 调 `清空玩法数据()`；
    ///     ② `恢复面板()` **无条件** `面板.物品 = new List<...>()`，再按存档灌。
    ///   存档是空的时候（新开档还没存过），每次切场景都会：
    ///     `跨场景数据` 刚把背包接过来 → 这里又清成空 → **玩家数据全丢**。
    ///   实测日志：
    ///     `[跨场景] 已把上一场景的数据接过来：背包 25 件`
    ///     `[存档] 判定为新档：已清空场景面板`        ← 刚接过来就被清掉
    ///
    /// 存档是**每局开局**的东西，不该在换场景时重放。所以用一个静态标记挡住。
    /// （`SaveSystem.应用到角色` 本身是"选完档套数据"，语义就是一次性。）
    /// </summary>
    static bool 本局已应用存档;

    void 应用存档()
    {
        if (本局已应用存档) return;                       // ★ 本局已经套过，别再动玩家数据
        if (SaveSystem.当前存档 == null) return;
        本局已应用存档 = true;
        SaveSystem.应用到角色(SaveSystem.当前存档);
        Debug.Log("[任务] 已应用存档（本局首次）：" + SaveSystem.当前存档.角色名
            + " / 槽位 " + SaveSystem.当前槽位);
    }

    /// <summary>
    /// 读档进来的任务进度 / 对话标记。
    ///
    /// 为什么放在 Start 而不是直接在 SaveSystem.应用到角色 里恢复：
    /// `应用到角色` 是**游戏场景加载之前**跑的，那时本组件还不存在。
    /// 所以 SaveSystem 把进度挂起，这里来取（取完即清，只恢复一次）。
    /// </summary>
    void 恢复存档进度()
    {
        string 任务进度, 标记串;
        SaveSystem.取挂起的进度(out 任务进度, out 标记串);
        if (string.IsNullOrEmpty(任务进度) && string.IsNullOrEmpty(标记串)) return;

        if (!string.IsNullOrEmpty(标记串))
        {
            对话标记.清空();
            foreach (var m in 标记串.Split(';'))
                if (!string.IsNullOrWhiteSpace(m)) 对话标记.添加(m.Trim());
        }
        if (!string.IsNullOrEmpty(任务进度)) 导入进度(任务进度);

        补齐进度标记();      // ★ 读旧档时把阶段早已过去的标记补回来（否则会掉进通用对话）

        Debug.Log("[任务] 已从存档恢复主线进度：[" + 任务进度 + "]");
    }

    // ================================================================ 查询

    /// <summary>
    /// 把进度导出成一行字符串，给跨场景接力用（`任务id:阶段;任务id:阶段;…`，阶段 0 = 已完成）。
    /// 场景切换会销毁旧场景的一切，靠这个 + <see cref="导入进度"/> 让主线进度不归零。
    /// </summary>
    public string 导出进度()
    {
        var 串 = new System.Text.StringBuilder();
        foreach (var kv in 当前阶段)
        {
            if (串.Length > 0) 串.Append(';');
            串.Append(kv.Key).Append(':').Append(kv.Value);
        }
        foreach (var id in 已完成任务)
        {
            if (串.Length > 0) 串.Append(';');
            串.Append(id).Append(":0");
        }
        return 串.ToString();
    }

    /// <summary>
    /// 切场景**之前**钉住主线进度（给任何切场景入口复用：任务表「切换场景」、传送点…）。
    ///
    /// 为什么必须在加载之前钉：`LoadSceneAsync` 一完成旧场景就卸载，
    /// `OnDestroy` 里那次 `FindObjectOfType` 可能抓到新旧场景共存期间即将销毁的旧管理器，
    /// 导出残缺 → 新场景主线不推进（实测过）。
    /// </summary>
    public void 切场景前钉进度()
    {
        if (当前阶段.Count == 0 && 已完成任务.Count == 0) return;   // 没在跑任务就不用钉
        准备切场景();
        跨场景数据.钉住任务进度(导出进度());
        Debug.Log("[任务] 切场景前钉住进度：[" + 导出进度() + "]");
    }

    /// <summary>
    /// **按当前进度把漏掉的对话标记补齐**（读档 / 跨场景后调）。
    ///
    /// 为什么需要（实测 bug）：`接取加标记` / `完成加标记` 只在阶段**实际走到**时才打。
    /// 读一个旧档时阶段已经过去了，标记**永远不会补上** ——
    /// 表现就是大师兄回话时 `q_主线_在宗门` 是 false，三条主线对白全不满足，
    /// 对话系统退到通用段 `dlg_common_01`（"这位道友面生得很…"）。
    ///
    /// 做法：对每个任务，把「已完成的阶段」和「当前阶段之前」的所有 `打标记` 重放一遍。
    /// 标记列是幂等的（HashSet），重复添加无副作用。
    /// </summary>
    void 补齐进度标记()
    {
        var db = 取库();
        if (db == null) return;
        int 补 = 0;

        // 已完成的任务：它的每个阶段的 打标记 都该有
        foreach (var id in 已完成任务)
        {
            foreach (var 阶段 in db.取任务(id))
            {
                if (阶段 == null || string.IsNullOrEmpty(阶段.打标记)) continue;
                对话标记.添加一批(阶段.打标记);
                补++;
            }
        }

        // 进行中的任务：当前阶段之前（不含当前）的 打标记 都该有
        foreach (var kv in 当前阶段)
        {
            foreach (var 阶段 in db.取任务(kv.Key))
            {
                if (阶段 == null || string.IsNullOrEmpty(阶段.打标记)) continue;
                if (阶段.阶段 >= kv.Value) continue;      // 当前阶段及之后还没走到
                对话标记.添加一批(阶段.打标记);
                补++;
            }
        }

        if (补 > 0)
            Debug.Log("[任务] 读档补齐对话标记 " + 补 + " 处，现有 ["
                + string.Join(";", 对话标记.全部标记()) + "]");
    }

    /// <summary>
    /// 切场景前调用：把「等待秒数」还没走完的剩余量记下来，交给新场景续上。
    /// 只记**当前阶段**且**条件=等待秒数**的任务 —— 别的任务没有等待在跑。
    /// 直接记「剩余秒」，不依赖 取库()（它在 Awake 期间可能还没加载）。
    /// </summary>
    public void 准备切场景()
    {
        剩余等待.Clear();
        foreach (var kv in 当前阶段)
        {
            var 阶段 = 取当前阶段(kv.Key);
            if (阶段 == null || 阶段.条件 != 任务条件.等待秒数) continue;
            if (阶段.等待秒 <= 0f) continue;

            float 起;
            if (!阶段开始时间.TryGetValue(kv.Key, out 起)) continue;
            float 剩 = 阶段.等待秒 - (Time.time - 起);
            if (剩 <= 0f) continue;                       // 已经等够了，不用带
            剩余等待[kv.Key] = 剩;
        }
        if (剩余等待.Count > 0)
        {
            var 串 = new System.Text.StringBuilder();
            foreach (var kv in 剩余等待) { if (串.Length > 0) 串.Append(';'); 串.Append(kv.Key).Append('#').Append(kv.Value.ToString("F2")); }
            Debug.Log("[任务] 切场景前记下未走完的等待：[" + 串 + "]");
        }
    }

    /// <summary>从 <see cref="导出进度"/> 的字符串恢复进度</summary>
    public void 导入进度(string 串)
    {
        if (string.IsNullOrEmpty(串)) return;
        取库();          // 下面换算剩余等待要用到阶段定义，先确保库已加载
        当前阶段.Clear();
        已完成任务.Clear();
        foreach (var 段 in 串.Split(';'))
        {
            if (string.IsNullOrWhiteSpace(段)) continue;
            var p = 段.Split(':');
            if (p.Length != 2) continue;
            int 阶段号;
            if (!int.TryParse(p[1], out 阶段号)) continue;
            if (阶段号 <= 0) 已完成任务.Add(p[0]);
            else 当前阶段[p[0]] = 阶段号;
        }
        // 把「阶段开始时间」补上 —— 「等待秒数」条件靠它计时，没有就永远不推进。
        // ⚠ 它**不能**兼作「动作是否已执行」的判据（见 已执行动作 的注释）。
        // ⚠ 动作**不在这里直接发**：导入进度 是在新场景 Awake 期间被 跨场景数据 调用的，
        //   那时场景还没激活完，StartCoroutine 会失败并抛异常，把整个 Awake 打断。
        //   改由第一帧 Update 发（待发动作）。
        待发动作.Clear();
        foreach (var kv in 当前阶段)
        {
            阶段开始时间[kv.Key] = Time.time;
            待发动作.Add(kv.Key);
        }

        // 切场景前没走完的等待：把开始时间往前挪，让 Update 的
        // `Time.time - 起 >= 阶段.等待秒` 按真实剩余量判定，而不是从零重数。
        // 放在这里依赖 取库()，所以前面先确保库已加载。
        if (剩余等待.Count > 0)
        {
            foreach (var kv in 剩余等待)
            {
                var q = 取当前阶段(kv.Key);
                if (q == null || q.条件 != 任务条件.等待秒数) continue;
                阶段开始时间[kv.Key] = Time.time - (q.等待秒 - kv.Value);
            }
            Debug.Log("[任务] 续上未走完的等待 " + 剩余等待.Count + " 个");
            剩余等待.Clear();
        }
        Debug.Log("[任务] 已接手上一场景的进度：" + 当前阶段.Count + " 个任务推进中、"
            + 已完成任务.Count + " 个已完成  [" + 串 + "]");
    }

    public bool 进行中(string 任务id) => !string.IsNullOrEmpty(任务id) && 当前阶段.ContainsKey(任务id);
    public bool 已完成(string 任务id) => !string.IsNullOrEmpty(任务id) && 已完成任务.Contains(任务id);
    public int 取当前阶段号(string 任务id)
    {
        int v;
        return 当前阶段.TryGetValue(任务id, out v) ? v : 0;
    }

    /// <summary>当前阶段的定义（没有就 null）</summary>
    public QuestDefinition 取当前阶段(string 任务id)
    {
        var db = 取库();
        int 段 = 取当前阶段号(任务id);
        return db != null && 段 > 0 ? db.取阶段(任务id, 段) : null;
    }

    /// <summary>面板要显示的：进行中的阶段列表</summary>
    public List<QuestDefinition> 进行中的阶段()
    {
        var 出 = new List<QuestDefinition>();
        foreach (var kv in 当前阶段)
        {
            var q = 取当前阶段(kv.Key);
            if (q != null) 出.Add(q);
        }
        return 出;
    }

    // ================================================================ 接取 / 推进

    /// <summary>接一个任务（从第 1 阶段开始）</summary>
    public bool 接取(string 任务id)
    {
        var db = 取库();
        if (db == null || string.IsNullOrEmpty(任务id)) return false;
        if (进行中(任务id) || 已完成(任务id)) return false;

        var 首 = db.取阶段(任务id, 1);
        if (首 == null) { Debug.LogWarning("[任务] 库里没有任务 " + 任务id + " 的第 1 阶段"); return false; }
        if (!string.IsNullOrEmpty(首.前置任务id) && !已完成(首.前置任务id))
        {
            Debug.Log("[任务] " + 首.任务名 + " 需要先完成前置任务 " + 首.前置任务id);
            return false;
        }

        进入阶段(任务id, 首);
        Debug.Log("[任务] 接取「" + 首.任务名 + "」第 " + 首.阶段 + " 阶段：" + 首.阶段名, 首);
        return true;
    }

    void 进入阶段(string 任务id, QuestDefinition 阶段)
    {
        当前阶段[任务id] = 阶段.阶段;
        阶段开始时间[任务id] = Time.time;      // ★ 「等待秒数」条件从这一刻开始计时
        对话标记.添加一批(阶段.接取加标记);
        执行动作(阶段);
        阶段开始?.Invoke(阶段);
    }

    /// <summary>完成当前阶段 → 加标记 → 发奖 → 进下一阶段（没有下一阶段就整条完成）</summary>
    public bool 完成当前阶段(string 任务id)
    {
        var db = 取库();
        var 阶段 = 取当前阶段(任务id);
        if (db == null || 阶段 == null) return false;

        // 提交物品：真的把东西扣掉
        if (阶段.条件 == 任务条件.提交物品 && 面板 != null)
        {
            var 物品 = db.找物品(阶段.物品id);
            if (物品 == null || !面板.移除物品(物品, 阶段.数量))
            {
                Debug.Log("[任务] 交不了：" + 阶段.物品id + " ×" + 阶段.数量 + " 不够", 阶段);
                return false;
            }
        }

        对话标记.添加一批(阶段.完成加标记);
        发奖励(阶段);
        阶段完成?.Invoke(阶段);
        Debug.Log("[任务] 完成「" + 阶段.任务名 + "」第 " + 阶段.阶段 + " 阶段：" + 阶段.阶段名, 阶段);

        var 下一 = db.取阶段(任务id, 阶段.阶段 + 1);
        if (下一 != null) 进入阶段(任务id, 下一);
        else
        {
            当前阶段.Remove(任务id);
            已完成任务.Add(任务id);
            Debug.Log("[任务] 「" + 阶段.任务名 + "」全部完成 ✔", 阶段);
            任务全部完成?.Invoke(任务id);
        }
        return true;
    }

    void 发奖励(QuestDefinition 阶段)
    {
        // 贡献是"加法"，和物品奖励互不影响：没有奖励物品的阶段也可能发贡献
        if (阶段.奖励贡献 > 0) 宗门贡献.加(阶段.奖励贡献, "任务「" + 阶段.任务名 + "」第 " + 阶段.阶段 + " 阶段");

        if (面板 == null || string.IsNullOrEmpty(阶段.奖励物品)) return;
        var db = 取库();
        if (db == null) return;
        foreach (var s in db.解析奖励(阶段.奖励物品))
        {
            if (s.物品 == null) continue;
            面板.给物品(s.物品, Mathf.Max(1, s.数量));
            Debug.Log("[任务] 奖励 " + s.物品.DisplayName + " ×" + s.数量);
        }
    }

    // ================================================================ 条件检查

    void Update()
    {
        计时 += Time.deltaTime;

        // ★ 镜头兜底：接管超过时限还没交还（多半是演出协程被切场景/销毁打断）→ 自己还回去，
        //   否则 TopDownCamera 会被永久关着，玩家再也不能操作视角 ✗
        if (被停的相机脚本.Count > 0 && 镜头兜底交还时间 > 0f && Time.time > 镜头兜底交还时间)
        {
            Debug.LogWarning("[任务] 镜头超时兜底交还（演出协程可能被打断了）");
            交还相机();
        }
        if (计时 < 检查间隔) return;
        计时 = 0f;

        // ★ 落点：不受检查间隔限制，每帧都看（但只在偏离超容差时才动手）
        处理落点();

        var db = 取库();
        if (db == null || 当前阶段.Count == 0) return;

        // ★★ 必须在「演出中」判断**之前**跑的条件：黑幕落下完成。
        //
        //   死锁教训（2026-09-27，实测「切场景卡住」就是这么来的）：
        //   阶段3 是「黑幕字幕 演完 → 条件=黑幕落下完成 → 下一阶段切场景」，
        //   而 播黑幕 会把 演出中 置真；原来 `if (演出中) return;` 在条件检查**之前**，
        //   于是演出期间**任何条件都不检查** → 黑幕落下完成 永远不被判定 →
        //   阶段3 永远不推进 → 永远卡在「从此…」那个黑幕上。
        //   这个条件天生就是"等演出做完"，所以必须无视演出锁。
        foreach (var id in new List<string>(当前阶段.Keys))
        {
            var q = 取当前阶段(id);
            if (q == null) continue;
            if (q.条件 != 任务条件.黑幕落下完成) continue;

            // ★ 两种用法要分开判，别用「或」：
            //   · 动作=黑幕字幕 且 等待秒=0 → 幕**不收起**、要留给后续（如切场景）：
            //     只认「字打完 + 停够 打完停留」。**不能**认 落黑幕完成（淡入结束），
            //     否则淡入（24 档≈2s）会比"字打完+停2s"先到，直接短路掉那 2 秒 ——
            //     实测就是这么"最后一个字打完马上转场"的。
            //   · 其余（动作=落黑幕 等）：淡入跑完就放行。
            bool 不收幕模式 = q.动作 == 任务动作.黑幕字幕 && q.等待秒 <= 0f;
            bool 放行 = 不收幕模式 ? 黑幕字幕.本句可以推进
                                   : (黑幕字幕.落黑幕完成 || 黑幕字幕.本句可以推进);
            if (放行)
            {
                黑幕字幕.记录放行时刻(q.id + " 第" + q.阶段 + "阶段");
                完成当前阶段(id);
            }
        }

        // ★ 「条件=对话」的阶段：要跟谁说话，那个人不在场就补出来。
        //   放在**演出锁之前**是有意的：它不参与"条件是否达成"的判定，纯粹是"把场景补齐"，
        //   所以不该被演出锁挡住（否则过场期间进场景，人得等过场结束才出现）。
        //   · 只补**任务自己生成过**的 NPC（`找NPC` 内部查 `生成过的NPC` 那张表），
        //     所以不会凭空造出场景里本来没有的路人；
        //   · 每个"阶段+NPC"只试一次（见 试过补的对话NPC），失败也不刷日志。
        //   踩过的坑（2026-10-02）：洞府那一幕要跟大师兄说话，而他是 `生成NPC` 造的
        //   场景内对象 —— 玩家自己走进洞府时他随旧场景销毁、任务动作又不会重跑 ⇒ 整条线卡死。
        foreach (var id in new List<string>(当前阶段.Keys))
        {
            var q = 取当前阶段(id);
            if (q == null || q.条件 != 任务条件.对话) continue;

            // ★ 场景门禁（2026-10-02 用户实测报的"还没进洞府，大师兄就把洞府那一幕演完了"）：
            //   这一段对话此刻**根本出不来**（例：`dlg_act5_dongfu_1` 被 `场景_3C_Testbed`
            //   锁在洞府里），那就别把那个 NPC 凭空补到玩家跟前 ——
            //   否则玩家在宗门传送点会看到一个能按 F 的师兄（虽然只会说兜底台词），
            //   而这一阶段的意图是"走进传送光"（引导这时候指的是传送点，见 任务引导.解析目标）。
            //   进洞府之后这段就"能出"了，补人照旧发生 —— 洞府线不会因此卡死。
            if (!阶段对话现在能出(q)) continue;

            确保对话NPC在场(q);
        }

        // ★★ 「演出中」自愈（2026-10-02 实测踩到）★
        //
        // `演出中` 是 **static**，由 `播黑幕 / 播对话 / 切到场景` 置真、由黑幕收幕或对话演完置假。
        // 一旦那段演出**被打断**（切场景把协程连带销毁、强制对话还在等玩家点、对象被销毁…），
        // 它就**永远卡在 true** —— 而它卡住的表现是"**所有阶段条件都不再判定**"，
        // 也就是整条主线**静默冻住**（实测：等级到了炼气三层，阶段28 死活不推进）。
        //
        // 这里做一次自检：连续 1.5 秒"自称在演出、但实际上既没有对话也没有黑幕"就清掉。
        // 用 1.5 秒而不是立刻，是因为 `播对话` 会**先**置真、对话框下一帧才显示，
        // 立刻清会误判成"演完了"，让条件=无 的阶段在过场里偷偷推进。
        if (演出中 && !DialogueUI.正在显示 && !黑幕字幕.有幕在显示)
        {
            空演出计时 += Time.unscaledDeltaTime;
            if (空演出计时 >= 1.5f)
            {
                空演出计时 = 0f;
                演出中 = false;
                Debug.LogWarning("[任务] 「演出中」卡住了（没有对话也没有黑幕）→ 已自动清掉，恢复阶段判定");
            }
        }
        else 空演出计时 = 0f;

        if (演出中) return;      // ★ 其余条件：有阻塞式演出在跑就先别推进阶段
        // ★ 接手过来的阶段，动作在这里（场景激活完之后的第一帧）才发
        if (待发动作.Count > 0)
        {
            var 待 = new List<string>(待发动作);
            待发动作.Clear();
            foreach (var id in 待)
            {
                var q = 取当前阶段(id);
                if (q != null && q.动作 != 任务动作.无)
                {
                    // ★ 动作已经执行过的阶段不要再执行一次（见 已执行动作 的注释）
                    if (已执行动作.Contains(动作键(id, q.阶段))) { Debug.Log("[任务] 跳过重复派发（动作已执行过）：" + 动作键(id, q.阶段)); continue; }
                    Debug.Log("[任务] 补发接手阶段的动作：" + q.id + " 动作=" + q.动作);
                    执行动作(q);
                }
            }
        }

        // ★ 跨场景队列（切场景前存下的），同样在第一帧发
        if (跨场景待发动作.Count > 0)
        {
            var 待 = new List<string>(跨场景待发动作);
            跨场景待发动作.Clear();
            foreach (var id in 待)
            {
                var q = 取当前阶段(id);
                Debug.Log("[任务] 跨场景补发动作：" + (q != null ? q.id + " 动作=" + q.动作 : id + "（这个任务在新场景里没有当前阶段）"));
                if (q != null && q.动作 != 任务动作.无)
                {
                    // 同 待发动作：动作执行过的阶段不要再跑一遍
                    if (已执行动作.Contains(动作键(id, q.阶段))) { Debug.Log("[任务] 跳过重复派发（动作已执行过）：" + 动作键(id, q.阶段)); continue; }
                    执行动作(q);
                }
            }
        }

        var 快照 = new List<string>(当前阶段.Keys);
        foreach (var 任务id in 快照)
        {
            var 阶段 = 取当前阶段(任务id);
            if (阶段 == null) continue;
            switch (阶段.条件)
            {
                case 任务条件.无:
                    完成当前阶段(任务id);
                    break;
                case 任务条件.等待秒数:
                    {
                        // ★ 演出用：进这一阶段后等 等待秒 就自动完成（"停留2s""等候60s"）
                        float 起;
                        if (阶段开始时间.TryGetValue(任务id, out 起) && Time.time - 起 >= 阶段.等待秒)
                            完成当前阶段(任务id);
                        break;
                    }
                case 任务条件.提交物品:
                    {
                        var 物品 = db.找物品(阶段.物品id);
                        if (物品 != null && 面板 != null && 面板.物品数量(物品) >= 阶段.数量) 完成当前阶段(任务id);
                        break;
                    }
                case 任务条件.到达:
                    {
                        var 玩家 = 物品使用器.取玩家物体();
                        if (玩家 != null && Vector3.Distance(玩家.transform.position, 阶段.坐标) <= 阶段.到达半径)
                            完成当前阶段(任务id);
                        break;
                    }
                case 任务条件.击杀:
                    检查击杀(任务id, 阶段);
                    break;
                case 任务条件.NPC到位:
                    {
                        // ★ 等目标 NPC 走到落点附近才推进。
                        //   四幕：大师兄飞回来要**到位之后**再跳对话，
                        //   用「等待秒数」只能碰运气（飞得慢还没到、飞得快就白等）。
                        //   判定基准优先取「飞到」记下的动态落点（飞向玩家时它是实时算的），
                        //   取不到才退回任务表里的坐标。
                        //
                        // ⚠️ 目标 id 的兜底（踩过的坑 2026-09-27）：
                        //   表里 NPC 常常只填在「动作目标npcId」（给 飞到/播动画 用），
                        //   而条件的「目标npcId」是空的。原来只读后者 ⇒
                        //   ① `查找在场NPC("")` 拿不到人；② 目标位退回 (0,0,0) ⇒
                        //   **距离判定永远是 >1.5，阶段永远卡死**（实测：大师兄飞到玩家身边后没后续）。
                        var 目标id = 阶段.目标npcId;
                        if (string.IsNullOrEmpty(目标id)) 目标id = 阶段.动作目标npcId;

                        // ★ 必须用会**重建**的 找NPC，不能用只读的 查找在场NPC。
                        //   踩过的坑（2026-09-27）：大师兄是「生成NPC」造出来的**场景内对象**，
                        //   玩家一换场景它就随旧场景销毁 ✗。而 查找在场NPC 是"只读、找不到就 null"，
                        //   于是「NPC到位」永远等不到人 → 阶段13 卡死、后面领路对话不出现
                        //   （用户报："大师兄飞到我身边就没后续了"）。
                        //   找NPC 在它不在场时会就地重建（重建点 = 玩家前方，正是"回来找玩家"的语义）。
                        var npc = 找NPC(目标id);
                        if (npc != null)
                        {
                            Vector3 目标位;
                            bool 有基准 = false;
                            if (!string.IsNullOrEmpty(目标id) && NPC落点.TryGetValue(目标id, out 目标位))
                                有基准 = true;
                            else if (阶段.坐标 != Vector3.zero)
                            {
                                目标位 = 阶段.坐标;
                                有基准 = true;
                            }
                            else
                            {
                                // 既没动态落点也没填坐标 → 没有判定基准。
                                // 大师兄这类"飞回来找玩家"的，基准就是**玩家脚下**：
                                // 重建点也在玩家身边，所以这样判是合理的，不会卡死。
                                var 玩 = 物品使用器.取玩家物体();
                                if (玩 != null) { 目标位 = 玩.transform.position; 有基准 = true; }
                                else 目标位 = Vector3.zero;
                                // ⚠️ 这条只报一次（踩过的坑 2026-09-27：写在每帧判定里 → 刷屏几百条）
                                if (有基准 && !报过NPC到位兜底)
                                {
                                    报过NPC到位兜底 = true;
                                    Debug.Log("[任务] " + 阶段.id + "「NPC到位」没动态落点也没坐标 → 以玩家位置为基准");
                                }
                            }

                            if (有基准)
                            {
                                var 平 = npc.transform.position - 目标位;
                                平.y = 0f;
                                if (平.magnitude <= 到位判定距离) 完成当前阶段(任务id);
                            }
                            else
                            {
                                // 连玩家都找不到：NPC 在场就算到位，别把主线卡死
                                Debug.LogWarning("[任务] " + 阶段.id + "「NPC到位」取不到玩家也取不到落点 → NPC 在场即完成");
                                完成当前阶段(任务id);
                            }
                        }
                        break;
                    }
                case 任务条件.灵田已种植:
                    {
                        // 灵田教学"种下第一颗种子"靠它验真（原来那一阶段是 `条件=无`，
                        // 收到道具的下一帧就完成，"去种一颗"这件事根本没被检查过）
                        var 田 = 灵田.取();
                        if (田 != null && 田.已种数 >= Mathf.Max(1, 阶段.数量)) 完成当前阶段(任务id);
                        break;
                    }
                case 任务条件.境界等级:
                    {
                        // 长线门槛（镇妖塔准入 = 炼气三层）
                        if (修行 == null) 修行 = FindObjectOfType<PlayerCultivation>();
                        if (修行 != null && 修行.等级 >= Mathf.Max(1, 阶段.数量)) 完成当前阶段(任务id);
                        break;
                    }
                case 任务条件.落点结束:
                    {
                        // ★ 等落点流程结束（主角已被放到策划落点）才推进。
                        //   四幕用它保证「黑幕结束 ≈ 主角已经站在宗门落点上」。
                        if (!落点进行中) 完成当前阶段(任务id);
                        break;
                    }
                case 任务条件.黑幕落下完成:
                    // 已在 Update 开头（演出锁之前）处理 —— 见那里的死锁说明。
                    break;
            }
        }
    }

    /// <summary>
    /// **`条件=对话` 的阶段：要跟谁说话，那个人不在场就把他补出来**（放在玩家旁边）。
    ///
    /// 【为什么需要】踩过的坑（2026-10-02，整条洞府线卡死）：
    ///   `q_main_004` 阶段17/18 的条件是"跟 `npc_dashixiong` 说完某一段对话"，
    ///   而大师兄是 `生成NPC` / `飞到` 造出来的**场景内对象** —— 玩家自己走进洞府
    ///   （走 `sect1 to 3c` 传送门，**不是**任务驱动的 `切换场景`）时，他随旧场景一起销毁；
    ///   而任务动作**不会**在新场景重跑（`已执行动作` 会跳过同一阶段）。
    ///   结果：洞府里根本没有大师兄 ⇒ 那两段对话永远触发不了 ⇒ 灵田教学整条线停在那儿。
    ///
    /// 【修法】谁在条件里"等着跟某个人说话"，那个人不在就补出来 ——
    ///   比"往每个场景都手摆一份 NPC"省事，也不会漏场景。
    ///   ⚠️ 只补**任务自己生成过**的 NPC（`找NPC` 内部查 `生成过的NPC` 那张表），
    ///      所以不会凭空造出场景里本来没有的路人。
    ///   ⚠️ 每个"阶段+NPC"只尝试一次：失败（没有预制体记录）也不要每 0.25 秒刷一条日志。
    /// </summary>
    void 确保对话NPC在场(QuestDefinition 阶段)
    {
        string npcId = 取对话目标npcId(阶段);
        if (string.IsNullOrEmpty(npcId)) return;
        if (查找在场NPC(npcId) != null) return;

        string 键 = 阶段.id + "|" + npcId;
        if (!试过补的对话NPC.Add(键)) return;      // 只补一次

        var 补 = 找NPC(npcId);                     // 不在场时会就地重建（重建点 = 玩家前方）
        Debug.Log("[任务] 「" + 阶段.阶段名 + "」要跟 " + npcId + " 说话，但它不在场 → "
                  + (补 != null ? "已就地补出来 @" + 补.transform.position.ToString("F2")
                                : "补不出来（任务没生成过它，没有预制体记录）"), 阶段);
    }

    /// <summary>每个"阶段+NPC"只补一次（免得失败时刷屏）</summary>
    readonly HashSet<string> 试过补的对话NPC = new HashSet<string>();

    /// <summary>
    /// 这一阶段要跟哪个 NPC 说话：优先任务表的 `目标npcId`，
    /// 没填就用 `对话id` 去对话库反查（`条件=对话` 的行常常只填了对话id）
    /// </summary>
    static string 取对话目标npcId(QuestDefinition 阶段)
    {
        if (阶段 == null) return "";
        if (!string.IsNullOrEmpty(阶段.目标npcId)) return 阶段.目标npcId;
        var 库 = DialogueDatabase.取();
        return 库 != null ? 库.取NpcId(阶段.对话id) : "";
    }

    /// <summary>
    /// 这一阶段点名的那段对话**此刻能不能出来**（条件 / 场景门禁都算上）。
    /// 对话库取不到时返回 true（= 按老行为办），见 <see cref="DialogueDatabase.阶段对话能出"/>。
    /// </summary>
    static bool 阶段对话现在能出(QuestDefinition 阶段)
    {
        var 库 = DialogueDatabase.取();
        return 库 == null || 库.阶段对话能出(阶段);
    }

    /// <summary>只读地在场查找：找不到就返回 null，**不会重建**（重建是 找NPC 的职责）</summary>
    static GameObject 查找在场NPC(string npcId)    {
        if (string.IsNullOrEmpty(npcId)) return null;
        foreach (var npc in FindObjectsOfType<NpcInstance>())
        {
            if (npc == null) continue;
            if (npc.定义 != null && npc.定义.id == npcId) return npc.gameObject;
        }
        return null;
    }

    /// <summary>「NPC到位」条件用的判定距离（米）</summary>
    const float 到位判定距离 = 1.5f;

    void 检查击杀(string 任务id, QuestDefinition 阶段)
    {
        foreach (var npc in FindObjectsOfType<NpcInstance>())
        {
            if (npc == null || !npc.IsDead) continue;
            if (!string.IsNullOrEmpty(阶段.目标npcId) && (npc.定义 == null || npc.定义.id != 阶段.目标npcId)) continue;
            完成当前阶段(任务id);
            return;
        }
    }

    void 订阅死亡()
    {
        foreach (var npc in FindObjectsOfType<NpcInstance>())
        {
            if (npc == null || 已订阅.Contains(npc)) continue;
            npc.Died += 处理死亡;
            已订阅.Add(npc);
        }
    }

    void 处理死亡(NpcInstance 谁)
    {
        if (谁 == null) return;
        string id = 谁.定义 != null ? 谁.定义.id : "";
        foreach (var 任务id in new List<string>(当前阶段.Keys))
        {
            var 阶段 = 取当前阶段(任务id);
            if (阶段 == null || 阶段.条件 != 任务条件.击杀) continue;
            if (!string.IsNullOrEmpty(阶段.目标npcId) && 阶段.目标npcId != id) continue;
            完成当前阶段(任务id);
        }
    }

    // ================================================================ 对话系统接线

    /// <summary>对话系统说完某一段时调这里（条件=对话 的阶段会因此完成）</summary>
    public void 通知对话(string 对话id, string npcId)
    {
        foreach (var 任务id in new List<string>(当前阶段.Keys))
        {
            var 阶段 = 取当前阶段(任务id);
            if (阶段 == null || 阶段.条件 != 任务条件.对话) continue;
            if (!string.IsNullOrEmpty(阶段.对话id) && 阶段.对话id != 对话id) continue;
            完成当前阶段(任务id);
        }
    }

    /// <summary>对话系统显示某一段时调这里：把这一段绑定的任务接/完成（对话表 触发任务 / 完成任务 列）</summary>
    public void 处理对话绑定(string 触发任务, string 完成任务)
    {
        if (!string.IsNullOrEmpty(触发任务)) 接取(触发任务);
        if (!string.IsNullOrEmpty(完成任务)) 完成当前阶段(完成任务);
    }

    // ================================================================ 调度 NPC

    void 执行动作(QuestDefinition 阶段)
    {
        if (阶段.动作 == 任务动作.无) return;

        // ★ 登记「这个阶段的动作已经跑过」：跨场景队列 / 待发动作据此跳过重复派发
        //   （重复执行会让「生成NPC」多生成一个大师兄，把前一个顶掉 → 对话不出现）
        已执行动作.Add(动作键(阶段.任务id, 阶段.阶段));

        // ---- 过场演出类：不需要目标 NPC，先处理掉 ----
        switch (阶段.动作)
        {
            case 任务动作.生成NPC:     生成NPC(阶段); return;
            case 任务动作.推进天数:
                {
                    // 「过了一夜」要真的过一天：不然玩家拿不到新的一份修炼机会，纪年也不动。
                    var 时 = 时间管理器.取();
                    if (时 == null) { Debug.LogWarning("[任务] 推进天数：场景里没有 时间管理器"); return; }
                    int 前 = 时.当前天数;
                    时.推进(1f);
                    Debug.Log("[任务] 调度：时间推进 1 天 → 第 " + 时.当前天数 + " 天（原第 " + 前 + " 天）");
                    return;
                }
            case 任务动作.镜头回玩家:  StartCoroutine(镜头回玩家(阶段.镜头时长, 阶段.镜头高度)); return;
            case 任务动作.黑幕字幕:    播黑幕(阶段); return;
            case 任务动作.闪白:        黑幕字幕.闪白(); Debug.Log("[任务] 调度：白屏闪一下"); return;
            case 任务动作.落黑幕:
                {
                    // ★ 只落黑幕（一般不打字）+ 登记玩家落点。
                    //   四幕：和仙人说完话 → 淡入黑幕 → **黑屏状态下**切场景，
                    //   这样切换那一帧不闪、进场景后主角被拉到策划落点的拉扯也看不见。
                    //   等待秒 = 等它淡完（淡入档数 ÷ 淡步频），随后阶段自动完成往下走。
                    var 行 = 拆台词(阶段.台词);
                    演出中 = true;                       // 起幕期间不推进阶段（幕收起时会清）
                    int 档 = 取淡入档数(阶段);
                    // 这次是"只要黑幕、不要自动收"，所以等待秒一律给 0：
                    // 幕由后续阶段（打字的那个）自己定时收。
                    黑幕字幕.下落并定时收起(0f, 行.ToArray(), 阶段.打字速度, 档, 0);
                    if (阶段.坐标 != Vector3.zero) 请求落点(阶段.坐标);
                    Debug.Log("[任务] 调度：落黑幕（无字）淡入 " + 档 + " 档 ≈ "
                        + 黑幕字幕.档数换算秒(档).ToString("F1") + "s"
                        + "，等待秒 " + 阶段.等待秒.ToString("F2") + "s"
                        + (阶段.坐标 != Vector3.zero ? "｜同时登记落点 " + 阶段.坐标.ToString("F2") : ""));
                    return;
                }
            case 任务动作.播放对话:    StartCoroutine(播对话(阶段)); return;
            // ★ 落点不走协程：切场景时协程会随旧场景一起被销毁（P1 的根因），
            //   改成登记一个静态目标，由活着的那份管理器在 Update 里按住若干帧。
            case 任务动作.移动玩家:    请求落点(阶段.坐标); return;
            case 任务动作.移动玩家且黑幕:
                {
                    // ★ 四幕专用：先落黑幕、**同一帧**登记落点，用黑幕盖住
                    //   「切到宗门 → 主角被拉到策划落点」这段拉扯（用户要求看不到）。
                    //
                    // 收幕**不能靠协程**：这个动作是在古古镇执行的，随后立刻切场景，
                    // 协程会随旧场景销毁 → 没人收幕 → 一直黑着。
                    // 所以交给黑幕自己收（见 黑幕字幕.下落并定时收起）。
                    //
                    // 观感参数（**全部来自任务表**，改表即可，不用动代码）：
                    //   · 等待秒     = 字打完之后再停多久（表里填 2）
                    //   · 淡入档数   = 阶梯淡入档数（24 档 ÷ 淡步频 12 ≈ 2 秒；空 = 默认 24）
                    //   · 淡出档数   = 同上
                    //   · 打字速度   = 每秒几个字（空 = 用组件默认 18）
                    var 行 = 拆台词(阶段.台词);
                    演出中 = true;                       // 这一刻起不推进阶段（静态，跨场景有效）
                    黑幕字幕.下落并定时收起(阶段.等待秒, 行.ToArray(), 阶段.打字速度,
                                             取淡入档数(阶段), 取淡出档数(阶段));
                    请求落点(阶段.坐标, 0.6f);          // 接管时间也收短：黑幕一收就该放权
                    Debug.Log("[任务] 调度：黑幕遮罩 + 移动玩家 → " + 阶段.坐标.ToString("F2")
                        + "｜停留 " + 阶段.等待秒.ToString("F2") + "s"
                        + "｜淡入 " + 取淡入档数(阶段) + " / 淡出 " + 取淡出档数(阶段) + " 档"
                        + "｜打字 " + (阶段.打字速度 > 0f ? 阶段.打字速度.ToString("F0") + " 字/秒" : "默认速度"));
                    return;
                }
            case 任务动作.切换场景:    StartCoroutine(切到场景(阶段)); return;
            // ★ 必须在「先找目标 NPC」之前处理：它的整个意义就是目标可能已经不在场了
            case 任务动作.确保NPC:     确保NPC(阶段); return;
            case 任务动作.接取任务:
                if (string.IsNullOrEmpty(阶段.动作参数)) { Debug.LogWarning("[任务] 接取任务没填「动作参数」= 要接的任务id", 阶段); return; }
                接取(阶段.动作参数);
                return;
        }

        var npc = 找NPC(阶段.动作目标npcId);
        if (npc == null)
        {
            Debug.LogWarning("[任务] 动作的目标 NPC 不在场景里：" + 阶段.动作目标npcId, 阶段);
            return;
        }

        switch (阶段.动作)
        {
            case 任务动作.播动画:
                {
                    var 动画 = npc.GetComponent<NpcAnimator>();
                    if (动画 != null) { 动画.Play(阶段.动作参数); }
                    else
                    {
                        var a = npc.GetComponent<Animator>();
                        if (a != null && !string.IsNullOrEmpty(阶段.动作参数)) a.CrossFade(阶段.动作参数, 0.15f);
                    }
                    Debug.Log("[任务] 调度：" + npc.name + " 播动画「" + 阶段.动作参数 + "」");
                    break;
                }
            case 任务动作.攻击玩家:
                {
                    // 靠好感度翻脸：低于「敌对好感阈值」→ 视玩家为敌 → 自动锁定玩家开打
                    var 实例 = npc.GetComponent<NpcInstance>();
                    if (实例 != null) 实例.改变好感度(-999f);
                    Debug.Log("[任务] 调度：" + npc.name + " 把玩家当成了敌人");
                    break;
                }
            case 任务动作.处决:
                {
                    // 直接打死（三幕大师兄一刀劈野猪）：TakeDamage(伤害, 是否已减免)
                    var 实例 = npc.GetComponent<NpcInstance>();
                    if (实例 != null) 实例.TakeDamage(9999999f, false);
                    Debug.Log("[任务] 调度：处决 " + npc.name);
                    break;
                }
            case 任务动作.走向:
                Debug.Log("[任务] 调度：" + npc.name + " 走向 " + 阶段.坐标);
                StartCoroutine(走过去(npc, 阶段.坐标, 阶段.动作速度));
                break;
            case 任务动作.飞到:
                {
                    // 「坐标」留空(0,0,0) = **飞向玩家当时所在的位置**，而不是飞回原点。
                    // 四幕：大师兄禀报完要飞回主角身边，用写死坐标会因为主角走动而飞错地方。
                    var 位 = 阶段.坐标;
                    if (位 == Vector3.zero)
                    {
                        var 玩家 = 物品使用器.取玩家物体();
                        if (玩家 != null)
                        {
                            var p = 玩家.transform.position;
                            var 朝向 = 玩家.transform.forward;
                            位 = new Vector3(p.x + 朝向.x * 2f, p.y, p.z + 朝向.z * 2f);   // 停在玩家身前 2 米
                            Debug.Log("[任务] " + npc.name + " 飞向玩家实时位置 " + 位.ToString("F2"));
                        }
                    }
                    Debug.Log("[任务] 调度：" + npc.name + " 飞到 " + 位.ToString("F2"));
                    // 记下落点：下一阶段若用「条件=NPC到位」，判定基准就是它
                    if (!string.IsNullOrEmpty(阶段.动作目标npcId)) NPC落点[阶段.动作目标npcId] = 位;
                    // 这里**不阻塞**阶段推进（执行动作是同步流程，没法 yield）；
                    // 「到位之后才播对话」由下一阶段的 `条件=NPC到位` 保证。
                    StartCoroutine(飞过去(npc, 位, 阶段.动作速度, 阶段.动作参数));
                }
                break;
            case 任务动作.镜头看目标:
                Debug.Log("[任务] 调度：镜头对焦 " + (npc != null ? npc.name : "null"));
                StartCoroutine(镜头对焦(npc != null ? npc.transform : null, 阶段.镜头时长, 阶段.镜头高度));
                break;
            case 任务动作.镜头回玩家:
                Debug.Log("[任务] 调度：镜头回玩家");
                StartCoroutine(镜头回玩家(阶段.镜头时长, 阶段.镜头高度));
                break;
            case 任务动作.生成NPC:
                生成NPC(阶段);
                break;
            case 任务动作.销毁:
                Debug.Log("[任务] 调度：销毁 " + npc.name);
                Destroy(npc.gameObject);
                break;
        }
    }

    // ================================================================ 黑幕参数默认值
    //
    // 全部可由**任务表**覆盖（列：淡入档数 / 淡出档数 / 打字速度；停留时间用 等待秒）。
    // 留空或 0 = 用这里的默认值。
    //
    // 为什么"空 = 用默认"而不是"空 = 0"：这几个值 0 在语义上没有意义
    // （0 档 = 不淡 = 就是原来的瞬间全黑，那个用 1 档也能表达），
    // 所以拿 0 当"没填"的哨兵值，避免策划不填就被清成 0。

    /// <summary>黑幕默认阶梯淡入档数（÷ 黑幕字幕.淡步频(12) ≈ 秒数）</summary>
    public const int 默认淡入档数 = 24;
    /// <summary>黑幕默认阶梯淡出档数</summary>
    public const int 默认淡出档数 = 24;

    /// <summary>
    /// 「不收幕」的黑幕（等待秒=0，留给后续切场景用）在**字全部打完之后**还要停多久才放行。
    /// 用户 2026-09-27 定：**2 秒**（"最后一个字打完马上转场，没有等 2 秒"）。
    /// </summary>
    public const float 黑幕打完停留 = 2f;

    static int 取淡入档数(QuestDefinition 阶段)
        => 阶段.淡入档数 > 0 ? 阶段.淡入档数 : 默认淡入档数;

    static int 取淡出档数(QuestDefinition 阶段)
        => 阶段.淡出档数 > 0 ? 阶段.淡出档数 : 默认淡出档数;

    /// <summary>黑幕白字：台词列按 | 拆行，逐字打出（左键长按加速）后**由黑幕自己**定时收幕。
    ///
    /// 停留时长 = 任务表的「等待秒」；打完字后再停这么久，等不及可以点一下左键跳过。
    /// 淡入/淡出档数与打字速度也都取自任务表（见上面的默认值）。
    ///
    /// ★ 为什么不用协程自己等：黑幕是 `DontDestroyOnLoad` 的、而任务管理器随场景重建，
    ///   协程一旦跨场景就没了（"换场景后一直黑着"的老 bug）。计时挂在黑幕自己身上才杀不掉。</summary>
    void 播黑幕(QuestDefinition 阶段)
    {
        var 行 = 拆台词(阶段.台词);

        // 没台词也允许当**纯黑幕遮罩**用（例如只想挡住落点拉扯）：落幕 → 停「等待秒」→ 收幕
        if (行.Count == 0 && 阶段.等待秒 <= 0f)
        {
            // ⚠️ 这里**不能**设「演出中 = true」。
            //   「演出中」只有黑幕自己收幕时才会清（黑幕字幕.Update → 清演出中），
            //   而这条分支压根不起幕 → 一旦置真就**永远没人清** → 任务管理器
            //   每帧 `if (演出中) return` → 阶段再也不推进 = 卡死。
            Debug.Log("[任务] 黑幕字幕：既没台词也没等待秒，跳过（不置演出中，避免卡死）");
            return;
        }

        if (DialogueUI.正在显示)
        {
            Debug.Log("[黑幕时序] 进入播黑幕时对话框还在显示 → 先关掉它再落黑幕");
            DialogueUI.关闭();
        }

        演出中 = true;      // 从这一刻起不推进阶段（黑幕收掉时它自己会清）

        // ★ 等待秒 = 0 表示「**打完字就停在全黑，不要收幕**」——
        //   给"紧接着要切场景"的场合用：幕原地留给切场景过渡接着用，alpha 不掉，
        //   于是两段黑幕是连续的（用户报过"中间断开一下"就是这个原因）。
        //   进度靠 条件=黑幕落下完成 推进；**打完字后还要再停 黑幕打完停留 秒**才放行
        //   ——用户 2026-09-27 要求："最后一个字打完马上转场，没有等 2 秒"。
        if (阶段.等待秒 <= 0f && 行.Count > 0)
        {
            Debug.Log("[任务] 调度：黑幕 " + 行.Count + " 行｜打完**不收幕**，再停 "
                + 黑幕打完停留.ToString("F1") + "s 才放行（留给后续，如切场景）");
            黑幕字幕.下落并停留不收起(行.ToArray(), 阶段.打字速度, 取淡入档数(阶段), 黑幕打完停留);
            return;
        }

        Debug.Log("[任务] 调度：黑幕 " + 行.Count + " 行｜停留 " + 阶段.等待秒.ToString("F2") + "s"
            + "｜淡入 " + 取淡入档数(阶段) + " 档 / 淡出 " + 取淡出档数(阶段) + " 档"
            + "｜打字 " + (阶段.打字速度 > 0f ? 阶段.打字速度.ToString("F0") + " 字/秒" : "默认速度"));

        黑幕字幕.下落并定时收起(阶段.等待秒, 行.ToArray(), 阶段.打字速度,
                                 取淡入档数(阶段), 取淡出档数(阶段));
    }

    /// <summary>台词列按 | 拆成多行（黑幕与强制对话共用）</summary>
    static List<string> 拆台词(string 台词)
    {
        var 行 = new List<string>();
        if (!string.IsNullOrEmpty(台词))
            foreach (var s in 台词.Split('|'))
                if (!string.IsNullOrWhiteSpace(s)) 行.Add(s.Trim());
        return 行;
    }

    /// <summary>强制对话（不用玩家按 F）：说话人 / 台词 / 情绪 全从阶段里取。
    /// 台词含 `|` 时按顺序**逐句**弹（见 DialogueUI.演出多行）。</summary>
    System.Collections.IEnumerator 播对话(QuestDefinition 阶段)
    {
        演出中 = true;      // ★ 对话期间不推进阶段（三幕「啊啊啊是妖怪」之后才该轮到野猪攻击）
        var 行 = 拆台词(阶段.台词);
        if (行.Count > 1)
        {
            Debug.Log("[任务] 调度：强制对话（" + 阶段.说话人 + "）共 " + 行.Count + " 句（逐句演出）");
            yield return DialogueUI.演出多行(阶段.说话人, 行.ToArray(), 阶段.情绪, 阶段.情绪强度);
        }
        else
        {
            Debug.Log("[任务] 调度：强制对话（" + 阶段.说话人 + "）「" + 阶段.台词 + "」情绪=" + 阶段.情绪);
            yield return DialogueUI.演出(阶段.说话人, 阶段.台词, 阶段.情绪, 阶段.情绪强度);
        }
        演出中 = false;
    }

    /// <summary>
    /// 【已废弃】原来是协程版落点，切场景时协程会随旧场景销毁，实测玩家停在场景初始摆放处。
    /// 现在走 <see cref="请求落点"/> + Update 的静态方案（跨场景安全）。
    /// 保留这个签名只为兼容可能的旧引用，不要再用它做落点。
    /// </summary>
    [System.Obsolete("落点改用 请求落点()，协程版在切场景时会被销毁")]
    System.Collections.IEnumerator 移动玩家到(Vector3 位)
    {
        请求落点(位);
        yield break;
    }

    /// <summary>
    /// **切场景过渡** —— 实际的加载/摆位/收幕由 `黑幕字幕` 自己每帧推进
    /// （见 <see cref="黑幕字幕.开始场景过渡"/> 的说明：协程挂在旧场景上会被销毁）。
    ///
    /// 这里只负责：落黑幕+场景名 → 推进进度、钉快照 → 交出控制权。
    /// </summary>
    System.Collections.IEnumerator 切到场景(QuestDefinition 阶段)
    {
        if (string.IsNullOrEmpty(阶段.场景名)) { Debug.LogWarning("[任务] 切换场景没填「场景名」", 阶段); yield break; }

        演出中 = true;      // 过渡期间不推进阶段（过渡自己会在收幕完成后清）

        // ① 黑幕 + 场景名（第一步，必须在加载之前）—— 并把后续交给黑幕自己跑
        黑幕字幕.开始场景过渡(阶段.场景名, 取场景显示名(阶段.场景名), 阶段.坐标);

        // ② 进度推进 + 钉快照（旧场景马上要卸载，导出的必须是"下一阶段"）
        当前阶段[阶段.任务id] = 阶段.阶段 + 1;
        跨场景待发动作.Add(阶段.任务id);
        准备切场景();
        跨场景数据.钉住任务进度(导出进度());

        yield break;
    }

    /// <summary>切场景时「场景名」至少停留多久（从打出场景名算起）</summary>
    public const float 场景名最短停留 = 2f;

    /// <summary>
    /// 场景 id → 中文显示名（切场景过渡时打在黑幕上的字）。
    /// 键就是 Build Settings 里的场景名；没登记的就把原名打出来（便于发现漏配）。
    /// </summary>
    public static string 取场景显示名(string 场景名)
    {
        switch (场景名)
        {
            case "Village":                 return "古古镇";
            case "Sect":                    return "太虚宗";
            case "3C_Testbed":              return "个人洞府";
            case "Sect_Wilderness":         return "宗门野外";
            case "Demon-Suppressing Tower": return "镇妖塔";
            default:                        return 场景名;
        }
    }

    /// <summary>走过去：走行走状态 + 平移到坐标，到位切回待机</summary>
    System.Collections.IEnumerator 走过去(GameObject npc, Vector3 目标, float 速度)
    {
        if (npc == null) yield break;
        播状态(npc, "Walk");
        var t = npc.transform;
        while (t != null)
        {
            var 平 = new Vector3(目标.x - t.position.x, 0f, 目标.z - t.position.z);
            if (平.magnitude <= 0.15f) break;
            t.position = Vector3.MoveTowards(t.position, new Vector3(目标.x, t.position.y, 目标.z), Mathf.Max(0.1f, 速度) * Time.deltaTime);
            if (平.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(平.normalized), 8f * Time.deltaTime);
            yield return null;
        }
        播状态(npc, "Idle");
    }

    /// <summary>
    /// 御风飞过去：**升空 → 前进（同时位移）→ 到点悬停**。
    /// 前进用的状态名从 <see cref="QuestDefinition.动作参数"/> 取，留空默认 `Yufeng_Forward`。
    /// （大师兄/村民这些同骨骼 NPC 都加了这几个状态，见 大师兄.controller）
    /// </summary>
    System.Collections.IEnumerator 飞过去(GameObject npc, Vector3 目标, float 速度, string 前进状态)
    {
        if (npc == null) yield break;
        string 前进 = string.IsNullOrEmpty(前进状态) ? "Yufeng_Forward" : 前进状态;
        播状态(npc, "Yufeng_TakeOff");
        yield return new WaitForSeconds(0.45f);
        播状态(npc, 前进);

        var t = npc.transform;
        while (t != null)
        {
            var 差 = 目标 - t.position;
            if (差.magnitude <= 0.2f) break;
            t.position = Vector3.MoveTowards(t.position, 目标, Mathf.Max(0.1f, 速度) * Time.deltaTime);
            var 平 = new Vector3(差.x, 0f, 差.z);
            if (平.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.Slerp(t.rotation, Quaternion.LookRotation(平.normalized), 8f * Time.deltaTime);
            yield return null;
        }
        播状态(npc, "Yufeng_Idle");
    }

    /// <summary>给 NPC 切动画状态（找不到那个状态时 Unity 只会打警告，不会崩）</summary>
    static void 播状态(GameObject npc, string 状态)
    {
        if (npc == null || string.IsNullOrEmpty(状态)) return;
        var a = npc.GetComponent<Animator>();
        if (a != null) a.CrossFade(状态, 0.2f);
    }

    // ================================================================ 镜头接管（演出用）
    //
    // 场景里的主相机是 `Main Camera`，挂的是 **TopDownCamera**（俯视跟随）+ TopDownCameraZoom。
    // 所以演出期间要**先把它们停掉**，否则每帧都会把镜头抢回玩家身上 ✗；演完再交还。

    readonly System.Collections.Generic.List<Behaviour> 被停的相机脚本 = new System.Collections.Generic.List<Behaviour>();

    [Tooltip("镜头兜底交还的时限（秒）：接管后最多霸占这么久，超时自动还给跟随脚本。\n" +
             "防的是「演出协程被打断（切场景 / 对象销毁 / 异常）→ 镜头一直被劫持」这种死法")]
    public float 镜头兜底上限 = 30f;

    float 镜头兜底交还时间 = -1f;

    void 接管相机()
    {
        if (被停的相机脚本.Count > 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        foreach (var b in cam.GetComponents<Behaviour>())
        {
            if (b == null || !b.enabled) continue;
            string n = b.GetType().Name;
            if (n.Contains("TopDownCamera") || n.Contains("CameraZoom")) { b.enabled = false; 被停的相机脚本.Add(b); }
        }
        if (被停的相机脚本.Count > 0)
        {
            镜头兜底交还时间 = Time.time + Mathf.Max(1f, 镜头兜底上限);
            Debug.Log("[任务] 镜头接管：停了 " + 被停的相机脚本.Count + " 个跟随脚本（兜底 " + 镜头兜底上限 + "s）");
        }
    }

    void 交还相机()
    {
        foreach (var b in 被停的相机脚本) if (b != null) b.enabled = true;
        if (被停的相机脚本.Count > 0) Debug.Log("[任务] 镜头交还：" + 被停的相机脚本.Count + " 个跟随脚本恢复");
        被停的相机脚本.Clear();
        镜头兜底交还时间 = -1f;
    }

    /// <summary>把镜头平滑推到某个目标身上（停在它的斜后方，略俯视）</summary>
    System.Collections.IEnumerator 镜头对焦(Transform 目标, float 时长, float 高度)
    {
        var cam = Camera.main;
        if (cam == null || 目标 == null) { 交还相机(); yield break; }
        接管相机();

        Vector3 起 = cam.transform.position;
        Quaternion 起转 = cam.transform.rotation;
        Vector3 看向 = 目标.position + Vector3.up * 1.0f;
        Vector3 终 = 目标.position + new Vector3(0f, Mathf.Max(1.0f, 高度) + 1.2f, -3.2f);
        Quaternion 终转 = Quaternion.LookRotation(看向 - 终, Vector3.up);
        yield return 推镜头(cam, 起, 终, 起转, 终转, 时长);
    }

    /// <summary>镜头回到玩家身上（然后交还给跟随脚本）</summary>
    System.Collections.IEnumerator 镜头回玩家(float 时长, float 高度)
    {
        var cam = Camera.main;
        var 玩家 = 物品使用器.取玩家物体();
        if (cam == null || 玩家 == null) { 交还相机(); yield break; }
        接管相机();

        Vector3 起 = cam.transform.position;
        Quaternion 起转 = cam.transform.rotation;
        Vector3 看向 = 玩家.transform.position + Vector3.up * 1.0f;
        Vector3 终 = 玩家.transform.position + new Vector3(0f, Mathf.Max(1.0f, 高度) + 1.2f, -3.2f);
        Quaternion 终转 = Quaternion.LookRotation(看向 - 终, Vector3.up);
        yield return 推镜头(cam, 起, 终, 起转, 终转, 时长);
        交还相机();          // 回玩家之后把控制权还给 TopDownCamera
    }

    static System.Collections.IEnumerator 推镜头(Camera cam, Vector3 起, Vector3 终, Quaternion 起转, Quaternion 终转, float 时长)
    {
        float t = 0f;
        float 总 = Mathf.Max(0.05f, 时长);
        while (t < 总)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 总));
            if (cam == null) yield break;
            cam.transform.position = Vector3.Lerp(起, 终, k);
            cam.transform.rotation = Quaternion.Slerp(起转, 终转, k);
            yield return null;
        }
        if (cam != null) { cam.transform.position = 终; cam.transform.rotation = 终转; }
    }

    /// <summary>
    /// 生成一个 NPC（三幕的野猪、大师兄都靠它）。
    /// **动作参数 = 预制体资源路径**（`Assets/resources/` 下、不带扩展名），位置取 `坐标`。
    /// 走 <see cref="NpcPrefabs.加载"/> —— 它内部处理了"同目录有同名 FBX 时 Resources.Load 会挑错"的坑。
    /// </summary>
    /// <summary>
    /// 任务生成过的 NPC：`npcId → 预制体路径`。
    ///
    /// 用途：`生成NPC` 造出来的是**场景内对象**，玩家一切场景它就随旧场景销毁。
    /// 四幕实测：大师兄在第 4 阶段生成，玩家在「等候大师兄禀报」那 60 秒里走进传送门
    /// （Sect → 3C_Testbed → Sect）→ 大师兄没了 → 之后「飞到」因为找不到目标而静默跳过，
    /// 表现就是「大师兄模型不见了、也没飞过来带我走」。
    ///
    /// 记下预制体路径后，任何动作找不到该 NPC 都能就地重建（见 找NPC），
    /// 不需要在任务表里额外插一行。
    /// </summary>
    static readonly Dictionary<string, string> 生成过的NPC = new Dictionary<string, string>();

    /// <summary>NPC 不在场时的兜底生成位置（一般是玩家身边，不至于生成到天边）</summary>
    static Vector3 NPC兜底位置;

    /// <summary>
    /// 最近一次「飞到」给每个 NPC 定的落点：`npcId → 目标位置`。
    /// 「条件=NPC到位」靠它判定 —— 因为 `飞到` 的落点可能是**动态**的
    /// （坐标留空 = 飞向玩家当时的实时位置），事后没法从任务表里读回来。
    /// </summary>
    static readonly Dictionary<string, Vector3> NPC落点 = new Dictionary<string, Vector3>();

    GameObject 生成NPC(QuestDefinition 阶段)
    {
        if (string.IsNullOrEmpty(阶段.动作参数))
        {
            Debug.LogWarning("[任务] 生成NPC 没填预制体路径（填在「动作参数」里，例：NPC/Demon/YeZhu/YeZhu）", 阶段);
            return null;
        }
        var 预制 = NpcPrefabs.加载(阶段.动作参数);
        if (预制 == null)
        {
            Debug.LogWarning("[任务] 生成NPC 载不到预制体：" + 阶段.动作参数, 阶段);
            return null;
        }
        var go = Instantiate(预制, 阶段.坐标, Quaternion.identity);
        go.name = 预制.name + "_任务生成";
        // ★ 记住「这个任务 NPC 是用哪个预制体造的」，供 找NPC 在它被切场景销毁后重建。
        //
        // ⚠️ 不能只看 `动作目标npcId` 有没有填 —— 任务表里 `生成NPC` 那一行**常常只填了
        //    「动作参数」（预制体路径），`动作目标npcId` 是空的**。实测大师兄就是这样：
        //    人是生成了，但**一次都没被记下来** ⇒ `找NPC` 的重建路径从来没生效过 ⇒
        //    玩家一进洞府他就永远消失、洞府那一幕的对话条件永远达不成（2026-10-02 定位）。
        //    所以拿"刚生成出来那个实例的 `定义.id`"兜底。
        var 记实例 = go.GetComponent<NpcInstance>();
        string 记id = !string.IsNullOrEmpty(阶段.动作目标npcId) ? 阶段.动作目标npcId
                      : (记实例 != null && 记实例.定义 != null ? 记实例.定义.id : "");
        if (!string.IsNullOrEmpty(记id)) 生成过的NPC[记id] = 阶段.动作参数;
        else Debug.LogWarning("[任务] 生成NPC：" + 阶段.动作参数
                              + " 既没填「动作目标npcId」、实例上也没有 NpcInstance.定义 → "
                              + "这个 NPC 以后不在场时**没法自动重建**", 阶段);
        NPC兜底位置 = 阶段.坐标;
        Debug.Log("[任务] 生成 " + go.name + " 于 " + 阶段.坐标, 阶段);
        return go;
    }

    GameObject 找NPC(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;
        foreach (var npc in FindObjectsOfType<NpcInstance>())
        {
            if (npc == null) continue;
            if (npc.定义 != null && npc.定义.id == npcId) return npc.gameObject;
        }

        // ★ 不在场：如果这个 NPC 是任务生成过的，就地重建一个。
        //   （切场景会销毁生成出来的 NPC，不重建的话后续「飞到/播动画」全都静默失败）
        string 预制路径;
        if (生成过的NPC.TryGetValue(npcId, out 预制路径))
        {
            var 位 = 兜底位置();
            Debug.LogWarning("[任务] 找NPC：" + npcId + " 不在场（多半被切场景销毁了）→ 就地重建于 " + 位.ToString("F2"));
            var 预制 = NpcPrefabs.加载(预制路径);
            if (预制 != null)
            {
                var go = Instantiate(预制, 位, Quaternion.identity);
                go.name = 预制.name + "_任务生成";
                // 兜底位置更新到「这次重建的地方」，免得反复重建时位置乱跳
                NPC兜底位置 = 位;
                return go;
            }
            Debug.LogWarning("[任务] 找NPC：重建失败，载不到预制体 " + 预制路径);
        }
        return null;
    }

    /// <summary>「NPC到位」兜底提示只报一次（免得每帧刷屏）</summary>
    static bool 报过NPC到位兜底;

    /// <summary>
    /// 重建位置：优先玩家附近（大师兄是回来找玩家的），否则用上次生成点。
    ///
    /// ★ 距离必须 **≤ 到位判定距离(1.5m)**，否则「NPC到位」判不到（踩过的坑 2026-09-27：
    ///   原来放玩家前方 2 米 > 1.5 米阈值 → 重建了也永远判不到位 → 阶段卡死）。
    /// </summary>
    static Vector3 兜底位置()
    {
        var 玩家 = 物品使用器.取玩家物体();
        if (玩家 != null)
        {
            var p = 玩家.transform.position;
            var 朝向 = 玩家.transform.forward;
            float 距 = Mathf.Min(重建距玩家, 到位判定距离 * 0.8f);   // 留点余量，别贴着阈值
            return new Vector3(p.x + 朝向.x * 距, p.y, p.z + 朝向.z * 距);
        }
        return NPC兜底位置;
    }

    /// <summary>任务 NPC 被切场景销毁后，重建时离玩家多远（米）。必须小于 到位判定距离</summary>
    const float 重建距玩家 = 1.2f;

    /// <summary>
    /// 确保目标 NPC 在场：在就什么都不做，不在就按「动作参数」的预制体路径重建到「坐标」。
    ///
    /// 为什么需要：`生成NPC` 生成的是**场景内对象**，玩家一旦切场景它就随旧场景销毁。
    /// 四幕实测：大师兄在第 4 阶段生成，玩家在「等候大师兄禀报」那 60 秒里走进传送门
    /// （Sect → 3C_Testbed → Sect），大师兄就没了 —— 之后第 11/13 阶段的「飞到」
    /// 因为 `找NPC` 返回 null 而静默跳过，表现就是「大师兄模型不见了、也没飞过来带我走」。
    /// </summary>
    GameObject 确保NPC(QuestDefinition 阶段)
    {
        var 已存在 = 找NPC(阶段.动作目标npcId);
        if (已存在 != null)
        {
            Debug.Log("[任务] 确保NPC：" + 阶段.动作目标npcId + " 在场，无需重建");
            return 已存在;
        }
        Debug.LogWarning("[任务] 确保NPC：" + 阶段.动作目标npcId + " 不在场（多半是切场景销毁了）→ 重建");
        return 生成NPC(阶段);
    }

    // ================================================================ 自动接取

    void 自动接取()
    {
        var db = 取库();
        if (db == null) return;
        foreach (var 任务id in db.全部任务id())
        {
            var 首 = db.取阶段(任务id, 1);
            if (首 != null && 首.自动接取) 接取(任务id);
        }
    }
}
