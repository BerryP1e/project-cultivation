using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把**竖直方向的抖动/位移**从动画片段里压平 —— **姿态、旋转、水平位移全都不动**。
///
/// ## 为什么需要它
///
/// 用户 2026-09-29 报的问题：`普攻_远程_01`（太虚炼气诀的普攻动作，很多功法/神通都在用它）
/// 会让人**上下晃**。实测（3C_Testbed，站立待机髋骨 Y = 0.872m）：
///
/// | 项 | 实测 |
/// |---|---|
/// | 起手过渡最高 | 1.766m（比待机窜高 **0.894m**） |
/// | 动作稳态（0.45~1.2s） | 1.529 ~ 1.785 → **动作内上下抖 0.256m** |
/// | 稳态悬空 | ≈0.785m（这个片段是拿**嫦娥的浮空施法动作**重定向来的，姿态本来就是悬空的） |
///
/// **为什么不能用"偏移"糊**：给模型/角色加一个固定的 Y 偏移，在**骑乘 / 御风**这些
/// 基准高度不同的状态下就错了（用户明确点名）。所以必须在**片段数据**里解决 ——
/// 改一次，所有用它的功法/神通、所有状态下都一致。
///
/// ## 做法
///
/// 逐帧采样**髋骨的世界 Y**（人体骨架的"身体高度"），把它的**波动**反相
/// 叠加到 Humanoid 的根位移 `RootT.y` 上：
///
/// ```
/// 新 RootT.y(t) = 原 RootT.y(t) − (髋骨Y(t) − 髋骨Y平均值)
/// ```
///
/// `RootT.y` 是"整体平移"，只会把**整具骨架**一起上下挪，**不碰任何一根骨头的旋转** ——
/// 所以动作（手臂、身体、出招姿态）**一模一样**，只是身体不再上下晃。
/// 水平位移（`RootT.x` / `RootT.z`，那 0.43m 的前冲）也**原样保留**。
///
/// 取"平均值"当目标高度 = 片段整体的平均高度不变（不会把人整体抬高或压低）。
///
/// ⚠️ 它**无权**处理"整段悬空 0.79m"这件事 —— 那是这个重定向姿势的固有高度，
/// 压掉就变成蹲姿了（见 `PlayerAnimationController.允许下拉` 的注释）。
/// 本工具只负责"晃"，不负责"悬空"。
///
/// ## 用法
///
/// 在 Project 窗口**选中一个或多个 `.anim`** → 菜单：
///   · `修仙/动画/检查竖直抖动（只读）`    —— 量并打日志（抖动 + 离地高度），不改任何东西
///   · `修仙/动画/压平竖直抖动（改片段）`  —— 压掉**片段内部**的上下抖（本片段只有 0.03m）
///   · `修仙/动画/把片段落到地面（改片段）` —— **整体下移一个常量**，让脚骨踩到地面
///
/// 采样需要一具人形骨架：优先用当前场景里的 `Player`（人形 Animator），
/// 找不到就退回场景里任意人形 Animator。采样走 `AnimationMode`，**不会**留在场景里。
///
/// ⚠️ 别用模态框报结果（见踩坑 F8）：一律打 Console。
///
/// ## 2026-09-29 实测（`普攻_远程_01`，3C_Testbed）
///
/// ```
/// 待机：      髋骨 0.872   脚趾 ≈0.02（踩地）
/// 攻击片段内：髋骨 1.759~1.790（片段内部只抖 0.032m）
///             脚趾最低 0.867 → 整段离地 0.85m
/// 实机（逐帧）：播动作 0.19s 内髋骨 0.87→1.76（窜高 0.89m），片段跑完 0.13s 内落回 0.87
/// ```
///
/// 结论：**"上下晃"不是片段在抖，而是这个姿势被整体抬高了 0.9m**
/// （它是拿嫦娥的浮空施法动作重定向来的）—— 每次普攻窜上去再落回来。
/// 脚骨与髋骨的距离（0.90m）和站立时（0.85m）几乎一样 → 腿是**正常垂着**的，
/// 所以「整体落回地面」是安全的：动作一模一样，只是人不再腾空。
/// </summary>
public static class 动画竖直压平
{
    /// <summary>Humanoid 的根位移竖直分量 —— 改它只会整体上下平移，不动任何旋转</summary>
    const string 竖直曲线 = "RootT.y";

