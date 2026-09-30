using UnityEngine;

/// <summary>
/// **全局调色后处理**（Built-in 渲染管线，挂主相机）。
///
/// ## 它解决什么问题
///
/// 这个项目用了大量第三方资源（塔、地形、怪物、特效各来自不同包），
/// 各自的色彩/对比/明暗都不一样 —— 逐个改资产既慢又会把原资源改脏。
///
/// 所以加一层**在所有场景之后**的统一调色：曝光 / Lift-Gamma-Gain / 对比度 /
/// 饱和度 / 色调染色 / 暗角。改几个数就能把整场收敛到一个色调，
/// **不用重新导入任何资产**。
///
/// ## 为什么不用 URP 的 Volume / Post Processing Stack
///
/// 本工程是 **Built-in**（`GraphicsSettings.m_CustomRenderPipeline = null`），
/// 且**没装** `com.unity.postprocessing` / URP 包。
/// 引入 URP 要迁移全部材质与 shader，风险极大；装 PPSv2 包则依赖网络与包解析。
/// 自写 `OnRenderImage` 是 Built-in 下最省事、最可控、零依赖的路子。
///
/// ## 挂载
///
/// **由 `场景自举.补玩家与相机()` 在所有场景自动补到主相机上** ——
/// 不写进场景，避免"某个场景忘了挂"那种漂移。
/// 想单独调某个场景就手动把本组件加到那个相机上并改参数（自举不会覆盖已有组件）。
///
/// ## 和别的后处理的关系
///
/// 顺序：相机渲染 → **本组件** → 屏幕。
/// 所以它对**所有**来源的画面一视同仁（含特效、HUD 之外的世界内容）。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[ImageEffectAllowedInSceneView]
[DisallowMultipleComponent]
public class GameGlobalGrade : MonoBehaviour
{
    [Header("总开关")]
    [Tooltip("关掉 = 完全不做处理（排查「画面变了是不是它干的」时用）")]
    public bool 启用 = true;

    [Header("Tonemap（高光滚降）")]
    [Tooltip("把超过曝点的部分**压着滚下去**，而不是硬砍掉。\n" +
             "【为什么需要】原来最后是 `saturate()` —— 超过 1 直接砍，\n" +
             "塔里 2~3 强度的火点光一照就是**死白**、没有层次。\n" +
             "开了之后高光有滚降、亮部细节能留住。")]
    public bool Tonemap = true;

    [Tooltip("曲线强度。1 = 完全用曲线；建议 0.7~0.9 —— " +
             "给满会把整体压得偏灰，因为曲线本身是「压缩」的")]
    [Range(0f, 1f)] public float Tonemap强度 = 0.85f;

    [Tooltip("Hable 白点。**越大高光压得越狠**；11.2 是 Uncharted2 原参")]
    [Range(1f, 30f)] public float Tonemap白点 = 11.2f;

    [Header("曝光与明暗")]
    [Tooltip("整体曝光。>1 提亮。<b>觉得画面昏暗就调这个</b>")]
    [Range(0.2f, 3f)] public float 曝光 = 1.06f;

    [Tooltip("Gain（高光乘算）。压高光就调小于 1")]
    public Color 高光 = Color.white;

    [Tooltip("Gamma（中间调）。1/γ 作用，小于 1 提亮中间调")]
    public Color 中间调 = Color.white;

    [Tooltip("Lift（暗部加算）。**把死黑抬起来**用这个，比整体提亮更自然。\n" +
             "⚠️ 别给太大 —— 它会把整个画面变成一片发灰/发红，反而更脏")]
    public Color 暗部 = new Color(0.006f, 0.007f, 0.010f, 0f);

    [Header("色彩")]
    [Tooltip("对比度。⚠️ 注意：>1 是**压暗部、提亮部**，在本来就偏暗的场景里会显得更黑")]
    [Range(0.5f, 2f)] public float 对比度 = 1.0f;

    [Range(0f, 2f)] public float 饱和度 = 1.06f;

