using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>日常鬼火随行；停用的预热模型只在吐息期间启用动画和蒙皮。</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(180)]
[UnityEngine.Scripting.Preserve]
public sealed class CheDianYeTingCompanion : MonoBehaviour
{
    public enum MotionState { GroundIdle, GroundMove, Takeoff, AirIdle, AirMove, Landing }
    public CheDianYeTingDefinition Definition;
    public GameObject Companion => rig?.Root;
    public GameObject Ghost => manifestation?.Ghost;
    public bool DragonVisible => rig!=null && rig.Root.activeSelf;
    public MotionState State { get; private set; }
    public float MotionTime => rig?.Time ?? 0;
    public float BlendProgress => rig?.BlendProgress ?? 1;
    public bool Following { get; private set; }
    public Vector3 Velocity { get; private set; }
    public bool Gliding => false;
    public CheDianYeTingBreath Breath { get; private set; }
    public CheDianPlayerChannel PlayerChannel { get; private set; }
    public bool Airborne => State==MotionState.AirIdle;
    public bool FlightChangePending => false; // 无日常起落动作，不用等待它们才能吐息。
    CompanionRig rig;
    CheDianManifestation manifestation;
    PlayerVitals vitals;
    YufengFlight flight;
    Vector3 delayedAnchor, referencePosition, lastPlayer;
    float attackRate=1;
    bool returning;
    void Awake(){vitals=GetComponent<PlayerVitals>();}
    void OnEnable(){if(!Definition)Definition=Resources.Load<CheDianYeTingDefinition>(CheDianYeTingDefinition.ResourcePath);Spawn();}
    void OnDisable(){Clear();}
    void OnDestroy(){Clear();}
    void Clear(){Breath?.Dispose();Breath=null;PlayerChannel?.Dispose();PlayerChannel=null;manifestation?.Dispose();manifestation=null;rig?.Dispose();rig=null;}
    bool PlayerAir(){flight=GetComponent<YufengFlight>();return flight&&flight.enabled&&(flight.状态==YufengFlight.FlightState.御风||flight.状态==YufengFlight.FlightState.升空||flight.高度偏移>.6f);}
    Vector3 Anchor(){var p=transform.position+Quaternion.Euler(0,transform.eulerAngles.y,0)*new Vector3(Definition.SideOffset.x,0,Definition.SideOffset.z);p.y=transform.position.y+Definition.SideOffset.y;return p;}
    void Spawn()
    {
        if(rig!=null || !Definition || !Definition.BreathReady || !Definition.Companion || vitals&&vitals.IsDead)return;
        rig=new CompanionRig(Definition.Companion,Definition.Scale);
        State=PlayerAir()?MotionState.AirIdle:MotionState.GroundIdle;
        delayedAnchor=referencePosition=Anchor();lastPlayer=transform.position;rig.Root.transform.SetPositionAndRotation(delayedAnchor,transform.rotation*Quaternion.Euler(Definition.FacingOffset));
        rig.Switch(State==MotionState.AirIdle?Definition.AirIdle:Definition.GroundIdle,0);
        PlayerChannel=new CheDianPlayerChannel(this);Breath=new CheDianYeTingBreath(this);manifestation=new CheDianManifestation(rig.Root,Definition,Breath.Head);
        rig.SetActive(false);manifestation.ShowGhost(true);
    }
    public bool TryBreath(ActiveSkillCaster caster,ActiveDivineAbility ability,NpcInstance target,Vector3? point,int reserve=1)
        => isActiveAndEnabled && rig!=null && Breath.TryStart(caster,ability,target,point,reserve);
    public void Deploy(Vector3 destination,bool newRound)
    {
        if(newRound)State=PlayerAir()?MotionState.AirIdle:MotionState.GroundIdle;
        if(!DragonVisible)rig.Root.transform.position=referencePosition;
        // 鬼火的停留位置就是出手位置；转向发生于显现开始，不被旧行走/起落状态阻挡。
        var planar=destination-rig.Root.transform.position;planar.y=0;if(planar.sqrMagnitude>.01f)rig.Root.transform.rotation=Quaternion.LookRotation(planar)*Quaternion.Euler(Definition.FacingOffset);
        bool hidden=!DragonVisible;rig.SetActive(true);returning=false;manifestation.Reveal(hidden);
    }
    public void PlayBreathClip(AnimationClip clip,float blend,float duration=0){attackRate=duration>0?clip.length/duration:1;rig?.Switch(clip,blend);}
    public void StartReturn(){if(returning)return;returning=true;manifestation?.Dissolve();}
    public void EndBreathMotion(){StartReturn();attackRate=1;rig?.Switch(Airborne?Definition.AirIdle:Definition.GroundIdle,1f);}
    void LateUpdate()
    {
        if(vitals&&vitals.IsDead){Clear();return;}if(rig==null)Spawn();if(rig==null||Time.deltaTime<=0)return;
        float dt=Time.deltaTime;Breath.TickVisuals(dt);
        if(Vector3.Distance(transform.position,lastPlayer)>Definition.TeleportDistance){Breath.Cancel();rig.Root.transform.position=Anchor();delayedAnchor=referencePosition=Anchor();manifestation.HideImmediately();rig.SetActive(false);}
        lastPlayer=transform.position;
        if(Breath.Busy){Breath.Tick(dt);if(DragonVisible)rig.Advance(dt,attackRate);Breath.AfterAnimation(dt);}
        else
        {
            var goal=Anchor();delayedAnchor=Vector3.Lerp(delayedAnchor,goal,1-Mathf.Exp(-dt/Mathf.Max(.05f,Definition.ReactionDelay)));
            var previous=referencePosition;float gap=Vector3.Distance(delayedAnchor,previous);Following=gap>Definition.ArriveDistance;
            referencePosition=Vector3.MoveTowards(previous,delayedAnchor,Mathf.Min(Definition.AirSpeed,2+gap*1.7f)*dt);
            Velocity=(referencePosition-previous)/Mathf.Max(.0001f,dt);
            if(!DragonVisible)rig.Root.transform.position=referencePosition;
            if(DragonVisible)
            {
                rig.Advance(dt,1);StartReturn();
                if(returning && manifestation.Hidden){rig.SetActive(false);manifestation.ShowGhost(true);Breath.TrimPool();State=PlayerAir()?MotionState.AirIdle:MotionState.GroundIdle;}
            }
            else State=PlayerAir()?MotionState.AirIdle:MotionState.GroundIdle;
        }
        manifestation.Tick(dt,referencePosition+Vector3.up*1.4f);
        PlayerChannel.Tick(dt);
    }

