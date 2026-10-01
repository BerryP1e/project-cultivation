using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一份存档的数据。
///
/// 现在只存最核心的一层（身份 + 位置 + 资源 + 已选功法/神通），
/// 因为完整的属性结算系统还没做。等那个做出来后再往这里加字段，
/// 有 版本 号兜底，旧存档可以按版本做迁移。
/// </summary>
[Serializable]
public class SaveData
{
    public const int 当前版本 = 14;   // 2：修炼；3：战阵；4：已获得能力；5：背包/装备/任务/对话标记；6：镇妖塔层数；7：场景名/朝向/外观；8：纪年/灵田；9：破境加成；10：灵田改成每块地独立状态；11：灵田改成玩家自由摆放（每块地带位置与朝向）；12：新增「摆设」（练功木桩等可摆放物）；13：新增「宗门贡献」（兑换用货币）；14：新增「今日清单 + 两大伏笔等级」（日常循环）

    [Header("身份")]
    public int 版本 = 当前版本;
    public string 角色名 = "无名散修";
    public string 境界 = "炼气期一层";
    public string 最后存档时间 = "";

    [Header("进度")]
    [Tooltip("玩家存档时所在的**场景**（`SceneManager.GetActiveScene().name`）。\n" +
             "读档时 `MainMenuUI` 按它决定加载哪个场景；空或 3C_Testbed 会兜底成 village。\n\n" +
             "【踩过的坑·2026-10-01】这个字段以前**从来没人写**！\n" +
             "建新档时写死 `\"village\"` 之后再也没更新过 —— 于是不管玩家在\n" +
             "太虚宗 / 宗门野外 / 镇妖塔存的档，读档**一律回到古古镇**，\n" +
             "而 `位置` 是对的（是那个场景里的坐标）⇒ 人被扔到古古镇的某个随机点。")]
    public string 场景名 = "village";

    [Tooltip("玩家在场景里的位置")]
    public Vector3 位置 = Vector3.zero;

    [Tooltip("**位置是否有效**。\n\n" +
             "【为什么不能拿 `位置 == Vector3.zero` 当哨兵】老代码是\n" +
             "`数据.位置 == Vector3.zero ? 保持现状 : 应用` ——\n" +
             "而 `(0,0,0)` 是**合法坐标**（塔中心、场景原点附近都可能站上去），\n" +
             "于是「正好站在原点」的档不会恢复位置。现在用显式标志。")]
    public bool 位置有效 = false;

    public float 朝向Y = 0f;

    [Header("资源")]
    public float 当前气血 = -1f;      // -1 表示用满值
    public float 当前灵气 = -1f;

    [Header("配置")]
    public string 功法id = "";

    [Tooltip("玩家当前穿的外观 id（`AppearanceDefinition.id`）。\n" +
             "`玩家外观.已选外观` 是 static —— 活过切场景，但**活不过读档**，所以必须存。")]
    public string 外观id = "";
    public List<string> 已装备神通 = new List<string>();
    public List<string> 已启用被动 = new List<string>();

    [Header("属性快照（属性结算系统做好前的临时方案）")]
    [Header("修炼系统")]
    [Tooltip("总灵气（与功法无关的通用积累）")]
    public long 总灵气 = 0;

    [Tooltip("修炼次数的小数累积（杀怪获得）")]
    public float 修炼次数累积 = 0f;

    [Tooltip("境界等级 1~90。可由 总灵气 ÷ 难度系数 重算，这里存一份方便读档后立刻显示")]
    public int 境界等级 = 1;

    [Tooltip("**已解锁的最高等级**（版本 8）。破境成功才 +1。\n" +
             "【为什么必须存】灵气只涨进度、不涨等级（破境封顶），\n" +
             "所以等级是**独立于总灵气**的状态 —— 不存的话读档会退回 1 级。")]
    public int 已解锁最高等级 = 1;

    [Tooltip("**破境加成**（版本 9）。服下对应大境界的破境丹时写入的一份提升，\n" +
             "`尝试破境()` 不论成败都会清空它。\n" +
             "【为什么必须存】用户 2026-10-01 明确要求：丹是**在背包里吃掉**的（已经没了），\n" +
             "要是服了丹还没破境就存档退出，加成不存就等于白吃一颗。")]
    public float 破境加成 = 0f;

    [Tooltip("已经学会的功法 id。转修功法只能在这几门里选")]
    public List<string> 已学功法 = new List<string>();

    [Header("已获得的能力（用户 2026-09-26：新档不是天生全会，用物品学会/获得）")]
    [Tooltip("已经获得的主动神通 id")]
    public List<string> 已获得主动神通 = new List<string>();

    [Tooltip("已经获得的被动神通 id")]
    public List<string> 已获得被动神通 = new List<string>();

    [Header("战阵")]
    [Tooltip("战阵站位：固定 9 个格子的真灵 id，空位写空字符串。读档时按 id 还原")]
    public List<string> 战阵站位 = new List<string>();

    [Header("背包 / 装备（版本 5 起）")]
    [Tooltip("背包物品的 id。**一件物品有 N 个就在列表里出现 N 次**（和 UIPanelData.物品 一致）")]
    public List<string> 背包物品 = new List<string>();

    [Tooltip("拥有的法宝 id")]
    public List<string> 法宝 = new List<string>();

    [Tooltip("拥有的灵阵 id")]
    public List<string> 灵阵 = new List<string>();

    [Tooltip("拥有的坐骑 id")]
    public List<string> 坐骑 = new List<string>();

    [Tooltip("当前乘骑的坐骑 id，空 = 没骑")]
    public string 当前坐骑 = "";

    [Tooltip("6 个主动技能槽的内容 id，空槽写空字符串")]
    public List<string> 主动技能槽 = new List<string>();

