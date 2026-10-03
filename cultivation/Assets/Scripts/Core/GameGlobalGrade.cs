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
    [Range(0.2f, 3f)] public float 曝光 = 1.14f;

    [Tooltip("Gain（高光乘算）。压高光就调小于 1")]
    public Color 高光 = Color.white;

    [Tooltip("Gamma（中间调）。1/γ 作用，小于 1 提亮中间调")]
    public Color 中间调 = Color.white;

    [Tooltip("Lift（暗部加算）。**把死黑抬起来**用这个，比整体提亮更自然。\n" +
             "⚠️ 别给太大 —— 它会把整个画面变成一片发灰/发红，反而更脏")]
    public Color 暗部 = new Color(0.055f, 0.058f, 0.062f, 0f);   // 水墨：抬黑位，暗部要变成淡墨而不是死黑

    [Header("色彩")]
    [Tooltip("对比度。⚠️ 注意：>1 是**压暗部、提亮部**，在本来就偏暗的场景里会显得更黑")]
    [Range(0.5f, 2f)] public float 对比度 = 0.97f;

    [Range(0f, 2f)] public float 饱和度 = 0.82f;    // 水墨：去饱和（别低于 0.7，会变灰泥）

    [Tooltip("染色目标色。配合 染色强度 用，做整体色调偏向（冷暖）")]
    public Color 染色 = new Color(0.88f, 0.93f, 0.95f, 1f);   // 水墨：偏青灰
    [Range(0f, 1f)] public float 染色强度 = 0.22f;

    [Header("水墨：墨分五色 / 留白 / 纸纹（2026-10-03 路线A）")]
    [Tooltip("墨分五色：把连续灰阶压成几档墨色。**0 = 关**。\n" +
             "水墨画只有几层墨，无限平滑的渐变看上去就是「3D 渲染」而不是画。")]
    [Range(0, 9)] public int 墨阶数 = 5;
    [Tooltip("墨阶强度：0 = 不压（原样），1 = 硬分层（3D 画面会很假，像坏了）。\n" +
             "0.3~0.45 是「有墨的层次，但不炸」。**觉得画面变脏就往下调**")]
    [Range(0f, 1f)] public float 墨阶强度 = 0.35f;

    [Tooltip("留白：亮到阈值以上的地方直接变成「纸」，而不是某个颜色。\n" +
             "天空/亮地会读成没画过的宣纸 —— 这是水墨最像水墨的一点")]
    [Range(0f, 1f)] public float 留白强度 = 0.25f;
    [Tooltip("留白的纸色（别用纯白，宣纸是暖白）")]
    public Color 纸色 = new Color(0.96f, 0.95f, 0.935f, 1f);
    [Tooltip("留白阈值：亮度超过它才算纸。调低 = 更多地方变纸（会吃掉细节）")]
    [Range(0.5f, 1f)] public float 留白阈值 = 0.86f;

    [Tooltip("宣纸纹理（可选）。**没填就自动去 Resources 取「默认纸纹」那张**；" +
             "连那张也没有才退回程序化纸纹 —— 不会有「忘了填资产就静默失效」的黑盒状态")]
    public Texture 纸纹;
    [Tooltip("自动加载的纸纹资源名（相对 Resources）。留空 = 不走自动加载。\n" +
             "用户 2026-10-03 提供的宣纸 → 已用 `修仙/美术/宣纸纸纹预处理` 生成归一化颗粒图放在 " +
             "`Assets/resources/宣纸/宣纸纹理_纸纹.png`")]
    public string 默认纸纹 = "宣纸/宣纸纹理_纸纹";
    [Tooltip("纸纹强度：0 = 关。0.25~0.45 有纸的颗粒感；太大就是噪点")]
    [Range(0f, 1f)] public float 纸纹强度 = 0.35f;
    [Tooltip("纸纹平铺次数（屏幕空间）。纸是不动的，所以 UV 用屏幕坐标。\n" +
             "**1 = 整屏一张纸**（原图不是无缝的，铺开了会看到镜像接缝）；要更细的颗粒再往上加")]
    [Range(0.5f, 40f)] public float 纸纹平铺 = 1f;
    [Tooltip("纸纹对比（增益）。**归一化过的纸纹（默认那张）用 2 左右**；\n" +
             "生图（没跑过预处理）要放大很多才看得见 —— 那种情况建议先去跑 `修仙/美术/宣纸纸纹预处理`")]
    [Range(0.2f, 40f)] public float 纸纹对比 = 2f;
    [Tooltip("纸纹是不是已归一化（`修仙/美术/宣纸纸纹预处理` 的产物：中点 0.5、±2σ ≈ ±0.25）。\n" +
             "勾上 = 直接取一次；**不勾 = 运行时做高通**（给没预处理的生图兜底）")]
    public bool 纸纹已归一化 = true;

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
    static readonly int ID_墨阶数 = Shader.PropertyToID("_InkLevels");
    static readonly int ID_墨阶强度 = Shader.PropertyToID("_InkAmount");
    static readonly int ID_纸色 = Shader.PropertyToID("_PaperWhite");
    static readonly int ID_留白强度 = Shader.PropertyToID("_PaperAmount");
    static readonly int ID_留白阈值 = Shader.PropertyToID("_PaperThr");
    static readonly int ID_纸纹 = Shader.PropertyToID("_GrainTex");
    static readonly int ID_纸纹强度 = Shader.PropertyToID("_GrainAmount");
    static readonly int ID_纸纹平铺 = Shader.PropertyToID("_GrainTiling");
    static readonly int ID_有纸纹 = Shader.PropertyToID("_GrainHasTex");
    static readonly int ID_纸纹对比 = Shader.PropertyToID("_GrainGain");
    static readonly int ID_纸纹归一 = Shader.PropertyToID("_GrainPre");

    void OnEnable() { 自动装纸纹(); 确保材质(); }
    void OnDisable() { if (材质 != null) { DestroyImmediate(材质); 材质 = null; } }

    /// <summary>
    /// 纸纹没手动指定时，自动从 Resources 取默认那张（用户提供的宣纸）。
    /// **本组件是运行时由 `场景自举` 补到相机上的** ⇒ 序列化引用存不进场景，
    /// 默认值只能靠"按资源名加载"这条路，不能指望 Inspector 里拖一下。
    /// </summary>
    void 自动装纸纹()
    {
        if (纸纹 != null || string.IsNullOrEmpty(默认纸纹)) return;
        纸纹 = Resources.Load<Texture>(默认纸纹);
        if (纸纹 != null) Debug.Log("[全局调色] 已自动装纸纹：" + 默认纸纹 + "（" + 纸纹.width + "x" + 纸纹.height + "）");
        else Debug.LogWarning("[全局调色] Resources 里找不到纸纹「" + 默认纸纹 + "」—— 会退回程序化纸纹兜底");
    }

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
        材质.SetFloat(ID_墨阶数, 墨阶数);
        材质.SetFloat(ID_墨阶强度, 墨阶强度);
        材质.SetColor(ID_纸色, 纸色);
        材质.SetFloat(ID_留白强度, 留白强度);
        材质.SetFloat(ID_留白阈值, 留白阈值);
        材质.SetFloat(ID_纸纹强度, 纸纹强度);
        材质.SetFloat(ID_纸纹平铺, 纸纹平铺);
        材质.SetFloat(ID_纸纹对比, 纸纹对比);
        材质.SetFloat(ID_纸纹归一, 纸纹已归一化 ? 1f : 0f);
        材质.SetFloat(ID_有纸纹, 纸纹 != null ? 1f : 0f);
        if (纸纹 != null) 材质.SetTexture(ID_纸纹, 纸纹);
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
