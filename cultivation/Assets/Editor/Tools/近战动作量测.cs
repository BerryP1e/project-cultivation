using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// **近战动作量测**：把一只怪的 `Attack1` / `Attack2` 逐帧采样，
/// 量出「判定体最前伸到身前多少米」以及**峰值出现在动画的百分之多少**。
///
/// 这两个数正是 <see cref="NpcAttackConfig"/> 要的：
///   · 峰值进度 → `出手进度`
///   · 峰值前伸 → 用来定 `攻击距离`（观感口径：×0.73，见下）
///
/// ### 为什么要 ×0.73 定攻击距离
/// 白熊精的实测：武器最远伸 **3.44 米**，但站位定的是 **2.5 米**（≈ 3.44 × 0.73）。
/// 原因写在 `NpcAiBaiXiongJing` 的类注释里 —— **别按"判定够得着"定站位**，
/// 那样武器尖只是刚够到人、看着像打空；要让武器**整条从玩家身上扫过去**。
///
/// ### 怎么用
/// 菜单 **修仙 / 怪物 / 量近战动作范围**。输出三样东西：
///   · **峰值前伸 / 峰值进度** → 用来定 `攻击距离`（观感口径 ×0.73）和进度式判定的出手点
///   · **接触窗**（把玩家盒子摆在站位上，逐帧判相交）→ `出手进度 / 判定结束进度`
///
/// ### ⚠️ 关于「白熊精标定」
/// 早先这里写着"白熊精的真值 3.44 @ 0.52，工具量出来对不上就是工具坏了"。
/// **现在工具量出来是 2.24 @ 0.50，对不上** —— 而同一版工具量 11 只妖魔的结果
/// 和上一轮**完全一致**，所以工具本身是自洽的：那个 3.44 是更早的**手量值**，存疑。
/// 又因为白熊精走的是**进度式判定**（不看几何），这个差异目前不影响游戏，先留着存疑。
/// **真正可信的验收是运行时**：把玩家摆在站位上真打一轮，看掉不掉血。
/// </summary>
public static class 近战动作量测
{
    /// <summary>量一条动作：返回 (峰值进度, 峰值前伸米, 片段长度)</summary>
    public struct 结果
    {
        public float 峰值进度;
        public float 峰值前伸;
        public float 片段长度;
        public bool 有效;

        // ---- 接触窗（只有给了「站位距离」才量）----
        /// <summary>判定体第一次碰到玩家的进度</summary>
        public float 接触起;
        /// <summary>判定体最后一次碰到玩家的进度</summary>
        public float 接触止;
        /// <summary>碰到的采样帧数（0 = 一直没碰到）</summary>
        public int 接触帧数;

        public bool 有接触 => 接触帧数 > 0;
    }

    /// <summary>每只怪：prefab 路径（相对 resources）+ 判定体用的子物件名（留空 = 整个角色）</summary>
    struct 目标
    {
        public string 名字;
        public string prefab路径;
        public string 判定体名;
        public 目标(string n, string p, string t) { 名字 = n; prefab路径 = p; 判定体名 = t; }
    }

    static readonly 目标[] 目标s =
    {
        // ---- 标定样本（真值已知，工具量出来必须对得上）----
        new 目标("白熊精(标定)", "NPC/Demon/BaiXiongJing/BaiXiongJing_01", "BaiXiongJing_wuqi"),
        new 目标("野猪(标定)",   "NPC/Beast/YeZhu/YeZhu_01",               ""),

        // ---- 有武器：判定网格名 ----
        new 目标("长刀忠魂", "NPC/Demon/ChangDaoZhongHun/ChangDaoZhongHun_01", "ChangDaoZhongHun_WuQi"),
        new 目标("赤蟒精",   "NPC/Demon/ChiMangJin_01/ChiMangJin_01",           "ChiMangJin_WuQi"),
        new 目标("锤魔",     "NPC/Demon/ChuiMo/ChuiMo_01",                     "ChuiMo_wuqi"),
        new 目标("盾甲忠魂", "NPC/Demon/DunJiaZhongHun/DunJiaZhongHun_01",     "DunJiaZhongHun_WuQi"),
        new 目标("鬼气将",   "NPC/Demon/GuiqiJiang_01/GuiqiJiang_01",          "GuiqiJiang_Wuqi1"),
        new 目标("蛤蟆精",   "NPC/Demon/HaMaJing/HaMaJing_01",                 "HaMaJing_wuqi"),
        new 目标("黑鱼精",   "NPC/Demon/HeiYuJin/HeiYuJin_01",                 "HeiYuJin_wuqi"),
        new 目标("金牛",     "NPC/Demon/JinNiu/JinNiu_01",                     "wuqi_1"),
        new 目标("南山大王", "NPC/Demon/NanShanDaWang/NanShanDaWang_01",       "NanShanDaWwang_WuQi01"),
        new 目标("牛头妖",   "NPC/Demon/NiuTouYao/NiuTouYao_01",               "NiuTouYao_01_Weapon"),
        new 目标("穷奇",     "NPC/Demon/QiongQi/QiongQi_01",                   "QiongQi_wuqi"),

        // ---- 没武器：整个角色按动作范围判 ----
        new 目标("方良兽", "NPC/Demon/FangLiangShou/FangLiangShou_01", ""),
        new 目标("寒狼",   "NPC/Demon/HanLang/HanLang_01",             ""),
        new 目标("巨石怪", "NPC/Demon/JuShiGuai/JuShiGuai_01",         ""),
        new 目标("老虎",   "NPC/Demon/LaoHu/LaoHu_01",                 ""),
        new 目标("猎犬",   "NPC/Demon/LieQuan/LieQuan_01",             ""),
        new 目标("梼杌",   "NPC/Demon/TaoWu/TaoWu_01",                 ""),
        new 目标("铁甲蝠", "NPC/Demon/TieJiaFu/TieJiaFu_01",           ""),
        new 目标("熊怪",   "NPC/Demon/XiongGuai/XiongGuai_01",         ""),
        new 目标("蜥蜴精", "NPC/Demon/XiYiJing/XiYiJing_01",           ""),
        new 目标("岩石怪", "NPC/Demon/YanShiGuai/YanShiGuai_01",       ""),
    };

