using UnityEngine;

/// <summary>
/// 百眼魔君（<c>NpcAiBaiYanMoJun</c>）—— 飞弹怪。用户口述：a1 动画 **35.5%** 从右手骨骼发射飞弹（特殊）；a2 动画 **64%** 同样从右手骨骼发射飞弹（特殊）。
/// 共用逻辑在 <see cref="飞弹妖魔Ai"/> 里，这里只填「这一只怪」的数和量测值。
/// </summary>
public class NpcAiBaiYanMoJun : 飞弹妖魔Ai
{
    protected override void 取默认参数()
    {
        // 站位：丢飞弹的站远处打（和白鹿精一个口径）
        站位距离 = 12f;
        飞弹速度 = 18f;
        消灭距离 = 22f;
        拦截层 = 0;      // 0 = 现在谁都拦不住；等灵阵/法宝做出来把它们放进这一层

        // ---- 飞弹特效三件套（主题 StarlightFairy：弹道 + 命中 + 枪口闪光）----
        弹道路径 = 飞弹库 + 弹道子目录 + "StarlightFairy_Projectile_Only";
        命中路径 = 飞弹库 + 命中的子目录 + "StarlightFairy_Impact";
        闪光路径 = 飞弹库 + 闪光子目录 + "StarlightFairy_Flash";

        // ---- a1 普攻：动画 35.5% 出弹 ----
        普攻挂点 = "Bip01 R Hand";
        普攻挂点偏移 = new Vector3(0f, 0f, 0f);
        普攻进度 = 0.355f;
        普攻属性 = DamageNature.特殊;
        普攻冷却 = 3f;

        // ---- a2 主动神通：动画 64% 出弹 ----
        有神通 = true;
        神通挂点 = "Bip01 R Hand";
        神通挂点偏移 = new Vector3(0f, 0f, 0f);
        神通进度 = 0.64f;
        神通属性 = DamageNature.特殊;
        神通冷却 = 8f;

        base.取默认参数();
    }
}
