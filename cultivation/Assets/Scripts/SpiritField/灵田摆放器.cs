using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// **摆放器** —— 把背包里的一件东西"拿在鼠标上"，找个地方放下。
///
/// ## 它现在能摆两种东西
///
/// | 模式 | 摆什么 | 外观来源 |
/// |---|---|---|
/// | 灵田 | `摆放物库` 的 `lingtian` | 程序化土床（`灵田地块外观.造幽灵`） |
/// | 摆设 | `muzhuang`（练功木桩）等 | **实例化那个 prefab** 当预览 |
///
/// 需求原文（用户 2026-10-01）：
/// > 「在背包中找到灵田开拓令……**取消面板**，鼠标会变成一块待摆放的**半透明的灵田**，
/// >   可以在整个洞府中有地面的有空间的地方任意的摆放。摆放中的灵田可以旋转 **8 个朝向**，
/// >   摆下来后它以及它周围的一部分空间会被标记为不可摆放东西。」
/// > 「让**木桩**也像灵田一样是存在背包里、可以放置的吧……这些东西都是要能够存档的。」
///
/// ## 操作
///
/// | 键 | 作用 |
/// |---|---|
/// | 移动鼠标 | 预览跟着在地面上滑（**自动吸附**到附近同朝向的同类物体旁边） |
/// | **R / E** | 顺时针转一档；**Q** 逆时针（8 档，45°） |
/// | **左键** | 放下（道具在使用那一刻已经扣过；挪动不扣） |
/// | **右键 / ESC** | 取消（**把道具还回去**） |
///
/// ## 为什么"取消要把道具还回去"
///
/// 道具是在**背包里点「使用」那一刻**就被框架扣掉的（`物品使用器.使用()`：
/// 效果返回 true → 扣一件），而"进入摆放态"和"真的摆下去"是两件事 ——
/// 玩家完全可能进去看一眼再取消。所以 <see cref="取消"/> 必须还回去，
/// 并且靠 `道具已扣过` 把关（调试时直接调进来没扣过，就不能白送）。
///
/// ## "取消面板"是什么意思
///
/// 用户要的是**别弹配置面板**，直接进摆放态。所以这里**没有**面板，
/// 只有屏幕底部一条极简按键提示，以及一行"为什么这里不能放"的原因。
/// 另外进摆放态时会把**角色/背包面板收起来**（它 `pauseGameWhenOpen = true`，
/// 开着不仅挡画面、还会把游戏暂停住）。
/// </summary>
[DisallowMultipleComponent]
public class 灵田摆放器 : MonoBehaviour
{
    // ============================================================ 单例

    static 灵田摆放器 实例;
    public static 灵田摆放器 取()
    {
        if (实例 != null) return 实例;
        实例 = FindObjectOfType<灵田摆放器>();
        if (实例 != null) return 实例;
        var go = new GameObject("灵田摆放器");
        DontDestroyOnLoad(go);
        实例 = go.AddComponent<灵田摆放器>();
        return 实例;
    }

    /// <summary>现在是不是在摆放态（别的地方要判断：摆放时不要响应别的交互）</summary>
    public static bool 正在摆放 => 实例 != null && 实例.摆放中;

    enum 干嘛 { 无, 开新地, 挪动, 摆摆设, 挪摆设 }

    干嘛 当前;
    int 挪动哪块 = -1;          // 灵田模式：挪第几块地
    int 挪摆设编号 = -1;        // 摆设模式：挪第几件
    摆放物定义 要摆的;          // 摆设模式：摆的是什么
    int 朝向档;
    GameObject 预览;
    /// <summary>进摆放态时道具**是不是已经被扣了** —— 决定取消时要不要还回去</summary>
    bool 道具已扣过;
    string 当前原因 = "";
    bool 当前可以;

    Canvas 提示画布;
    Text 提示文字;

    bool 摆放中 => 当前 != 干嘛.无;
    bool 是摆设模式 => 当前 == 干嘛.摆摆设 || 当前 == 干嘛.挪摆设;

    // ============================================================ 进入 / 退出

