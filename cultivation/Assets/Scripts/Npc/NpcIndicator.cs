using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 NPC 上的视觉指示器：
///   · 选中 → 脚底绿色圆环
///   · 锁定 → 脚底红色圆环
///   · **敌对（对主角好感度 &lt; 0）→ 常显头顶血条（实时跟随血量）**
///   · 被锁定 → 同样显示血条（哪怕它不敌对）
///
/// 血条的显隐每帧同步一次，所以好感度变化 / 死亡 / 重生都能立刻反映出来。
/// 圆环与血条都在运行时按需生成，圆环尺寸取自该 NPC 的碰撞体大小，
/// 所以不同体型的 NPC 不用各自调参数。
/// </summary>
[RequireComponent(typeof(NpcInstance))]
public class NpcIndicator : MonoBehaviour
{
    [Header("圆环")]
    [Tooltip("圆环相对 NPC 半径的放大倍数")]
    public float 圆环半径系数 = 1.15f;

    [Tooltip("圆环宽度占半径的比例")]
    [Range(0.05f, 0.5f)]
    public float 圆环宽度比例 = 0.16f;

    [Tooltip("圆环离地高度，避免和地面 Z-fighting")]
    public float 圆环离地 = 0.04f;

    [Tooltip("选中圆环颜色")]
    public Color 选中颜色 = new Color(0.25f, 0.95f, 0.35f, 0.85f);

    [Tooltip("锁定圆环颜色")]
    public Color 锁定颜色 = new Color(0.95f, 0.25f, 0.25f, 0.9f);

    [Header("血条")]
    [Tooltip("血条离头顶的高度")]
    public float 血条高度偏移 = 0.35f;

    [Tooltip("血条世界宽度（米）")]
    public float 血条宽度 = 1.1f;

    [Tooltip("血条世界高度（米）")]
    public float 血条高度 = 0.13f;

    [Tooltip("血条底色")]
    public Color 血条底色 = new Color(0.08f, 0.08f, 0.08f, 0.85f);

    [Tooltip("血条填充色")]
    public Color 血条填充色 = new Color(0.85f, 0.18f, 0.18f, 1f);

    [Tooltip("敌对（好感度 < 0）的 NPC 是否常显血条")]
    public bool 敌对常显血条 = true;

    [Tooltip("死亡时是否把血条藏起来（关掉就一直挂着空血条）")]
    public bool 死亡时隐藏血条 = true;

    [Header("受伤飘字")]
    [Tooltip("挨打时在旁边飘一个伤害数字（暴击 / 会心字号更大）")]
    public bool 显示受伤飘字 = true;

    /// <summary>是否被选中（绿环）</summary>
    public bool IsSelected { get; private set; }

    /// <summary>是否被锁定（红环 + 血条）</summary>
    public bool IsLocked { get; private set; }

    /// <summary>
    /// 这个 NPC 现在该不该显示血条：
    /// 被锁定 → 显示；敌对（对主角好感度 &lt; 0）→ 显示；死了 → 按 <see cref="死亡时隐藏血条"/> 决定。
    /// </summary>
    public bool 应显示血条
    {
        get
        {
            if (npc == null) return false;
            if (npc.IsDead && 死亡时隐藏血条) return false;
            return IsLocked || (敌对常显血条 && npc.是敌对目标);
        }
    }

    NpcInstance npc;
    Transform 选中环;
    Transform 锁定环;
    Transform 血条根;
    Image 血条填充;
    Camera 主相机;
    float 头顶高度 = 2f;

    void Awake()
    {
        npc = GetComponent<NpcInstance>();
        主相机 = Camera.main;
        建补偿层();
        计算尺寸();
        创建圆环();
        创建血条();
        刷新显隐();

        if (npc != null) npc.结算完成 += 处理结算;
    }

    void OnDestroy()
    {
        if (npc != null) npc.结算完成 -= 处理结算;
    }

    /// <summary>挨打时在旁边飘一个伤害数字</summary>
    void 处理结算(NpcInstance 谁, AttackResult 结果)
    {
        if (!显示受伤飘字) return;
        if (!结果.命中 || 结果.伤害 <= 0f) return;   // 被闪避 / 没伤害就不飘
        DamagePopup.弹出(飘字位置, 结果.伤害, 结果.暴击, 结果.会心);
    }

