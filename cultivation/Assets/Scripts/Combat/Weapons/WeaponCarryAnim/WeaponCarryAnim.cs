using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备某门功法时，把玩家控制器里的几个**通用移动 / 御风片段**换成"带这门功法持械上半身"的版本。
///
/// 为什么需要它：`持刀_站 / 持刀_走 / 持刀_跑 / 持刀_御风Idle / 持刀_御风前进` 这五个片段是
/// 站立和御风保留杨戬 Run 的持械上半身，地面移动直接使用杨戬 Run 全身动作。
/// 但控制器 `PlayerLocomotion` 是**全局资产**，不能为了这门功法把别人的动作也改掉，
/// 所以改成运行时用 <see cref="AnimatorOverrideController"/> **只覆盖这几个片段**：
/// 装备八九玄功 ⇒ 换成持械版；换别的功法 / 组件被停用 ⇒ 立刻还原。
///
/// 覆盖用官方**字符串索引器** `覆盖["原片段名"] = 新片段` 做（BlendTree 里的片段同样能覆盖，
/// `Armature|Idle_Loop` 这些就是 `UAL1_Standard.fbx` 里的子资产）。
/// ⚠️ 别改成 `GetOverrides` 自己配对：pairs 里的 key 是 Unity 造的占位片段，实测只换中 1 条还换错。
/// </summary>
public class WeaponCarryAnim : MonoBehaviour
{
    [System.Serializable]
    public class 替换项
    {
        [Tooltip("控制器里原来用的片段名（BlendTree 里的也算）")]
        public string 原片段 = "";
        [Tooltip("换成哪个：Resources 路径，不带扩展名")]
        public string 新片段路径 = "";
    }

    [Header("什么时候生效")]
    [Tooltip("当前功法的 功法id 等于它才换（留空 = 一直生效）")]
    public string 显示条件功法id = "gongfa_jiuba_xuangong";

    [Header("替换表（默认 = 八九玄功的五个持刀片段）")]
    public List<替换项> 替换 = new List<替换项>
    {
        new 替换项 { 原片段 = "Armature|Idle_Loop",   新片段路径 = "技能动作/八九玄功/持刀_站" },
        new 替换项 { 原片段 = "Armature|Walk_Loop",   新片段路径 = "技能动作/八九玄功/持刀_走" },
        new 替换项 { 原片段 = "Armature|Sprint_Loop", 新片段路径 = "技能动作/八九玄功/持刀_跑" },
        new 替换项 { 原片段 = "御风_Idle",            新片段路径 = "技能动作/八九玄功/持刀_御风Idle" },
        new 替换项 { 原片段 = "御风_前进",            新片段路径 = "技能动作/八九玄功/持刀_御风前进" },
    };

    [Header("调试")]
    public bool 打印日志 = true;

    bool 太虚剑 => 面板 != null && 面板.当前功法 != null && 面板.当前功法.功法id == "gongfa_taixu_jianjue";
    string 已应用功法;
    static readonly List<替换项> 太虚替换 = new List<替换项>
    {
        new 替换项 { 原片段="Armature|Idle_Loop", 新片段路径="技能动作/太虚剑决/持剑_Idle" },
        new 替换项 { 原片段="Armature|Walk_Loop", 新片段路径="技能动作/太虚剑决/持剑_前进" },
        new 替换项 { 原片段="Armature|Sprint_Loop", 新片段路径="技能动作/太虚剑决/持剑_前进" },
        new 替换项 { 原片段="御风_Idle", 新片段路径="技能动作/太虚剑决/持剑_Idle_御风" },
        new 替换项 { 原片段="御风_前进", 新片段路径="技能动作/太虚剑决/持剑_前进_御风" },
    };
    Animator 动画器;
    RuntimeAnimatorController 原始控制器;
    AnimatorOverrideController 覆盖;
    UIPanelData 面板;
    readonly HashSet<string> 报过找不到 = new HashSet<string>();

    void Awake() { 解析(); }

    void OnEnable() { 解析(); if (面板 != null) { 面板.Changed -= 刷新; 面板.Changed += 刷新; } 刷新(); }

    void OnDisable() { 退订(); 还原(); }

    void OnDestroy() { 退订(); 还原(); }

