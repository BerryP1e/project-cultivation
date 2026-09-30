using UnityEngine;

/// <summary>
/// **临时调机位工具**：用 `【` / `】` 两个键让摄像机绕玩家在**水平面**旋转，
/// 保持当前**距离 / 高度 / 俯角(pitch)** 不变 —— 只改 <see cref="TopDownCamera.yaw"/>。
///
/// 用户 2026-09-27 的需求原话：
///   「【】【】这两个键可以用来控制摄像头以玩家为中心保持当前距离高度朝向玩家在水平面旋转」，
///   并且这是**暂时性**的 —— 他是要转到一个合适的角度，以后每个场景就用那个角度当摄像头默认位置。
///
/// 所以：
///   · 旋转时屏幕上会显示**当前 yaw**，转到满意的角度记下来，填进场景里 TopDownCamera 的 yaw 即可；
///   · 想彻底关掉这个工具，把本组件的 enabled 取消勾选（或删掉组件）就行。
///
/// 为什么读键盘要分两套（和 起名界面 同一个坑）：
///   `activeInputHandler = 2`（两套输入共存）时，旧 `Input.GetKey` 实测拿不到按键，
///   必须用新 Input System 的 `Keyboard.current`。而 InputSystem 包没解析完时
///   `UnityEngine.InputSystem` 编译不过，所以整段用 `#if ENABLE_INPUT_SYSTEM` 包住。
/// </summary>
[RequireComponent(typeof(TopDownCamera))]
public class CameraYawRotator : MonoBehaviour
{
    [Tooltip("按【】旋转的速度（度/秒）")]
    public float 旋转速度 = 90f;

    [Tooltip("每按一下转多少度（按住会按速度连续转）")]
    public float 每步角度 = 0f;

    [Tooltip("在屏幕左上角显示当前 yaw，方便抄下来当场景默认角度。\n\n" +
             "**默认关**（用户 2026-10-01：「那个【】调整摄像头转动的提示显示可以去掉了」）。\n" +
             "要用来调机位时手动勾上即可。")]
    public bool 显示角度 = false;

    TopDownCamera 相机;

    /// <summary>供外部/调试读取：当前 yaw</summary>
    public float 当前yaw => 相机 != null ? 相机.yaw : 0f;

    void Awake()
    {
        相机 = GetComponent<TopDownCamera>();
    }

    void Update()
    {
        if (相机 == null) return;

        float 方向 = 0f;      // -1 = 往左转，+1 = 往右转
        int 点按 = 0;         // 本帧是否"刚按下"（配合 每步角度 用）

#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            // 【 = 左方括号键，】 = 右方括号键
            if (kb.leftBracketKey.isPressed) 方向 -= 1f;
            if (kb.rightBracketKey.isPressed) 方向 += 1f;
            if (kb.leftBracketKey.wasPressedThisFrame) 点按 -= 1;
            if (kb.rightBracketKey.wasPressedThisFrame) 点按 += 1;
        }
#else
        // 老输入后端（activeInputHandler = 0/1）走这条
        if (Input.GetKey(KeyCode.LeftBracket)) 方向 -= 1f;
        if (Input.GetKey(KeyCode.RightBracket)) 方向 += 1f;
        if (Input.GetKeyDown(KeyCode.LeftBracket)) 点按 -= 1;
        if (Input.GetKeyDown(KeyCode.RightBracket)) 点按 += 1;
#endif

        if (方向 == 0f && 点按 == 0) return;

        float 变化;
        if (每步角度 > 0f && 点按 != 0) 变化 = 点按 * 每步角度;   // 阶梯式
        else 变化 = 方向 * 旋转速度 * Time.unscaledDeltaTime;     // 连续式

        if (Mathf.Approximately(变化, 0f)) return;

        // 只改 yaw：距离 / 高度 / pitch 一律不动，注视点仍是玩家
        相机.yaw = Mathf.Repeat(相机.yaw + 变化, 360f);
        相机.SnapToTarget();      // 立刻摆到位，转起来才跟手
    }

    void OnGUI()
    {
        if (!显示角度 || 相机 == null) return;
        var 旧 = GUI.skin.label.fontSize;
        GUI.skin.label.fontSize = 22;
        GUI.Label(new Rect(20, 16, 620, 34),
            string.Format("机位 yaw = {0:F1}°   （【 左转  /  】 右转）", 相机.yaw));
        GUI.skin.label.fontSize = 旧;
    }
}
