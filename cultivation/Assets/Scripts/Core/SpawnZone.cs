using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **刷怪区** = 「一片留给刷怪的空地」的标记 + **按组刷怪的运行时**。
///
/// ## 一、标记（原有职责，2026-09-27 起）
///
/// 在 Scene 视图画一个贴地圈 + 十字，标出"这片空地是留给刷怪点的"。
/// <c>SectWildernessBuilder</c> 会在 <c>野外环境/刷怪区/</c> 下按固定坐标生成几个空物体挂本组件，
/// 地形那边已经保证**这片区域不长树**（见 builder 的 <c>可放()</c> / <c>空地权()</c>）。
///
/// ## 二、刷怪（2026-09-30 新增，镇妖塔用）
///
/// 按 <see cref="SpawnGroup"/>「组团」刷怪，**全清后过 <see cref="重生延迟"/> 秒再来一组**。
///
/// 用户的机制要求：
/// · 每次怪物都以表格中的**刷怪组类别组团出现**
/// · 镇妖塔每层生成一个组团
/// · 所有怪物清理完毕 **10 秒后**会再次刷出这一组
///
/// 【向后兼容】<see cref="自动开始"/> 默认 **false** ——
/// 野外那 7 片刷怪区只是标记，不会因为本组件升级而突然开始刷怪。
///
/// ## 三、怪怎么变强
///
/// 生成时调 <c>NpcInstance.应用等级补正(等级, 表)</c>：
/// NPC表 里只填 1 级基准值，实际强度由等级补正表算。
/// 所以同一个组的怪在第 1 层和第 900 层是**同一份定义、不同强度**，不用为每层建怪。
///
/// ## 四、挂载要点
///
/// · <see cref="刷怪点"/> 留空 = 自动收集 <see cref="刷怪父节点"/>（留空则全场景）下的 <see cref="SpawnPoint"/>
/// · 一个刷怪点都没有时**退化成以本物体为中心散开**，并警告一次（不是错误）
/// </summary>
[DisallowMultipleComponent]
public class SpawnZone : MonoBehaviour
{
    // ============================================================ 一、标记

    [Header("标记（编辑器提示圈）")]
    [Tooltip("区域半径（米）。地形生成时按这个尺寸留白")]
    public float 半径 = 26f;

    [Tooltip("线框颜色")]
    public Color 颜色 = new Color(1f, 0.35f, 0.25f, 1f);

    // ============================================================ 二、刷怪

    [Header("刷什么")]
    [Tooltip("刷怪组 id（刷怪组表的 组id）。由塔控按层写入；也可以手填来单独测")]
    public string 刷怪组 = "";

    [Tooltip("直接指定刷怪组资产。留空则按 刷怪组 id 从刷怪组库查")]
    public SpawnGroup 刷怪组资产;

    [Header("等级")]
    [Tooltip("刷出来的怪的**整数等级**（1~100）。\n" +
             "报给玩家、也算进「击杀掉修炼次数」的等级差。")]
    [Min(1)] public float 怪物等级 = 1f;

    [Tooltip("**总强度倍率**：直接乘在怪的基础数值上（气血/攻击/防御/灵力/回复）。\n" +
             "塔里由塔控按层给 —— 它同时包含了「大境界门槛」和「层内递进」\n" +
             "（见 TowerFloorTable.取怪强度倍率）。1 = 不额外加强（普通场景用）。")]
    [Min(0.01f)] public float 层内倍率 = 1f;

    [Tooltip("等级补正表。留空自动用 Resources 里那张")]
    public NpcLevelScale 补正表;

    [Tooltip("勾上 = 生成后把等级打在怪名字上（调试用）")]
    public bool 名字带等级 = false;

    [Header("数量")]
    [Tooltip("整组的数量倍率（1 = 按刷怪组原样）")]
    [Min(0f)] public float 数量倍率 = 1f;

    [Header("刷怪点")]
    [Tooltip("刷怪点列表。留空则在 刷怪父节点 下自动收集")]
    public List<SpawnPoint> 刷怪点 = new List<SpawnPoint>();

