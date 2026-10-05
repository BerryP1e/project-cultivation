using UnityEngine;

/// <summary>禁锢锁链不造成伤害。再次施放、灵力耗尽、死亡、卸下或锁定变化都释放。</summary>
public class BindingChainSkillRunner : MonoBehaviour
{
    ActiveSkillCaster 施法者;
    ActiveDivineAbility 神通;
    NpcInstance 目标;
    NpcMovementLock 移动锁;
    int 槽位;
    bool 已取消;
    public float 每秒消耗 { get; private set; }

    public static float 计算消耗(float 基础, float 每级倍率, float 玩家等级, float 目标等级)
        => Mathf.Max(0f, 基础) * (1f + Mathf.Max(0f, 每级倍率) * Mathf.Abs(目标等级 - 玩家等级));

    public void 初始化(ActiveSkillCaster 玩家, ActiveDivineAbility 定义, NpcInstance 锁定, int 格)
    {
        施法者 = 玩家; 神通 = 定义; 目标 = 锁定; 槽位 = 格;
        if (目标 == null) { 取消(); return; }
        移动锁 = 目标.GetComponent<NpcMovementLock>() ?? 目标.gameObject.AddComponent<NpcMovementLock>();
        移动锁.加锁(this);
        var 边界 = AbilityVfxUtility.身体边界(目标.transform);
        transform.position = 边界.center;
        var 特效 = AbilityVfxUtility.生成(神通.特效资源路径, transform, Mathf.Clamp(边界.size.y / 14f, .04f, .5f));
        if (特效 != null) 特效.AddComponent<AbilityBindingChainVfx>();
    }

    void Update()
    {
        if (已取消) return;
        if (施法者 == null || !施法者.isActiveAndEnabled || 施法者.生命 == null || 施法者.生命.IsDead
            || 目标 == null || 目标.IsDead || 施法者.槽位内容(槽位) != 神通
            || 施法者.目标管理器 == null || 施法者.目标管理器.LockedNpc != 目标
            || (神通.范围 > 0f && Vector3.Distance(施法者.transform.position, 目标.transform.position) > 神通.范围))
        { 取消(); return; }
        var 修炼 = 施法者.GetComponent<PlayerCultivation>();
        float 目标等级 = 目标.已等级补正 ? 目标.当前补正等级 : 目标.定义 != null ? 目标.定义.境界 : 1f;
        每秒消耗 = 计算消耗(神通.维持消耗灵力, 神通.每级差消耗倍率, 修炼 != null ? 修炼.等级 : 1f, 目标等级);
        float 消耗 = 每秒消耗 * Time.deltaTime;
        if (施法者.生命.当前灵气 <= 消耗)
        {
            施法者.生命.扣灵气(施法者.生命.当前灵气);
            取消(); return;
        }
        施法者.生命.扣灵气(消耗);
        transform.position = AbilityVfxUtility.命中点(目标.transform);
    }

    public void 取消()
    {
        if (已取消) return;
        已取消 = true;
        if (移动锁 != null) 移动锁.解锁(this);
        Destroy(gameObject);
    }
    void OnDisable() { if (移动锁 != null) 移动锁.解锁(this); }
}
