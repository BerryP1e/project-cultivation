using UnityEngine;

/// <summary>
/// **只在编辑器里可见** —— 挂在点位标记 / 触发区这类「给策划摆位用」的物件上。
///
/// 编辑器里（Scene / Game 视图）照常显示，**一进 Play 就把所有 Renderer 关掉**，
/// 所以实机观感干净，但摆位置时又看得见。不需要改材质、不影响任何逻辑。
/// </summary>
[DisallowMultipleComponent]
public class 仅编辑器可见 : MonoBehaviour
{
    [Tooltip("勾上 = 进 Play 就隐藏（默认）。关掉 = 一直显示")]
    public bool 进游戏就隐藏 = true;

    [Tooltip("隐藏时顺便把碰撞体也关掉（一般不需要：这些物件本来就没碰撞体）")]
    public bool 顺便关碰撞体 = false;

    void Awake()
    {
        if (!进游戏就隐藏 || !Application.isPlaying) return;
        应用();
    }

    void OnEnable()
    {
        if (进游戏就隐藏 && Application.isPlaying) 应用();
    }

    public void 应用()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        if (顺便关碰撞体)
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }

    // ---- ASCII 别名 ----
    public bool hideInGame { get => 进游戏就隐藏; set => 进游戏就隐藏 = value; }
}
