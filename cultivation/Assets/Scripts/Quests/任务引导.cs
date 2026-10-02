using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// **任务引导（主线追踪）** —— 一次做三件事，全部只读 `任务管理器`，不碰任何任务数据：
///
/// | 部分 | 长什么样 | 什么时候出现 |
/// |---|---|---|
/// | **追踪面板** | 屏幕**左侧**一块深色小牌：`主线 · 任务名` / 当前目标 / 一句话说明 / 目标与距离 | 只要有一条**主线**阶段在进行中 |
/// | **头顶感叹号** | 目标（NPC 或地点）**上方**一个金色「！」，**屏幕空间**画的 | 目标在**镜头里**时 |
/// | **边缘箭头** | 屏幕边缘一个金色三角，**朝着目标的方向** | 目标在**镜头外**（含身后）时 |
///
/// ## 一、引导哪个阶段：只看「主线」，多条并跑时取**剧情最靠后**的那一条
///
/// 用户要的是"屏幕左侧显示**当前的主线任务**"。所以：
/// · **只看 `类型 = 主线`** —— 支线/悬赏不进追踪面板（以后要做多线追踪，再加"手动钉一条"）；
/// · 多条主线同时挂着时，取 **`任务id` 最大**的那条：本工程的 id 是按剧情顺序编的
///   （`q_main_001` 村口 → `q_main_004` 拜入太虚宗），越大 = 玩家此刻在演的那一幕。
///
/// ⚠️ **这里踩过两次，别再改回去**：
///   ① 一开始写的是"任务id **最小**的优先"（先来的先做完）—— 实测进洞府后追踪面板
///      一直显示「交一株回春丹」，而玩家在推宗门那一幕；
///   ② 改成"**最近开始**的那条优先"也不行 —— `初入山门` 第 1 阶段是 `自动接取` 的
///      （交一株回春丹给村口老丈，而丹药要靠灵田种出来，玩家会长期挂着它），
///      一进场景它就先 fire 一次「阶段开始」，于是它永远"最近"。
///   根因是同一个：**"在跑的主线"不等于"玩家现在要做的事"**，得按剧情先后挑。
///
/// ## 二、目标从哪来：**先按条件自动推，推不出来才查表**
///
/// 见 <see cref="QuestDefinition.引导npcId"/> 的注释。自动推导覆盖了绝大多数阶段，
/// 所以**加新主线阶段通常一个字都不用填**。
///
/// ## 三、为什么"头顶的感叹号"是**屏幕空间**画的，而不是世界空间牌子
///
/// 世界空间那块牌子（像 `灵田地块牌` 那样）要处理"被树挡住""离远了太大/太小"，
/// 而且**它没法顺势变成屏幕边缘的箭头** —— 那是两套东西。
/// 改成把目标世界坐标**投影到屏幕**之后：
/// · 在镜头里 → 就在目标头顶画「！」（看起来一样是"头顶的感叹号"）；
/// · 出画面 / 到身后 → 同一个目标变成屏幕边缘的箭头，**方向天然是对的**。
/// 一套投影同时满足两个需求，还**不往场景里塞任何物件**（没有场景漂移、不用收拾）。
///
/// ## 挂载
///
/// 由 `场景自举` 自动补到**主相机**上（和 `纪年HUD` 一样）——
/// UI 是"每个场景一份"的重灾区，绝不能靠手工往场景里摆。
/// </summary>
[DisallowMultipleComponent]
public class 任务引导 : MonoBehaviour
{
    // ============================================================ 可调
    [Header("字体")]
    [Tooltip("中文字体。留空自动找 SimHei")]
    public Font 字体;

    [Header("追踪面板（屏幕左侧）")]
    [Tooltip("面板离屏幕左边的距离（像素，1080p 参考分辨率下）")]
    public float 左边缘 = 26f;
    [Tooltip("面板离屏幕**中心**的竖直偏移（像素）。0 = 正中间")]
    public float 竖直偏移 = 0f;
    [Tooltip("面板宽度")]
    public float 面板宽度 = 400f;
    public Color 底板色 = new Color(0.05f, 0.06f, 0.08f, 0.74f);
    public Color 标题色 = new Color(1f, 0.84f, 0.42f, 1f);     // 金：任务名
    public Color 目标色 = new Color(0.97f, 0.96f, 0.92f, 1f);   // 白：当前目标
    public Color 说明色 = new Color(0.72f, 0.73f, 0.70f, 1f);   // 灰：说明
    public Color 距离色 = new Color(0.85f, 0.78f, 0.55f, 1f);   // 淡金：目标与距离
    public int 标题字号 = 25;
    public int 目标字号 = 24;
    public int 说明字号 = 18;
    public int 距离字号 = 18;

