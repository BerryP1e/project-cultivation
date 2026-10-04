using UnityEngine;

/// <summary>
/// **玩家外观** —— 换外观 = **只换蒙皮网格和材质**，不换人。
///
/// 实测结论（2026-09-27，用户提醒后确认）：
/// 「成男村民」和玩家两套模型的**骨骼完全一样** ——
/// 节点数都是 81、路径除根名外逐段相同（`…/Armature/BoneRoot/Hip/Pelvis/L_Thigh/…`）、
/// 两个 `SkinnedMeshRenderer` 的 **78 根骨骼同名同序**。所以：
///
///   · **不换 `Player_Visual`**、**不换 Animator**、**不动朝向补偿**、**不做动画状态同步**；
///   · 只把玩家自己那个 `SkinnedMeshRenderer` 的 `sharedMesh` 和 `sharedMaterials` 换成外观的
///     —— 因为骨骼一模一样，新网格挂在同一套骨骼上，动画/朝向/物理全都天然正确 ✓
///
/// 之前那版"整套 Player_Visual 换掉"的复杂度（缓存引用、死亡流程指向、动画桥接）**全都不需要了**。
///
/// 外观怎么来：由 `AppearanceDefinition` 给（模型资源路径 → 预制体 → 取它的 SkinnedMeshRenderer）。
/// 主线挂钩：外观自带 `默认拥有` / `解锁标记`，任务完成写的标记一上就自动换上（任务侧零代码）。
/// </summary>
[DisallowMultipleComponent]
public class 玩家外观 : MonoBehaviour
{
    [Tooltip("玩家模型根（找它的 SkinnedMeshRenderer）。留空自动找名为 Player_Visual 的子物件")]
    public Transform 玩家视觉;

    [Tooltip("【调试/预览用】直接看当前穿的是哪件")]
    public AppearanceDefinition 当前外观;

    [Header("已拥有（靠物品获得，比如「门派便服」）")]
    [Tooltip("已经拿到手的外观。**用户 2026-09-27**：不靠标记解锁，而是任务发道具、道具使用后获得。\n" +
             "以后存进存档就按这个列表的 id 存")]
    public System.Collections.Generic.List<AppearanceDefinition> 已获得
        = new System.Collections.Generic.List<AppearanceDefinition>();

    /// <summary>这件外观现在拥有吗（默认拥有 或 已经拿到手）</summary>
    public bool 已拥有(AppearanceDefinition a)
    {
        if (a == null) return false;
        if (a.默认拥有) return true;
        return 已获得 != null && 已获得.Contains(a);
    }

    /// <summary>
    /// 获得一件外观（已经是就不重复加），默认**立刻换上**。
    /// 由 <see cref="学外观效果"/> 在玩家使用道具时调用。
    /// </summary>
    public bool 获得(AppearanceDefinition 外观, bool 立刻装备 = true)
    {
        if (外观 == null) return false;
        if (已获得 == null) 已获得 = new System.Collections.Generic.List<AppearanceDefinition>();

        bool 新的 = !已拥有(外观);
        if (新的 && !外观.默认拥有) 已获得.Add(外观);
        if (立刻装备) 装备(外观);
        Debug.Log("[外观] 获得「" + 外观.DisplayName + "」" + (新的 ? "" : "（早就有了，不重复给）"), 外观);
        return 新的;
    }

    /// <summary>外观换了（参数是新外观，null = 换回原始网格）</summary>
    public static event System.Action<AppearanceDefinition> 外观变化;

    AppearanceDatabase 库;
    SkinnedMeshRenderer 网格;
    Mesh 原网格;
    Material[] 原材质;

    void Awake()
    {
        if (玩家视觉 == null)
        {
            var t = transform.Find("Player_Visual");
            玩家视觉 = t != null ? t : transform;
        }
        网格 = 找身体网格();
        if (网格 != null)
        {
            原网格 = 网格.sharedMesh;
            原材质 = 网格.sharedMaterials;
        }
        else Debug.LogWarning("[外观] 在 " + (玩家视觉 != null ? 玩家视觉.name : "?") + " 底下找不到 SkinnedMeshRenderer，换外观会没反应", this);
    }

