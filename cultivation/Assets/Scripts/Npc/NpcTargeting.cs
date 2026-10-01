using System;
using UnityEngine;

/// <summary>
/// 全局的「选中 / 锁定」管理器。挂在 Player 上。
///
/// 语义区分（按策划说明）：
///   · 选中：鼠标左键点 NPC。用于查看信息、执行任务。脚底显示【绿色圆环】。
///   · 锁定：鼠标右键点 NPC。用于攻击。脚底显示【红色圆环】，头顶显示血条。
///
/// 注意：**鼠标直接右键锁定的目标不受好感度限制**（好感度 &gt; 0 也能锁）。
///      「好感度 &lt; 0」只用于战斗中的【自动寻找下一个目标】。
/// </summary>
public class NpcTargeting : MonoBehaviour
{
    [Header("输入")]
    [Tooltip("选中键")]
    public KeyCode 选中键 = KeyCode.Mouse0;

    [Tooltip("锁定键")]
    public KeyCode 锁定键 = KeyCode.Mouse1;

    [Tooltip("射线检测层级")]
    public LayerMask 射线层 = ~0;

    [Tooltip("最远拾取距离（射线本身的长度上限）。实际能不能选中/锁定还要过「神识范围」这一关")]
    public float 最远距离 = 200f;

    [Header("神识范围（选中 / 锁定的有效距离）")]
    [Tooltip("勾掉 = 退回旧行为（只受「最远拾取距离」限制，能锁 200 米外的目标）")]
    public bool 受神识范围限制 = true;

    [Tooltip("神识范围基础值（米）")]
    public float 基础神识范围 = 4f;

    [Tooltip("每 1 点神识增加的范围（米）")]
    public float 每点神识范围 = 0.8f;

    [Tooltip("神识范围上限（米）")]
    public float 神识范围上限 = 40f;

    [Tooltip("选中/锁定目标超出神识范围时，右上角飘一条提示")]
    public bool 超范围时提示 = true;

    [Header("引用")]
    [Tooltip("用于发射鼠标射线的相机。留空则取 Camera.main")]
    public Camera 射线相机;

    [Tooltip("神识数值来源。留空则自动取自己身上的 PlayerCombatStats")]
    public PlayerCombatStats 战斗属性;

    /// <summary>当前选中的 NPC（绿环）</summary>
    public NpcInstance Selected { get; private set; }

    /// <summary>当前锁定的 NPC（红环 + 血条）</summary>
    public NpcInstance Locked { get; private set; }

    /// <summary>选中发生变化</summary>
    public event Action<NpcInstance> SelectionChanged;

    /// <summary>锁定发生变化。第二个参数表示是否由玩家鼠标点击触发</summary>
    public event Action<NpcInstance, bool> LockChanged;

    void Awake()
    {
        if (射线相机 == null) 射线相机 = Camera.main;
        解析引用();
    }

    void 解析引用()
    {
        if (战斗属性 == null) 战斗属性 = GetComponent<PlayerCombatStats>();
        if (射线相机 == null) 射线相机 = Camera.main;
    }

    /// <summary>
    /// 神识范围（米）= 基础 + 神识 × 每点，封顶。
    ///
    /// **这是一条全局规则**：选中（左键）和锁定（右键）都必须落在范围内。
    /// 以前这里只判 <see cref="最远距离"/>（200 米），等于神识范围对索敌完全没作用 ——
    /// 会出现「14 米的神识能锁 40 米外的怪」。换算式子统一走
    /// <see cref="PlayerCombatStats.算神识范围"/>，不再各抄一份。
    /// </summary>
    public float 神识范围
    {
        get
        {
            if (!受神识范围限制) return float.PositiveInfinity;
            if (战斗属性 == null) 解析引用();
            return PlayerCombatStats.算神识范围(战斗属性, 基础神识范围, 每点神识范围, 神识范围上限);
        }
    }

