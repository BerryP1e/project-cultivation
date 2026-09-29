using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

/// <summary>
/// **飞弹出生点量测**：把某只怪的某个动作采样到指定进度，量出**出生点该挂在哪根骨骼、偏移多少**。
///
/// ### 为什么需要它
/// 策划说的出生点是「右手骨骼」「武器顶端」「尾部（模型末端）」这种人话，
/// 而 `NpcAttackConfig.挂点` 要的是**一个骨骼名 + 一个局部偏移**。
/// 这些怪的武器**根本不是骨骼** —— 是蒙皮网格（`GouTouJunShi_01_Weapon` 只有 1 根骨骼，
/// 网格本身长 2 米），所以"武器顶端"没有任何节点可以挂 → 只能量出「最近的骨骼 + 偏移」。
/// 玄蜂更极端：整个模型就一个蒙皮网格，连尾巴骨骼都没有。
///
/// ### 三种模式
/// | 模式 | 取哪个点 |
/// |---|---|
/// | `武器顶端` | 指定网格里**离它自己的骨骼最远**的那个顶点（= 尖端） |
/// | `武器中部` | 指定网格顶点包围盒的**中心** |
/// | `模型末端` | **整个模型**里**最靠后**（本地 Z 最小）的顶点 —— 尾巴/尾针用这个 |
///
/// ### 坑
/// 顶点位置**必须自己做蒙皮运算**（`骨骼.localToWorldMatrix × bindpose × 顶点` 加权）：
/// 编辑模式下 `Renderer.bounds` 不跟骨骼更新，`BakeMesh` 也不行（见《怪物近战判定》§5.2）。
/// 量出来的点再**转回最近骨骼的局部空间**，这样运行时骨骼一动、出生点跟着动。
/// </summary>
public static class 飞弹出生点量测
{
    struct 目标
    {
        public string 名字;
        public string prefab路径;
        public string 网格节点名;     // 空 = 整个模型
        public string 动作名;
        public float 进度;
        public string 模式;
        public 目标(string n, string p, string 网格, string 动作, float 进度, string 模式)
        { 名字 = n; prefab路径 = p; 网格节点名 = 网格; 动作名 = 动作; this.进度 = 进度; this.模式 = 模式; }
    }

    static readonly 目标[] 目标s =
    {
        // 有手的直接用手骨骼，偏移 0（列出来是留个"已知正确"的对照）
        new 目标("百眼魔君-右手", "NPC/Demon/BaiYanMoJun/BaiYanMoJun_01", "", "Attack1", 0.355f, "骨骼:Bip01 R Hand"),
        new 目标("长火长老-左手", "NPC/Demon/ChangHuoZhangLao/ChangHuoZhangLao_01", "", "Attack1", 0.468f, "骨骼:Bip01 L Hand"),
        new 目标("长火长老-右手", "NPC/Demon/ChangHuoZhangLao/ChangHuoZhangLao_01", "", "Attack2", 0.76f, "骨骼:Bip01 R Hand"),
        new 目标("飞蛇-头部",     "NPC/Demon/Feishe/Feishe_01",                 "", "Attack1", 0.51f, "骨骼:Bip01 Head"),
        // 尾部：蜘蛛精有尾巴骨骼；玄蜂没有，只能量"模型最靠后的点"
        new 目标("蜘蛛精-尾部",   "NPC/Demon/ZhiZhuJing/ZhiZhuJing_01",         "", "Attack2", 0.77f, "模型末端"),
        new 目标("玄蜂-尾部",     "NPC/Demon/XuanFeng/XuanFeng_01",             "", "Attack1", 0.465f, "模型末端"),
        // 武器是蒙皮网格：量"离它自己骨骼最远的顶点"（尖端）和包围盒中心（中部）
        new 目标("狗头军师-武器顶端", "NPC/Demon/GouTouJunShi/GouTouJunShi_01", "GouTouJunShi_01_Weapon", "Attack1", 0.31f, "武器顶端"),
        new 目标("狗头军师-武器顶端2", "NPC/Demon/GouTouJunShi/GouTouJunShi_01", "GouTouJunShi_01_Weapon", "Attack2", 0.57f, "武器顶端"),
        new 目标("老龟精-武器顶端",   "NPC/Demon/LaoGuiJing/LaoGuiJing_01",     "LaoGuiJing_wuqi", "Attack1", 0.465f, "武器顶端"),
        new 目标("老龟精-武器顶端2",  "NPC/Demon/LaoGuiJing/LaoGuiJing_01",     "LaoGuiJing_wuqi", "Attack2", 0.645f, "武器顶端"),
        new 目标("箭魔-武器中部",     "NPC/Demon/JianMo/JianMo_01",             "JianMo_01_02", "Attack1", 0.31f, "武器中部"),
        new 目标("箭魔-武器中部2",    "NPC/Demon/JianMo/JianMo_01",             "JianMo_01_02", "Attack2", 0.57f, "武器中部"),
    };

