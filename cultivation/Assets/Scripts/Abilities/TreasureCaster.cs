using System.Collections.Generic;
using UnityEngine;

/// <summary>独立 E 键法宝槽；镇妖葫只收服本次施放中受到过葫芦伤害的原始目标。</summary>
[DisallowMultipleComponent]
public class TreasureCaster : MonoBehaviour
{
    public enum Phase { Idle, Outbound, Siphoning, Absorbing, Returning }
    public Phase CurrentPhase { get; private set; }
    public float CooldownRemaining => data != null && data.当前法宝 != null && data.当前法宝.法宝id==QingshanSwordTreasure.法宝id ? (GetComponent<QingshanSwordTreasure>()?.UltimateCooldown ?? 0) : data != null && data.当前法宝 == definition ? Mathf.Max(0, readyAt-Time.time) : 0f;
    public string CurrentSkillState => data != null && data.当前法宝 != null && data.当前法宝.法宝id==QingshanSwordTreasure.法宝id && (GetComponent<QingshanSwordTreasure>()?.UltimateState ?? QingshanSwordTreasure.UltimatePhase.Idle)!=QingshanSwordTreasure.UltimatePhase.Idle ? "万剑归宗" : "";
    public int DamageTicks { get; private set; }
    public Transform Gourd => gourd != null ? gourd.transform : null;
    public Vector3 Mouth => gourd != null ? gourd.transform.TransformPoint(definition.瓶口局部位置) : transform.position;
    UIPanelData data;
    NpcTargeting targeting;
    PlayerVitals vitals;
    PlayerCombatStats stats;
    TreasureDefinition definition;
    NpcInstance target;
    NpcDefinition captured;
    GameObject gourd, effect, soul;
    readonly List<Mesh> baked = new List<Mesh>();
    ParticleSystem mouthParticles;
    Mesh ringMesh;
    float age, nextTick, readyAt, releasedAt;
    bool ticking;
    Vector3 start, soulStart, flightEnd;
    Quaternion startRotation;
    Vector3 bodySize = Vector3.one;

