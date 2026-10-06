using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色面板的数据源。所有页面都从这里取数据，方便之后换成真正的存档/运行时数据。
/// 现在直接挂一串 ScriptableObject 引用，填进去就能在面板里看到内容。
/// </summary>
public class UIPanelData : MonoBehaviour
{
    [Tooltip("主动四御施放后恢复四枚；切场景和读档保留剩余次数。")]
    public int 绝对护罩剩余次数;
    public bool 四御已激活;
    [Header("玩家")]
    [Tooltip("玩家基本属性定义（lore/玩家基本属性.txt）")]
    public PlayerStatsDefinition 玩家属性;

    [Tooltip("当前正在修炼的功法，显示在「境界」页的功法展示区")]
    public GongFaDefinition 当前功法;

    [Header("境界进度")]
    [Tooltip("当前境界已积累的吐纳经验")]
    public long 当前经验 = 0;

    [Tooltip("突破到下一境界所需的总经验。留 0 则自动取境界定义里的数值")]
    public long 突破所需总经验 = 0;

    [Header("持有内容")]
    public List<ItemDefinition> 物品 = new List<ItemDefinition>();

    [Tooltip("神通目录（所有主动+被动）。**运行时从 面板库.取() 灌**，场景里存的会被覆盖 —— " +
             "因为「目录」每个场景一份必然漂移（实测古古镇 11 条、宗门 0 条）")]
    public List<DivineAbilityDefinition> 神通 = new List<DivineAbilityDefinition>();
    [Tooltip("法宝目录。运行时从 面板库 灌")]
    public List<TreasureDefinition> 法宝 = new List<TreasureDefinition>();
    public List<string> 已拥有法宝 = new List<string>();
    public TreasureDefinition 当前法宝;
    public bool 收服系统已初始化;
    public int 青山剑有效击杀;
    public int 青山剑数量 => Mathf.Clamp(1+Mathf.Clamp(青山剑有效击杀,0,400)/50,1,9);
    public bool 记录青山剑击杀(NpcInstance npc, int playerLevel) {
        if(当前法宝==null || 当前法宝.法宝id!=QingshanSwordTreasure.法宝id || !已拥有(当前法宝)
            || npc==null || npc.类型!=NpcKind.妖魔 || npc.GetComponent<FormationSpirit>()!=null || npc.定义.死亡后立即重生 || 青山剑有效击杀>=400)return false;
        int level=npc.击杀计入等级>0 ? npc.击杀计入等级 : npc.当前补正等级>0 ? Mathf.RoundToInt(npc.当前补正等级) : npc.定义.境界;
        if(level<playerLevel)return false;
        int before=青山剑数量;青山剑有效击杀=Mathf.Clamp(青山剑有效击杀+1,0,400);RaiseChanged();
        if(青山剑数量>before)ShowHint("青山剑进化 · "+青山剑数量+"剑"+(青山剑数量==3?" · 万剑归宗已解锁":""));return true;
    }
    public const string 镇妖葫id = "treasure_zhenyaohu";
    public bool 已拥有(TreasureDefinition t) => t != null && 已拥有法宝.Contains(t.法宝id);
    public void 设置当前法宝(TreasureDefinition t) { EnsureLists(); if(t != null && !已拥有(t)) return; 当前法宝=t; RaiseChanged(); }
    public bool 收服真灵(NpcDefinition d) {
        EnsureLists(); if(d == null || 已获得真灵.Exists(n=>n != null && n.id == d.id)) return false;
        已获得真灵.Add(d); 已获得真灵.Sort(比真灵); RaiseChanged(); ShowHint("收服真灵：「"+d.DisplayName+"」"); return true;
    }
    [Tooltip("灵阵目录。运行时从 面板库 灌")]
    public List<SpiritArrayDefinition> 灵阵 = new List<SpiritArrayDefinition>();

    [Header("坐骑")]
    [Tooltip("坐骑目录。运行时从 面板库 灌（按门槛排序）")]
    public List<MountDefinition> 坐骑 = new List<MountDefinition>();
    [Tooltip("实际学会驾驭的坐骑 id；目录不代表拥有")]
    public List<string> 已学坐骑 = new List<string>();

    [Tooltip("当前乘骑的坐骑。null = 没骑。\n" +
             "★ **不要直接赋值** —— 一律走 设置当前坐骑()，" +
             "互斥规则（装备坐骑 → 自动停用【凭虚御风】）就写在那一处")]
    public MountDefinition 当前坐骑;

    [Header("坐骑 ⇄ 御风 互斥")]
    [Tooltip("【凭虚御风】在被动神通表里的 id。\n" +
             "用户 2026-09-24 定的规则：**装备坐骑 → 自动停用这个被动；" +
             "启用这个被动 → 自动取消装备坐骑**，两者不能同时生效\n" +
             "（Shift 是它们共用的一个键，见 MountRider / YufengFlight）")]
    public string 御风神通id = 御风神通默认id;

    /// <summary>【凭虚御风】在被动神通表里的 id 的默认值</summary>
    public const string 御风神通默认id = "ability_pingxu_yufeng";

    [Header("主动技能装备（神通/法宝/灵阵共用）")]
    [Tooltip("6 个主动技能槽的内容。空位用 null 表示")]
    public List<UnityEngine.Object> 主动技能 = new List<UnityEngine.Object>();

    [Header("被动神通开关")]
    [Tooltip("被玩家【停用】的被动神通。列表里没有的默认都是启用状态，" +
             "这样不用为「默认全开」做额外初始化")]
    public List<PassiveDivineAbility> 已停用被动 = new List<PassiveDivineAbility>();

