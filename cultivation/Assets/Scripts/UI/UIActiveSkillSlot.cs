using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 主动技能装备位里的一个格子。
/// 两种用法：
///   · 有待装备神通时点它 → 放入（空格才行，被占用会给出提示）
///   · 平时点它          → 选中看信息
/// 也支持从列表拖过来直接放入。
///
/// 【注意】这个类是**独立文件**、而且**文件名和类名一致** —— 这是必须的：
/// Unity 只认「文件名同名的那一个类」是文件的主类，同一个文件里的**其它** MonoBehaviour
/// 在编辑期 `AddComponent` 看着是好的，但一旦**场景从磁盘重新加载**（进出 Play、重启编辑器）
/// 就会变成 **Missing 脚本**。
/// 血泪史：它以前和 `UIActiveSkillBar` 挤在一个文件里，实测 18 个格子里 14 个变成了 Missing，
/// 表现是「神通页点装备格子没反应」；战阵九宫格也踩过同一个坑（见docs/ai/archive/开发注意事项-流水原文.md §17）。
/// </summary>
public class UIActiveSkillSlot : MonoBehaviour, IDropHandler
{
    [Header("引用")]
    public Image background;
    public Image icon;
    public Text label;
    public Button button;

    [Tooltip("清空按钮（右上角的小叉），留空则没有清空功能")]
    public Button clearButton;

    [Tooltip("格子序号，0~5")]
    public int index;

    [Tooltip("数据源。留空则自动往 Canvas 上找")]
    public UIPanelData data;

    /// <summary>本格绑定的内容（可能是神通/法宝/灵阵）</summary>
    public Object Content { get; private set; }

    [Tooltip("有待装备内容时，空位的高亮色")]
    public Color emptyReadyColor = new Color(0.55f, 0.88f, 0.55f, 1f);

    static Sprite emptyFrame;
    static Sprite filledFrame;

    static Sprite LoadFrame(bool filled)
    {
        if (!InkUITheme.Enabled) return null;
        if (filled)
        {
            if (filledFrame == null)
                filledFrame = InkUITheme.Load("SkillsPage/skills/slot-filled");
            return filledFrame;
        }

        if (emptyFrame == null)
            emptyFrame = InkUITheme.Load("SkillsPage/skills/slot-empty");
        return emptyFrame;
    }

    public void Bind(Object content, bool pendingHighlight = false)
    {
        bool changed = Content != content;
        Content = content;
        var entry = content as IPanelEntry;

        if (icon != null)
        {
            // 空槽不要让生成器的灰色占位 Image 盖住独立的异形槽底；
            // 有真实 DisplayIcon 时才显示内容层。
            icon.gameObject.SetActive(entry != null);
            icon.sprite = entry != null ? entry.DisplayIcon : null;
            icon.color = entry != null
                ? (entry.DisplayIcon != null ? Color.white : UIEntryRow.TierColor(entry.DisplayTier))
                : new Color(0.9f, 0.9f, 0.9f, 1f);
        }

        // 名字挂在格子下方
        if (label != null)
        {
            label.text = entry != null ? entry.DisplayName : "";
            label.color = entry != null ? UIEntryRow.TagColor(entry) : new Color(0.5f, 0.5f, 0.5f);
        }

        // 有东西在等着放进来时，把空格点亮，提示玩家可以点
        if (background != null)
        {
            var frame = LoadFrame(content != null);
            if (frame != null)
            {
                background.sprite = frame;
                background.overrideSprite = null;
                background.type = Image.Type.Simple;
                background.color = Color.white;
            }
            background.color = (content == null && pendingHighlight)
                ? emptyReadyColor
                : (frame != null ? Color.white : UIBuildUtils.ColorSlot);
        }

        if (clearButton != null) clearButton.gameObject.SetActive(content != null);
        if (changed && gameObject.activeInHierarchy) UIInkMotion.晕开(transform as RectTransform, null, UIInkMotion.Timing.Slot);
    }

    /// <summary>取数据源。优先用接好的引用，没有就顺着 Canvas 往上找</summary>
    public UIPanelData ResolveData()
    {
        if (data != null) return data;

        var canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            data = canvas.GetComponentInParent<UIPanelData>();
            if (data == null) data = canvas.GetComponent<UIPanelData>();
        }
        return data;
    }

    /// <summary>拖动到本格 → 装备</summary>
    public void OnDrop(PointerEventData e)
    {
        if (!UIDragContext.Dragging || UIDragContext.Entry == null) return;
        if (UIDragContext.Entry is PassiveDivineAbility) return;

        var panel = ResolveData();
        if (panel == null) return;

        var obj = UIDragContext.Entry as Object;
        if (obj == null) return;

        panel.EquipToSlot(index, obj);
        UIDragContext.End(true);
    }

    /// <summary>清空本格</summary>
    public void Clear()
    {
        var panel = ResolveData();
        if (panel == null) return;
        panel.ClearSlot(index);
    }
}
