using UnityEngine;

/// <summary>
/// **一次性特效的摆放工具**（生成 → 旋转 → 对齐 → 等比缩放 → 销毁）。
///
/// 为什么抽出来：`BasicThunder01`（玄霄雷决普攻）、`BlinkSkillRunner`（雷动千闪）、
/// `QianJieLeiYu`（千劫雷狱）三处都要做同一套事，原来是各抄一份、各踩一遍同样的坑。
///
/// ## 这套工具有三个坑，每一个都真踩过
///
/// **① 旋转必须显式给。** 资源包的 prefab 常常"躺平"或"倒立"，
///    实测 `lightning-ray` 要绕 X 转 **−90**、`lightning-arc-black-ring` /
///    `lightning-whirling-arc-zone` 也要 **−90**。
///    默认不转 → 躺平；转 +90 → 倒过来。
///
/// **② 对齐要按【几何中心】，且要量【所有】粒子系统。**
///    · 不能只量"根级"部件：prefab 常层层嵌套
///      （`ground-dust → ground-pebbles → ground-flashes → lightning-ray`），
///      只量根级会把真正的部件漏掉。
///    · 不能跳过子发射器：同上，子发射器就是真部件。
///    · 用 `transform.position` 而不是 `renderer.bounds` —— 后者含粒子体积
///      （`startSize` 可达 8），会把只有 1~2 米的真实偏移糊掉。
///
/// **③ 缩放要先改 `scalingMode`。** 资源包的 prefab 常常是 `Shape` 模式，
///    那种模式下 transform 缩放**只拉开部件间距、不放大粒子**，
///    实测 `Shape` 下 localScale 1→4 粒子世界尺寸 3.56→3.62（没变），
///    改 `Hierarchy` 后才到 14.18。必须改成 `Hierarchy`。
/// </summary>
public static class 特效摆放
{
    /// <summary>
    /// 生成一个特效并摆好。
    /// </summary>
    /// <param name="特效路径">相对 Assets/resources、不带扩展名</param>
    /// <param name="位置">目标位置（会被对齐修正）</param>
    /// <param name="旋转欧拉">生成旋转。**必须按特效实测给**，零值 = 不转</param>
    /// <param name="缩放">等比缩放（1 = 原大小）</param>
    /// <param name="对齐到锚点">
    /// true = 把特效的几何中心挪到 `位置`（推荐，能自动消掉 prefab 自带的偏移）；
    /// false = 原样摆在 `位置`
    /// </param>
    /// <param name="存活秒">多少秒后销毁；≤0 = 不自动销毁</param>
    /// <param name="名">物件名</param>
    public static GameObject 生成(string 特效路径, Vector3 位置, Vector3 旋转欧拉,
                                   float 缩放 = 1f, bool 对齐到锚点 = true,
                                   float 存活秒 = 1.2f, string 名 = null)
    {
        if (string.IsNullOrEmpty(特效路径)) return null;

        var prefab = Resources.Load<GameObject>(特效路径);
        if (prefab == null)
        {
            Debug.LogWarning("[特效摆放] 找不到特效：" + 特效路径
                + "（路径要相对 Assets/resources、不带扩展名）");
            return null;
        }

        var go = Object.Instantiate(prefab, 位置, Quaternion.Euler(旋转欧拉));
        if (!string.IsNullOrEmpty(名)) go.name = 名;
        go.transform.localScale = Vector3.one * Mathf.Max(0.001f, 缩放);

        开等比缩放(go);
        if (对齐到锚点) 对齐到(go, 位置);
        if (存活秒 > 0f) Object.Destroy(go, Mathf.Max(0.05f, 存活秒));
        return go;
    }

