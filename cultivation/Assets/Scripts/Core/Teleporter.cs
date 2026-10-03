using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 传送光圈。★ 2026-09-28 起**和 NPC / 建筑同一套交互**：
///   玩家走进交互范围 → 头顶出现「F 传送」提示 → **按 F** 才弹出「是否传送？」面板 → 确认后传送。
///
/// 一个组件同时管两种传送：
///   · **场景间**：选项里填了场景名 → `SceneManager.LoadScene`，再在新场景里找落点物体
///   · **场景内**：场景名留空 → 直接把玩家挪到本场景里的落点物体
///
/// 一个光圈可以给**多个选项**（例如镇妖塔的光圈：①进入下一层 ②出塔），
/// 选项可标记「暂未开放」→ 界面上灰掉、点了提示"尚未开放"。
///
/// 交互是怎么接上的：`Awake` 里给自己补一个 <see cref="StationInteractable"/>（类型 = 传送），
/// 剩下的"找最近 / 画提示 / 收 F 键"全由玩家身上的 <see cref="StationInteractor"/> 统一做 ——
/// 见 <see cref="StationInteractor.打开界面"/> 里的「传送」分支。
///
/// ★ 前置条件：<see cref="传送选项.场景"/> 里写的场景必须加进 **Build Settings**，否则 LoadScene 会失败。
/// ★ 落点物体必须真实存在于目标场景，且**不能和它对应的传送圈同名** ——
///   否则玩家会被放到圆圈自己身上（同一帧又踩进圈里）。详见 `docs/ai/踩坑总库.md`。
/// </summary>
[DisallowMultipleComponent]
public class Teleporter : MonoBehaviour
{
    [System.Serializable]
    public class 传送选项
    {
        [Tooltip("按钮上显示的名字，例如「出塔」")]
        public string 名称 = "传送";
        [Tooltip("目标场景名（不要带路径/扩展名）。**留空 = 本场景内传送**")]
        public string 场景 = "";
        [Tooltip("落点物体名。跨场景时在新场景里找；同场景时在本场景里找")]
        public string 落点 = "SpawnPoint";
        [Tooltip("还没做好的选项勾上 → 界面灰掉、点了只提示「尚未开放」")]
        public bool 暂未开放 = false;
    }

    [Header("选项")]
    public 传送选项[] 选项 = new 传送选项[0];

    [Header("触发")]
    [Tooltip("光圈触发半径（米）。留 0 则用物体上已有的 Trigger 碰撞体")]
    public float 半径 = 1.8f;

    [Header("交互（★ 2026-09-28 改成和 NPC / 建筑一样）")]
    [Tooltip("玩家离这么近才出现「F 传送」提示。留 0 则用 半径")]
    public float 交互距离 = 3.5f;
    [Tooltip("提示里显示的字。留空 = 只有一个选项时用选项名，多个选项时用「传送」")]
    public string 提示文字 = "";
    [Tooltip("提示框离地面多高（米）。传送圈是平的，没有渲染体高度可用")]
    public float 提示高度 = 1.6f;

    [Header("界面")]
    [Tooltip("提示标题")]
    public string 标题 = "是否传送？";
    [Tooltip("打印日志")]
    public bool 打印日志 = false;

    /// <summary>
    /// ★ 2026-09-28 用户要求的改动：**不再"走上去就弹面板"**，
    /// 而是和 NPC / 建筑一样 —— 靠近出现「F 传送」提示，**按 F 才开面板**。
    ///
    /// 为什么这样更好（顺带修掉一个老毛病）：
    ///   老的 `OnTriggerEnter` 自动弹面板 + 出圈才关，玩家被送到落点后
    ///   如果落点压在传送圈里，就会**立刻再次触发**、来回弹。
    ///   改成"按 F"之后，落点就算压在圈上也不会自己弹（见 黑幕字幕.找场景物体 的注释）。
    /// </summary>
    public bool 开面板_允许 = true;