    /// <summary>飘伤害数字的位置：血条上方一点</summary>
    public Vector3 飘字位置
        => 血条根 != null
           ? 血条根.position + Vector3.up * 0.5f
           : transform.position + Vector3.up * (头顶高度 + 0.8f);

    /// <summary>
    /// 缩放补偿层 —— 这是修「圈大得离谱」的关键。
    ///
    /// 圆环网格和血条都是按【世界尺寸】建的，但直接挂成 NPC 的子物件时，
    /// 会再乘一遍 NPC 的根缩放。母鸡 prefab 的根缩放是 12，于是：
    ///   · 圆环半径 0.5 变成世界 6 米   → 圈大得离谱
    ///   · 血条 localPosition.y 也被乘 12 → 飞到天上去，看不见
    ///
    /// 中间垫一层、把根缩放取倒数抵消掉，下面所有东西就都是世界单位了，
    /// 计算尺寸时可以直接用 collider.bounds 的世界数值，不用到处除缩放。
    /// </summary>
    void 建补偿层()
    {
        if (指示器根 != null) return;

        var s = transform.lossyScale;
        var go = new GameObject("IndicatorRoot");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(
            Mathf.Abs(s.x) > 1e-6f ? 1f / s.x : 1f,
            Mathf.Abs(s.y) > 1e-6f ? 1f / s.y : 1f,
            Mathf.Abs(s.z) > 1e-6f ? 1f / s.z : 1f);

        指示器根 = go.transform;
    }

    Transform 指示器根;