    [Header("头顶感叹号 / 边缘箭头")]
    [Tooltip("感叹号画在目标头顶再高多少米")]
    public float 头顶抬高 = 0.5f;
    [Tooltip("目标离玩家近于这个距离就不画标记了（就在眼前了，别挡视线）")]
    public float 近处不画 = 2.2f;
    [Tooltip("箭头离屏幕边缘留多少像素（太小会被刘海/边框切掉）")]
    public float 边缘留白 = 78f;
    public float 叹号大小 = 52f;
    public float 箭头大小 = 44f;
    public Color 标记色 = new Color(1f, 0.82f, 0.2f, 1f);
    public Color 描边色 = new Color(0f, 0f, 0f, 0.85f);

    [Header("节奏")]
    [Tooltip("多久重新解析一次目标（NPC 会走会死会生成，但不必每帧全场景找）")]
    public float 解析间隔 = 0.4f;

    [Header("调试")]
    [Tooltip("勾上把每次「当前引导目标」的变化打进 Console")]
    public bool 打印日志 = false;

    // ============================================================ 状态
    Canvas 画布;
    RectTransform 画布根, 标记层, 面板;
    Text 标题文本, 目标文本, 说明文本, 距离文本;
    RectTransform 叹号, 箭头;

    /// <summary>当前正在引导的主线阶段（null = 没有主线在跑）</summary>
    QuestDefinition 当前阶段;

    /// <summary>阶段变了要重算（事件驱动，别每帧扫任务列表 —— 那会每帧分配一个 List）</summary>
    bool 需要重算 = true;

    /// <summary>解析出来的目标（每 <see cref="解析间隔"/> 秒刷一次）</summary>
    目标 当前目标;
    float 下次解析;

    /// <summary>演出/对话标记疑似卡住的起始时刻（-1 = 没在计时）</summary>
    float 卡住起始时刻 = -1f;

    /// <summary>容忍多久之后不再相信"演出 / 对话"标记（秒）—— 见 Update 里的兜底自愈</summary>
    const float 卡住容忍秒 = 8f;

    /// <summary>玩家（每个场景一份组件，缓存一次就够）</summary>
    Transform 玩家;

    /// <summary>上一次打印过的目标描述（只在变化时打日志）</summary>
    string 上次日志 = "";

    static Sprite 三角缓存;

    // ============================================================ 生命周期

    void Awake()
    {
        取默认字体();
        搭界面();
        任务管理器.阶段开始 += 处理阶段变化;
        任务管理器.阶段完成 += 处理阶段变化;
        任务管理器.任务全部完成 += 处理任务结束;
    }

    void OnDestroy()
    {
        任务管理器.阶段开始 -= 处理阶段变化;
        任务管理器.阶段完成 -= 处理阶段变化;
        任务管理器.任务全部完成 -= 处理任务结束;
    }

    void 处理阶段变化(QuestDefinition 阶段) { 需要重算 = true; }
    void 处理任务结束(string 任务id) { 需要重算 = true; }

