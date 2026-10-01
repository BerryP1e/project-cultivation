using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// **两大伏笔的"看得见"那部分** —— 由 <see cref="伏笔管理器"/> 的等级驱动：
///
/// | 等级 | 灵田（洞府） | 镇妖塔 |
/// |---|---|---|
/// | 0 | 什么都没有 | 什么都没有 |
/// | 1 | 每块地上飘 1 点灵气微光 | 进塔后每隔 ~7 秒一次"共鸣"提示 + 画面极淡泛红 |
/// | 2 | 2 点，更亮 | 间隔缩短、泛光更明显 |
/// | 3 | 3 点，最亮 | 最急、最亮 |
///
/// ## 为什么用"自发光的半透明小球"而不是粒子系统
///
/// 粒子系统在**代码里现配**很容易配歪（形状/生命周期/发射率有一堆默认值），
/// 而这个项目里最稳的一套"发光的半透明东西"是 <see cref="灵田地块外观"/> 那套
/// **Standard + Fade + Emission** 的做法（`_Mode=2` + 三个 Blend/关键字）。
/// 小球数量可控、开销可以忽略（满级也就是 地块数 × 3）。
///
/// ## 两条纪律
///
/// · **等级没变就不重建**；重建只在"等级变了"或"场景换了导致光点被销毁"时发生。
/// · 微光只是**表现**，不参与任何逻辑判定 —— 关掉它游戏照样跑
///   （所以它随时可以换成更好的美术资源）。
/// </summary>
public class 伏笔表现 : MonoBehaviour
{
    [Tooltip("微光离地块中心的抬高")]
    public float 微光高度 = 1.15f;
    [Tooltip("微光在水平面上的散布半径")]
    public float 微光散布 = 0.8f;
    [Tooltip("上下浮动的幅度")]
    public float 涨落幅度 = 0.16f;

    readonly List<Transform> 光点 = new List<Transform>();
    readonly List<Vector3> 基点 = new List<Vector3>();
    readonly List<float> 相位 = new List<float>();

    Canvas 画布;
    Image 泛光;
    float 下次共鸣时刻;
    int 上次田级 = -1;
    string 上次场景 = "";

    void OnDestroy() => 清空光点();

    void Update()
    {
        var f = 伏笔管理器.取();
        int 田 = f != null ? f.灵田等级 : 0;
        int 塔 = f != null ? f.塔等级 : 0;

        string 场景 = SceneManager.GetActiveScene().name;
        // 换场景会把上一张图里的光点一起销毁 ⇒ 光点列表里会出现 null，这时也要重建
        bool 光点丢了 = 光点.Count > 0 && 光点[0] == null;
        bool 换场景了 = 场景 != 上次场景;
        if (田 != 上次田级 || 光点丢了 || 换场景了)
        {
            上次田级 = 田;
            上次场景 = 场景;
            重建微光(田);
        }

        浮动();
        共鸣(塔);
    }

    // ============================================================ 灵田微光

    void 清空光点()
    {
        foreach (var t in 光点) if (t != null) Destroy(t.gameObject);
        光点.Clear();
        基点.Clear();
        相位.Clear();
    }

    void 重建微光(int 等级)
    {
        清空光点();
        if (等级 <= 0) return;

        var 地块 = Object.FindObjectsOfType<灵田地块>();
        if (地块 == null || 地块.Length == 0) return;

        var 随 = new System.Random(20261002);
        foreach (var 块 in 地块)
        {
            if (块 == null) continue;
            for (int i = 0; i < 等级; i++)
            {
                float 角 = (float)随.NextDouble() * Mathf.PI * 2f;
                float 半 = (float)随.NextDouble() * 微光散布;
                var 位 = 块.transform.position + Vector3.up * 微光高度
                        + new Vector3(Mathf.Cos(角) * 半, (float)随.NextDouble() * 0.25f, Mathf.Sin(角) * 半);
                var t = 造光点(位, 等级);
                if (t == null) continue;
                光点.Add(t);
                基点.Add(位);
                相位.Add((float)随.NextDouble() * Mathf.PI * 2f);
            }
        }
        Debug.Log("[伏笔] 灵田微光重建：" + 光点.Count + " 点（地块 " + 地块.Length + " 块，等级 " + 等级 + "）");
    }