    sealed class CompanionRig : IDisposable
    {
        struct Pose { public Vector3 p, s; public Quaternion q; public Pose(Transform t) { p = t.localPosition; s = t.localScale; q = t.localRotation; } }
        public GameObject Root { get; }
        public float Time { get; private set; }
        public float BlendProgress => blendDuration <= 0 ? 1 : Mathf.Clamp01(blendElapsed / blendDuration);
        readonly Transform[] bones, wings;
        readonly Pose[] rest, from, incoming, residual;
        readonly PlayableGraph graph;
        readonly AnimationPlayableOutput output;
        AnimationClipPlayable motion;
        AnimationClipPlayable outgoing;
        AnimationClip clip, outgoingClip;
        float outgoingTime, outgoingSpeed = 1, lastSpeed = 1;
        float blendElapsed, blendDuration;
        bool disposed;
        public CompanionRig(GameObject prefab, Vector3 scale)
        {
            Root = Instantiate(prefab); Root.name = "掣电烨霆_伴龙"; Root.transform.localScale = scale;
            foreach (var script in Root.GetComponentsInChildren<MonoBehaviour>(true)) { script.enabled = false; Destroy(script); }
            foreach (var c in Root.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Destroy(c); }
            foreach (var t in Root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
            foreach (var skin in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            var animator = Root.GetComponent<Animator>(); animator.runtimeAnimatorController = null; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind();
            bones = Root.GetComponentsInChildren<Transform>(true).Where(t => t != Root.transform).ToArray();
            wings = bones.Where(t => t.name.Contains("UpperArm") || t.name.Contains("Forearm") || t.name.EndsWith(" Hand")).ToArray();
            rest = bones.Select(t => new Pose(t)).ToArray(); from = new Pose[bones.Length]; incoming = new Pose[bones.Length]; residual = new Pose[bones.Length];
            graph = PlayableGraph.Create("CheDianYeTingCompanion"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Dragon", animator); graph.Play();
        }
        void ResetPose() { for (int i = 0; i < bones.Length; i++) { bones[i].localPosition = rest[i].p; bones[i].localRotation = rest[i].q; bones[i].localScale = rest[i].s; } }
        void Sample(float time) { Sample(motion, time); }
        void Sample(AnimationClipPlayable playable, float time) { ResetPose(); output.SetSourcePlayable(playable); playable.SetTime(time); graph.Evaluate(0); }
        public void Switch(AnimationClip next, float duration)
        {
            for (int i = 0; i < bones.Length; i++) from[i] = new Pose(bones[i]);
            var wingPose = wings.Select(t => t.localRotation).ToArray();
            if (outgoing.IsValid()) graph.DestroyPlayable(outgoing);
            outgoing = motion; outgoingClip = clip; outgoingTime = Time; outgoingSpeed = lastSpeed;
            clip = next; motion = AnimationClipPlayable.Create(graph, clip); motion.SetApplyFootIK(false); motion.SetApplyPlayableIK(false); output.SetSourcePlayable(motion);
            Time = 0;
            if (duration > 0 && clip.isLooping)
            {
                float best = float.MaxValue;
                // Pick matching wing phase before blending; a high wing must not suddenly mix into a downstroke.
                for (int candidate = 0; candidate < 24; candidate++)
                {
                    float time = clip.length * candidate / 24; Sample(time); float error = 0;
                    for (int i = 0; i < wings.Length; i++) error += Quaternion.Angle(wingPose[i], wings[i].localRotation);
                    if (error < best) { best = error; Time = time; }
                }
            }
            if (outgoing.IsValid())
            {
                Sample(outgoing, outgoingTime);
                for (int i = 0; i < bones.Length; i++) residual[i] = new Pose {
                    p = from[i].p - bones[i].localPosition, q = from[i].q * Quaternion.Inverse(bones[i].localRotation), s = from[i].s - bones[i].localScale };
            }
            blendDuration = duration; blendElapsed = 0; Apply();
        }
        public void SetActive(bool value) { Root.SetActive(value); if(value)graph.Play();else graph.Stop(); }
        public void Advance(float dt, float speed)
        {
            Time += dt * speed; if (clip.isLooping) Time = Mathf.Repeat(Time, clip.length); else Time = Mathf.Min(Time, clip.length);
            lastSpeed = speed;
            if (outgoing.IsValid()) { outgoingTime += dt * outgoingSpeed; outgoingTime = outgoingClip.isLooping ? Mathf.Repeat(outgoingTime, outgoingClip.length) : Mathf.Min(outgoingTime, outgoingClip.length); }
            blendElapsed += dt; Apply();
        }
        void Apply()
        {
            Sample(Time);
            if (blendDuration <= 0 || blendElapsed >= blendDuration * 1.1f)
            {
                if (outgoing.IsValid()) { graph.DestroyPlayable(outgoing); outgoing = default; }
                return;
            }
            for (int i = 0; i < bones.Length; i++) incoming[i] = new Pose(bones[i]);
            if (outgoing.IsValid()) Sample(outgoing, outgoingTime);
            for (int i = 0; i < bones.Length; i++)
            {
                bool wing = bones[i].name.Contains("UpperArm") || bones[i].name.Contains("Forearm") || bones[i].name.Contains("Finger") || bones[i].name.EndsWith(" Hand");
                bool head = bones[i].name.Contains("Head") || bones[i].name.Contains("Neck");
                float duration = blendDuration * (head ? 1.1f : wing ? .8f : 1);
                float weight = Mathf.SmoothStep(0, 1, Mathf.Clamp01(blendElapsed / duration));
                float correction = 1 - weight;
                var previous = outgoing.IsValid() ? new Pose {
                    p = bones[i].localPosition + residual[i].p * correction,
                    q = Quaternion.Slerp(Quaternion.identity, residual[i].q, correction) * bones[i].localRotation,
                    s = bones[i].localScale + residual[i].s * correction } : from[i];
                bones[i].localPosition = Vector3.Lerp(previous.p, incoming[i].p, weight);
                bones[i].localRotation = Quaternion.Slerp(previous.q, incoming[i].q, weight);
                bones[i].localScale = Vector3.Lerp(previous.s, incoming[i].s, weight);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (graph.IsValid()) graph.Destroy();
            if (Root) { Root.SetActive(false); if (Application.isPlaying) Destroy(Root); else DestroyImmediate(Root); }
        }
    }
}