    void Update()
    {
        if (玩家 == null) 玩家 = 找玩家();

        // ★ 重算必须放在**可见性判断之前** —— 否则开局 `当前阶段` 是 null、
        //   可见性为 false、直接 return，`当前阶段` 就永远是 null（面板永远不出现）。
        //   实测踩过：把重算挪到 hide 分支后面 → 面板一个字都不显示。
        if (需要重算 || Time.unscaledTime >= 下次解析)
        {
            下次解析 = Time.unscaledTime + Mathf.Max(0.05f, 解析间隔);
            需要重算 = false;

            // ★ **每 0.4 秒重新挑一次**，不能只靠 `阶段开始/阶段完成` 事件。
            //   踩过的坑（2026-10-02）：读档和"跨场景接手上一场景的进度"走的是
            //   `任务管理器.导入进度()`，它**故意不发事件**（读档不能重放剧情动作），
            //   于是只订阅事件的话，追踪面板会一直显示进场景那一刻挑中的那条 ——
            //   实测：进度明明是 `q_main_004:27`，面板却一直挂着 `q_main_001` 的「交一株回春丹」。
            //   自检一遍的成本是每 0.4 秒一个很小的 List，完全可以接受。
            var 新 = 选当前阶段();
            if (新 != 当前阶段)
            {
                当前阶段 = 新;
                if (打印日志) Debug.Log("[任务引导] 当前引导 → "
                    + (新 != null ? 新.任务id + " 阶段" + 新.阶段 + "「" + 新.阶段名 + "」" : "（没有主线在跑）"), 新);
            }

            当前目标 = 解析目标(当前阶段);
        }

        // 演出 / 对话期间整块藏起来（过场时屏幕左侧挂着一块任务牌很跳）
        bool 演出标记 = 黑幕字幕.演出中;
        bool 对话标记 = DialogueUI.正在显示;
        bool 该显示 = 当前阶段 != null && !演出标记 && !对话标记;

        // ★ **兜底自愈（用户 2026-10-02 报的"对完话面板和箭头被删了"）**：
        //   `演出中` / `正在显示` 这类**静态标记**被中断时会一直留着 true
        //   （同类坑见 [踩坑总库 B84]）—— 标记说"还在演"，可两块界面其实都不在屏幕上，
        //   于是面板和两个标记就**永远不再出现**，看起来就像被删掉了。
        //   判据用"界面真的在不在"（`有幕在显示`）而不是再信一次标记：
        //   只要"有幕在显示 / 对话在显示"都为假、而标记为真，连续 8 秒就强制显示。
        if (该显示 || 当前阶段 == null || 对话标记 || 黑幕字幕.有幕在显示)
        {
            卡住起始时刻 = -1f;
        }
        else
        {
            if (卡住起始时刻 < 0f) 卡住起始时刻 = Time.unscaledTime;
            else if (Time.unscaledTime - 卡住起始时刻 > 卡住容忍秒)
            {
                卡住起始时刻 = -1f;
                该显示 = true;
                Debug.LogWarning("[任务引导] 演出/对话标记疑似卡住（幕与对话框都不在屏幕上，已连续 "
                    + 卡住容忍秒 + " 秒）→ 强制显示追踪面板", this);
            }
        }

        if (面板 != null && 面板.gameObject.activeSelf != 该显示)
        {
            面板.gameObject.SetActive(该显示);
            // 【诊断】面板一隐藏就把**原因**写进 Console。
            //   用户 2026-10-02 报"对完话左边面板和箭头就没了" —— 这句话能直接区分两种原因：
            //   `当前阶段=null`（那一刻没有主线在跑，比如对话正好把整条任务做完）还是
            //   `演出中/对话中=True`（标记卡住，见下面那段兜底自愈）。
            if (!该显示)
                Debug.Log("[任务引导] 追踪面板隐藏："
                    + (当前阶段 == null ? "当前没有主线在跑（当前阶段=null）" : "")
                    + (演出标记 ? " 演出中=True" : "")
                    + (对话标记 ? " 对话中=True" : ""), this);
        }
        if (!该显示) { 隐藏两个标记(); return; }

        刷新面板文字();
        刷新标记();
    }

    // ============================================================ 选哪一条主线

    QuestDefinition 选当前阶段()
    {
        var 任务 = 任务管理器.实例;
        if (任务 == null) return null;

        // 取 **任务id 最大**的那条主线（见类头 §一：踩过"最小优先"和"最近开始"两次）
        QuestDefinition 最好 = null;
        var 在跑 = 任务.进行中的阶段();
        for (int i = 0; i < 在跑.Count; i++)
        {
            var q = 在跑[i];
            if (q == null || q.类型 != 任务类型.主线) continue;
            if (最好 == null || string.CompareOrdinal(q.任务id, 最好.任务id) > 0) 最好 = q;
        }
        return 最好;
    }

    // ============================================================ 解析目标