    [MenuItem("修仙/怪物/量近战动作范围", false, 700)]
    public static void 量全部()
    {
        var 报 = new System.Text.StringBuilder();
        报.AppendLine("近战动作量测（每 2% 采一帧）");
        报.AppendLine("攻击距离 建议 = 峰值前伸 × 0.73（白熊精口径：3.44 → 2.5）");
        报.AppendLine("");
        报.AppendLine("怪物".PadRight(14) + "招".PadRight(9) + "片段长  峰值进度  峰值前伸  站位    接触窗(按站位采样)");
        报.AppendLine(new string('-', 96));

        foreach (var t in 目标s)
        {
            var prefab = Resources.Load<GameObject>(t.prefab路径);
            if (prefab == null) { 报.AppendLine(t.名字.PadRight(14) + "✗ prefab 加载不到：" + t.prefab路径); continue; }

            var go = Object.Instantiate(prefab);
            go.name = "量测_" + t.名字;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            // 判定体：按名字找那根子物件
            Transform 根 = go.transform;
            if (!string.IsNullOrEmpty(t.判定体名))
            {
                foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                    if (tr.name == t.判定体名) { 根 = tr; break; }
                if (根 == go.transform) 报.AppendLine("   ⚠ " + t.名字 + " 找不到判定体「" + t.判定体名 + "」，退回整个角色");
            }
            var 渲染s = 根.GetComponentsInChildren<Renderer>(true);
            // 蒙皮网格要开 updateWhenOffscreen，否则包围盒不跟着骨骼更新（量出来是静止的）
            foreach (var r in 渲染s)
            {
                var sm = r as SkinnedMeshRenderer;
                if (sm != null) sm.updateWhenOffscreen = true;
            }

            var an = go.GetComponentInChildren<Animator>();
            if (an == null) { 报.AppendLine(t.名字.PadRight(14) + "✗ 没有 Animator"); Object.DestroyImmediate(go); continue; }
            var ctrl = an.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;

            // ---- 一遍采样，把「峰值」和「每帧包围盒」都拿到手 ----
            var 片段s = new AnimationClip[2];
            var 峰s = new 结果[2];
            var 最小表s = new Vector3[2][];
            var 最大表s = new Vector3[2][];
            for (int i = 0; i < 2; i++)
            {
                片段s[i] = 取片段(ctrl, i == 0 ? "Attack1" : "Attack2");
                if (片段s[i] != null)
                    峰s[i] = 量一条(go, 片段s[i], 渲染s, out 最小表s[i], out 最大表s[i]);
            }

            // 站位口径和 近战妖魔Ai 完全一致：min(两招前伸) × 0.73（取短的，保证两招都扫得到人）
            float 甲 = 峰s[0].有效 ? 峰s[0].峰值前伸 : (峰s[1].有效 ? 峰s[1].峰值前伸 : 0f);
            float 乙 = 峰s[1].有效 ? 峰s[1].峰值前伸 : 甲;
            float 站位 = Mathf.Max(1.2f, Mathf.Min(甲, 乙) * 0.73f);

            for (int i = 0; i < 2; i++)
            {
                string 招 = i == 0 ? "Attack1" : "Attack2";
                if (片段s[i] == null)
                {
                    报.AppendLine(t.名字.PadRight(14) + 招.PadRight(9) + "✗ 控制器里没有这个状态");
                    continue;
                }
                float 起, 止; int 帧数;
                算接触窗(最小表s[i], 最大表s[i], 站位, out 起, out 止, out 帧数);
                报.AppendLine(t.名字.PadRight(14) + 招.PadRight(9)
                    + 片段s[i].length.ToString("0.00").PadRight(8)
                    + 峰s[i].峰值进度.ToString("0.00").PadRight(10)
                    + 峰s[i].峰值前伸.ToString("0.00").PadRight(10)
                    + 站位.ToString("0.0").PadRight(8)
                    + (帧数 > 0
                        ? 起.ToString("0.00") + " ~ " + 止.ToString("0.00") + "  (" + 帧数 + " 帧)"
                        : "✗ 这个站位碰不到"));
            }
            Object.DestroyImmediate(go);
        }

        Debug.Log(报.ToString());
    }

