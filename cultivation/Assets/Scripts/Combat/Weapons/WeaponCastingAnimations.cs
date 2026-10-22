using UnityEngine;

/// <summary>手持武器时使用同套持械施法动作；飞剑保持功法原动作。</summary>
public static class WeaponCastingAnimations
{
    static readonly string[] Sources={"atk_energy07","atk_energy04","atk_energy08","atk_energy05"};
    static readonly AnimationClip[] clips=new AnimationClip[4];
    public static AnimationClip Resolve(GameObject actor,int phase,AnimationClip original)
    {
        var data=actor.GetComponent<PlayerAbilityLoader>()?.面板数据;
        if(!data)data=Object.FindObjectOfType<UIPanelData>();
        if(!data||!data.当前武器||!data.当前武器.手持)return original;
        int index=Mathf.Clamp(phase,0,3);
        if(!clips[index])clips[index]=Resources.Load<AnimationClip>("Animation Library/Magical-Knight_Set/Animation/Humanoid/"+Sources[index]);
        return clips[index]?clips[index]:original;
    }
}
