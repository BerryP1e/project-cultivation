using UnityEngine;

/// <summary>
/// 2.5D 俯视角跟随摄像机。
/// 机位角度恒定（yaw / pitch 不变，永不自转），只平滑跟随目标位置。
/// 场景里靠 OnValidate 实时预览：在 Inspector 里拖 yaw / pitch / distance 就能直接看到效果。
/// </summary>
[DefaultExecutionOrder(300)]
public class TopDownCamera : MonoBehaviour
{
    [Header("跟随目标")]
    [Tooltip("要跟随的角色。留空则自动找场景里名为 Player 的对象")]
    public Transform target;

    [Tooltip("注视点相对目标脚底的高度")]
    public float lookHeight = 1.2f;

    [Header("机位角度（恒定，不随角色旋转）")]
    [Range(5f, 89f)]
    [Tooltip("俯角。越大越接近正俯视，45~60 是常见 2.5D 区间。\n\n" +
             "用户 2026-10-01：「视角倾角有点太高了，希望它低个 10 度，稍微平一点」\n" +
             "→ 五个游玩场景从 **50 调到 40**（更平、更像平视）。")]
    public float pitch = 40f;

    [Tooltip("水平朝向。45 是经典斜 45 度 2.5D 视角")]
    public float yaw = 45f;

    [Header("距离")]
    [Tooltip("摄像机到注视点的距离。约 16 时角色占屏高 ~15%")]
    public float distance = 16f;

    [Header("跟随平滑")]
    [Tooltip("位置平滑时间（秒）。0 = 硬跟随，0.1~0.2 比较舒服")]
    public float smoothTime = 0.12f;

    [Tooltip("最大跟随速度，0 = 不限制（内部换算成 Infinity）")]
    public float maxFollowSpeed = 0f;

    [Header("投影方式")]
    [Tooltip("勾选=正交投影（更纯粹的 2.5D），取消=透视")]
    public bool orthographic = false;

    [Tooltip("正交模式下的视野高度")]
    public float orthographicSize = 14f;

    [Tooltip("透视模式下的视野角度")]
    [Range(10f, 90f)]
    public float fieldOfView = 40f;

    Camera cam;
    Vector3 followVelocity;
    Transform breathShakeOwner;
    Vector3 breathShakeOffset;
    float breathShakeAge,breathShakeDuration;
    float breathShakePower;
    public bool BreathShaking => breathShakeOwner && breathShakeAge<breathShakeDuration;
    public float BreathShakePower => BreathShaking?breathShakePower:0;
    public void BeginBreathShake(Transform source,float duration,int tier)
    {
        if(source!=target)return;
        breathShakeOwner=source;breathShakeAge=0;breathShakeDuration=duration;
        breathShakePower=Mathf.Clamp(tier,1,4)*.25f;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();

        if (target == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null) target = player.transform;
        }

        SnapToTarget();
    }

    // 放在 LateUpdate：保证在角色 Update 移动之后才取位置，避免抖动
    void LateUpdate()
    {
        if (target == null) return;

        // Keep the additive tremor out of the follow spring, so it cannot accumulate camera drift.
        transform.position-=breathShakeOffset;breathShakeOffset=Vector3.zero;

        ApplyProjection();

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        lookPoint=CheDianComboFocus.CameraPoint(target,lookPoint);
        Vector3 desired = lookPoint - rotation * Vector3.forward * distance;

        // 注意：SmoothDamp 内部按 maxSpeed * smoothTime 限制单次位移，
        // 传 0 会把位移钳成 0（摄像机完全不动）。0 必须换算成 Infinity。
        float maxSpeed = maxFollowSpeed > 0f ? maxFollowSpeed : Mathf.Infinity;

        transform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(transform.position, desired, ref followVelocity, smoothTime, maxSpeed, CheDianComboFocus.CameraWeight>0 && Time.timeScale>0 ? Time.unscaledDeltaTime : Time.deltaTime)
            : desired;

        transform.rotation = rotation;   // 方向恒定
        if(BreathShaking)
        {
            var dragon=breathShakeOwner.GetComponent<CheDianYeTingCompanion>();
            if(!dragon||!dragon.isActiveAndEnabled||dragon.Breath?.State!=CheDianYeTingBreath.Phase.Fire){breathShakeOwner=null;return;}
            breathShakeAge+=Time.deltaTime;
            float envelope=Mathf.Clamp01(breathShakeAge/.08f)*Mathf.Clamp01((breathShakeDuration-breathShakeAge)/.2f)*breathShakePower;
            float x=Mathf.Sin(breathShakeAge*82)*.035f+Mathf.Sin(breathShakeAge*121)*.012f;
            float y=Mathf.Sin(breathShakeAge*109)*.028f;
            breathShakeOffset=(rotation*Vector3.right*x+rotation*Vector3.up*y)*envelope;
            transform.position+=breathShakeOffset;
            transform.rotation=rotation*Quaternion.Euler(Mathf.Sin(breathShakeAge*91)*.08f*envelope,0,Mathf.Sin(breathShakeAge*73)*.07f*envelope);
        }
    }

    void ApplyProjection()
    {
        if (cam == null) return;
        cam.orthographic = orthographic;
        float focus = DodgeNearMissFocus.镜头倍率(target)*CheDianComboFocus.CameraScale(target);
        if (orthographic) cam.orthographicSize = orthographicSize * focus;
        else cam.fieldOfView = fieldOfView * focus;
    }

    /// <summary>立刻把摄像机摆到目标位置，不做平滑</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        breathShakeOffset=Vector3.zero;breathShakeOwner=null;

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = rotation;
        transform.position = target.position + Vector3.up * lookHeight - rotation * Vector3.forward * distance;
        followVelocity = Vector3.zero;
        ApplyProjection();
    }
    void OnDisable(){transform.position-=breathShakeOffset;breathShakeOffset=Vector3.zero;breathShakeOwner=null;}

    // 编辑期调参时实时预览，不用进 Play
    void OnValidate()
    {
        distance = Mathf.Max(0.1f, distance);
        if (cam == null) cam = GetComponent<Camera>();
        ApplyProjection();                                            // 改 fov / 正交 立刻生效
        if (!Application.isPlaying && target != null) SnapToTarget();  // 改角度 / 距离 立刻挪机位
    }
}