    [Tooltip("自动收集刷怪点的父节点。留空 = 整个场景")]
    public Transform 刷怪父节点;

    [Tooltip("一个刷怪点都没有时的散开半径")]
    public float 无点散开半径 = 6f;

    [Header("节奏")]
    [Tooltip("**全部清空后**过多少秒刷下一组（用户定：10 秒）")]
    [Min(0f)] public float 重生延迟 = 10f;

    [Tooltip("进场景后先等多少秒才刷第一组")]
    [Min(0f)] public float 首刷延迟 = 0f;

    [Tooltip("刷出来的怪最多活多久（秒）。0 = 不管")]
    [Min(0f)] public float 存活上限 = 0f;

    [Header("自动开始")]
    [Tooltip("勾上 = Start 时自己刷第一组。\n" +
             "**默认关** —— 镇妖塔由塔控按层开；野外那 7 片刷怪区只是标记，不该自己刷怪。")]
    public bool 自动开始 = false;

    // ============================================================ 状态

    readonly List<NpcInstance> 活着的 = new List<NpcInstance>();
    readonly List<GameObject> 本波 = new List<GameObject>();
    Coroutine 循环;
    bool 已警告无点;

    /// <summary>正在刷怪 / 等重刷</summary>
    public bool 运行中 { get; private set; }

    /// <summary>本波还剩几只活的</summary>
    public int 存活数 { get { 清掉失效(); return 活着的.Count; } }

    /// <summary>本波是不是已经清空了</summary>
    public bool 已清空 => 运行中 && 存活数 == 0;

    /// <summary>第几波（从 1 开始）</summary>
    public int 波次 { get; private set; }

    /// <summary>清空时触发（参数：本区）</summary>
    public event System.Action<SpawnZone> 清空;
    /// <summary>刷出新一组时触发（参数：本区、怪的列表）</summary>
    public event System.Action<SpawnZone, List<NpcInstance>> 刷出了;

    // ============================================================ 生命周期

    void Start()
    {
        if (刷怪点 == null || 刷怪点.Count == 0) 刷怪点 = SpawnPoint.收集(刷怪父节点);
        if (自动开始) 开始刷怪();
    }

    /// <summary>开始刷怪循环（塔控进层时调）</summary>
    public void 开始刷怪()
    {
        if (运行中) return;
        运行中 = true;
        波次 = 0;
        循环 = StartCoroutine(刷怪循环());
    }

    /// <summary>停止刷怪（可选是否清掉场上的怪）</summary>
    public void 停止刷怪(bool 清掉现有 = true)
    {
        运行中 = false;
        if (循环 != null) { StopCoroutine(循环); 循环 = null; }
        if (清掉现有) 清掉本波();
    }

    /// <summary>把本区刷出来的怪全部销毁</summary>
    public void 清掉本波()
    {
        for (int i = 0; i < 本波.Count; i++)
            if (本波[i] != null) Destroy(本波[i]);
        本波.Clear();
        活着的.Clear();
    }

    /// <summary>立刻重刷一组（不等重生延迟）—— 塔控换层 / 玩家复活时用</summary>
    public void 立刻重刷()
    {
        if (!运行中) { 开始刷怪(); return; }
        if (循环 != null) StopCoroutine(循环);
        清掉本波();
        循环 = StartCoroutine(刷怪循环());
    }

    /// <summary>换一个刷怪组（塔控换层时用）</summary>
    /// <param name="组id">刷怪组 id</param>
    /// <param name="等级">**整数**等级（显示 + 击杀经验用）</param>
    /// <param name="数量倍数">数量倍率</param>
    /// <param name="强度倍率">总强度倍率（大境界门槛 × 层内递进；1 = 不加）</param>
    public void 设为刷怪组(string 组id, float 等级, float 数量倍数 = 1f, float 强度倍率 = 1f)
    {
        刷怪组 = 组id;
        刷怪组资产 = null;               // 强制按 id 重查
        怪物等级 = Mathf.Max(1f, 等级);
        数量倍率 = Mathf.Max(0f, 数量倍数);
        层内倍率 = Mathf.Max(0.01f, 强度倍率);
    }

