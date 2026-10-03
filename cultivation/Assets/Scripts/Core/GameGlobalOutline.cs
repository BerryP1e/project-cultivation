using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// **屏幕空间角色描边**（Built-in 渲染管线）—— 和 `GameGlobalGrade`（调色）/ `GameGlobalBloom`（辉光）
/// 同一挂法：由 `场景自举` 补到**主相机**上，后处理顺序里排在最前（描边先画，再被辉光带出光晕、被调色统一色调）。
///
/// ## 怎么做的（一趟掩码 + 一趟合成）
///
/// ```
/// ① 掩码（CommandBuffer，挂在 CameraEvent.AfterForwardOpaque，每个角色 DrawRenderer 重画一次）
///      [Cultivation/OutlineMask] 一趟 MRT 写两张临时图：
///        RT0 = 相机空间法线(RGB) + 归一化线性深度(A)
///        RT1 = 对象组 ID（0 = 背景 / 1 = 玩家 / 2 = 其他角色）
/// ② 合成（OnRenderImage → Graphics.Blit）
///      [Cultivation/OutlineComposite] 沿 8 方向 × 2 半径采样上面两张图，
///      比较 深度 / 法线 / 组 ID 的差 ⇒ 在角色**内侧**画一圈线（宽 = 采样半径，单位像素）
/// ```
///
/// ## 为什么不用外扩网格（`OcclusionOutline` 用的那套）
///
/// 外扩网格要靠"沿法线把网格胀一圈、只画背面"—— 在**蒙皮网格**上宽度随骨骼缩放变化（我们的角色
/// 全是 Tripo 蒙皮），而且每个角色都要复制一份网格。屏幕空间这套线宽按像素算、角色变小也不糊，
/// 还能**按组给不同颜色**（玩家暖金 / 其他冷蓝）。
/// ⚠️ `OcclusionOutline`（玩家被挡住时穿墙显形）这轮**没动**：它管的是"看得见玩家"，
/// 不是风格描边。以后可以用同一趟掩码（那批物体改成 `ZTest Always` 再画一遍）把它并进来。
///
/// ## 性能
///
/// · 两张临时 RT（掩码 ARGBHalf + id ARGB32，都带 24 位深度，**不开 MSAA** —— 深度/法线边缘跟 MSAA 会打架）；
/// · 每个角色多画一遍（几十个 renderer 量级）；
/// · 合成一趟 16 次采样 × 2 图，1080p 桌面无压力。
/// 角色列表每 `列表刷新秒` 重收一次（`FindObjectsOfType` 不适合每帧跑）。
/// </summary>
[DisallowMultipleComponent]
public class GameGlobalOutline : MonoBehaviour
{
    [Header("总开关")]
    [Tooltip("关掉 = 完全不画（也不建临时 RT），画面和后处理链跟没加它一样。\n" +
             "⚠️ 默认关：这效果还没在真机画面上验证过（2026-10-03 写完只做到编译通过）。\n" +
             "在 Inspector 里勾上就能看效果，步骤见 docs/guides/描边.md")]
    public bool 启用 = false;

    [Header("描谁（对象组）")]
    [Tooltip("玩家所在组：暖金线")]
    public Color 玩家线色 = new Color(1.00f, 0.86f, 0.45f, 1f);
    [Tooltip("其他角色（NPC / 妖魔）所在组：冷蓝线")]
    public Color 其他线色 = new Color(0.55f, 0.75f, 1.00f, 1f);
    [Tooltip("其他角色要不要描（关掉就只描玩家一个人）")]
    public bool 描其他角色 = true;
    [Tooltip("死了的 NPC 还描不描")]
    public bool 描尸体 = false;
    [Tooltip("角色列表多久重收一次（秒）。场景里人不会天天变，别每帧收")]
    public float 列表刷新秒 = 2f;

    [Header("线的样子")]
    [Tooltip("采样半径（像素）= 线粗细。1~2 是细描边，3 以上偏风格化")]
    [Range(0.4f, 8f)] public float 粗细 = 1.6f;
    [Tooltip("边缘软化（抗锯齿）。太小斜边会有台阶")]
    [Range(0.01f, 1f)] public float 软化 = 0.45f;
    [Tooltip("线的强度")]
    [Range(0f, 1f)] public float 强度 = 0.9f;

    [Header("三个阈值（越大越只留最明显的边）")]
    [Tooltip("深度**相对**差阈值（远处物体的绝对差天生大，所以按相对值比）")]
    [Range(0.0005f, 0.3f)] public float 深度阈值 = 0.02f;
    [Tooltip("法线夹角阈值（1-dot）。默认 0.85 ≈ 80° ⇒ 只管轮廓，不管脸上的折痕")]
    [Range(0.01f, 2f)] public float 法线阈值 = 0.85f;
    [Tooltip("组 ID 阈值（不同组交界一定画线）")]
    [Range(0.001f, 1f)] public float ID阈值 = 0.02f;

    // ---- 组 ID（0 = 背景，别用 0；合成里拿它判"是不是角色"）----
    const float 玩家组 = 0.25f;
    const float 其他组 = 0.6f;

    Camera 相机;
    CommandBuffer 命令;
    Material 掩码材质, 合成材质;
    RenderTexture 法线深度图, ID图;
    int 上次宽, 上次高;

    class 条目 { public Renderer 渲染体; public Material 材质; }
    readonly List<条目> 玩家渲染体 = new List<条目>();
    readonly List<条目> 其他渲染体 = new List<条目>();
    float 下次收列表;

    void Awake() { 相机 = GetComponent<Camera>(); }