    [Header("已学会的功法")]
    [Tooltip("玩家已经学会的功法。转修功法只能在这几门里选。**新存档是空的**，靠在背包里「使用」秘籍类物品学会")]
    public List<GongFaDefinition> 已学功法 = new List<GongFaDefinition>();

    [Header("已获得的能力（新存档为空，靠物品学会/获得；都会存进存档）")]
    [Tooltip("已经获得的主动神通。没获得的：神通页不显示、也不能装备")]
    public List<ActiveDivineAbility> 已获得主动神通 = new List<ActiveDivineAbility>();

    [Tooltip("已经获得的被动神通。没获得的不参与生效（和「停用」是两回事：没获得=还没有，停用=有但关着）")]
    public List<PassiveDivineAbility> 已获得被动神通 = new List<PassiveDivineAbility>();

    [Header("待装备")]
    [Tooltip("点了「启用」之后、等待玩家点一个空槽放入的主动神通。null 表示没有待装备")]
    public ActiveDivineAbility 待装备神通;

    [Header("战阵")]
    [Tooltip("玩家通过镇妖葫实际收服的真灵，存档与跨场景保存；不代表候选目录")]
    public List<NpcDefinition> 已获得真灵 = new List<NpcDefinition>();

    [Tooltip("战阵站位。**固定 9 个格子**（概念图的九宫格），null = 空位。\n" +
             "索引就是格子序号（行优先），能不能上阵由 SpiritFormationLayout 决定")]
    public List<NpcDefinition> 战阵站位 = new List<NpcDefinition>();

    [Tooltip("点了「上阵」之后、等待玩家点一个空格子放进去的真灵。null = 没有待上阵")]
    public NpcDefinition 待上阵真灵;

    /// <summary>
    /// 保证所有列表都不为 null。
    /// Unity 在反序列化 / 重建组件时可能把这些 List 留成 null，
    /// 那样后面所有的 .Count / .Contains 都会直接抛空引用。
    /// </summary>
    void Awake() => EnsureLists();

    void OnEnable() => EnsureLists();

    /// <summary>这门功法学会了吗</summary>
    public bool 已学(GongFaDefinition g) => g != null && 已学功法 != null && 已学功法.Contains(g);

    /// <summary>学会一门功法</summary>
    public bool 学会(GongFaDefinition g)
    {
        if (g == null) return false;
        if (已学功法 == null) 已学功法 = new List<GongFaDefinition>();
        已学功法.RemoveAll(g=>g==null);
        if (已学功法.Contains(g)) return false;
        已学功法.Add(g);
        RaiseChanged();
        return true;
    }

    /// <summary>把没学会的补进来（接线时用，保证至少能选）</summary>
    public void 补齐已学功法(IEnumerable<GongFaDefinition> 全部)
    {
        if (全部 == null) return;
        if (已学功法 == null) 已学功法 = new List<GongFaDefinition>();
        foreach (var g in 全部)
            if (g != null && !已学功法.Contains(g)) 已学功法.Add(g);
        RaiseChanged();
    }

    // ============================================================ 能力「是否已获得」

    /// <summary>这门主动神通获得了吗</summary>
    public bool 已获得主动(ActiveDivineAbility a)
        => a != null && 已获得主动神通 != null && 已获得主动神通.Contains(a);

    /// <summary>获得一门主动神通（已经有了返回 false —— 用户要求"无法重复习得"）</summary>
    public bool 获得主动(ActiveDivineAbility a)
    {
        if (a == null) return false;
        if (已获得主动神通 == null) 已获得主动神通 = new List<ActiveDivineAbility>();
        if (已获得主动神通.Contains(a)) return false;
        已获得主动神通.Add(a);
        RaiseChanged();
        return true;
    }

    /// <summary>这门被动神通获得了吗</summary>
    public bool 已获得被动(PassiveDivineAbility p)
        => p != null && 已获得被动神通 != null && 已获得被动神通.Contains(p);

    /// <summary>获得一门被动神通。获得即启用（从"已停用"里摘掉）</summary>
    public bool 获得被动(PassiveDivineAbility p)
    {
        if (p == null) return false;
        if (已获得被动神通 == null) 已获得被动神通 = new List<PassiveDivineAbility>();
        if (已获得被动神通.Contains(p)) return false;
        已获得被动神通.Add(p);
        if (已停用被动 != null) 已停用被动.Remove(p);
        关闭其他护罩(p);
        RaiseChanged();
        return true;
    }

    // ============================================================ 背包数量（同一件物品有 N 个 = N 条）

    /// <summary>背包里有几个</summary>
    public int 物品数量(ItemDefinition 物品)
    {
        if (物品 == null || 物品 == null) return 0;
        if (this.物品 == null) return 0;
        int n = 0;
        foreach (var it in this.物品) if (it == 物品) n++;
        return n;
    }

    /// <summary>给物品（任务奖励、使用器都走这里）</summary>
    public void 给物品(ItemDefinition 物品, int 数量 = 1)
    {
        if (物品 == null || 数量 <= 0) return;
        if (this.物品 == null) this.物品 = new List<ItemDefinition>();
        for (int i = 0; i < 数量; i++) this.物品.Add(物品);
        RaiseChanged();
    }

    /// <summary>扣物品（不够就返回 false，什么都不扣）</summary>
    public bool 移除物品(ItemDefinition 物品, int 数量 = 1)
    {
        if (物品 == null || 数量 <= 0) return false;
        if (this.物品 == null || 物品数量(物品) < 数量) return false;
        for (int i = 0; i < 数量; i++) this.物品.Remove(物品);
        RaiseChanged();
        return true;
    }