    void 解析()
    {
        if (动画器 == null) 动画器 = GetComponentInChildren<Animator>(true);
        if (动画器 != null && 原始控制器 == null) 原始控制器 = 动画器.runtimeAnimatorController;

        var 找到 = 面板;
        if (找到 == null)
        {
            var ui = GameObject.Find("CharacterUI");
            if (ui != null) 找到 = ui.GetComponent<UIPanelData>();
            if (找到 == null) 找到 = FindObjectOfType<UIPanelData>();
        }
        if (找到 != 面板)
        {
            退订();                 // ★ 先减后加（踩坑 H24）：面板被销毁重建之后要换到新面板上
            面板 = 找到;
            if (面板 != null) 面板.Changed += 刷新;
        }
    }

    void 退订() { if (面板 != null) 面板.Changed -= 刷新; }

    /// <summary>当前功法是不是要持械动作</summary>
    public bool 条件满足
    {
        get
        {
            if (string.IsNullOrEmpty(显示条件功法id)) return true;
            return 面板 != null && 面板.当前功法 != null && (面板.当前功法.功法id == 显示条件功法id || 太虚剑);
        }
    }

    /// <summary>重算：该换就换、不该换就还原</summary>
    public void 刷新()
    {
        解析();
        if (动画器 == null || 原始控制器 == null) return;
        if (条件满足) 应用(); else 还原();
    }

    void 应用()
    {
        string profile = 太虚剑 ? "gongfa_taixu_jianjue" : 显示条件功法id;
        if (覆盖 == null || 覆盖.runtimeAnimatorController != 原始控制器 || 已应用功法 != profile)
        {
            覆盖 = new AnimatorOverrideController(原始控制器);
            已应用功法 = profile;
            int 换了 = 0;
            foreach (var 项 in 太虚剑 ? 太虚替换 : 替换)
            {
                if (项 == null || string.IsNullOrEmpty(项.原片段) || string.IsNullOrEmpty(项.新片段路径)) continue;
                var 新 = Resources.Load<AnimationClip>(项.新片段路径);
                if (新 == null)
                {
                    if (报过找不到.Add(项.新片段路径))
                        Debug.LogWarning("[WeaponCarryAnim] 取不到片段：" + 项.新片段路径, this);
                    continue;
                }
                // ★ 用官方**字符串索引器**按"原片段名"覆盖。
                //   不要用 GetOverrides 自己配对 —— 实测那样只换中 1 条、还换到了别的片段上
                //   （日志出现 `普攻_远程_01 → 持刀_御风Idle`），因为 pairs 里的 key 是 Unity 造的占位片段，
                //   名字跟控制器里的原片段对不上。
                覆盖[项.原片段] = 新;
                换了++;
            }
            if (打印日志)
                Debug.Log("[WeaponCarryAnim] 已把 " + 换了 + " 条片段换成持械版（基=" + 原始控制器.name + "）", this);
        }
        // The action controller may wrap this carry override. Preserve that owner instead
        // of resetting a running attack every time unrelated panel data raises Changed.
        var current = 动画器.runtimeAnimatorController;
        var driver = GetComponent<PlayerAnimationController>();
        if (driver != null && driver.保留了控制器(覆盖)) return;
        while (current is AnimatorOverrideController nested)
        {
            if (current == 覆盖) return;
            current = nested.runtimeAnimatorController;
        }
        if (动画器.runtimeAnimatorController != 覆盖) 动画器.runtimeAnimatorController = 覆盖;
    }

    void 还原()
    {
        if (动画器 == null || 原始控制器 == null) return;
        if (动画器.runtimeAnimatorController != 原始控制器)
        {
            动画器.runtimeAnimatorController = 原始控制器;
            if (打印日志) Debug.Log("[WeaponCarryAnim] 已还原成原控制器 " + 原始控制器.name, this);
        }
    }

    /// <summary>现在盖着的覆盖控制器（调试/测试用，没盖就是 null）</summary>
    public AnimatorOverrideController 当前覆盖 => 动画器 != null && 动画器.runtimeAnimatorController == 覆盖 ? 覆盖 : null;

    // ---- ASCII 别名 ----
    public void Refresh2() => 刷新();
    public bool ConditionMet => 条件满足;
}