    void Start() { Resolve(); if(GetComponent<QingshanSwordTreasure>()==null)gameObject.AddComponent<QingshanSwordTreasure>(); TreasureSkillHud.Attach(this); }
    void Resolve() {
        if(data == null) data=FindObjectOfType<UIPanelData>();
        if(targeting == null) targeting=GetComponent<NpcTargeting>();
        if(vitals == null) vitals=GetComponent<PlayerVitals>();
        if(stats == null) stats=GetComponent<PlayerCombatStats>();
    }
    void Update() {
        Resolve();
        bool allowed=!UiEscRegistry.SceneInputBlocked && !灵田摆放器.正在摆放 && !(GetComponent<演出锁>()?.正在锁 ?? false) && !(vitals != null && vitals.IsDead);
        if(Input.GetKeyDown(KeyCode.E) && allowed) {
            if(CurrentPhase == Phase.Idle || data!=null && data.当前法宝!=null && data.当前法宝.法宝id==QingshanSwordTreasure.法宝id) TryCast();
            else if(CurrentPhase == Phase.Siphoning || CurrentPhase == Phase.Outbound) BeginReturn();
        }
        if(CurrentPhase == Phase.Idle) return;
        if(gourd == null) { Cleanup(); return; }
        if(CurrentPhase != Phase.Returning && (vitals != null && vitals.IsDead || data == null || data.当前法宝 != definition)) BeginReturn();
        age += Time.deltaTime;
        if(CurrentPhase == Phase.Outbound) {
            if(target == null || target.IsDead) { BeginReturn(); return; }
            flightEnd=HoverPoint();
            float t=Mathf.Clamp01(age/.65f), ease=t*t*(3-2*t);
            gourd.transform.position=Vector3.Lerp(start,flightEnd,ease)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.6f;
            gourd.transform.localScale=Vector3.one*Mathf.Lerp(.22f,definition.放大比例,ease);
            gourd.transform.rotation=Quaternion.Slerp(startRotation,AimRotation(),ease);
            if(t>=1) { CurrentPhase=Phase.Siphoning; age=0; nextTick=Time.time+1; CreateEffect(); }
        } else if(CurrentPhase == Phase.Siphoning) {
            if(target == null || target.IsDead || Time.time-releasedAt>definition.最长施放时间 || Vector3.Distance(transform.position,target.transform.position)>definition.收服距离*1.25f) { BeginReturn(); return; }
            gourd.transform.position=Vector3.Lerp(gourd.transform.position,HoverPoint(),1-Mathf.Exp(-10*Time.deltaTime));
            gourd.transform.rotation=AimRotation()*Quaternion.Euler(Mathf.Sin(age*5)*1.5f,0,Mathf.Sin(age*3.3f)*2);
            AlignEffect();
            if(Time.time>=nextTick) {
                nextTick=Time.time+1;
                ticking=true;
                try { target.ReceiveAttack(stats,new AttackSpec(DamageNature.特殊,AttackKind.主动神通,true,definition.伤害倍率)); }
                finally { ticking=false; }
            }
        } else if(CurrentPhase == Phase.Absorbing) {
            if(soul != null) {
                float t=Mathf.Clamp01(age/1.05f), ease=t*t;
                soul.transform.position=Vector3.Lerp(soulStart,Mouth,ease)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.35f;
                soul.transform.localScale=Vector3.one*Mathf.Lerp(1,.015f,ease);
                soul.transform.Rotate(0,Time.deltaTime*160,0,Space.World);
                AlignEffect();
                if(t>=1) { Destroy(soul); soul=null; if(captured != null && data != null) data.收服真灵(captured); }
            }
            gourd.transform.rotation=startRotation*Quaternion.Euler(0,0,Mathf.Sin(age*32)*5*Mathf.Clamp01((age-1.05f)/.12f));
            if(age>=1.4f) BeginReturn();
        } else if(CurrentPhase == Phase.Returning) {
            float t=Mathf.Clamp01(age/.65f), ease=t*t*(3-2*t);
            gourd.transform.position=Vector3.Lerp(start,PlayerPoint(),ease)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.35f;
            gourd.transform.localScale=bodySize*Mathf.Lerp(1,.02f,ease);
            gourd.transform.rotation=Quaternion.Slerp(startRotation,transform.rotation,ease);
            if(t>=1) Cleanup();
        }
    }
    public bool TryCast() {
        Resolve();
        if(UiEscRegistry.SceneInputBlocked || 灵田摆放器.正在摆放 || (GetComponent<演出锁>()?.正在锁 ?? false) || vitals != null && vitals.IsDead) return false;
        if(data == null || data.当前法宝 == null || !data.已拥有(data.当前法宝)) { data?.ShowHint("先在法宝界面装备法宝"); return false; }
        if(data.当前法宝.法宝id==QingshanSwordTreasure.法宝id) { return GetComponent<QingshanSwordTreasure>()?.TryCastUltimate() ?? false; }
        if(data.当前法宝.法宝id!=UIPanelData.镇妖葫id || CurrentPhase!=Phase.Idle)return false;
        if(CooldownRemaining>0) { data.ShowHint("法宝尚在冷却"); return false; }
        var candidate=targeting != null ? targeting.Locked : null;
        if(candidate == null || candidate.IsDead || candidate.无敌 || candidate.定义 == null || candidate.定义.死亡后立即重生 || candidate.GetComponent<FormationSpirit>() != null) { data.ShowHint("请锁定一个可收服的目标"); return false; }
        var catalog=PanelDatabase.取();
        if(catalog == null || !catalog.真灵.Exists(d=>d != null && d.id == candidate.定义.id)) { data.ShowHint("此目标无法凝成真灵"); return false; }
        definition=data.当前法宝;
        if(Vector3.Distance(transform.position,candidate.transform.position)>definition.收服距离) { data.ShowHint("目标距离太远"); return false; }
        var prefab=definition.加载模型();
        if(prefab == null || stats == null) { data.ShowHint("法宝模型或战斗属性尚未就绪"); return false; }
        target=candidate; target.Damaged+=Damaged; target.Died+=Died;
        DamageTicks=0; captured=null; releasedAt=Time.time; readyAt=Time.time+definition.冷却时间;
        gourd=Instantiate(prefab,PlayerPoint(),transform.rotation); gourd.name="镇妖葫_收服";
        foreach(var c in gourd.GetComponentsInChildren<Collider>()) c.enabled=false;
        foreach(var r in gourd.GetComponentsInChildren<Rigidbody>()) { r.isKinematic=true; r.detectCollisions=false; }
        start=gourd.transform.position; startRotation=gourd.transform.rotation;
        gourd.transform.localScale=Vector3.one*.22f;
        age=0; CurrentPhase=Phase.Outbound;
        return true;
    }
    Vector3 PlayerPoint() => transform.position+Vector3.up*.9f;
    Vector3 TargetPoint() {
        if(target == null) return flightEnd;
        var rs=target.GetComponentsInChildren<Renderer>();
        var b=new Bounds(target.transform.position+Vector3.up*.8f,Vector3.zero); bool found=false;
        foreach(var r in rs) if(!(r is ParticleSystemRenderer) && r.enabled) { if(!found) { b=r.bounds; found=true; } else b.Encapsulate(r.bounds); }
        return b.center;
    }
    Vector3 HoverPoint() {
        Vector3 away=transform.position-target.transform.position; away.y=0;
        if(away.sqrMagnitude<.01f) away=-transform.forward;
        away.Normalize(); return TargetPoint()+away*2.65f+Vector3.Cross(Vector3.up,away)*.5f+Vector3.up*.65f;
    }
    Quaternion AimRotation() => Quaternion.FromToRotation(Vector3.up,(TargetPoint()-gourd.transform.position).normalized);
    void Damaged(NpcInstance npc,float amount) {
        if(npc != target || amount<=0) return;
        if(ticking) DamageTicks++;
        // Damaged 在死亡事件之前广播，先定格模型，避免 AI 的死亡隐藏/销毁影响吸入画面。
        if(DamageTicks>0 && CurrentPhase == Phase.Siphoning && npc.CurrentHealth<=0 && soul == null) {
            soul=Snapshot(npc.gameObject); soulStart=soul.transform.position;
        }
    }
    void Died(NpcInstance npc) {
        if(npc != target || CurrentPhase != Phase.Siphoning || DamageTicks<1) { BeginReturn(); return; }
        captured=npc.定义;
        if(soul == null) { soul=Snapshot(npc.gameObject); soulStart=soul.transform.position; }
        age=0; startRotation=gourd.transform.rotation; CurrentPhase=Phase.Absorbing;
        Unsubscribe();
    }
    GameObject Snapshot(GameObject original) {
        var root=new GameObject("真灵_吸入瓶口"); root.transform.position=TargetPoint();
        foreach(var r in original.GetComponentsInChildren<Renderer>()) {
            if(!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            Mesh mesh=null;
            if(r is SkinnedMeshRenderer skin) { mesh=new Mesh(); skin.BakeMesh(mesh); baked.Add(mesh); }
            else if(r is MeshRenderer) { var f=r.GetComponent<MeshFilter>(); if(f != null) mesh=f.sharedMesh; }
            if(mesh == null) continue;
            var part=new GameObject(r.name,typeof(MeshFilter),typeof(MeshRenderer));
            part.transform.SetParent(root.transform,false); part.transform.position=r.transform.position;
            part.transform.rotation=r.transform.rotation; part.transform.localScale=r.transform.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh=mesh; part.GetComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
            r.enabled=false;
        }
        return root;
    }
    void CreateEffect() {
        var prefab=Resources.Load<GameObject>("特效/传送/Prefab/Teleport_3");
        if(prefab == null) return;
        effect=Instantiate(prefab); effect.name="镇妖葫_Teleport_3_大端到小端";
        ringMesh=new Mesh { name="收服粒子环平面" };
        ringMesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
        ringMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up}; ringMesh.triangles=new[]{0,2,1,0,3,2}; ringMesh.RecalculateBounds();
        foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true)) {
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.localPosition=Vector3.zero; ps.transform.localRotation=Quaternion.identity; ps.transform.localScale=Vector3.one;
            var main=ps.main; main.simulationSpace=ParticleSystemSimulationSpace.Local; main.scalingMode=ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier=0; main.startSpeed=0; main.loop=true; main.maxParticles=64; main.startLifetime=1.15f; main.startSize=ps.name=="Teleport_3" ? 1.5f : 1.25f;
            var sh=ps.shape; sh.enabled=false;
            var em=ps.emission; em.SetBursts(new ParticleSystem.Burst[0]); em.rateOverTime=ps.name=="Teleport_3" ? 8 : ps.name=="Teleport" ? 6 : 4;
            var size=ps.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.8f,.15f),new Keyframe(1,.02f)));
            var renderer=ps.GetComponent<ParticleSystemRenderer>();
            if(ps.name=="Flare") {
                mouthParticles=ps; renderer.renderMode=ParticleSystemRenderMode.Billboard; main.startLifetime=.35f; main.startSize=.085f; em.rateOverTime=18;
                sh.enabled=true; sh.shapeType=ParticleSystemShapeType.Sphere; sh.radius=.09f;
                main.startSpeed=.15f;
            } else { renderer.renderMode=ParticleSystemRenderMode.Mesh; renderer.mesh=ringMesh; }
            ps.Play();
        }
        AlignEffect();
    }
    void AlignEffect() {
        if(effect == null) return;
        Vector3 large=CurrentPhase == Phase.Absorbing ? soulStart : TargetPoint();
        Vector3 delta=Mouth-large; float length=delta.magnitude;
        effect.transform.position=large; effect.transform.rotation=Quaternion.LookRotation(delta.sqrMagnitude>.001f?delta:Vector3.up);
        effect.transform.localScale=Vector3.one;
        foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>()) {
            if(ps == mouthParticles) { ps.transform.position=Mouth; continue; }
            var vel=ps.velocityOverLifetime; vel.enabled=true; vel.space=ParticleSystemSimulationSpace.Local;
            vel.x=0; vel.y=0; vel.z=length/ps.main.startLifetime.constant;
        }
    }
    void BeginReturn() {
        if(CurrentPhase == Phase.Idle || CurrentPhase == Phase.Returning) return;
        Unsubscribe(); captured=null;
        if(effect != null) Destroy(effect); effect=null;
        if(soul != null) Destroy(soul); soul=null;
        if(gourd == null) { Cleanup(); return; }
        start=gourd.transform.position; startRotation=gourd.transform.rotation; bodySize=gourd.transform.localScale;
        age=0; CurrentPhase=Phase.Returning;
    }
    void Unsubscribe() { if(target != null) { target.Damaged-=Damaged; target.Died-=Died; } }
    void Cleanup() {
        Unsubscribe(); target=null; captured=null;
        if(gourd != null) Destroy(gourd); if(effect != null) Destroy(effect); if(soul != null) Destroy(soul);
        foreach(var m in baked) if(m != null) Destroy(m); baked.Clear(); if(ringMesh != null) Destroy(ringMesh);
        gourd=null; effect=null; soul=null; ringMesh=null; CurrentPhase=Phase.Idle;
    }
    void OnDisable() => Cleanup();
}
