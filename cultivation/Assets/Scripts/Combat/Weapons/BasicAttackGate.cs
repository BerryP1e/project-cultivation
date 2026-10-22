using UnityEngine;

/// <summary>两套普攻独立轮转，共享角色动作占用，避免同时按键产生双重结算。</summary>
public static class BasicAttackGate
{
    public static bool OtherAttackRunning(MonoBehaviour requester)
    {
        foreach(var c in requester.GetComponents<BasicJiuba01>())if(c!=requester&&c.enabled&&c.出手动作中)return true;
        var remote=requester.GetComponent<BasicRemoteAttack01>();if(remote&&remote!=requester&&remote.enabled&&remote.出手动作中)return true;
        var thunder=requester.GetComponent<BasicThunder01>();if(thunder&&thunder!=requester&&thunder.enabled&&thunder.出手动作中)return true;
        var frost=requester.GetComponent<BasicFrostSpike01>();return frost&&frost!=requester&&frost.enabled&&frost.出手动作中;
    }
}