    public void EnsureLists()
    {
        if (物品 == null) 物品 = new List<ItemDefinition>();
        if (神通 == null) 神通 = new List<DivineAbilityDefinition>();
        if (已获得主动神通 == null) 已获得主动神通 = new List<ActiveDivineAbility>();
        if (已获得被动神通 == null) 已获得被动神通 = new List<PassiveDivineAbility>();
        if (已学功法 == null) 已学功法 = new List<GongFaDefinition>();
        if (法宝 == null) 法宝 = new List<TreasureDefinition>();
        if (灵阵 == null) 灵阵 = new List<SpiritArrayDefinition>();
        if (坐骑 == null) 坐骑 = new List<MountDefinition>();
        if (已学坐骑 == null) 已学坐骑 = new List<string>();
        已学坐骑.RemoveAll(id => id == "mount_julong_01");
        坐骑.RemoveAll(m => m == null || m.坐骑id == "mount_julong_01");
        if (当前坐骑 != null && 当前坐骑.坐骑id == "mount_julong_01") 当前坐骑 = null;
        EnsureSkillSlots();
        if (已停用被动 == null) 已停用被动 = new List<PassiveDivineAbility>();
        if (已获得真灵 == null) 已获得真灵 = new List<NpcDefinition>();
        EnsureFormationSlots();
        从面板库灌目录();
        if(当前功法==null)当前功法=PanelDatabase.取()?.功法.Find(g=>g!=null && g.功法id=="gongfa_taixu_lianqi");
        if (已拥有法宝 == null) 已拥有法宝 = new List<string>();
        if (!收服系统已初始化) {
            已获得真灵.Clear(); for(int i=0;i<战阵站位.Count;i++) 战阵站位[i]=null;
            待上阵真灵=null; 收服系统已初始化=true;
        }
        if (当前法宝 != null && !已拥有(当前法宝)) 当前法宝=null;
        坐骑.RemoveAll(m => m == null || m.坐骑id == "mount_julong_01");
        if (当前坐骑 != null && !已学坐骑.Contains(当前坐骑.坐骑id)) 当前坐骑 = null;
        迁移四御学习记录();
        PassiveDivineAbility 保留 = null;
        foreach (var p in 已获得被动神通)
            if (是减伤护罩(p) && !已停用被动.Contains(p))
            {
                if (保留 == null) 保留 = p;
                else 已停用被动.Add(p);
            }
    }

    public static bool 是减伤护罩(PassiveDivineAbility p) => p != null
        && (p.结算方式 == PassiveSkillKind.物理护罩 || p.结算方式 == PassiveSkillKind.特殊护罩
            || p.结算方式 == PassiveSkillKind.双重护罩);

    void 关闭其他护罩(PassiveDivineAbility p)
    {
        if (!是减伤护罩(p)) return;
        if (已停用被动 == null) 已停用被动 = new List<PassiveDivineAbility>();
        foreach (var 其他 in 已获得被动神通)
            if (其他 != p && 是减伤护罩(其他) && !已停用被动.Contains(其他)) 已停用被动.Add(其他);
    }

    void 迁移四御学习记录()
    {
        var 主动 = 神通.Find(a => a is ActiveDivineAbility && a.神通id == "ability_huzhao_juedui") as ActiveDivineAbility;
        if (主动 == null) return;
        bool 已学 = 已获得被动神通.Exists(p => p != null && p.神通id == 主动.神通id);
        if (已学 && !已获得主动神通.Contains(主动)) 已获得主动神通.Add(主动);
        已获得被动神通.RemoveAll(p => p != null && p.神通id == 主动.神通id);
        已停用被动.RemoveAll(p => p != null && p.神通id == 主动.神通id);
    }

    /// <summary>
    /// **主动技能槽永远是 <see cref="技能槽位数"/> 格**（缺的补 null）。
    ///
    /// 为什么必须有这个（用户 2026-09-28 报的 bug）：
    ///   UI 上三个页面各画了 **6 个格子**，但 `UIPanelData.主动技能` 在场景里
    ///   序列化出来是**空列表（Count = 0）**。于是：
    ///     · 画面：6 个空格子，看着完全空 ✓
    ///     · 数据：`HasEmptySlot()` 遍历 0 格 → 一个空位都没找到 → 返回 false
    ///     · `UIEntryList.OnRowAction` 据此弹出「主动技能装备栏已经满了」✗
    ///   也就是**"栏是空的却说满了"** —— 根因就是"UI 的格数"和"数据列表的长度"是两套东西。
    ///
    /// ⚠️ 别改成"只在 null 时 new 一个空 List"：那正是原来的写法，
    ///    场景里存的是**已存在的空 List**，`null` 判断根本不成立，所以修不掉。
    ///
    /// 与 `SceneRigSyncer` / builder 的分工：那些负责把 **6 个格子摆出来**，
    /// 这里负责保证**数据列表也是 6 格**，两边格数必须一致。
    /// </summary>
    public const int 技能槽位数 = 6;

    void EnsureSkillSlots()
    {
        if (主动技能 == null) 主动技能 = new List<UnityEngine.Object>();
        while (主动技能.Count < 技能槽位数) 主动技能.Add(null);
        while (主动技能.Count > 技能槽位数) 主动技能.RemoveAt(主动技能.Count - 1);
        // 兼容旧状态：同一神通只保留最前面的槽位，法宝/灵阵不受此规则影响。
        for (int i = 0; i < 主动技能.Count; i++)
            for (int j = 0; j < i; j++)
                if (同一主动神通(主动技能[i], 主动技能[j]))
                {
                    主动技能[i] = null;
                    break;
                }
    }
    static bool 同一主动神通(UnityEngine.Object a, UnityEngine.Object b)
    {
        var x = a as ActiveDivineAbility;
        var y = b as ActiveDivineAbility;
        return x != null && y != null && (x == y ||
            !string.IsNullOrEmpty(x.神通id) && x.神通id == y.神通id);
    }

