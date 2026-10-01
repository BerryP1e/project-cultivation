using System.Collections;
using UnityEngine;

/// <summary>
/// **主线第一幕：开场（黑幕白字 → 起名 → 出生在主角小屋前）**。
///
/// 策划原文（`docs/design/主线剧情.md`）：
/// ```
/// 这是一个漫长的故事
/// 讲述了一个少年如何一步步争渡成仙
/// 那个少年的名字叫做：
/// ```
/// → 玩家起名（回车确认，5 个汉字以内）→ 等 1 秒 → **全黑、字幕清空** → `在古古镇上……`
/// → **场景切到古古镇（= village）**，主角出生在「主角小屋」前方。
///
/// 用法：把本组件挂到**古古镇场景**里任意一个常驻物件上（放个空物体叫「主线开场」就行），
/// 进游戏就会自动演这一段。**出生点**留空会自动找场景里名为 `主角小屋` 的物件。
///
/// 备注：真正的主线任务表还没做，所以这里只演"开场"这一段；等主线阶段表就位，
/// 在 <see cref="开场结束再接任务id"/> 里填上第一幕的任务 id，它就会在演完时自动接取。
/// </summary>
[DisallowMultipleComponent]
public class 主线开场 : MonoBehaviour
{
    [Header("出生点")]
    [Tooltip("出生点物件名（留空 = 找「主角小屋」）")]
    public string 出生点物件名 = "主角小屋";

    [Tooltip("相对出生点的偏移（默认往物件前方 3 米、略抬高免得陷地）")]
    public Vector3 出生偏移 = new Vector3(0f, 0.1f, -3f);

    [Tooltip("出生朝向：面向这个物件（勾上 = 面向主角小屋）")]
    public bool 面向出生点物件 = true;

    [Header("开关")]
    [Tooltip("进游戏就自动演（关掉 = 只能手动调 开始开场()）")]
    public bool 进游戏就播 = true;

    [Tooltip("演完自动接哪个任务（留空 = 不接）。主线阶段表做好后填这里")]
    public string 开场结束再接任务id = "";

    [Tooltip("每次黑幕停留的额外时间（秒）")]
    public float 行间额外停顿 = 0f;

    /// <summary>
    /// **本局是否已经演过开场**。
    ///
    /// ★★ 必须是 static（用户 2026-09-27 报的 bug）：
    ///   原来是实例字段 `bool 演过`，而 `LoadSceneMode.Single` **每次进古古镇都会把这个组件
    ///   重建一遍** → `演过` 归零 → **每次从别的场景回到古古镇都重演一遍开场**
    ///   （三句黑幕 + 起名界面）。用户从宗门回古古镇就撞上了。
    ///
    ///   开场的语义是"**新游戏开局演一次**"，不是"每次进古古镇都演"。
    ///   用 static 让它跨场景存活；`RuntimeInitializeOnLoadMethod` 保证重进 Play 时归零。
    /// </summary>
    static bool 演过;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置静态() { 演过 = false; }

    void Start()
    {
        if (演过) return;                       // 本局已经演过（或正在演），别再演
        if (!进游戏就播) return;

        // ★ 只有**新开局**才演开场。判据：**存档里没有任何主线进度**。
        //
        //   为什么用这个判据（踩过的坑 2026-09-27，两次）：
        //     ① 原来没有任何判据 → `演过` 又是实例字段 → **每次回古古镇都重演开场** ✗
        //     ② 我改成判 `SaveSystem.当前存档 != null` → 但主菜单点「新游戏」时
        //        **也会先 建新档() 再赋给 当前存档**，于是**新开局的开场也被挡掉了** ✗
        //        （用户报："开新存档又没有开局简介的黑幕了"）
        //     ③ 改成判 `SaveSystem.本局是新开局`（建新档时置真）→ 但那个静态字段
        //        在**编辑器里按 Play 时会被 RuntimeInitializeOnLoadMethod 归零**，
        //        而主菜单是在 Play **里面**点的，于是又演不出来 ✗
        //
        //   现在的判据同时满足两边：
        //     · 新开局：存档是空档（`建新档` 会 `面板内容全清` → 任务进度为空）→ 演 ✓
        //     · 读档：存档里有进度 → 不演 ✓
        //     · 从别的场景回古古镇：`演过` 已经是 true（static）→ 不演 ✓
        if (SaveSystem.当前存档 != null && !string.IsNullOrEmpty(SaveSystem.当前存档.任务进度))
        {
            Debug.Log("[主线] 开场：存档里已有主线进度（读档/切场景回来）→ 跳过开场演出");
            演过 = true;
            return;
        }

        演过 = true;
        StartCoroutine(开始开场());
    }

