using System.Collections.Generic;
using UnityEngine;

/// <summary>可叠加的移动禁锢。保留 NPC 攻击与 AI，释放一个施法者不会释放其他施法者。</summary>
[DefaultExecutionOrder(10000)]
public class NpcMovementLock : MonoBehaviour
{
    readonly HashSet<Object> 来源 = new HashSet<Object>();
    Vector3 固定位置;
    public bool 已禁锢 => 来源.Count > 0;
    public void 加锁(Object 谁)
    {
        if (谁 == null) return;
        if (来源.Count == 0) 固定位置 = transform.position;
        来源.Add(谁);
    }
    public void 解锁(Object 谁) { 来源.Remove(谁); }
    void LateUpdate()
    {
        来源.RemoveWhere(x => x == null);
        if (已禁锢) transform.position = 固定位置;
    }
}
