using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public sealed class TeleportBookmark
{
    public string 场景 = "", 名称 = "", 落点名 = "";
    public Vector3 位置;
    public float 朝向;
    public TeleportBookmark Copy() => new TeleportBookmark { 场景 = 场景, 名称 = 名称, 落点名 = 落点名, 位置 = 位置, 朝向 = 朝向 };
}

/// <summary>实际传送到达的地点；学习或打开空间腾挪不解锁地点。</summary>
public static class SpatialTravel
{
    static TeleportBookmark pending;
    static float deadline;
    public static List<TeleportBookmark> Copy(IEnumerable<TeleportBookmark> source)
    {
        var result = new List<TeleportBookmark>();
        if (source != null) foreach (var point in source)
            if (point != null && !string.IsNullOrEmpty(point.场景)) result.Add(point.Copy());
        return result;
    }
    public static void Record(UIPanelData data, TeleportBookmark point)
    {
        if (!data || point == null || string.IsNullOrEmpty(point.场景)) return;
        if (data.已访传送点 == null) data.已访传送点 = new List<TeleportBookmark>();
        var existing = data.已访传送点.Find(x => x != null && string.Equals(x.场景, point.场景, StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(point.落点名) ? Vector3.Distance(x.位置, point.位置) < 1 : x.落点名 == point.落点名));
        if (existing == null) data.已访传送点.Add(point.Copy());
        else { existing.位置 = point.位置; existing.朝向 = point.朝向; existing.名称 = point.名称; }
    }
    public static void PlanArrival(string scene, string landing, string name)
    {
        pending = new TeleportBookmark { 场景 = scene, 落点名 = landing, 名称 = name };
        deadline = Time.realtimeSinceStartup + 60;
    }
    public static void CancelPending() => pending = null;
    public static void CheckArrival(Transform player, UIPanelData data)
    {
        if (pending == null) return;
        if (Time.realtimeSinceStartup > deadline) { pending = null; return; }
        if (!player || !data || !string.Equals(player.gameObject.scene.name, pending.场景, StringComparison.OrdinalIgnoreCase)) return;
        var landing = FindLanding(player.gameObject.scene, pending.落点名);
        if (!landing || Vector3.Distance(player.position, landing.position) > 1.5f) return;
        pending.位置 = landing.position; pending.朝向 = landing.eulerAngles.y;
        Record(data, pending); pending = null;
    }
    public static Transform FindLanding(Scene scene, string name)
    {
        if (string.IsNullOrEmpty(name) || !scene.IsValid()) return null;
        foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name && !t.GetComponent<Teleporter>()) return t;
        return null;
    }
    public static bool TryQuest(out TeleportBookmark point)
    {
        var guide = UnityEngine.Object.FindObjectOfType<宗门任务引导>();
        if (guide && guide.TryGetTravelTarget(out point)) return true;
        point = null; return false;
    }
    public static bool Travel(PlayerTraversalController owner, TeleportBookmark point, UnityEngine.Object inputOwner = null)
    {
        if (!owner || point == null || UiEscRegistry.SceneInputBlockedByOthers(inputOwner) || !owner.可用(TraversalAction.SpaceShift, out _)) return false;
        if (!string.IsNullOrEmpty(point.场景) && !string.Equals(point.场景, owner.gameObject.scene.name, StringComparison.OrdinalIgnoreCase))
        {
            if (!Application.CanStreamedLevelBeLoaded(point.场景)) { owner.数据.ShowHint("目的地暂不可到达"); return false; }

            黑幕字幕.开始场景过渡(point.场景, SaveApplier.取场景显示名(point.场景), point.位置, point.落点名, 有坐标落点: string.IsNullOrEmpty(point.落点名));
            return true;
        }
        var destination = string.IsNullOrEmpty(point.落点名) ? null : FindLanding(owner.gameObject.scene, point.落点名);
        if (!string.IsNullOrEmpty(point.落点名) && !destination) { owner.数据.ShowHint("传送落点不存在"); return false; }
        owner.御风?.强制落地(); owner.骑手?.强制下坐骑(); owner.取消待切换();
        var cc = owner.GetComponent<CharacterController>(); bool enabled = cc && cc.enabled;
        if (enabled) cc.enabled = false;
        owner.transform.SetPositionAndRotation(destination ? destination.position : point.位置, Quaternion.Euler(0, destination ? destination.eulerAngles.y : point.朝向, 0));
        if (enabled) cc.enabled = true;
        Physics.SyncTransforms();
        Camera.main?.GetComponent<TopDownCamera>()?.SnapToTarget();
        return true;
    }
}