    void 清除其他主动神通槽(int target, UnityEngine.Object content)
    {
        for (int i = 0; i < 主动技能.Count; i++)
            if (i != target && 同一主动神通(主动技能[i], content)) 主动技能[i] = null;
    }

    /// <summary>
    /// 把**目录**（神通 / 法宝 / 灵阵 / 坐骑 / 可获真灵）从运行时聚合库灌进来。
    ///
    /// 为什么必须这么做（用户 2026-09-27 报的 bug）：
    ///   这些目录原本是**序列化在场景文件里**的，而 `UIPanelData` 每个场景各一份 ⇒
    ///   只有当初被灌过表的场景是对的。实测：古古镇 神通全表=11、宗门=0，
    ///   于是在宗门打开神通页一片空白。
    ///   改成运行时从 `PanelDatabase.取()` 读，**唯一源头**，哪个场景进都一样。
    ///
    /// ⚠️ **只灌"目录"**，绝不碰玩家自己的数据：
    ///    物品（背包）/ 已获得主动·被动神通 / 已学功法 / 已停用被动 / 技能槽 / 战阵站位。
    ///    那些是玩家的，由**存档**负责恢复。
    ///
    /// 幂等：每次调都按库重灌一遍（库是常量，重灌不丢东西）。
    /// </summary>
    void 从面板库灌目录()
    {
        var 库 = PanelDatabase.取();

        // ★ `Resources.Load` 读不到就是 **null** —— 以前这里直接 `return`（静默失败），
        //   表现成"神通页 / 法宝页一片空白"，而且**一句日志都没有**，极难排查。
        //   现在：第一次读不到就**明确报错并说怎么修**，之后不再刷屏。
        if (库 == null)
        {
            if (!报过缺面板库)
            {
                报过缺面板库 = true;
                Debug.LogError("[面板库] 读不到 Assets/resources/面板/面板库.asset —— " +
                    "神通/法宝/灵阵/坐骑/真灵 目录会是空的（面板页空白）。" +
                    "修法：菜单「修仙/面板/收集面板目录」生成它。");
            }
            return;
        }

        // 列表本身也要防 null（ScriptableObject 被手改过 / 旧版本资产可能字段为空）
        if (库.神通 != null && 库.神通.Count > 0)
        {
            神通 = new List<DivineAbilityDefinition>(库.神通);
        }
        if (库.法宝 != null && 库.法宝.Count > 0)
        {
            法宝 = new List<TreasureDefinition>(库.法宝);
        }
        if (库.灵阵 != null && 库.灵阵.Count > 0)
        {
            灵阵 = new List<SpiritArrayDefinition>(库.灵阵);
        }
        if (库.坐骑 != null && 库.坐骑.Count > 0)
        {
            坐骑 = new List<MountDefinition>(库.坐骑);
            if (坐骑.Count > 1) 坐骑.Sort(比坐骑);       // 和编辑器同一套排序，避免两处各排一套
        }
    }

    /// <summary>「读不到面板库」只报一次，别每帧刷屏</summary>
    static bool 报过缺面板库;

    /// <summary>战阵站位列表永远保持 9 格（缺的补 null）</summary>
    void EnsureFormationSlots()
    {
        if (战阵站位 == null) 战阵站位 = new List<NpcDefinition>();
        while (战阵站位.Count < SpiritFormationLayout.格子数) 战阵站位.Add(null);
        while (战阵站位.Count > SpiritFormationLayout.格子数) 战阵站位.RemoveAt(战阵站位.Count - 1);
    }

    /// <summary>
    /// **把面板上的玩法数据全部清空**（背包 / 法宝 / 灵阵 / 坐骑 / 已获真灵 /
    /// 已学功法 / 已获得主被动神通 / 技能槽 / 战阵 / 经验 / 当前功法）。
    ///
    /// 用途：① 新档口径（用户 2026-09-27：最开始创建的角色背包里什么都没有）
    ///       ② 编辑器调试菜单「清空当前场景面板」
    ///
    /// ⚠️ 这里清的是**面板数据本身**，所以它会跟着场景一起被保存 ——
    /// 场景里预置过物品的话，光靠存档里的空列表是清不掉的，必须调这个方法。
    /// 另外 `神通`（全量表）和 `玩家属性`（定义）**不清**，它们不是"进度"。
    /// 被动神通按用户口径全部改为「停用」状态（没获得的被动 = 停用）。
    /// </summary>
    public void 清空玩法数据()
    {
        EnsureLists();
        物品 = new List<ItemDefinition>();
        已拥有法宝.Clear(); 当前法宝=null; 收服系统已初始化=false;
        灵阵 = new List<SpiritArrayDefinition>();
        坐骑 = new List<MountDefinition>();
        已学坐骑.Clear();
        当前坐骑 = null;
        已获得真灵 = new List<NpcDefinition>();
        已学功法 = new List<GongFaDefinition>();
        已获得主动神通 = new List<ActiveDivineAbility>();
        已获得被动神通 = new List<PassiveDivineAbility>();
        当前功法 = null;
        四御已激活 = false; 绝对护罩剩余次数 = 0; 青山剑有效击杀=0;
        待装备神通 = null;
        当前经验 = 0;
        突破所需总经验 = 0;
        for (int i = 0; i < 主动技能.Count; i++) 主动技能[i] = null;
        EnsureFormationSlots();
        for (int i = 0; i < 战阵站位.Count; i++) 战阵站位[i] = null;
        已停用被动 = new List<PassiveDivineAbility>();
        foreach (var a in 神通) if (a is PassiveDivineAbility p) 已停用被动.Add(p);
        RaiseChanged();
    }