    void Update()
    {
        if (射线相机 == null) { 射线相机 = Camera.main; if (射线相机 == null) return; }

        维护锁定目标还活着();

        // 有全屏界面（设施界面等）开着时，鼠标不该穿透到场景里。
        // 否则站在炼丹炉前开着界面，右键会连建筑一起点，又开一个新界面。
        if (StationInteractor.有界面打开) return;

        if (Input.GetKeyDown(选中键))
        {
            // 【点空白 = 取消选中】以前只有"点到 NPC 才做事"，点空地什么都不发生，
            // 选中的绿环就撤不掉（只有右键能撤锁定）。现在点空地也走一遍：
            // RaycastNpc() 返回 null → Select(null) → 绿环消失、SelectionChanged 发 null。
            //
            // 点在可点的 UI（技能格子 / 面板按钮）上时不算"世界里的点击" ——
            // HUD 是 ScreenSpaceOverlay，Physics.Raycast 打不到它（见docs/ai/archive/开发注意事项-流水原文.md 9.5），
            // 不挡一下的话点技能格子会顺手把选中也取消掉。
            if (!点在界面上()) 点击选中();
        }

        if (Input.GetKeyDown(锁定键))
        {
            // 站在设施（修炼房屋/炼丹炉/炼器炉/布阵点）旁边按右键时，
            // 这一下是"开界面"而不是"锁定敌人" —— 否则想开炼丹炉结果锁了个怪。
            //
            // 【为什么不靠 StationInteractor 设的静态标记】
            // 那依赖两个脚本的 Update 执行顺序，顺序不定就会偶发失效。
            // 这里自己查一遍，和顺序无关，稳定。
            if (附近有设施()) return;

            var npc = RaycastNpc();
            if (npc != null)
            {
                if (在神识范围内(npc))
                {
                    Lock(npc, byPlayerClick: true);
                }
                else
                {
                    // 超出了就不锁，也不能顺手把已有锁定清掉 —— 玩家是想锁但够不着，
                    // 和"右键点空地"是两回事。
                    提示超范围(npc);
                }
            }
            else ClearLock();          // 右键点在空白处 → 取消锁定（红环与血条一并消失）
        }
    }

    /// <summary>
    /// 鼠标左键那一下：射到 NPC 就选中它，射到**空白处就取消选中**。
    /// 单独抽出来是为了能直接验证（不用伪造鼠标输入）。
    /// 和右键一样受神识范围限制：够不着的目标不选中，也不把已有选中清掉。
    /// </summary>
    public void 点击选中()
    {
        var npc = RaycastNpc();
        if (npc == null) { Select(null); return; }
        if (!在神识范围内(npc)) { 提示超范围(npc); return; }
        Select(npc);
    }

    /// <summary>鼠标是不是压在可点的 UI 上（HUD 按钮 / 面板）</summary>
    static bool 点在界面上()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    /// <summary>玩家身边有没有可交互的设施（有的话右键要留给它）</summary>
    bool 附近有设施()
    {
        foreach (var s in FindObjectsOfType<StationInteractable>())
        {
            if (s == null || !s.isActiveAndEnabled) continue;
            var 差 = s.transform.position - transform.position;
            float 水平 = new Vector2(差.x, 差.z).magnitude;
            if (s.玩家在范围内(水平, 差.y)) return true;
        }
        return false;
    }