    // ============================================================ 循环

    IEnumerator 刷怪循环()
    {
        if (首刷延迟 > 0f) yield return new WaitForSeconds(首刷延迟);

        while (运行中)
        {
            刷一波();
            if (!运行中) yield break;                 // 空组会把自己关掉

            while (运行中 && 存活数 > 0) yield return null;
            if (!运行中) yield break;

            清空?.Invoke(this);
            Debug.Log("[刷怪区] " + name + " 第 " + 波次 + " 波已清空，"
                      + 重生延迟.ToString("0.#") + " 秒后重刷", this);

            if (重生延迟 > 0f) yield return new WaitForSeconds(重生延迟);
            if (!运行中) yield break;
        }
    }

    /// <summary>刷一波（生成 + 补正等级）</summary>
    public void 刷一波()
    {
        var 组 = 解析刷怪组();
        if (组 == null || 组.是空的)
        {
            Debug.LogWarning("[刷怪区] " + name + " 没有可用的刷怪组（刷怪组 id = '"
                             + 刷怪组 + "'）。先跑「修仙/从配置表生成资产」，"
                             + "并确认刷怪组表里有这个组", this);
            运行中 = false;                            // 免得每帧空转刷屏
            return;
        }

        波次++;
        var 模板 = 组.展开();

        // 数量倍率：至少 1 只，免得倍率调到 0 之后变成"永远清空"的死循环
        int 目标数 = Mathf.Max(1, Mathf.RoundToInt(模板.Count * Mathf.Max(0f, 数量倍率)));
        var ids = new List<string>(目标数);
        for (int i = 0; i < 目标数; i++) ids.Add(模板[i % 模板.Count]);

        var 点 = 确保刷怪点();
        var 新怪 = new List<NpcInstance>();

        for (int i = 0; i < ids.Count; i++)
        {
            var prefab = NpcPrefabs.按id取(ids[i]);
            if (prefab == null)
            {
                Debug.LogWarning("[刷怪区] 找不到 NPC 预制体，id = " + ids[i]
                                 + "（resources/NPC 下没有它的 prefab，或 NPC表 里「模型资源路径」为空）", this);
                continue;
            }

            Vector3 位置;
            Quaternion 朝向;
            if (点.Count > 0)
            {
                var p = 点[i % 点.Count];
                位置 = p.出生位置;
                朝向 = p.出生朝向;
            }
            else
            {
                // 退化：绕本物体一圈散开
                float ang = i * Mathf.PI * 2f / Mathf.Max(1, ids.Count);
                位置 = transform.position
                       + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 无点散开半径;
                朝向 = transform.rotation;
            }

            var go = Instantiate(prefab, 位置, 朝向);
            go.name = prefab.name + "_W" + 波次 + "_" + i;

            var inst = go.GetComponent<NpcInstance>();
            if (inst != null)
            {
                // ★ 只按**整数等级**补正（±0，因为塔里的怪是同级的），
                //   然后乘上塔给的**总强度倍率**（大境界门槛 × 层内递进）。
                //   倍率是"相对第 1 层的总倍数"，所以基准用**1 级**属性更直观 ——
                //   见下面注释。
                inst.应用等级补正(1f, 补正表, Mathf.RoundToInt(怪物等级));
                inst.叠加数值倍率(层内倍率);
                if (名字带等级) go.name = prefab.name + "(Lv" + 怪物等级.ToString("0") + "×" + 层内倍率.ToString("0.#") + ")_" + i;

                inst.Died += 处理死亡;
                活着的.Add(inst);
                新怪.Add(inst);

                if (存活上限 > 0f) StartCoroutine(超时回收(go));
            }
            else
            {
                Debug.LogWarning("[刷怪区] " + prefab.name + " 上没有 NpcInstance，刷出来不会被打", this);
            }

            本波.Add(go);
        }

        Debug.Log("[刷怪区] " + name + " 第 " + 波次 + " 波：等级 " + 怪物等级
                  + "，共 " + 新怪.Count + " 只（" + 组.摘要() + "）", this);
        刷出了?.Invoke(this, 新怪);
    }

