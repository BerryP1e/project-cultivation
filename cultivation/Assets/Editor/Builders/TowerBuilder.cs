using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// **镇妖塔场景装配**：生成刷怪点 + 塔控 + 塔界面。
///
/// 菜单：**修仙/镇妖塔/装配镇妖塔场景**
///
/// ## 幂等
///
/// 可以反复跑：会先删掉自己上次建的 <c>刷怪区</c> / <c>塔控</c> 两个节点再重建，
/// **不碰**场景里手工摆的任何东西（塔的模型、玩家、相机、HUD 都不动）。
///
/// ## 摆在哪
///
/// 全都**从地面实测**（向下打射线取地面高度），不写死 Y —— 塔的地面在 y≈4.93，
/// 写死 0 会把刷怪点埋到地下或吊在半空，而这种错**在俯视相机下看不出来**。
///
/// 刷怪点摆成**两圈**（16 个点），让一组 3~8 只怪能围上来而不是叠在一起
/// （叠在一起的表现见 `AI开发注意事项.md` §3.5：一群怪方位角全是 0°）。
/// </summary>
public static class TowerBuilder
{
    const string 场景路径 = "Assets/Scenes/Demon-Suppressing Tower.scene";
    const string 刷怪区名 = "刷怪区";
    const string 塔控名 = "塔控";
    const string 刷怪点名 = "刷怪点";

    /// <summary>刷怪区根节点的名字（墙内判定要跳过它自己，免得自己挡自己）</summary>
    const string 刷怪区根名 = "刷怪区";

    /// <summary>
    /// 刷怪点圈：半径（米）。
    ///
    /// 【实测的塔内尺寸，2026-10-01】
    /// <code>
    /// 墙       ±27.36（内圈边界）
    /// 柱       ±26.13
    /// 地面板   ±29 / ±35.8（地板 y=4.94）
    /// 中央构筑 X -4.4~3.8  Z 10.9~17.0  高到 y=10.4
    /// </code>
    /// 所以内圈 11m 安全，外圈**不能超过 26**（柱）—— 这里取 17，
    /// 留出余地给"柱 + 怪的身躯半径"。而且下面还会做**墙内校验**，
    /// 半径只是候选，最后还是靠碰撞体说话。
    /// </summary>
    static readonly float[] 圈半径 = { 10f, 17f };

    /// <summary>每圈几个点</summary>
    const int 每圈点数 = 8;

    /// <summary>
    /// 单个刷怪点的**净空半径**：这个圆里不能有墙/柱/中央构筑。
    /// 取 1.2 米 —— 比一只怪的身躯半径大一点，免得刷出来卡在柱子里。
    /// </summary>
    const float 点净空半径 = 1.2f;

    /// <summary>
    /// 净空校验时的竖向采样高度（**相对地板**）。
    ///
    /// 【为什么从 0.6 起、不从 0.3 起】地板本身是个厚板
    /// （实测 `地面板` 包围盒 Y 4.33~4.94，而地板上表面 = 4.94）。
    /// 采样球在 y=地+0.3、半径 1.2 时**下缘低到 -0.9，直接扎进地板里** ⇒
    /// 每个点都会被"地板"判成没净空（我第一次就是这样，16 个点全废）。
    /// 从 0.6 起 + 下面的"包围盒在上方"过滤，两个一起才稳。
    /// </summary>
    static readonly float[] 净空采样高度 = { 0.6f, 1.1f, 1.8f };

    /// <summary>
    /// **不许生成在墙外**（用户 2026-10-01 明确要求）。
    ///
    /// 判定：从该点朝**塔心**打一条射线，如果中途撞到墙/柱/中央构筑 → 在墙外（或贴着障碍）。
    /// 为什么用"朝心打"而不是"朝外打"：塔心一定是"里面"，
    /// 朝心通畅就说明自己和塔心之间没有障碍 —— 这正是"在同一个厅里"的定义。
    /// 朝外打的话，廊/顶盖那些外围结构会误伤。
    /// </summary>
    static bool 在墙内(Vector3 点, float 地面高度)
    {
        var 采样 = new Vector3(点.x, 地面高度 + 1.0f, 点.z);
        var 心 = new Vector3(0f, 采样.y, 0f);
        Vector3 方向 = 心 - 采样;
        float 距 = 方向.magnitude;
        if (距 < 0.5f) return true;                       // 就站在塔心附近

        var 命中 = Physics.RaycastAll(采样, 方向 / 距, 距,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var h in 命中)
        {
            var c = h.collider;
            if (c == null) continue;
            if (c.GetComponentInParent<PlayerVitals>() != null) continue;
            if (c.GetComponentInParent<NpcInstance>() != null) continue;
            if (是刷怪区的东西(c)) continue;                // 别被自己挡了
            return false;                                  // 到塔心之间有障碍 → 判为墙外/贴障碍
        }
        return true;
    }

