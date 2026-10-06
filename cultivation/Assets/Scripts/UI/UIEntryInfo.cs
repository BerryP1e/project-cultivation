using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 条目信息栏。背包的「选中物品图片 / 选中物品介绍」、神通的「神通信息说明」、
/// 灵阵的「选中灵阵的相关信息介绍」都用它。
///
/// 对被动神通会额外显示一个【启用 / 停用】按钮。
/// </summary>
public class UIEntryInfo : MonoBehaviour
{
    [Header("引用")]
    public Image iconImage;
    public Text nameText;
    public Text tierText;
    public Text descriptionText;

    [Tooltip("主动 / 被动 标签")]
    public Text kindText;

    [Tooltip("启用/停用按钮，只对被动神通出现")]
    public Button actionButton;

    [Tooltip("按钮上的文字")]
    public Text actionLabel;

    [Tooltip("数据源，用于读写被动神通的启用状态")]
    public UIPanelData data;

    /// <summary>
    /// **固定显示"当前修炼的功法"**（境界页上半块「功法展示」用）。
    ///
    /// 为什么需要它（用户 2026-09-27 报的 bug）：
    ///   这块原来是"列表选中项详情"，`Show()` 只由 `UIEntryList.选中某行()` 调用 ——
    ///   而「功法展示」**不在任何列表里**，`infoTarget` 也没人指向它，
    ///   于是 `Show()` 从没被调用过，**永远显示占位「（未设定当前功法）」**，
    ///   玩家学完秘籍也看不到自己的功法。
    ///
    /// 填上这个引用后：本组件会**订阅 `data.Changed`**（转修、学会都会触发），
    /// 一直显示 `data.当前功法`。没设当前功法时显示 <see cref="emptyHint"/>。
    /// 不填 = 保持原行为（由列表选中驱动）。
    /// </summary>
    [Tooltip("固定显示「当前功法」。留空 = 由列表选中驱动（原行为）")]
    public bool 显示当前功法 = false;

    [Header("占位")]
    [Tooltip("没有选中内容时显示的提示")]
    public string emptyHint = "（未选中）";

    /// <summary>当前展示的条目</summary>
    public IPanelEntry Current { get; private set; }

    /// <summary>按钮被点击（参数为当前条目）</summary>
    public event Action<IPanelEntry> ActionClicked;

    void Awake()
    {
        if (actionButton != null)
            actionButton.onClick.AddListener(OnActionClicked);
    }

    void OnEnable()
    {
        if (data != null)
        {
            // 防重复订阅：先退再订
            data.Changed -= 处理数据变化;
            data.Changed += 处理数据变化;
        }
        刷新固定显示();
        RefreshActionState();
    }

    void OnDisable()
    {
        if (data != null) data.Changed -= 处理数据变化;
    }

    /// <summary>数据变了：固定显示模式要跟着换内容，所有模式都要刷新按钮可用状态</summary>
    void 处理数据变化()
    {
        刷新固定显示();
        RefreshActionState();
    }

    /// <summary>「显示当前功法」模式下，把展示内容同步成 data.当前功法</summary>
    void 刷新固定显示()
    {
        if (!显示当前功法 || data == null) return;
        Show(data.当前功法);      // 可能在 Awake/OnEnable 阶段，asset 引用已就绪，安全
    }

    void OnActionClicked()
    {
        // ★ 可使用物品：交给 物品使用器（用掉会从背包扣一个，Changed 事件会刷新列表）
        if (Current is ItemDefinition 物品 && 物品.可使用 && data != null)
        {
            物品使用器.使用(物品, data);
        }
        else if (Current is PassiveDivineAbility p && data != null)
            data.TogglePassive(p);          // 会触发 Changed，列表跟着刷新
        else if (Current is ActiveDivineAbility active && data != null)
        {
            if (data.IsEquipped(active)) data.Unequip(active);
            else if (data.HasEmptySlot()) data.BeginPendingEquip(active);
            else data.ShowHint("主动技能装备栏已经满了，先卸下一个再装备");
        }
        else if (Current is MountDefinition m && data != null)
        {
            // 装备 / 取消装备坐骑。再点一次同一只 = 卸下。
            // ★ 一律走 设置当前坐骑()：互斥规则（装备坐骑 → 自动停用【凭虚御风】）写在那一处，
            //   这里直接写 data.当前坐骑 会绕过它
            data.设置当前坐骑(data.当前坐骑 == m ? null : m);
        }

        RefreshActionState();
        ActionClicked?.Invoke(Current);
    }