    /// <summary>装备会在 Awake 阶段挂到手骨下，层级中的第一个蒙皮不一定是身体。</summary>
    SkinnedMeshRenderer 找身体网格()
    {
        if (玩家视觉 == null) return null;
        var animator = 玩家视觉.GetComponentInChildren<Animator>(true);
        Transform hip = animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid
            ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
        var weapon = GetComponent<武器挂载>();
        SkinnedMeshRenderer fallback = null;
        int boneCount = -1;
        foreach (var candidate in 玩家视觉.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (candidate.sharedMesh == null) continue;
            if (weapon != null && weapon.实例 != null && candidate.transform.IsChildOf(weapon.实例.transform)) continue;
            var bones = candidate.bones;
            if (bones == null) continue;
            foreach (var bone in bones)
                if (hip != null && bone == hip) return candidate;
            // Generic/non-human fallback: prefer the body rig over a one-bone attachment.
            if (bones.Length > boneCount) { fallback = candidate; boneCount = bones.Length; }
        }
        return fallback;
    }

    void Start()
    {
        库 = AppearanceDatabase.取();
        对话标记.变化 += 处理标记变化;

        // ★ 先把上一次会话记下的外观装上（跨场景保持玩家自己换的那身）。
        //   踩过的坑（用户 2026-09-27："我用村中少年的外观进镇妖塔，出来就变成宗门便服了"）：
        //   原来这里无条件调 按规则选一件()，而 按规则挑() 选的是"表里最靠后的解锁即装备外观"。
        //   一旦门派便服解锁了「修仙者」(app_player)，**每次进场景都会强制换成它**，
        //   把玩家手动选的村中少年覆盖掉。
        if (已选外观 != null)
        {
            装备(已选外观);
        }
        else
        {
            按规则选一件();          // 本局第一次进场景：按规则挑一件，并记下来
        }
    }

    /// <summary>
    /// **跨场景保持的"玩家当前外观"**。
    /// 为什么用静态：`玩家外观` 挂在 Player 上，每次切场景都重新构造，
    /// 实例字段活不过切场景；而"玩家选了什么外观"是这一局的会话状态。
    /// </summary>
    static AppearanceDefinition 已选外观;

    /// <summary>
    /// **当前外观的 id**（存档用）。没选过返回空串。
    ///
    /// 【为什么需要单独给存档用】`已选外观` 是 **static** —— 它能活过**切场景**，
    /// 但活不过**读档**（读档时进程里的静态可能还是上一次会话的，或者干脆是空的）。
    /// 所以"玩家自己换的那身"必须进存档，否则读档后会被 `按规则选一件()` 覆盖掉
    /// （用户在 2026-09-27 报过一个同源的 bug：进镇妖塔出来变回宗门便服）。
    /// </summary>
    public static string 当前外观id
    {
        get
        {
            var 库 = AppearanceDatabase.取();
            if (库 == null || 库.全部 == null) return 已选外观 != null ? 已选外观.id : "";
            // 已选外观本身可能来自上一局的库实例，按 id 反查一次更稳
            if (已选外观 == null) return "";
            return 已选外观.id;
        }
    }

    /// <summary>读档用：按 id 把外观记进静态，并立刻装上（读档时可能还没 Start）</summary>
    public static void 从存档设置外观(string 外观id, 玩家外观 组件 = null)
    {
        if (string.IsNullOrEmpty(外观id)) return;
        var 库 = AppearanceDatabase.取();
        if (库 == null || 库.全部 == null) return;

        foreach (var a in 库.全部)
            if (a != null && a.id == 外观id)
            {
                已选外观 = a;
                if (组件 != null) 组件.装备(a);
                Debug.Log("[外观] 读档恢复外观：" + a.DisplayName);
                return;
            }
        Debug.LogWarning("[外观] 存档里的外观 id 找不到：" + 外观id);
    }

    void OnDestroy() { 对话标记.变化 -= 处理标记变化; }

    void 处理标记变化(string 标记, bool 新增)
    {
        if (!新增) return;
        var 该穿 = 按规则挑();
        if (该穿 != null && 该穿 != 当前外观) 装备(该穿);
    }

