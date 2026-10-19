using UnityEngine;

/// <summary>御风闪避；位移只从 PlayerController 的碰撞移动入口执行。</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class PlayerFlightDodge : MonoBehaviour
{
    [Min(.1f)] public float 距离 = 6f;
    [Min(.1f)] public float 时长 = .45f;
    [Min(0)] public float 冷却 = .9f;
    [Range(.2f, .8f)] public float 动作衔接起点 = .45f;
    public FlightDodgeAnimations 动作;
    [Header("擦身攻击反馈")]
    [Min(0)] public float 慢放时长 = .3f;
    [Range(.05f, 1)] public float 慢放倍率 = .2f;
    [Range(0, .2f)] public float 特写幅度 = .08f;
    [Min(0)] public float 擦身判定半径 = 1.25f;
    public Vector3 擦身判定中心 => transform.position + Vector3.up * PlayerTarget.判定高度;

    /// <summary>临时的大判定球，仅用于慢镜，不扩大实体碰撞、扣血或拦截范围。</summary>
    public bool 检查擦身轨迹(Vector3 from, Vector3 to, float attackRadius = 0)
    {
        if (!伤害免疫 || feedbackPlayed) return false;
        var segment = to - from;
        float t = segment.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(擦身判定中心 - from, segment) / segment.sqrMagnitude) : 0;
        float radius = Mathf.Max(0, 擦身判定半径) + Mathf.Max(0, attackRadius);
        if ((from + segment * t - 擦身判定中心).sqrMagnitude > radius * radius) return false;
        收到擦身攻击();
        return feedbackPlayed;
    }
    public void 检查擦身网格(Bounds weapon)
    {
        if (伤害免疫 && (weapon.ClosestPoint(擦身判定中心) - 擦身判定中心).sqrMagnitude <= 擦身判定半径 * 擦身判定半径) 收到擦身攻击();
    }
    public int 擦身触发次数 { get; private set; }
    public bool 闪避中 { get; private set; }
    public Vector3 方向 { get; private set; }
    public float 进度 => 闪避中 ? Mathf.Clamp01(elapsed / duration) : 0;
    public float 冷却剩余 => Mathf.Max(0, availableAt - Time.time);
    public bool 伤害免疫 => 闪避中 && 环境允许 && actions && actions.闪避动作中 && actions.当前动作 == playing;

    PlayerController movement;
    PlayerAnimationController actions;
    PlayerVitals vitals;
    演出锁 cutscene;
    AnimationClip playing;
    UIPanelData panel;
    float elapsed, duration, distance, availableAt;
    bool feedbackPlayed;
    FlightDodgeVfx vfx;

    bool 环境允许 => isActiveAndEnabled && movement && movement.isActiveAndEnabled
        && movement.御风 && movement.御风.isActiveAndEnabled && movement.御风.御风中
        && !(movement.坐骑 && (movement.坐骑.骑乘中 || movement.坐骑.过渡中))
        && !(vitals && vitals.已死亡) && !UiEscRegistry.SceneInputBlocked
        && !(cutscene && cutscene.正在锁) && !黑幕字幕.演出中 && !DialogueUI.正在显示 && Time.timeScale > 0;

    void Resolve()
    {
        if (!movement) movement = GetComponent<PlayerController>();
        if (!actions) actions = GetComponent<PlayerAnimationController>();
        if (!vitals) vitals = GetComponent<PlayerVitals>();
        if (!cutscene) cutscene = GetComponent<演出锁>();
        if (!动作) 动作 = Resources.Load<FlightDodgeAnimations>(FlightDodgeAnimations.ResourcePath);
        if (!vfx) vfx = GetComponent<FlightDodgeVfx>() ?? gameObject.AddComponent<FlightDodgeVfx>();
    }

    /// <summary>在普通移动前读取空格，方向沿用相机相对的 WASD/方向键。</summary>
    public void 读取输入()
    {
        Resolve();
        if (闪避中 && !环境允许) 取消();
        if (movement && Input.GetKeyDown(KeyCode.Space)) 尝试闪避(movement.输入世界方向());
    }
    void Update() => 读取输入();

    public void 检查状态() { if (闪避中 && !环境允许) 取消(); }

    /// <summary>零向量是相对视觉朝向后闪；可打断普攻，已独占的引导施法需先结束。</summary>
    public bool 尝试闪避(Vector3 worldDirection)
    {
        Resolve();
        if (!环境允许 || 闪避中 || 冷却剩余 > 0 || CombatHitStop.正在顿帧(transform)
            || !actions || !actions.isActiveAndEnabled || actions.施法占用中 || !动作) return false;
        var facing = transform.rotation * Quaternion.Euler(0, -movement.visualYawOffset, 0);
        worldDirection.y = 0;
        var direction = worldDirection.sqrMagnitude > .0001f ? worldDirection.normalized : -(facing * Vector3.forward);
        direction.y = 0; direction.Normalize();
        var local = Quaternion.Inverse(facing) * direction;
        var kind = local.z > .5f ? FlightDodgeDirection.Forward : local.z < -.5f ? FlightDodgeDirection.Back : local.x > 0 ? FlightDodgeDirection.Right : FlightDodgeDirection.Left;
        if (!panel) panel = FindObjectOfType<UIPanelData>();
        var clip = 动作.取(kind, panel && panel.当前功法 ? panel.当前功法.功法id : "");
        if (!clip) return false;
        duration = Mathf.Max(.1f, 时长); distance = Mathf.Max(.1f, 距离);
        if (!actions.播闪避(clip, duration)) return false;
        playing = clip; 方向 = direction; elapsed = 0; 闪避中 = true;
        feedbackPlayed = false;
        vfx.开始();
        availableAt = Time.time + Mathf.Max(duration, 冷却);
        return true;
    }

    /// <summary>按累计进度取增量，总长固定且有起步/收势，不受移速和攻速倍率影响。</summary>
    public Vector3 推进(float dt)
    {
        if (!闪避中) return Vector3.zero;
        if (!环境允许 || !actions || actions.当前动作 != playing) { 取消(); return Vector3.zero; }
        if (CombatHitStop.正在顿帧(transform)) return Vector3.zero;
        float before = Mathf.Clamp01(elapsed / duration);
        elapsed = Mathf.Min(duration, elapsed + Mathf.Max(0, dt));
        float after = Mathf.Clamp01(elapsed / duration);
        return 方向 * distance * (Travel(after) - Travel(before));
    }
    static float Travel(float t) => t - .08f * Mathf.Sin(2 * Mathf.PI * t);

    public void 准备衔接(Vector3 direction) => actions.衔接闪避(playing, direction, Mathf.Max(0, duration - elapsed));

    /// <summary>真实攻击接触被闪避免疫挡住时调用；同次闪避仅一次，不消耗护盾或伤害随机数。</summary>
    public void 收到擦身攻击()
    {
        if (!伤害免疫 || feedbackPlayed || 慢放时长 <= 0 || (vitals && (vitals.调试无敌 || vitals.保护中 || vitals.过场保护))) return;
        if (!DodgeNearMissFocus.触发(this, 慢放时长, 慢放倍率, 特写幅度)) return;
        feedbackPlayed = true; 擦身触发次数++;
    }

    /// <summary>碰撞移动完成后关闭免疫，避免同帧叠加普通位移。</summary>
    public void 完成本帧() { if (闪避中 && elapsed >= duration) 取消(); }
    public void 取消()
    {
        闪避中 = false;
        if (actions) actions.结束闪避(playing);
        if (vfx) vfx.停止();
        if (!环境允许) DodgeNearMissFocus.取消(this);
        playing = null; elapsed = 0;
    }
    void OnDisable() => 取消();
}
