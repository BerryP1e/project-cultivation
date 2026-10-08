using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 按【当前功法】与【启用中的被动神通】动态装卸玩家身上的能力组件。
///
/// 规则（按策划说明）：
///   · 太虚炼气诀 → basic_remoteattack_01；青山剑由法宝装备独立管理
///   · 凭虚御风启用 → 装 YufengFlight + YufengVfx；停用就卸掉，重新启用再装上
///
/// 组件按【类名】配置，运行时用反射找类型 —— 这样加新功法/新神通只要在表里加一行，
/// 不用改代码。（Unity 不能序列化 System.Type，所以只能存字符串。）
/// </summary>
public class PlayerAbilityLoader : MonoBehaviour
{
    [Serializable]
    public class 普攻方法绑定
    {
        [Tooltip("功法表里「普攻方法id」的值")]
        public string 方法id = "";
        [Tooltip("提供这个普攻的组件类名")]
        public string 组件类名 = "";
    }

    [Serializable]
    public class 被动神通绑定
    {
        [Tooltip("被动神通表里的 神通id")]
        public string 神通id = "";
        [Tooltip("启用这个被动时要挂上的组件类名，可以多个")]
        public string[] 组件类名 = new string[0];
    }

    [Header("数据源")]
    [Tooltip("角色面板数据（功法 + 被动神通启用状态都从这里读）。留空则自动找 CharacterUI 上的")]
    public UIPanelData 面板数据;

    [Header("配置表")]
    public List<普攻方法绑定> 普攻方法表 = new List<普攻方法绑定>();
    public List<被动神通绑定> 被动神通表 = new List<被动神通绑定>();

    /// <summary>
    /// 【内置登记】表里没写、但代码知道的能力组件：`{ 普攻方法id / 神通id, 组件类名 }`。
    ///
    /// 为什么要有它：上面那两张表是**逐场景序列化**的（Player 在 5 个游玩场景里各一份），
    /// 加一个新普攻就得动 5 个场景文件，而**存场景会把场景里那份运行时 UI 列表一起重排**
    /// （踩坑 A8 / B94：一次保存带出上千行无关改动）。所以新加的能力走这里**在运行时补进内存**，
    /// 一个场景文件都不用碰。表里**手工登记过的照样生效**（这里只在缺的时候补）。
    /// </summary>
    public static readonly string[][] 内置登记 = new string[][]
    {
        // { 功法表里的 普攻方法id, 组件类名 }
        new string[] { "basic_jiuba_01", "BasicJiuba01" },   // 八九玄功：近战普攻
        new string[] { "basic_jiuba_01", "武器挂载" },         // 八九玄功：把三尖两刃刀挂到右手
        new string[] { "basic_jiuba_01", "WeaponCarryAnim" }, // 八九玄功：移动/御风片段换成持械上半身版
        new string[] { "basic_taixu_sword_01", "BasicTaixuSword01" },
        new string[] { "basic_taixu_sword_01", "武器挂载" },
        new string[] { "basic_taixu_sword_01", "WeaponCarryAnim" },
        new string[] { "basic_lingxu_sword_01", "BasicLingxuSword01" },
        new string[] { "basic_lingxu_sword_01", "武器挂载" },
        new string[] { "basic_lingxu_sword_01", "WeaponCarryAnim" },
    };

    [Header("调试")]
    [Tooltip("装卸组件时打日志")]
    public bool 打印装卸日志 = true;

    // 当前由本组件负责装载出来的能力组件（只记录本组件装的，不碰手工挂的）
    readonly Dictionary<string, Component> 已装载 = new Dictionary<string, Component>();

    void Awake()
    {
        if (面板数据 == null)
        {
            var ui = GameObject.Find("CharacterUI");
            if (ui != null) 面板数据 = ui.GetComponent<UIPanelData>();
        }
        if (面板数据 == null) 面板数据 = FindObjectOfType<UIPanelData>();

        补齐内置登记();
    }

    /// <summary>把 <see cref="内置登记"/> 里缺的条目补进 <see cref="普攻方法表"/>（**只改内存，不写场景**）</summary>
    void 补齐内置登记()
    {
        if (被动神通表 == null) 被动神通表 = new List<被动神通绑定>();
        string[] 新被动 = { "ability_xieyan", "ability_huzhao_wuli", "ability_huzhao_teshu", "ability_huzhao_shuangchong" };
        foreach (string id in 新被动)
            if (!被动神通表.Exists(b => b != null && b.神通id == id))
                被动神通表.Add(new 被动神通绑定 { 神通id = id,
                    组件类名 = new[] { id == "ability_xieyan" ? "DevilEyeAbility" : "PassiveShieldAbilities" } });
        if (普攻方法表 == null) 普攻方法表 = new List<普攻方法绑定>();
        普攻方法表.RemoveAll(b=>b!=null && (b.方法id=="basic_sword_01" || b.组件类名=="QingshanSwordTreasure" || b.组件类名=="BasicSword01"));
        foreach (var 条 in 内置登记)
        {
            if (条 == null || 条.Length < 2) continue;
            string id = 条[0], 类 = 条[1];
            if (普攻方法表.Exists(b => b != null && b.方法id == id && b.组件类名 == 类)) continue;
            普攻方法表.Add(new 普攻方法绑定 { 方法id = id, 组件类名 = 类 });
            if (打印装卸日志)
                Debug.Log("[PlayerAbilityLoader] 内置登记补进内存：" + id + " → " + 类 + "（没写进场景）", this);
        }
    }

    void OnEnable()
    {
        if (面板数据 != null) 面板数据.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (面板数据 != null) 面板数据.Changed -= Refresh;
    }