    /// <summary>引导目标（可能"有名字但不在本场景"）</summary>
    struct 目标
    {
        public bool 有;          // 有没有**本场景里**能指出来的点（有才能画标记）
        public bool 有名字;      // 至少知道要找谁/去哪（面板能写出来）
        public string 名字;      // 显示用的名字
        public string 场景;      // 目标所在场景名（空 = 本场景）
        public Vector3 世界点;   // 标记画在哪
    }

    目标 解析目标(QuestDefinition q)
    {
        var 出 = new 目标();
        if (q == null) return 出;

        string 本场景 = SceneManager.GetActiveScene().name;

        // ---- ① NPC：表里显式填的 > 条件自动推 ----
        string npcId = !string.IsNullOrWhiteSpace(q.引导npcId) ? q.引导npcId : 自动npcId(q);
        if (!string.IsNullOrWhiteSpace(npcId))
        {
            出.有名字 = true;
            出.名字 = 取NPC名(npcId);
            出.场景 = "";
            var npc = 找NPC(npcId);
            if (npc != null)
            {
                出.有 = true;
                出.世界点 = npc.transform.position + Vector3.up * (取身高(npc) + 头顶抬高);
            }
            // NPC 不在本场景：名字照样显示（面板写"去找 X"），但不画标记
            return 出;
        }

        // ---- ② 地点 ----
        Vector3 点;
        bool 有地点 = q.试解析引导坐标(out 点);
        if (!有地点 && q.条件 == 任务条件.到达) { 点 = q.坐标; 有地点 = true; }
        if (!有地点) return 出;   // 没有具体目标：面板只显示阶段名 + 说明

        出.有名字 = true;
        出.名字 = string.IsNullOrWhiteSpace(q.引导地点名) ? "目的地" : q.引导地点名;
        出.场景 = q.引导场景 ?? "";

        // ★ 只在**同一个场景**里才画标记：两个场景的坐标是两套空间，
        //   在洞府里拿宗门的坐标画箭头，会指到一个毫不相干的方向（实测很容易被当成"引导坏了"）
        if (!string.IsNullOrEmpty(出.场景) && 出.场景 != 本场景) return 出;

        出.有 = true;
        出.世界点 = 点 + Vector3.up * 头顶抬高;
        return 出;
    }

    /// <summary>按「条件」自动推要去找的那个 NPC（推不出来返回空串）</summary>
    static string 自动npcId(QuestDefinition q)
    {
        if (q.条件 == 任务条件.对话 || q.条件 == 任务条件.击杀 || q.条件 == 任务条件.NPC到位)
        {
            if (!string.IsNullOrWhiteSpace(q.目标npcId)) return q.目标npcId;
            // 条件=对话 常常只填了 `对话id`（说话的 NPC 写在对话表里）→ 反查一次
            if (q.条件 == 任务条件.对话)
            {
                var 库 = DialogueDatabase.取();
                if (库 != null) return 库.取NpcId(q.对话id);
            }
        }
        return "";
    }

    static NpcInstance 找NPC(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return null;
        var 全部 = Object.FindObjectsOfType<NpcInstance>();
        for (int i = 0; i < 全部.Length; i++)
        {
            var n = 全部[i];
            if (n == null || n.定义 == null) continue;
            if (n.定义.id != npcId) continue;
            if (n.IsDead) continue;                 // 死了的别指
            return n;
        }
        return null;
    }

    /// <summary>NPC 有多高（用来把感叹号顶到头上，别插进身体里）</summary>
    static float 取身高(NpcInstance n)
    {
        var r = n.GetComponentInChildren<Renderer>();
        if (r != null) return Mathf.Max(1.2f, r.bounds.max.y - n.transform.position.y);
        var c = n.GetComponentInChildren<Collider>();
        if (c != null) return Mathf.Max(1.2f, c.bounds.max.y - n.transform.position.y);
        return 1.8f;
    }

    /// <summary>NPC 不在场景里时也能显示个名字（走 NPC库 反查）</summary>
    static string 取NPC名(string npcId)
    {
        var 库 = NpcDatabase库.取();
        if (库 != null)
        {
            var 定义 = 库.按id(npcId);
            if (定义 != null) return 定义.DisplayName;
        }
        return npcId;
    }