    /// <summary>把粒子系统全改成 Hierarchy —— 否则 transform 缩放只拉开间距、不放大粒子</summary>
    public static void 开等比缩放(GameObject go)
    {
        if (go == null) return;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null) continue;
            var m = ps.main;
            if (m.scalingMode != ParticleSystemScalingMode.Hierarchy)
                m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    /// <summary>
    /// 把特效的**几何中心**挪到锚点。
    /// 量所有粒子系统的 `transform.position` 求包围盒中心（理由见类注释 ②）。
    /// </summary>
    public static void 对齐到(GameObject go, Vector3 锚点)
    {
        if (go == null) return;
        if (!量部件范围(go, out Bounds 范围)) return;
        go.transform.position += 锚点 - 范围.center;
    }

    /// <summary>只对齐水平（XZ），竖直保持生成时的值</summary>
    public static void 只对齐水平(GameObject go, Vector3 锚点)
    {
        if (go == null) return;
        if (!量部件范围(go, out Bounds 范围)) return;
        var t = go.transform;
        t.position = new Vector3(
            t.position.x + (锚点.x - 范围.center.x),
            t.position.y,
            t.position.z + (锚点.z - 范围.center.z));
    }

    // ============================================================ 特效时长

    /// <summary>
    /// 量一个特效 prefab 的**自然总时长**（秒）—— 所有粒子系统里
    /// `startDelay + duration + startLifetime(最大)` 的最大值（各自再除以 simulationSpeed）。
    ///
    /// 为什么要它：**写死一个偏短的存活秒数就是把特效硬切掉**。
    /// 实测 `frost-shock` 的自然总时长是 **9.1 秒**（`spikes-*` 的粒子寿命 0~9 秒随机 → 慢慢融化消失），
    /// 早先写死 1.2 秒时的表现就是「冰刺忽然消失」（用户 2026-09-29 报的）。
    ///
    /// ⚠️ `loop = true` 的系统本身不会结束，这里只按「一个循环 + 一次寿命」估，仅作下限。
    /// </summary>
    public static float 量特效总时长(GameObject prefab, float 兜底 = 2f)
    {
        if (prefab == null) return 兜底;
        float 最 = 0f;
        foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null) continue;
            var m = ps.main;
            float 速 = Mathf.Max(0.05f, m.simulationSpeed);
            float 单 = (m.startDelay.constantMax + m.duration + m.startLifetime.constantMax) / 速;
            if (单 > 最) 最 = 单;
        }
        return 最 > 0.01f ? 最 : 兜底;
    }

    // ============================================================ 按【子节点】对齐

    /// <summary>
    /// 按名字找一个子节点（**含未激活的**）。
    ///
    /// 为什么需要：有些资源包的 prefab **根节点不在几何中心上**
    /// （实测 `frost-shock` 的根自身带 (−2.75, 0, 3.21) 的偏移，真正的中心是子节点 `spikes-second`），
    /// 这时候「按粒子几何中心对齐」和「按根节点摆」都会歪。
    /// </summary>
    /// <param name="名字">要匹配的子节点名；精确名优先，其次包含匹配</param>
    public static Transform 找子节点(GameObject go, string 名字)
    {
        if (go == null || string.IsNullOrEmpty(名字)) return null;
        Transform 包含 = null;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == go.transform) continue;
            if (t.name == 名字) return t;                      // 精确
            if (包含 == null && t.name.ToLower().Contains(名字.ToLower())) 包含 = t;   // 退一步
        }
        return 包含;
    }

    /// <summary>
    /// 把**指定的子节点**的世界位置对齐到锚点 —— prefab 根不在中心时用这个。
    ///
    /// <paramref name="只水平"/> = true 时只对齐 XZ（竖直留给调用方自己控制）。
    /// 找不到该子节点返回 false（调用方可以退回 <see cref="只对齐水平"/>）。
    /// </summary>
    public static bool 对齐子节点到(GameObject go, string 子节点名, Vector3 锚点, bool 只水平 = false)
    {
        var t = 找子节点(go, 子节点名);
        if (t == null) return false;
        var 差 = 锚点 - t.position;
        if (只水平) 差.y = 0f;
        go.transform.position += 差;
        return true;
    }

    /// <summary>量所有粒子系统的世界位置包围盒（不跳子发射器、不用 renderer.bounds）</summary>
    public static bool 量部件范围(GameObject go, out Bounds 范围)
    {
        范围 = new Bounds();
        if (go == null) return false;

        bool 有 = false;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps == null) continue;
            Vector3 p = ps.transform.position;
            if (!有) { 范围 = new Bounds(p, Vector3.zero); 有 = true; }
            else 范围.Encapsulate(p);
        }
        return 有;
    }

    /// <summary>
    /// 量一个物件（敌人 / 目标）的高度：优先碰撞体包围盒，其次渲染体包围盒。
    /// 取不到返回 <paramref name="兜底"/>。
    /// </summary>
    public static float 量高度(Transform 目标, float 兜底 = 1.8f)
    {
        if (目标 == null) return 兜底;

        bool 有 = false; Bounds 总 = new Bounds();
        foreach (var c in 目标.GetComponentsInChildren<Collider>(true))
        {
            if (c == null || !c.enabled) continue;
            if (!有) { 总 = c.bounds; 有 = true; } else 总.Encapsulate(c.bounds);
        }
        if (有 && 总.size.y > 0.05f) return 总.size.y;

        有 = false;
        foreach (var r in 目标.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
            if (!有) { 总 = r.bounds; 有 = true; } else 总.Encapsulate(r.bounds);
        }
        return 有 && 总.size.y > 0.05f ? 总.size.y : 兜底;
    }

    // ============================================================ 拉伸链

    /// <summary>
    /// 生成一道**从「起点」拉到「终点」**的链状特效（闪电链 / 雷云的落雷都用它）。
    ///
    /// 做法：先让特效的"长度轴"指向终点，再把该轴的 localScale 设成
    /// 「两点距离 ÷ 基准长度」→ 特效正好横跨两端；另外两根轴按粗细缩放。
    ///
    /// ⚠️ **特效路径为空时直接返回 null（不打警告）** —— 闪电链/落雷的特效槽
    /// 目前**空置待补**（用户 2026-09-28 决定，我自制的 `雷链` 已删）。
    /// 补特效的要求与标定方法见 `docs/guides/闪电链特效.md`。
    ///
    /// ⚠️ **长度轴不是固定的**，必须按 prefab 自动探测（各粒子 `shape.scale` 最长的那个轴），
    /// 探不出来才用 <paramref name="兜底拉伸轴"/>。
    ///
    /// ⚠️ 要拉伸的特效 prefab 必须满足两条：
    /// 1. `scalingMode = Hierarchy` —— `Shape` 会把 localScale 作用**两次**
    ///    （实测 localScale 3.42 时铺开 11.7m 而不是 3.42m，链会糊成一片）；
    /// 2. `renderMode = Billboard` —— `Stretch` 在"速度≈0"的粒子上会退化成**看不见**。
    /// </summary>
    /// <param name="长度补偿">
    /// 特效两端**固定外溢**多少米（粒子自身的长度，不随拉伸变化）。
    /// 多数填 0；接了新特效要用探针（`GetParticles`，**别用 `renderer.bounds`**）重新标定。
    /// </param>
    /// <returns>生成出来的物件（null = 失败）</returns>
    public static GameObject 生成拉伸链(string 特效路径, Vector3 起点, Vector3 终点,
                                        float 粗细 = 1f,
                                        float 基准长度 = 2f,
                                        float 最小长度 = 1f,
                                        float 存活秒 = 0.35f,
                                        Vector3 兜底拉伸轴 = default,
                                        float 长度补偿 = 0f,
                                        string 名 = null)
    {
        if (string.IsNullOrEmpty(特效路径)) return null;

        var prefab = Resources.Load<GameObject>(特效路径);
        if (prefab == null)
        {
            Debug.LogWarning("[特效摆放] 找不到拉伸链特效：" + 特效路径
                + "（路径要相对 Assets/resources、不带扩展名）");
            return null;
        }

        Vector3 向 = 终点 - 起点;
        if (向.sqrMagnitude < 0.0001f) 向 = Vector3.forward;
        // ★ 用**三维距离**：雷云的雷是从天上斜着劈下来的，
        //   只算水平距离会让链明显短一截（两端都够不着）。
        float 距离 = 向.magnitude;
        var 中点 = (起点 + 终点) * 0.5f;

        // 让本地 Z 轴指向终点
        var 旋 = Quaternion.LookRotation(向.normalized, Vector3.up);

        var go = Object.Instantiate(prefab, 中点, 旋);
        if (!string.IsNullOrEmpty(名)) go.name = 名;

        var 长度轴 = 探测长度轴(prefab, 兜底拉伸轴);
        // 把"长度轴指向终点"这件事做掉：旋转后再绕 Y 补一个角度
        go.transform.rotation = 旋 * 长度轴旋转修正(长度轴);

        float 拉伸 = Mathf.Max(最小长度, 距离 - Mathf.Max(0f, 长度补偿)) / Mathf.Max(0.05f, 基准长度);
        go.transform.localScale = 按轴分配(长度轴, Mathf.Max(0.05f, 粗细), 拉伸);

        if (存活秒 > 0f) Object.Destroy(go, Mathf.Max(0.05f, 存活秒));
        return go;
    }

    /// <summary>本地哪根轴是"长度轴"：看所有粒子 shape 的 scale，取最大的那根</summary>
    public static Vector3 探测长度轴(GameObject prefab, Vector3 兜底 = default)
    {
        var 最 = Vector3.zero;
        if (prefab != null)
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var s = ps.shape;
                if (!s.enabled) continue;
                var sc = s.scale;
                if (sc.x > 最.x) 最.x = sc.x;
                if (sc.y > 最.y) 最.y = sc.y;
                if (sc.z > 最.z) 最.z = sc.z;
            }

        if (最.sqrMagnitude < 0.0001f) return 归一(兜底);
        if (最.x >= 最.y && 最.x >= 最.z) return Vector3.right;
        if (最.y >= 最.x && 最.y >= 最.z) return Vector3.up;
        return Vector3.forward;
    }

    /// <summary>把一个向量归到最近的正交轴上</summary>
    public static Vector3 归一(Vector3 v)
    {
        if (v.sqrMagnitude < 0.0001f) return Vector3.forward;
        var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (a.x >= a.y && a.x >= a.z) return Vector3.right;
        if (a.y >= a.x && a.y >= a.z) return Vector3.up;
        return Vector3.forward;
    }

    /// <summary>
    /// `LookRotation` 让**本地 Z** 指向终点。如果长度轴是 X，就要再绕 Y 转 90°，
    /// 把它转成 Z；是 Y 也同理。这样"长度轴"才真的指向终点。
    /// </summary>
    public static Quaternion 长度轴旋转修正(Vector3 长度轴)
    {
        if (长度轴 == Vector3.right) return Quaternion.Euler(0f, 90f, 0f);
        if (长度轴 == Vector3.up) return Quaternion.Euler(-90f, 0f, 0f);
        return Quaternion.identity;      // forward：本来就是 Z
    }

    /// <summary>把"粗细"和"拉伸"按长度轴分配到三根轴上</summary>
    public static Vector3 按轴分配(Vector3 长度轴, float 粗细, float 拉伸)
    {
        if (长度轴 == Vector3.right) return new Vector3(拉伸, 粗细, 粗细);
        if (长度轴 == Vector3.up) return new Vector3(粗细, 拉伸, 粗细);
        return new Vector3(粗细, 粗细, 拉伸);
    }
}
