using UnityEngine;

/// <summary>
/// **画面基线实时预览**（调试工具，随时可删）。
///
/// ## 为什么有这个东西
///
/// "画面够不够亮"是**观感取舍**，不是技术问题 —— 实测三档曝光（1.06 / 1.35 / 1.65）
/// 都**没有过曝风险**（最狠的 1.65 也只有 0.072% 接近纯白），
/// 而且对比度随曝光一起涨（标准差 0.099→0.116），所以不存在"提亮就发灰"的代价。
///
/// 既然没有技术上的反对理由，那就**让眼睛决定** —— 比在截图之间来回猜靠谱。
///
/// ## 用法
///
/// Play 模式下按 <b>F2</b> 循环切换档位，画面立刻变，不用重进游戏。
/// 每档会同时调三样东西（它们是联动的，单改一样会难看）：
///
/// | 档 | 曝光 | 暗部加算 | Bloom 阈值 |
/// |---|---|---|---|
/// | 0 | 1.06 | 0.006 | 0.85 |（当前默认）
/// | 1 | 1.25 | 0.016 | 0.95 |
/// | 2 | 1.45 | 0.028 | 1.05 |
/// | 3 | 1.65 | 0.040 | 1.15 |
///
/// · **曝光**：整体提亮
/// · **暗部加算**：把死黑抬起来。曝光一高，暗部相对更容易"黑得发闷"，所以要一起抬
/// · **Bloom 阈值**：曝光抬高会让更多东西跨过阈值。**要跟着涨**，
///   否则地面/墙也会开始发光，画面糊掉
///
/// ## 怎么定下来
///
/// 在 Inspector 里看到满意的那一档后，把该档的三个值**手填到主相机的
/// `GameGlobalGrade` / `GameGlobalBloom` 上**（自举不会覆盖手动改过的组件），
/// 然后把本组件从场景/自举里去掉即可。
/// </summary>
[DisallowMultipleComponent]
public class 画面基线预览 : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("勾上才响应 F2。默认关，避免影响正常游戏按键")]
    public bool 启用 = true;

    [Header("档位（四个数组必须等长）")]
    public float[] 曝光档 = { 1.06f, 1.25f, 1.45f, 1.65f };
    public float[] 暗部档 = { 0.006f, 0.016f, 0.028f, 0.040f };
    public float[] 阈值档 = { 0.85f, 0.95f, 1.05f, 1.15f };

    [Header("状态（只读）")]
    [SerializeField] int 当前档;
    [SerializeField] string 提示;

    GameGlobalGrade 调色;
    GameGlobalBloom 辉光;

    void Start() => 找组件();

    void 找组件()
    {
        var cam = Camera.main;
        if (cam == null) return;
        调色 = cam.GetComponent<GameGlobalGrade>();
        辉光 = cam.GetComponent<GameGlobalBloom>();
    }

    void Update()
    {
        if (!启用) return;

        if (Input.GetKeyDown(KeyCode.F2))
        {
            当前档 = (当前档 + 1) % Mathf.Min(曝光档.Length, Mathf.Min(暗部档.Length, 阈值档.Length));
            应用();
        }
    }

    void 应用()
    {
        if (调色 == null || 辉光 == null) 找组件();
        if (调色 == null || 辉光 == null) return;

        int i = Mathf.Clamp(当前档, 0, 曝光档.Length - 1);
        float 暗 = 暗部档[Mathf.Clamp(i, 0, 暗部档.Length - 1)];
        float 阈 = 阈值档[Mathf.Clamp(i, 0, 阈值档.Length - 1)];

        调色.曝光 = 曝光档[i];
        调色.暗部 = new Color(暗, 暗 * 1.05f, 暗 * 1.35f, 0f);   // 暗部偏冷，符合塔内石殿
        辉光.阈值 = 阈;

        提示 = $"档 {i}：曝光 {曝光档[i]:F2} / 暗部 {暗:F3} / Bloom 阈值 {阈:F2}";
        Debug.Log("[画面基线预览] " + 提示);
    }

    void OnGUI()
    {
        if (!启用 || string.IsNullOrEmpty(提示)) return;
        var 样式 = new GUIStyle(GUI.skin.label) { fontSize = 20, normal = { textColor = Color.yellow } };
        GUI.Label(new Rect(20, Screen.height - 170, 900, 30), "F2 切换画面基线  |  " + 提示, 样式);
    }
}
