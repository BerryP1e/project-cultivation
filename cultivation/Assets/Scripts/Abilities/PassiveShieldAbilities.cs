using System.Collections.Generic;
using UnityEngine;

/// <summary>四种护罩共用一个受击入口。各减伤相乘；绝对护罩优先挡住整次真实伤害。</summary>
public class PassiveShieldAbilities : MonoBehaviour
{
    public UIPanelData 面板数据;
    readonly Dictionary<PassiveDivineAbility, GameObject> 特效 = new Dictionary<PassiveDivineAbility, GameObject>();
    float 下次检查;
    const float 尺寸倍率 = 1.35f;
    bool 尺寸已确定;
    float 固定缩放;
    Vector3 固定位置;
    public int 剩余棱柱 => 面板数据 != null ? Mathf.Clamp(面板数据.绝对护罩剩余次数, 0, 4) : 0;

    void Awake()
    {
        var 装载器 = GetComponent<PlayerAbilityLoader>();
        面板数据 = 装载器 != null ? 装载器.面板数据 : FindObjectOfType<UIPanelData>();
    }
    public float 过滤伤害(float 伤害, DamageNature 属性)
    {
        if (面板数据 == null || 伤害 <= 0f) return 伤害;
        var 启用 = 面板数据.GetEnabledPassives();
        foreach (var 技能 in 启用)
            if (技能.结算方式 == PassiveSkillKind.绝对护罩 && 剩余棱柱 > 0)
            {
                面板数据.绝对护罩剩余次数--;
                更新棱柱();
                return 0f;
            }
        foreach (var 技能 in 启用)
        {
            bool 有效 = 技能.结算方式 == PassiveSkillKind.双重护罩
                || (属性 == DamageNature.物理 && 技能.结算方式 == PassiveSkillKind.物理护罩)
                || (属性 == DamageNature.特殊 && 技能.结算方式 == PassiveSkillKind.特殊护罩);
            if (有效) 伤害 *= 1f - Mathf.Clamp01(技能.减伤比例);
        }
        return 伤害;
    }

    void Update()
    {
        if (Time.unscaledTime < 下次检查) return;
        下次检查 = Time.unscaledTime + .2f;
        if (面板数据 == null) return;
        var 启用 = 面板数据.GetEnabledPassives();
        var 删除 = new List<PassiveDivineAbility>();
        foreach (var 对 in 特效)
            if (!启用.Contains(对.Key)) { if (对.Value != null) Destroy(对.Value); 删除.Add(对.Key); }
        foreach (var 技能 in 删除) 特效.Remove(技能);
        确定尺寸();
        foreach (var 技能 in 启用)
        {
            if (技能.结算方式 < PassiveSkillKind.物理护罩 || 特效.ContainsKey(技能)) continue;
            特效[技能] = AbilityVfxUtility.生成(技能.特效资源路径, transform, 固定缩放, true);
        }
        foreach (var 对 in 特效)
            if (对.Value != null)
            {
                对.Value.transform.localScale = Vector3.one * 固定缩放;
                对.Value.transform.localPosition = 固定位置;
            }
        更新棱柱();
    }

    void 确定尺寸()
    {
        if (尺寸已确定) return;
        // 动画中的蒙皮 bounds 随迈步/转身变化，不能拿它逐帧改变罩体大小或中心。
        // 使用角色根上的稳定碰撞体，护罩位置和缩放固定在角色根空间。
        var 胶囊 = GetComponent<CapsuleCollider>();
        float 身高;
        Vector3 中心;
        if (胶囊 != null && 胶囊.direction == 1)
        {
            身高 = 胶囊.height;
            中心 = 胶囊.center;
        }
        else
        {
            var 身体 = AbilityVfxUtility.身体边界(transform);
            身高 = 身体.size.y / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.y));
            中心 = transform.InverseTransformPoint(身体.center);
        }
        固定缩放 = Mathf.Clamp(身高 / 4f, .15f, 1.5f) * 尺寸倍率;
        固定位置 = 中心 - Vector3.up * 固定缩放;
        尺寸已确定 = true;
    }

    void 更新棱柱()
    {
        foreach (var 对 in 特效)
        {
            if (对.Key.结算方式 != PassiveSkillKind.绝对护罩 || 对.Value == null) continue;
            var 棱柱 = 对.Value.transform.Find("Effect_09_Crystals");
            if (棱柱 != null)
                for (int i = 0; i < 棱柱.childCount; i++) 棱柱.GetChild(i).gameObject.SetActive(i < 剩余棱柱);
            对.Value.SetActive(剩余棱柱 > 0);
        }
    }
    void OnDisable()
    {
        foreach (var 对 in 特效) if (对.Value != null) Destroy(对.Value);
        特效.Clear();
    }
}