    /// <summary>整段开场。外部也可以手动调（例如从主菜单"新游戏"进来时）</summary>
    public IEnumerator 开始开场()
    {
        // 1) 黑幕白字（逐字打字、长按左键加速，都在 黑幕字幕 里）
        yield return 黑幕字幕.说("这是一个漫长的故事");
        yield return 黑幕字幕.说("讲述了一个少年如何一步步争渡成仙");
        yield return 黑幕字幕.说("那个少年的名字叫做：");

        // 2) 起名（回车确认，≤5 个汉字）
        string 主角名 = null;
        yield return 起名界面.取名("那个少年的名字叫做：", s => 主角名 = s);
        if (string.IsNullOrEmpty(主角名)) 主角名 = 起名界面.当前名字;
        Debug.Log("[主线] 主角名：" + 主角名);

        // 3) 等 1 秒 → 字幕清空（保持全黑）
        yield return new WaitForSeconds(1f);
        黑幕字幕.清空();

        // 4) 出生到「主角小屋」前（此时还是全黑，看不到瞬移）
        把玩家放到出生点();

        // 5) 打下一句，然后收幕
        //    ★ 这里要带上**玩家刚起的名字**（用户 2026-09-27：原来这一句完全没用上名字）
        string 名字 = string.IsNullOrEmpty(主角名) ? "少年" : 主角名;
        yield return 黑幕字幕.说("这个少年的名字叫做「" + 名字 + "」");
        yield return 黑幕字幕.说("在古古镇上……");
        if (行间额外停顿 > 0f) yield return new WaitForSeconds(行间额外停顿);
        黑幕字幕.收幕();

        // 6) 接第一幕的任务（阶段表就位后填 id）
        if (!string.IsNullOrEmpty(开场结束再接任务id))
        {
            var 任务 = 任务管理器.实例 != null ? 任务管理器.实例 : Object.FindObjectOfType<任务管理器>();
            if (任务 != null) 任务.接取(开场结束再接任务id);
            else Debug.LogWarning("[主线] 场景里没有任务管理器，没法接 " + 开场结束再接任务id);
        }
        Debug.Log("[主线] 第一幕开场演出结束");
    }

    void 把玩家放到出生点()
    {
        var 玩家 = 物品使用器.取玩家物体();
        if (玩家 == null) { Debug.LogWarning("[主线] 场景里找不到玩家"); return; }

        Transform 点 = null;
        if (!string.IsNullOrEmpty(出生点物件名))
        {
            var go = GameObject.Find(出生点物件名);
            if (go != null) 点 = go.transform;
        }
        if (点 == null) { Debug.LogWarning("[主线] 找不到出生点物件「" + 出生点物件名 + "」，玩家留在原地"); return; }

        Vector3 位 = 点.position + 出生偏移;
        var cc = 玩家.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;          // 直接改 position 时先关掉角色控制器
        玩家.transform.position = 位;
        if (面向出生点物件)
        {
            var 朝 = 点.position - 位; 朝.y = 0f;
            if (朝.sqrMagnitude > 0.0001f) 玩家.transform.rotation = Quaternion.LookRotation(朝.normalized, Vector3.up);
        }
        if (cc != null) cc.enabled = true;
        Debug.Log("[主线] 玩家出生在「" + 出生点物件名 + "」前：" + 位.ToString("F1"));
    }

    // ---- ASCII 别名 ----
    public IEnumerator PlayOpening() => 开始开场();
}