    // ============================================================ 面板

    void 刷新面板文字()
    {
        var q = 当前阶段;
        if (q == null) return;

        if (标题文本 != null)
            标题文本.text = "主线 · " + (string.IsNullOrEmpty(q.任务名) ? q.任务id : q.任务名);
        if (目标文本 != null) 目标文本.text = q.阶段名;
        if (说明文本 != null)
        {
            bool 有说明 = !string.IsNullOrWhiteSpace(q.说明);
            说明文本.text = 有说明 ? q.说明 : "";
            if (说明文本.gameObject.activeSelf != 有说明) 说明文本.gameObject.SetActive(有说明);
        }

        if (距离文本 != null)
        {
            var 目 = 当前目标;
            string s;
            if (!目.有名字) s = "（按任务说明进行）";
            else if (!目.有 && !string.IsNullOrEmpty(目.场景)) s = "目标：" + 目.名字 + "（在「" + 目.场景 + "」）";
            else if (!目.有) s = "目标：去找「" + 目.名字 + "」";
            else if (玩家 == null) s = "目标：" + 目.名字;
            else
            {
                float 距离 = Vector3.Distance(玩家.position, 目.世界点);
                s = "目标：" + 目.名字 + "　" + (距离 >= 10f ? 距离.ToString("0") : 距离.ToString("0.#")) + " 米";
            }
            if (距离文本.text != s) 距离文本.text = s;
        }

        if (打印日志)
        {
            string 记 = q.任务id + " 阶段" + q.阶段 + "「" + q.阶段名 + "」→ "
                       + (当前目标.有 ? 当前目标.名字 + " " + 当前目标.世界点.ToString("F1")
                          : (当前目标.有名字 ? 当前目标.名字 + "（不在本场景）" : "无标记"));
            if (记 != 上次日志) { 上次日志 = 记; Debug.Log("[任务引导] " + 记, q); }
        }
    }

    // ============================================================ 标记（投影）

    void 刷新标记()
    {
        var cam = Camera.main;
        var 目 = 当前目标;
        if (cam == null || 画布根 == null || !目.有) { 隐藏两个标记(); return; }
        if (玩家 != null && Vector3.Distance(玩家.position, 目.世界点) < 近处不画) { 隐藏两个标记(); return; }

        Vector3 屏 = cam.WorldToScreenPoint(目.世界点);
        bool 在镜头前 = 屏.z > 0.01f;

        // 屏幕点 → 画布本地坐标（ScreenSpaceOverlay 传 null 相机）。
        // 画布根的 pivot 是 (0.5,0.5)，所以本地坐标就是"以屏幕中心为原点"。
        Vector2 本地;
        bool 转好了 = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            画布根, new Vector2(屏.x, 屏.y), null, out 本地);
        // 在身后时投影会左右/上下翻转，翻回来方向才是对的
        if (!在镜头前) 本地 = -本地;

        Rect 区域 = 画布根.rect;
        float 半宽 = Mathf.Max(1f, 区域.width * 0.5f - 边缘留白);      // 箭头走的"内缩框"
        float 半高 = Mathf.Max(1f, 区域.height * 0.5f - 边缘留白);
        float 全宽 = Mathf.Max(1f, 区域.width * 0.5f);
        float 全高 = Mathf.Max(1f, 区域.height * 0.5f);

        // ★ 「在画面里」按**整个视口**判，不按内缩框判。
        //   踩过的坑（2026-10-02）：一开始拿"内缩 78 像素后的框"当在画面里的判据，
        //   结果目标只要落在屏幕最外一圈（俯视机位下，17 米外的地面目标就在屏幕底部
        //   50 像素处）就**只出箭头、头顶的感叹号永远不出现**。
        //   内缩只该用来决定"箭头贴在哪条边上"。
        bool 在画面里 = 转好了 && 在镜头前
                        && Mathf.Abs(本地.x) <= 全宽 && Mathf.Abs(本地.y) <= 全高;

