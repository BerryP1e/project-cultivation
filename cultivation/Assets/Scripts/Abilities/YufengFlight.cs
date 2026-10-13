using System;
using UnityEngine;

/// <summary>
/// 被动神通【凭虚御风】的运行时表现。
///
/// 规则：通过Tab行游轮盘起飞/落地；维持消耗、原生升降动画和对地高度照常。
/// 御风与坐骑可同时准备，但实际运行流程互斥，轮盘控制器负责先落地再切换。
///
/// ## 高度是「对地」的（用户定）
///
/// 悬浮高度 = **每帧向下探到的地面** + <see cref="飞行高度"/>，不是世界绝对高度。
/// 上坡下坡会跟着地形升降，始终维持这个离地高度；
/// **探不到地面（断崖 / 空洞）时沿用上一次探到的地面高度** —— 也就是原地维持一个固定高度。
/// （以前是记「起飞那一刻的 transform.y」，地形一变就贴地或悬空 ✗）
///
/// 本组件只负责「状态机 + 灵气消耗 + 高度」，移动速度由 PlayerController 读取，
/// 动画参数由 PlayerAnimationController 读取，互不耦合。
/// </summary>
public class YufengFlight : MonoBehaviour
{
    public enum FlightState
    {
        地面 = 0,
        升空 = 1,
        御风 = 2,
        落地 = 3,
    }

    [Header("神通来源")]
    [Tooltip("被动神通定义。填了就直接用它，否则按下面的 id 从面板数据里找")]
    public PassiveDivineAbility 御风神通;

    [Tooltip("凭虚御风在被动神通表里的 id")]
    public string 神通id = "ability_pingxu_yufeng";

    [Tooltip("数据源，用于判断该被动是否处于启用状态。留空则视为始终可用")]
    public UIPanelData 面板数据;

    // 旧场景字段保留以兼容序列化；不再读取独立切换键。
    [HideInInspector] public KeyCode 切换键 = KeyCode.None;
    [HideInInspector] public KeyCode 切换键2 = KeyCode.None;

    [Header("引用")]
    public PlayerVitals 灵气;
    public PlayerController 移动;

    [Header("消耗")]
    [Tooltip("御风期间每秒消耗的灵气")]
    public float 每秒消耗灵气 = 0.1f;

    [Header("飞行")]
    [Tooltip("御风时的移动速度（要比跑步快）")]
    public float 飞行速度 = 9f;

    [Tooltip("★ **对地悬浮高度（米）**：相对**脚下探到的地面**算，不是世界绝对高度。\n" +
             "上坡下坡会跟着地形升降，始终维持这个离地高度。")]
    public float 飞行高度 = 2.4f;

    [Tooltip("向下探地面能探多深（米）。**探不到就沿用上一次探到的地面高度**\n" +
             "（= 原地维持一个固定高度），所以飞在断崖 / 空洞上方不会一路掉下去")]
    public float 地面探测深度 = 60f;

    [Tooltip("升空动画时长（秒），到时间才进入悬浮")]
    public float 升空时长 = 0.85f;

    [Tooltip("落地动画时长（秒）")]
    public float 落地时长 = 0.70f;

