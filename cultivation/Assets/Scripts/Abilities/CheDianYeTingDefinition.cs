using UnityEngine;

[CreateAssetMenu(menuName = "修仙/掣电烨霆伴龙配置")]
public sealed class CheDianYeTingDefinition : ScriptableObject
{
    public const string AbilityId = "ability_chedian_yeting";
    public const string ResourcePath = "Abilities/CheDianYeTing/Definition";
    public GameObject Companion;
    public Vector3 SideOffset = new Vector3(4.050743f, .03000614f, -.6459469f);
    public Vector3 FacingOffset;
    public Vector3 Scale = Vector3.one * 2.5f;
    public AnimationClip GroundIdle, GroundMove, Takeoff, FlapLoop, AirIdle, AirMove, Landing;
    public AnimationClip AirPowered, AirGlide;
    public AnimationClip GroundBreathStart, GroundBreathLoop, GroundBreathEnd, GroundBreathRepeat;
    public AnimationClip AirBreathStart, AirBreathLoop, AirBreathEnd, AirBreathRepeat;
    [Min(1)] public float TransitionSeconds = 1.6f;
    [Min(1)] public float MoveTransitionSeconds = 1.35f;
    [Header("随行AI：进入身侧范围后自行停留，不绑定玩家的位置")]
    public float FollowStartDistance = 1.65f;
    public float ArriveDistance = .65f;
    public float GroundRunSpeed = 2.7f;
    public float GroundCatchupSpeed = 3.8f;
    public float CatchupDistance = 7;
    public float Acceleration = 2.8f;
    public float Braking = 4.2f;
    public float GroundTurnDegrees = 130;
    public float AirSpeed = 10;
    public float AirTurnDegrees = 170;
    public float ObstacleRadius = .45f;
    public float ObstacleLookAhead = 1.4f;
    public float GroundReferenceSpeed = 1.35f;
    public float AirAnimationSpeed = .85f;
    public float AirHeightOffset = .15f;
    public float TeleportDistance = 28;
    [Header("独立感知与扑翼/滑翔")]
    public float ReactionDelay = .45f;
    public float PoweredFlightSeconds = 3.2f;
    public float GlideSeconds = 2.8f;
    public float FlapBurstSeconds = 1.65f;
    [Header("吐息：普通攻击0.87→2.39、2.39→5、3.4→2.39倒播")]
    public float BreathDuration = 1.725f;
    public float FirstWindupSeconds = .58f;
    public float RepeatWindupSeconds = .24f;
    public float RecoverySeconds = .7f;
    public float ReturnToGhostDelay = 0;
    public float ReturnToGhostSeconds = 1.6f;
    public float ManifestSeconds = .6f;
    public float RepeatRecoveryTime = 1.01f;
    public float BreathRadius = .85f;
    public Material BreathMaterial;
    public Material BreathPlumeMaterial, BreathDistortionMaterial;
    public GameObject BreathImpact;
    public float BreathDissolveSeconds = 1.3f;
    public float ComboWindowSeconds = 1.3f;
    [Range(.05f, 1)] public float ComboTimeScale = .18f;
    public Vector3 MouthOffset = new Vector3(-.22f, -.015f, 0);
    public bool Ready => Companion && GroundIdle && GroundMove && Takeoff && AirIdle && AirMove && Landing;
    public bool BreathReady => BreathMaterial && BreathPlumeMaterial && BreathDistortionMaterial && BreathImpact && GroundBreathStart && GroundBreathLoop && GroundBreathEnd && GroundBreathRepeat
        && AirBreathStart && AirBreathLoop && AirBreathEnd && AirBreathRepeat;
}
