using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 找出一个 NPC 的【身躯】渲染体，并据此算包围盒 —— **把武器和飘带/特效排除在外**。
///
/// 为什么不能用「体积最大的那个」（我第一版就是这么写的，结果全错）：
///
/// | 渲染体（白骨娘娘） | 世界包围盒 minY | 体积 |
/// |---|---|---|
/// | `BaiGuNiangNiang_01`（身体） | **0** | 4.76 |
/// | `BaiGuNiangNiang_01_wuqi`（武器） | 1.069 | 0.37 |
/// | `BaiGuNiangNiang_02`（飘带） | **0.945** | **8.63 ← 最大** |
///
/// 体积最大的是**飘带**，它最低点在 0.945 米高 —— 拿它当身躯去贴地，
/// 就会把整个 NPC 往下压 0.945 米，表现就是「陷进地板只露一个头」。
///
/// **正确的识别办法（按命名，用户给的规律）**：
///   1. 先按关键字排掉武器：名字里有 `wuqi` / `weapon` / `武器` 的
///   2. 剩下的里面优先取**名字以 `_01` 结尾、且数字后缀最少**的那个 —— 那就是身躯
///      （`BaiGuNiangNiang_01` ✓、`BLJ_01` ✓ 而不是 `BLJ_01_01`、
///        `BaiXiongJing_01_01` ✓ 而不是 `_01_02`、`Changdaonv_01` ✓ 而不是 `ChangDaoNv_02`）
///   3. 都对不上（`gou` / `muji` 这种只有一个网格的）→ 取体积最大的
///
/// 包围盒取自 mesh 的**绑定姿势本地包围盒**（`sharedMesh.bounds`）再变换到世界，
/// 而不是 `Renderer.bounds` —— 后者是「当前姿势」的动态包围盒，没播动画时不可靠。
/// </summary>
public static class NpcBodyBounds
{
    /// <summary>名字里带这些字样的当武器排掉</summary>
    static readonly string[] 武器关键字 = { "wuqi", "weapon", "武器" };

    static readonly Regex 数字后缀 = new Regex(@"_(\d+)(?=_|$)", RegexOptions.Compiled);

    /// <summary>取身躯包围盒。取不到渲染体时返回 false</summary>
    public static bool 取(GameObject 目标, out Bounds 结果)
    {
        结果 = default;
        if (目标 == null) return false;

        var 身躯 = 取身躯渲染体(目标);
        if (身躯 == null) return false;
        if (!取网格包围盒(身躯, out 结果)) return false;

        // ---- 安全阀 ----
        // 万一「以 _01 结尾」挑中的其实是个小挂件（实测 BaiLuJing_02 挑出来只有 0.69 米高），
        // 就退回「所有非武器渲染体的并集」—— 宁可宽松一点，也不能小到能穿过去。
        if (取非武器并集(目标, out var 并集))
        {
            float 并集高 = 并集.size.y;
            if (并集高 > 0.01f && 结果.size.y < 并集高 * 0.6f)
                结果 = 并集;
        }
        return true;
    }

    /// <summary>所有「非武器」渲染体的并集包围盒</summary>
    public static bool 取非武器并集(GameObject 目标, out Bounds 结果)
    {
        结果 = default;
        if (目标 == null) return false;

        var 全部 = 目标.GetComponentsInChildren<Renderer>(true);
        bool 有 = false;
        foreach (var r in 全部)
        {
            if (r == null || !r.enabled || !有网格(r) || 是武器(r.name)) continue;
            if (!取网格包围盒(r, out var b)) continue;
            if (!有) { 结果 = b; 有 = true; }
            else 结果.Encapsulate(b);
        }
        return 有;
    }

