using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **一种"能摆在洞府里的东西"的静态定义**。
///
/// 用户 2026-10-01 追加的需求：
/// > 「让**木桩**也像灵田一样是**存在背包里、可以放置**的吧，注意这些东西都是**要能够存档**的哦。」
/// > （追问后确定：木桩要是**能打的练功桩**）
///
/// 所以摆放这件事从"只会摆灵田"泛化成"按定义摆任意物件"。一个定义回答四个问题：
///   · 它是什么（`id` / `名`）、**背包里用哪个道具**摆（`道具id`）；
///   · 它**占多大**（`占地边长` + `占用边距`）—— 用于重叠判定与吸附；
///   · 它**长什么样**（`模型路径`；留空 = 由各自的代码程序化生成，灵田就是这种）；
///   · 摆下来之后**归谁管**（`是灵田` = 走灵田那套品阶/作物的状态机；否则是"摆设"）。
///
/// 【为什么灵田也放进这张表】不放进来的话，"木桩不能插进灵田里"这种**跨种类互斥**
/// 就没地方判 —— 两套代码各判各的，必然出现"木桩能插进田里、田却盖不住木桩"。
/// </summary>
[System.Serializable]
public class 摆放物定义
{
    /// <summary>唯一 id（存档里存的就是它）</summary>
    public string id = "";

    /// <summary>显示名</summary>
    public string 名 = "";

    /// <summary>**背包里用哪个道具把它摆下来**（物品表里的 id）</summary>
    public string 道具id = "";

    /// <summary>占地方盒的边长（米）。重叠判定与吸附都按它算</summary>
    public float 占地边长 = 1f;

    /// <summary>它周围这一圈也算被占（米），给别的物件留过道</summary>
    public float 占用边距 = 0.3f;

    /// <summary>占地半边长（含边距）—— 重叠判定用的就是它</summary>
    public float 占用半边长 => 占地边长 * 0.5f + 占用边距;

    /// <summary>
    /// 模型资源路径（`Resources.Load` 用，例：`摆设/练功木桩`）。
    /// **留空 = 这个物件的外观由它自己的代码生成**（灵田的土床就是程序化网格）。
    /// </summary>
    public string 模型路径 = "";

    /// <summary>
    /// 摆下来之后**同时生成的那个 NPC 定义 id**（`NPC表.csv` 的 id，例：`monster_wooden_dummy`）。
    /// 留空 = 纯摆件。填了 = 摆下来就是个真 NPC（带 `NpcInstance`、血条、受击、AI 与否看定义）。
    /// </summary>
    public string npc定义id = "";

    /// <summary>true = 走灵田那套状态（品阶 / 作物 / 生长），也就是"灵田地块"</summary>
    public bool 是灵田 = false;
}

/// <summary>
/// **摆放物库**（代码内置，只读）。理由和 <see cref="灵植库"/> 一样：
/// 这套东西先跑通，以后要挪到 CSV 时只改这一个类。
/// </summary>
public static class 摆放物库
{
    static List<摆放物定义> 缓存;

    public static IReadOnlyList<摆放物定义> 全部
    {
        get { 确保(); return 缓存; }
    }

    static void 确保()
    {
        if (缓存 != null) return;
        缓存 = new List<摆放物定义>
        {
            // 灵田地块：外观是程序化土床（见 灵田地块外观），状态由 灵田 管
            new 摆放物定义
            {
                id = "lingtian", 名 = "灵田地块", 道具id = 灵田.开拓令id,
                占地边长 = 灵田规格.床边长, 占用边距 = 灵田规格.占用边距,
                模型路径 = "", 是灵田 = true,
            },

            // 练功木桩：外观直接实例化 prefab，**一实例化就自带 NpcInstance**
            // （NPC表.csv 的 monster_wooden_dummy：中立 / 气血 10000 / 死亡后立即重生 ⇒ 打空自动满血）
            new 摆放物定义
            {
                id = "muzhuang", 名 = "练功木桩", 道具id = "item_muzhuang",
                占地边长 = 0.6f, 占用边距 = 0.3f,
                模型路径 = "摆设/练功木桩", npc定义id = "monster_wooden_dummy",
            },
        };
    }

