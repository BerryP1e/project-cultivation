using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **刷怪点**：怪从这里生出来。挂在塔里地面的空物件上。
///
/// 【为什么要一个专门的组件，而不是用 <c>GameObject.Find</c> 找名字】
/// 文档里反复踩过「按名字找」的坑（Sect 里传送门和落点同名，玩家被放到传送门身上）。
/// 刷怪点重名的概率极高（「刷怪点 1/2/3」），所以**必须是组件引用**。
///
/// <see cref="SpawnZone"/> 会把一个组里的怪**轮流铺**到所有刷怪点上，
/// 所以想围一圈就在塔里摆一圈空物件挂上本组件。
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Tooltip("刷新时的朝向（Euler）。留空用物体自身朝向")]
    public bool 用自身朝向 = true;

    [Tooltip("额外的高度偏移，避免怪陷进地板")]
    public float 高度偏移 = 0.1f;

    /// <summary>怪应该出现的世界坐标</summary>
    public Vector3 出生位置 => transform.position + Vector3.up * 高度偏移;

    /// <summary>怪应该朝向</summary>
    public Quaternion 出生朝向 => 用自身朝向 ? transform.rotation : Quaternion.identity;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(出生位置, 0.6f);
        Gizmos.DrawLine(出生位置, 出生位置 + transform.forward * 1.4f);
    }

    /// <summary>把一个层级下的刷怪点全找出来（按兄弟顺序，保证可复现）</summary>
    public static List<SpawnPoint> 收集(Transform 父节点)
    {
        var r = new List<SpawnPoint>();
        if (父节点 != null)
        {
            父节点.GetComponentsInChildren(true, r);
        }
        else
        {
            r.AddRange(FindObjectsOfType<SpawnPoint>());
        }
        // 按 hierarchy 路径排序 —— GetComponentsInChildren 的顺序虽然稳定，
        // 但排序后跨机器 / 跨重开场景也一致，刷怪位置可复现
        r.Sort((a, b) => string.CompareOrdinal(路径(a), 路径(b)));
        return r;
    }

    static string 路径(SpawnPoint p)
    {
        if (p == null) return "";
        var sb = new System.Text.StringBuilder(p.name);
        var t = p.transform.parent;
        while (t != null) { sb.Insert(0, t.name + "/"); t = t.parent; }
        return sb.ToString();
    }
}