    /// <summary>鼠标位置射到的 NPC（没有则 null）。**只负责射线**，不含神识判定</summary>
    public NpcInstance RaycastNpc()
    {
        if (射线相机 == null) return null;
        var ray = 射线相机.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 最远距离, 射线层)) return null;
        return hit.collider.GetComponentInParent<NpcInstance>();
    }

    // ============================================================ 神识范围判定

    /// <summary>
    /// 目标是不是在神识范围内。**量的是"身体最近点"到玩家的距离**，不是碰撞点：
    /// 鼠标射线打到的往往是怪身上朝向镜头的那个点，拿它算会低估距离。
    /// 大体积目标（龙、巨兽）用"最近的体表点"更符合"我感知得到它"的直觉。
    /// </summary>
    public bool 在神识范围内(NpcInstance npc)
    {
        return 到目标距离(npc) <= 神识范围;
    }

    /// <summary>玩家到目标体表最近点的距离（米）。目标为空返回 0</summary>
    public float 到目标距离(NpcInstance npc)
    {
        if (npc == null) return 0f;

        Vector3 玩家位 = transform.position;
        var 碰撞体 = npc.GetComponentsInChildren<Collider>(true);
        if (碰撞体 == null || 碰撞体.Length == 0)
            return Vector3.Distance(玩家位, npc.transform.position);

        float 最近 = float.PositiveInfinity;
        foreach (var c in 碰撞体)
        {
            if (c == null || !c.enabled) continue;
            float d = Vector3.Distance(玩家位, c.ClosestPoint(玩家位));
            if (d < 最近) 最近 = d;
        }
        return float.IsPositiveInfinity(最近)
            ? Vector3.Distance(玩家位, npc.transform.position)
            : 最近;
    }

    /// <summary>目标超出神识范围时给一条提示（右上角 Toast）。公开出来方便调试 / 自测</summary>
    public void 提示超范围(NpcInstance npc)
    {
        if (!超范围时提示 || !受神识范围限制) return;

        // 提示别刷屏：同一条 1 秒内只飘一次
        if (Time.unscaledTime - 上次超范围提示 < 1f) return;
        上次超范围提示 = Time.unscaledTime;

        Debug.Log("[索敌] 「" + npc.DisplayName + "」在神识范围外（" + 到目标距离(npc).ToString("0.#")
            + "m > " + 神识范围.ToString("0.#") + "m）→ 不能锁定", this);
        ToastUI.提示("超出神识范围（" + 到目标距离(npc).ToString("0.#")
            + "m / " + 神识范围.ToString("0.#") + "m）");
    }

    float 上次超范围提示 = -99f;

    /// <summary>选中某个 NPC（绿环）</summary>
    public void Select(NpcInstance npc)
    {
        if (Selected == npc) return;

        if (Selected != null)
        {
            var old = IndicatorOf(Selected);
            if (old != null) old.SetSelected(false);
        }

        Selected = npc;

        if (Selected != null)
        {
            var cur = IndicatorOf(Selected);
            if (cur != null) cur.SetSelected(true);
        }

        SelectionChanged?.Invoke(Selected);
    }

    /// <summary>
    /// 锁定某个 NPC（红环 + 血条）。
    /// 加上 byPlayerClick 语义只是为了区分「玩家手动点的」和「系统自动找的」，
    /// 两者在锁定条件上本来就不同：手动点不看好感度，自动找要求好感度 &lt; 0。
    /// </summary>
    public void Lock(NpcInstance npc, bool byPlayerClick = false)
    {
        // ★ 记下"这个锁定是不是玩家的意图"—— 决定目标死了之后要不要自动改锁。
        //   手动点 → 是；系统自动找的 → 继承前一次的状态（见 维护锁定目标还活着）
        if (byPlayerClick) 玩家指定的锁定 = true;
        else if (npc == null) 玩家指定的锁定 = false;

        if (Locked == npc) return;

        if (Locked != null)
        {
            var old = IndicatorOf(Locked);
            if (old != null) old.SetLocked(false);
        }

        Locked = npc;

        if (Locked != null)
        {
            var cur = IndicatorOf(Locked);
            if (cur != null) cur.SetLocked(true);
        }

        LockChanged?.Invoke(Locked, byPlayerClick);
    }

    /// <summary>
    /// 解除锁定
    /// </summary>
    public void ClearLock()
    {
        玩家指定的锁定 = false;
        Lock(null);
    }

    // ============================================================ 目标死了 → 自动改锁

    /// <summary>
    /// 当前锁定是不是**玩家指定的**。只有玩家指定过的锁定，目标死了才会自动改锁；
    /// 系统自动找的下一个目标同样继承这个标记，所以会**一直续下去**。
    ///
    /// 【为什么要这个标记】如果无条件自动改锁，玩家明明想停手（或收工），
    /// 一死就又被锁上一个新的，反而是在替他做决定。
    /// </summary>
    public bool 玩家指定的锁定 { get; private set; }

    /// <summary>
    /// **锁定目标死了 → 自动锁一个"神识范围内、对我好感度 &lt; 0（会攻击我）"的目标。**
    ///
    /// 用户 2026-09-30 / 10-01 的要求：
    /// > 如果锁定中的怪物死了，就会锁定神识中的对我好感度低于 0 要攻击我的 NPC
    ///
    /// ## 为什么要放在这里（玩家身上），而不是某个普攻方法里
    ///
    /// 这条功能原来写在 `BasicSword01.找下一个目标()` 里 —— 结果是**剑自己的逻辑**：
    /// · 量的是「到**剑**的距离」（剑在飞，不是玩家在感知）
    /// · **换功法就没了**：`PlayerAbilityLoader` 会把 `BasicSword01` 停用
    ///   （用玄霄雷决 / 雷动千闪 / 沧澜寒渊录时它就是停用状态），功能整个消失
    /// · 别的普攻方法（远程 / 冰刺 / 雷）**根本没有**这套
    ///
    /// 但「锁定」本来就是**玩家级**的概念（`Locked` 就挂在 `NpcTargeting` 上），
    /// 所以判断依据必须是玩家自己的**神识范围**（和右键锁定用同一个 `神识范围`），
    /// 这样换任何功法、任何普攻方法、甚至靠神通打，行为都一致。
    ///
    /// ## 判据（和右键锁定对齐）
    ///
    /// · 必须 `是敌对目标`（= `对主角好感度 &lt; 0`，也就是"会主动攻击我"的）
    /// · 必须在 <see cref="神识范围"/> 内 —— 和右键锁定同一把尺子
    /// · 取**最近**的一个
    ///
    /// **一个都没找到 → 清掉锁定**（红环和血条一起消失），而不是留着一个死目标。
    /// </summary>
    void 维护锁定目标还活着()
    {
        if (!玩家指定的锁定) return;
        if (Locked == null) return;              // 已经被清掉了
        if (!Locked.IsDead) return;              // 还活着，正常打

        var 下一个 = 找最近的敌对目标();
        if (下一个 == null)
        {
            Debug.Log("[索敌] 锁定目标「" + Locked.DisplayName + "」已死，神识范围内没有其他敌对目标 → 解除锁定");
            ClearLock();                          // 注意：它会清 玩家指定的锁定
            return;
        }

        Debug.Log("[索敌] 锁定目标「" + Locked.DisplayName + "」已死 → 自动改锁「"
                  + 下一个.DisplayName + "」（距 " + 到目标距离(下一个).ToString("0.#")
                  + "m / 神识 " + 神识范围.ToString("0.#") + "m）");

        // ★ byPlayerClick: false —— 这是系统自动找的，不是玩家点的。
        //   玩家指定的锁定 这个标记**不会被清掉**，所以下一只死了还会继续改锁。
        Lock(下一个, byPlayerClick: false);
    }

    /// <summary>
    /// 神识范围内最近的**敌对目标**（好感度 &lt; 0 = 会攻击我）。没有就返回 null。
    ///
    /// 公开出来是为了让普攻方法 / 神通也能复用同一套判据，
    /// 不用各抄一份（抄一份就会各自跑偏 —— 这正是老代码的病根）。
    /// </summary>
    public NpcInstance 找最近的敌对目标()
    {
        NpcInstance 最佳 = null;
        float 最近 = float.MaxValue;
        float 范围 = 神识范围;

        foreach (var npc in FindObjectsOfType<NpcInstance>())
        {
            if (npc == null || npc.IsDead) continue;
            if (!npc.是敌对目标) continue;              // ★ 必须是"对我好感度 < 0、会攻击我"的

            float d = 到目标距离(npc);                  // 量的是**玩家**到目标体表的最近距离
            if (d > 范围) continue;
            if (d >= 最近) continue;

            最近 = d;
            最佳 = npc;
        }
        return 最佳;
    }

    /// <summary>解除选中</summary>
    public void ClearSelection()
    {
        Select(null);
    }

    static NpcIndicator IndicatorOf(NpcInstance npc)
    {
        if (npc == null) return null;
        var ind = npc.GetComponent<NpcIndicator>();
        if (ind == null) ind = npc.gameObject.AddComponent<NpcIndicator>();
        return ind;
    }

    // ============================================================
    //  ASCII 公开接口（内部中文命名，对外统一 ASCII，见项目约定）
    // ============================================================
    public NpcInstance SelectedNpc => Selected;
    public NpcInstance LockedNpc => Locked;
    public void SelectNpc(NpcInstance npc) => Select(npc);
    public void LockNpc(NpcInstance npc, bool byPlayerClick = true) => Lock(npc, byPlayerClick);
    public NpcInstance RaycastNpcUnderMouse() => RaycastNpc();

    /// <summary>神识范围（米）。<see cref="受神识范围限制"/> 关掉时返回正无穷</summary>
    public float SenseRange => 神识范围;
    public bool IsInSenseRange(NpcInstance npc) => 在神识范围内(npc);
    public float DistanceTo(NpcInstance npc) => 到目标距离(npc);
}