    public static 摆放物定义 取(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        确保();
        foreach (var d in 缓存) if (d.id == id) return d;
        return null;
    }

    /// <summary>这个道具是用来摆什么的（背包「使用」时反查）</summary>
    public static 摆放物定义 按道具(string 道具id)
    {
        if (string.IsNullOrEmpty(道具id)) return null;
        确保();
        foreach (var d in 缓存) if (d.道具id == 道具id) return d;
        return null;
    }
}

/// <summary>
/// **一件摆下来的"摆设"**（木桩这类：占地 + 位置 + 朝向，没有自己的复杂状态）。
///
/// ⚠️ 本类**就是存档类型**（`SaveData.摆设` 是 `List&lt;摆设状态&gt;`）——
/// 字段必须都是 `JsonUtility` 支持的（string / float / int / Vector3），
/// **别加 Dictionary、接口、属性**，否则存档静默丢失。
/// </summary>
[System.Serializable]
public class 摆设状态
{
    /// <summary>摆放物 id（`摆放物库` 的 id）</summary>
    public string 类型id = "";

    public Vector3 位置 = Vector3.zero;

    /// <summary>朝向档 0~7（每档 45°）</summary>
    public int 朝向档 = 0;

    public float 朝向角度 => 朝向档 * 灵田规格.每档角度;

    public 摆放物定义 定义 => 摆放物库.取(类型id);

    public string 名
    {
        get { var d = 定义; return d != null ? d.名 : 类型id; }
    }
}

/// <summary>
/// **摆放校验** —— "这个位置能不能放下这件东西"。
///
/// 【为什么单独抽一个静态类】要判的东西有**四**类，而且**必须跨种类互相判**：
///   1. 场景允不允许摆（`允许摆放的场景`，用户要求"不在洞府场景无法使用开拓令"）；
///   2. 底下是不是够平的地面；
///   3. **有没有压住别的灵田**；
///   4. **有没有压住别的摆设**（木桩插进田里、田盖住木桩，都得拦住）；
///   5. 有没有插进场景里的树 / 岩石 / 建筑。
/// 灵田和摆设两套代码各判各的，早晚出现"木桩能插进田里、田却盖不住木桩"这种不对称的 bug，
/// 所以判定**只有这一份**。
/// </summary>
public static class 摆放校验
{
    /// <summary>只有在这些场景里才能摆放（★ 改名场景时记得改这里）</summary>
    public static readonly string[] 允许摆放的场景 = { "3C_Testbed" };