    /// <summary>背包/法宝/灵阵/坐骑/真灵 是不是都为空（用来判定"这是一份新档"）</summary>
    public bool 玩法数据为空()
    {
        return (物品 == null || 物品.Count == 0)
            && (法宝 == null || 法宝.Count == 0)
            && (灵阵 == null || 灵阵.Count == 0)
            && (已学坐骑 == null || 已学坐骑.Count == 0)
            && (已获得真灵 == null || 已获得真灵.Count == 0);
    }

    /// <summary>数据发生变化（装备变更 / 被动启停）。UI 收到后自行刷新</summary>
    public event Action Changed;

    /// <summary>需要给玩家一句提示（装备栏满、等待选槽位等）</summary>
    public event Action<string> Hint;

    /// <summary>通知 UI 刷新</summary>
    public void RaiseChanged() => Changed?.Invoke();

    /// <summary>弹一句提示</summary>
    public void ShowHint(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        Hint?.Invoke(message);
    }

    // ---------------------------------------------------------------- 被动启停

    /// <summary>某个被动神通是否处于启用状态</summary>
    public bool IsPassiveEnabled(PassiveDivineAbility ability)
    {
        EnsureLists();
        // ★ 用户 2026-09-26 报的 bug：**没获得 = 不生效**。
        //   原来只判「已停用」列表 —— 而"停用列表为空 = 认为全开"是旧口径（主角天赋全会），
        //   结果主角什么都没获得，御风却照飞（YufengFlight 第 158 行就是调这个方法判的）。
        //   所有权是硬门槛，必须挡在最前面。
        if (ability == null) return false;
        if (ability.结算方式 == PassiveSkillKind.绝对护罩) return false;
        if (!已获得被动(ability)) return false;
        return !已停用被动.Contains(ability);
    }

    /// <summary>启用/停用某个被动神通，返回切换后的状态</summary>
    public bool TogglePassive(PassiveDivineAbility ability)
    {
        EnsureLists();
        if (ability == null) return false;

        bool nowEnabled;
        if (已停用被动.Contains(ability)) { 已停用被动.Remove(ability); nowEnabled = true; }
        else { 已停用被动.Add(ability); nowEnabled = false; }
        if (nowEnabled) 关闭其他护罩(ability);

        // ★ 启用【凭虚御风】→ **自动取消装备坐骑**（用户 2026-09-24 定的规则）
        bool 卸了坐骑 = nowEnabled && 取消坐骑因为御风(ability);

        RaiseChanged();
        ShowHint(ability.DisplayName + (nowEnabled ? " 已启用" : " 已停用")
                 + (卸了坐骑 ? "，坐骑已自动取消装备" : ""));
        return nowEnabled;
    }

    /// <summary>直接设置被动神通状态</summary>
    public void SetPassiveEnabled(PassiveDivineAbility ability, bool enabled)
    {
        if (ability == null) return;
        EnsureLists();
        if (enabled) 已停用被动.Remove(ability);
        else if (!已停用被动.Contains(ability)) 已停用被动.Add(ability);
        if (enabled) 关闭其他护罩(ability);

        // 同 TogglePassive：启用御风就卸坐骑
        if (enabled && 取消坐骑因为御风(ability))
            ShowHint("【" + ability.DisplayName + "】已启用，坐骑已自动取消装备");

        RaiseChanged();
    }

    // ---------------------------------------------------------------- 坐骑 ⇄ 御风 互斥

    /// <summary>
    /// **装备 / 取消装备坐骑**。★ 所有入口都走这里 ——
    /// 互斥规则（装备坐骑就自动停用【凭虚御风】）只在这一处实现。
    ///
    /// 用户 2026-09-24：「装备坐骑时会自动停用御风这个被动神通。
    /// 启用凭虚御风时会自动取消装备坐骑」—— 两条规则一起保证两者**不会同时生效**，
    /// Shift 那个共用键才不会有歧义。
    /// </summary>
    public void 设置当前坐骑(MountDefinition m)
    {
        EnsureLists();
        if (当前坐骑 == m) return;
        if (m != null && !已学坐骑.Contains(m.坐骑id)) { ShowHint("请先学习此坐骑的御兽契"); return; }
        var 修炼 = FindObjectOfType<PlayerCultivation>();
        if (m != null && 修炼 != null && 修炼.等级 < m.修炼门槛)
        { ShowHint("驾驭「" + m.DisplayName + "」需要境界序号 " + m.修炼门槛); return; }

        当前坐骑 = m;

        bool 停了御风 = m != null && 停用御风被动();
        RaiseChanged();

        if (m != null)
            ShowHint("已装备坐骑「" + m.DisplayName + "」"
                     + (停了御风 ? "，【凭虚御风】已自动停用" : ""));
    }

    /// <summary>取消装备当前坐骑。返回是否真的卸掉了（本来就没装 → false）</summary>
    public bool 取消装备坐骑()
    {
        EnsureLists();
        if (当前坐骑 == null) return false;
        当前坐骑 = null;
        return true;
    }

    /// <summary>表里那条【凭虚御风】被动（按 <see cref="御风神通id"/> 找）</summary>
    public PassiveDivineAbility 取御风神通()
    {
        EnsureLists();
        if (神通 == null) return null;
        foreach (var a in 神通)
        {
            var p = a as PassiveDivineAbility;
            if (p != null && !string.IsNullOrEmpty(御风神通id) && p.神通id == 御风神通id) return p;
        }
        return null;
    }

    /// <summary>停用【凭虚御风】。返回是否真的改了状态（本来就是停用 → false）</summary>
    public bool 停用御风被动()
    {
        var yf = 取御风神通();
        if (yf == null || !IsPassiveEnabled(yf)) return false;
        已停用被动.Add(yf);
        return true;
    }

