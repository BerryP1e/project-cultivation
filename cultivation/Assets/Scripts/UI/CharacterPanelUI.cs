using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色面板的页签，顺序与概念图左侧一致：
/// **背包 / 境界 / 神通 / 法宝 / 法则 / 坐骑 / 外观**；保留旧索引以对应现有场景绑定。
/// </summary>
public enum CharacterTab
{
    背包 = 0,
    境界 = 1,
    神通 = 2,
    法宝 = 3,
    法则 = 4,
    战阵 = 5, // 仅保留页面索引，入口由镇妖葫图标右键接管。
    坐骑 = 6,
    外观 = 7,
}

/// <summary>一个页签按钮与它对应的页面。</summary>
[Serializable]
public class TabBinding
{
    [Tooltip("页签按钮")]
    public Button button;

    [Tooltip("该页签对应的整页根节点")]
    public GameObject page;

    [Tooltip("按钮底板，用于切换选中态颜色")]
    public Image background;
}

/// <summary>
/// 角色面板主控。I 键唤出/收起，默认落在「背包」页；Esc 关闭。
/// 挂在与 Canvas 同级的 UI 根节点上。
/// </summary>
public class CharacterPanelUI : MonoBehaviour
{
    [Header("面板")]
    [Tooltip("整个面板的根节点。它会随开关显隐")]
    public GameObject panelRoot;

    [Tooltip("打开面板时暂停游戏（Time.timeScale = 0），方便玩家从容操作")]
    public bool pauseGameWhenOpen = true;

    [Tooltip("唤出/收起按键")]
    public KeyCode toggleKey = KeyCode.I;

    [Tooltip("关闭按键")]
    public KeyCode closeKey = KeyCode.Escape;

    [Tooltip("打开时是否显示鼠标（2.5D 游戏需要）")]
    public bool showCursorWhenOpen = true;

    [Tooltip("启动时是否处于关闭状态")]
    public bool startClosed = true;

    [Header("页签（顺序需与左侧一致）")]
    public List<TabBinding> tabs = new List<TabBinding>();

    [Header("默认页")]
    public CharacterTab defaultTab = CharacterTab.背包;

    /// <summary>当前是否展开</summary>
    public bool IsOpen { get; private set; }
    public bool 过渡中 => panelFade != null;
    CanvasGroup panelGroup;
    Coroutine panelFade;

    /// <summary>当前页签</summary>
    public CharacterTab CurrentTab { get; private set; }

    /// <summary>页签切换事件（参数为新页签）</summary>
    public event Action<CharacterTab> TabChanged;

    // 暂停前的时间缩放，关闭时原样恢复
    float cachedTimeScale = 1f;
    bool timeScaleCached;
    readonly Dictionary<CanvasGroup,Vector3> hiddenHud=new Dictionary<CanvasGroup,Vector3>();
    float nextHudScan;
    void SuppressHud(){
        if(CurrentTab==CharacterTab.背包||UIInkNavigation.启用&&(CurrentTab==CharacterTab.神通||CurrentTab==CharacterTab.法则)){RestoreHud();return;}
        foreach(var canvas in FindObjectsOfType<Canvas>(true)){
            if(canvas.name!="HudCanvas" && canvas.name!="QuestGuideCanvas" && canvas.name!="ChronicleCanvas")continue;
            var group=canvas.GetComponent<CanvasGroup>();if(group==null)group=canvas.gameObject.AddComponent<CanvasGroup>();
            if(!hiddenHud.ContainsKey(group))hiddenHud.Add(group,new Vector3(group.alpha,group.interactable?1:0,group.blocksRaycasts?1:0));
            group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
        }
    }
    void RestoreHud(){foreach(var pair in hiddenHud)if(pair.Key!=null){pair.Key.alpha=pair.Value.x;pair.Key.interactable=pair.Value.y>0;pair.Key.blocksRaycasts=pair.Value.z>0;}hiddenHud.Clear();}
    void LateUpdate(){if(IsOpen && Time.unscaledTime>=nextHudScan){nextHudScan=Time.unscaledTime+.5f;SuppressHud();}}

