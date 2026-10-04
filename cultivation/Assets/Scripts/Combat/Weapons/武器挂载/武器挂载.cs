using UnityEngine;

/// <summary>
/// **通用「武器挂载」** —— 按【当前功法】把一把武器 prefab 实例化到玩家某根骨骼下面。
///
/// 不是八九玄功专用：任何"手里要拿个东西"的功法/外观都能用它，
/// 换武器只要改 <see cref="武器资源路径"/> 与三个本地 TRS 字段。
///
/// ## 为什么需要它
/// 八九玄功的四段近战动作（长刀女 / 杨戬的 Attack1、2）**手里必须真的有刀**才不违和 ——
/// 动作是"持械挥砍"，而玩家模型本身不带武器。工程里以前没有"给玩家挂握持武器"的机制：
/// `base_sword` 只当**飞剑弹丸**用（<see cref="BasicSword01"/> 生成后飞出去），
/// NPC 的武器则是各自模型里的蒙皮网格。所以这一层是新加的。
///
/// ## 标定值（别随手改 <see cref="本地缩放"/>）
/// 玩家与杨戬的 Avatar 比例需要包含各自模型根的缩放：
/// (0.493647 × 1.8007) / (1.52885 × 0.9) ≈ 0.646。
/// 旧标定把玩家身高按重复缩放的网格高度计算，刀明显过长；
/// 校正后本地缩放为 0.3173，刀长约 2.67 米。
///
/// ⚠️ 反面教训：**不要拿"原主人武器包围盒的最长边"当参照** —— 静止姿势下斜放的 AABB
/// 会随姿势变化。先用 Avatar 定比例，再用 BakeMesh 的长轴量测成品。
///
/// ## 生命周期：`OnDisable` 里**按数据**决定收不收刀（这里很微妙，别改坏）
/// 两种"组件被关掉"长得一模一样，但要求**相反**：
///   · **过场**：<see cref="演出锁"/> 会 `foreach (var c in GetComponents&lt;MonoBehaviour&gt;()) c.enabled = false;`
///     把 Player 上所有 MonoBehaviour 关掉 —— 这时刀**必须留在手里**（否则过场里凭空消失）；
///   · **换功法**：<see cref="PlayerAbilityLoader"/> 是「停用而不是销毁」（它的注释里写明了理由：
///     普攻组件身上有很贵的 Inspector 配置）—— 这时刀**必须收掉**，
///     否则换到别的功法手里还攥着三尖两刃刀 ✗。
/// 两者都只表现为 `enabled = false`，**没法从 enabled 区分**，所以判据只能是**数据本身**：
/// `OnDisable` 调一次 <see cref="刷新"/> —— "当前功法还匹配"就什么也不做、"不匹配"才销毁。
/// （武器挂在玩家骨骼下，切场景随玩家一起销毁，不会泄漏。）
/// </summary>
public class 武器挂载 : MonoBehaviour
{
    // ============================================================ 武器

    [Header("武器")]
    [Tooltip("武器 prefab 的 Resources 路径（相对 Assets/resources，不带扩展名）")]
    public string 武器资源路径 = "Weapons/八九玄功_三尖两刃刀";

    // ============================================================ 显示条件

    [Header("显示条件")]
    [Tooltip("只有【当前功法】的 id 等于这个值时才显示这把武器。\n" +
             "**留空 = 一直显示**（不看功法）")]
    public string 显示条件功法id = "gongfa_jiuba_xuangong";

    // ============================================================ 挂点

    [Header("挂点")]
    [Tooltip("挂到哪根骨骼下（按名字找，大小写不敏感）。默认右手 `R_Hand`")]
    public string 挂点骨骼名 = "R_Hand";

    [Tooltip("实例相对挂点骨骼的本地位置")]
    public Vector3 本地位置 = Vector3.zero;

    [Tooltip("实例相对挂点骨骼的本地旋转（欧拉角）")]
    // Align hand coordinate frames in the same neutral HumanPose. Copying the source
    // weapon's offset with identity rotation reverses/misaligns the blade on this avatar.
    public Vector3 本地旋转欧拉 = new Vector3(2.328f, 254.529f, 263.733f);

    [Tooltip("★ 实例的本地缩放。默认 0.3173，按玩家/杨戬 Avatar 比例校正，\n" +
             "刀长约 2.67 米；换武器或 Avatar 要重新标定")]
    public float 本地缩放 = 0.3173f;

