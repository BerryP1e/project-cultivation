using UnityEngine;

/// <summary>法环固定到胸骨，斜眼处于环心；粒子光束与伤害同时指向当前锁定单位。</summary>
public class DevilEyeAbility : MonoBehaviour
{
    public UIPanelData 面板数据;
    public GameObject 法环 { get; private set; }
    Transform 挂点;
    PassiveDivineAbility 神通;
    NpcTargeting 索敌;
    PlayerCombatStats 属性;
    PlayerVitals 生命;
    GameObject 激光;
    float 下次攻击, 光束结束;

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
                if (技能.结算方式 == PassiveSkillKind.斜眼) { 神通 = 技能; break; }
        if (神通 == null || 生命 == null || 生命.IsDead) { 清理(); return; }
        var 胸 = AbilityVfxUtility.胸骨(transform);
        if (法环 == null || 挂点 != 胸 || !挂点.gameObject.activeInHierarchy)
        {
            清理(); 挂点 = 胸;
            法环 = AbilityVfxUtility.生成("Abilities/DevilEyeHalo", 胸, 1f);
            if (法环 == null) return;
            // 用玩家方向定义背部，转换到当前胸骨空间后固定；不逐帧重新纠正，避免与动作争抢。
            float 身高 = AbilityVfxUtility.身体边界(transform).size.y;
            法环.transform.position = AbilityVfxUtility.命中点(transform) + Vector3.up * 身高 * .17f - transform.forward * 身高 * .18f;
            法环.transform.rotation = transform.rotation;
            float 世界缩放 = 身高 / 2f;
            Vector3 父缩放 = 胸.lossyScale;
            法环.transform.localScale = new Vector3(世界缩放 / Mathf.Max(.001f, Mathf.Abs(父缩放.x)),
                世界缩放 / Mathf.Max(.001f, Mathf.Abs(父缩放.y)), 世界缩放 / Mathf.Max(.001f, Mathf.Abs(父缩放.z)));
            var 眼 = AbilityVfxUtility.生成(神通.特效资源路径, 法环.transform, .06f, true);
            if (眼 != null) 眼.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        var 目标 = 索敌 != null ? 索敌.LockedNpc : null;
        bool 有目标 = 目标 != null && !目标.IsDead && 索敌.IsInSenseRange(目标)
            && Vector3.Distance(transform.position, 目标.transform.position) <= 神通.范围;
        if (激光 != null)
        {
            if (!有目标 || Time.time >= 光束结束) { Destroy(激光); 激光 = null; }
            else AbilityVfxUtility.对准光束(激光, 法环.transform.position, AbilityVfxUtility.命中点(目标.transform), .025f);
        }
        if (!有目标 || 属性 == null || Time.time < 下次攻击) return;
        下次攻击 = Time.time + Mathf.Max(.1f, 神通.攻击间隔);
        目标.ReceiveAttack(属性, new AttackSpec(DamageNature.特殊, AttackKind.被动神通, false, 神通.伤害倍率));
        激光 = AbilityVfxUtility.生成("Abilities/DevilEyeLaser", null, 1f);
        光束结束 = Time.time + .4f;
        AbilityVfxUtility.对准光束(激光, 法环.transform.position, AbilityVfxUtility.命中点(目标.transform), .025f);
    }
    void 清理()
    {
        if (法环 != null) Destroy(法环);
        if (激光 != null) Destroy(激光);
        法环 = null; 激光 = null;
    }
    void OnDisable() { 清理(); }
}
