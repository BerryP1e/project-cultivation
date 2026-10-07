using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 开始界面的逻辑。
///
/// 布局按概念图：
///   · 深青黑底与独立动态阵法、漩涡、人物和灵气粒子
///   · 左侧竖排四个按钮：新游戏 / 读取存档 / 设置 / 退出
///   · 中央漩涡浮现滚动存档栏，每格左上「角色名 境界」、右下「最后存档时间」，
///     空槽显示「暂无存档」
///
/// 「新游戏」和「读取存档」共用同一个面板，只是模式不同：
///   新游戏模式 —— 只能点空槽，点了就建新档
///   读取模式   —— 只能点有档的槽，点了就载入
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("面板")]
    public GameObject 存档面板;
    public Text 面板标题;
    public Transform 槽位容器;
    public Text 提示;

    [Header("按钮")]
    public Button 新游戏按钮;
    public Button 读取存档按钮;
    public Button 设置按钮;
    public Button 退出按钮;
    public Button 关闭按钮;

    [Header("外观")]
    public Font 字体;

    [Header("颜色")]
    public Color 槽位底色 = new Color(0.62f, 0.62f, 0.62f, 1f);
    public Color 槽位空底色 = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color 文字色 = new Color(0.1f, 0.1f, 0.1f, 1f);

    bool 新游戏模式;
    readonly List<GameObject> 槽位行 = new List<GameObject>();
    float 提示到期;
    MainMenuSaveCarousel 存档轮盘;
    bool[] 存档占用;

    // 删除确认：点第一次是「准备删除」，3 秒内再点一次才真删
    int 待删除槽位 = -1;
    float 确认删除到期;

    static readonly Color 窗口底 = new Color(.075f, .09f, .095f, .98f);
    static readonly Color 窗口字 = new Color(.91f, .90f, .84f);
    static readonly Color 次要字 = new Color(.66f, .70f, .69f);
    static readonly Color 边线色 = new Color(.60f, .58f, .46f, .75f);

    void Awake()
    {
        配置存档窗口();
        MainMenuPresentation.Install(this);
        存档轮盘 = MainMenuSaveCarousel.Build(this);
        if (新游戏按钮 != null) 新游戏按钮.onClick.AddListener(() => 打开面板(true));
        if (读取存档按钮 != null) 读取存档按钮.onClick.AddListener(() => 打开面板(false));
        if (设置按钮 != null) 设置按钮.onClick.AddListener(() => 提示一下("设置功能尚未实现"));
        if (退出按钮 != null) 退出按钮.onClick.AddListener(退出游戏);
        // 关闭按钮原来是在生成器里用 AddListener 挂的，但那个监听器没被序列化下来
        // （实测 GetPersistentEventCount()==0），所以 ✕ 点了没反应。
        // 改成和其它按钮一样，运行时在 Awake 里接，一定生效。
        if (关闭按钮 != null) 关闭按钮.onClick.AddListener(关闭面板);

        if (存档面板 != null) 存档面板.SetActive(false);
        if (提示 != null) 提示.text = "";
    }

    void Update()
    {
        if (提示 != null && 提示.text.Length > 0 && Time.unscaledTime > 提示到期) 提示.text = "";

        // 「确认删除」窗口过期就撤销，避免用户过一会儿点删除结果直接删掉了
        if (待删除槽位 >= 0 && Time.unscaledTime > 确认删除到期)
        {
            待删除槽位 = -1;
            重建槽位();
        }
    }

    void 提示一下(string 内容)
    {
        if (提示 == null) return;
        提示.text = 内容;
        提示到期 = Time.unscaledTime + 3f;
    }

    // ---------------------------------------------------------------- 面板

    void 打开面板(bool 新游戏)
    {
        新游戏模式 = 新游戏;
        待删除槽位 = -1;        // 每次打开都重置删除确认状态
        if (存档面板 != null) 存档面板.SetActive(true);
        if (面板标题 != null) 面板标题.text = 新游戏 ? "选择存档位（新游戏）" : "读取存档";
        var mode = 存档面板 != null ? 存档面板.transform.Find("ModeHint") : null;
        if (mode != null) mode.GetComponent<Text>().text = 新游戏 ? "选择空存档位开始新游戏" : "选择已有存档继续游戏";
        重建槽位();
    }

    // 独立窗口样式：不使用墨迹标签，也不改存档、读档或删除流程。
    void 配置存档窗口()
    {
        if (存档面板 == null) return;
        var rt = 存档面板.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
        rt.pivot = new Vector2(.5f, .5f);
        rt.sizeDelta = new Vector2(760, 780);
        rt.anchoredPosition = new Vector2(150, -20);
        var bg = 存档面板.GetComponent<Image>();
        bg.sprite = null; bg.material = null; bg.type = Image.Type.Simple;
        bg.color = 窗口底; bg.raycastTarget = true;
        细框(rt, true);
        文字色 = 窗口字;
        槽位底色 = new Color(.13f, .16f, .17f);
        槽位空底色 = new Color(.095f, .115f, .12f);
        if (面板标题 != null)
        {
            面板标题.color = 窗口字; 面板标题.fontSize = 28;
            面板标题.alignment = TextAnchor.MiddleLeft;
            UIBuildUtils.Place(面板标题.rectTransform, new Vector2(0,1), Vector2.one,
                new Vector2(30,-76), new Vector2(-90,-22));
        }
        if (槽位容器 is RectTransform slots)
        {
            UIBuildUtils.Place(slots, Vector2.zero, Vector2.one, new Vector2(30,100), new Vector2(-30,-100));
            var layout = slots.GetComponent<VerticalLayoutGroup>();
            if (layout != null) { layout.spacing = 12; layout.padding = new RectOffset(); }
        }
        if (关闭按钮 != null)
        {
            var close = 关闭按钮.GetComponent<RectTransform>();
            close.anchorMin = close.anchorMax = Vector2.one; close.pivot = Vector2.one;
            close.sizeDelta = new Vector2(42,42); close.anchoredPosition = new Vector2(-26,-28);
            普通按钮(关闭按钮, new Color(.13f,.16f,.17f));
        }
        var hint = UIBuildUtils.CreateText("ModeHint", rt, 字体, "", 18, TextAnchor.MiddleLeft, 次要字);
        UIBuildUtils.Place(hint.rectTransform, Vector2.zero, new Vector2(1,0), new Vector2(30,56), new Vector2(-30,84));
        if (提示 != null)
        {
            // 通用提示保留在画布上，窗口关闭时“设置尚未实现”等反馈仍能显示。
            提示.fontSize = 20; 提示.color = new Color(.89f,.72f,.54f);
        }
    }

    static void 细框(RectTransform parent, bool 花纹)
    {
        var decor = UIBuildUtils.CreateRect("FineFrame", parent);
        UIBuildUtils.Stretch(decor, 5);
        for (int side=0; side<4; side++)
        {
            var line = UIBuildUtils.CreateImage("Edge"+side, decor, 边线色);
            bool horizontal = side < 2;
            float edge = side % 2;
            line.rectTransform.anchorMin = horizontal ? new Vector2(0,edge) : new Vector2(edge,0);
            line.rectTransform.anchorMax = horizontal ? new Vector2(1,edge) : new Vector2(edge,1);
            line.rectTransform.sizeDelta = horizontal ? new Vector2(0,1) : new Vector2(1,0);
            line.rectTransform.anchoredPosition = Vector2.zero;
        }
        if (!花纹) return;
        // 小型回纹角饰，使用几何线段保持缩放后的细边清晰。
        for (int corner=0; corner<4; corner++)
        {
            var anchor = new Vector2(corner%2, corner/2);
            var sign = new Vector2(anchor.x==0 ? 1 : -1, anchor.y==0 ? 1 : -1);
            for (int part=0; part<4; part++)
            {
                var line = UIBuildUtils.CreateImage("Corner"+corner+"_"+part, decor, 边线色);
                var r = line.rectTransform; r.anchorMin = r.anchorMax = anchor;
                bool horizontal = part%2==0;
                r.sizeDelta = horizontal ? new Vector2(part<2 ? 22 : 12,1) : new Vector2(1,part<2 ? 22 : 12);
                r.anchoredPosition = Vector2.Scale(sign, horizontal ? new Vector2(part<2 ? 17 : 22,part<2 ? 6 : 12) : new Vector2(part<2 ? 6 : 12,part<2 ? 17 : 22));
            }
        }
    }

    static void 普通按钮(Button button, Color color)
    {
        var image = button.GetComponent<Image>();
        image.sprite = null; image.material = null; image.type = Image.Type.Simple; image.color = color;
        细框(image.rectTransform, false);
        var label = button.GetComponentInChildren<Text>();
        if (label != null) label.color = 窗口字;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1.3f,1.3f,1.3f);
        colors.pressedColor = new Color(.8f,.8f,.8f); colors.disabledColor = new Color(.75f,.75f,.75f);
        button.colors = colors;
    }

    public void 关闭面板()
    {
        if (存档面板 != null) 存档面板.SetActive(false);
    }

    void 重建槽位()
    {
        // 先 SetParent(null, false) 摘出去，再 Destroy。
        // Destroy 延迟到帧末才生效，只调 Destroy 的话：
        // 同一帧重建两次 → 旧行还挂在布局里、新行又加进来 → 槽位行成倍累积。
        // （项目里 UIEntryList 早就是这么写的，这里踩了同一个坑。）
        foreach (var go in 槽位行)
            if (go != null) { go.transform.SetParent(null, false); Destroy(go); }
        槽位行.Clear();
        if (槽位容器 == null) return;

        var 全部 = SaveSystem.读全部();
        存档占用 = new bool[SaveSystem.槽位数];
        for (int i = 0; i < 存档占用.Length; i++) 存档占用[i] = 全部[i] != null && !全部[i].是空的;
        for (int i = 0; i < SaveSystem.槽位数; i++) 建一行(i, 全部[i]);
        if (存档轮盘 != null) 存档轮盘.Refresh(新游戏模式);
    }

    void 建一行(int 槽位, SaveData 数据)
    {
        bool 有档 = 数据 != null && !数据.是空的;

        var 行 = UIBuildUtils.CreateImage("Slot" + 槽位, 槽位容器, 有档 ? 槽位底色 : 槽位空底色);
        var 行Rt = 行.rectTransform;
        var le = 行.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 116f; le.preferredHeight = 116f;
        细框(行Rt, false);

        // UIBuildUtils.CreateImage 默认把 raycastTarget 关掉了（它当底板用）。
        // 但这一行要当按钮，必须打开 —— 否则 Button 永远收不到点击，
        // 表现就是"点存档位没反应"。这是 bug1/bug2 的根因。
        行.raycastTarget = true;

        var 按钮 = 行.gameObject.AddComponent<Button>();
        按钮.targetGraphic = 行;
        按钮.interactable = true;
        var 行颜色 = 按钮.colors;
        行颜色.disabledColor = new Color(.72f,.72f,.72f,1);
        行颜色.highlightedColor = new Color(1.2f,1.2f,1.2f,1);
        按钮.colors = 行颜色;
        int 捕获 = 槽位;
        按钮.onClick.AddListener(() => 存档轮盘.Select(捕获));

        // 左上：角色名 + 境界
        var 左上 = UIBuildUtils.CreateText("Name", 行Rt, 字体,
            有档 ? (数据.角色名 + "    " + 数据.境界) : "暂无存档", 22, TextAnchor.MiddleLeft, 文字色);
        UIBuildUtils.Place(左上.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(20f, 42f), new Vector2(-130f, -14f));
        var number = UIBuildUtils.CreateText("SlotNumber", 行Rt, 字体, "存档 " + (槽位+1).ToString("00"), 16, TextAnchor.LowerLeft, 次要字);
        UIBuildUtils.Place(number.rectTransform, Vector2.zero, Vector2.one, new Vector2(20,16), new Vector2(-130,-62));
        左上.verticalOverflow = VerticalWrapMode.Truncate;
        左上.resizeTextForBestFit = true; 左上.resizeTextMinSize = 16; 左上.resizeTextMaxSize = 22;

        if (有档)
        {
            var 右下 = UIBuildUtils.CreateText("Time", 行Rt, 字体, 数据.最后存档时间, 16, TextAnchor.LowerRight, 次要字);
            UIBuildUtils.Place(右下.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(130f, 16f), new Vector2(-130f, -62f));

            // 删除按钮：贴在行的右侧。
            // 它是行的【子 Button】，点它会先被它自己消费掉，不会冒泡到整行的 Button，
            // 所以不会"点删除反而读档"。
            var 删 = UIBuildUtils.CreateButton("Delete" + 槽位, 行Rt, 字体,
                待删除槽位 == 槽位 ? "确认删除" : "删除", 18);
            var 删Rt = 删.GetComponent<RectTransform>();
            删Rt.anchorMin = new Vector2(1f, 0.5f);
            删Rt.anchorMax = new Vector2(1f, 0.5f);
            删Rt.pivot = new Vector2(1f, 0.5f);
            删Rt.sizeDelta = new Vector2(96f, 44f);
            删Rt.anchoredPosition = new Vector2(-14f, 0f);

            普通按钮(删, 待删除槽位 == 槽位 ? new Color(.40f,.16f,.13f) : new Color(.18f,.20f,.20f));

            int 捕获2 = 槽位;
            删.onClick.AddListener(() => 请求删除(捕获2));
        }

        槽位行.Add(行.gameObject);
    }

    /// <summary>
    /// 删除需要点两次：第一次变成「确认删除」，3 秒内再点一次才真删。
    /// 存档删了就没了，不值得为省一次点击冒险。
    /// </summary>
    void 请求删除(int 槽位)
    {
        if (待删除槽位 == 槽位 && Time.unscaledTime < 确认删除到期)
        {
            待删除槽位 = -1;
            SaveSystem.删档并补位(槽位);
            提示一下("已删除存档位 " + (槽位 + 1) + "，后面的存档已往前补位");
            重建槽位();          // 立刻刷新，能看到补位结果
            return;
        }

        待删除槽位 = 槽位;
        确认删除到期 = Time.unscaledTime + 3f;
        提示一下("再点一次「确认删除」");
        重建槽位();              // 让按钮变成红色「确认删除」
    }

    void 点槽位(int 槽位)
    {
        var 数据 = SaveSystem.读档(槽位);

        if (新游戏模式)
        {
            if (数据 != null && !数据.是空的) { 提示一下("该存档位已有存档"); return; }
            数据 = SaveSystem.建新档();
            SaveSystem.存档(槽位, 数据);
        }
        else
        {
            if (数据 == null || 数据.是空的) { 提示一下("该存档位暂无存档"); return; }
        }

        SaveSystem.当前存档 = 数据;
        SaveSystem.当前槽位 = 槽位;
        进游戏(数据);
    }

    public bool 中央存档可用(int 槽位)
    {
        if (存档占用 == null || 槽位 < 0 || 槽位 >= 存档占用.Length) return false;
        bool exists = 存档占用[槽位];
        return 新游戏模式 ? !exists : exists;
    }

    public void 确认中央存档(int 槽位)
    {
        if (中央存档可用(槽位)) 点槽位(槽位);
    }

    void 进游戏(SaveData 数据)
    {
        // 新存档默认进「古古镇」；老存档里存的 3C_Testbed 是开发测试场景，一并迁到 village
        string 场景 = (string.IsNullOrEmpty(数据.场景名) || 数据.场景名 == "3C_Testbed") ? "village" : 数据.场景名;
        Debug.Log("[MainMenu] 进入场景 " + 场景 + "（槽位 " + SaveSystem.当前槽位 + "）");
        SceneManager.LoadScene(场景);
    }

    void 退出游戏()
    {
        Debug.Log("[MainMenu] 退出游戏");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- ASCII 别名 ----
    public void OpenSlots(bool newGame) => 打开面板(newGame);
    public void CloseSlots() => 关闭面板();
    public void Quit() => 退出游戏();
}