        if (在画面里)
        {
            if (箭头 != null && 箭头.gameObject.activeSelf) 箭头.gameObject.SetActive(false);
            if (叹号 != null)
            {
                if (!叹号.gameObject.activeSelf) 叹号.gameObject.SetActive(true);
                // 贴边时往里收一点，别让「！」被屏幕边缘切掉
                float 边距 = 叹号大小 * 0.6f;
                叹号.anchoredPosition = new Vector2(
                    Mathf.Clamp(本地.x, -全宽 + 边距, 全宽 - 边距),
                    Mathf.Clamp(本地.y, -全高 + 边距, 全高 - 边距));
            }
            return;
        }

        if (叹号 != null && 叹号.gameObject.activeSelf) 叹号.gameObject.SetActive(false);
        if (箭头 == null) return;

        // 边缘箭头：把"屏幕中心 → 目标"这条射线夹到留白框上
        Vector2 方向 = 本地;
        if (方向.sqrMagnitude < 1e-4f) 方向 = new Vector2(1f, 0f);   // 正好在中心（背后）：给个默认朝右
        float 比例x = Mathf.Abs(方向.x) > 1e-4f ? 半宽 / Mathf.Abs(方向.x) : float.MaxValue;
        float 比例y = Mathf.Abs(方向.y) > 1e-4f ? 半高 / Mathf.Abs(方向.y) : float.MaxValue;
        float 比例 = Mathf.Min(1f, Mathf.Min(比例x, 比例y));

