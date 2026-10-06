using UnityEngine;

/// <summary>邪眼在背部法环环心；轻微快速插值跟随，光束与伤害指向当前锁定单位。</summary>
public class DevilEyeAbility : MonoBehaviour
{
    public UIPanelData 面板数据;
    public GameObject 法环 { get; private set; }
    [Header("用户标定的背部法环姿态（玩家根空间）")]
    public Vector3 法环位置 = new Vector3(-.04910f, 2.23887f, -.91732f);
    public Vector3 法环旋转 = new Vector3(36.46175f, 359.32650f, 359.45850f);
    public float 法环尺寸 = 1.25768f;
    Transform 挂点;
    PassiveDivineAbility 神通;
    NpcTargeting 索敌;
    PlayerCombatStats 属性;
    PlayerVitals 生命;
    GameObject 激光;
    AbilityPreciseShotVfx 射击;
    float 下次攻击, 光束结束;
    Vector3 挂点偏移, 跟随位置;
    float 跟随限幅;

    void Awake()
    {
        var 装载器 = GetComponent<PlayerAbilityLoader>();
        面板数据 = 装载器 != null ? 装载器.面板数据 : FindObjectOfType<UIPanelData>();
        索敌 = GetComponent<NpcTargeting>(); 属性 = GetComponent<PlayerCombatStats>(); 生命 = GetComponent<PlayerVitals>();
    }
    void OnEnable() { 下次攻击 = Time.time + 1f; }
    void LateUpdate()
    {
        神通 = null;
        if (面板数据 != null)
            foreach (var 技能 in 面板数据.GetEnabledPassives())
                if (技能.结算方式 == PassiveSkillKind.邪眼) { 神通 = 技能; break; }
        if (神通 == null || 生命 == null || 生命.IsDead) { 清理(); return; }
        var 胸 = AbilityVfxUtility.胸骨(transform);
        if (法环 == null || 挂点 != 胸 || !挂点.gameObject.activeInHierarchy)
        {
            清理(); 挂点 = 胸;
            法环 = AbilityVfxUtility.生成("Abilities/DevilEyeHalo", 胸, 1f);
            if (法环 == null) return;
            // 用玩家方向定义背部，转换到当前胸骨空间后固定；不逐帧重新纠正，避免与动作争抢。
            float 身高 = AbilityVfxUtility.身体边界(transform).size.y;
            法环.transform.position = transform.TransformPoint(法环位置);
            法环.transform.rotation = transform.rotation * Quaternion.Euler(法环旋转);
            float 世界缩放 = 法环尺寸 * Mathf.Abs(transform.lossyScale.y);
            Vector3 父缩放 = 胸.lossyScale;
            法环.transform.localScale = new Vector3(世界缩放 / Mathf.Max(.001f, Mathf.Abs(父缩放.x)),
                世界缩放 / Mathf.Max(.001f, Mathf.Abs(父缩放.y)), 世界缩放 / Mathf.Max(.001f, Mathf.Abs(父缩放.z)));
            var 眼 = AbilityVfxUtility.生成(神通.特效资源路径, 法环.transform, .06f, true);
            if (眼 != null) 眼.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AbilityVfxUtility.生成("Abilities/DevilEyePupil", 法环.transform, 1f);
            挂点偏移 = 法环.transform.localPosition;
            跟随位置 = 法环.transform.position;
            跟随限幅 = 身高 * .12f;
        }
        Vector3 应在 = 挂点.TransformPoint(挂点偏移);
        跟随位置 = Vector3.Distance(跟随位置, 应在) > 2f ? 应在
            : Vector3.Lerp(跟随位置, 应在, 1f - Mathf.Exp(-14f * Time.deltaTime));
        跟随位置 = 应在 + Vector3.ClampMagnitude(跟随位置 - 应在, 跟随限幅);
        法环.transform.position = 跟随位置;
        var 目标 = 索敌 != null ? 索敌.LockedNpc : null;
        bool 有目标 = 目标 != null && !目标.IsDead && 索敌.IsInSenseRange(目标)
            && Vector3.Distance(transform.position, 目标.transform.position) <= 神通.范围;
        if (激光 != null)
        {
            if (!有目标 || Time.time >= 光束结束) { Destroy(激光); 激光 = null; }
            else if (射击 != null) 射击.对准(法环.transform.position, AbilityVfxUtility.命中点(目标.transform));
        }
        if (!有目标 || 属性 == null || Time.time < 下次攻击) return;
        下次攻击 = Time.time + Mathf.Max(.1f, 神通.攻击间隔);
        目标.ReceiveAttack(属性, new AttackSpec(DamageNature.特殊, AttackKind.被动神通, false, 神通.伤害倍率));
        激光 = AbilityVfxUtility.生成("Abilities/DevilEyeLaser", null, .12f);
        if (激光 != null)
        {
            射击 = 激光.AddComponent<AbilityPreciseShotVfx>();
            射击.对准(法环.transform.position, AbilityVfxUtility.命中点(目标.transform));
        }
        光束结束 = Time.time + 2.4f;
    }
    void 清理()
    {
        if (法环 != null) Destroy(法环);
        if (激光 != null) Destroy(激光);
        法环 = null; 激光 = null; 射击 = null;
    }
    void OnDisable() { 清理(); }
}