    public static bool 当前场景可摆放
    {
        get
        {
            var 场景 = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!场景.IsValid() || !场景.isLoaded) return false;
            foreach (var n in 允许摆放的场景) if (n == 场景.name) return true;
            return false;
        }
    }

    /// <summary>地面最大坡度（度）</summary>
    public const float 最大地面坡度 = 15f;

    /// <summary>地面往下探多远算探到地（米）</summary>
    public const float 探地距离 = 6f;

    /// <summary>
    /// 这件东西能不能放在这里。
    /// <paramref name="忽略灵田"/> / <paramref name="忽略摆设"/> 是"挪动自己"时跳过自己那一条。
    /// </summary>
    public static bool 位置可用(摆放物定义 定义, Vector3 位置, int 朝向档,
                                out string 原因, out float 地面高,
                                int 忽略灵田 = -1, int 忽略摆设 = -1)
    {
        原因 = "";
        地面高 = 位置.y;

        if (定义 == null) { 原因 = "没有这种东西的定义"; return false; }
        if (!当前场景可摆放) { 原因 = "只能在个人洞府里摆放"; return false; }

        // ---- 地面：往下探，要有地、而且够平 ----
        //
        // ⚠️ 【不能用单发 Raycast】已经摆好的**摆设是有碰撞体的**（木桩要能被打，就必须有），
        //    单发射线会先打到木桩身上 ⇒ 拿到的"地面高度"是木桩顶，于是报出
        //    「这里和地面差太多」这种**牛头不对马嘴的原因**（实测踩到：在木桩上摆灵田）。
        //    所以要把射线上的命中全取出来，**挑最下面那个真地面**（跳过摆设自己和玩家）。
        RaycastHit 地 = default;
        bool 有地 = false;
        {
            var 命中 = Physics.RaycastAll(位置 + Vector3.up * 3f, Vector3.down, 探地距离 + 3f,
                                          ~0, QueryTriggerInteraction.Ignore);
            float 最低 = float.MaxValue;
            foreach (var h in 命中)
            {
                if (h.collider == null) continue;
                if (h.collider is CharacterController) continue;
                if (h.collider.GetComponentInParent<摆设物件>() != null) continue;   // 摆设不是地面
                if (h.point.y < 最低) { 最低 = h.point.y; 地 = h; 有地 = true; }
            }
        }
        if (!有地)
        {
            原因 = "这里没有地面";
            return false;
        }
        if (Vector3.Angle(地.normal, Vector3.up) > 最大地面坡度)
        {
            原因 = "这里地面太陡（" + Vector3.Angle(地.normal, Vector3.up).ToString("0") + "°）";
            return false;
        }
        if (Mathf.Abs(地.point.y - 位置.y) > 0.6f)
        {
            原因 = "这里和地面差太多";
            return false;
        }
        地面高 = 地.point.y;

        var 我 = new Vector2(位置.x, 位置.z);

        // ---- 压住别的灵田？----
        var 田 = 灵田.取();
        if (田 != null)
        {
            var 地块 = 田.所有地块;
            for (int i = 0; i < 地块.Count; i++)
            {
                if (i == 忽略灵田) continue;
                var b = 地块[i];
                if (b == null) continue;
                if (灵田规格.压住(我, 朝向档, 定义.占用半边长,
                                  new Vector2(b.位置.x, b.位置.z), b.朝向档, 灵田规格.占用半边长))
                {
                    原因 = "压住第 " + (i + 1) + " 块灵田了";
                    return false;
                }
            }
        }

        // ---- 压住别的摆设？----
        var 摆 = 摆设.取();
        if (摆 != null)
        {
            var 全部 = 摆.所有摆设;
            for (int i = 0; i < 全部.Count; i++)
            {
                if (i == 忽略摆设) continue;
                var s = 全部[i];
                if (s == null) continue;
                var 他 = s.定义;
                if (灵田规格.压住(我, 朝向档, 定义.占用半边长,
                                  new Vector2(s.位置.x, s.位置.z), s.朝向档,
                                  他 != null ? 他.占用半边长 : 0.3f))
                {
                    原因 = "压住第 " + (i + 1) + " 个" + s.名 + "了";
                    return false;
                }
            }
        }

        // ---- 插进场景里的东西？----
        // 【怎么区分"地面"和"障碍"】不用图层：**地面就是刚才那根向下射线打到的那个碰撞体**，
        // 把它（和玩家自己）排除掉，剩下的算障碍。
        // ⚠️ 这里用的是**物件本身的半边长、不含边距**：占用边距是给别的物件留的过道，
        //    不该拿来判定场景物件 —— 否则旁边一根细木桩就能让整块地摆不下去（实测踩过）。
        float 半 = 定义.占地边长 * 0.5f;
        var 中心 = new Vector3(位置.x, 地.point.y + 0.5f, 位置.z);
        var 盒 = new Vector3(半, 0.5f, 半);
        var 撞 = Physics.OverlapBox(中心, 盒, Quaternion.Euler(0f, 灵田规格.档转角度(朝向档), 0f),
                                    ~0, QueryTriggerInteraction.Ignore);
        foreach (var c in 撞)
        {
            if (c == null || c == 地.collider) continue;
            if (c is CharacterController) continue;                       // 玩家自己
            if (c.transform.IsChildOf(田 != null ? 田.transform : null)) continue;
            if (摆 != null && c.transform.IsChildOf(摆.transform)) continue;
            // 已经摆好的**摆设自己**（木桩等）也会被 OverlapBox 撞到 —— 但那是"别的摆设"，
            // 上面第 4 条已经按占用盒判过了，这里再拦一次会导致"贴着摆"永远失败。
            if (c.GetComponentInParent<摆设物件>() != null) continue;
            原因 = "这里被「" + c.name + "」占着";
            return false;
        }

        return true;
    }
}