    [Tooltip("被玩家停用的被动神通 id（列表里没有的=启用中）")]
    public List<string> 已停用被动 = new List<string>();

    [Tooltip("已获得的真灵 id")]
    public List<string> 已获得真灵 = new List<string>();

    [Header("主线进度（版本 5 起）")]
    [Tooltip("任务进度，格式同 任务管理器.导出进度()：`任务id:阶段;任务id:阶段`，阶段 0 = 已完成")]
    public string 任务进度 = "";

    [Tooltip("对话标记（分号分隔）。任务条件、对话分支都靠它")]
    public string 对话标记 = "";

    [Header("境界经验（版本 5 起）")]
    public long 当前经验 = 0;
    public long 突破所需总经验 = 0;

    [Header("镇妖塔（版本 6 起）")]
    [Tooltip("当前所在层（1 起）。进塔时从这里继续")]
    public int 镇妖塔当前层 = 1;

    [Tooltip("历史最高层（1 起）")]
    public int 镇妖塔最高层 = 1;

    public List<string> 属性键 = new List<string>();
    public List<float> 属性值 = new List<float>();

    [Header("纪年（版本 8 起）")]
    [Tooltip("从开局起过了多少天。**累加值，不重置** —— 日期/月份都从它换算。\n" +
             "【为什么不存日期】存日期的话，改「一天多少秒」会让老档的月/日错乱；\n" +
             "存「第几天」就永远自洽。")]
    public int 天数 = 0;

    [Tooltip("当天已经过了多少（0~1）。0.5 = 正午。")]
    public float 日内进度 = 0f;

    [Header("修炼机会（版本 8 起）")]
    [Tooltip("每天白送的那次修炼机会的**逐日余额**。格式 `天数:剩余次数`，分号分隔。\n\n" +
             "【为什么按天存而不是存一个总数】需求是「日常机会**最多保存 3 天**，3 天后消失」——\n" +
             "必须知道每一次机会是哪天发的才能让它过期。\n" +
             "打怪得到的机会**不在这里**（那个不过期，走 修炼次数累积）。")]
    public string 日常机会 = "";

    [Tooltip("打怪获得的修炼机会（小数累积）。**不过期**")]
    public float 打怪机会 = 0f;

    [Header("灵田（版本 11 起：每块地自己带位置与朝向）")]
    [Tooltip("玩家在洞府里摆下来的每一块地（位置 / 朝向 / 品阶 / 种了什么 / 长了多久）。\n\n" +
             "⚠️ 这里直接存 `灵田地块状态` 对象 —— 它的字段全是 string/float/int/Vector3，\n" +
             "都在 JsonUtility 支持范围内。**别往那个类里加 Dictionary / 接口 / 属性**，否则存档静默丢失。\n\n" +
             "版本 < 11 的档没有位置信息（那时候地是固定 12 块）→ 走重新开局。")]
    public List<灵田地块状态> 灵田地块 = new List<灵田地块状态>();

    [Header("摆设（版本 12 起）")]
    [Tooltip("玩家在洞府里摆下来的**非灵田**物件（目前是练功木桩）：`类型id` + 位置 + 朝向档。\n\n" +
             "和灵田一样是**玩家自己摆的**，所以位置必须进存档 —— 用户明确要求「这些东西都是要能够存档的」。\n" +
             "木桩的**血量/受击/打空自动回血不在存档里** —— 那来自 prefab 上的 `NpcInstance` + NPC 表定义，\n" +
             "每次读档重新实例化即可，不用存。")]
    public List<摆设状态> 摆设 = new List<摆设状态>();

    [Header("宗门贡献（版本 13 起）")]
    [Tooltip("**宗门里的通用货币**（`宗门贡献.当前` 的落盘处）。\n" +
             "来路：任务表 `奖励贡献` 列（⚠️ **打镇妖塔暂时不发奖励**，用户 2026-10-02 明确）；去处：功德堂兑换。\n" +
             "⚠️ 加字段是**加法**，不做版本门槛 —— 老档读出来是 0，正好等于「新玩家一点贡献都没有」。")]
    public int 宗门贡献 = 0;

    [Header("日常循环（版本 14 起）")]
    [Tooltip("**「今日」清单勾到哪了**：`日常_天` 是勾的是哪一天，`日常_掩码` 按 `日常循环.项` 的位存。\n" +
             "存「哪一天」是为了读档回到**同一天**时勾还在；换了天自然作废（不用清）。\n" +
             "加法字段，不做版本门槛：老档读出来是 天=-1 / 掩码=0，等于「今天还没做事」。")]
    public int 日常_天 = -1;
    public int 日常_掩码 = 0;

    [Header("两大伏笔的异象等级（版本 14 起）")]
    [Tooltip("灵田异常灵气 / 镇妖塔妖力共鸣 的等级（各 0~3，见 `伏笔管理器`）。\n" +
             "加法字段：老档读出来都是 0，正好等于两条线都没开始发酵。")]
    public int 伏笔_灵田 = 0;
    public int 伏笔_塔 = 0;

    /// <summary>空槽 = 从没用过。读不出来或角色名为空都算空</summary>
    public bool 是空的 => string.IsNullOrEmpty(角色名) || string.IsNullOrEmpty(最后存档时间);

    public void 刷新时间戳()
    {
        最后存档时间 = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
    }

    public void 写属性(string 键, float 值)
    {
        int i = 属性键.IndexOf(键);
        if (i >= 0) 属性值[i] = 值;
        else { 属性键.Add(键); 属性值.Add(值); }
    }

    public float 读属性(string 键, float 默认值 = 0f)
    {
        int i = 属性键.IndexOf(键);
        return i >= 0 && i < 属性值.Count ? 属性值[i] : 默认值;
    }
}
