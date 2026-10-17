using UnityEngine;

public enum TraversalAction { None, Flight, Mount, Pressure, Clone, SpaceShift }

/// <summary>行游指令复用御风/骑乘状态机。装备可共存，切换必须等另一流程落地完成。</summary>
[DisallowMultipleComponent]
public sealed class PlayerTraversalController : MonoBehaviour
{
    public UIPanelData 面板数据;
    TraversalAction pending;
    MountDefinition pendingMount;
    float deadline;
    public TraversalAction 待切换 => pending;
    public YufengFlight 御风 => GetComponent<YufengFlight>();
    public MountRider 骑手 => GetComponent<MountRider>();
    public UIPanelData 数据
    {
        get
        {
            if (!面板数据)
            {
                var local = GetComponent<UIPanelData>(); var loader = GetComponent<PlayerAbilityLoader>();
                if (local) 面板数据 = local;
                else if (loader && loader.面板数据) 面板数据 = loader.面板数据;
                else if (御风 && 御风.面板数据) 面板数据 = 御风.面板数据;
                else if (骑手 && 骑手.面板数据) 面板数据 = 骑手.面板数据;
                else 面板数据 = FindObjectOfType<UIPanelData>();
            }
            return 面板数据;
        }
    }
    bool Alive => !(GetComponent<PlayerVitals>()?.IsDead ?? false);
    bool ActionBusy => GetComponent<PlayerAnimationController>() is PlayerAnimationController a && (a.施法占用中 || a.动作播放中)
        || (GetComponent<PlayerFlightDodge>()?.闪避中 ?? false);
    bool Transitioning => pending != TraversalAction.None || (骑手?.过渡中 ?? false)
        || (御风 && (御风.状态 == YufengFlight.FlightState.升空 || 御风.状态 == YufengFlight.FlightState.落地
            || (御风.想飞 && !御风.御风中)));

    public bool 可用(TraversalAction action, out string reason)
    {
        reason = "";
        if (action == TraversalAction.None) return false;
        if (!Alive) { reason = "无法使用"; return false; }
        if (ActionBusy) { reason = "动作进行中"; return false; }
        if (Transitioning) { reason = "正在切换"; return false; }
        var data = 数据;
        if (action == TraversalAction.Pressure)
        {
            var passives = GetComponent<CultivationPassiveAbilities>();
            if (!passives) { reason = "尚未启用灵势威压"; return false; }
            return passives.CanPressure(out reason);
        }
        if (action == TraversalAction.Clone || action == TraversalAction.SpaceShift)
        {
            if (!CultivationPassiveAbilities.Find(data, action == TraversalAction.Clone ? CultivationPassiveAbilities.CloneId : CultivationPassiveAbilities.SpaceId))
            { reason = "尚未启用"; return false; }
            if (action == TraversalAction.Clone) { reason = "身外化身暂未开放"; return false; }
            if (黑幕字幕.有幕在显示 || DialogueUI.正在显示 || (GetComponent<演出锁>()?.正在锁 ?? false))
            { reason = "当前无法腾挪"; return false; }
            return true;
        }
        if (action == TraversalAction.Flight)
        {
            if (御风 && 御风.御风中) return true;
            var ability = data ? data.取御风神通() : null;
            if (!ability || !data.已获得被动(ability)) { reason = "尚未掌握"; return false; }
            var vitals = GetComponent<PlayerVitals>();
            if (vitals && !vitals.有灵气) { reason = "灵力不足"; return false; }
            if (骑手 && 骑手.骑乘中 && !骑手.允许下坐骑) { reason = "无法下坐骑"; return false; }
            return true;
        }
        if (骑手 && 骑手.已骑稳)
        {
            if (!骑手.允许下坐骑) { reason = "无法下坐骑"; return false; }
            return true;
        }
        var mount = data ? data.当前坐骑 : null;
        if (!mount || !data.已学坐骑.Contains(mount.坐骑id)) { reason = "未装备坐骑"; return false; }
        var cultivation = GetComponent<PlayerCultivation>();
        if (cultivation && cultivation.等级 < mount.修炼门槛) { reason = "境界不足"; return false; }
        if (!mount.加载模型()) { reason = "坐骑不可用"; return false; }
        if (御风 && 御风.强制御风) { reason = "当前需保持御风"; return false; }
        return true;
    }

    public bool 请求(TraversalAction action, Object inputOwner = null)
    {
        string reason = "";
        if (UiEscRegistry.SceneInputBlockedByOthers(inputOwner) || !可用(action, out reason))
        { if (!string.IsNullOrEmpty(reason)) 数据?.ShowHint(reason); return false; }
        if (action == TraversalAction.Pressure) return GetComponent<CultivationPassiveAbilities>().Pressure();
        if (action == TraversalAction.SpaceShift) { UISpatialTravelMenu.Open(this, inputOwner as PlayerTraversalWheel); return true; }
        if (action == TraversalAction.Flight)
        {
            if (御风 && 御风.御风中) { 御风.切换(); return true; }
            var ability = 数据.取御风神通();
            if (!数据.IsPassiveEnabled(ability)) 数据.SetPassiveEnabled(ability, true);
            GetComponent<PlayerAbilityLoader>()?.Refresh();
            var flight = 御风;
            if (!flight || !flight.isActiveAndEnabled) { 数据.ShowHint("御风尚未就绪"); return false; }
            var movement = GetComponent<PlayerController>();
            if (movement) { movement.御风 = flight; flight.移动 = movement; }
            flight.面板数据 = 数据;
            if (骑手) 骑手.御风 = flight;
            GetComponent<PlayerAnimationController>()?.绑定御风(flight);
            if (骑手 && 骑手.已骑稳)
            { pending = action; deadline = Time.time + 12; 骑手.下坐骑(); }
            else flight.切换();
        }
        else
        {
            if (骑手 && 骑手.已骑稳) { 骑手.下坐骑(); return true; }
            var rider = 骑手;
            if (!rider || !rider.isActiveAndEnabled) { 数据.ShowHint("坐骑尚未就绪"); return false; }
            rider.面板数据 = 数据;
            if (御风 && 御风.御风中)
            {
                pending = action; pendingMount = 数据.当前坐骑; deadline = Time.time + 12;
                御风.切换();
            }
            else rider.上坐骑();
        }
        return true;
    }
    void Update()
    {
        SpatialTravel.CheckArrival(transform, 数据);
        if (pending == TraversalAction.None) return;
        if (!Alive || Time.time > deadline || !数据 || GetComponent<PlayerController>()?.isActiveAndEnabled != true)
        { 取消待切换(); return; }
        if (UiEscRegistry.SceneInputBlocked || ActionBusy) return;
        if (pending == TraversalAction.Flight)
        {
            if (骑手 && 骑手.骑乘中) return;
            var flight = 御风;
            取消待切换();
            if (flight && flight.isActiveAndEnabled && flight.神通已启用) flight.切换();
        }
        else
        {
            if (数据.当前坐骑 != pendingMount) { 取消待切换(); return; }
            if (御风 && 御风.御风流程中) return;
            取消待切换();
            if (骑手 && 骑手.isActiveAndEnabled) 骑手.上坐骑();
        }
    }
    public void 取消待切换() { pending = TraversalAction.None; pendingMount = null; }
    void OnDisable() => 取消待切换();
}