    /// <summary>判"踩没踩地"用这几根骨（和 PlayerAnimationController 的口径一致：脚趾 / 脚掌）</summary>
    static readonly HumanBodyBones[] 脚骨 =
    {
        HumanBodyBones.LeftToes, HumanBodyBones.RightToes,
        HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot,
    };

    [MenuItem("修仙/动画/检查竖直抖动（只读）", false, 700)]
    static void 检查()
    {
        处理(false);
    }

    [MenuItem("修仙/动画/压平竖直抖动（改片段）", false, 701)]
    static void 修复()
    {
        处理(true);
    }

    [MenuItem("修仙/动画/把片段落到地面（改片段）", false, 702)]
    static void 落地面()
    {
        处理落地面(true);
    }

    static void 处理(bool 真改)
    {
        var 片段们 = Selection.GetFiltered<AnimationClip>(SelectionMode.Assets);
        if (片段们 == null || 片段们.Length == 0)
        {
            Debug.LogWarning("[竖直压平] 先在 Project 窗口选中一个或多个 .anim 片段，再执行这个菜单。");
            return;
        }

        var 骨架 = 找采样骨架();
        if (骨架 == null)
        {
            Debug.LogError("[竖直压平] 找不到人形骨架：请先打开一个有人形 Animator 的场景"
                + "（例如 3C_Testbed 里的 Player），这个工具要靠它采样姿态。");
            return;
        }

        foreach (var 片段 in 片段们)
        {
            if (片段 == null) continue;

            var 改动前 = 量髋骨(片段, 骨架);
            float 抖动前 = 改动前.Max - 改动前.Min;
            var 脚 = 量脚底(片段, 骨架);
            Debug.Log(string.Format("[竖直压平] {0}（{1:0.##}s）：髋骨世界Y {2:0.000} ~ {3:0.000}"
                + "，竖直抖动 {4:0.000}m，平均 {5:0.000}m｜最低脚骨 {6:0.000}，整段**离地** {7:0.000}m",
                片段.name, 片段.length, 改动前.Min, 改动前.Max, 抖动前, 改动前.Mean,
                脚.最低脚Y, 脚.最低脚Y - 脚.根Y));

            if (!真改) continue;

            var 绑定 = EditorCurveBinding.FloatCurve("", typeof(Animator), 竖直曲线);
            var 原曲线 = AnimationUtility.GetEditorCurve(片段, 绑定);
            if (原曲线 == null)
            {
                Debug.LogError("[竖直压平] " + 片段.name + " 里没有 " + 竖直曲线
                    + " 曲线（不是 Humanoid 片段？）—— 没动它。"
                    + "Generic 骨架的片段请改成压平根节点的 Position.y。");
                continue;
            }

            // 反相叠加：让髋骨的世界 Y 在整段里保持恒定（= 原来的平均值）
            var 新曲线 = new AnimationCurve();
            int 段数 = 改动前.值.Length - 1;
            for (int i = 0; i <= 段数; i++)
            {
                float t = 片段.length * i / Mathf.Max(1, 段数);
                float 修正 = 改动前.值[i] - 改动前.Mean;
                新曲线.AddKey(new Keyframe(t, 原曲线.Evaluate(t) - 修正));
            }

            AnimationUtility.SetEditorCurve(片段, 绑定, 新曲线);
            EditorUtility.SetDirty(片段);
            AssetDatabase.SaveAssets();

            var 改动后 = 量髋骨(片段, 骨架);
            Debug.Log(string.Format("[竖直压平] ✔ {0} 改完复测：髋骨世界Y {1:0.000} ~ {2:0.000}"
                + "，竖直抖动 {3:0.000}m（原来 {4:0.000}m）",
                片段.name, 改动后.Min, 改动后.Max, 改动后.Max - 改动后.Min, 抖动前), 片段);
        }

        if (真改) AssetDatabase.Refresh();
    }

