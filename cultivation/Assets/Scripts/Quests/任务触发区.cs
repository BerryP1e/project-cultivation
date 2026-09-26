using UnityEngine;

/// <summary>
/// **范围型任务触发区** —— 摆在场景里，主角走进半径就触发任务。
///
/// 用法：把本组件（或预制体 `Assets/点位/任务触发区.prefab`）放到场景里想要的位置，
/// 填好「半径」和要触发的东西。策划不用再去任务表里手写坐标。
///
/// 能触发三种事（可以同时填）：
///   · <see cref="触发任务id"/>     —— 进圈就**接取**这个任务
///   · <see cref="完成阶段任务id"/> —— 进圈就**完成该任务的当前阶段**（推进一格）
///   · <see cref="加标记"/>         —— 进圈就**加标记**（分号分隔），对话表用「需要标记」就能长出新回答
///
/// <see cref="需要任务id"/> 留空 = 谁都能触发；填了就要求那个任务**正在进行中**才触发。
/// <see cref="只触发一次"/> 勾上 = 触发过就不再触发（默认）。
/// </summary>
[DisallowMultipleComponent]
public class 任务触发区 : MonoBehaviour
{
    [Header("范围")]
    [Tooltip("触发半径（米）。主角走到球心这个距离以内就算进圈")]
    [Min(0.1f)] public float 半径 = 5f;

    [Tooltip("进圈判定用的高度差上限（米）。填 0 = 不考虑高度，只看水平距离")]
    [Min(0f)] public float 高度容差 = 6f;

    [Header("触发内容（可只填一项，也可同时填）")]
    [Tooltip("进圈就接取这个任务 id（留空 = 不接）")]
    public string 触发任务id = "";

    [Tooltip("进圈就完成这个任务的当前阶段（留空 = 不推进）")]
    public string 完成阶段任务id = "";

    [Tooltip("进圈就加上的标记，分号分隔（留空 = 不加）")]
    public string 加标记 = "";

    [Header("条件")]
    [Tooltip("要求这个任务正在进行中才触发（留空 = 不要求）。注意：任务**已完成**就不算进行中")]
    public string 需要任务id = "";

    [Tooltip("需要全部具备这些标记才触发，分号分隔（留空 = 不要求）。\n⚠ 标记只存在内存里、**不存档**，重开游戏就没了 —— 想表达「某个任务做完之后才触发」，请用下面的 需要完成任务id")]
    public string 需要标记 = "";

    [Tooltip("要求这个任务**已经完成**才触发（留空 = 不要求）。\n★ 推荐用这个：任务完成状态会跟着存档走，重启游戏也还在")]
    public string 需要完成任务id = "";

    [Tooltip("勾上 = 条件没满足时把「范围圈」隐藏起来（你看不到圈就说明这一环还没解锁）")]
    public bool 未解锁时隐藏范围圈 = true;

    [Tooltip("勾上 = **游戏运行时**把范围圈整个藏起来（编辑器里照样显示，方便摆位置）。实机观感更干净")]
    public bool 游戏里隐藏范围圈 = true;

    [Tooltip("★ 勾上（默认）= 检测半径**直接跟随「范围圈」在场景里的实际大小**：你缩放触发区实例，检测范围跟着变，所见即所得。关掉才用上面的「半径」数字。")]
    public bool 半径跟随范围圈 = true;

    [Tooltip("勾上 = 触发过一次就不再触发")]
    public bool 只触发一次 = true;

    [Tooltip("演出/过场进行中不触发（避免和黑幕、对话抢）")]
    public bool 演出中不触发 = true;

    [Header("调试")]
    [Tooltip("在 Scene 视图里画出范围圈和竖线，方便摆位置")]
    public bool 显示范围 = true;

    public Color 范围颜色 = new Color(1f, 0.78f, 0.12f, 0.9f);

    [Tooltip("进圈时在 Console 打一条日志")]
    public bool 打日志 = true;

    bool 已触发;
    Renderer 范围圈;
    float 上次提示;