    static AnimationClip 取片段(UnityEditor.Animations.AnimatorController ctrl, string 状态名)
    {
        if (ctrl == null) return null;
        foreach (var lay in ctrl.layers)
            foreach (var st in lay.stateMachine.states)
                if (st.state.name == 状态名)
                    return st.state.motion as AnimationClip;
        return null;
    }

    /// <summary>
    /// 逐帧采样一次，返回峰值 + **每帧的判定体包围盒**（本地坐标系）。
    /// 接触窗**不要在这里算** —— 站位依赖两招的峰值，必须等两招都量完才知道。
    /// 而且**不能为了窗口再采样第二遍**：实测同一个实例连采两轮，第二轮的数字会跑偏
    /// （白熊精 Attack1 从真值 3.44 掉到 2.24），所以每帧的盒子必须一次采完存下来。
    /// </summary>
    static 结果 量一条(GameObject go, AnimationClip 片段, Renderer[] 渲染s,
                      out Vector3[] 最小表, out Vector3[] 最大表)
    {
        var 果 = new 结果 { 片段长度 = 片段.length, 接触起 = 1f, 接触止 = 0f };
        最小表 = null; 最大表 = null;
        if (片段.length < 0.01f) return 果;

        int 步数 = 50;                      // 每 2% 一帧
        最小表 = new Vector3[步数 + 1];
        最大表 = new Vector3[步数 + 1];

        AnimationMode.StartAnimationMode();
        try
        {
            for (int i = 0; i <= 步数; i++)
            {
                float k = (float)i / 步数;
                AnimationMode.SampleAnimationClip(go, 片段, k * 片段.length);

                Vector3 最小, 最大;
                量包围盒(go, 渲染s, out 最小, out 最大);
                最小表[i] = 最小;
                最大表[i] = 最大;

                if (最大.z > 果.峰值前伸) { 果.峰值前伸 = 最大.z; 果.峰值进度 = k; 果.有效 = true; }
            }
        }
        finally { AnimationMode.StopAnimationMode(); }
        return 果;
    }

    /// <summary>
    /// **接触窗**：把玩家当成一个 0.8×1.6×0.8 的盒子（+ 判定外扩 0.15）摆在判定体正前方
    /// `站位` 米处，看哪几帧判定体的包围盒和它相交。这就是
    /// `NpcAttackConfig.出手进度 / 判定结束进度` 该填的值。
    /// </summary>
    static void 算接触窗(Vector3[] 最小表, Vector3[] 最大表, float 站位,
                       out float 起, out float 止, out int 帧数)
    {
        起 = 1f; 止 = 0f; 帧数 = 0;
        if (最小表 == null || 站位 <= 0f) return;

        // 和运行时 NpcAiBase.取玩家体积() 的兜底口径一致
        var 玩家心 = new Vector3(0f, 玩家判定高度, 站位);
        var 玩家半 = new Vector3(0.4f, 0.8f, 0.4f) + Vector3.one * 0.15f;

        for (int i = 0; i < 最小表.Length; i++)
        {
            var 最小 = 最小表[i];
            var 最大 = 最大表[i];
            if (最大.x >= 玩家心.x - 玩家半.x && 最小.x <= 玩家心.x + 玩家半.x
             && 最大.y >= 玩家心.y - 玩家半.y && 最小.y <= 玩家心.y + 玩家半.y
             && 最大.z >= 玩家心.z - 玩家半.z && 最小.z <= 玩家心.z + 玩家半.z)
            {
                帧数++;
                float k = (float)i / (最小表.Length - 1);
                if (k < 起) 起 = k;
                if (k > 止) 止 = k;
            }
        }
    }

