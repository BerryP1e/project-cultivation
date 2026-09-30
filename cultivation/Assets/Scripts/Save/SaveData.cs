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
    public const int 当前版本 = 7;   // 2：修炼；3：战阵；4：已获得能力；5：背包/装备/任务/对话标记；6：镇妖塔层数；7：场景名/朝向/外观

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