    /// <summary>取「身躯」那个渲染体（可能为 null）</summary>
    public static Renderer 取身躯渲染体(GameObject 目标)
    {
        if (目标 == null) return null;

        var 全部 = 目标.GetComponentsInChildren<Renderer>(true);
        if (全部 == null || 全部.Length == 0) return null;

        // ---- 1) 排掉武器（和没有网格的）----
        var 候选 = new List<Renderer>();
        foreach (var r in 全部)
        {
            if (r == null || !r.enabled) continue;
            if (!有网格(r)) continue;
            if (是武器(r.name)) continue;
            候选.Add(r);
        }
        if (候选.Count == 0) return 最大体积(全部);
        if (候选.Count == 1) return 候选[0];

        // ---- 2) 优先「以 _01 结尾且数字后缀最少」----
        Renderer 最佳 = null;
        int 最佳组数 = int.MaxValue;
        foreach (var r in 候选)
        {
            string 名 = r.name.Trim().ToLowerInvariant();
            if (!名.EndsWith("_01")) continue;

            int 组数 = 数字后缀.Matches(r.name).Count;
            if (组数 < 最佳组数) { 最佳组数 = 组数; 最佳 = r; }
        }
        if (最佳 != null) return 最佳;

        // ---- 3) 兜底：体积最大的 ----
        return 最大体积(候选.ToArray());
    }

    static bool 是武器(string 名)
    {
        if (string.IsNullOrEmpty(名)) return false;
        string 小写 = 名.ToLowerInvariant();
        foreach (var k in 武器关键字)
            if (小写.Contains(k)) return true;
        return false;
    }

    static bool 有网格(Renderer r)
    {
        if (r is SkinnedMeshRenderer s) return s.sharedMesh != null;
        var mf = r.GetComponent<MeshFilter>();
        return mf != null && mf.sharedMesh != null;
    }

    static Renderer 最大体积(Renderer[] 们)
    {
        Renderer 最佳 = null; float 最大 = -1f;
        foreach (var r in 们)
        {
            if (r == null || !有网格(r)) continue;
            var s = r.bounds.size;
            float v = s.x * s.y * s.z;
            if (v > 最大) { 最大 = v; 最佳 = r; }
        }
        return 最佳;
    }