    /// <summary>这条被动是不是【凭虚御风】</summary>
    bool 是御风(PassiveDivineAbility ability)
        => ability != null && !string.IsNullOrEmpty(御风神通id) && ability.神通id == 御风神通id;

    /// <summary>启用御风时要顺手卸坐骑。返回是否真的卸了（本来就没坐骑 → false）</summary>
    bool 取消坐骑因为御风(PassiveDivineAbility ability)
        => 是御风(ability) && 取消装备坐骑();

    // ---------------------------------------------------------------- 装备槽

    /// <summary>某个内容是否已经装在主动技能槽里</summary>
    public bool IsEquipped(UnityEngine.Object content)
    {
        EnsureLists();
        return content != null && 主动技能.Contains(content);
    }

    /// <summary>返回它所在的槽位序号，没装返回 -1</summary>
    public int IndexOfEquipped(UnityEngine.Object content)
        => content == null || 主动技能 == null ? -1 : 主动技能.IndexOf(content);

    /// <summary>还有没有空格</summary>
    public bool HasEmptySlot()
    {
        EnsureLists();
        for (int i = 0; i < 主动技能.Count; i++) if (主动技能[i] == null) return true;
        return false;
    }

    /// <summary>第一个空格序号，没有返回 -1</summary>
    public int FirstEmptySlot()
    {
        EnsureLists();
        for (int i = 0; i < 主动技能.Count; i++) if (主动技能[i] == null) return i;
        return -1;
    }

    /// <summary>把一个内容放进主动技能槽（越界自动忽略）</summary>
    public void EquipToSlot(int index, UnityEngine.Object content)
    {
        EnsureLists();
        if (content is PassiveDivineAbility) { ShowHint("被动神通不能装备到主动技能栏"); return; }
        if (index < 0 || index >= 主动技能.Count) return;
        清除其他主动神通槽(index, content);
        主动技能[index] = content;
        RaiseChanged();
    }

    /// <summary>装备星位之间交换，校验拖拽原内容，统一通知一次。</summary>
    public bool 交换主动槽(int from,int to,UnityEngine.Object expected)
    {
        EnsureLists();
        if(from<0 || to<0 || from>=主动技能.Count || to>=主动技能.Count || 主动技能[from]!=expected || expected==null || expected is PassiveDivineAbility)return false;
        if(from==to)return true;
        var target=主动技能[to];主动技能[to]=expected;主动技能[from]=target;RaiseChanged();return true;
    }

    /// <summary>自动放进第一个空格，返回是否成功</summary>
    public bool EquipToFirstEmpty(UnityEngine.Object content)
    {
        if (content is PassiveDivineAbility) return false;
        int i = FirstEmptySlot();
        if (i < 0) return false;
        EquipToSlot(i, content);
        return true;
    }

    /// <summary>清空某个主动技能槽</summary>
    public void ClearSlot(int index)
    {
        EnsureLists();
        if (index < 0 || index >= 主动技能.Count) return;
        主动技能[index] = null;
        RaiseChanged();
    }

    /// <summary>把某个内容从装备栏里卸下（它可能在任意一格）</summary>
    public bool Unequip(UnityEngine.Object content)
    {
        int i = IndexOfEquipped(content);
        if (i < 0) return false;
        主动技能[i] = null;
        RaiseChanged();
        return true;
    }

    // ---------------------------------------------------------------- 待装备流程

    /// <summary>进入「等待选槽位」状态</summary>
    public void BeginPendingEquip(ActiveDivineAbility ability)
    {
        待装备神通 = ability;
        RaiseChanged();
    }

    /// <summary>取消待装备</summary>
    public void CancelPendingEquip()
    {
        if (待装备神通 == null) return;
        待装备神通 = null;
        RaiseChanged();
    }

    /// <summary>
    /// 玩家点了某个主动技能槽。
    /// 有待装备时 → 放入；否则 → 单纯选中看信息。
    /// 返回是否发生了「放入」。
    /// </summary>
    public bool HandleSlotClicked(int index)
    {
        EnsureLists();
        if (待装备神通 == null) return false;
        if (index < 0 || index >= 主动技能.Count) return false;

        if (主动技能[index] != null)
        {
            var occupant = 主动技能[index] as IPanelEntry;
            ShowHint("这一格已经放了「" + (occupant != null ? occupant.DisplayName : 主动技能[index].name)
                     + "」，请点一个空格");
            return false;
        }

        var ability = 待装备神通;
        清除其他主动神通槽(index, ability);
        主动技能[index] = ability;
        待装备神通 = null;

        ShowHint("已装备「" + ability.DisplayName + "」");
        RaiseChanged();
        return true;
    }

    // ---------------------------------------------------------------- 战阵真灵

    /// <summary>这个真灵现在在阵上吗</summary>
    public bool IsSpiritOnField(NpcDefinition spirit)
    {
        EnsureLists();
        return spirit != null && 战阵站位.Contains(spirit);
    }

    /// <summary>它在哪个格子，没上阵返回 -1</summary>
    public int IndexOfSpirit(NpcDefinition spirit)
    {
        EnsureLists();
        return spirit == null ? -1 : 战阵站位.IndexOf(spirit);
    }

    /// <summary>已上阵几个</summary>
    public int 已上阵数量
    {
        get
        {
            EnsureLists();
            int n = 0;
            for (int i = 0; i < 战阵站位.Count; i++) if (战阵站位[i] != null) n++;
            return n;
        }
    }

    /// <summary>还剩几个上阵名额</summary>
    public int 剩余名额 => Mathf.Max(0, SpiritFormationLayout.可上阵数 - 已上阵数量);