    [Tooltip("升空/落地的高度过渡曲线")]
    public AnimationCurve 高度曲线 = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("调试")]
    public bool 打印状态切换 = false;

    [Tooltip("勾上就一直保持御风，方便调试 / 以后做剧情飞行")]
    public bool 强制御风 = false;

    /// <summary>
    /// **禁止切换御风**。骑乘坐骑期间由 <see cref="MountRider"/> 打开 ——
    /// 用户定的规则：**骑乘坐骑时无法开始御风，但也不会掉高度**（高度归坐骑管）。
    /// 只挡切换指令，已经在御风中的收尾流程照常走完。
    /// </summary>
    public bool 禁止切换 = false;

    /// <summary>当前状态</summary>
    public FlightState 状态 { get; private set; } = FlightState.地面;

    /// <summary>是否已经在御风悬浮中（可以正常飞行移动）</summary>
    public bool 御风中 => 状态 == FlightState.御风;

    /// <summary>
    /// **玩家「想飞」的开关**（按一下开、再按一下关）。
    /// 灵气耗尽 / 神通被停用 / 被强制落地时会自动关掉，不会"灵气一回来就自己又飞起来"。
    /// </summary>
    public bool 想飞 { get; private set; }

    /// <summary>是否处于整个御风流程里（升空/悬浮/落地都算）</summary>
    public bool 御风流程中 => 状态 != FlightState.地面;

    /// <summary>当前相对地面的高度偏移，PlayerController 用它控制升降</summary>
    public float 高度偏移 { get; private set; }

    /// <summary>
    /// **当前脚下的地面高度**（世界 Y）。每帧向下探一次；**探不到就保持上一次的值**。
    /// PlayerController 用「它 + <see cref="高度偏移"/>」算目标高度，
    /// YufengVfx 用「它 + 高度偏移」把风环踩在脚下。
    /// </summary>
    public float 地面高度 { get; private set; }

    /// <summary>状态变化事件（参数为新状态）</summary>
    public event Action<FlightState> 状态变化;

    /// <summary>
    /// 解析数据源。
    /// 必须按需解析：面板数据挂在 CharacterUI 上，而重建角色面板会把 CharacterUI 整个销毁重建，
    /// 手工在 Inspector 里接的引用届时会失效变成空。
    /// </summary>
    UIPanelData 解析面板数据()
    {
        if (面板数据 != null) return 面板数据;

        var ui = GameObject.Find("CharacterUI");
        if (ui != null) 面板数据 = ui.GetComponent<UIPanelData>();
        if (面板数据 == null) 面板数据 = FindObjectOfType<UIPanelData>();
        return 面板数据;
    }

    /// <summary>神通是否处于启用状态</summary>
    public bool 神通已启用
    {
        get
        {
            var d = 解析面板数据();
            if (d == null) return true;            // 实在找不到数据源就不拦着，先保证功能可用

            var ability = 取神通();
            if (ability == null) return true;      // 表里没有这条也不拦着
            return d.IsPassiveEnabled(ability);
        }
    }

    PassiveDivineAbility 取神通()
    {
        if (御风神通 != null) return 御风神通;

        var d = 解析面板数据();
        if (d == null) return null;
        if (d.神通 == null) return null;

        foreach (var a in d.神通)
            if (a is PassiveDivineAbility p && p.神通id == 神通id) { 御风神通 = p; return p; }
        return null;
    }

    /// <summary>每秒消耗：优先用神通表里的「维持消耗灵力」，没配就用面板上的值</summary>
    public float 实际每秒消耗
    {
        get
        {
            var a = 取神通();
            return a != null && a.维持消耗灵力 > 0f ? a.维持消耗灵力 : 每秒消耗灵气;
        }
    }

    void Awake()
    {
        if (灵气 == null) 灵气 = GetComponent<PlayerVitals>();
        if (移动 == null) 移动 = GetComponent<PlayerController>();
    }

    void Update()
    {
        if (移动 == null) return;

        读取切换输入();

        // ★ 高度是**对地**的：整个御风流程里每帧重新探脚下的地面。
        //   探不到（断崖 / 空洞）就沿用上一次的值 → 原地维持一个固定高度。
        //   放在状态机之前：这样升空 / 悬浮 / 落地三段用的是同一个地面基准。
        if (状态 != FlightState.地面) 维护地面高度();

        bool wantFly = (想飞 || 强制御风) && 神通已启用 && (灵气 == null || 灵气.有灵气)
            && !(GetComponent<MountRider>()?.骑乘中 ?? false);

        switch (状态)
        {
            case FlightState.地面:
                if (wantFly) 进入升空();
                else if (想飞 && !神通已启用) 想飞 = false;   // 神通被停用 → 开关也清掉
                break;

            case FlightState.升空:
                高度偏移 = 飞行高度 * 高度曲线.Evaluate(1f - Mathf.Clamp01(升空剩余 / Mathf.Max(0.01f, 升空时长)));
                升空剩余 -= Time.deltaTime;
                if (升空剩余 <= 0f) { 高度偏移 = 飞行高度; 切换到(FlightState.御风); }
                break;

            case FlightState.御风:
                高度偏移 = 飞行高度;
                // 持续消耗灵气；耗尽自动落地
                if (灵气 != null)
                {
                    float cost = 实际每秒消耗 * Time.deltaTime;
                    if (cost > 0f)
                    {
                        灵气.扣灵气直到零(cost);
                        if (!灵气.有灵气) { 想飞 = false; 进入落地(); break; }   // ★ 开关一起关掉
                    }
                }
                // 开关关了 / 神通被停用 → 落地
                if (!wantFly) { 想飞 = false; 进入落地(); }
                break;

            case FlightState.落地:
                高度偏移 = 飞行高度 * (1f - 高度曲线.Evaluate(1f - Mathf.Clamp01(落地剩余 / Mathf.Max(0.01f, 落地时长))));
                落地剩余 -= Time.deltaTime;
                if (落地剩余 <= 0f) { 高度偏移 = 0f; 切换到(FlightState.地面); }
                break;
        }
    }

    // 仅保留调试强制御风；输入由Tab轮盘统一管理。
    void 读取切换输入()
    {
        if (强制御风 && !(GetComponent<MountRider>()?.骑乘中 ?? false)) 想飞 = true;
    }

    /// <summary>
    /// 向下探一次脚下的地面。**探到才更新**，探不到就保持上一次的值 ——
    /// 也就是用户要的「检测不到地面时维持一个固定高度」。
    /// 返回这一次到底探没探到。
    /// </summary>
    bool 维护地面高度()
    {
        float y;
        if (!GroundProbe.向下取地面(transform.position, 地面探测深度, transform, out y)) return false;
        地面高度 = y;
        已探到地面 = true;
        return true;
    }

    /// <summary>
    /// **切换飞行**（按一下起飞、再按一下落地）。
    /// 抽成公开方法：既给按键用，也方便以后接 UI 按钮 / 剧情脚本 / 自动化测试。
    /// </summary>
    public void 切换()
    {
        if (强制御风) { 想飞 = true; return; }
        if (禁止切换) return;      // 骑乘坐骑期间不许起飞
        if (GetComponent<MountRider>()?.骑乘中 == true) return; // 同时准备，运行互斥

        // 升空 / 落地过渡中不响应 —— 免得刚起飞就被自己取消掉
        if (状态 == FlightState.升空 || 状态 == FlightState.落地) return;

        if (想飞) { 想飞 = false; }
        else
        {
            if (!神通已启用 || (灵气 != null && !灵气.有灵气)) return;   // 起飞条件不满足，这一下不算
            想飞 = true;
        }

        if (打印状态切换) Debug.Log("[凭虚御风] 切换 → " + (想飞 ? "起飞" : "落地"), this);
    }

    float 升空剩余;
    float 落地剩余;

    void 进入升空()
    {
        // 起飞那一刻先探一次脚下的地面；**探不到**（例如站在悬空平台边缘）就退回当前站位高度。
        // 之后每一帧都由 维护地面高度() 跟着地形走
        if (!维护地面高度()) { 地面高度 = transform.position.y; 已探到地面 = false; }

        升空剩余 = Mathf.Max(0.01f, 升空时长);
        高度偏移 = 0f;
        切换到(FlightState.升空);
    }

    /// <summary>本次御风流程里**有没有真探到过地面**（排查 / 自动化验证用：false = 正在靠"固定高度"兜底）</summary>
    public bool 已探到地面 { get; private set; }

    void 进入落地()
    {
        落地剩余 = Mathf.Max(0.01f, 落地时长);
        切换到(FlightState.落地);
    }

    void 切换到(FlightState s)
    {
        if (状态 == s) return;
        状态 = s;
        if (打印状态切换) Debug.Log("[凭虚御风] -> " + s, this);
        状态变化?.Invoke(s);
    }

    /// <summary>强制落地（例如面板打开、角色死亡时）。**连「想飞」的开关一起关掉** ——
    /// 否则关了面板 / 复活之后又自己飞起来。</summary>
    public void 强制落地()
    {
        想飞 = false;
        if (状态 == FlightState.地面) return;
        高度偏移 = 0f;
        切换到(FlightState.地面);
    }

    // ---- ASCII 别名 ----
    public FlightState State => 状态;
    public bool IsFlying => 御风中;
    public bool InFlightFlow => 御风流程中;
    public float HeightOffset => 高度偏移;
    public float GroundY => 地面高度;
    public bool AbilityEnabled => 神通已启用;
    public float SpiritCostPerSecond => 实际每秒消耗;
    public float FlySpeed { get => 飞行速度; set => 飞行速度 = value; }
    public float FlyHeight { get => 飞行高度; set => 飞行高度 = value; }
    public bool ForceFly { get => 强制御风; set => 强制御风 = value; }
    /// <summary>切换键按下的「想飞」开关（按一下开、再按一下关）</summary>
    public bool WantFly => 想飞;
    public void ForceLand() => 强制落地();
    public void ToggleFlight() => 切换();
}
