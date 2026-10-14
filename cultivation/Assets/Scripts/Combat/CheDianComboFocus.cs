using UnityEngine;

/// <summary>吐息追击选择：包括玩家和龙在内的同一世界时钟；窗口使用真实秒数，暂停不消耗窗口。</summary>
[DefaultExecutionOrder(-250)]
public sealed class CheDianComboFocus : MonoBehaviour
{
    static CheDianComboFocus instance;
    CheDianYeTingCompanion owner;
    float baseline, fixedBaseline, applied, remaining;
    bool ownsClock, releasing;
    Transform cameraOwner, head;
    Vector3 headPoint;
    float cameraWeight;
    CheDianComboCue cue;
    public static bool Active => instance && instance.ownsClock && !instance.releasing;
    public static float Remaining => Active ? instance.remaining : 0;
    public static bool For(CheDianYeTingCompanion dragon) => Active && instance.owner == dragon;
    public static float CameraWeight => instance ? instance.cameraWeight : 0;
    public static Vector3 CameraPoint(Transform target, Vector3 ordinary)
        => instance && target == instance.cameraOwner ? Vector3.Lerp(ordinary,instance.headPoint,instance.cameraWeight*.85f) : ordinary;
    public static float CameraScale(Transform target)
        => instance && target == instance.cameraOwner ? Mathf.Lerp(1,.90f,instance.cameraWeight) : 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static bool Begin(CheDianYeTingCompanion dragon, float seconds, float multiplier)
    {
        if (!dragon || Time.timeScale <= 0 || UiEscRegistry.SceneInputBlocked || Active) return false;
        DodgeNearMissFocus.取消(dragon.GetComponent<PlayerFlightDodge>());
        if (!instance)
        {
            instance = new GameObject("CheDianComboFocus_Runtime").AddComponent<CheDianComboFocus>();
            DontDestroyOnLoad(instance.gameObject);
        }
        instance.owner = dragon;
        instance.baseline = Time.timeScale;
        instance.fixedBaseline = Time.fixedDeltaTime;
        instance.remaining = seconds;
        instance.applied = instance.baseline * Mathf.Clamp(multiplier, .05f, 1);
        instance.ownsClock = true;
        instance.releasing = false;
        instance.cameraOwner=dragon.transform;instance.head=dragon.Breath?.Head;
        instance.headPoint=instance.head?instance.head.position:dragon.Companion.transform.position+Vector3.up*2;
        if(instance.cue)Destroy(instance.cue.gameObject);
        instance.cue=new GameObject("CheDianComboCue_Runtime").AddComponent<CheDianComboCue>();instance.cue.Initialize(dragon);
        Time.timeScale = instance.applied;
        Time.fixedDeltaTime = instance.fixedBaseline * instance.applied / instance.baseline;
        return true;
    }

    public static void Cancel(CheDianYeTingCompanion dragon)
    {
        if (instance && instance.owner == dragon) instance.Release();
    }
    public static void Confirm(CheDianYeTingCompanion dragon)
    {
        if(instance && instance.owner==dragon){instance.Release();instance.cue?.Confirm();}
    }
    void Update()
    {
        if(head)headPoint=head.position;
        if(Time.timeScale>0)cameraWeight=Mathf.MoveTowards(cameraWeight,Active?1:0,Time.unscaledDeltaTime*(Active?4:3));
        if (!ownsClock) return;
        // 面板记住慢放倍率再暂停，关闭时会恢复该倍率。保留租约至恢复，避免遗留慢放。
        if (!owner || !owner.isActiveAndEnabled || !owner.Companion) releasing = true;
        if (Time.timeScale <= 0) return;
        if (releasing) { Release(); return; }
        if (!Mathf.Approximately(Time.timeScale, applied)) { Release(); return; }
        if (UiEscRegistry.SceneInputBlocked || DialogueUI.正在显示 || 黑幕字幕.演出中) return;
        remaining -= Time.unscaledDeltaTime;
        if (remaining <= 0) Release();
    }
    void Release()
    {
        if (!ownsClock) return;
        releasing = true;
        if (Time.timeScale <= 0) return;
        if (Mathf.Approximately(Time.timeScale, applied)) Time.timeScale = baseline;
        if (Mathf.Approximately(Time.fixedDeltaTime, fixedBaseline * applied / baseline)) Time.fixedDeltaTime = fixedBaseline;
        ownsClock = false; owner = null; remaining = 0;
    }
    void OnDestroy() { Release(); if(cue)Destroy(cue.gameObject); if (instance == this) instance = null; }
}
