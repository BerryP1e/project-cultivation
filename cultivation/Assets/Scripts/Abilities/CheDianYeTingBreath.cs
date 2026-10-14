using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>单轮储备快吐息。每次续段仅由当前选择窗口的即时按键触发，无队列、无起落等待。</summary>
public sealed class CheDianYeTingBreath : IDisposable
{
    public enum Phase { None, Prepare, Fire, Recovery, RepeatPrepare, Choice }
    struct Request { public ActiveSkillCaster caster; public ActiveDivineAbility ability; public NpcInstance target; public Vector3 point; public bool aimed; }
    readonly CheDianYeTingCompanion owner;
    readonly Transform head,body;
    Request current;
    CheDianBreathVisual visual;
    readonly List<CheDianBreathVisual> retiring=new List<CheDianBreathVisual>();
    readonly Stack<CheDianBreathVisual> pooled=new Stack<CheDianBreathVisual>();
    readonly RaycastHit[] sceneryHits=new RaycastHit[32];
    readonly Collider[] targets=new Collider[64];
    float elapsed,nextTick;
    bool air,aimedShot;
    Vector3 direction,firePoint;
    public Phase State {get;private set;}
    public bool Busy => State!=Phase.None;
    public bool BlocksMovement => false; // Player keeps locomotion under the upper-body channel.
    public int Queued => 0;
    public int RemainingShots {get;private set;}
    public bool ComboWindow => State==Phase.Choice&&RemainingShots>0&&CheDianComboFocus.For(owner);
    public float ComboRemaining => ComboWindow?CheDianComboFocus.Remaining:0;
    public int Fired {get;private set;}
    public int ShotIndex {get;private set;}
    public Vector3 AimPoint => State==Phase.Fire||State==Phase.Choice?firePoint:Destination();
    public int DamageTicks {get;private set;}
    public Vector3 Mouth => head?head.TransformPoint(owner.Definition.MouthOffset):owner.Companion.transform.position+Vector3.up;
    public Transform Head => head;
    public Vector3 BeamEnd {get;private set;}
    public bool AirBreath => air;
    public GameObject Visual => visual?.Root;
    public int RetiringWaves => retiring.Count;
    public CheDianYeTingBreath(CheDianYeTingCompanion owner){this.owner=owner;foreach(var bone in owner.Companion.GetComponentsInChildren<Transform>(true)){if(bone.name=="Bip001 Head")head=bone;if(bone.name=="Bip001")body=bone;}}
    public bool TryStart(ActiveSkillCaster caster,ActiveDivineAbility ability,NpcInstance target,Vector3? point,int reserve)
    {
        if(!caster||caster.transform!=owner.transform||!ability||!owner.Definition.BreathReady||Time.timeScale<=0||UiEscRegistry.SceneInputBlocked)return false;
        if(Busy&&!ComboWindow)return false;
        if(!Busy&&reserve<1)return false;
        if(!Busy&&!owner.PlayerChannel.Begin())return false;
        var request=new Request{caster=caster,ability=ability,target=target,aimed=target||point.HasValue,point=point??(target?AbilityVfxUtility.命中点(target.transform):owner.Companion.transform.position+owner.transform.forward*Mathf.Max(5,ability.范围))};
        if(Busy)
        {
            // Confirm NOW. Closing the focus restores the clock and the dark mask immediately.
            RemainingShots--;ShotIndex++;CheDianComboFocus.Confirm(owner);RetireBeam();current=request;Begin(true);
        }
        else {RemainingShots=Mathf.Clamp(reserve,1,4)-1;ShotIndex=1;current=request;Begin(false);}
        return true;
    }
    AnimationClip Clip(Phase phase)
    {
        var d=owner.Definition;
        if(phase==Phase.Prepare)return air?d.AirBreathStart:d.GroundBreathStart;
        if(phase==Phase.RepeatPrepare)return air?d.AirBreathRepeat:d.GroundBreathRepeat;
        if(phase==Phase.Fire)return air?d.AirBreathLoop:d.GroundBreathLoop;
        return air?d.AirBreathEnd:d.GroundBreathEnd;
    }
    float WindupDuration(Phase phase)=>phase==Phase.RepeatPrepare?owner.Definition.RepeatWindupSeconds:owner.Definition.FirstWindupSeconds;
    void Begin(bool repeat)
    {
        owner.Deploy(Destination(),!repeat);air=owner.Airborne;
        owner.PlayerChannel.Prepare(repeat);
        visual?.Dispose();visual=pooled.Count>0?pooled.Pop():new CheDianBreathVisual(owner.Definition);
        visual.SetTier(ShotIndex);visual.Root.SetActive(true);visual.ShowCharge(Mouth,0);
        SetPhase(repeat?Phase.RepeatPrepare:Phase.Prepare,repeat?.10f:.12f);
    }
    void RetireBeam(){if(visual!=null){visual.BeginDissolve();retiring.Add(visual);visual=null;}}
    void SetPhase(Phase next,float blend)
    {
        State=next;elapsed=0;
        float duration=next==Phase.Prepare||next==Phase.RepeatPrepare?WindupDuration(next):next==Phase.Recovery?owner.Definition.RecoverySeconds:0;
        owner.PlayBreathClip(Clip(next),blend,duration);
        if(next==Phase.Recovery){RetireBeam();owner.StartReturn();owner.PlayerChannel.Hold();}
        if(next==Phase.Fire){owner.PlayerChannel.Fire();Camera.main?.GetComponent<TopDownCamera>()?.BeginBreathShake(owner.transform,owner.Definition.BreathDuration,ShotIndex);aimedShot=current.aimed;firePoint=Destination();direction=(firePoint-Mouth).normalized;if(direction.sqrMagnitude<.5f)direction=owner.Companion.transform.forward;nextTick=0;Fired++;}
    }
    Vector3 Destination()=>current.target&&!current.target.IsDead?AbilityVfxUtility.命中点(current.target.transform):current.point;
    bool CanFollowup()
    {
        if(RemainingShots<=0||!current.caster.生命.够灵气(current.ability.消耗灵力))return false;
        for(int i=0;i<6;i++)if(current.caster.SlotContent(i)==current.ability)return true;
        return false;
    }
    public void Tick(float dt)
    {
        if(!Busy)return;
        if(!current.caster||!current.caster.isActiveAndEnabled||!current.caster.生命||current.caster.生命.IsDead){Cancel();return;}
        elapsed+=dt;
        if(State==Phase.Prepare||State==Phase.RepeatPrepare){if(elapsed>=WindupDuration(State))SetPhase(Phase.Fire,.10f);}
        else if(State==Phase.Fire&&elapsed>=owner.Definition.BreathDuration)
        {
            if(CanFollowup()&&CheDianComboFocus.Begin(owner,owner.Definition.ComboWindowSeconds,owner.Definition.ComboTimeScale))SetPhase(Phase.Choice,.25f);
            else SetPhase(Phase.Recovery,.22f);
        }
        else if(State==Phase.Choice){if(!CheDianComboFocus.For(owner))SetPhase(Phase.Recovery,.22f);}
        else if(State==Phase.Recovery&&elapsed>=owner.Definition.RecoverySeconds){State=Phase.None;RemainingShots=0;visual?.Dispose();visual=null;owner.PlayerChannel.End();owner.EndBreathMotion();}
    }
    public void AfterAnimation(float dt)
    {
        if (!Busy || visual == null) return;
        if (State == Phase.Prepare || State == Phase.RepeatPrepare)
        {
            visual.ShowCharge(Mouth, Mathf.Clamp01(elapsed / WindupDuration(State))); return;
        }
        if (State == Phase.Recovery) { visual.Fade(Mathf.Clamp01(1 - elapsed / .24f)); return; }
        // The destination freezes at launch; compensate mouth recoil, never chase a moving target.
        if (body)
        {
            float recoil = elapsed < .28f ? .07f*Mathf.Sin(Mathf.PI*elapsed/.28f) : 0;
            recoil += .012f*Mathf.Max(0,Mathf.Sin(elapsed*Mathf.PI*2*11));
            body.localPosition -= body.parent.InverseTransformVector(direction*recoil);
        }
        if (head)
        {
            direction = (firePoint - Mouth).normalized;
            var aim = Quaternion.FromToRotation(head.TransformDirection(Vector3.left), direction) * head.rotation;
            head.rotation = Quaternion.Slerp(head.rotation, aim, .92f);
            head.rotation = Quaternion.AngleAxis(Mathf.Sin(elapsed*Mathf.PI*2*11)*.8f,direction)*head.rotation;
        }
        var origin = Mouth;
        direction = (firePoint - origin).normalized;
        float length = Mathf.Max(1, current.ability.范围);
        if (aimedShot) length = Mathf.Min(length, Vector3.Distance(origin, firePoint));
        // Opaque scenery cuts both the rendered beam and its hit volume. Other creatures do not block it.
        int obstacleCount=Physics.RaycastNonAlloc(origin,direction,sceneryHits,length,~0,QueryTriggerInteraction.Ignore);
        var obstacles=obstacleCount==sceneryHits.Length?Physics.RaycastAll(origin,direction,length,~0,QueryTriggerInteraction.Ignore):sceneryHits;
        if(obstacles!=sceneryHits)obstacleCount=obstacles.Length;
        for(int i=0;i<obstacleCount;i++)
        {
            var hit=obstacles[i];
            if (!hit.collider || hit.collider.transform.IsChildOf(owner.transform) || hit.collider.GetComponentInParent<NpcInstance>() || hit.collider.GetComponentInParent<PlayerVitals>()) continue;
            length = Mathf.Min(length, hit.distance);
        }
        if (State != Phase.Choice) length = Mathf.Min(length, Mathf.Max(.05f, elapsed*95));
        BeamEnd = origin + direction * length;
        visual.ShowBeam(origin, BeamEnd, Mathf.Clamp01(elapsed / .10f));
        if (State == Phase.Choice) return; // Visible last plume, no additional damage during the choice.
        if (elapsed >= .16f) visual.Hit(BeamEnd);
        if (elapsed + .0001f < nextTick) return;
        nextTick = elapsed + Mathf.Max(.05f, current.ability.伤害间隔);
        if (!current.caster.战斗属性) return;
        var batch = new CombatHitBatch();
        var spec = new AttackSpec(current.ability.伤害属性, current.ability.攻击类别, false, current.ability.伤害倍率);
        long attackId = CombatDamagePipeline.新攻击序号();
        int targetCount=Physics.OverlapCapsuleNonAlloc(origin,BeamEnd,owner.Definition.BreathRadius,targets,current.caster.敌人层,QueryTriggerInteraction.Ignore);
        var contacts=targetCount==targets.Length?Physics.OverlapCapsule(origin,BeamEnd,owner.Definition.BreathRadius,current.caster.敌人层,QueryTriggerInteraction.Ignore):targets;
        if(contacts!=targets)targetCount=contacts.Length;
        for(int i=0;i<targetCount;i++)
        {
            var collider=contacts[i];
            var npc = collider.GetComponentInParent<NpcInstance>();
            if (!npc || npc.IsDead || (!npc.是敌对目标 && npc != current.target)) continue;
            var point = origin + direction * Mathf.Clamp(Vector3.Dot(collider.bounds.center - origin, direction), 0, length);
            var context = new CombatHitContext(current.caster.战斗属性, spec, owner, current.ability.神通id, point, direction, DamageTicks, attackId);
            var hit = batch.命中(new NpcTarget(npc), context);
            if (hit.有效接触) visual.Hit(point);
        }
        DamageTicks++;
    }
    public void Cancel(){CheDianComboFocus.Cancel(owner);owner.PlayerChannel.End();bool active=Busy;State=Phase.None;RemainingShots=0;visual?.Dispose();visual=null;foreach(var old in retiring)old.Dispose();retiring.Clear();if(active&&owner&&owner.Companion)owner.EndBreathMotion();}
    public void TickVisuals(float dt){visual?.Tick(dt);for(int i=retiring.Count-1;i>=0;i--){retiring[i].Tick(dt);if(retiring[i].Ended){retiring[i].Root.SetActive(false);pooled.Push(retiring[i]);retiring.RemoveAt(i);}}}
    public void TrimPool(){while(pooled.Count>1)pooled.Pop().Dispose();}
    public void Dispose(){CheDianComboFocus.Cancel(owner);owner.PlayerChannel.End();State=Phase.None;RemainingShots=0;visual?.Dispose();visual=null;foreach(var old in retiring)old.Dispose();retiring.Clear();while(pooled.Count>0)pooled.Pop().Dispose();}
}
