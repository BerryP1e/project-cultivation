using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class 宗门委托
{
    public string id, 名称, 类型, 物品id, 说明;
    public int 数量, 等级 = 1, 奖励贡献;
    public bool 是悬赏 => 类型 == "悬赏";
    public int 对应塔层
    {
        get
        {
            var t = TowerFloorTable库.取();
            return t == null ? 1 : t.夹层((等级 - t.起始等级) * Mathf.Max(1, t.每多少层一级) + 1);
        }
    }
    public SpawnGroup 妖群 => 是悬赏 ? SpawnGroup库.按id(TowerFloorTable库.取()?.取刷怪组(对应塔层)) : null;
    public List<string> 妖魔列表()
    {
        var g = 妖群; if (g == null || g.是空的) return new List<string>();
        var ids = g.展开();
        int n = Mathf.Max(1, Mathf.RoundToInt(ids.Count * TowerFloorTable库.取().取数量倍率(对应塔层)));
        return Enumerable.Range(0, n).Select(i => ids[i % ids.Count]).ToList();
    }
    public ItemDefinition 需求物品 => QuestDatabase.取()?.物品库?.FirstOrDefault(i => i != null && i.物品id == 物品id);
    public string 目标介绍 => 是悬赏 ? 宗门任务.妖群介绍(妖魔列表()) : (需求物品?.DisplayName ?? 物品id) + " ×" + 数量;
}

[Serializable]
public class 宗门委托记录
{
    public string id;
    // 每个元素是一只尚未击杀的妖魔，混合妖群也能准确恢复。
    public List<string> 剩余妖魔 = new List<string>();
}

