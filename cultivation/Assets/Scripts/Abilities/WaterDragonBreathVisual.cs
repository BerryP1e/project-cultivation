using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>倒放受击显形、攻击定格与跑步骨骼震颤；仅控制召唤龙的演出。</summary>
public sealed class WaterDragonBreathVisual : MonoBehaviour
{
    public AnimationClip 攻击动作, 被击动作, 跑步动作;
    public Transform 嘴部;
    [Range(0, 1)] public float 吐息定格进度 = .55f;
    [Range(0, .6f)] public float 震颤权重 = .3f;
    public float 震颤角度上限 = 8f;
    public float 震颤播放速度 = 2.4f;
    public float 显形过渡秒 = .32f;
    public float 消融秒 = .8f;
    enum Phase { Spawn, Blend, Attack, Breath, Dissolve, Done }
    Phase phase;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    AnimationClipPlayable hit, attack, run;
    Transform[] bones;
    Quaternion[] reference, sampled, offsets, exitOffsets;
    bool[] shaking;
    Renderer[] renderers;
    MaterialPropertyBlock block;
    float age, dissolve = 1f, dissolveFrom, visibility, visibilityFrom, attackTime, hitTime;
    bool initialized, placed, breathExit;
    public bool 配置有效 => 攻击动作 && 被击动作 && 跑步动作 && 嘴部;
    public bool 正在吐息 => initialized && phase == Phase.Breath;
    public bool 已消融 => phase == Phase.Done;
    public float 当前攻击进度 => 攻击动作 ? attackTime / 攻击动作.length : 0;
    public float 当前被击进度 => 被击动作 ? hitTime / 被击动作.length : 0;
    public float 吐息前伸距离 { get; private set; }
    public float 当前消融 => dissolve;
    public float 吐息预备时长 => Mathf.Max(.01f, 被击动作.length) + Mathf.Max(.01f, 显形过渡秒) + Mathf.Max(.01f, 攻击动作.length * 吐息定格进度 * Mathf.PI * .5f);
    public Vector3 炮口 => 嘴部 ? 嘴部.position : transform.position;

    public bool 初始化()
    {
        var animator = GetComponent<Animator>();
        if (!配置有效 || !animator || !animator.avatar || !animator.avatar.isValid) return false;
        animator.runtimeAnimatorController = null;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        bones = GetComponentsInChildren<Transform>(true);
        reference = new Quaternion[bones.Length];
        sampled = new Quaternion[bones.Length];
        offsets = new Quaternion[bones.Length];
        exitOffsets = new Quaternion[bones.Length];
        shaking = new bool[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            bones[i].gameObject.layer = 2;
            offsets[i] = Quaternion.identity;
            string n = bones[i].name;
            shaking[i] = n.StartsWith("Bip001") && n != "Bip001" && !n.Contains("脖子") && !n.Contains("下颚") && !n.Contains("Head") && !n.Contains("Neck") && !n.Contains("Footsteps") && !n.Contains("Nub");
        }
        renderers = GetComponentsInChildren<Renderer>(true);
        block = new MaterialPropertyBlock();
        graph = PlayableGraph.Create("WaterDragonBreath");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        mixer = AnimationMixerPlayable.Create(graph, 3);
        hit = Clip(被击动作, 0); attack = Clip(攻击动作, 1); run = Clip(跑步动作, 2);
        AnimationPlayableOutput.Create(graph, "Dragon", animator).SetSourcePlayable(mixer);
        graph.Play();
        Pose(0, 0, 1, 0, 0, 0);
        for (int i = 0; i < bones.Length; i++) reference[i] = bones[i].localRotation;
        Pose(0, 1, 0, 0, 攻击动作.length * 吐息定格进度, 0);
        吐息前伸距离 = Mathf.Max(0, Vector3.Dot(炮口 - transform.position, transform.forward));
        initialized = true; age = 0; phase = Phase.Spawn;
        Pose(1, 0, 0, 被击动作.length, 0, 0);
        Appearance(1, 0);
        return true;
    }

    AnimationClipPlayable Clip(AnimationClip clip, int input)
    {
        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetSpeed(0); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
        graph.Connect(playable, 0, mixer, input);
        return playable;
    }

    void Pose(float hitWeight, float attackWeight, float runWeight, float hitAt, float attackAt, float runTime)
    {
        mixer.SetInputWeight(0, hitWeight); mixer.SetInputWeight(1, attackWeight); mixer.SetInputWeight(2, runWeight);
        hitTime = hitAt; attackTime = attackAt;
        hit.SetTime(hitAt); attack.SetTime(attackAt); run.SetTime(runTime);
        graph.Evaluate(0);
    }