    /// <summary>
    /// 名额用完了吗。
    /// **九格全部可以上阵**，只是同时最多上 <see cref="SpiritFormationLayout.可上阵数"/> 个；
    /// 满了之后 UI 会把剩下的空格画成「已满」并点不动。
    /// </summary>
    public bool 战阵已满 => 剩余名额 <= 0;

    /// <summary>还有没有能上阵的空格子（既要有空格，也要还有名额；中间那格是玩家自己，不算）</summary>
    public bool HasEmptySpiritSlot()
    {
        EnsureLists();
        if (战阵已满) return false;
        for (int i = 0; i < 战阵站位.Count; i++)
            if (SpiritFormationLayout.格子可上阵(i) && 战阵站位[i] == null) return true;
        return false;
    }

    /// <summary>第一个能上阵的空格子序号，没有返回 -1</summary>
    public int FirstEmptySpiritSlot()
    {
        EnsureLists();
        for (int i = 0; i < 战阵站位.Count; i++)
            if (SpiritFormationLayout.格子可上阵(i) && 战阵站位[i] == null) return i;
        return -1;
    }

    /// <summary>进入「等玩家点格子」的状态</summary>
    public void BeginPendingSpirit(NpcDefinition spirit)
    {
        if (spirit == null || !已获得真灵.Contains(spirit)) return;
        待上阵真灵 = spirit;
        RaiseChanged();
    }

    /// <summary>取消待上阵</summary>
    public void CancelPendingSpirit()
    {
        if (待上阵真灵 == null) return;
        待上阵真灵 = null;
        RaiseChanged();
    }

    /// <summary>
    /// 玩家点了一个战阵格子：有待上阵的真灵就放进去。
    /// 返回是否真的放进去了（没待上阵 = false，表示「只是看看信息」）。
    /// </summary>
    public bool HandleSpiritSlotClicked(int slot)
    {
        EnsureLists();
        if (待上阵真灵 == null) return false;
        if (slot < 0 || slot >= 战阵站位.Count) return false;

        if (战阵已满)
        {
            ShowHint("战阵已经满了（最多同时上阵 " + SpiritFormationLayout.可上阵数 + " 个），先下阵一个");
            return false;
        }

        if (!SpiritFormationLayout.格子可上阵(slot))
        {
            ShowHint("中间那一格是你自己站的位置，不能放真灵 —— 请点外围的 8 格");
            return false;
        }

        if (战阵站位[slot] != null)
        {
            ShowHint("这一格已经站了「" + 战阵站位[slot].DisplayName + "」，请点一个空格子");
            return false;
        }

        var spirit = 待上阵真灵;
        战阵站位[slot] = spirit;
        待上阵真灵 = null;

        ShowHint("「" + spirit.DisplayName + "」已上阵");
        RaiseChanged();
        return true;
    }

    /// <summary>把一个真灵从阵上撤下来</summary>
    public bool UnequipSpirit(NpcDefinition spirit)
    {
        EnsureLists();
        int i = IndexOfSpirit(spirit);
        if (i < 0) return false;

        战阵站位[i] = null;
        if (待上阵真灵 == spirit) 待上阵真灵 = null;

        ShowHint("「" + spirit.DisplayName + "」已下阵");
        RaiseChanged();
        return true;
    }

    /// <summary>点列表行上的「上阵 / 下阵」按钮</summary>
    public void ToggleSpirit(NpcDefinition spirit)
    {
        EnsureLists();
        if (spirit == null) return;

        if (IsSpiritOnField(spirit)) { UnequipSpirit(spirit); return; }

        if (!HasEmptySpiritSlot())
        {
            ShowHint("战阵最多同时上阵 " + SpiritFormationLayout.可上阵数 + " 个真灵，先下阵一个再上");
            return;
        }

        BeginPendingSpirit(spirit);
        ShowHint("「" + spirit.DisplayName + "」待上阵：请点一个空阵位");
    }

    // ---------------------------------------------------------------- 真灵列表排序

    /// <summary>真灵列表里的「大类」排序键：**妖魔在上、人类在下**（不许混在一起）</summary>
    public static int 真灵大类序(NpcKind 类型)
    {
        switch (类型)
        {
            case NpcKind.妖魔: return 0;
            case NpcKind.人类: return 1;
            default: return 2;      // 兽类 / 中立 / 未指定 —— 目前没有这类真灵，兜底排最后
        }
    }

    /// <summary>
    /// **一族真灵的家族名。**
    ///
    /// 取模型 prefab 的**文件夹名**，再去掉结尾的变体号 —— 因为「同一族的几个蒙皮版本」
    /// 在本工程里有两种摆法，都要归到同一个家族上：
    ///   · 共用一个文件夹：白熊精 `BaiXiongJing/BaiXiongJing_01..04`
    ///   · 一个版本一个文件夹：白鹿精 `BaiLuJing_01/…`、赤蟒筋 `ChiMangJin_01/…`
    ///
    /// 没有模型路径的真灵退回用 id（去掉 `_01` 这类后缀）。
    /// </summary>
    public static string 取真灵家族(NpcDefinition d)
    {
        if (d == null) return "";

        string 路径 = d.模型资源路径;
        if (!string.IsNullOrEmpty(路径))
        {
            int 尾 = 路径.LastIndexOf('/');
            if (尾 > 0)
            {
                string 文件夹 = 路径.Substring(0, 尾);
                int 再尾 = 文件夹.LastIndexOf('/');
                string 名 = 再尾 >= 0 ? 文件夹.Substring(再尾 + 1) : 文件夹;
                return 去掉变体号(名);
            }
        }
        return 去掉变体号(d.id);
    }

    /// <summary>去掉结尾的 `_01` / `_2` 这类**纯数字**变体号（别的后缀一律不动）</summary>
    static string 去掉变体号(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int i = s.LastIndexOf('_');
        if (i <= 0 || i == s.Length - 1) return s;

        for (int k = i + 1; k < s.Length; k++)
            if (s[k] < '0' || s[k] > '9') return s;

        return s.Substring(0, i);
    }

