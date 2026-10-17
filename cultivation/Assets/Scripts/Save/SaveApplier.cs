using UnityEngine;

/// <summary>每局只恢复一次角色数据；独立处理家园和塔内的场景落点。</summary>
[DefaultExecutionOrder(-40)]
public class SaveApplier : MonoBehaviour
{
    static bool applied;
    static Vector3 arrival;
    static int arrivalFrame = -1;
    public static bool 落点进行中_公开 => arrivalFrame >= 0;
    public const float 场景名最短停留 = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { applied = false; arrivalFrame = -1; }

    public static void 开始读取存档()
    {
        applied = false; arrivalFrame = -1;
        跨场景数据.正在接力 = false;
        跨场景数据.ClearSnapshot();
        TowerRealmTreasure.清除返回点();
    }

    void Start()
    {
        if (applied || SaveSystem.当前存档 == null) return;
        applied = true;
        SaveSystem.应用到角色(SaveSystem.当前存档);
    }

    public static string 取场景显示名(string scene)
    {
        if (scene == "3C_Testbed") return "个人洞府";
        if (scene == TowerRealmTreasure.塔场景) return "镇妖阁";
        if (scene == "Sect_Wilderness") return "宗门野外";
        return scene;
    }

    public static void 请求落点(Vector3 position, float seconds = 1.2f)
    {
        arrival = position; arrivalFrame = Time.frameCount;
    }

    void LateUpdate()
    {
        if (arrivalFrame < 0 || Time.frameCount == arrivalFrame) return;
        var cc = GetComponent<CharacterController>();
        bool enabled = cc && cc.enabled;
        if (enabled) cc.enabled = false;
        transform.position = arrival;
        TowerRealmTreasure.完成返回(transform);
        if (enabled) cc.enabled = true;
        Physics.SyncTransforms();
        Camera.main?.GetComponent<TopDownCamera>()?.SnapToTarget();
        arrivalFrame = -1;
    }
}
