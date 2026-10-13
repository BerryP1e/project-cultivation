using UnityEngine;

/// <summary>擦身慢镜：使用同一世界时钟保持所有生物、飞弹与命中节点同步；以真实时间退出。</summary>
[DefaultExecutionOrder(-200)]
public sealed class DodgeNearMissFocus : MonoBehaviour
{
    static DodgeNearMissFocus instance;
    PlayerFlightDodge owner;
    float started, duration, factor, zoom, baseline, fixedBaseline, applied;
    bool ownsClock;
    public static bool 正在慢放 => instance && instance.ownsClock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static bool 触发(PlayerFlightDodge player, float seconds, float speed, float closeup)
    {
        if (!player || !player.伤害免疫 || seconds <= 0 || Time.timeScale <= 0 || UiEscRegistry.SceneInputBlocked || 正在慢放) return false;
        if (!instance)
        {
            var go = new GameObject("DodgeNearMissFocus_Runtime");
            instance = go.AddComponent<DodgeNearMissFocus>();
            DontDestroyOnLoad(go);
        }
        instance.owner = player;
        instance.started = Time.unscaledTime;
        instance.duration = Mathf.Clamp(seconds, .05f, .5f);
        instance.factor = Mathf.Clamp(speed, .05f, 1);
        instance.zoom = Mathf.Clamp(closeup, 0, .2f);
        instance.baseline = Time.timeScale;
        instance.fixedBaseline = Time.fixedDeltaTime;
        instance.ownsClock = true;
        instance.Apply(instance.factor);
        return true;
    }

    public static float 镜头倍率(Transform target)
    {
        if (!正在慢放 || !instance.owner || target != instance.owner.transform) return 1;
        float age = Time.unscaledTime - instance.started;
        float envelope = Mathf.SmoothStep(0, 1, age / .035f)
            * Mathf.SmoothStep(0, 1, (instance.duration - age) / .12f);
        return 1 - instance.zoom * envelope;
    }

    public static void 取消(PlayerFlightDodge player)
    {
        if (instance && instance.owner == player) instance.Release();
    }

    void Update()
    {
        if (!ownsClock) return;
        // 暂停/切场景等流程有更高优先级；不把外部设置的0或其他倍率覆盖回去。
        if (!Mathf.Approximately(Time.timeScale, applied) || !owner || !owner.isActiveAndEnabled
            || !owner.GetComponent<PlayerController>() || !owner.GetComponent<PlayerController>().isActiveAndEnabled || UiEscRegistry.SceneInputBlocked
            || 黑幕字幕.演出中 || DialogueUI.正在显示)
        { Release(); return; }
        float age = Time.unscaledTime - started;
        if (age >= duration) { Release(); return; }
        Apply(Mathf.Lerp(factor, 1, Mathf.SmoothStep(0, 1, (age - duration + .08f) / .08f)));
    }

    void Apply(float multiplier)
    {
        applied = baseline * multiplier;
        Time.timeScale = applied;
        Time.fixedDeltaTime = fixedBaseline * multiplier;
    }

    void Release()
    {
        if (!ownsClock) return;
        if (Mathf.Approximately(Time.timeScale, applied)) Time.timeScale = baseline;
        if (Mathf.Approximately(Time.fixedDeltaTime, fixedBaseline * applied / baseline)) Time.fixedDeltaTime = fixedBaseline;
        ownsClock = false; owner = null;
    }
    void OnDisable() => Release();
    void OnDestroy() { Release(); if (instance == this) instance = null; }
}