        if (!箭头.gameObject.activeSelf) 箭头.gameObject.SetActive(true);
        箭头.anchoredPosition = 方向 * 比例;
        箭头.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(方向.y, 方向.x) * Mathf.Rad2Deg - 90f);
    }

    void 隐藏两个标记()
    {
        if (叹号 != null && 叹号.gameObject.activeSelf) 叹号.gameObject.SetActive(false);
        if (箭头 != null && 箭头.gameObject.activeSelf) 箭头.gameObject.SetActive(false);
    }

    static Transform 找玩家()
    {
        var v = Object.FindObjectOfType<PlayerVitals>();
        if (v != null) return v.transform;
        var p = GameObject.Find("Player");
        return p != null ? p.transform : null;
    }

    // ============================================================ 搭界面

    void 取默认字体()
    {
        if (字体 != null) return;
#if UNITY_EDITOR
        字体 = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
#endif
        if (字体 == null)
            foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                if (f != null && f.name.ToLowerInvariant().Contains("simhei")) { 字体 = f; return; }
        if (字体 == null) Debug.LogWarning("[任务引导] 找不到中文字体，追踪面板会显示成方块", this);
    }

    void 搭界面()
    {
        if (画布 != null) return;

        var 根 = new GameObject("QuestGuideCanvas", typeof(Canvas), typeof(CanvasScaler));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        // ★ 压在**所有面板之下**（1500）。
        //
        // 【为什么不是 2300】原来给的是 2300 —— 比塔(2500)/对话(2600)/黑幕(2900) 低，
        //   看着"够低了"，但**面板里有一个比它更低**：`灵田地块界面` 是 1800、
        //   `灵田摆放器` 是 1900 ⇒ 玩家一按 F 打开的界面**被这个 HUD 盖住**（用户实测报过）。
        //   HUD 和面板比大小是比不完的（以后每加一个面板都要重排一次），
        //   所以直接压到**面板层之下、世界内提示之上**：
        //   世界内提示最高只到 500（传送圈）／210（地块牌）／200（F 提示）／150（血条飘字）／100（选中环）。
        //   ⇒ 1500 保证"面板永远盖得住 HUD，HUD 永远盖得住世界提示"。
        画布.sortingOrder = 1500;
        // 不吃射线（不然屏幕左侧一整块会挡住点击）
        画布.gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        画布根 = (RectTransform)根.transform;

        // 标记层：铺满屏幕；感叹号/箭头都挂它下面、锚点在正中，所以 anchoredPosition 就是"屏幕中心为原点"的坐标
        标记层 = UIBuildUtils.CreateRect("标记层", 画布根);
        UIBuildUtils.Stretch(标记层);
        标记层.pivot = new Vector2(0.5f, 0.5f);

        搭面板(画布根);
        搭标记();
    }

    void 搭面板(Transform 父)
    {
        var 板 = UIBuildUtils.CreateImage("主线追踪", 父, 底板色);
        面板 = 板.rectTransform;
        面板.anchorMin = new Vector2(0f, 0.5f);
        面板.anchorMax = new Vector2(0f, 0.5f);
        面板.pivot = new Vector2(0f, 0.5f);
        面板.sizeDelta = new Vector2(面板宽度, 0f);
        面板.anchoredPosition = new Vector2(左边缘, 竖直偏移);

        var 竖 = UIBuildUtils.AddVerticalLayout(面板, 8f, new RectOffset(16, 16, 14, 14));
        竖.childAlignment = TextAnchor.UpperLeft;
        竖.childControlWidth = true;
        // 高度跟着内容走（说明有几行就多高）
        var 自适应 = 板.gameObject.AddComponent<ContentSizeFitter>();
        自适应.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        标题文本 = 加一行(面板, "标题", 标题字号, 标题色);
        目标文本 = 加一行(面板, "目标", 目标字号, 目标色);
        说明文本 = 加一行(面板, "说明", 说明字号, 说明色);
        距离文本 = 加一行(面板, "距离", 距离字号, 距离色);
    }

    Text 加一行(RectTransform 父, string 名, int 号, Color 色)
    {
        var t = UIBuildUtils.CreateText(名, 父, 字体, "", 号, TextAnchor.UpperLeft, 色);
        // 黑描边：底板是半透明的，压在亮场景上也要看得清（和灵田信息牌同一个理由）
        UIBuildUtils.AddOutline(t.rectTransform, 描边色);
        return t;
    }

    void 搭标记()
    {
        // ---- 头顶感叹号：就是一个「！」----
        var 叹 = UIBuildUtils.CreateText("感叹号", 标记层, 字体, "！", Mathf.RoundToInt(叹号大小),
            TextAnchor.MiddleCenter, 标记色);
        叹号 = 叹.rectTransform;
        叹号.anchorMin = 叹号.anchorMax = new Vector2(0.5f, 0.5f);
        叹号.pivot = new Vector2(0.5f, 0.5f);
        叹号.sizeDelta = new Vector2(叹号大小, 叹号大小);
        UIBuildUtils.AddOutline(叹号, 描边色);
        叹号.gameObject.SetActive(false);

        // ---- 边缘箭头：运行时画一个**朝上的**金三角，靠旋转指方向 ----
        var 箭 = UIBuildUtils.CreateImage("边缘箭头", 标记层, 标记色);
        箭头 = 箭.rectTransform;
        箭头.anchorMin = 箭头.anchorMax = new Vector2(0.5f, 0.5f);
        箭头.pivot = new Vector2(0.5f, 0.5f);
        箭头.sizeDelta = new Vector2(箭头大小, 箭头大小);
        箭.sprite = 取三角精灵();
        箭.gameObject.SetActive(false);
    }

    /// <summary>
    /// 生成"朝上的实心三角 + 深色描边"的精灵（和 `StationInteractor` 现画圆角精灵一个路子）。
    /// 为什么不用现成图：工程里没有箭头图，为一个 44 像素的小三角去导一张 png 不值当。
    /// </summary>
    static Sprite 取三角精灵()
    {
        if (三角缓存 != null) return 三角缓存;
        const int 边 = 64;
        var tex = new Texture2D(边, 边, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var 亮 = Color.white;                              // 会被 Image.color 染成金色
        var 暗 = new Color(0.10f, 0.08f, 0.04f, 1f);
        var 透 = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < 边; y++)
        {
            float t = 1f - y / (float)(边 - 1);            // 1 = 顶点，0 = 底边
            float 半宽 = Mathf.Lerp(边 * 0.5f, 0f, t);
            for (int x = 0; x < 边; x++)
            {
                float dx = Mathf.Abs(x - (边 - 1) * 0.5f);
                tex.SetPixel(x, y, dx <= 半宽 - 3f ? 亮 : (dx <= 半宽 ? 暗 : 透));
            }
        }
        tex.Apply();
        三角缓存 = Sprite.Create(tex, new Rect(0, 0, 边, 边), new Vector2(0.5f, 0.5f), 100f);
        return 三角缓存;
    }

    // ---- ASCII 别名 ----
    public QuestDefinition GuidedStage => 当前阶段;
    public bool HasTarget => 当前目标.有;
}