    /// <summary>
    /// **战阵真灵列表的排序规则**（用户要求）：
    ///   1. 妖魔在前、人类在后（不许混在一起）
    ///   2. 同一族的挨在一起（<see cref="取真灵家族"/>）
    ///   3. 族内**由弱到强**（境界升序）—— 和「幼 / 少 / 壮 / 王」那套称呼同序
    ///   4. 最后按 id 兜底，保证顺序稳定
    /// </summary>
    public static int 比真灵(NpcDefinition a, NpcDefinition b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        int r = 真灵大类序(a.类型).CompareTo(真灵大类序(b.类型));
        if (r != 0) return r;

        r = string.Compare(取真灵家族(a), 取真灵家族(b), StringComparison.OrdinalIgnoreCase);
        if (r != 0) return r;

        r = a.境界.CompareTo(b.境界);
        if (r != 0) return r;

        return string.Compare(a.id, b.id, StringComparison.Ordinal);
    }

    /// <summary>已获得的真灵（给列表用）。**按「妖魔 → 人类 → 同族相邻 → 由弱到强」排好序**</summary>
    public List<IPanelEntry> GetSpirits()
    {
        EnsureLists();

        // 排的是副本，不动 已获得真灵 本身的顺序（存档 / 别的模块可能按原序遍历）
        var 列表 = new List<NpcDefinition>();
        foreach (var d in 已获得真灵) if (d != null) 列表.Add(d);
        列表.Sort(比真灵);

        return ToEntries(列表);
    }

    // ---------------------------------------------------------------- 查询

    /// <summary>突破所需总经验，未显式设置时回退到境界定义</summary>
    public long GetRequiredExp()
    {
        if (突破所需总经验 > 0) return 突破所需总经验;
        if (玩家属性 != null && 玩家属性.境界 != null) return 玩家属性.境界.所需总经验;
        return 0;
    }

    public string GetRealmName()
    {
        if (玩家属性 != null && 玩家属性.境界 != null) return 玩家属性.境界.境界名;
        return "未设定境界";
    }

    /// <summary>把某个类型化列表转成 UI 能吃的条目序列（自动过滤空引用）</summary>
    public static List<IPanelEntry> ToEntries<T>(List<T> source) where T : UnityEngine.Object
    {
        var list = new List<IPanelEntry>();
        if (source == null) return list;
        foreach (var o in source)
        {
            if (o == null) continue;
            if (o is IPanelEntry entry) list.Add(entry);
        }
        return list;
    }

    public List<IPanelEntry> GetItems()        { return ToEntries(物品); }

    /// <summary>神通页只列**已经获得的**（没获得的要用物品学 —— 用户 2026-09-26 定的）</summary>
    public List<IPanelEntry> GetAbilities()
    {
        var 出 = new List<IPanelEntry>();
        if (神通 == null) return 出;
        foreach (var a in 神通)
        {
            if (a == null) continue;
            if (a is ActiveDivineAbility act) { if (已获得主动(act)) 出.Add(act); }
            else if (a is PassiveDivineAbility p) { if (已获得被动(p)) 出.Add(p); }
        }
        return 出;
    }
    public List<IPanelEntry> GetTreasures()    { return ToEntries(法宝.FindAll(t=>已拥有(t))); }
    public List<IPanelEntry> GetSpiritArrays() { return ToEntries(灵阵); }

    /// <summary>拥有的坐骑（给坐骑页列表用）。**按修炼门槛升序**，同一门槛按 id 稳定排序</summary>
    public List<IPanelEntry> GetMounts()
    {
        EnsureLists();
        var 列表 = new List<MountDefinition>();
        foreach (var m in 坐骑) if (m != null && m.坐骑id != "mount_julong_01" && 已学坐骑.Contains(m.坐骑id)) 列表.Add(m);
        列表.Sort(比坐骑);
        return ToEntries(列表);
    }

    /// <summary>坐骑排序：门槛低的在前（由弱到强），同门槛按 id 兜底保证稳定</summary>
    public static int 比坐骑(MountDefinition a, MountDefinition b)
    {
        if (a == b) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        int r = a.修炼门槛.CompareTo(b.修炼门槛);
        if (r != 0) return r;
        return string.Compare(a.坐骑id, b.坐骑id, StringComparison.Ordinal);
    }

    /// <summary>「生效中的被动神通」：只列启用状态的</summary>
    public List<IPanelEntry> GetPassiveAbilities()
    {
        var list = new List<IPanelEntry>();
        if (神通 == null) return list;
        foreach (var a in 神通)
        {
            if (a == null) continue;
            if (a is PassiveDivineAbility p && 已获得被动(p) && IsPassiveEnabled(p)) list.Add(p);
        }
        return list;
    }

    /// <summary>当前启用中的被动神通（类型化版本，给结算系统用）</summary>
    public List<PassiveDivineAbility> GetEnabledPassives()
    {
        var list = new List<PassiveDivineAbility>();
        if (神通 == null) return list;
        foreach (var a in 神通)
            if (a is PassiveDivineAbility p && 已获得被动(p) && IsPassiveEnabled(p)) list.Add(p);
        return list;
    }

    // ---- ASCII 别名 ----
    public bool IsEquippedInSlot(UnityEngine.Object content) => IsEquipped(content);
    public bool HasFreeSlot() => HasEmptySlot();
    public int FirstFreeSlot() => FirstEmptySlot();
    public bool ClickSlot(int index) => HandleSlotClicked(index);
    public ActiveDivineAbility PendingEquip => 待装备神通;
}