    /// <summary>展示某个条目；传 null 清空</summary>
    public void Show(IPanelEntry entry)
    {
        // ★ 「显示当前功法」模式下，内容**只认** data.当前功法 ——
        //   不能被列表选中覆盖（这块不是列表项详情，是常驻展示）。
        //   这样无论是"数据变了"还是"面板被打开时刷新"，显示的都是当前功法。
        if (显示当前功法 && data != null) entry = data.当前功法;

        Current = entry;

        if (entry == null)
        {
            if (nameText != null) nameText.text = emptyHint;
            if (tierText != null) tierText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (kindText != null) kindText.text = "";
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = new Color(0.62f, 0.62f, 0.62f, 1f);
            }
            if (actionButton != null) actionButton.gameObject.SetActive(false);
            UIInkAbilityArt.RefreshDetail(this);
            return;
        }

        if (nameText != null) nameText.text = entry.DisplayName;
        if (tierText != null) tierText.text = "【" + entry.DisplayTier + "】";
        if (descriptionText != null) descriptionText.text = entry.DisplayDescription;
        if (descriptionText != null && entry is ActiveDivineAbility activeInfo)
            descriptionText.text += "\n\n灵力消耗：" + activeInfo.消耗灵力 + "\n冷却：" + activeInfo.冷却时间 + " 秒";
        if (descriptionText != null && (entry is TreasureDefinition || entry is SpiritArrayDefinition))
            descriptionText.text += "\n\n效果待开发";

        // 主动 / 被动 标识
        if (kindText != null)
        {
            kindText.text = UIEntryRow.TagOf(entry);
            kindText.color = UIEntryRow.TagColor(entry);
        }

        if (iconImage != null)
        {
            iconImage.sprite = UIInkAbilityArt.Icon(entry);
            // 没有图时用一块品阶色底板占位，避免一片空白
            iconImage.color = iconImage.sprite != null
                ? Color.white
                : UIEntryRow.TierColor(entry.DisplayTier);
        }

        UIInkAbilityArt.RefreshDetail(this);
        RefreshActionState();
    }

    /// <summary>
    /// 刷新操作按钮的显隐与文字。
    /// 两种条目会用到这个按钮：**被动神通**（启用 / 停用）、**坐骑**（装备坐骑 / 取消装备）。
    /// </summary>
    public void RefreshActionState()
    {
        if (actionButton == null) return;

        var passive = Current as PassiveDivineAbility;
        var active = Current as ActiveDivineAbility;
        var mount = Current as MountDefinition;
        var 物品 = Current as ItemDefinition;
        bool 是可用物品 = 物品 != null && 物品.可使用;

        bool show = data != null && (passive != null || active != null || mount != null || 是可用物品);
        actionButton.gameObject.SetActive(show);
        if (!show) return;

        actionButton.interactable = true;      // 下面各分支按需再关掉
        if (active != null)
        {
            bool equipped = data.IsEquipped(active);
            if (actionLabel != null) actionLabel.text = equipped ? "卸下神通" : "装备神通";
            actionButton.interactable = equipped || data.HasEmptySlot();
            InkUITheme.Button(actionButton, equipped ? "ivory" : "jade");
            return;
        }

        // ---- 可使用物品：使用（材料/提交物不会走到这里，它们 可使用=false）----
        if (是可用物品)
        {
            bool 能用 = 物品使用器.可使用(物品, data);
            bool 是认主法宝 = 物品.使用效果 is 获得法宝效果;
            if (actionLabel != null) actionLabel.text = 是认主法宝 ? (能用 ? "滴血认主" : "已认主") : (能用 ? "使用" : "已获得");
            actionButton.image.color = 能用
                ? new Color(0.40f, 0.70f, 0.45f)      // 能用 → 绿
                : new Color(0.45f, 0.45f, 0.45f);     // 已经学过了 → 灰
            actionButton.interactable = 能用;
            InkUITheme.Button(actionButton);
            return;
        }

        // ---- 坐骑：装备 / 取消装备 ----
        if (mount != null)
        {
            bool 已装备 = data.当前坐骑 == mount;
            if (actionLabel != null) actionLabel.text = 已装备 ? "取消装备" : "装备坐骑";
            actionButton.image.color = 已装备
                ? new Color(0.85f, 0.45f, 0.35f)      // 已装备 → 点它是卸下，用暖色
                : new Color(0.40f, 0.70f, 0.45f);     // 未装备 → 点它是装上，用冷色
            InkUITheme.Button(actionButton);
            return;
        }

        bool enabled = data.IsPassiveEnabled(passive);
        if (actionLabel != null) actionLabel.text = enabled ? "停用" : "启用";
        actionButton.image.color = enabled
            ? new Color(0.85f, 0.45f, 0.35f)      // 已启用 → 点它是停用，用暖色
            : new Color(0.40f, 0.70f, 0.45f);     // 已停用 → 点它是启用，用冷色
        InkUITheme.Button(actionButton);
    }
}
