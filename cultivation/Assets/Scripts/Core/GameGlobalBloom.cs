using UnityEngine;

/// <summary>
/// **辉光（Bloom）后处理** —— Built-in 渲染管线，挂主相机。
///
/// ## 为什么加它
///
/// 这是"动漫感"里丢得最大的一块。法术、灯火、描边、金属高光 ——
/// **有辉光才叫"发光"，没有辉光就只是一块块死白的色斑**。
///
/// ## 实现要点（都在踩坑之后定的）
///
/// · **多 mip 链**：每级降一半分辨率，逐级模糊。这样"小而亮的核"和"大而柔的光"
///   能同时保留 —— 单级模糊要么糊成一团、要么范围不够。
/// · **升采样用 Blend One One，不是 shader 里相加**：
///   升采样是"把小图加到中图"，用 blend 做可以彻底避开"读自己写自己"的未定义行为。
///   这是 Unity PostProcessing 栈 Upsample 的标准做法。
/// · **亮部提取带"软膝"**：阈值附近平滑过渡。硬阈值会让辉光边界随画面抖动闪烁，
///   是 bloom 最常见的瑕疵。
/// · **临时 RT 全部 `Release()`**：不释放会一路泄漏显存，切场景几十次就爆。
///
/// ## 和调色的顺序
///
/// 相机 → **本组件（出辉光）** → `GameGlobalGrade`（调色） → 屏幕。
/// 顺序靠 `[DefaultExecutionOrder]` 保证：本组件必须在调色**之前**跑完，
/// 否则辉光不会被调色，会和画面脱节。
///
/// ## 挂载
///
/// 由 `场景自举.补玩家与相机()` 自动补到主相机，不写进场景（避免漂移）。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Camera))]
[ImageEffectAllowedInSceneView]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class GameGlobalBloom : MonoBehaviour
{
    [Header("总开关")]
    public bool 启用 = true;

    [Header("辉光")]
    [Tooltip("亮度阈值。**只有超过它的像素才会发光**。\n" +
             "调低 = 更多东西发光（容易糊）；调高 = 只有真正亮的东西发光")]
    [Range(0f, 4f)] public float 阈值 = 0.85f;

    [Tooltip("软膝：阈值附近过渡的柔和度。0 = 硬切（会闪烁）")]
    [Range(0f, 1f)] public float 软膝 = 0.5f;

    [Tooltip("辉光强度（叠加到画面上的量）。0 = 关")]
    [Range(0f, 4f)] public float 强度 = 0.9f;

    [Tooltip("每级模糊的采样半径倍率。越大越糊")]
    [Range(0f, 4f)] public float 模糊半径 = 1.0f;

    [Tooltip("降采样级数。越多辉光范围越大、开销也越大。\n" +
             "5 级 ≈ 覆盖画面 1/32 的尺度，够用了")]
    [Range(2, 7)] public int 级数 = 5;

    [Header("状态（只读）")]
    [SerializeField] bool 材质不可用;

    Material 材质;
    Shader 找过的Shader;
    RenderTexture[] 链;
    RenderTexture 累积A, 累积B;

    static readonly int ID_MainTex = Shader.PropertyToID("_MainTex");
    static readonly int ID_Source = Shader.PropertyToID("_Source");
    static readonly int ID_BloomTex = Shader.PropertyToID("_BloomTex");
    static readonly int ID_Threshold = Shader.PropertyToID("_Threshold");
    static readonly int ID_SoftKnee = Shader.PropertyToID("_SoftKnee");
    static readonly int ID_Intensity = Shader.PropertyToID("_Intensity");
    static readonly int ID_BlurRadius = Shader.PropertyToID("_BlurRadius");

    const int Pass亮部提取 = 0;
    const int Pass模糊 = 1;
    const int Pass升采样 = 2;
    const int Pass合成 = 3;

    void OnEnable() => 确保材质();

    void OnDisable()
    {
        释放();
        if (材质 != null) { DestroyImmediate(材质); 材质 = null; }
    }

    void 确保材质()
    {
        if (材质 != null || 材质不可用) return;
        if (找过的Shader == null) 找过的Shader = Shader.Find("Cultivation/Bloom");
        if (找过的Shader == null || !找过的Shader.isSupported)
        {
            材质不可用 = true;
            Debug.LogWarning("[辉光] 找不到或不支持 shader「Cultivation/Bloom」——辉光不会生效。"
                             + "检查 Assets/Environment/CultivationBloom.shader 有没有编译错误");
            return;
        }
        材质 = new Material(找过的Shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    /// <summary>释放所有临时 RT —— 不释放会一路泄漏显存</summary>
    void 释放()
    {
        if (链 != null)
        {
            for (int i = 0; i < 链.Length; i++)
            {
                if (链[i] != null) { 链[i].Release(); DestroyImmediate(链[i]); 链[i] = null; }
            }
            链 = null;
        }
        if (累积A != null) { 累积A.Release(); DestroyImmediate(累积A); 累积A = null; }
        if (累积B != null) { 累积B.Release(); DestroyImmediate(累积B); 累积B = null; }
    }

    /// <summary>按当前分辨率建 mip 链（尺寸变了会重建）</summary>
    bool 准备链(int 宽, int 高)
    {
        int n = Mathf.Clamp(级数, 2, 7);
        if (链 != null && 链.Length == n && 链[0] != null && 链[0].width == Mathf.Max(1, 宽 / 2))
            return true;

        释放();
        链 = new RenderTexture[n];
        int w = Mathf.Max(1, 宽 / 2), h = Mathf.Max(1, 高 / 2);
        for (int i = 0; i < n; i++)
        {
            var rt = new RenderTexture(Mathf.Max(1, w), Mathf.Max(1, h), 0, RenderTextureFormat.Default);
            rt.filterMode = FilterMode.Bilinear;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.hideFlags = HideFlags.HideAndDontSave;
            rt.Create();
            链[i] = rt;
            w = Mathf.Max(1, w / 2);
            h = Mathf.Max(1, h / 2);
        }
        int lw = Mathf.Max(1, 链[n - 1].width), lh = Mathf.Max(1, 链[n - 1].height);
        累积A = 建(lw, lh);
        累积B = 建(lw, lh);
        return true;
    }

    static RenderTexture 建(int w, int h)
    {
        var rt = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
        rt.filterMode = FilterMode.Bilinear;
        rt.wrapMode = TextureWrapMode.Clamp;
        rt.hideFlags = HideFlags.HideAndDontSave;
        rt.Create();
        return rt;
    }

    void 同步参数()
    {
        材质.SetFloat(ID_Threshold, 阈值);
        材质.SetFloat(ID_SoftKnee, 软膝);
        材质.SetFloat(ID_Intensity, 强度);
        材质.SetFloat(ID_BlurRadius, 模糊半径);
    }

    void OnRenderImage(RenderTexture 源, RenderTexture 目标)
    {
        // 关掉时**顺手释放临时 RT**：否则关了也一直占着显存。
        // 再打开时 准备链 会按需重建，不会因为释放出问题。
        if (!启用 || 强度 <= 0.0001f) { 释放(); Graphics.Blit(源, 目标); return; }

        确保材质();
        if (材质 == null) { Graphics.Blit(源, 目标); return; }

        准备链(源.width, 源.height);
        同步参数();

        int n = 链.Length;

        // ---- ① 亮部提取（同时降采样到 1/2） ----
        Graphics.Blit(源, 链[0], 材质, Pass亮部提取);

        // ---- ② 逐级降采样 + 双向模糊 ----
        for (int i = 1; i < n; i++)
        {
            Graphics.Blit(链[i - 1], 链[i]);                       // bilinear 降采样
            Graphics.Blit(链[i], 累积A, 材质, Pass模糊);            // 横
            Graphics.Blit(累积A, 链[i], 材质, Pass模糊);            // 纵
        }

        // ---- ③ 自小向大升采样累加（blend 加法，见 shader Pass 2 的说明） ----
        Graphics.Blit(链[n - 1], 累积A);
        for (int i = n - 2; i >= 0; i--)
        {
            材质.SetTexture(ID_Source, 链[i]);
            Graphics.Blit(累积A, 累积B, 材质, Pass升采样);
            // A / B 互换，下一轮把刚加好的结果当累积
            var t = 累积A; 累积A = 累积B; 累积B = t;
        }

        // ---- ④ 叠回原图 ----
        材质.SetTexture(ID_BloomTex, 累积A);
        Graphics.Blit(源, 目标, 材质, Pass合成);
    }

    // ---- ASCII 别名 ----
    public bool Enabled { get => 启用; set => 启用 = value; }
}