    IEnumerator 超时回收(GameObject go)
    {
        yield return new WaitForSeconds(存活上限);
        if (go != null) Destroy(go);
    }

    void 处理死亡(NpcInstance 谁)
    {
        if (谁 != null) { 谁.Died -= 处理死亡; 活着的.Remove(谁); 清掉失效(); }
    }

    void 清掉失效()
    {
        for (int i = 活着的.Count - 1; i >= 0; i--)
            if (活着的[i] == null || 活着的[i].IsDead) 活着的.RemoveAt(i);
    }

    // ============================================================ 工具

    List<SpawnPoint> 确保刷怪点()
    {
        if (刷怪点 == null) 刷怪点 = new List<SpawnPoint>();
        if (刷怪点.Count == 0) 刷怪点 = SpawnPoint.收集(刷怪父节点);
        for (int i = 刷怪点.Count - 1; i >= 0; i--) if (刷怪点[i] == null) 刷怪点.RemoveAt(i);

        if (刷怪点.Count == 0 && !已警告无点)
        {
            已警告无点 = true;
            Debug.LogWarning("[刷怪区] " + name + " 没找到任何 SpawnPoint，"
                             + "会以本物体为中心 " + 无点散开半径 + " 米散开刷怪。"
                             + "想在指定位置刷就摆几个挂 SpawnPoint 的空物件", this);
        }
        return 刷怪点;
    }

    SpawnGroup 解析刷怪组()
    {
        if (刷怪组资产 != null) return 刷怪组资产;
        if (string.IsNullOrEmpty(刷怪组)) return null;
        刷怪组资产 = SpawnGroup库.按id(刷怪组);
        return 刷怪组资产;
    }

    // ============================================================ 编辑期绘制

    void OnDrawGizmos()
    {
        var 旧 = Gizmos.color;
        Gizmos.color = 颜色;

        // 贴地画一圈（每段都按地形高度取点，山坡上也不会悬空/埋进地里）
        const int 段 = 48;
        Vector3 上一点 = Vector3.zero;
        for (int i = 0; i <= 段; i++)
        {
            float a = i / (float)段 * Mathf.PI * 2f;
            var w = new Vector3(transform.position.x + Mathf.Cos(a) * 半径, 0f,
                                transform.position.z + Mathf.Sin(a) * 半径);
            w.y = 取地面(w);
            if (i > 0) Gizmos.DrawLine(上一点, w);
            上一点 = w;
        }

        // 中间一个十字，好认中心点
        var c = new Vector3(transform.position.x, 取地面(transform.position), transform.position.z);
        float r = Mathf.Min(4f, 半径 * 0.25f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.PI * 0.5f;
            Gizmos.DrawLine(c, c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
        }
        Gizmos.DrawLine(c, c + Vector3.up * 3f);

        Gizmos.color = 旧;
    }

    void OnDrawGizmosSelected()
    {
        var 点 = 刷怪点 != null && 刷怪点.Count > 0 ? 刷怪点 : SpawnPoint.收集(刷怪父节点);
        if (点 == null || 点.Count == 0) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < 点.Count; i++)
            if (点[i] != null) Gizmos.DrawLine(transform.position, 点[i].出生位置);
    }

    static float 取地面(Vector3 w)
    {
        if (Physics.Raycast(new Vector3(w.x, w.y + 60f, w.z), Vector3.down, out var h, 200f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return h.point.y + 0.15f;
        return w.y;
    }

    // ---- ASCII 别名 ----
    public float Radius { get => 半径; set => 半径 = value; }
    public int AliveCount => 存活数;
    public bool IsRunning => 运行中;
}