    void OnEnable()
    {
        相机 = 相机 != null ? 相机 : GetComponent<Camera>();
        if (相机 == null) { Debug.LogWarning("[描边] 不在相机上，功能不生效", this); return; }
        建材质();
        if (命令 == null)
        {
            命令 = new CommandBuffer { name = "角色描边·掩码" };
            相机.AddCommandBuffer(CameraEvent.AfterForwardOpaque, 命令);
        }
    }

    void OnDisable()
    {
        if (相机 != null && 命令 != null) 相机.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, 命令);
        命令 = null;
        释放RT();
    }

    void OnDestroy()
    {
        if (掩码材质 != null) Destroy(掩码材质);
        if (合成材质 != null) Destroy(合成材质);
    }

    void 建材质()
    {
        if (掩码材质 == null)
        {
            var s = Shader.Find("Cultivation/OutlineMask");
            if (s == null) { Debug.LogError("[描边] 找不到 shader「Cultivation/OutlineMask」（Assets/Shaders/OutlineMask.shader）"); return; }
            掩码材质 = new Material(s) { name = "描边·掩码（运行时）" };
        }
        if (合成材质 == null)
        {
            var s = Shader.Find("Cultivation/OutlineComposite");
            if (s == null) { Debug.LogError("[描边] 找不到 shader「Cultivation/OutlineComposite」（Assets/Shaders/OutlineComposite.shader）"); return; }
            合成材质 = new Material(s) { name = "描边·合成（运行时）" };
        }
    }

    /// <summary>收一遍"要描的人"：玩家（`PlayerVitals`）+ 所有 `NpcInstance`</summary>
    void 收列表()
    {
        玩家渲染体.Clear();
        其他渲染体.Clear();

        foreach (var pv in FindObjectsOfType<PlayerVitals>())
            收一个(pv.gameObject, 玩家渲染体, 玩家组);

        if (描其他角色)
        {
            foreach (var npc in FindObjectsOfType<NpcInstance>())
            {
                if (npc == null) continue;
                if (!描尸体 && npc.IsDead) continue;
                收一个(npc.gameObject, 其他渲染体, 其他组);
            }
        }
    }

    void 收一个(GameObject 根, List<条目> 放哪, float 组)
    {
        foreach (var r in 根.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer) continue;              // 粒子/拖尾不进掩码（会把法术也算成角色）
            if (r is TrailRenderer || r is LineRenderer) continue;
            // 一个渲染体一份材质实例：组 ID 走材质属性（CommandBuffer.DrawRenderer 没有 MPB 重载）
            var m = new Material(掩码材质) { name = "描边·掩码·" + 组, hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_ID", 组);
            放哪.Add(new 条目 { 渲染体 = r, 材质 = m });
        }
    }

    void 释放RT()
    {
        if (法线深度图 != null) { RenderTexture.ReleaseTemporary(法线深度图); 法线深度图 = null; }
        if (ID图 != null) { RenderTexture.ReleaseTemporary(ID图); ID图 = null; }
        上次宽 = 上次高 = 0;
    }

    void OnPreRender()
    {
        if (!启用 || 掩码材质 == null || 相机 == null || 命令 == null) { if (命令 != null) 命令.Clear(); return; }

        if (Time.unscaledTime >= 下次收列表) { 下次收列表 = Time.unscaledTime + Mathf.Max(0.2f, 列表刷新秒); 收列表(); }

        int w = Mathf.Max(2, 相机.pixelWidth), h = Mathf.Max(2, 相机.pixelHeight);
        if (法线深度图 == null || 上次宽 != w || 上次高 != h)
        {
            释放RT();
            // ⚠️ 采样数固定 1（不开 MSAA）：深度/法线的边缘检测和 MSAA 会打架
            法线深度图 = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear, 1);
            ID图 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1);
            上次宽 = w; 上次高 = h;
        }

        命令.Clear();
        命令.SetRenderTarget(new RenderTargetIdentifier[] { 法线深度图, ID图 }, 法线深度图.depthBuffer);
        命令.ClearRenderTarget(true, true, new Color(0.5f, 0.5f, 0.5f, 1f));   // 法线=0、深度=远
        掩码材质.SetFloat("_CamFar", Mathf.Max(1f, 相机.farClipPlane));
        foreach (var e in 玩家渲染体) if (e.渲染体 != null) 命令.DrawRenderer(e.渲染体, e.材质, 0, 0);
        foreach (var e in 其他渲染体) if (e.渲染体 != null) 命令.DrawRenderer(e.渲染体, e.材质, 0, 0);
    }

    void OnRenderImage(RenderTexture 源, RenderTexture 目标)
    {
        if (!启用 || 合成材质 == null || 法线深度图 == null || ID图 == null)
        {
            Graphics.Blit(源, 目标);
            return;
        }

        合成材质.SetTexture("_MainTex", 源);
        合成材质.SetTexture("_MaskTex", 法线深度图);
        合成材质.SetTexture("_IDTex", ID图);
        合成材质.SetColor("_PlayerColor", 玩家线色);
        合成材质.SetColor("_OtherColor", 其他线色);
        合成材质.SetFloat("_PlayerID", 玩家组);
        合成材质.SetFloat("_Radius", 粗细);
        合成材质.SetFloat("_Soft", 软化);
        合成材质.SetFloat("_Strength", 强度);
        合成材质.SetFloat("_DepthThr", 深度阈值);
        合成材质.SetFloat("_NormalThr", 法线阈值);
        合成材质.SetFloat("_IDThr", ID阈值);

        Graphics.Blit(源, 目标, 合成材质);
    }
}