    [Tooltip("染色目标色。配合 染色强度 用，做整体色调偏向（冷暖）")]
    public Color 染色 = new Color(1f, 0.99f, 0.97f, 1f);
    [Range(0f, 1f)] public float 染色强度 = 0.06f;

    [Header("暗角")]
    [Tooltip("0 = 关。\n" +
             "⚠️ **默认关**：实测 0.35 就把画面压得很闷（用户 2026-10-01 的塔/宗门截图为证）。\n" +
             "要用的话建议 0.1~0.2，并且配合提亮一起调")]
    [Range(0f, 1.5f)] public float 暗角 = 0f;
    [Range(0.05f, 1f)] public float 暗角柔和度 = 0.45f;

    [Header("状态（只读）")]
    [Tooltip("材质创建失败时为真（说明 shader 没编译过）")]
    [SerializeField] bool 材质不可用;

    Material 材质;
    Shader 找过的Shader;

    static readonly int ID_TonemapOn = Shader.PropertyToID("_TonemapOn");
    static readonly int ID_Tonemap量 = Shader.PropertyToID("_TonemapAmount");
    static readonly int ID_Tonemap白 = Shader.PropertyToID("_TonemapWhite");
    static readonly int ID_曝光 = Shader.PropertyToID("_Exposure");
    static readonly int ID_高光 = Shader.PropertyToID("_Gain");
    static readonly int ID_中间调 = Shader.PropertyToID("_Gamma");
    static readonly int ID_暗部 = Shader.PropertyToID("_Lift");
    static readonly int ID_对比度 = Shader.PropertyToID("_Contrast");
    static readonly int ID_饱和度 = Shader.PropertyToID("_Saturation");
    static readonly int ID_染色 = Shader.PropertyToID("_Tint");
    static readonly int ID_染色强度 = Shader.PropertyToID("_TintAmount");
    static readonly int ID_暗角 = Shader.PropertyToID("_Vignette");
    static readonly int ID_暗角柔和 = Shader.PropertyToID("_VignetteSoft");

    void OnEnable() => 确保材质();
    void OnDisable() { if (材质 != null) { DestroyImmediate(材质); 材质 = null; } }

    void 确保材质()
    {
        if (材质 != null || 材质不可用) return;

        if (找过的Shader == null) 找过的Shader = Shader.Find("Cultivation/GlobalGrade");
        if (找过的Shader == null || !找过的Shader.isSupported)
        {
            材质不可用 = true;
            Debug.LogWarning("[全局调色] 找不到或不支持 shader「Cultivation/GlobalGrade」——"
                             + "后处理不会生效。检查 Assets/Environment/CultivationGlobalGrade.shader 有没有编译错误");
            return;
        }

        材质 = new Material(找过的Shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    void 同步参数()
    {
        if (材质 == null) return;
        材质.SetFloat(ID_TonemapOn, Tonemap ? 1f : 0f);
        材质.SetFloat(ID_Tonemap量, Tonemap强度);
        材质.SetFloat(ID_Tonemap白, Tonemap白点);
        材质.SetFloat(ID_曝光, 曝光);
        材质.SetColor(ID_高光, 高光);
        材质.SetColor(ID_中间调, 中间调);
        材质.SetColor(ID_暗部, 暗部);
        材质.SetFloat(ID_对比度, 对比度);
        材质.SetFloat(ID_饱和度, 饱和度);
        材质.SetColor(ID_染色, 染色);
        材质.SetFloat(ID_染色强度, 染色强度);
        材质.SetFloat(ID_暗角, 暗角);
        材质.SetFloat(ID_暗角柔和, 暗角柔和度);
    }

    void OnRenderImage(RenderTexture 源, RenderTexture 目标)
    {
        if (!启用) { Graphics.Blit(源, 目标); return; }

        确保材质();
        if (材质 == null) { Graphics.Blit(源, 目标); return; }

        同步参数();
        Graphics.Blit(源, 目标, 材质);
    }

    // ---- ASCII 别名 ----
    public bool Enabled { get => 启用; set => 启用 = value; }
}