    /// <summary>按「默认拥有 + 解锁标记」挑一件：已解锁且“解锁即装备”的最后一件优先（表里越靠后越高级）</summary>
    AppearanceDefinition 按规则挑()
    {
        if (库 == null) return null;
        AppearanceDefinition 缺省 = null, 解锁的 = null;
        for (int i = 0; i < 库.全部.Count; i++)
        {
            var a = 库.全部[i];
            if (a == null) continue;
            if (a.默认拥有 && 缺省 == null) 缺省 = a;
            if (a.已解锁 && a.解锁即装备) 解锁的 = a;
        }
        return 解锁的 != null ? 解锁的 : 缺省;
    }

    void 按规则选一件() => 装备(按规则挑());

    /// <summary>换上某件外观（传 null = 换回原始网格）</summary>
    public bool 装备(AppearanceDefinition 外观)
    {
        if (外观 == null) { 还原(); return false; }
        if (网格 == null) return false;
        if (当前外观 == 外观 && 网格.sharedMesh != 原网格) return true;

        var 源 = 外观.取模型();
        var 源网格 = 源 != null ? 源.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
        if (源网格 == null || 源网格.sharedMesh == null)
        {
            Debug.LogWarning("[外观] 「" + 外观.DisplayName + "」没有可用的蒙皮网格（模型=" + (源 != null ? 源.name : "取不到") + "）", 外观);
            return false;
        }

        网格.sharedMesh = 源网格.sharedMesh;
        网格.sharedMaterials = 源网格.sharedMaterials;

        // 【加固，不是必须】换完网格把包围盒也一起搬过来，并让 Unity 按**蒙皮后的实际顶点**算包围盒。
        //   理由：`SkinnedMeshRenderer.localBounds` 是**跟着渲染体序列化的**，运行时换 `sharedMesh`
        //   它**不会**自动跟着变；新网格一旦比旧的大、或挂在别的骨骼层级上，Unity 就会拿旧包围盒
        //   做视锥剔除 → 模型明明在镜头里却被剔掉。`updateWhenOffscreen = true` 让剔除改用实际顶点，
        //   换网格 / 换动作都不会再剔错（代价：这个渲染体不再被静态剔除；只给玩家一个，量很小）。
        //
        // ★ 别搞错因果 ★ 「宗门玩家模型不显示」的**根因不是包围盒剔除**，修这个只是顺手加固。
        //   真正的根因是 **Sect.scene 里 `Player_Visual` 的本地位置被存成了 (32.9, 0, -39)** ——
        //   整套骨架离 `Player` 根 51 米，而相机跟的是 `Player` 根，人自然看不见。
        //   （实测：玩家 (22.07,-0.66,-3.79)，蒙皮渲染体世界位置 (54.97,-0.66,-42.79)，偏 50.9 米。）
        //   排查这类"模型不显示"的正确顺序：① 打印 `Player_Visual.localPosition` 和世界位置；
        //   ② 再看包围盒 / 剔除。详见 docs/ai/踩坑总库.md 里「模型不显示」那条。
        网格.localBounds = 源网格.localBounds;
        网格.updateWhenOffscreen = true;
        当前外观 = 外观;
        已选外观 = 外观;          // ★ 记住，切场景时不再被规则覆盖
        Debug.Log("[外观] 换上「" + 外观.DisplayName + "」（网格 " + 源网格.sharedMesh.name + "）", 外观);
        外观变化?.Invoke(外观);
        return true;
    }

    /// <summary>换回原始的玩家网格</summary>
    public void 还原()
    {
        if (网格 != null && 原网格 != null)
        {
            网格.sharedMesh = 原网格;
            网格.sharedMaterials = 原材质;
        }
        当前外观 = null;
        已选外观 = null;
        外观变化?.Invoke(null);
    }

    // ---- ASCII 别名 ----
    public AppearanceDefinition CurrentAppearance => 当前外观;
    public bool Equip(AppearanceDefinition a) => 装备(a);
    public void RestoreOriginal() => 还原();
}
