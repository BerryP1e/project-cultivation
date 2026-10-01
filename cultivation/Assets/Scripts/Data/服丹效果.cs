using UnityEngine;

/// <summary>
/// **服丹效果** —— 让丹药能在背包里"使用"。
///
/// 数值不写在这个资产上，而是按 <see cref="丹方id"/> 去 <see cref="灵丹库"/> 反查那张丹方
/// （留空则退回用物品自己的 id）。于是"一味丹的配方 / 成功率 / 作用 / 药力"只有**一处定义**
/// （`灵丹定义.cs`），物品表那边填 `效果类型 = 服丹` + `效果参数id = 丹方id` 就够，
/// 不会出现"表里写 25、代码里写 30"的漂移。
///
/// 覆盖：`破境`（服下提升破境成功率）/ `加修为` / `回气血` / `回灵力`。
/// **`无` 不走这里**（例如回春丹，它的效果挂在物品自己的「属性增益」上）。
/// </summary>
[CreateAssetMenu(fileName = "服丹_", menuName = "修仙/物品效果/服丹", order = 15)]
public class 服丹效果 : 物品使用效果
{
    [Tooltip("要服的丹方 id（`灵丹定义.id`）。留空 = 用物品自己的 id")]
    public string 丹方id = "";

    灵丹定义 找丹(物品使用请求 请求)
    {
        string id = !string.IsNullOrEmpty(丹方id)
            ? 丹方id
            : (请求.物品 != null ? 请求.物品.物品id : "");
        return 灵丹库.取(id);
    }

    public override bool 能使用(物品使用请求 请求)
    {
        var 丹 = 找丹(请求);
        if (丹 == null) return false;
        if (丹.作用 == 灵丹作用.无) return false;
        if (请求.玩家 == null) return false;

        // 回复类：满的就不让吃（免得白费一味丹）
        if (丹.作用 == 灵丹作用.回气血 || 丹.作用 == 灵丹作用.回灵力)
        {
            var 命 = 取命(请求);
            if (命 == null) return false;
            if (丹.作用 == 灵丹作用.回气血 && 命.当前气血 >= 命.气血上限 - 0.01f) return false;
            if (丹.作用 == 灵丹作用.回灵力 && 命.当前灵气 >= 命.灵气上限 - 0.01f) return false;
        }

        // 加修为：总灵气已经被本级封顶了就不让吃（否则白吃）
        if (丹.作用 == 灵丹作用.加修为)
        {
            var 修 = 取修(请求);
            if (修 == null) return false;
            if (修.总灵气 >= 修.取累计(修.已解锁最高等级)) return false;
        }

        // 破境丹：必须是**当前大境界**对应的那一颗，而且已经嗑过就别再嗑
        if (丹.作用 == 灵丹作用.破境)
        {
            var 修 = 取修(请求);
            if (修 == null || 修.当前境界 == null) return false;
            if (修.当前境界.大境界 != 丹.对应大境界) return false;
            if (修.破境加成 > 0.001f) return false;      // 已经嗑着，别浪费
        }

        return true;
    }

    public override string 不能用原因(物品使用请求 请求)
    {
        var 丹 = 找丹(请求);
        if (丹 == null) return "这不是一味丹";
        switch (丹.作用)
        {
            case 灵丹作用.无: return "这味丹的效果走物品自身的使用效果";
            case 灵丹作用.回气血: return "气血已满";
            case 灵丹作用.回灵力: return "灵力已满";
            case 灵丹作用.加修为: return "当前境界的修为已攒满，先去破境";
            case 灵丹作用.破境:
                {
                    var 修 = 取修(请求);
                    if (修 == null || 修.当前境界 == null) return "读不到境界";
                    if (修.当前境界.大境界 != 丹.对应大境界)
                        return $"这是{丹.对应大境界}期专用丹，当前是{修.当前境界.大境界}期";
                    if (修.破境加成 > 0.001f) return "已经服过一颗了，先去破境";
                    return "现在不能服用";
                }
        }
        return "现在不能服用";
    }

    public override bool 使用(物品使用请求 请求)
    {
        var 丹 = 找丹(请求);
        if (丹 == null)
        {
            Debug.LogWarning("[服丹] 找不到丹方（物品「" + (请求.物品 != null ? 请求.物品.物品id : "?")
                             + "」，丹方id「" + 丹方id + "」）—— 检查物品表的「效果参数id」是否写错");
            return false;
        }

        switch (丹.作用)
        {
            case 灵丹作用.加修为:
                {
                    var 修 = 取修(请求);
                    if (修 == null) return false;
                    long 前 = 修.总灵气;
                    long 实际 = 修.加总灵气(丹.作用数值);
                    if (实际 <= 0) return false;
                    Debug.Log("[服丹] 「" + 丹.名 + "」：总灵气 " + 前 + " → " + 修.总灵气
                              + "（+" + 实际 + "，本级进度 " + (修.本级进度 * 100f).ToString("0.#") + "%）");
                    return true;
                }
            case 灵丹作用.回气血:
            case 灵丹作用.回灵力:
                {
                    var 命 = 取命(请求);
                    if (命 == null) return false;
                    float 实际 = 丹.作用 == 灵丹作用.回气血
                        ? 命.回复气血(丹.作用数值)
                        : 命.回复灵气(丹.作用数值);
                    if (实际 <= 0f) return false;
                    Debug.Log("[服丹] 「" + 丹.名 + "」："
                              + (丹.作用 == 灵丹作用.回气血 ? "气血" : "灵力")
                              + " +" + 实际.ToString("0.#"));
                    return true;
                }
            case 灵丹作用.破境:
                {
                    var 修 = 取修(请求);
                    if (修 == null || 修.当前境界 == null) return false;
                    var 级 = 修.当前境界;
                    float 基础 = Mathf.Clamp01(级.基础成功率);
                    float 目标 = Mathf.Clamp(级.服丹成功率, 基础, 1f);
                    // 加成 = 目标 − 基础。写进 PlayerCultivation，UI 直接读「当前破境成功率」，
                    // 所以成功率是**一个时时变化的值**，不是一个写死的展示。
                    修.设置破境加成(Mathf.Max(0f, 目标 - 基础));
                    Debug.Log("[服丹] 「" + 丹.名 + "」：破境成功率 "
                              + (基础 * 100f).ToString("0") + "% → " + (修.当前破境成功率 * 100f).ToString("0") + "%");
                    return true;
                }
        }

        Debug.LogWarning("[服丹] 「" + 丹.名 + "」的作用是 " + 丹.作用 + "，不该从背包使用");
        return false;
    }

    // ---- 找组件：先看玩家身上，再退回全场景找 ----
    //  ⚠️ 用 `== null` 判（Unity 的"假 null"只有它的 `==` 重载认得出），别用 `??`。

    static PlayerVitals 取命(物品使用请求 请求)
    {
        var v = 请求.玩家 != null ? 请求.玩家.GetComponent<PlayerVitals>() : null;
        if (v == null) v = Object.FindObjectOfType<PlayerVitals>();
        return v;
    }

    static PlayerCultivation 取修(物品使用请求 请求)
    {
        var c = 请求.玩家 != null ? 请求.玩家.GetComponent<PlayerCultivation>() : null;
        if (c == null) c = Object.FindObjectOfType<PlayerCultivation>();
        return c;
    }
}