    /// <summary>玩家判定高度（= <see cref="PlayerTarget.判定高度"/> 的默认值）</summary>
    const float 玩家判定高度 = 1.1f;

    /// <summary>
    /// 「判定体」的顶点包围盒，坐标是**角色根节点往上一点的世界坐标**（= 世界米）。
    ///
    /// ⚠️ **这里踩过三个坑**，都不能用现成 API 偷懒：
    ///
    /// 0. **不能用「角色本地坐标」**（`worldToLocalMatrix`）—— prefab 根节点带缩放
    ///    （白熊精 1.54 倍），本地米 ≠ 世界米：实测白熊精 Attack1 世界口径 3.44 米、
    ///    本地口径只有 2.24 米。而运行时的 `攻击距离` 和 `Renderer.bounds` **都是世界米**，
    ///    所以量测必须用世界口径，否则站位和接触窗全都对不上。
    /// 1. `Renderer.bounds` 不行 —— **蒙皮网格的 bounds 在编辑模式下不跟着骨骼更新**
    ///    （实测白熊精恒为 1.56m，真值 3.44m）。
    /// 2. `BakeMesh()` + 节点矩阵也不行 —— 这些武器节点**本身就是
    ///    `SkinnedMeshRenderer`**，网格位置由**骨骼**决定，节点自己的 `lossyScale`
    ///    不该参与（实测鬼气将的节点 scale=0.02 → 量出 0.00m；
    ///    南山大王的节点 scale=6.92 → 量出 27m，全是假的）。
    ///
    /// 所以这里**自己做蒙皮运算**：`骨骼.localToWorldMatrix × bindpose × 顶点`，按权重加权。
    /// 静态网格才用节点矩阵。
    /// </summary>
    static void 量包围盒(GameObject go, Renderer[] 渲染s, out Vector3 最小, out Vector3 最大)
    {
        最小 = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        最大 = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var 原点 = go.transform.position;      // 量测时根节点在原点、朝向 identity，所以世界坐标就是"身前多少米"

        foreach (var r in 渲染s)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

            var sm = r as SkinnedMeshRenderer;
            if (sm != null)
            {
                var m = sm.sharedMesh;
                if (m == null) continue;
                var vs = m.vertices;
                var bw = m.boneWeights;
                var bp = m.bindposes;
                var 骨 = sm.bones;
                bool 有权重 = bw != null && bw.Length == vs.Length && bp != null && 骨 != null && 骨.Length > 0;

                for (int i = 0; i < vs.Length; i++)
                {
                    Vector3 w;
                    if (有权重)
                    {
                        // 四个权重分别处理
                        w = 蒙一个(vs[i], bw[i].boneIndex0, bw[i].weight0, 骨, bp)
                          + 蒙一个(vs[i], bw[i].boneIndex1, bw[i].weight1, 骨, bp)
                          + 蒙一个(vs[i], bw[i].boneIndex2, bw[i].weight2, 骨, bp)
                          + 蒙一个(vs[i], bw[i].boneIndex3, bw[i].weight3, 骨, bp);
                        float 总 = bw[i].weight0 + bw[i].weight1 + bw[i].weight2 + bw[i].weight3;
                        if (总 < 0.0001f) w = sm.transform.TransformPoint(vs[i]);   // 没有权重就退回节点矩阵
                    }
                    else w = sm.transform.TransformPoint(vs[i]);

                    吃(w - 原点, ref 最小, ref 最大);
                }
                continue;
            }

            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var M = r.transform.localToWorldMatrix;
            var vs2 = mf.sharedMesh.vertices;
            for (int i = 0; i < vs2.Length; i++) 吃(M.MultiplyPoint3x4(vs2[i]) - 原点, ref 最小, ref 最大);
        }
    }

    static void 吃(Vector3 p, ref Vector3 最小, ref Vector3 最大)
    {
        if (p.x < 最小.x) 最小.x = p.x; if (p.x > 最大.x) 最大.x = p.x;
        if (p.y < 最小.y) 最小.y = p.y; if (p.y > 最大.y) 最大.y = p.y;
        if (p.z < 最小.z) 最小.z = p.z; if (p.z > 最大.z) 最大.z = p.z;
    }

    /// <summary>单个骨骼影响：骨骼世界矩阵 × bindpose × 顶点，再乘权重</summary>
    static Vector3 蒙一个(Vector3 v, int 骨索引, float 权, Transform[] 骨, Matrix4x4[] bp)
    {
        if (权 <= 0.0001f || 骨索引 < 0 || 骨索引 >= 骨.Length || 骨[骨索引] == null) return Vector3.zero;
        var M = 骨[骨索引].localToWorldMatrix * bp[骨索引];
        return M.MultiplyPoint3x4(v) * 权;
    }
}