    /// <summary>重算需要哪些能力组件，装缺的、卸多的</summary>
    public void Refresh()
    {
        if (面板数据 == null) return;
        面板数据.EnsureLists();

        var 需要 = new HashSet<string>();
        if (面板数据.四御已激活 && 面板数据.绝对护罩剩余次数 > 0) 需要.Add("PassiveShieldAbilities");

        // ---- 功法提供的普攻方法 ----
        var 功法 = 面板数据.当前功法;
        if (功法 != null && !string.IsNullOrEmpty(功法.普攻方法id))
        {
            foreach (var b in 普攻方法表)
                if (b != null && b.方法id == 功法.普攻方法id && !string.IsNullOrEmpty(b.组件类名))
                    需要.Add(b.组件类名);
        }

        // ---- 启用中的被动神通 ----
        if (面板数据.神通 != null)
        {
            foreach (var a in 面板数据.神通)
            {
                var 被动 = a as PassiveDivineAbility;
                if (被动 == null || !面板数据.已获得被动(被动) || !面板数据.IsPassiveEnabled(被动)) continue;

                foreach (var b in 被动神通表)
                {
                    if (b == null || b.神通id != 被动.神通id) continue;
                    foreach (var n in b.组件类名)
                        if (!string.IsNullOrEmpty(n)) 需要.Add(n);
                }
            }
        }

        // ---- 该开的开、该关的关 ----
        //
        // 【为什么是「启用/停用」而不是「新建/销毁」】
        // 以前是 需要 → AddComponent、不需要 → Destroy。问题是普攻组件身上有**很贵的
        // Inspector 配置**（`剑模型资源`、模型朝向补偿、悬浮偏移、绕行/瞄准参数…）。
        //   · 手工挂在玩家身上的那份，加进来时 `GetComponent(type) != null` 就跳过，
        //     于是它**永远不会被卸载** → 换功法之后新旧两套普攻同时生效 ✗
        //   · 真用 AddComponent 新建的话，所有配置都是默认值 → 剑直接废掉 ✗
        // 所以改成：**只在「本表里登记过的能力类名」范围内切换 enabled**，
        // 谁挂的、配了什么一概不动。
        foreach (var 名 in 表里登记过的能力类名())
        {
            bool 要 = 需要.Contains(名);
            var 现有 = 取组件(名);      // Behaviour：要能读写 enabled

            if (要)
            {
                if (现有 == null)
                {
                    var type = FindType(名);
                    if (type == null) { Debug.LogWarning("[PlayerAbilityLoader] 找不到组件类型 " + 名, this); continue; }
                    现有 = gameObject.AddComponent(type) as Behaviour;
                    已装载[名] = 现有;
                    if (打印装卸日志)
                        Debug.Log("[PlayerAbilityLoader] 新建并启用 " + 名
                                  + "（★ 玩家身上没有这个组件，新建出来的是**默认配置**，记得手动配一遍）", this);
                }
                else if (!现有.enabled)
                {
                    现有.enabled = true;
                    if (打印装卸日志) Debug.Log("[PlayerAbilityLoader] 启用 " + 名, this);
                }
            }
            else if (现有 != null && 现有.enabled)
            {
                // 御风停用前先落地，免得角色卡在半空
                var yf = 现有 as YufengFlight;
                if (yf != null) yf.强制落地();

                现有.enabled = false;          // ★ 停用而不是销毁：配置全留着
                if (打印装卸日志) Debug.Log("[PlayerAbilityLoader] 停用 " + 名, this);
            }
        }

        // 清掉已经被别处销毁掉的记录
        var 失效 = new List<string>();
        foreach (var kv in 已装载) if (kv.Value == null) 失效.Add(kv.Key);
        foreach (var k in 失效) 已装载.Remove(k);
    }

    /// <summary>配置表里登记过的**所有**能力组件类名（普攻方法表 + 被动神通表）</summary>
    HashSet<string> 表里登记过的能力类名()
    {
        var 名 = new HashSet<string>();
        foreach (var b in 普攻方法表)
            if (b != null && !string.IsNullOrEmpty(b.组件类名)) 名.Add(b.组件类名);
        foreach (var b in 被动神通表)
        {
            if (b == null || b.组件类名 == null) continue;
            foreach (var n in b.组件类名) if (!string.IsNullOrEmpty(n)) 名.Add(n);
        }
        return 名;
    }

    /// <summary>按类名找玩家身上已有的组件（找不到类型返回 null）</summary>
    Behaviour 取组件(string 类名)
    {
        var t = FindType(类名);
        if (t == null) return null;
        foreach (var component in GetComponents<Behaviour>())
            if (component.GetType() == t) return component;
        return null;
    }

    /// <summary>尝试销毁组件。有别的组件 RequireComponent 依赖它时 Unity 会拒绝，这里返回 false。</summary>
    static bool TryDestroy(Component c)
    {
        try
        {
            if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static Type FindType(string className)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(className);
            if (t != null) return t;
            // 也允许只写短名
            foreach (var t2 in asm.GetTypes())
                if (t2.Name == className) return t2;
        }
        return null;
    }

    /// <summary>当前已装载的能力组件类名（调试用）</summary>
    public List<string> 已装载列表
    {
        get
        {
            var l = new List<string>();
            foreach (var kv in 已装载) if (kv.Value != null) l.Add(kv.Key);
            return l;
        }
    }

    // ---- ASCII 别名 ----
    public void Reload() => Refresh();
    public List<string> LoadedTypes => 已装载列表;
}