    // ============================================================ 数据源

    [Header("数据源")]
    [Tooltip("角色面板数据（当前功法从这里读）。留空则自动找 CharacterUI 上的")]
    public UIPanelData 面板数据;

    [Header("调试")]
    public bool 打印日志 = true;

    // ============================================================ 运行时

    /// <summary>当前挂在手上的武器实例（null = 没挂 / 已销毁）</summary>
    public GameObject 实例 { get; private set; }

    Transform 挂点缓存;
    bool 报过找不到武器;
    bool 报过找不到骨骼;
    bool 报过没有面板;

    void Awake() => 解析面板();

    /// <summary>
    /// `Start` 再解析 + 刷新一次：`CharacterUI` 有可能比本组件晚一点出现
    /// （本工程的角色面板是运行时装配的）。仍然拿不到就得由外部再调一次 <see cref="刷新"/> ——
    /// 这一处**故意不写 Update 轮询**，避免每帧做事。
    /// </summary>
    void Start()
    {
        解析面板();
        刷新();
    }

    void OnEnable()
    {
        解析面板();
        if (面板数据 != null) 面板数据.Changed += 刷新;
        刷新();
    }

    void OnDisable()
    {
        if (面板数据 != null) 面板数据.Changed -= 刷新;
        // During scene teardown the panel and hierarchy are already being destroyed.
        // Do not search inactive scene objects or recreate children from this callback.
        // Disabling only the component for a cutscene still refreshes while its GO is active.
        if (!gameObject.activeInHierarchy) return;

        // ⚠️ 这里**不能无脑销毁武器**，也**不能什么都不做** —— 两种"被关掉"长得一模一样：
        //   · **过场**：`演出锁` 把 Player 上所有 MonoBehaviour 关掉 ⇒ 刀必须留在手里；
        //   · **换功法**：`PlayerAbilityLoader` 是「停用而不是销毁」⇒ 刀必须收掉，
        //     否则换到别的功法手里还攥着三尖两刃刀 ✗
        // 两者都表现为 enabled=false，所以**判据只能是数据本身**（当前功法还匹不匹配）：
        // 刷新() 在"还匹配"时什么也不做、在"不匹配"时才销毁，两种情况各得其所。
        刷新();
    }

    void OnDestroy()
    {
        if (面板数据 != null) 面板数据.Changed -= 刷新;
    }

    /// <summary>照 PlayerAbilityLoader 取面板数据的同一套做法</summary>
    void 解析面板()
    {
        if (面板数据 != null) return;
        var ui = GameObject.Find("CharacterUI");
        if (ui != null) 面板数据 = ui.GetComponent<UIPanelData>();
        if (面板数据 == null) 面板数据 = FindObjectOfType<UIPanelData>();
    }

    // ============================================================ 刷新

    /// <summary>
    /// 按「当前功法」重新决定显不显示。功法一变（`面板数据.Changed`）就会被调；
    /// 也可以外部手动调。**幂等**：重复调用不会叠加出第二把。
    /// </summary>
    public void 刷新()
    {
        解析面板();

        if (面板数据 == null)
        {
            if (!报过没有面板)
            {
                报过没有面板 = true;
                Debug.LogWarning("[武器挂载] 找不到 UIPanelData（CharacterUI 还没出来？）→ 先不显示武器", this);
            }
            销毁实例();
            return;
        }

        if (!该显示())
        {
            销毁实例();
            return;
        }

        var 骨 = 找挂点();
        if (骨 == null) { 销毁实例(); return; }      // 找不到骨骼时不实例化（已经在 找挂点 里警告过）

        if (实例 == null) 实例 = 创建实例(骨);
        if (实例 == null) return;

        应用本地TRS(实例.transform);
        清理重复实例(骨, 实例.name);
    }

    bool 该显示()
    {
        if (string.IsNullOrEmpty(显示条件功法id)) return true;          // 留空 = 一直显示
        var 功法 = 面板数据 != null ? 面板数据.当前功法 : null;
        return 功法 != null && 功法.功法id == 显示条件功法id;
    }

    // ============================================================ 实例