    /// <summary>**通用入口**：把某种摆放物"拿在鼠标上"（灵田开拓令 / 练功木桩都走它）</summary>
    public static bool 开始摆(摆放物定义 定义, out string 原因, bool 道具已扣 = true)
    {
        原因 = "";
        if (定义 == null) { 原因 = "没有这种东西的定义"; return false; }
        if (!摆放校验.当前场景可摆放)
        {
            原因 = 定义.名 + "只能在个人洞府里摆放";
            return false;
        }

        var 自 = 取();
        自.要摆的 = 定义;
        自.挪动哪块 = -1;
        自.挪摆设编号 = -1;
        自.朝向档 = 0;
        自.道具已扣过 = 道具已扣;
        自.当前 = 定义.是灵田 ? 干嘛.开新地 : 干嘛.摆摆设;
        自.建预览();
        自.收起角色面板();
        Debug.Log("[摆放] 进入摆放态（" + 定义.名 + "）：R 旋转 / 左键放下 / 右键取消");
        return true;
    }

    /// <summary>从背包用「灵田开拓令」开一块新地（<see cref="开始摆"/> 的灵田快捷方式）</summary>
    public static bool 开始开新地(out string 原因, bool 道具已扣 = true)
        => 开始摆(摆放物库.取("lingtian"), out 原因, 道具已扣);

    /// <summary>把第 i 块灵田拿起来重新摆（用户选的是"能挪位置，但不退道具也不多花"）</summary>
    public static bool 开始挪动(int 编号, out string 原因)
    {
        原因 = "";
        var 田 = 灵田.取();
        if (田 == null || 田.状态(编号) == null) { 原因 = "没有这块地"; return false; }

        var 自 = 取();
        自.当前 = 干嘛.挪动;
        自.挪动哪块 = 编号;
        自.要摆的 = 摆放物库.取("lingtian");
        自.道具已扣过 = false;
        自.朝向档 = 田.状态(编号).朝向档;
        自.建预览();
        自.收起角色面板();
        Debug.Log("[摆放] 进入摆放态（挪动第 " + (编号 + 1) + " 块灵田）");
        return true;
    }

    /// <summary>把第 i 件摆设拿起来重新摆（同灵田：不花道具也不退）</summary>
    public static bool 开始挪摆设(int 编号, out string 原因)
    {
        原因 = "";
        var 摆 = 摆设.取();
        var s = 摆 != null ? 摆.状态(编号) : null;
        if (s == null) { 原因 = "没有这件摆设"; return false; }
        var d = s.定义;
        if (d == null) { 原因 = "这件摆设的类型已经不存在了"; return false; }

        var 自 = 取();
        自.当前 = 干嘛.挪摆设;
        自.要摆的 = d;
        自.挪摆设编号 = 编号;
        自.挪动哪块 = -1;
        自.道具已扣过 = false;
        自.朝向档 = s.朝向档;
        自.建预览();
        自.收起角色面板();
        Debug.Log("[摆放] 进入摆放态（挪动第 " + (编号 + 1) + " 件「" + d.名 + "」）");
        return true;
    }

    /// <summary>取消摆放。**只有真的扣过道具**才会退回去，且退的是**对的那一件**</summary>
    public void 取消()
    {
        if (!摆放中) return;

        // 退哪一件：灵田 → 开拓令；摆设 → 那件摆设自己的道具
        string 退的道具id = null;
        if (道具已扣过)
        {
            if (当前 == 干嘛.开新地) 退的道具id = 灵田.开拓令id;
            else if (当前 == 干嘛.摆摆设) 退的道具id = 要摆的 != null ? 要摆的.道具id : null;
        }

        收尾();
        if (!string.IsNullOrEmpty(退的道具id))
        {
            灵田.给道具(退的道具id, 1);
            Debug.Log("[摆放] 取消摆放，「" + 灵田.取物品名(退的道具id) + "」已退回背包");
        }
    }

    void 收尾()
    {
        当前 = 干嘛.无;
        挪动哪块 = -1;
        挪摆设编号 = -1;
        要摆的 = null;
        道具已扣过 = false;
        if (预览 != null) Destroy(预览);
        预览 = null;
        if (提示画布 != null) 提示画布.gameObject.SetActive(false);
    }