    // ============================================================ 落地面

    /// <summary>
    /// **整体落到地面**：把 `RootT.y` 减去一个**常量** = 整段里最低的那根脚骨离地多少。
    ///
    /// 为什么用常量（而不是逐帧）：片段内部的上下抖已经很小（本片段 0.03m），
    /// 真正的问题是**整个姿势被抬高了 0.9m**；减常量既解决问题，又**完全不动动作的节奏**，
    /// 也不会把脚"钉"在地面（脚该怎么抬还怎么抬）。
    ///
    /// 和"贴地补偿"的区别：那是运行时按帧把模型往下拉（会跟骑乘/御风抢高度，
    /// 也可能把人拉成蹲姿）；这里改的是**片段数据**，与状态无关。
    /// </summary>
    static void 处理落地面(bool 真改)
    {
        var 片段们 = Selection.GetFiltered<AnimationClip>(SelectionMode.Assets);
        if (片段们 == null || 片段们.Length == 0)
        {
            Debug.LogWarning("[落地面] 先在 Project 窗口选中一个或多个 .anim 片段，再执行这个菜单。");
            return;
        }

        var 骨架 = 找采样骨架();
        if (骨架 == null)
        {
            Debug.LogError("[落地面] 找不到人形骨架：请先打开一个有人形 Animator 的场景（例如 3C_Testbed 里的 Player）。");
            return;
        }

        foreach (var 片段 in 片段们)
        {
            if (片段 == null) continue;

            var 脚 = 量脚底(片段, 骨架);
            float 离地 = 脚.最低脚Y - 脚.根Y;
            Debug.Log(string.Format("[落地面] {0}：最低脚骨 {1:0.000}m，骨架根 Y {2:0.000}m → 整段离地 {3:0.000}m",
                片段.name, 脚.最低脚Y, 脚.根Y, 离地));

            if (!真改)
            {
                Debug.Log("[落地面] （只读，没有改动）");
                continue;
            }
            if (Mathf.Abs(离地) < 0.005f)
            {
                Debug.Log("[落地面] " + 片段.name + " 本来就踩在地上（离地 < 5mm），跳过。");
                continue;
            }

            var 绑定 = EditorCurveBinding.FloatCurve("", typeof(Animator), 竖直曲线);
            if (AnimationUtility.GetEditorCurve(片段, 绑定) == null)
            {
                Debug.LogError("[落地面] " + 片段.name + " 里没有 " + 竖直曲线
                    + " 曲线（不是 Humanoid 片段？）—— 没动它。");
                continue;
            }

            // ⚠️ **一次减不干净**：人形重定向会把 RootT 的位移按「目标骨架髋高比」缩放
            //    （本片段实测 k ≈ 0.89：名义减 0.847m，实际只落 0.753m）。
            //    所以循环：量剩余离地 → 再减掉它 → 复量。误差每轮按 (1−k) ≈ 0.11 收敛，
            //    通常 2~3 轮就进 5mm。
            int 轮次 = 0;
            for (; 轮次 < 6; 轮次++)
            {
                var 当前 = 量脚底(片段, 骨架);
                float 剩余 = 当前.最低脚Y - 当前.根Y;
                if (Mathf.Abs(剩余) < 0.005f) break;

                var 曲线 = AnimationUtility.GetEditorCurve(片段, 绑定);
                if (曲线 == null) break;
                var 新曲线 = new AnimationCurve();
                for (int i = 0; i < 曲线.length; i++)
                {
                    var k = 曲线.keys[i];
                    k.value -= 剩余;
                    新曲线.AddKey(k);
                }
                AnimationUtility.SetEditorCurve(片段, 绑定, 新曲线);
                EditorUtility.SetDirty(片段);
                AssetDatabase.SaveAssets();
            }

            var 复 = 量脚底(片段, 骨架);
            var 复髋 = 量髋骨(片段, 骨架);
            Debug.Log(string.Format("[落地面] ✔ {0} 改完复测：最低脚骨 {1:0.000}m（离地 {2:0.000}m）"
                + "，髋骨 {3:0.000} ~ {4:0.000}（内部抖动 {5:0.000}m）",
                片段.name, 复.最低脚Y, 复.最低脚Y - 复.根Y, 复髋.Min, 复髋.Max, 复髋.Max - 复髋.Min), 片段);
        }

        if (真改) AssetDatabase.Refresh();
    }

