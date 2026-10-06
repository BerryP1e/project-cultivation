#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;

/// <summary>Play 内验证收服时序和存档迁移；不写用户存档或场景。</summary>
public class ZhenyaohuChecks : MonoBehaviour
{
    [Serializable] public class Report { public List<string> passed=new List<string>(); public List<string> failed=new List<string>(); }
    readonly Report report=new Report();
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    const BindingFlags StaticPrivate=BindingFlags.NonPublic|BindingFlags.Static;
    public static void Begin() {
        if(!Application.isPlaying) throw new InvalidOperationException("先进入 Play");
        new GameObject("镇妖葫流程自检").AddComponent<ZhenyaohuChecks>().StartCoroutine(Run());
    }
    static IEnumerator Run() { var self=FindObjectOfType<ZhenyaohuChecks>(); return self.Checks(); }
    void Check(bool condition,string name) { (condition?report.passed:report.failed).Add(name); Debug.Log("[镇妖葫自检] "+(condition?"PASS ":"FAIL ")+name); }
    void ResetCooldown(TreasureCaster caster) => typeof(TreasureCaster).GetField("readyAt",Private).SetValue(caster,0f);
    NpcInstance Spawn(NpcDefinition definition,Vector3 at) {
        var parent=new GameObject("镇妖葫测试NPC父节点"); parent.SetActive(false);
        var go=Instantiate(NpcPrefabs.加载(definition.模型资源路径),parent.transform);
        go.name="镇妖葫测试目标"; go.transform.position=at;
        var npc=go.GetComponent<NpcInstance>(); npc.定义=definition;
        foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true)) if(!(script is NpcInstance) && !(script is NpcAnimator)) script.enabled=false;
        parent.SetActive(true);
        foreach(var script in go.GetComponentsInChildren<MonoBehaviour>(true)) if(!(script is NpcInstance) && !(script is NpcAnimator)) script.enabled=false;
        npc.ResetHealth(); typeof(NpcInstance).GetField("当前气血",Private).SetValue(npc,10000f);
        return npc;
    }
    IEnumerator Checks() {
        var player=GameObject.Find("Player"); var caster=player.GetComponent<TreasureCaster>();
        var targeter=player.GetComponent<NpcTargeting>(); var data=FindObjectOfType<UIPanelData>();
        var ui=FindObjectOfType<CharacterPanelUI>(true); ui.SetOpen(false);
        var originalTreasure=data.当前法宝; var originalSpirits=new List<NpcDefinition>(data.已获得真灵);
        var originalFormation=new List<NpcDefinition>(data.战阵站位);
        var cultivation=player.GetComponent<PlayerCultivation>(); string cultivationJson=cultivation!=null?JsonUtility.ToJson(cultivation):null;
        var originalLocked=targeter.Locked;
        var definition=PanelDatabase.取().真灵.First(d=>d!=null && d.类型==NpcKind.妖魔 && !d.死亡后立即重生 && NpcPrefabs.加载(d.模型资源路径)!=null);
        var treasure=data.法宝.First(d=>d.法宝id==UIPanelData.镇妖葫id);
        NpcInstance npc=null; var temps=new List<GameObject>();
        try {
            yield return null;
            data.设置当前法宝(null); Check(!caster.TryCast(),"未装备不能使用");
            data.设置当前法宝(treasure); targeter.ClearLock(); yield return null;
            Check(!caster.TryCast(),"未锁定不能使用");
            npc=Spawn(definition,player.transform.position+player.transform.forward*3); temps.Add(npc.transform.parent.gameObject);
            targeter.LockNpc(npc); yield return null;
            ui.SetOpen(true); Check(!caster.TryCast(),"界面打开阻止法宝施放"); ui.SetOpen(false); yield return null;
            Check(caster.TryCast(),"E 槽释放已装备法宝");
            yield return new WaitForSeconds(.85f);
            Check(caster.CurrentPhase==TreasureCaster.Phase.Siphoning,"抛出放大后持续收服");
            var delta=npc.transform.position+Vector3.up*.5f-caster.Gourd.position;
            Check(Vector3.Dot(caster.Gourd.up,delta.normalized)>.90f,"瓶口指向锁定目标");
            var effect=GameObject.Find("镇妖葫_Teleport_3_大端到小端");
            Check(effect!=null && effect.GetComponentsInChildren<ParticleSystem>().All(p=>p.main.simulationSpace==ParticleSystemSimulationSpace.Local && p.main.gravityModifier.constant==0),"Teleport_3 环沿目标至瓶口方向运动");
            if(effect!=null) {
                var ps=effect.GetComponent<ParticleSystem>();
                float length=Vector3.Distance(effect.transform.position,caster.Mouth);
                Check(Mathf.Abs(ps.velocityOverLifetime.z.constant*ps.main.startLifetime.constant-length)<.01f,"粒子收缩终点严格接瓶口");
            }
            yield return new WaitForSeconds(1.1f);
            Check(caster.DamageTicks>=1 && npc.CurrentHealth<10000,"每秒造成实际微弱伤害");
            npc.TakeDamage(100000,true);
            Check(caster.CurrentPhase==TreasureCaster.Phase.Absorbing,"其他攻击击杀已受葫芦伤害的目标也能收服");
            var soul=GameObject.Find("真灵_吸入瓶口"); Check(soul!=null && soul.GetComponentsInChildren<MeshRenderer>().Length>0,"死亡模型已定格用于缩小吸入");
            yield return new WaitForSeconds(2.2f);
            Check(caster.CurrentPhase==TreasureCaster.Phase.Idle && caster.Gourd==null,"葫芦摇晃后返回玩家并消失");
            Check(data.已获得真灵.Exists(d=>d.id==definition.id),"捕获真灵出现在玩家持有列表");
            Check(!data.收服真灵(definition),"重复收服不会重复授予真灵");
            int acquired=data.已获得真灵.Count; data.EnsureLists(); data.EnsureLists();
            Check(data.已获得真灵.Count==acquired,"目录刷新不覆盖捕获记录");
            ResetCooldown(caster); npc=Spawn(definition,player.transform.position+player.transform.forward*3); temps.Add(npc.transform.parent.gameObject); targeter.LockNpc(npc); yield return null;
            Check(caster.TryCast(),"第二次释放成功"); npc.TakeDamage(100000,true);
            yield return new WaitForSeconds(.85f);
            Check(caster.CurrentPhase==TreasureCaster.Phase.Idle && caster.DamageTicks==0 && data.已获得真灵.Count==acquired,"未造成伤害前死亡不授予真灵");
            ResetCooldown(caster); npc=Spawn(definition,player.transform.position+player.transform.forward*3); temps.Add(npc.transform.parent.gameObject); targeter.LockNpc(npc); yield return null;
            typeof(NpcInstance).GetField("当前气血",Private).SetValue(npc,.001f);
            caster.TryCast(); yield return new WaitForSeconds(1.9f);
            Check(npc.IsDead && caster.DamageTicks==1 && caster.CurrentPhase==TreasureCaster.Phase.Absorbing,"葫芦首次伤害直接击杀也能触发收服");
            yield return new WaitForSeconds(2);
            ResetCooldown(caster); npc=Spawn(definition,player.transform.position+player.transform.forward*3); temps.Add(npc.transform.parent.gameObject); targeter.LockNpc(npc); yield return null; caster.TryCast();
            yield return new WaitForSeconds(.8f); data.设置当前法宝(null); yield return new WaitForSeconds(.85f);
            Check(caster.CurrentPhase==TreasureCaster.Phase.Idle && !npc.IsDead,"取消装备安全回收法宝");
            data.设置当前法宝(treasure);
            var saved=new SaveData(); typeof(SaveSystem).GetMethod("采集面板",StaticPrivate).Invoke(null,new object[]{saved,data});
            saved=JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved));
            var fresh=new GameObject("法宝存档测试面板"); fresh.SetActive(false); temps.Add(fresh); var restored=fresh.AddComponent<UIPanelData>(); restored.EnsureLists();
            typeof(SaveSystem).GetMethod("恢复面板",StaticPrivate).Invoke(null,new object[]{saved,restored});
            Check(restored.当前法宝==treasure && restored.已拥有(treasure) && restored.已获得真灵.Exists(d=>d.id==definition.id),"JSON 存档往返保留法宝装备和已捕获真灵");
            var legacy=new SaveData {版本=16,收服系统已初始化=false}; legacy.已获得真灵.Add(definition.id); legacy.战阵站位.Add(definition.id);
            typeof(SaveSystem).GetMethod("恢复面板",StaticPrivate).Invoke(null,new object[]{legacy,restored});
            Check(restored.已获得真灵.Count==0 && restored.战阵站位.All(d=>d==null) && restored.当前法宝==null && restored.已拥有(treasure),"旧存档赠送真灵清空一次，保留新法宝供装备");
            var snapshot=typeof(跨场景数据).GetNestedType("快照",BindingFlags.NonPublic); var fields=snapshot.GetFields(BindingFlags.Public|BindingFlags.Static);
            var backup=fields.ToDictionary(f=>f,f=>f.GetValue(null));
            try {
                data.战阵站位[0]=definition;
                snapshot.GetMethod("拍下").Invoke(null,new object[]{data}); snapshot.GetMethod("灌回").Invoke(null,new object[]{restored});
                Check(restored.当前法宝==treasure && restored.已获得真灵.Exists(d=>d.id==definition.id) && restored.战阵站位[0]==definition,"跨场景接力保留法宝、真灵和战阵站位");
                bool relay=跨场景数据.正在接力;
                try {
                    跨场景数据.正在接力=true;
                    typeof(SaveSystem).GetMethod("恢复面板",StaticPrivate).Invoke(null,new object[]{legacy,restored});
                    Check(restored.当前法宝==treasure && restored.已获得真灵.Exists(d=>d.id==definition.id) && restored.战阵站位[0]==definition,"切场景时旧存档不会覆盖刚捕获的真灵和装备");
                } finally { 跨场景数据.正在接力=relay; }
            } finally { foreach(var pair in backup) pair.Key.SetValue(null,pair.Value); }
        } finally {
            caster.enabled=false; caster.enabled=true;
            data.当前法宝=originalTreasure; data.已获得真灵=originalSpirits; data.战阵站位=originalFormation; data.RaiseChanged();
            targeter.ClearLock(); if(originalLocked!=null) targeter.LockNpc(originalLocked);
            if(cultivation!=null) JsonUtility.FromJsonOverwrite(cultivationJson,cultivation);
            foreach(var go in temps) if(go!=null) Destroy(go);
            File.WriteAllText("../.dsh/_diag/gourd-checks.json",JsonUtility.ToJson(report,true));
            Debug.Log("[镇妖葫自检] 完成："+report.passed.Count+" 通过，"+report.failed.Count+" 失败；未写入用户存档。"); Destroy(gameObject);
        }
    }
}

#endif