    /// <summary>
    /// **把角色/背包面板收起来** —— 用户说的"**取消面板**，鼠标会变成一块半透明的灵田"。
    /// 面板开着有两件事会挡住摆放：它全屏盖住画面；`pauseGameWhenOpen = true` 时**游戏是暂停的**。
    /// </summary>
    void 收起角色面板()
    {
        var 面板 = FindObjectOfType<CharacterPanelUI>();
        if (面板 == null || !面板.IsOpen) return;
        面板.SetOpen(false, true);          // 内部会 RestoreTimeScale()
        Debug.Log("[摆放] 已收起角色面板，开始摆放");
    }

    // ============================================================ 每帧

    void Update()
    {
        if (!摆放中) return;

        if (!摆放校验.当前场景可摆放) { 取消(); return; }   // 场景切走了，别留孤儿预览

        // ---- 旋转：8 档 ----
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.E))
            朝向档 = (朝向档 + 1) % 灵田规格.朝向档数;
        if (Input.GetKeyDown(KeyCode.Q))
            朝向档 = (朝向档 + 灵田规格.朝向档数 - 1) % 灵田规格.朝向档数;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { 取消(); return; }

        // ---- 鼠标 → 地面 ----
        var 相 = Camera.main;
        if (相 == null) return;
        RaycastHit 地;
        if (!Physics.Raycast(相.ScreenPointToRay(Input.mousePosition), out 地, 200f,
                             ~0, QueryTriggerInteraction.Ignore)
            || 地.collider is CharacterController)
        {
            当前可以 = false;
            当前原因 = "把鼠标移到地面上";
            摆预览(预览 != null ? 预览.transform.position : transform.position);
            刷提示();
            return;
        }

        var 位 = 地.point;

        // ---- 吸附：附近有**同朝向的同类**时，把位置吸到"边对齐"处 ----
        var 吸到 = 吸一下(new Vector2(位.x, 位.z), out bool 吸了);
        if (吸了)
        {
            位.x = 吸到.x;
            位.z = 吸到.y;
            RaycastHit 地2;
            if (Physics.Raycast(new Vector3(位.x, 地.point.y + 3f, 位.z), Vector3.down, out 地2, 8f,
                                ~0, QueryTriggerInteraction.Ignore))
                位.y = 地2.point.y;
        }

        当前可以 = 校验(位, out 当前原因);
        摆预览(位);
        刷提示();
    }

    /// <summary>这个位置合不合法（灵田 / 摆设各查各的定义，但都走 `摆放校验`）</summary>
    bool 校验(Vector3 位, out string 原因)
    {
        原因 = "";

        if (是摆设模式)
        {
            var 摆 = 摆设.取();
            if (摆 == null) { 原因 = "找不到摆设系统"; return false; }
            return 摆设.取().位置可用(要摆的 != null ? 要摆的.id : "", 位, 朝向档,
                                      out 原因, 当前 == 干嘛.挪摆设 ? 挪摆设编号 : -1);
        }

        var 田 = 灵田.取();
        if (田 == null) { 原因 = "找不到灵田系统"; return false; }
        return 田.位置可用(位, 朝向档, out 原因, 当前 == 干嘛.挪动 ? 挪动哪块 : -1);
    }

    /// <summary>
    /// 找最近的吸附点。**只吸附"同朝向的同类"**（用户要求"同朝向的同种物品"）：
    /// 摆灵田就只跟别的灵田吸，摆木桩就只跟别的木桩吸。
    /// 中心距按**各自的占地**算 —— 木桩 0.6 米、灵田 1.9 米，共用一个数会一个太远一个太近。
    /// </summary>
    Vector2 吸一下(Vector2 想放, out bool 吸了)
    {
        吸了 = false;
        Vector2 最好 = 想放;

        float 边长 = 要摆的 != null ? 要摆的.占地边长
                   : (是摆设模式 ? 0.6f : 灵田规格.床边长);
        float 中心距 = 灵田规格.吸附中心距_按(边长);
        float 半径 = 灵田规格.吸附距离;
        float 最近 = 半径;

        var 候选 = new System.Collections.Generic.List<Vector2>();

        if (是摆设模式)
        {
            var 摆 = 摆设.取();
            if (摆 == null) return 最好;
            var 全部 = 摆.所有摆设;
            for (int i = 0; i < 全部.Count; i++)
            {
                if (当前 == 干嘛.挪摆设 && i == 挪摆设编号) continue;
                var s = 全部[i];
                if (s == null || 要摆的 == null || s.类型id != 要摆的.id) continue;   // ★ 同类
                if (s.朝向档 != 朝向档) continue;                                      // ★ 同朝向
                灵田规格.吸附候选(new Vector2(s.位置.x, s.位置.z), s.朝向档, 想放, 中心距, 半径, 候选);
                foreach (var c in 候选)
                {
                    float d = Vector2.Distance(c, 想放);
                    if (d < 最近) { 最近 = d; 最好 = c; 吸了 = true; }
                }
            }
            return 最好;
        }

        var 田 = 灵田.取();
        if (田 == null) return 最好;
        var 地块 = 田.所有地块;
        for (int i = 0; i < 地块.Count; i++)
        {
            if (当前 == 干嘛.挪动 && i == 挪动哪块) continue;
            var b = 地块[i];
            if (b == null || b.朝向档 != 朝向档) continue;
            灵田规格.吸附候选(new Vector2(b.位置.x, b.位置.z), b.朝向档, 想放, 中心距, 半径, 候选);
            foreach (var c in 候选)
            {
                float d = Vector2.Distance(c, 想放);
                if (d < 最近) { 最近 = d; 最好 = c; 吸了 = true; }
            }
        }
        return 最好;
    }

    // ============================================================ 预览

    /// <summary>造出那块半透明的"预览"（已经有一个就复用）</summary>
    void 建预览()
    {
        if (预览 != null) { Destroy(预览); 预览 = null; }

        if (是摆设模式 && 要摆的 != null && !string.IsNullOrEmpty(要摆的.模型路径))
        {
            // 摆设：**实例化它自己的 prefab** 当预览（形状一看就知道要摆什么），
            // 然后把所有材质换成不带贴图的半透明绿/红，并**摘掉碰撞体**
            // （否则鼠标射线会打到预览自己，预览贴在自己身上不动，见踩坑 B71）。
            var 预 = Resources.Load<GameObject>(要摆的.模型路径);
            if (预 == null)
            {
                Debug.LogWarning("[摆放] 找不到模型「" + 要摆的.模型路径 + "」——预览会用一块空网格代替");
                预览 = 灵田地块外观.造幽灵(null);
            }
            else
            {
                预览 = Instantiate(预);
                预览.name = "摆设预览";
                foreach (var c in 预览.GetComponentsInChildren<Collider>()) Destroy(c);
                // ★ **预览上的一切逻辑都要停掉**：木桩 prefab 自带 NpcInstance / AI / 血条，
                //   不停的话预览会自己动、自己显示血条，甚至被打。
                //   用 `enabled = false` 而不是 `Destroy`：销毁别人的组件容易连带把引用打断。
                foreach (var b in 预览.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
                灵田地块外观.幽灵上色(预览, 当前可以, false);
            }
        }
        else
        {
            // 灵田：用程序化土床网格
            预览 = 灵田地块外观.造幽灵(null);
        }

        预览.SetActive(true);
        灵田地块外观.幽灵上色(预览, 当前可以, !是摆设模式);
    }

    void 摆预览(Vector3 位)
    {
        if (预览 == null) return;
        预览.transform.position = 位;
        预览.transform.rotation = Quaternion.Euler(0f, 灵田规格.档转角度(朝向档), 0f);
        灵田地块外观.幽灵上色(预览, 当前可以, !是摆设模式);
    }

    // ============================================================ 放下

    void LateUpdate()
    {
        if (!摆放中) return;
        if (!Input.GetMouseButtonDown(0)) return;

        // 鼠标压在 UI 上时不当成"放下"
        if (UnityEngine.EventSystems.EventSystem.current != null
            && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        试着放下();
    }

    /// <summary>
    /// 把预览**落在它现在的位置**上。返回 true = 放成功（已收尾、预览已销毁）。
    ///
    /// 【为什么可以指定位置】鼠标左键那条路在自动化里按不出来，而 `Update` 每帧又会
    /// 用鼠标射线重算预览位置 ⇒ "从外面挪预览再放下"是无效的。传位置就能验证
    /// "放下之后预览有没有被销毁"（用户报过"放下去还是透明的"，就是这条路）。
    /// </summary>
    public bool 试着放下(Vector3? 指定位置 = null)
    {
        if (!摆放中) return false;

        bool 用指定 = 指定位置.HasValue;
        if (!用指定 && 预览 == null) return false;

        var 位 = 用指定 ? 指定位置.Value : 预览.transform.position;

        if (!用指定 && !当前可以)
        {
            Debug.LogWarning("[摆放] 放不下：" + 当前原因);
            return false;
        }

        // ---- 摆设 ----
        if (是摆设模式)
        {
            var 摆 = 摆设.取();
            if (摆 == null) return false;
            if (当前 == 干嘛.摆摆设)
            {
                if (摆.放置(要摆的.id, 位, 朝向档) < 0) return false;
                收尾();
                return true;
            }
            if (当前 == 干嘛.挪摆设)
            {
                if (!摆.挪动(挪摆设编号, 位, 朝向档)) return false;
                收尾();
                return true;
            }
            return false;
        }

        // ---- 灵田 ----
        var 田 = 灵田.取();
        if (田 == null) return false;

        if (当前 == 干嘛.开新地)
        {
            if (!田.位置可用(位, 朝向档, out string 原因))
            {
                Debug.LogWarning("[摆放] 放不下：" + 原因);
                return false;
            }
            田.放置(位, 朝向档);
            收尾();
            return true;
        }

        if (当前 == 干嘛.挪动)
        {
            if (!田.挪动(挪动哪块, 位, 朝向档)) return false;
            收尾();
            return true;
        }
        return false;
    }

    // ============================================================ 底部提示（唯一的 UI）

    void 建提示()
    {
        if (提示画布 != null) return;

        var go = new GameObject("摆放提示", typeof(Canvas), typeof(CanvasScaler));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 1900;
        var s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920f, 1080f);
        s.matchWidthOrHeight = 0.5f;

        var 底 = new GameObject("底", typeof(RectTransform));
        底.transform.SetParent(go.transform, false);
        var 图 = 底.AddComponent<Image>();
        图.color = new Color(0.05f, 0.06f, 0.08f, 0.95f);
        图.raycastTarget = false;
        var rt = 图.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(940f, 76f);

        var t = new GameObject("字", typeof(RectTransform));
        t.transform.SetParent(底.transform, false);
        提示文字 = t.AddComponent<Text>();
        提示文字.font = 取字体();
        提示文字.fontSize = 22;
        提示文字.alignment = TextAnchor.MiddleCenter;
        提示文字.color = new Color(0.97f, 0.97f, 0.94f);
        提示文字.raycastTarget = false;
        提示文字.horizontalOverflow = HorizontalWrapMode.Overflow;
        提示文字.supportRichText = true;
        var 描 = t.AddComponent<Outline>();
        描.effectColor = new Color(0f, 0f, 0f, 0.9f);
        描.effectDistance = new Vector2(1.5f, -1.5f);
        var trt = 提示文字.rectTransform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(10f, 0f); trt.offsetMax = new Vector2(-10f, 0f);

        提示画布 = c;
        go.SetActive(false);
    }

    void 刷提示()
    {
        建提示();
        if (提示画布 == null) return;
        if (!提示画布.gameObject.activeSelf) 提示画布.gameObject.SetActive(true);

        string 第一行;
        if (是摆设模式)
            第一行 = (当前 == 干嘛.挪摆设 ? "挪动" : "摆放") + "「" + (要摆的 != null ? 要摆的.名 : "?") + "」";
        else
            第一行 = 当前 == 干嘛.挪动 ? "挪动第 " + (挪动哪块 + 1) + " 块灵田" : "开垦新灵田";

        提示文字.text = 第一行 + "　[R] 旋转（第 " + (朝向档 + 1) + "/8 朝向）　[左键] 放下　[右键/ESC] 取消\n"
                      + (当前可以 ? "<color=#8CE08C>这里可以放</color>"
                                  : "<color=#FF8080>放不了：" + 当前原因 + "</color>");
    }

    // ============================================================ 字体

    static Font 取字体()
    {
#if UNITY_EDITOR
        var f = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/SimHei.ttf");
        if (f != null) return f;
#endif
        foreach (var x in Resources.FindObjectsOfTypeAll<Font>())
            if (x != null && x.name.ToLowerInvariant().Contains("simhei")) return x;
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }
}