    /// <summary>
    /// **选项被点时的外部接管**（返回 <c>true</c> = 我处理了，本组件不再自己传送）。
    ///
    /// 为什么需要它：有的传送圈是**场景机制的一部分**，它的选项要触发的是"逻辑"而不是"换个地方"。
    /// 例：镇妖塔清完怪出现的那个圈 —— 「进入下一层」= `TowerController.下一层()`、
    /// 「出塔」= `TowerController.退到塔外()`（停塔 + 记进度 + 走黑幕过场）,
    /// 这两件事都**表达不成"把玩家挪到某个落点"**。
    ///
    /// 有了这个钩子：圈本身不用知道塔的存在（它只管画界面），塔也只是"借用"这个圈的界面。
    /// 参数 = 选项下标，和 <see cref="执行"/> 收到的那个一致。
    /// </summary>
    public System.Func<int, bool> 选项接管;

    [Header("准入（可选）")]
    [Tooltip("**玩家境界等级**低于这个值就不给传送（0 = 不限）。\n" +
             "等级不够时：所有选项**置灰**、面板上写明原因（见「未开放原因」）。\n" +
             "镇妖塔就是用它卡的「炼气三层」—— 由 `塔准入.cs` 在运行时写进来，不用改场景。")]
    public int 需要等级 = 0;

    [Tooltip("等级不够时面板上写的提示。留空 = 自动拼「需要炼气 N 层」。\n" +
             "`{当前}` 会被替换成玩家当前等级。")]
    public string 未开放原因 = "";

    /// <summary>现在够不够格（需要等级 ≤ 0 = 不限）</summary>
    public bool 准入通过
    {
        get
        {
            if (需要等级 <= 0) return true;
            var 修 = Object.FindObjectOfType<PlayerCultivation>();
            return 修 != null && 修.等级 >= 需要等级;
        }
    }

    /// <summary>等级不够时给玩家看的话</summary>
    public string 取未开放原因()
    {
        if (string.IsNullOrEmpty(未开放原因)) return "需要境界等级 " + 需要等级;
        var 修 = Object.FindObjectOfType<PlayerCultivation>();
        return 未开放原因.Replace("{当前}", 修 != null ? 修.等级.ToString() : "?")
                          .Replace("{需要}", 需要等级.ToString());
    }

    /// <summary>
    /// 准入条件变了（等级升了 / 运行时改了 `需要等级`）之后调它：作废旧面板、重刷标题。
    /// **由 `塔准入.cs` 在等级变化时调**，不指望玩家重开界面。
    /// </summary>
    public void 刷新准入()
    {
        重建面板();
        刷新文本();
    }

    bool 面板开着;
    GameObject 面板;
    readonly List<Button> 按钮s = new List<Button>();
    Text 文本;

    void Awake()
    {
        // 没有 Trigger 就自己补一个球。**保留它**：别的脚本（任务触发区、死亡流程）
        // 可能还在用圈的物理边界；而且 StationInteractor 用的是距离判定，不依赖它。
        if (GetComponent<Collider>() == null && 半径 > 0.01f)
        {
            var sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 半径;
        }

        // ★ 让 StationInteractor 能发现我（"靠近出「F 传送」提示、按 F 开面板"）。
        //   Awake 早于 StationInteractor 的首次扫描，所以运行时补的标记一定来得及被看到。
        确保交互标记();
    }

    /// <summary>运行时改了 提示文字 / 交互距离 之后调这个刷新</summary>
    public void 刷新交互标记() { if (isActiveAndEnabled) 确保交互标记(); }

