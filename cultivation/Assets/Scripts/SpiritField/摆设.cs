using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **摆设的管理者** —— 玩家在洞府里摆下来的"非灵田"物件（目前是**练功木桩**）。
///
/// 需求原文（用户 2026-10-01）：
/// > 「让**木桩**也像灵田一样是**存在背包里、可以放置**的吧，
/// >   注意这些东西都是**要能够存档**的哦。」
///
/// ## 它和 `灵田` 的分工
///
/// | | 管什么 |
/// |---|---|
/// | <see cref="灵田"/> | 灵田地块（有品阶 / 作物 / 生长，一整套状态机） |
/// | <see cref="摆设"/> | **其余**摆下来的东西：只记 `类型id + 位置 + 朝向档` |
/// | <see cref="摆放校验"/> | 两边**共用**的"这个位置能不能放"（含跨种类互斥） |
///
/// 【为什么不塞进 `灵田`】木桩没有品阶、没有作物、不生长 —— 硬塞进去会让
/// `灵田地块状态` 里一半字段对木桩毫无意义，以后每加一种摆设就更脏一层。
///
/// ## 木桩为什么"能打"
///
/// 这里**不实现任何战斗逻辑** —— 它只负责把那颗 prefab `Instantiate` 出来。
/// 木桩的**血条 / 受击 / 打空自动满血**全部来自 prefab 自带的 `NpcInstance`
/// 加上 `NPC表.csv` 里 `monster_wooden_dummy` 那条定义
/// （`类型=中立`、`气血=10000`、`死亡后立即重生=是`、`重生延迟=0`）。
/// 也就是说：**摆放系统只解决"它在哪里"，"它是什么"仍由 NPC 表决定**。
///
/// ## 数据与视图分离
///
/// 和灵田同一个道理：**状态**在 `DontDestroyOnLoad` 单例里跨场景活着，
/// **视图**每次进场景重建，且只有在 `摆放校验.允许摆放的场景` 里才建
/// （否则摆设会跟着玩家跑到宗门去，见踩坑 B57）。
/// </summary>
[DisallowMultipleComponent]
public class 摆设 : MonoBehaviour
{
    // ============================================================ 单例

    static 摆设 实例;
    public static 摆设 取()
    {
        if (实例 != null) return 实例;
        实例 = FindObjectOfType<摆设>();
        if (实例 != null) return 实例;
        var go = new GameObject("摆设");
        DontDestroyOnLoad(go);
        实例 = go.AddComponent<摆设>();
        return 实例;
    }

    // ============================================================ 状态

    [Header("状态（只读，进存档）")]
    [SerializeField] List<摆设状态> 全部 = new List<摆设状态>();

    /// <summary>所有摆设（只读）。下标 = 摆设编号</summary>
    public IReadOnlyList<摆设状态> 所有摆设 => 全部;

    public int 数量 => 全部.Count;

    public 摆设状态 状态(int i) => (i >= 0 && i < 全部.Count) ? 全部[i] : null;

    /// <summary>任何一件摆设变了（摆下 / 挪动 / 读档）</summary>
    public event System.Action 变化;

    void 通知变化() => 变化?.Invoke();