    /// <summary>这个碰撞体是不是刷怪区自己生成的物件</summary>
    static bool 是刷怪区的东西(Collider c)
    {
        var t = c.transform;
        while (t != null)
        {
            if (t.name == 刷怪区根名) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>这个点在给定高度上有没有净空（用 OverlapSphere 查有没有障碍）</summary>
    static bool 有净空(Vector3 点, float 地面高度)
    {
        foreach (var 高 in 净空采样高度)
        {
            var 球心 = new Vector3(点.x, 地面高度 + 高, 点.z);
            var 命中 = Physics.OverlapSphere(球心, 点净空半径,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var c in 命中)
            {
                if (c == null) continue;
                if (c.GetComponentInParent<PlayerVitals>() != null) continue;   // 玩家不算障碍
                if (c.GetComponentInParent<NpcInstance>() != null) continue;    // NPC 不算障碍
                if (是刷怪区的东西(c)) continue;                                 // 别被自己挡了

                // ★ 只把"**确实伸到采样高度之上**的东西"算障碍。
                //   地板、台阶这类**完全在脚下**的板子，其包围盒顶面还低于球心，
                //   但球的下缘会碰到它 —— 那不是障碍，是地面本身。
                if (c.bounds.max.y <= 球心.y) continue;

                return false;                              // 撞到墙/柱/中央构筑
            }
        }
        return true;
    }

    [MenuItem("修仙/镇妖塔/装配镇妖塔场景")]
    [MenuItem("Cultivation/Tower/Build Tower Scene")]
    public static void 装配()
    {
        // 先确保打开的是镇妖塔场景 —— 这个 builder 只对它有意义上
        var 当前 = EditorSceneManager.GetActiveScene();
        if (当前.path != 场景路径)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(场景路径, OpenSceneMode.Single);
        }

        var 报告 = new System.Text.StringBuilder();
        清理旧节点(报告);
        建刷怪点(报告);
        var 控 = 建塔控(报告);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("[镇妖塔] 装配完成：\n" + 报告);
        Selection.activeGameObject = 控 != null ? 控.gameObject : null;
    }

    /// <summary>只读体检：这个场景装配好了没有 + **曲线数值自检**</summary>
    [MenuItem("修仙/镇妖塔/体检（只报告）")]
    public static void 体检()
    {
        var 报告 = new System.Text.StringBuilder();
        var 区 = Object.FindObjectsOfType<SpawnZone>();
        var 点 = Object.FindObjectsOfType<SpawnPoint>();
        var 控 = Object.FindObjectOfType<TowerController>();
        var 界面 = Object.FindObjectOfType<TowerUI>();

        报告.AppendLine("镇妖塔场景体检（" + SceneManager.GetActiveScene().name + "）");
        报告.AppendLine("  刷怪区 SpawnZone : " + 区.Length);
        报告.AppendLine("  刷怪点 SpawnPoint: " + 点.Length);
        报告.AppendLine("  塔控 TowerController: " + (控 != null ? "有（第 " + 控.当前层 + " 层）" : "★缺"));
        报告.AppendLine("  塔界面 TowerUI     : " + (界面 != null ? "有" : "★缺"));
        报告.AppendLine("  层表 TowerFloorTable: " + (TowerFloorTable库.取() != null ? "有" : "★缺"));
        报告.AppendLine("  等级补正表 NpcLevelScale: " + (NpcLevelScale库.取() != null ? "有" : "★缺"));

        报告.AppendLine();
        报告.Append(曲线自检());
        Debug.Log(报告.ToString());
    }

    /// <summary>
    /// **等级补正的数值自检**（只读）。
    ///
    /// 补正曲线出过两次错，而且**两次都不报任何错**，只是数值悄悄不对：
    /// 一次把档内因子方向写反（等级越高越弱）、一次漏了常数因子（1 级 = ×0.679）。
    /// 光看代码看不出来，所以把判据固化成断言。
    ///
    /// 三条判据：
    ///   ① 1 级必须正好是基准（倍数 == 1）—— 否则所有怪的基准值都被整体缩放
    ///   ② 相邻大境界起点之比必须 == 每大境界倍率（默认 25）—— 这就是「质变台阶」
    ///   ③ 曲线单调递增 —— 否则「升级反而变弱」
    /// </summary>
    public static string 曲线自检()
    {
        var sb = new System.Text.StringBuilder();
        var 表 = NpcLevelScale库.取();
        if (表 == null) return "★曲线自检：拿不到 NpcLevelScale，跳过\n";

        sb.AppendLine("等级补正 · 曲线自检（" + 表.曲线摘要() + "）");

        // ① 1 级 = 基准
        float m1; float a1;
        表.取倍率(1, out m1, out a1);
        bool ok1 = Mathf.Abs(m1 - 1f) < 0.001f;
        sb.AppendLine("  ① 1 级 = ×1　　　　: " + m1.ToString("0.0000") + (ok1 ? "  ✓" : "  ★错（基准被整体缩放了）"));

        // ② 台阶 = 每大境界倍率（比较**档内涨满之后跨到下一档**，即 10→11、20→21…）
        //
        //   【为什么不是比较 1→11】1 级是档内曲线的**起点**，10 级是**终点**，
        //   两者之间本来就含了档内那一段涨（×1.47），所以 1→11 是 ×17 而不是 ×25。
        //   「台阶」的定义必须是「上一档涨满 → 下一档起点」。
        //   一开始这里比的是 1→11，于是一直误报 ★错误（曲线其实是好的）。
        float 每档 = 表.每大境界倍率;
        int 步长 = Mathf.Max(1, LevelCurve.每档级数);
        bool ok2 = true;
        var 明细 = new System.Text.StringBuilder();
        for (int 档 = 1; 档 < 表.档数; 档++)
        {
            int A = NpcLevelScale.大境界到起点等级(档) + 步长 - 1;   // 本档最后一集（涨满）
            int B = NpcLevelScale.大境界到起点等级(档 + 1);          // 下一档起点
            float a; float _a; 表.取倍率(A, out a, out _a);
            float b; float _b; 表.取倍率(B, out b, out _b);
            float r = a > 0f ? b / a : 0f;
            bool ok = Mathf.Abs(r - 每档) / Mathf.Max(1f, 每档) < 0.02f;
            if (!ok) ok2 = false;
            明细.Append("    ").Append(A).Append("→").Append(B).Append(": ×")
                 .Append(r.ToString("0.000")).Append(ok ? "  ✓" : "  ★").AppendLine();
        }
        sb.AppendLine("  ② 台阶倍率 = ×" + 每档.ToString("0.##") + " : " + (ok2 ? "全部正确  ✓" : "★有错")
                      + "（上一档涨满 → 下一档起点）");
        sb.Append(明细);

        // ②b 档内成长（1→10 级）
        float 档首; float _s; 表.取倍率(1, out 档首, out _s);
        float 档末; float _e; 表.取倍率(Mathf.Max(1, 步长), out 档末, out _e);
        sb.AppendLine("  ②b 档内 1→" + 步长 + " 级成长 : ×"
                      + (档首 > 0f ? (档末 / 档首) : 0f).ToString("0.000")
                      + "（档内平滑段）");

        // ③ 单调递增
        bool ok3 = true;
        float 上 = -1f;
        for (int lv = 1; lv <= LevelCurve.等级上限; lv++)
        {
            float m; float _m; 表.取倍率(lv, out m, out _m);
            if (上 > 0f && m < 上) { ok3 = false; break; }
            上 = m;
        }
        sb.AppendLine("  ③ 曲线单调递增　　: " + (ok3 ? "✓" : "★错（升级反而变弱）"));

        sb.AppendLine("  结论: " + (ok1 && ok2 && ok3 ? "✓ 曲线正常" : "★曲线有问题，见上面标★的项"));
        return sb.ToString();
    }

    // ============================================================ 清理

    static void 清理旧节点(System.Text.StringBuilder 报告)
    {
        int 删 = 0;
        foreach (var 名 in new[] { 刷怪区名, 塔控名 })
        {
            // 用根节点查找而不是 GameObject.Find —— 后者找不到 inactive 的
            var 根们 = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < 根们.Length; i++)
            {
                if (根们[i] == null || 根们[i].name != 名) continue;
                Object.DestroyImmediate(根们[i]);
                删++;
            }
        }
        if (删 > 0) 报告.AppendLine("  清理：删掉上次生成的 " + 删 + " 个节点");
    }

