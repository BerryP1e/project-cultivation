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
    [Tooltip("要求这个任务正在进行中才触发（留空 = 不要求）")]
    public string 需要任务id = "";

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

    /// <summary>外部想手动复位（例如重开一段剧情）</summary>
    public void 复位() { 已触发 = false; }

    void Update()
    {
        if (只触发一次 && 已触发) return;
        if (演出中不触发 && (黑幕字幕.演出中 || DialogueUI.正在显示)) return;

        var 玩家 = 物品使用器.取玩家物体();
        if (玩家 == null) return;

        Vector3 差 = 玩家.transform.position - transform.position;
        if (高度容差 > 0f && Mathf.Abs(差.y) > 高度容差) return;
        差.y = 0f;
        if (差.sqrMagnitude > 半径 * 半径) return;

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
        if (!string.IsNullOrEmpty(需要任务id) && !任务.进行中(需要任务id)) return;

        已触发 = true;
        if (打日志) Debug.Log("[触发区] " + name + " 触发（玩家距 " + Vector3.Distance(玩家.transform.position, transform.position).ToString("F2") + "m）", this);

        if (!string.IsNullOrEmpty(触发任务id)) 任务.接取(触发任务id);

        if (!string.IsNullOrEmpty(加标记)) 对话标记.添加一批(加标记);

        if (!string.IsNullOrEmpty(完成阶段任务id)) 任务.完成当前阶段(完成阶段任务id);
    }

    void OnDrawGizmos()
    {
        if (!显示范围) return;
        Gizmos.color = 范围颜色;
        var 中心 = transform.position;
        int N = 64;
        var 前 = 中心 + Vector3.forward * 半径;
        for (int i = 1; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            var 点 = 中心 + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 半径;
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
