using UnityEngine;

public enum FlightDodgeDirection { Left, Right, Back, Forward }

/// <summary>四向御风闪避；前闪有独立的前倾旋身，持械变体保留各自握持。</summary>
[CreateAssetMenu(menuName = "修仙/御风闪避动作")]
public sealed class FlightDodgeAnimations : ScriptableObject
{
    public const string ResourcePath = "Abilities/YufengDodgeAnimations";
    [System.Serializable]
    public class Set
    {
        public string 功法id;
        public AnimationClip 左旋, 右旋, 后闪, 前旋;
        public AnimationClip 取(FlightDodgeDirection kind) => kind == FlightDodgeDirection.Forward ? 前旋 : kind == FlightDodgeDirection.Back ? 后闪 : kind == FlightDodgeDirection.Right ? 右旋 : 左旋;
    }
    public Set 自由手;
    public Set[] 持械;
    public AnimationClip 取(FlightDodgeDirection kind, string id)
    {
        if (持械 != null) foreach (var set in 持械) if (set != null && set.功法id == id) return set.取(kind);
        return 自由手 != null ? 自由手.取(kind) : null;
    }
}