    void 计算尺寸()
    {
        // 依次尝试：普通 Collider → CharacterController（它不是 Collider 子类！）
        //            → 渲染体包围盒 → 兜底常数
        Bounds? bb = null;

        var col = GetComponent<Collider>();
        if (col != null) bb = col.bounds;
        else
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) bb = cc.bounds;
        }

        if (bb == null)
        {
            var 渲染器 = GetComponentsInChildren<Renderer>(true);
            if (渲染器.Length > 0)
            {
                var b0 = 渲染器[0].bounds;
                for (int i = 1; i < 渲染器.Length; i++) b0.Encapsulate(渲染器[i].bounds);
                bb = b0;
            }
        }

        if (bb.HasValue)
        {
            var b = bb.Value;
            // 这些都是【世界空间】的值 —— 有补偿层在，可以直接用
            float r = Mathf.Max(b.extents.x, b.extents.z);
            半径 = Mathf.Max(0.15f, r * 圆环半径系数);

            // ★【2026-09-29 全量审计揪出来的】头顶高度还要跟**最高的渲染体**比一下：
            // 碰撞体是按「身躯」算的（排除武器），可有些怪的**武器/头饰会高过头顶**
            // （实测 15 只：狗头军师 2.48 / 蛤蟆精_04 3.03 / 寒狼_03 3.51 / 哪吒 3.03 …），
            // 血条按身躯算就**落在武器里**了 ✗。取两者更高的那个。
            float 顶 = b.max.y;
            foreach (var r2 in GetComponentsInChildren<Renderer>(true))
            {
                if (r2 == null || r2 is ParticleSystemRenderer) continue;
                if (NpcBodyBounds.取网格包围盒(r2, out var rb) && rb.max.y > 顶) 顶 = rb.max.y;
            }
            头顶高度 = (顶 - transform.position.y) + 血条高度偏移;
        }
        else
        {
            半径 = 0.5f;
            头顶高度 = 2f + 血条高度偏移;
        }

        // 圆环：放在**身体最低点**（而不是根节点）—— 模型浮在根上方时，按根放会埋进地下
        float 最低 = NpcBodyBounds.取最低点(gameObject);
        圆环高度 = (最低 - transform.position.y) + 圆环离地;
    }

    float 半径 = 0.5f;

    /// <summary>圆环相对**根节点**的高度（世界值）= 身体最低点到根的距离 + <see cref="圆环离地"/>。
    /// 见 <see cref="建环"/> 里的说明：不能直接用根（模型可能是浮在根上方的）。</summary>
    float 圆环高度 = 0.02f;

    void 创建圆环()
    {
        选中环 = 建环("Ring_Selected", 选中颜色);
        锁定环 = 建环("Ring_Locked", 锁定颜色);
    }

    Transform 建环(string name, Color color)
    {
        var go = new GameObject(name);
        // 挂到补偿层下面，不直接挂 NPC 根 —— 否则会被根缩放再乘一遍
        go.transform.SetParent(指示器根 != null ? 指示器根 : transform, false);
        // ★【2026-09-29】圆环放**身体最低点**，不是根节点：
        // 有些怪的模型是**浮在根上方**的（实测玄蜂的身体最低点在根上方 **1.075 米**），
        // 圆环按根放就会**整圈埋进地下** —— 表现就是用户报的「**没看到锁定的红圈**」✗
        go.transform.localPosition = new Vector3(0f, 圆环高度, 0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = 生成圆环网格(半径, 半径 * (1f - 圆环宽度比例), 64);

        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = color;
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        return go.transform;
    }

    /// <summary>生成一个平躺在 XZ 平面的圆环网格</summary>
    static Mesh 生成圆环网格(float outer, float inner, int segments)
    {
        var m = new Mesh { name = "SelectionRing" };
        var verts = new Vector3[segments * 2];
        var tris = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = (float)i / segments * Mathf.PI * 2f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            verts[i * 2] = new Vector3(c * outer, 0f, s * outer);
            verts[i * 2 + 1] = new Vector3(c * inner, 0f, s * inner);
        }
        for (int i = 0; i < segments; i++)
        {
            int n = (i + 1) % segments;
            int t = i * 6;
            tris[t] = i * 2; tris[t + 1] = n * 2; tris[t + 2] = n * 2 + 1;
            tris[t + 3] = i * 2; tris[t + 4] = n * 2 + 1; tris[t + 5] = i * 2 + 1;
        }
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void 创建血条()
    {
        var go = new GameObject("HealthBar", typeof(Canvas));
        // 同样挂到补偿层 —— 头顶高度是世界值，直接当 localPosition 会被根缩放放大
        go.transform.SetParent(指示器根 != null ? 指示器根 : transform, false);
        go.transform.localPosition = new Vector3(0f, 头顶高度, 0f);
        go.transform.localScale = Vector3.one * 0.01f;

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;                 // 盖在场景之上（只在"同一个透明队列"里生效）
        // ★【用户 2026-09-29 要求：不要让怪的模型挡住血条】
        // 世界空间 Canvas + `UI/Default` 默认是 **ZTest LEqual** —— 也就是说**模型挡在血条前面
        // 就真的会把血条盖掉**（玄蜂就是：血条算在 2.22 米，而它的模型真实顶在 2.59 米，
        // 血条整条埋在身体里）。`sortingOrder` 只管透明队列内部，**不管深度测试**，救不了。
        // 这里给血条单独一张 **ZTest Always** 的材质：血条永远画在最上面，模型挡不住它。
        canvas.overrideSorting = true;

        var 血条材质 = 建不挡材质();

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(血条宽度 / 0.01f, 血条高度 / 0.01f);   // 配合 localScale 换算成世界尺寸

        var bg = new GameObject("BG", typeof(RectTransform));
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = 取白色Sprite();
        bgImg.color = 血条底色;
        bgImg.material = 血条材质;                 // ★ 不挡（ZTest Always）
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill", typeof(RectTransform));
        fill.transform.SetParent(go.transform, false);
        var fillImg = fill.AddComponent<Image>();
        // 关键：Image 在 sprite 为空时【会忽略 fillAmount】，不管 type 是不是 Filled，
        // 血条就会永远是满的。所以必须给一张 sprite。
        fillImg.sprite = 取白色Sprite();
        fillImg.color = 血条填充色;
        fillImg.material = 血条材质;               // ★ 不挡（ZTest Always）
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f); fillRt.offsetMax = new Vector2(-2f, -2f);

        血条根 = go.transform;
        血条填充 = fillImg;
    }

    static Sprite 白色Sprite;

    static Material 不挡材质;

    /// <summary>
    /// 一张 **ZTest Always** 的 UI 材质：**血条永远画在模型前面**（用户要求"不要让怪的模型挡住血条"）。
    ///
    /// 用 `UI/Default` 复制一份，把 `unity_GUIZTestMode` 设成 `Always` ——
    /// 这是 Unity 给 UI 留的深度测试开关（Canvas 的材质面板里就是 "Shader Channels → ZTest"）。
    /// 代价：血条也会**透过墙**看得见。对"怪的血条"来说这是常规做法，也正是用户要的效果。
    /// </summary>
    static Material 建不挡材质()
    {
        if (不挡材质 != null) return 不挡材质;
        var 着色器 = Shader.Find("UI/Default");
        if (着色器 == null) return null;
        不挡材质 = new Material(着色器);
        不挡材质.name = "NpcHealthBar_AlwaysOnTop";
        if (不挡材质.HasProperty("unity_GUIZTestMode"))
            不挡材质.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        return 不挡材质;
    }

    /// <summary>运行时生成一张 1x1 白色 sprite，给血条的 Image 用</summary>
    static Sprite 取白色Sprite()
    {
        if (白色Sprite != null) return 白色Sprite;

        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(px);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;

        白色Sprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        白色Sprite.hideFlags = HideFlags.HideAndDontSave;
        return 白色Sprite;
    }

    void LateUpdate()
    {
        // 每帧同步一次显隐 —— 好感度变化 / 死亡 / 重生都能立刻反映，
        // 不能只在 SetLocked 里刷，否则敌对 NPC 的血条要等到被锁定才出现。
        同步血条显隐();

        // ★ 圆环每帧按**地面**摆（见 摆圆环 的注释）——
        // `计算尺寸()` 是在**贴地之前**跑的，之后根节点被贴地挪动，固定偏移就过期了
        摆圆环();

        if (血条根 != null && 血条根.gameObject.activeSelf)
        {
            // 血条始终面向相机
            if (主相机 == null) 主相机 = Camera.main;
            if (主相机 != null)
                血条根.rotation = Quaternion.LookRotation(血条根.position - 主相机.transform.position, 主相机.transform.up);

            if (血条填充 != null && npc != null)
                血条填充.fillAmount = npc.HealthPercent;
        }
    }

    /// <summary>
    /// **每帧**把选中/锁定圆环摆到「脚下的地面」上。
    ///
    /// 【坑·已修·2026-09-29】以前圆环的 Y 是在 <see cref="计算尺寸"/> 里算一次就不动了，
    /// 而那个函数跑在**贴地之前** —— 贴地随后会把根节点上下挪（玄蜂那种"模型浮在根上方"的挪得更多），
    /// 于是圆环的固定偏移全部过期：**实测四只怪的圆环都埋在地下 2~3 米** ✗
    /// 用户看到的就是「**没看到锁定的红圈**」。
    ///
    /// 现在每帧从胸口往下打一条射线找地面（一次 raycast，很便宜），
    /// 圆环永远贴在**它脚下那块地**上 —— 顺带还能跟斜坡。
    /// </summary>
    void 摆圆环()
    {
        if (选中环 == null && 锁定环 == null) return;

        Vector3 起点 = transform.position + Vector3.up * 2f;
        float 环Y = transform.position.y;                 // 兜底：射线没打到就用自己脚下
        var 命中 = Physics.RaycastAll(起点, Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
        float 最好 = float.MaxValue;
        foreach (var h in 命中)
        {
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;   // 自己的碰撞体
            if (h.collider is CharacterController) continue;           // 玩家的角色控制器
            if (h.distance < 最好) { 最好 = h.distance; 环Y = h.point.y; }
        }

        float 局部Y = 环Y - transform.position.y + 圆环离地;
        if (锁定环 != null) { var p = 锁定环.localPosition; p.y = 局部Y; 锁定环.localPosition = p; }
        if (选中环 != null) { var p = 选中环.localPosition; p.y = 局部Y; 选中环.localPosition = p; }
    }

    void 同步血条显隐()
    {
        if (血条根 == null) return;
        bool 要显示 = 应显示血条;
        if (血条根.gameObject.activeSelf != 要显示) 血条根.gameObject.SetActive(要显示);
    }

    /// <summary>设置选中状态（绿环）</summary>
    public void SetSelected(bool on)
    {
        IsSelected = on;
        刷新显隐();
    }

    /// <summary>设置锁定状态（红环 + 血条）</summary>
    public void SetLocked(bool on)
    {
        IsLocked = on;
        刷新显隐();
    }

    void 刷新显隐()
    {
        if (选中环 != null) 选中环.gameObject.SetActive(IsSelected && !IsLocked);
        if (锁定环 != null) 锁定环.gameObject.SetActive(IsLocked);
        同步血条显隐();
    }
}
