#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>独立Play验收用指针，驱动原StandaloneInputModule；不进入玩家构建。</summary>
public sealed class UIValidationPointerInput : BaseInput
{
    public Vector2 Position;
    public bool Down,Pressed,Up;
    public override bool mousePresent=>true;
    public override bool touchSupported=>false;
    public override Vector2 mousePosition=>Position;
    public override Vector2 mouseScrollDelta=>Vector2.zero;
    public override bool GetMouseButtonDown(int button)=>button==0&&Down;
    public override bool GetMouseButton(int button)=>button==0&&Pressed;
    public override bool GetMouseButtonUp(int button)=>button==0&&Up;
}

#endif