    /// <summary>前置没满足时，说清楚是哪一条没满足（进圈后 1 秒一条日志）</summary>
    string 未满足原因()
    {
        var 任务 = 任务管理器.实例 != null ? 任务管理器.实例 : Object.FindObjectOfType<任务管理器>();
        var 缺 = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrEmpty(需要任务id) && (任务 == null || !任务.进行中(需要任务id)))
            缺.Add("需要任务「" + 需要任务id + "」正在进行中（现在：" + (任务 == null ? "没有任务管理器" : 任务.已完成(需要任务id) ? "已完成，不算进行中" : "未接取") + "）");
        if (!string.IsNullOrEmpty(需要完成任务id) && (任务 == null || !任务.已完成(需要完成任务id)))
            缺.Add("需要完成任务「" + 需要完成任务id + "」（现在：" + (任务 == null ? "没有任务管理器" : 任务.进行中(需要完成任务id) ? "正在进行中，还没完成" : "未接取") + "）");
        if (!string.IsNullOrEmpty(需要标记))
        {
            var 有 = 对话标记.全部标记();
            var 少的 = new System.Collections.Generic.List<string>();
            foreach (var m in 需要标记.Split(';'))
            {
                var k = m.Trim();
                if (k.Length == 0) continue;
                bool 找到 = false;
                foreach (var x in 有) if (x == k) { 找到 = true; break; }
                if (!找到) 少的.Add(k);
            }
            if (少的.Count > 0) 缺.Add("缺少标记「" + string.Join("、", 少的) + "」（标记不存档，重开游戏会没）");
        }
        return 缺.Count == 0 ? "（没有前置要求，可能是别的问题）" : string.Join("；", 缺);
    }

    void Awake()
    {
        取范围圈();
    }

    /// <summary>拿「范围圈」（编辑器里也拿得到，Gizmo 要用）</summary>
    Renderer 取范围圈()
    {
        if (范围圈 == null)
        {
            var t = transform.Find("\u8303\u56f4\u5708");
            if (t != null) 范围圈 = t.GetComponent<Renderer>();
        }
        return 范围圈;
    }

    /// <summary>
    /// **实际检测半径**。默认直接由「范围圈」的场景实际大小算出来：
    /// 范围圈模型是直径 1 的圆柱，所以 lossyScale.x = 直径，半径 = 直径 / 2。
    /// 这样策划缩放触发区实例就等于调范围，不会出现「看到的圈」和「检测范围」脱钩。
    /// </summary>
    public float 有效半径()
    {
        if (半径跟随范围圈)
        {
            var r = 取范围圈();
            if (r != null)
            {
                float 直径 = r.transform.lossyScale.x;
                if (直径 > 0.001f) return 直径 * 0.5f;
            }
        }
        return 半径;
    }

    /// <summary>所有前置条件是否都满足（不含「玩家是否在圈内」）</summary>
    public bool 条件满足()
    {
        var 任务 = 任务管理器.实例 != null ? 任务管理器.实例 : Object.FindObjectOfType<任务管理器>();
        if (!string.IsNullOrEmpty(需要任务id) && (任务 == null || !任务.进行中(需要任务id))) return false;
        if (!string.IsNullOrEmpty(需要完成任务id) && (任务 == null || !任务.已完成(需要完成任务id))) return false;
        if (!有全部标记(需要标记)) return false;
        return true;
    }

    void 刷新范围圈()
    {
        if (范围圈 == null) return;
        if (游戏里隐藏范围圈 && Application.isPlaying) { 范围圈.enabled = false; return; }
        if (!未解锁时隐藏范围圈) return;              // 一直显示
        范围圈.enabled = 条件满足();
    }

    /// <summary>外部想手动复位（例如重开一段剧情）</summary>
    public void 复位() { 已触发 = false; }

    void Update()
    {
        刷新范围圈();
        if (只触发一次 && 已触发) return;
        if (演出中不触发 && (黑幕字幕.演出中 || DialogueUI.正在显示)) return;

        var 玩家 = 物品使用器.取玩家物体();
        if (玩家 == null) return;

        // ★ 用玩家**碰撞体中心**判断高度，而不是根坐标（脚底）：
        //   地面不平时脚底会明显低于圈心，用根坐标容易被「高度容差」误挡。
        Vector3 玩家点 = 玩家.transform.position;
        var 玩家碰撞体 = 玩家.GetComponent<Collider>();
        if (玩家碰撞体 != null) 玩家点 = 玩家碰撞体.bounds.center;
        Vector3 差 = 玩家点 - transform.position;
        if (高度容差 > 0f && Mathf.Abs(差.y) > 高度容差)
        {
            if (Time.time - 上次提示 > 1f)
            {
                上次提示 = Time.time;
                Debug.Log("[触发区] " + name + "：玩家水平位置已经在圈里，但高度差 " + Mathf.Abs(差.y).ToString("F1")
                    + "m 超过容差 " + 高度容差 + "m（圈在 y=" + transform.position.y.ToString("F1") + "，玩家在 y=" + 玩家.transform.position.y.ToString("F1") + "）", this);
            }
            return;
        }
        差.y = 0f;
        float R = 有效半径();
        if (差.sqrMagnitude > R * R)
        {
            // 还在圈外：已经接近（半径 3 倍以内）时每秒报一次实际距离，
            // 这样「到底要走到多近才触发」在 Console 里能直接看出来。
            if (差.sqrMagnitude < R * R * 9f && Time.time - 上次提示 > 1f)
            {
                上次提示 = Time.time;
                Debug.Log("[触发区] " + name + "：还差 " + 差.magnitude.ToString("F2") + "m 进圈（实际半径 " + R.ToString("F2")
                    + (半径跟随范围圈 ? "＝范围圈直径/2" : "＝半径字段") + "，圈心 " + transform.position.ToString("F1")
                    + "，玩家 " + 玩家点.ToString("F1") + "）", this);
            }
            return;
        }

        // ★ 人已经进圈：不管最不触发，都把原因打出来（每秒最多一条），免得「没反应」查不出原因
        if (Time.time - 上次提示 > 1f)
        {
            上次提示 = Time.time;
            if (只触发一次 && 已触发) Debug.Log("[触发区] " + name + "：已触发过（只触发一次已勾上），不再重复", this);
            else if (!条件满足()) Debug.Log("[触发区] " + name + "：玩家已进圈，但前置未满足 —— " + 未满足原因(), this);
            else Debug.Log("[触发区] " + name + "：条件已满足，正在触发", this);
        }

        触发(玩家);
    }

    void 触发(GameObject 玩家)
    {
        var 任务 = 任务管理器.实例 != null ? 任务管理器.实例 : Object.FindObjectOfType<任务管理器>();
        if (任务 == null)
        {
            Debug.LogWarning("[触发区] " + name + " 进圈了，但场景里没有任务管理器", this);
            return;
        }
        if (!条件满足()) return;

        已触发 = true;
        if (打日志) Debug.Log("[触发区] " + name + " 触发（玩家距 " + Vector3.Distance(玩家.transform.position, transform.position).ToString("F2") + "m）", this);

        if (!string.IsNullOrEmpty(触发任务id)) 任务.接取(触发任务id);

        if (!string.IsNullOrEmpty(加标记)) 对话标记.添加一批(加标记);

        if (!string.IsNullOrEmpty(完成阶段任务id)) 任务.完成当前阶段(完成阶段任务id);
    }

    /// <summary>分号分隔的标记串是否全都有（空串 = 直接通过）</summary>
    public static bool 有全部标记(string 标记串)
    {
        if (string.IsNullOrEmpty(标记串)) return true;
        var 有 = 对话标记.全部标记();
        foreach (var m in 标记串.Split(';'))
        {
            var k = m.Trim();
            if (k.Length == 0) continue;
            bool 找到 = false;
            foreach (var x in 有) if (x == k) { 找到 = true; break; }
            if (!找到) return false;
        }
        return true;
    }

    void OnDrawGizmos()
    {
        if (!显示范围) return;
        Gizmos.color = 范围颜色;
        float R = 有效半径();
        var 中心 = transform.position;
        int N = 64;
        var 前 = 中心 + Vector3.forward * R;
        for (int i = 1; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            var 点 = 中心 + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * R;
            Gizmos.DrawLine(前, 点);
            前 = 点;
        }
        Gizmos.DrawLine(中心, 中心 + Vector3.up * 3f);
        Gizmos.DrawWireSphere(中心, 0.4f);
    }

    // ---- ASCII 别名 ----
    public float radius { get => 半径; set => 半径 = value; }
    public string questOnEnter { get => 触发任务id; set => 触发任务id = value; }
    public string completeQuestStage { get => 完成阶段任务id; set => 完成阶段任务id = value; }
    public string addMarks { get => 加标记; set => 加标记 = value; }
    public string requiresQuest { get => 需要任务id; set => 需要任务id = value; }
    public void ResetTrigger() => 复位();
}