    public void 推进(float delta, Vector3 position, Quaternion rotation, float preparationDelta = -1f)
    {
        if (!initialized || phase == Phase.Done) return;
        bool preparing = phase == Phase.Spawn || phase == Phase.Blend || phase == Phase.Attack;
        age += Mathf.Max(0, preparing && preparationDelta >= 0 ? preparationDelta : delta);
        if (phase == Phase.Dissolve)
        {
            if (breathExit)
            {
                Pose(0, 1, 0, 0, 攻击动作.length * 吐息定格进度, 0);
                float settle = Ease(age / .25f);
                for (int i = 0; i < bones.Length; i++) if (shaking[i])
                    bones[i].localRotation *= Quaternion.Slerp(exitOffsets[i], Quaternion.identity, settle);
            }
            float p = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / Mathf.Max(.05f, 消融秒)));
            Appearance(Mathf.Lerp(dissolveFrom, 1, p), visibilityFrom * (1 - p));
            if (p >= 1) phase = Phase.Done;
            return;
        }
        if (!placed) { transform.SetPositionAndRotation(position, rotation); placed = true; }
        else
        {
            float follow = 1f - Mathf.Exp(-16f * Mathf.Max(0, delta));
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, follow), Quaternion.Slerp(transform.rotation, rotation, follow));
        }
        // 保留越过阶段边界的余时；显形、融合和攻击共用连续时间轴。
        float hitDuration = Mathf.Max(.01f, 被击动作.length);
        float blendDuration = Mathf.Max(.01f, 显形过渡秒);
        float holdTime = 攻击动作.length * 吐息定格进度;
        float attackDuration = Mathf.Max(.01f, holdTime * Mathf.PI * .5f);
        if (phase == Phase.Spawn && age + .00001f >= hitDuration) { phase = Phase.Blend; age = Mathf.Max(0, age - hitDuration); }
        if (phase == Phase.Blend && age + .00001f >= blendDuration) { phase = Phase.Attack; age = Mathf.Max(0, age - blendDuration); }
        if (phase == Phase.Attack && age + .00001f >= attackDuration) { phase = Phase.Breath; age = Mathf.Max(0, age - attackDuration); }
        if (phase == Phase.Spawn)
        {
            float smooth = Ease(age / hitDuration);
            Pose(1, 0, 0, (1 - smooth) * 被击动作.length, 0, 0);
            Appearance(1 - smooth, smooth);
        }
        else if (phase == Phase.Blend)
        {
            // 先融合两段的接合姿势，完成融合后才推进攻击，避免翅膀突然张开。
            float blend = Ease(age / blendDuration);
            Pose(1 - blend, blend, 0, 0, 0, 0);
            Appearance(0, 1);
        }
        else if (phase == Phase.Attack)
        {
            // 正弦时间映射使起步和定格时的速度连续，中段不超过源动作速度。
            float p = Mathf.Clamp01(age / attackDuration);
            float t = holdTime * .5f * (1f - Mathf.Cos(p * Mathf.PI));
            Pose(0, 1, 0, 0, t, 0);
            Appearance(0, 1);
        }
        else
        {
            Appearance(0, 1);
            // 取跑步相对首帧的旋转变化，小幅叠加，不让跑步姿势或根位移覆盖吐息定格。
            Pose(0, 0, 1, 0, 0, Mathf.Repeat(age * 震颤播放速度, 跑步动作.length));
            for (int i = 0; i < bones.Length; i++) sampled[i] = bones[i].localRotation;
            Pose(0, 1, 0, 0, 攻击动作.length * 吐息定格进度, 0);
            float weight = 震颤权重 * Ease(age / .25f);
            float damping = 1f - Mathf.Exp(-Mathf.Max(0, delta) / .07f);
            for (int i = 0; i < bones.Length; i++) if (shaking[i])
            {
                Quaternion offset = Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(reference[i]) * sampled[i], weight);
                offset = Quaternion.RotateTowards(Quaternion.identity, offset, 震颤角度上限);
                offsets[i] = Quaternion.Slerp(offsets[i], offset, damping);
                bones[i].localRotation *= offsets[i];
            }
        }
    }

    public void 结束()
    {
        if (!initialized || phase == Phase.Dissolve || phase == Phase.Done) return;
        breathExit = phase == Phase.Breath;
        if (breathExit) System.Array.Copy(offsets, exitOffsets, offsets.Length);
        dissolveFrom = dissolve; visibilityFrom = visibility; age = 0; phase = Phase.Dissolve;
    }
    static float Ease(float p)
    {
        p = Mathf.Clamp01(p);
        return p * p * p * (p * (p * 6f - 15f) + 10f);
    }
    void Appearance(float amount, float opacity)
    {
        dissolve = amount; visibility = opacity;
        foreach (var renderer in renderers) if (renderer)
        {
            renderer.GetPropertyBlock(block);
            block.SetFloat("_Dissolve", amount); block.SetFloat("_Visibility", opacity);
            renderer.SetPropertyBlock(block); block.Clear();
        }
    }
    void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
}