    struct 脚底量
    {
        public float 最低脚Y;
        public float 根Y;
    }

    /// <summary>整段里最低的那根脚骨的**世界 Y**（以及骨架根的世界 Y）</summary>
    static 脚底量 量脚底(AnimationClip 片段, Animator 骨架)
    {
        int 段数 = Mathf.Clamp(Mathf.RoundToInt(片段.length * 片段.frameRate), 2, 600);
        float 最低 = float.MaxValue;

        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i <= 段数; i++)
            {
                AnimationMode.SampleAnimationClip(骨架.gameObject, 片段, 片段.length * i / 段数);
                foreach (var b in 脚骨)
                {
                    var 骨 = 骨架.GetBoneTransform(b);
                    if (骨 == null) continue;
                    float y = 骨.position.y;
                    if (y < 最低) 最低 = y;
                }
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        return new 脚底量 { 最低脚Y = 最低, 根Y = 骨架.transform.position.y };
    }

    // ============================================================ 采样

    struct 髋骨量
    {
        public float[] 值;
        public float Min, Max, Mean;
    }

    /// <summary>
    /// 逐帧采样髋骨的世界 Y。走 `AnimationMode`：临时驱动场景里的骨架、结束时还原，
    /// **不会**改到场景里的任何东西。
    /// </summary>
    static 髋骨量 量髋骨(AnimationClip 片段, Animator 骨架)
    {
        int 段数 = Mathf.Clamp(Mathf.RoundToInt(片段.length * 片段.frameRate), 2, 600);
        var 值 = new float[段数 + 1];
        float 最小 = float.MaxValue, 最大 = float.MinValue, 和 = 0f;

        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i <= 段数; i++)
            {
                float t = 片段.length * i / 段数;
                AnimationMode.SampleAnimationClip(骨架.gameObject, 片段, t);
                var 髋 = 骨架.GetBoneTransform(HumanBodyBones.Hips);
                float y = 髋 != null ? 髋.position.y : 骨架.transform.position.y;
                值[i] = y;
                和 += y;
                if (y < 最小) 最小 = y;
                if (y > 最大) 最大 = y;
            }
        }
        finally
        {
            AnimationMode.StopAnimationMode();
        }

        return new 髋骨量 { 值 = 值, Min = 最小, Max = 最大, Mean = 和 / 值.Length };
    }

    static Animator 找采样骨架()
    {
        var 玩家 = GameObject.Find("Player");
        if (玩家 != null)
        {
            var a = 玩家.GetComponentInChildren<Animator>();
            if (a != null && a.isHuman && a.avatar != null) return a;
        }
        foreach (var a in Object.FindObjectsOfType<Animator>())
            if (a != null && a.isHuman && a.avatar != null) return a;
        return null;
    }

    // ---- ASCII 别名（避免外部工具按名反射时找不到）----
    public static void CheckOnly() { 处理(false); }
    public static void Flatten() { 处理(true); }
    public static void CheckGroundOnly() { 处理落地面(false); }
    public static void DropToGround() { 处理落地面(true); }
}