    // ============================================================ 刷怪点

    static void 建刷怪点(System.Text.StringBuilder 报告)
    {
        var 组 = new GameObject(刷怪区名);
        组.transform.position = Vector3.zero;

        // ★ 先拿到"人站的那一层"当基准 —— 见 取地面 的说明。
        //   这一步是整个修复的关键：塔是立体的，"最高的命中"是塔顶不是地板。
        bool 有基准 = 取玩家立足高度(out float 基准高度);
        if (有基准)
            报告.AppendLine("  参考高度（玩家立足层）：y = " + 基准高度.ToString("F2"));
        else
            报告.AppendLine("  ★找不到玩家，无法确定立足层 —— 退化成「取最低的水平面」");

        int 成功 = 0, 打不到地板 = 0, 在墙外 = 0, 没净空 = 0;
        for (int c = 0; c < 圈半径.Length; c++)
        {
            for (int i = 0; i < 每圈点数; i++)
            {
                float a = (i + (c * 0.5f)) / 每圈点数 * Mathf.PI * 2f;   // 两圈错开半格
                float x = Mathf.Cos(a) * 圈半径[c];
                float z = Mathf.Sin(a) * 圈半径[c];
                var 候选 = new Vector3(x, 0f, z);

                // ① 先找地板（用玩家立足层当基准，见 取地面 的说明）
                if (!取地面(new Vector3(x, 60f, z), 基准高度, out float y))
                {
                    打不到地板++;
                    continue;
                }

                // ② 必须在墙内（用户 2026-10-01 要求：不要生成到墙板外）
                if (!在墙内(候选, y))
                {
                    在墙外++;
                    continue;
                }

                // ③ 周围得有净空（别卡在柱子里 / 中央构筑里）
                if (!有净空(候选, y))
                {
                    没净空++;
                    continue;
                }

                var go = new GameObject(刷怪点名 + "_" + (c + 1) + "_" + (i + 1).ToString("D2"));
                go.transform.SetParent(组.transform, false);
                // 朝向塔心：刷出来的怪一开始就面朝中间（玩家大概在那边）
                var 朝心 = new Vector3(-x, 0f, -z);
                go.transform.position = new Vector3(x, y, z);
                go.transform.rotation = 朝心.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(朝心.normalized, Vector3.up)
                    : Quaternion.identity;
                go.AddComponent<SpawnPoint>();
                成功++;
            }
        }

        var 区 = new GameObject("塔刷怪区");
        区.transform.SetParent(组.transform, false);
        区.transform.position = Vector3.zero;
        var z2 = 区.AddComponent<SpawnZone>();
        z2.刷怪父节点 = 组.transform;
        z2.自动开始 = false;                 // 由塔控按层开
        z2.重生延迟 = 10f;                   // 用户定：全清后 10 秒重刷
        z2.半径 = 0f;                        // 不是"留白标记"，不画大圈
        z2.无点散开半径 = 14f;

        报告.AppendLine("  刷怪点：" + 成功 + " 个"
                        + "（打不到地板 " + 打不到地板
                        + "｜在墙外 " + 在墙外
                        + "｜没净空 " + 没净空 + "）");
        if (成功 == 0) 报告.AppendLine("  ★一个都没摆成 —— 检查基准高度 / 墙内判定");
        报告.AppendLine("  刷怪区：1 片（重生延迟 " + z2.重生延迟 + " 秒，自动开始=关）");
    }