/// <summary>宗门支线独立于主线阶段；纯数据跨场景保留，存档使用显式 JSON 字段。</summary>
public static class 宗门任务
{
    [Serializable] class 配置 { public List<宗门委托> 任务 = new List<宗门委托>(); }
    [Serializable] class 进度 { public string 追踪id; public List<宗门委托记录> 记录 = new List<宗门委托记录>(); }
    static 配置 配表;
    static 进度 数据 = new 进度();
    public static event Action 变化;
    public static IReadOnlyList<宗门委托> 全部
    {
        get
        {
            if (配表 == null)
            {
                var json = Resources.Load<TextAsset>("宗门/宗门任务");
                配表 = json != null ? JsonUtility.FromJson<配置>(json.text) : new 配置();
                if (配表 == null || 配表.任务 == null) 配表 = new 配置();
            }
            return 配表.任务;
        }
    }
    public static 宗门委托 按id(string id) => 全部.FirstOrDefault(q => q.id == id);
    public static 宗门委托记录 记录(string id) => 数据.记录.FirstOrDefault(q => q.id == id);
    public static 宗门委托 当前追踪 => 按id(数据.追踪id);
    public static 宗门委托 当前悬赏 => 全部.FirstOrDefault(q => q.是悬赏 && 记录(q.id) != null);
    public static string 导出() => JsonUtility.ToJson(数据);
    public static void 导入(string json)
    {
        var next = string.IsNullOrEmpty(json) ? new 进度() : JsonUtility.FromJson<进度>(json);
        数据 = new 进度();
        foreach (var r in next?.记录 ?? new List<宗门委托记录>())
        {
            var q = 按id(r?.id);
            if (q == null || 记录(q.id) != null || q.是悬赏 && 当前悬赏 != null) continue;
            var restored = new 宗门委托记录 { id = q.id };
            // 限制为配置中真实存在的成员，避免旧数据/坏数据刷出无关怪物。
            var valid = q.妖魔列表();
            foreach (var id in r.剩余妖魔 ?? new List<string>())
                if (valid.Remove(id)) restored.剩余妖魔.Add(id);
            数据.记录.Add(restored);
        }
        数据.追踪id = 记录(next?.追踪id) != null ? next.追踪id : 数据.记录.FirstOrDefault()?.id;
        变化?.Invoke();
    }
    public static bool 接取(string id, out string 提示)
    {
        var q = 按id(id); 提示 = "委托不存在";
        if (q == null) return false;
        if (记录(id) != null) { 提示 = "已接取该委托"; return false; }
        if (q.是悬赏 && 当前悬赏 != null) { 提示 = "先完成或放弃当前悬赏"; return false; }
        var ids = q.妖魔列表();
        if (q.是悬赏 && (ids.Count == 0 || ids.Any(n => NpcPrefabs.按id取(n)?.GetComponent<NpcInstance>() == null)))
        { 提示 = "此悬赏的妖群配置不完整，暂不能接取"; return false; }
        if (!q.是悬赏 && (q.需求物品 == null || q.数量 <= 0)) { 提示 = "提交物品配置不完整"; return false; }
        数据.记录.Add(new 宗门委托记录 { id = id, 剩余妖魔 = ids }); 数据.追踪id = id;
        提示 = q.是悬赏 ? "悬赏已接取，前往宗门野外剿妖" : "委托已接取，备齐物品后回来提交";
        变化?.Invoke(); return true;
    }
    public static bool 放弃(string id)
    {
        var r = 记录(id); if (r == null) return false; 数据.记录.Remove(r);
        if (数据.追踪id == id) 数据.追踪id = 数据.记录.FirstOrDefault()?.id;
        变化?.Invoke(); return true;
    }
    public static void 追踪(string id) { if (记录(id) == null) return; 数据.追踪id = id; 变化?.Invoke(); }
    public static int 持有数(宗门委托 q, UIPanelData bag = null)
    {
        if (q == null) return 0; bag = bag != null ? bag : 玩家背包();
        return bag?.物品?.Count(i => i != null && i.物品id == q.物品id) ?? 0;
    }
    public static bool 可交付(宗门委托 q, UIPanelData bag = null)
    {
        var r = 记录(q?.id); return r != null && (q.是悬赏 ? r.剩余妖魔.Count == 0 : 持有数(q, bag) >= q.数量);
    }
    public static bool 交付(string id, out string 提示)
    {
        var q = 按id(id); var r = 记录(id); var bag = 玩家背包(); 提示 = "尚未接取此委托";
        if (q == null || r == null) return false;
        if (!可交付(q, bag)) { 提示 = q.是悬赏 ? "妖魔尚未清剿完毕" : "背包中的物品数量不足"; return false; }
        if (!q.是悬赏)
        {
            if (bag == null) { 提示 = "未找到玩家背包"; return false; }
            int count = q.数量;
            for (int i = bag.物品.Count - 1; i >= 0 && count > 0; i--)
                if (bag.物品[i] != null && bag.物品[i].物品id == q.物品id) { bag.物品.RemoveAt(i); count--; }
        }
        // 先移除委托，再广播背包/贡献事件，连续点击只能领奖一次。
        数据.记录.Remove(r);
        if (数据.追踪id == id) 数据.追踪id = 数据.记录.FirstOrDefault()?.id;
        if (!q.是悬赏) bag.RaiseChanged();
        宗门贡献.加(q.奖励贡献, "执事阁「" + q.名称 + "」");
        提示 = "委托完成，获得 " + q.奖励贡献 + " 宗门贡献"; 变化?.Invoke(); return true;
    }
    // 只由绑定到本次悬赏实例的死亡事件调用；野外其他怪物和镇妖塔不计入。
    internal static void 记录击杀(宗门委托记录 owner, string npcId)
    {
        if (owner == null || !ReferenceEquals(owner, 记录(owner.id))) return;
        if (owner.剩余妖魔.Remove(npcId)) 变化?.Invoke();
    }
    public static string 妖群介绍(IEnumerable<string> ids)
    {
        var db = Resources.Load<NpcDatabase>("NPC数据/NPC库");
        return string.Join("、", ids.GroupBy(i => i).Select(g => (db?.按id(g.Key)?.DisplayName ?? g.Key) + " ×" + g.Count()));
    }
    public static UIPanelData 玩家背包()
    {
        var c = UnityEngine.Object.FindObjectOfType<PlayerCultivation>();
        if (c != null && c.面板数据 != null) return c.面板数据;
        var p = GameObject.Find("Player")?.GetComponent<UIPanelData>(); if (p != null) return p;
        return UnityEngine.Object.FindObjectsOfType<UIPanelData>().FirstOrDefault(d => d.isActiveAndEnabled);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void 重置() { 配表 = null; 数据 = new 进度(); 变化 = null; }
}