    Transform 造光点(Vector3 位, int 等级)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.name = "灵气微光";
        go.transform.position = 位;
        // 【为什么这么大】第一版是 0.10~0.18 米、半透明 —— 实测**在游戏画面里根本看不见**
        //   （相机离地 8 米左右，0.18 米不到 8 像素，再乘 0.4 的透明度就没了）。
        //   现在做成**不透明的自发光珠子**，靠大小 + 亮度分级，远看也一眼能认出"田里不对"。
        go.transform.localScale = Vector3.one * (0.22f + 0.07f * 等级);

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = 微光材质(等级);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        return go.transform;
    }

    readonly Dictionary<int, Material> 材质表 = new Dictionary<int, Material>();

    Material 微光材质(int 等级)
    {
        Material 已有;
        if (材质表.TryGetValue(等级, out 已有) && 已有 != null) return 已有;

        var m = new Material(Shader.Find("Standard"));
        m.name = "灵气微光_" + 等级;
        m.color = new Color(0.45f, 1f, 0.62f, 1f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", new Color(0.35f, 1f, 0.55f) * (1.2f + 0.9f * 等级));
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Glossiness", 0.55f);

        材质表[等级] = m;
        return m;
    }

    void 浮动()
    {
        for (int i = 0; i < 光点.Count; i++)
        {
            var t = 光点[i];
            if (t == null) continue;
            var p = 基点[i];
            p.y += Mathf.Sin(Time.time * 0.9f + 相位[i]) * 涨落幅度;
            t.position = p;
        }
    }

    // ============================================================ 塔共鸣

    void 共鸣(int 等级)
    {
        bool 在塔 = !string.IsNullOrEmpty(伏笔管理器.取() != null ? 伏笔管理器.取().塔场景名 : "")
                    && SceneManager.GetActiveScene().name == 伏笔管理器.取().塔场景名;

        if (等级 <= 0 || !在塔)
        {
            if (泛光 != null) 泛光.color = new Color(0.45f, 0.06f, 0.08f, 0f);
            下次共鸣时刻 = 0f;
            return;
        }

        确保泛光();

        if (Time.time >= 下次共鸣时刻)
        {
            下次共鸣时刻 = Time.time + Mathf.Max(4f, 9f - 2f * 等级);
            ToastUI.提示(伏笔管理器.塔台词[Mathf.Clamp(等级, 1, 3)], 2.6f);
            Debug.Log("[伏笔] 塔共鸣脉冲（等级 " + 等级 + "）");
        }

        // 越接近下一次脉冲越暗：脉冲刚发生时最亮，然后 1.6 秒内淡下去
        float 余 = Mathf.Clamp01(1f - (下次共鸣时刻 - Time.time) / 1.6f);
        泛光.color = new Color(0.45f, 0.06f, 0.08f, 0.05f * 等级 * 余);
    }

    void 确保泛光()
    {
        if (泛光 != null) return;

        var 根 = new GameObject("OmensCanvas", typeof(Canvas), typeof(CanvasScaler));
        根.transform.SetParent(transform, false);
        画布 = 根.GetComponent<Canvas>();
        画布.renderMode = RenderMode.ScreenSpaceOverlay;
        // 压在 HUD 之下（纪年 2400 / 引导 2300），只当一层"气氛"，不挡字
        画布.sortingOrder = 2200;
        画布.gameObject.AddComponent<GraphicRaycaster>().enabled = false;

        var 缩放 = 根.GetComponent<CanvasScaler>();
        缩放.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        缩放.referenceResolution = new Vector2(1920f, 1080f);
        缩放.matchWidthOrHeight = 0.5f;

        泛光 = UIBuildUtils.CreateImage("塔共鸣泛光", 根.transform, new Color(0.45f, 0.06f, 0.08f, 0f));
        UIBuildUtils.Stretch(泛光.rectTransform);
        泛光.raycastTarget = false;
    }
}
