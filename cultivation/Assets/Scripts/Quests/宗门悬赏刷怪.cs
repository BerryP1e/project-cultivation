using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>只在宗门野外复用现有空地刷一批悬赏；出场景保留剩余名单，回场景只补剩余妖魔。</summary>
public class 宗门悬赏刷怪 : MonoBehaviour
{
    public const string 野外场景 = "Sect_Wilderness";
    SpawnZone 区;
    SpawnGroup 本批;
    宗门委托记录 所属;
    readonly Dictionary<NpcInstance, Action<NpcInstance>> 死亡订阅 = new Dictionary<NpcInstance, Action<NpcInstance>>();
    float 下次检查;
    public SpawnZone 当前刷怪区 => 区;
    public Transform 目标
    {
        get
        {
            var p = GameObject.Find("Player");
            return 死亡订阅.Keys.Where(n => n != null && !n.IsDead)
                .OrderBy(n => p != null ? (p.transform.position - n.transform.position).sqrMagnitude : 0)
                .Select(n => n.transform).FirstOrDefault() ?? (区 != null ? 区.transform : null);
        }
    }
    void Update()
    {
        if (Time.unscaledTime < 下次检查) return; 下次检查 = Time.unscaledTime + .4f;
        同步();
    }
    public void 同步()
    {
        var q = 宗门任务.当前悬赏; var r = 宗门任务.记录(q?.id);
        bool inWild = string.Equals(gameObject.scene.name, 野外场景, StringComparison.OrdinalIgnoreCase);
        if (!inWild || r == null || r.剩余妖魔.Count == 0) { 清理(); return; }
        if (ReferenceEquals(所属, r) && 区 != null && 区.存活数 > 0) return;
        清理();
        var sites = FindObjectsOfType<SpawnZone>().Where(z => z.gameObject.scene == gameObject.scene && !z.运行中 && !z.name.StartsWith("宗门悬赏_", StringComparison.Ordinal))
            .OrderBy(z => z.name, StringComparer.Ordinal).ToList();
        if (sites.Count == 0) return;
        // 等级固定对应一个空地；接取页的内容与刷出的剩余名单共用同一组配置。
        var site = sites[(Mathf.Max(1, q.等级) - 1) % sites.Count];
        // 管理器挂在相机上，妖群必须是独立的世界根节点，不能跟着相机移动或倾斜。
        var root = new GameObject("宗门悬赏_" + q.id); SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        root.transform.SetPositionAndRotation(site.transform.position, Quaternion.identity);
        区 = root.AddComponent<SpawnZone>(); 所属 = r;
        本批 = ScriptableObject.CreateInstance<SpawnGroup>(); 本批.组id = q.妖群.组id; 本批.名字 = q.名称;
        foreach (var g in r.剩余妖魔.GroupBy(id => id)) 本批.成员.Add(new SpawnGroupEntry { npcId = g.Key, 数量 = g.Count() });
        区.刷怪组资产 = 本批; 区.怪物等级 = TowerFloorTable库.取().取怪物等级(q.对应塔层);
        区.补正表 = NpcLevelScale库.取(); 区.层内倍率 = TowerFloorTable库.取().取怪强度倍率(q.对应塔层);
        区.数量倍率 = 1f; 区.自动开始 = false; 区.重生延迟 = 999999f; 区.刷怪父节点 = root.transform;
        区.半径 = site.半径;
        // 每个点独立落地，避免直接用空地中心高度导致坡地上的妖魔悬空。
        int n = r.剩余妖魔.Count; float radius = Mathf.Min(6f, Mathf.Max(1f, site.半径 * .5f));
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var pos = site.transform.position + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
            if (Physics.Raycast(pos + Vector3.up * 60f, Vector3.down, out var h, 160f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) pos.y = h.point.y;
            var point = new GameObject("悬赏刷怪点_" + i); point.transform.SetParent(root.transform, false); point.transform.position = pos;
            point.transform.rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            区.刷怪点.Add(point.AddComponent<SpawnPoint>());
        }
        区.刷出了 += 刷出; 区.清空 += 本波结束; 区.开始刷怪();
    }
    void 刷出(SpawnZone zone, List<NpcInstance> npcs)
    {
        var owner = 所属;
        foreach (var npc in npcs)
        {
            // 归入本场景和本组件，取消/离开只销毁这批，绝不把销毁算成击杀。
            npc.transform.SetParent(zone.transform, true);
            string id = npc.定义.id;
            Action<NpcInstance> callback = null;
            callback = dead => { dead.Died -= callback; 死亡订阅.Remove(dead); 宗门任务.记录击杀(owner, id); };
            npc.Died += callback; 死亡订阅[npc] = callback;
        }
    }
    void 本波结束(SpawnZone zone) { zone.停止刷怪(false); }
    public void 清理()
    {
        foreach (var pair in 死亡订阅) if (pair.Key != null) pair.Key.Died -= pair.Value;
        死亡订阅.Clear();
        if (区 != null) { 区.刷出了 -= 刷出; 区.清空 -= 本波结束; 区.停止刷怪(); Destroy(区.gameObject); }
        if (本批 != null) Destroy(本批); 区 = null; 本批 = null; 所属 = null;
    }
    void OnDestroy() { 清理(); }
}