    [MenuItem("修仙/怪物/量飞弹出生点", false, 701)]
    public static void 量全部()
    {
        var 报 = new StringBuilder();
        报.AppendLine("飞弹出生点量测（把动作采样到指定进度，求「最近骨骼 + 局部偏移」）");
        报.AppendLine("");
        报.AppendLine("怪物".PadRight(22) + "动作".PadRight(9) + "进度".PadRight(7) + "模式".PadRight(16)
                       + "→ 挂点骨骼 / 挂点偏移");
        报.AppendLine(new string('-', 96));

        foreach (var t in 目标s)
        {
            var 实例 = 载入(t.prefab路径);
            if (实例 == null) { 报.AppendLine(t.名字.PadRight(22) + "✗ 载入不到 " + t.prefab路径); continue; }

            if (t.模式.StartsWith("骨骼:"))
            {
                // 直接用手/头骨骼，偏移 0（确认这根骨骼真的存在）
                var 名 = t.模式.Substring("骨骼:".Length);
                Transform 找 = null;
                foreach (var tr in 实例.GetComponentsInChildren<Transform>(true)) if (tr.name == 名) { 找 = tr; break; }
                报.AppendLine(t.名字.PadRight(22) + t.动作名.PadRight(9) + t.进度.ToString("0.000").PadRight(7)
                    + "骨骼".PadRight(16) + "→ " + 名 + " / (0, 0, 0)"
                    + (找 == null ? "   ✗ 这个模型里没有这根骨骼！" : "   ✓"));
                Object.DestroyImmediate(实例);
                continue;
            }

            var 片段 = 找片段(实例, t.动作名);
            if (片段 == null)
            {
                报.AppendLine(t.名字.PadRight(22) + t.动作名.PadRight(9) + "✗ 控制器里没这个动作");
                Object.DestroyImmediate(实例);
                continue;
            }

            Vector3 点; string 说明;
            bool 成功 = 量点(实例, t, 片段, out 点, out 说明);
            if (!成功) { 报.AppendLine(t.名字.PadRight(22) + t.动作名.PadRight(9) + "✗ " + 说明); Object.DestroyImmediate(实例); continue; }

            // 找离这个点最近的骨骼（武器网格自己那根骨骼优先 —— 它才是带着武器动的那根）
            Transform 骨 = 最近骨骼(实例, 点, t.网格节点名);
            if (骨 == null) { 报.AppendLine(t.名字.PadRight(22) + t.动作名.PadRight(9) + "✗ 找不到可用骨骼"); Object.DestroyImmediate(实例); continue; }
            // ⚠️ `点` 是**角色本地坐标**，而 `InverseTransformPoint` 要的是**世界坐标** ——
            // 直接喂进去会得到一堆几百米的鬼数字（第一版就是这么错的：248 / -208）。
            var 世界点 = 实例.transform.TransformPoint(点);
            var 偏移 = 骨.InverseTransformPoint(世界点);
            var 缩放 = 骨.lossyScale;
            报.AppendLine(t.名字.PadRight(22) + t.动作名.PadRight(9) + t.进度.ToString("0.000").PadRight(7)
                + t.模式.PadRight(16) + "→ " + 骨.name + " / (" + 偏移.x.ToString("0.000") + ", "
                + 偏移.y.ToString("0.000") + ", " + 偏移.z.ToString("0.000") + ")   [" + 说明
                + "  骨骼lossyScale=" + 缩放.ToString("0.00") + "]");
            Object.DestroyImmediate(实例);
        }

        Debug.Log(报.ToString());
    }

    static GameObject 载入(string 路径)
    {
        // ⚠️ 这些怪的 FBX 和 prefab **同名同目录**（`MingYao_01.FBX` + `MingYao_01.prefab`），
        // Resources.Load 拿到哪个是不确定的 —— 所以要**按文件夹全取出来、挑带 NpcInstance 的那个**
        var 目录 = 路径.Substring(0, 路径.LastIndexOf('/'));
        var 名 = 路径.Substring(路径.LastIndexOf('/') + 1);
        foreach (var g in Resources.LoadAll<GameObject>(目录))
            if (g != null && g.name == 名 && g.GetComponent<NpcInstance>() != null) return Object.Instantiate(g);
        var 直 = Resources.Load<GameObject>(路径);
        return 直 == null ? null : Object.Instantiate(直);
    }

    static AnimationClip 找片段(GameObject go, string 状态名)
    {
        var an = go.GetComponentInChildren<Animator>();
        var ctrl = an != null ? an.runtimeAnimatorController as UnityEditor.Animations.AnimatorController : null;
        if (ctrl == null) return null;
        foreach (var lay in ctrl.layers)
            foreach (var st in lay.stateMachine.states)
                if (st.state.name == 状态名) return st.state.motion as AnimationClip;
        return null;
    }