    void Awake()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;   // 闭包捕获
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => ShowTab((CharacterTab)index));
        }
    }

    void Start()
    {
        if (tabs.Count > 5 && tabs[5].button != null) tabs[5].button.gameObject.SetActive(false);
        SetOpen(!startClosed, true);
        StartCoroutine(DetachFormation());
    }

    System.Collections.IEnumerator DetachFormation()
    {
        // Let the existing skin finish building the formation preview before moving it.
        yield return null;
        GourdFormationUI.Attach(this);
        UIInkNavigation.应用(transform);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && (IsOpen || 过渡中 || !UiEscRegistry.SceneInputBlocked))
            SetOpen(!IsOpen);

        if (IsOpen && Input.GetKeyDown(closeKey))
            HandleBack();
    }

    public void HandleBack()
    {
        if(CurrentTab==CharacterTab.法则 && tabs.Count>4 && tabs[4].page) {
            var law=tabs[4].page.GetComponent<UIInkLawPage>();
            if(law && law.熔炼已打开 && law.返回上层()){UiEscRegistry.记录关闭();return;}
        }
        SetOpen(false);
    }

    /// <summary>开关面板</summary>
    public void SetOpen(bool open, bool snap = false)
    {
        if(panelFade!=null){StopCoroutine(panelFade);panelFade=null;}
        IsOpen = open;
        bool closingFade=!open&&!snap&&panelRoot&&panelRoot.activeInHierarchy;
        UiEscRegistry.SetSceneInputBlocked(this,open||closingFade);
        if(open)SuppressHud();else {RestoreHud();UIDragContext.End(true);}

        if (panelRoot != null)
        {
            panelGroup=panelRoot.GetComponent<CanvasGroup>();if(!panelGroup)panelGroup=panelRoot.AddComponent<CanvasGroup>();
            var oldMotion=panelRoot.GetComponent<UIInkMotion>();if(oldMotion)oldMotion.enabled=false;
            panelGroup.interactable=open;panelGroup.blocksRaycasts=open||closingFade;
            if(open)panelRoot.SetActive(true);
            else if(!closingFade)panelRoot.SetActive(false);
        }

        if (open)
        {
            ShowTab(defaultTab == CharacterTab.战阵 ? CharacterTab.境界 : defaultTab);
            if (showCursorWhenOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            if (pauseGameWhenOpen && !timeScaleCached)
            {
                cachedTimeScale = Time.timeScale;
                timeScaleCached = true;
                Time.timeScale = 0f;
            }
        }
        else
        {
            if(!closingFade)RestoreTimeScale();
            UiEscRegistry.记录关闭();     // 告诉别人"我刚关过"，这一下 ESC 别再被暂停菜单消费
        }
        if(panelRoot&&panelGroup)
        {
            float target=open?1:0;
            if(snap||!isActiveAndEnabled||UIInkMotion.减少动效||Mathf.Abs(panelGroup.alpha-target)<.001f)
            {
                panelGroup.alpha=target;
                if(!open)FinishClose();
            }
            else panelFade=StartCoroutine(FadePanel(open,open?.12f:.10f));
        }
        GetComponent<UIInkNavigation>()?.刷新状态();
    }

    System.Collections.IEnumerator FadePanel(bool open,float duration)
    {
        float from=panelGroup.alpha,elapsed=0,target=open?1:0;
        while(elapsed<duration)
        {
            float t=Mathf.Clamp01(elapsed/duration);float eased=1-Mathf.Pow(1-t,3);
            panelGroup.alpha=Mathf.Lerp(from,target,eased);
            yield return null;elapsed+=Time.unscaledDeltaTime;
        }
        panelGroup.alpha=target;panelFade=null;
        if(!open)FinishClose();
        GetComponent<UIInkNavigation>()?.刷新状态();
    }

    void FinishClose()
    {
        if(panelGroup){panelGroup.interactable=false;panelGroup.blocksRaycasts=false;}
        if(panelRoot)panelRoot.SetActive(false);
        UiEscRegistry.SetSceneInputBlocked(this,false);RestoreTimeScale();
    }

    /// <summary>恢复暂停前的时间缩放。关闭面板、组件被禁用或销毁时都会调用</summary>
    public void RestoreTimeScale()
    {
        if (!timeScaleCached) return;
        Time.timeScale = cachedTimeScale;
        timeScaleCached = false;
    }

    void OnDisable()
    {
        if(panelFade!=null){StopCoroutine(panelFade);panelFade=null;}
        if(!IsOpen&&panelRoot)panelRoot.SetActive(false);
        UiEscRegistry.SetSceneInputBlocked(this,false);
        RestoreHud();UIDragContext.End(true);
        RestoreTimeScale();
    }
    void OnEnable()=>UiEscRegistry.SetSceneInputBlocked(this,IsOpen);

    void OnDestroy()
    {
        UiEscRegistry.SetSceneInputBlocked(this,false);
        RestoreHud();
        RestoreTimeScale();
    }

    /// <summary>切换到指定页签</summary>
    public void ShowTab(CharacterTab tab)
    {
        if (tab == CharacterTab.战阵) return;
        if(tab!=CurrentTab)UIDragContext.End(true);
        CurrentTab = tab;
        bool equipment=UIInkNavigation.启用&&(tab==CharacterTab.神通||tab==CharacterTab.法则);
        int active = equipment?(int)CharacterTab.神通:(int)tab;

        for (int i = 0; i < tabs.Count; i++)
        {
            var b = tabs[i];
            if (b.page != null) b.page.SetActive(i == active && i != (int)CharacterTab.战阵);
            if (b.background != null)
                b.background.color = (i == active) ? UIBuildUtils.ColorTabActive : UIBuildUtils.ColorTabNormal;
        }

        TabChanged?.Invoke(tab);
        if(IsOpen)SuppressHud();
        if(!equipment&&tab==CharacterTab.法则&&tabs.Count>4&&tabs[4].page)
            tabs[4].page.GetComponent<UIInkLawPage>()?.切换背包(true);
    }

    /// <summary>按索引切换（给 UnityEvent 用）</summary>
    public void ShowTabByIndex(int index)
    {
        ShowTab((CharacterTab)Mathf.Clamp(index, 0, tabs.Count - 1));
    }
}