    GameObject 创建实例(Transform 骨)
    {
        if (string.IsNullOrEmpty(武器资源路径)) return null;

        var 预制 = Resources.Load<GameObject>(武器资源路径);
        if (预制 == null)
        {
            if (!报过找不到武器)
            {
                报过找不到武器 = true;
                Debug.LogWarning("[武器挂载] 找不到武器 prefab：Assets/resources/" + 武器资源路径 + ".prefab", this);
            }
            return null;
        }

        var go = Instantiate(预制, 骨, false);
        // 打上标记（= prefab 自己的名字，本例就是 `八九玄功_三尖两刃刀`）——
        // 刷新时靠这个名字认出"是我挂的"，重复的会被清掉，不会叠出多把
        go.name = 预制.name;
        应用本地TRS(go.transform);

        // ★ 必须开 updateWhenOffscreen：武器的 `SkinnedMeshRenderer` 出厂是 `m_UpdateWhenOffscreen: 0`，
        //   离屏时**包围盒不跟着骨骼刷新**，于是近战判定（`BasicJiuba01` 用武器网格求交）拿到的是
        //   **静止时那个旧盒子** ⇒ 站在靶子正前方 2 米连打 4 次**全部打空**（实测）。
        //   工程自己的量测工具（近战动作量测.cs）和 `玩家外观` 也是这么开的。
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            smr.updateWhenOffscreen = true;

        if (打印日志)
            Debug.Log("[武器挂载] 已把「" + go.name + "」挂到「" + 骨.name + "」（缩放 " + 本地缩放.ToString("0.####") + "）", this);
        return go;
    }

    void 应用本地TRS(Transform t)
    {
        if (t == null) return;
        t.localPosition = 本地位置;
        t.localRotation = Quaternion.Euler(本地旋转欧拉);
        t.localScale = Vector3.one * Mathf.Max(0.0001f, 本地缩放);
    }

    void 销毁实例()
    {
        if (实例 == null) return;
        if (打印日志) Debug.Log("[武器挂载] 收掉武器「" + 实例.name + "」（当前功法不需要它）", this);
        Destroy(实例);
        实例 = null;
    }

    /// <summary>同一个骨骼下如果还挂着同名实例（比如换过一次组件），只留自己这一把</summary>
    void 清理重复实例(Transform 骨, string 名)
    {
        if (骨 == null || string.IsNullOrEmpty(名)) return;
        foreach (var t in 骨.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == 骨) continue;
            if (t.parent != 骨) continue;                 // 只看直接挂在骨骼下的那些
            if (t.gameObject == 实例) continue;
            if (t.name != 名) continue;
            if (打印日志) Debug.Log("[武器挂载] 发现重复的武器实例，销毁：" + t.name, this);
            Destroy(t.gameObject);
        }
    }

    // ============================================================ 找骨骼

    /// <summary>
    /// 在自己（以及所在层级根）下面按名字找挂点骨骼。
    /// ⚠️ 只在这条玩家自己的层级里找 —— 不去场景里乱搜别人的骨骼。
    /// </summary>
    Transform 找挂点()
    {
        if (挂点缓存 != null) return 挂点缓存;
        if (string.IsNullOrEmpty(挂点骨骼名)) return null;

        foreach (var 候选 in 挂点骨骼名.Split('|'))
        {
            var 名 = 候选.Trim();
            if (名.Length == 0) continue;

            挂点缓存 = 按名字找(transform, 名);
            if (挂点缓存 == null && transform.root != null && transform.root != transform)
                挂点缓存 = 按名字找(transform.root, 名);
            if (挂点缓存 != null) break;
        }

        if (挂点缓存 == null && !报过找不到骨骼)
        {
            报过找不到骨骼 = true;
            Debug.LogWarning("[武器挂载] 在玩家层级里找不到挂点骨骼「" + 挂点骨骼名
                + "」→ 不显示武器（本组件要挂在 Player 或它下面）", this);
        }
        return 挂点缓存;
    }

    static Transform 按名字找(Transform 根, string 名)
    {
        if (根 == null) return null;
        foreach (var t in 根.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            if (string.Equals(t.name, 名, System.StringComparison.OrdinalIgnoreCase)) return t;
        }
        return null;
    }

    /// <summary>把骨骼/实例缓存清掉（换外观、换骨架之后要重新找）</summary>
    public void 清缓存()
    {
        挂点缓存 = null;
        报过找不到骨骼 = false;
        if (实例 != null) 销毁实例();
    }
}