    /// <summary>
    /// 渲染体的世界包围盒。
    ///
    /// ### 蒙皮网格：**自己蒙皮算真实顶点**（2026-09-29 修）
    /// 【坑·已修】以前这里用的是 `sharedMesh.bounds`（**绑定姿势的本地包围盒**）再乘节点的矩阵 ——
    /// 对蒙皮网格这是**错的**：网格的顶点位置由**骨骼**决定，绑定姿势的包围盒跟真实蒙皮范围可以差很远。
    /// 实测 **玄蜂**：这样量出来只有 **1.16 米高**，而真实蒙皮范围是 **2.81 × 1.51 × 1.21** ✗
    /// → 后果（用户报的）：
    ///   · **Collider 太小** → 点它身上大部分地方**射线打不到** → **右键锁不上**（连红圈都不出）
    ///   · **血条高度按 Collider 算** → 只到 2.22 米，而模型真实顶在 **2.59 米**
    ///     → **血条被怪自己的模型挡住**
    /// 所以改成：`骨骼.localToWorldMatrix × bindpose × 顶点` 按权重加权，取真实顶点的包围盒。
    /// 顶点多的网格会有一次开销，但这个函数只在**生成碰撞体 / 标定贴地**时调，不在每帧。
    ///
    /// 拿不到骨骼权重（老资产 / 静态网格）时退回原来的做法。
    /// </summary>
    public static bool 取网格包围盒(Renderer r, out Bounds 世界)
    {
        世界 = default;
        if (r == null) return false;

        try
        {
            // ---- 蒙皮网格：真实蒙皮包围盒 ----
            // ⚠️ 这里**不能**让 蒙皮包围盒 的异常穿出去 —— 穿出去的话下面
            //    「退回绑定姿势包围盒」的兜底执行不到，调用方拿到 default，
            //    表现是一串毫不相干的毛病（血条 / 贴地）。
            //    所以 蒙皮包围盒 内部已经自己 try/catch + 判 isReadable。
            if (r is SkinnedMeshRenderer 蒙皮 && 蒙皮.sharedMesh != null)
            {
                if (蒙皮包围盒(蒙皮, out 世界)) return true;
            }

            Bounds 本地;
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) 本地 = smr.sharedMesh.bounds;
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) { 世界 = r.bounds; return true; }
                本地 = mf.sharedMesh.bounds;
            }
            // 包围盒数据本身可能是坏的（NaN / 全 0）—— 用 Renderer.bounds 兜底
            if (float.IsNaN(本地.center.x) || float.IsNaN(本地.size.x)) { 世界 = r.bounds; return true; }

            var t = r.transform;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                var 角 = new Vector3(
                    (i & 1) == 0 ? 本地.min.x : 本地.max.x,
                    (i & 2) == 0 ? 本地.min.y : 本地.max.y,
                    (i & 4) == 0 ? 本地.min.z : 本地.max.z);
                var w = t.TransformPoint(角);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
            if (float.IsNaN(min.x) || float.IsInfinity(min.x)) { 世界 = r.bounds; return true; }
            世界.SetMinMax(min, max);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[NpcBodyBounds] 取包围盒失败，用 Renderer.bounds 兜底：{r.name} — {e.Message}");
            世界 = r.bounds;
            return true;
        }
    }

    /// <summary>
    /// 蒙皮网格的**真实**世界包围盒：逐顶点自己做蒙皮（四根骨骼按权重加权）。
    /// 骨骼权重拿不到时返回 false，让调用方退回绑定姿势包围盒。
    ///
    /// ⚠️【踩过的坑·2026-10-01】**读顶点前必须判 <c>isReadable</c>！**
    ///
    /// 原来这里直接 `var vs = m.vertices;`，而模型是 glb 转进来的、
    /// **Import Settings 里没勾 Read/Write**。于是这一行**抛异常**：
    /// <code>Not allowed to access vertices on mesh 'tripo_node_xxx' (isReadable is false)</code>
    ///
    /// 异常**直接穿透** <see cref="取网格包围盒"/>（那个函数里没有 catch），
    /// 连它下面「退回绑定姿势包围盒」的兜底都**执行不到** ——
    /// 于是 <see cref="取"/> 整个失败，<c>out</c> 结果还是 <c>default</c>。
    ///
    /// 后果是一串看似**毫不相干**的症状，非常难查：
    ///   · <b>NPC 血条不显示</b>（<c>NpcIndicator.计算尺寸</c> 拿不到尺寸）
    ///   · <b>NPC 贴地被算歪</b>（<c>NpcAiBase.标定贴地</c> 拿不到最低点）
    ///   · <b>模型看着不对劲/看不见</b>（包围盒为 0）
    ///
    /// ⇒ 两条一起做：**先判 `isReadable`，再包 try/catch**。
    ///   判 isReadable 是正路；try/catch 是防止别的访问方式（boneWeights / bindposes）
    ///   也在没开 Read/Write 时抛。
    /// </summary>
    static bool 蒙皮包围盒(SkinnedMeshRenderer 蒙皮, out Bounds 世界)
    {
        世界 = default;
        var m = 蒙皮.sharedMesh;
        if (m == null) return false;

        // ★ 关键闸门：没开 Read/Write 的网格**根本不能读顶点**，直接放弃、让调用方兜底
        if (!m.isReadable) return false;

        try
        {
            var vs = m.vertices;
            var bw = m.boneWeights;
            var bp = m.bindposes;
            var 骨 = 蒙皮.bones;
            if (vs == null || vs.Length == 0) return false;
            if (bw == null || bw.Length != vs.Length || bp == null || 骨 == null || 骨.Length == 0) return false;

            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            bool 有 = false;
            for (int i = 0; i < vs.Length; i++)
            {
                Vector3 w = Vector3.zero;
                float 总 = 0f;
                for (int k = 0; k < 4; k++)
                {
                    int bi = k == 0 ? bw[i].boneIndex0 : k == 1 ? bw[i].boneIndex1 : k == 2 ? bw[i].boneIndex2 : bw[i].boneIndex3;
                    float wt = k == 0 ? bw[i].weight0 : k == 1 ? bw[i].weight1 : k == 2 ? bw[i].weight2 : bw[i].weight3;
                    if (wt <= 0.0001f || bi < 0 || bi >= 骨.Length || 骨[bi] == null) continue;
                    w += (骨[bi].localToWorldMatrix * bp[bi]).MultiplyPoint3x4(vs[i]) * wt;
                    总 += wt;
                }
                if (总 < 0.0001f) w = 蒙皮.transform.TransformPoint(vs[i]);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
                有 = true;
            }
            if (!有) return false;
            世界.SetMinMax(min, max);
            return true;
        }
        catch (System.Exception e)
        {
            // 兜底：绝不把异常放出去 —— 放出去会让调用方的 out 参数停在 default，
            // 引发一串"看起来毫不相干"的毛病（血条 / 贴地 / 碰撞体）
            Debug.LogWarning($"[NpcBodyBounds] 读蒙皮顶点失败，退回绑定姿势包围盒：{蒙皮.name} — {e.Message}");
            return false;
        }
    }

    /// <summary>身躯最低点的世界 Y。取不到就用碰撞体兜底</summary>
    public static float 取最低点(GameObject 目标)
    {
        if (取(目标, out var b)) return b.min.y;

        var col = 目标 != null ? 目标.GetComponent<Collider>() : null;
        return col != null ? col.bounds.min.y : (目标 != null ? 目标.transform.position.y : 0f);
    }

    /// <summary>
    /// 给一个碰撞体算出「贴合身躯」的胶囊参数（本地空间）。
    /// 返回 false 表示算不出来（没有渲染体）。
    /// </summary>
    public static bool 算胶囊(GameObject 目标, out Vector3 中心, out float 半径, out float 高度)
    {
        中心 = Vector3.zero; 半径 = 0.5f; 高度 = 1.8f;
        if (!取(目标, out var 世界)) return false;

        var t = 目标.transform;
        // 世界包围盒 → 本地：8 个角逆变换再合并，兼容任意旋转/缩放
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            var 角 = new Vector3(
                (i & 1) == 0 ? 世界.min.x : 世界.max.x,
                (i & 2) == 0 ? 世界.min.y : 世界.max.y,
                (i & 4) == 0 ? 世界.min.z : 世界.max.z);
            var 本地 = t.InverseTransformPoint(角);
            min = Vector3.Min(min, 本地);
            max = Vector3.Max(max, 本地);
        }

        Vector3 尺寸 = max - min;
        float 水平半径 = Mathf.Max(尺寸.x, 尺寸.z) * 0.5f;
        float 竖直半径 = 尺寸.y * 0.5f;

        // 【坑·已修·2026-09-29】半径**不能**取 min(水平, 竖直)。
        //
        // 原来写的是 `min(水平半径, 竖直半径)`，理由是"轴对齐胶囊半径超过半高会被 Unity 夹成球"。
        // 但**又宽又扁**的身体会被这个 min 掐死：实测 **玄蜂** 身体 2.80 宽 × 1.16 高
        // → min(1.40, 0.58) = **0.58**，得到一个又细又矮的胶囊，**罩不住身体的两侧和上方** ✗
        // 后果（用户报的）：**点它身上大部分地方射线打不到 → 右键锁不上**（连红圈都不出）。
        //
        // 现在：**按水平尺寸给半径**，高度不足就抬高到 2r（Unity 会把这根胶囊当球用）——
        // 这也正是项目里本来就在用的形状（白熊精 r=1.25 h=2.50 就是个球状胶囊）。
        // 宁可稍微宽松（点到旁边的空气也能选中），也不能小到**点它自己都点不到**。
        半径 = Mathf.Max(0.05f, 水平半径);
        高度 = Mathf.Max(半径 * 2f, 尺寸.y);
        中心 = (min + max) * 0.5f;
        return true;
    }

    // ---- ASCII 别名 ----
    public static Renderer GetBodyRenderer(GameObject go) => 取身躯渲染体(go);
    public static float GetLowestY(GameObject go) => 取最低点(go);
    public static bool TryGetBodyBounds(GameObject go, out Bounds b) => 取(go, out b);
}
