using UnityEngine;

/// <summary>
/// **镇妖塔准入** —— 把"进塔要炼气三层"这条规则**在运行时**写到那个传送圈上。
///
/// ## 为什么是运行时补，而不是在场景里配
///
/// 规则本来就该是 `Teleporter.需要等级` 一个字段的事，但那个圈在 `Sect.scene` 里，
/// **改它就得保存场景** —— 而保存那个场景会连带把它序列化着的一份运行时 UI 列表重新排版
/// （实测一次 1541 行无关改动，见 `docs/ai/踩坑总库.md` A8）。
/// 所以照本工程的老办法：**能运行时补齐的就别写进场景** ——
/// 由 `场景自举` 按名字给那个圈补上本组件，本组件再把规则写进 `Teleporter`。
///
/// ## 为什么每帧看着
///
/// 等级会变（修炼 / 破境 / 读档）。等级一变就把传送圈的准入刷新一遍，
/// 玩家刚破到炼气三层、界面还开着的话，选项会**当场从灰变亮**，不用重开界面。
///
/// ## 挂在哪
///
/// 由 `场景自举.补塔准入()` 补到 `Sect` 场景里那个 `sect2 to tower` 传送圈上。
/// 手动挂也行（挂在同一个物件上即可）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Teleporter))]
public class 塔准入 : MonoBehaviour
{
    [Tooltip("需要的最低**境界等级**（炼气一层 = 1、炼气三层 = 3）")]
    [Min(0)] public int 需要等级 = 3;

    [Tooltip("等级不够时面板上写的提示。`{当前}` / `{需要}` 会被替换成数字")]
    public string 未开放原因 = "镇妖塔凶险，需炼气三层方可入内（你当前炼气 {当前} 层）";

    Teleporter 传送;
    PlayerCultivation 修行;
    int 上次等级 = -1;

    void Awake() { 应用(); }

    void Update()
    {
        int 现在 = 修行 != null ? 修行.等级 : -1;
        if (现在 == 上次等级) return;
        应用();
    }

    void 应用()
    {
        if (传送 == null) 传送 = GetComponent<Teleporter>();
        if (传送 == null) return;
        if (修行 == null) 修行 = Object.FindObjectOfType<PlayerCultivation>();

        传送.需要等级 = 需要等级;
        传送.未开放原因 = 未开放原因;
        上次等级 = 修行 != null ? 修行.等级 : -1;
        传送.刷新准入();

        Debug.Log("[塔准入] 镇妖塔需要境界等级 " + 需要等级
                  + "，玩家当前 " + (修行 != null ? 修行.等级.ToString() : "?")
                  + " → " + (传送.准入通过 ? "✅ 可以进塔" : "⛔ 暂不开放"), this);
    }

    // ---- ASCII 别名 ----
    public int RequiredLevel { get => 需要等级; set { 需要等级 = value; 应用(); } }
}