    // ============================================================ 生命周期

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(this); return; }
        实例 = this;
        if (全部 == null) 全部 = new List<摆设状态>();
    }

    void Start()
    {
        重建视图();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= 处理场景加载;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += 处理场景加载;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= 处理场景加载;
    }

    void 处理场景加载(UnityEngine.SceneManagement.Scene 场景, UnityEngine.SceneManagement.LoadSceneMode 模式)
        => 重建视图();

    // ============================================================ 摆放

    /// <summary>这件东西能不能放在这里（<paramref name="忽略"/> = 挪动自己时跳过自己）</summary>
    public bool 位置可用(string 类型id, Vector3 位置, int 朝向档, out string 原因, int 忽略 = -1)
    {
        float 地面高;
        return 摆放校验.位置可用(摆放物库.取(类型id), 位置, 朝向档, out 原因, out 地面高,
                                 -1, 忽略);
    }

    /// <summary>摆下一件新摆设（**道具由调用方扣**）。返回编号；失败返回 -1</summary>
    public int 放置(string 类型id, Vector3 位置, int 朝向档)
    {
        string 原因;
        if (!位置可用(类型id, 位置, 朝向档, out 原因))
        {
            Debug.LogWarning("[摆设] 摆不了：" + 原因);
            return -1;
        }

        全部.Add(new 摆设状态 { 类型id = 类型id, 位置 = 位置, 朝向档 = 朝向档 });
        var d = 摆放物库.取(类型id);
        Debug.Log("[摆设] 摆下第 " + 全部.Count + " 件「" + (d != null ? d.名 : 类型id) + "」@ "
                  + 位置.ToString("F2") + "  朝向 " + 灵田规格.档转角度(朝向档) + "°");
        重建视图();
        通知变化();
        return 全部.Count - 1;
    }

    /// <summary>把第 i 件摆设挪到新位置（和灵田一样：**不花道具、也不退**）</summary>
    public bool 挪动(int i, Vector3 位置, int 朝向档)
    {
        var s = 状态(i);
        if (s == null) return false;

        string 原因;
        if (!位置可用(s.类型id, 位置, 朝向档, out 原因, i))
        {
            Debug.LogWarning("[摆设] 挪不了：" + 原因);
            return false;
        }

        s.位置 = 位置;
        s.朝向档 = 朝向档;
        Debug.Log("[摆设] 第 " + (i + 1) + " 件「" + s.名 + "」挪到 " + 位置.ToString("F2"));
        重建视图();
        通知变化();
        return true;
    }

    // ============================================================ 场景视图

    GameObject 根;

    /// <summary>把摆设视图**在本场景重建一遍**（进场景 / 摆完 / 挪完 / 读档后都调它）</summary>
    public void 重建视图()
    {
        if (根 != null) Object.Destroy(根);
        根 = null;

        if (!摆放校验.当前场景可摆放) return;      // 宗门/村庄/塔里**一件都不建**

        根 = new GameObject("摆设根");
        for (int i = 0; i < 全部.Count; i++)
        {
            var s = 全部[i];
            if (s == null) continue;

            var go = new GameObject("摆设" + (i + 1) + "_" + s.类型id);
            go.transform.SetParent(根.transform, false);
            go.transform.position = s.位置;
            go.transform.rotation = Quaternion.Euler(0f, s.朝向角度, 0f);

            var 件 = go.AddComponent<摆设物件>();
            件.编号 = i;
        }
    }

    // ============================================================ 存档
    //
    // ⚠️ `摆设状态` 本身就是存档类型（见 `SaveData.摆设`），字段全是
    //    string / float / int / Vector3，`JsonUtility` 直接吃，不用字符串打包。

    public List<摆设状态> 导出()
    {
        var 出 = new List<摆设状态>();
        foreach (var s in 全部) if (s != null) 出.Add(s);
        return 出;
    }

    public void 导入(List<摆设状态> 存档)
    {
        全部 = new List<摆设状态>();
        if (存档 != null)
            foreach (var s in 存档)
            {
                if (s == null) continue;
                // 类型没了（改过 摆放物库）→ 丢掉这一件，免得留一个永远长不出来的僵尸
                if (摆放物库.取(s.类型id) == null)
                {
                    Debug.LogWarning("[摆设] 存档里的类型「" + s.类型id + "」已经不存在了，丢弃这一件");
                    continue;
                }
                s.朝向档 = Mathf.Clamp(s.朝向档, 0, 灵田规格.朝向档数 - 1);
                全部.Add(s);
            }

        重建视图();
        通知变化();
    }
}

/// <summary>
/// **一件摆设的场景视图**：把 `摆放物定义.模型路径` 那个 prefab 实例化出来。
///
/// 它**故意什么都不做** —— 木桩的血条、受击、打空自动满血全在 prefab 自带的
/// `NpcInstance` 上。这里只负责"把 prefab 放到该在的位置"。
/// </summary>
[DisallowMultipleComponent]
public class 摆设物件 : MonoBehaviour
{
    [Tooltip("是第几件摆设（对应 摆设.状态(编号)）")]
    public int 编号;

    public 摆设状态 状态
    {
        get
        {
            var 摆 = 摆设.取();
            return 摆 != null ? 摆.状态(编号) : null;
        }
    }

    GameObject 实例;

    void Start()
    {
        var s = 状态;
        var d = s != null ? s.定义 : null;
        if (d == null) return;

        if (string.IsNullOrEmpty(d.模型路径)) return;    // 留空 = 外观由别的代码生成

        var 预 = Resources.Load<GameObject>(d.模型路径);
        if (预 == null)
        {
            Debug.LogWarning("[摆设] 找不到模型「" + d.模型路径 + "」——"
                             + "确认它在某个 Resources 目录下（例：Assets/resources/摆设/练功木桩.prefab）");
            return;
        }

        实例 = Instantiate(预, transform);
        实例.name = d.名;
        实例.transform.localPosition = Vector3.zero;
        实例.transform.localRotation = Quaternion.identity;
        // 缩放也归零成 1：prefab 自己带的缩放要保留的话，这里就不该动 ——
        // 木桩 prefab 的缩放是它自己的事，别覆盖。
        实例.transform.localScale = 预.transform.localScale;
    }
}