    /// <summary>
    /// ⚠️ 编辑期**只刷新已经存在的标记，绝不 AddComponent** ——
    ///    在 OnValidate 里加组件会往场景文件里写东西（而且要标脏场景），
    ///    对一个"只在运行时补齐"的辅助组件来说没必要。
    ///    真正需要补的场景是 Awake（进 Play 时）。
    /// </summary>
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        var 标 = GetComponent<StationInteractable>();
        if (标 != null) 刷新交互标记();
    }

    /// <summary>
    /// 挂上/刷新给 <see cref="StationInteractor"/> 用的可交互标记。
    ///
    /// 为什么用"运行时补一个 StationInteractable"而不是让美术在每个场景里手挂：
    ///   传送圈共 5 个场景 7 个，手挂必然漏（这正是「场景一致性」那类 bug 的成因）。
    ///   挂在同一个物件上、运行时保证，就永远不会漏。
    /// </summary>
    StationInteractable 确保交互标记()
    {
        var 标 = GetComponent<StationInteractable>();
        if (标 == null) 标 = gameObject.AddComponent<StationInteractable>();

        标.类型 = StationInteractable.StationKind.传送;
        标.显示名 = 取提示文字();
        标.交互距离 = 交互距离 > 0.01f ? 交互距离 : Mathf.Max(0.5f, 半径);
        标.交互键覆盖 = KeyCode.F;          // 用户要求：和 NPC 一样按 F
        标.显示靠近提示 = true;
        标.提示抬高 = 提示高度;
        标.现在可交互 = 开面板_允许;
        return 标;
    }

    /// <summary>提示里显示的字：只有一个选项时用选项名（「前往古古镇」），多个选项时用「传送」</summary>
    public string 取提示文字()
    {
        if (!string.IsNullOrEmpty(提示文字)) return 提示文字;
        if (选项 != null && 选项.Length == 1 && 选项[0] != null && !string.IsNullOrEmpty(选项[0].名称))
            return 选项[0].名称;
        return "传送";
    }

    // ---------------- 面板 ----------------

    /// <summary>开面板。★ 现在是**由 <see cref="StationInteractor"/> 按 F 调**（原来是走上去自动调）</summary>
    public void 开面板()
    {
        if (!开面板_允许) return;
        if (面板 == null) 建面板();
        面板.SetActive(true);
        面板开着 = true;
        刷新文本();
        if (打印日志) Debug.Log("[传送] 「" + name + "」弹出选项（" + 选项.Length + " 个）", this);
    }

    /// <summary>关面板</summary>
    public void 关面板()
    {
        if (面板 != null) 面板.SetActive(false);
        面板开着 = false;
    }

    /// <summary>面板开着吗（StationInteractor 据此知道要不要吞掉 F / ESC）</summary>
    public bool 面板已开 => 面板开着;

    /// <summary>
    /// **把面板作废、下次开面板时按当前选项重建**。
    ///
    /// 为什么需要：面板是**第一次 `开面板()` 时按当时的 `选项` 一次性搭好的**
    /// （按钮文字 / 灰不灰都烘在那个时刻），之后改 `选项` 不会反映到已经搭好的面板上。
    /// 镇妖塔的「进入下一层 / 出塔」是**每次清完怪由塔控写进 `选项`** 的
    /// （顶层时那一项还要变灰），所以它必须在写完之后调这个。
    ///
    /// ⚠️ **面板开着的时候不重建**：面板根正被 `StationInteractor.当前界面` 记着，
    ///    当场销毁会让"界面开着"的标记指向一个已销毁对象（见 `面板根` 那段说明）。
    ///    关着的时候销毁 + 置空，下一次 `开面板()` 就会重新搭。
    /// </summary>
    public void 重建面板()
    {
        if (面板开着) return;
        if (面板 == null) return;
        if (Application.isPlaying) Destroy(面板); else DestroyImmediate(面板);
        面板 = null;
        文本 = null;
        按钮s.Clear();
    }

    /// <summary>
    /// 面板的根物件（没建过时是 null）。
    ///
    /// ★ 必须有这个：`StationInteractor` 用 `当前界面 != null` 表示"界面开着"，
    ///   并在 `Update` 里拿它自愈 —— `if (有界面打开 && 当前界面 == null) 有界面打开 = false;`。
    ///   传送面板是**本组件自己造的**，以前不给 `StationInteractor` 这个引用，
    ///   于是那个自愈逻辑**下一帧就把"界面开着"的标记清掉**了 →
    ///   表现成「按 ESC 关不掉传送面板」（用户 2026-09-28 报的）。
    ///   把它交出去之后，标记就一直是 true，ESC 那条路才走得到。
    /// </summary>
    public GameObject 面板根 => 面板;

    void 刷新文本()
    {
        if (文本 == null) return;
        string s = 标题;
        for (int i = 0; i < 选项.Length; i++)
            if (选项[i] != null && 选项[i].暂未开放) s += "\n（「" + 选项[i].名称 + "」尚未开放）";
        if (!准入通过) s += "\n（" + 取未开放原因() + "）";
        文本.text = s;
    }

    void 建面板()
    {
        // 借场景里已有 UI 的字体，避免依赖内置字体（团结引擎里名称可能不同）
        Font 字 = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Text>())
            if (t.font != null && t.gameObject.scene.IsValid()) { 字 = t.font; break; }

        var canvasGo = new GameObject("传送面板", typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasScaler));
        面板 = canvasGo;
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var 底板 = new GameObject("底板", typeof(Image));
        底板.transform.SetParent(canvasGo.transform, false);
        var rt = 底板.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460f, (InkUITheme.Enabled ? 160f : 120f) + 56f * Mathf.Max(1, 选项.Length));
        rt.anchoredPosition = new Vector2(0f, 40f);
        底板.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.07f, 0.92f);

        var 文go = new GameObject("文本", typeof(Text));
        文go.transform.SetParent(底板.transform, false);
        var 文rt = 文go.GetComponent<RectTransform>();
        文rt.anchorMin = new Vector2(0f, 1f); 文rt.anchorMax = new Vector2(1f, 1f);
        文rt.pivot = new Vector2(0.5f, 1f);
        文rt.sizeDelta = new Vector2(-24f, InkUITheme.Enabled ? 60f : 96f);
        文rt.anchoredPosition = new Vector2(0f, InkUITheme.Enabled ? -42f : -14f);
        文本 = 文go.GetComponent<Text>();
        文本.font = 字; 文本.fontSize = 26; 文本.alignment = TextAnchor.UpperCenter;
        文本.color = new Color(0.95f, 0.92f, 0.8f);

        按钮s.Clear();
        for (int i = 0; i < 选项.Length; i++)
        {
            int 序号 = i;                                   // 闭包捕获
            var bgo = new GameObject("按钮_" + 选项[i].名称, typeof(Image), typeof(Button));
            bgo.transform.SetParent(底板.transform, false);
            var brt = bgo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0f); brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(400f, 46f);
            brt.anchoredPosition = new Vector2(0f, (InkUITheme.Enabled ? 30f : 18f) + 56f * (选项.Length - 1 - i));
            bool 灰 = 选项[i] == null || 选项[i].暂未开放 || !准入通过;
            bgo.GetComponent<Image>().color = 灰 ? new Color(0.22f, 0.2f, 0.22f, 1f) : new Color(0.18f, 0.32f, 0.22f, 1f);
            var bt = bgo.GetComponent<Button>();
            bt.interactable = !灰;
            if (!灰) bt.onClick.AddListener(() => 执行(序号));

            var tgo = new GameObject("字", typeof(Text));
            tgo.transform.SetParent(bgo.transform, false);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector3.zero; trt.anchorMax = Vector3.one;
            trt.sizeDelta = Vector2.zero;
            var tt = tgo.GetComponent<Text>();
            tt.font = 字; tt.fontSize = 24; tt.alignment = TextAnchor.MiddleCenter;
            tt.color = 灰 ? new Color(0.55f, 0.53f, 0.55f) : Color.white;
            tt.text = (选项[i] == null ? "?" : 选项[i].名称) + (灰 ? "（未开放）" : "");
            按钮s.Add(bt);
        }
    }

    // ---------------- 执行 ----------------

    /// <summary>界面按钮调这个；也可以从别处（剧情/快捷键）直接调</summary>
    public void 执行(int 序号)
    {
        if (序号 < 0 || 序号 >= 选项.Length || 选项[序号] == null) return;
        var o = 选项[序号];
        if (o.暂未开放)
        {
            if (打印日志) Debug.Log("[传送]「" + o.名称 + "」尚未开放", this);
            return;
        }
        // ★ 准入没通过：给一句人话，别静默不动（玩家会以为"点了没反应"）
        if (!准入通过)
        {
            string 原因 = 取未开放原因();
            ToastUI.提示(原因);
            Debug.Log("[传送]「" + o.名称 + "」准入不通过：" + 原因, this);
            return;
        }
        if (打印日志) Debug.Log("[传送] 执行「" + o.名称 + "」→ 场景「" + o.场景 + "」落点「" + o.落点 + "」", this);

        关面板();
        // ★ 先给外部接管（例：镇妖塔的「进入下一层」—— 它不是一次传送，是塔的逻辑）
        if (选项接管 != null && 选项接管(序号)) return;
        if (string.IsNullOrEmpty(o.场景)) 同场景传送(o);
        else 跨场景传送(o);
    }

    void 同场景传送(传送选项 o)
    {
        var 点 = 找落点(o.落点, SceneManager.GetActiveScene());
        if (点 == null) { Debug.LogWarning("[传送] 本场景里找不到落点「" + o.落点 + "」", this); return; }
        放下玩家(点);
    }

    /// <summary>
    /// ★★ 跨场景传送必须走**统一的切场景过渡**（用户 2026-09-27 要求：
    /// 「黑幕 + 场景名应该成为切换场景的惯例」）。
    ///
    /// 历史教训（两条，都踩过）：
    ///   ① 用本组件自己的协程 ✗ —— `LoadSceneMode.Single` 会销毁旧场景里所有物体，
    ///      包括挂本组件的光圈，协程当场中断，"加载完找落点、把玩家放过去"永不执行。
    ///   ② 后来改成自己造一个 `传送宿主`（DontDestroyOnLoad）**直接 LoadSceneAsync** ——
    ///      落点是能找到了，但**完全绕过了黑幕+场景名**，而且没有把主线进度钉进快照，
    ///      玩家会在新场景里被"重新接续旧进度"，表现成剧情重放（师兄又带你进一次宗门）。
    ///
    /// 现在只调 `黑幕字幕.开始场景过渡`：它会把【落黑幕+场景名 → 加载 → 摆位 →
    /// 停 2 秒 → 淡出】整条跑完，而且宿主就是黑幕自己（DontDestroyOnLoad），
    /// 落点名交给它在**新场景**里解析。
    /// </summary>
    void 跨场景传送(传送选项 o)
    {
        // 进度先钉住：新场景里的任务管理器会接手这份进度，不会从头再来
        var 任务 = Object.FindObjectOfType<任务管理器>();
        if (任务 != null) 任务.切场景前钉进度();

        黑幕字幕.开始场景过渡(o.场景, 任务管理器.取场景显示名(o.场景), Vector3.zero, o.落点);
    }

    static Transform 找落点(string 名, Scene 场景)
    {
        if (string.IsNullOrEmpty(名)) return null;
        foreach (var g in 场景.GetRootGameObjects())
        {
            if (g.name == 名) return g.transform;
            foreach (var t in g.GetComponentsInChildren<Transform>(true))
                if (t.name == 名) return t.transform;
        }
        return null;
    }

    static void 放下玩家(Transform 点)
    {
        var 玩 = Object.FindObjectOfType<PlayerVitals>();
        if (玩 == null) { Debug.LogWarning("[传送] 场景里找不到玩家（PlayerVitals）"); return; }
        var cc = 玩.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;                  // 关掉再挪，免得被"推出"
        玩.transform.position = 点.position;
        玩.transform.rotation = 点.rotation;
        if (cc != null) cc.enabled = true;
        var 骑 = 玩.GetComponent<MountRider>();
        if (骑 != null) 骑.强制下坐骑();                      // 传送时先下坐骑，免得坐骑跟丢
        if (点.name != null) Debug.Log("[传送] 玩家已放到「" + 点.name + "」 " + 点.position.ToString("F2"));
    }
}