    /// <summary>采样到指定进度，求出目标点（本地坐标系 = 角色根节点空间）</summary>
    static bool 量点(GameObject go, 目标 t, AnimationClip 片段, out Vector3 点, out string 说明)
    {
        点 = Vector3.zero;
        说明 = "";
        var 逆 = go.transform.worldToLocalMatrix;

        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.SampleAnimationClip(go, 片段, Mathf.Clamp01(t.进度) * 片段.length);

            Transform 网格 = null;
            if (!string.IsNullOrEmpty(t.网格节点名))
                foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                    if (tr.name == t.网格节点名) { 网格 = tr; break; }

            var 顶点 = new List<Vector3>();
            var 渲染s = 网格 != null ? 网格.GetComponentsInChildren<Renderer>(true)
                                    : go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in 渲染s)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                var sm = r as SkinnedMeshRenderer;
                if (sm != null && sm.sharedMesh != null) 蒙皮顶点(sm, 逆, 顶点);
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var M = 逆 * r.transform.localToWorldMatrix;
                    foreach (var v in mf.sharedMesh.vertices) 顶点.Add(M.MultiplyPoint3x4(v));
                }
            }
            if (顶点.Count == 0) { 说明 = "这个网格没有可用的顶点"; return false; }

            if (t.模式 == "模型末端")
            {
                float 最小 = float.MaxValue;
                foreach (var v in 顶点) if (v.z < 最小) { 最小 = v.z; 点 = v; }
                说明 = "本地 Z 最小的顶点 (" + 点.ToString("0.00") + ")";
            }
            else if (t.模式 == "武器中部")
            {
                var 盒 = new Bounds(顶点[0], Vector3.zero);
                foreach (var v in 顶点) 盒.Encapsulate(v);
                点 = 盒.center;
                说明 = "包围盒中心，尺寸 " + 盒.size.ToString("0.00");
            }
            else
            {
                // 武器顶端：离"这根网格的蒙皮骨骼"最远的顶点
                Transform 靶 = null;
                if (网格 != null)
                {
                    var sm = 网格.GetComponent<SkinnedMeshRenderer>();
                    if (sm != null && sm.rootBone != null) 靶 = sm.rootBone;
                }
                if (靶 == null) 靶 = go.transform;
                var 靶位 = 逆.MultiplyPoint3x4(靶.position);
                float 最远 = -1f;
                foreach (var v in 顶点)
                {
                    float d = Vector3.Distance(v, 靶位);
                    if (d > 最远) { 最远 = d; 点 = v; }
                }
                说明 = "离骨骼(" + 靶.name + ")最远 " + 最远.ToString("0.00") + " 米";
            }
            return true;
        }
        finally { AnimationMode.StopAnimationMode(); }
    }

    /// <summary>蒙皮网格的真实顶点（自己做蒙皮：骨骼矩阵 × bindpose × 顶点，按权重加权）</summary>
    static void 蒙皮顶点(SkinnedMeshRenderer sm, Matrix4x4 逆, List<Vector3> 出)
    {
        var m = sm.sharedMesh;
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
                w = 蒙(vs[i], bw[i].boneIndex0, bw[i].weight0, 骨, bp)
                  + 蒙(vs[i], bw[i].boneIndex1, bw[i].weight1, 骨, bp)
                  + 蒙(vs[i], bw[i].boneIndex2, bw[i].weight2, 骨, bp)
                  + 蒙(vs[i], bw[i].boneIndex3, bw[i].weight3, 骨, bp);
                float 总 = bw[i].weight0 + bw[i].weight1 + bw[i].weight2 + bw[i].weight3;
                if (总 < 0.0001f) w = sm.transform.TransformPoint(vs[i]);
            }
            else w = sm.transform.TransformPoint(vs[i]);
            出.Add(逆.MultiplyPoint3x4(w));
        }
    }

    static Vector3 蒙(Vector3 v, int 骨索引, float 权, Transform[] 骨, Matrix4x4[] bp)
    {
        if (权 <= 0.0001f || 骨索引 < 0 || 骨索引 >= 骨.Length || 骨[骨索引] == null) return Vector3.zero;
        return (骨[骨索引].localToWorldMatrix * bp[骨索引]).MultiplyPoint3x4(v) * 权;
    }

    /// <summary>
    /// 离目标点最近的骨骼。**武器网格自己那根骨骼优先** —— 它才是"带着武器动"的那根，
    /// 用它当挂点的父节点，武器怎么动出生点就怎么动。
    /// </summary>
    static Transform 最近骨骼(GameObject go, Vector3 局部点, string 网格节点名)
    {
        if (!string.IsNullOrEmpty(网格节点名))
        {
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
                if (tr.name == 网格节点名)
                {
                    var sm = tr.GetComponent<SkinnedMeshRenderer>();
                    if (sm != null && sm.rootBone != null) return sm.rootBone;
                    if (sm != null && sm.bones != null && sm.bones.Length > 0 && sm.bones[0] != null) return sm.bones[0];
                }
        }

        var 世界 = go.transform.TransformPoint(局部点);
        Transform 最好 = null;
        float 最近 = float.MaxValue;
        foreach (var tr in go.GetComponentsInChildren<Transform>(true))
        {
            if (tr == go.transform) continue;
            float d = Vector3.Distance(tr.position, 世界);
            if (d < 最近) { 最近 = d; 最好 = tr; }
        }
        return 最好;
    }
}