    /// <summary>
    /// **找"地板"的高度。**
    ///
    /// ## 为什么不能"取最高的命中"（用户 2026-10-01 实测报的）
    ///
    /// 原来是从 y=60 往下打射线、**取最高的命中** —— 思路是"最上面那层就是地面"。
    /// 在塔里**完全错了**，因为塔是立体的，同一个 XZ 上有多层：
    ///
    /// <code>
    /// 半径 11m 处实测命中 2 个：
    ///     y = 35.66  顶盖（父=廊）     ← "最高命中"选了这个 ✗
    ///     y =  4.94  地面板（父=地面）  ← 这才是地板
    /// 半径 18m 处命中 4 个：
    ///     y = 35.66  顶盖（父=廊）
    ///     y = 17.43  梯步_49（父=楼梯）
    ///     y =  4.94  地面板（父=地面）
    /// </code>
    ///
    /// 于是 **16 个刷怪点全跑到 35.71 米的塔顶**（模型是塔顶，人却在塔底）——
    /// 而且这种错在**俯视相机下完全看不出来**。
    ///
    /// ## 现在怎么判
    ///
    /// **用玩家实际站的那一层当基准**（`参考高度`）——
    /// "地板"的定义就是"人能站着的那层"，玩家在塔里就站在它上面，
    /// 拿它当基准比任何几何猜测都可靠。然后：
    ///
    /// 1. 只接受离基准 **±<see cref="允许高差"/>** 以内的命中（排掉塔顶、吊顶、上层楼板）
    /// 2. 只接受**近水平**的面（法线朝上）—— 排掉楼梯侧面、栏杆、斜屋顶
    /// 3. 在这批里取**最高**的（同一层有厚板/薄装饰时贴上面那层）
    /// </summary>
    /// <param name="上方点">从这儿往下打（给个高处即可）</param>
    /// <param name="参考高度">"地板大概在这附近"——用玩家的立足高度</param>
    /// <param name="y">找到的地板高度</param>
    static bool 取地面(Vector3 上方点, float 参考高度, out float y)
    {
        y = 0f;
        var 命中 = Physics.RaycastAll(上方点, Vector3.down, 200f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (命中 == null || 命中.Length == 0) return false;

        // 地板一定是"脚面高度"。留一点余量：
        // 地面板有厚度（实测 4.33~4.94），玩家脚底 ≈4.99，都在 1 米内
        const float 允许高差 = 1.5f;
        // 法线朝上到什么程度算"能站"（0.7 ≈ 45°）—— 楼梯的台阶面也能过，楼梯**侧面**过不了
        const float 最小朝上度 = 0.7f;

        float 最好 = float.MinValue;
        bool 有 = false;
        foreach (var h in 命中)
        {
            var c = h.collider;
            if (c == null) continue;
            if (c.GetComponentInParent<PlayerVitals>() != null) continue;   // 玩家不算地板
            if (c.GetComponentInParent<NpcInstance>() != null) continue;    // NPC 不算地板
            if (Mathf.Abs(h.point.y - 参考高度) > 允许高差) continue;        // ★ 排掉塔顶/上层
            if (Vector3.Dot(h.normal, Vector3.up) < 最小朝上度) continue;    // ★ 排掉墙/侧面
            if (h.point.y > 最好) { 最好 = h.point.y; 有 = true; }
        }
        if (!有) return false;
        y = 最好 + 0.05f;
        return true;
    }

    /// <summary>
    /// 参考高度 = **玩家实际站的那一层**。
    ///
    /// 找不到玩家时返回 0 并让调用方据 <c>y==0 视为未知</c>（会退化成"最低的水平面"）。
    /// 这里**故意不猜一个魔数** —— 塔的地面在 y≈4.94，写死它换台机器/改场景就崩了。
    /// </summary>
    static bool 取玩家立足高度(out float y)
    {
        y = 0f;
        var 玩家 = Object.FindObjectOfType<PlayerVitals>();
        if (玩家 == null) return false;
        y = 玩家.transform.position.y;
        return true;
    }

    // ============================================================ 塔控

    static TowerController 建塔控(System.Text.StringBuilder 报告)
    {
        var go = new GameObject(塔控名);
        var 控 = go.AddComponent<TowerController>();
        控.层表 = TowerFloorTable库.取();
        控.补正表 = NpcLevelScale库.取();
        控.自动存档 = true;

        var 区 = Object.FindObjectsOfType<SpawnZone>();
        控.刷怪区 = new List<SpawnZone>(区);

        // 界面单独挂一个节点（和塔控同层），好单独调
        var 界面节点 = new GameObject("塔界面");
        界面节点.transform.SetParent(go.transform, false);
        var 界面 = 界面节点.AddComponent<TowerUI>();
        界面.塔 = 控;

        报告.AppendLine("  塔控：" + 塔控名 + "（刷怪区 " + 控.刷怪区.Count + " 片）");
        报告.AppendLine("  塔界面：" + 界面节点.name);
        报告.AppendLine("  层表：" + (控.层表 != null
            ? 控.层表.有效总层数 + " 层，每 " + 控.层表.每多少层一级 + " 层一级" : "★缺（先跑「修仙/从配置表生成资产」）"));
        报告.AppendLine("  补正表：" + (控.补正表 != null ? 控.补正表.曲线摘要() : "★缺"));

        return 控;
    }
}
