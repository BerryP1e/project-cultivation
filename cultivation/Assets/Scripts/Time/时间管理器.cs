using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// **游戏内时间 / 纪年**。全局单例，跟着存档走。
///
/// ## 时间尺度
///
/// 默认 **10 分钟现实时间 = 1 游戏日**（<see cref="秒每天"/> = 600）。
/// 调这一个数就能整体变快/变慢，**所有依赖日期的东西自动跟着变**。
///
/// ## 为什么"不算离线时间"（用户 2026-10-01 定的）
///
/// 设计稿里提到"离线挂机"，但如果按现实时间戳算，
/// 10 分钟一天 ⇒ 离线一晚上回来就是几百天，数值完全失控。
/// 所以**只用游戏内时间推进**：关掉游戏，时间不走。
///
/// > 好处：时间快慢完全可控、方便测试、数值不会失控。
/// > 代价：「关掉游戏也在长」这个卖点暂时没有 —— 以后要做的话，
/// > 应该做成**独立的"离线收益"结算**（按真实时间给一笔资源），
/// > 而不是让游戏内时间疯跑。
///
/// ## 修炼机会（两套，分开算）
///
/// | 来源 | 存量 | 会不会过期 |
/// |---|---|---|
/// | **每日**（每过一天 +1） | 一场最多同时留 3 天 | **会**，第 4 天作废 |
/// | **打怪** | 小数累积，无上限 | 不会 |
///
/// 每天**总共最多修炼 10 次**（两边加起来，见 <see cref="还能修炼几次"/>）。
///
/// 【为什么每日机会要"逐日存"】需求是"最多保存 3 天，3 天后消失" ——
/// 存一个总数就没法知道哪一次是哪天发的、也就没法让它过期。
/// 所以内部按 <c>天数 → 剩余次数</c> 记账，每过一天清掉太老的账。
/// </summary>
[DisallowMultipleComponent]
public class 时间管理器 : MonoBehaviour
{
    // ============================================================ 单例

    static 时间管理器 实例;
    public static 时间管理器 取()
    {
        if (实例 != null) return 实例;
        实例 = FindObjectOfType<时间管理器>();
        if (实例 != null) return 实例;

        // 兜底：运行时凭空造一个。它没有场景依赖，可以安全地这么做
        var go = new GameObject("时间管理器");
        DontDestroyOnLoad(go);
        实例 = go.AddComponent<时间管理器>();
        return 实例;
    }

    // ============================================================ 时间尺度

    [Header("时间尺度")]
    [Tooltip("现实多少秒 = 游戏内 1 天。默认 600 秒（10 分钟）")]
    [Min(1f)] public float 秒每天 = 600f;

    [Tooltip("勾上后时间暂停（调试用）")]
    public bool 暂停 = false;

    [Tooltip("时间流速倍率。1 = 正常；调大可以快速验证")]
    [Min(0.01f)] public float 倍率 = 1f;

    // ============================================================ 世界状态

    [Header("世界状态（只读，由存档恢复）")]
    [Tooltip("从开局起过了多少天")]
    [SerializeField] int 天数 = 0;

    [Tooltip("当天已过的比例 0~1")]
    [SerializeField] float 日内进度 = 0f;

    [Tooltip("纪元起始年。太虚历 1 年 = 开局")]
    public int 起始年 = 1;

    [Tooltip("每个月按多少天算（只影响显示）")]
    public int 每月天数 = 30;

    [Tooltip("每年按多少月算（只影响显示）")]
    public int 每年月数 = 12;

    // ============================================================ 修炼机会

    [Header("修炼机会")]
    [Tooltip("每天白送几次修炼机会")]
    [Min(0)] public int 每日机会数 = 1;

    [Tooltip("每日机会**最多保存几天**。超过就作废")]
    [Min(1)] public int 机会保存天数 = 3;

    [Tooltip("每天最多能修炼几次（日常 + 打怪 加在一起）")]
    [Min(1)] public int 每日修炼上限 = 10;

    /// <summary>打怪获得的修炼机会（小数累积，不过期）</summary>
    public float 打怪机会 { get; private set; } = 0f;

    /// <summary>逐日机会账本：天数 → 那一天发的还没用掉的次数</summary>
    readonly Dictionary<int, int> 日常机会 = new Dictionary<int, int>();

    /// <summary>今天已经修炼了几次（跨天清零）</summary>
    int 今日已修炼 = 0;
    int 今日已修炼归属天数 = -1;

    // ============================================================ 事件

    /// <summary>跨了一天（参数 = 新的天数）</summary>
    public event System.Action<int> 过了一天;
    /// <summary>时间推进时每帧触发（HUD 用），参数 = 当天进度</summary>
    public event System.Action<float> 时间推进;
    /// <summary>修炼机会有变化</summary>
    public event System.Action 机会变化;

    // ============================================================ 只读属性

    public int 当前天数 => 天数;
    public float 当天进度 => 日内进度;

    /// <summary>
    /// **含小数的总天数**（`天数 + 日内进度`），单调不减。
    ///
    /// 【为什么必须有它】"这一帧过了多少游戏日"**不能**用 `Time.deltaTime ÷ 秒每天` 去反算 ——
    /// <see cref="推进"/> 允许**一次跨好几天**（新加的「推进天数」任务动作、倍率调很大、读档补时间），
    /// 反算会把整段跳变丢掉。实测：主线阶段23「次日清晨」推进 1 天后，
    /// 灵田里的草**一点没长**（用 `Time.deltaTime` 的那版）。
    /// 唯一可靠的口径是**相邻两帧这个值之差**。
    /// </summary>
    public float 总天数 => 天数 + 日内进度;

    /// <summary>第几天 → 年（1 起）</summary>
    public int 年 => 起始年 + 天数 / Mathf.Max(1, 每月天数 * 每年月数);
    /// <summary>第几天 → 月（1 起）</summary>
    public int 月 => (天数 / Mathf.Max(1, 每月天数)) % Mathf.Max(1, 每年月数) + 1;
    /// <summary>第几天 → 日（1 起）</summary>
    public int 日 => 天数 % Mathf.Max(1, 每月天数) + 1;

    /// <summary>时辰名（把 1 天分 12 个时辰）</summary>
    public string 时辰
    {
        get
        {
            string[] 名 = { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
            int i = Mathf.Clamp((int)(日内进度 * 12f), 0, 11);
            return 名[i] + "时";
        }
    }

    /// <summary>给 HUD 用的一整行</summary>
    public string 纪年文本
        => $"太虚历 {年} 年 {月} 月 {日} 日 · {时辰}";

    /// <summary>当天还剩多少现实秒</summary>
    public float 今日剩余秒 => Mathf.Max(0f, (1f - 日内进度) * 秒每天);

    // ============================================================ 修炼机会

    /// <summary>每日机会还剩几次（已经排除过期的）</summary>
    public int 日常机会剩余
    {
        get
        {
            清理过期机会();
            int 总 = 0;
            foreach (var kv in 日常机会) 总 += kv.Value;
            return 总;
        }
    }

    /// <summary>总剩余 = 日常 + 打怪折算，并且不超今日上限</summary>
    public int 还能修炼几次
        => Mathf.Max(0, Mathf.Min(日常机会剩余 + Mathf.FloorToInt(打怪机会), 每日修炼上限 - 今日已修炼));

    /// <summary>今天已经修炼了几次</summary>
    public int 今日次数
    {
        get { 确保今日计数是今天的(); return 今日已修炼; }
    }

    /// <summary>今天还能修炼几次（= 上限 - 已用）</summary>
    public int 今日剩余额度
    {
        get { 确保今日计数是今天的(); return Mathf.Max(0, 每日修炼上限 - 今日已修炼); }
    }

    /// <summary>
    /// **花掉一次修炼机会。** 返回是否成功。
    ///
    /// 优先花**日常**的（因为它会过期，先花掉更划算），
    /// 日常花完再花打怪的。
    /// </summary>
    public bool 消耗一次修炼()
    {
        确保今日计数是今天的();
        if (今日剩余额度 <= 0) return false;
        if (还能修炼几次 <= 0) return false;

        清理过期机会();
        // 先花最早的那一天（FIFO，最老的先过期，所以先花它）
        int 最早 = int.MaxValue;
        foreach (var kv in 日常机会) if (kv.Value > 0 && kv.Key < 最早) 最早 = kv.Key;
        if (最早 != int.MaxValue)
        {
            日常机会[最早] -= 1;
        }
        else if (打怪机会 >= 1f)
        {
            打怪机会 -= 1f;
        }
        else if (日常机会剩余 > 0)
        {
            // 兜底：正常情况下上面已经花掉了
            foreach (var k in new List<int>(日常机会.Keys))
                if (日常机会[k] > 0) { 日常机会[k] -= 1; break; }
        }
        else return false;

        今日已修炼++;
        机会变化?.Invoke();

        // ★ 把这次机会**换成修炼系统认的"修炼次数"**（整数 1 次）。
        //
        //   为什么要这么绕一层：修炼小屋的 `PlayerCultivation.修炼一次()` 花的是
        //   `修炼次数累积`，而那个值同时也是**打怪掉的那一份**（不过期）。
        //   如果直接把每日机会塞进同一个池子，就**没法区分哪一份该过期**了。
        //   所以这里保持两套账：本类管"每日机会（会过期、每天上限 10）"，
        //   花掉的那一刻才转成修炼次数交给它。
        var 修 = FindObjectOfType<PlayerCultivation>();
        if (修 != null) 修.设置修炼次数(修.修炼次数累积 + 1f);

        return true;
    }

    /// <summary>加打怪获得的修炼机会（不过期）</summary>
    public void 加打怪机会(float 次数)
    {
        if (次数 <= 0f) return;
        打怪机会 += 次数;
        机会变化?.Invoke();
    }

    /// <summary>直接给日常机会（任务奖励用）</summary>
    public void 加日常机会(int 次)
    {
        if (次 <= 0) return;
        if (!日常机会.ContainsKey(天数)) 日常机会[天数] = 0;
        日常机会[天数] += 次;
        机会变化?.Invoke();
    }

    /// <summary>把"超过保存天数"的日常机会清掉</summary>
    void 清理过期机会()
    {
        if (日常机会.Count == 0) return;
        int 最老 = 天数 - 机会保存天数 + 1;
        var 过期 = new List<int>();
        foreach (var kv in 日常机会) if (kv.Key < 最老) 过期.Add(kv.Key);
        foreach (var k in 过期) 日常机会.Remove(k);
    }

    void 确保今日计数是今天的()
    {
        if (今日已修炼归属天数 == 天数) return;
        今日已修炼归属天数 = 天数;
        今日已修炼 = 0;
    }

    // ============================================================ 主循环

    void Awake()
    {
        if (实例 != null && 实例 != this) { Destroy(gameObject); return; }
        实例 = this;
    }

    void Update()
    {
        if (暂停) return;
        float 每天秒 = Mathf.Max(1f, 秒每天);
        推进(Time.deltaTime * 倍率 / 每天秒);
    }

    /// <summary>推进时间。参数 = 过了多少"天"（可以是小数）</summary>
    public void 推进(float 增量天)
    {
        if (增量天 <= 0f) return;
        日内进度 += 增量天;

        // 一帧里可能跨好几天（倍率调很大 / 读档补时间），所以用 while
        while (日内进度 >= 1f)
        {
            日内进度 -= 1f;
            天数++;
            确保今日计数是今天的();      // 新的一天，修炼次数归零
            发当日修炼机会();
            过了一天?.Invoke(天数);
        }

        时间推进?.Invoke(日内进度);
    }

    void 发当日修炼机会()
    {
        if (每日机会数 <= 0) return;
        日常机会[天数] = 每日机会数;
        清理过期机会();
        机会变化?.Invoke();
    }

    // ============================================================ 存档

    /// <summary>导出给存档</summary>
    public void 导出(out int 天, out float 进度, out string 机会, out float 打怪, out int 今日)
    {
        清理过期机会();
        确保今日计数是今天的();
        天 = 天数;
        进度 = 日内进度;
        打怪 = 打怪机会;
        今日 = 今日已修炼;

        var 项 = new List<string>();
        foreach (var kv in 日常机会) if (kv.Value > 0) 项.Add(kv.Key + ":" + kv.Value);
        机会 = string.Join(";", 项);
    }

    /// <summary>从存档恢复</summary>
    public void 导入(int 天, float 进度, string 机会, float 打怪, int 今日已用)
    {
        天数 = Mathf.Max(0, 天);
        日内进度 = Mathf.Clamp01(进度);
        打怪机会 = Mathf.Max(0f, 打怪);
        今日已修炼 = Mathf.Max(0, 今日已用);
        今日已修炼归属天数 = 天数;

        日常机会.Clear();
        if (!string.IsNullOrEmpty(机会))
        {
            foreach (var 段 in 机会.Split(';'))
            {
                if (string.IsNullOrEmpty(段)) continue;
                var kv = 段.Split(':');
                if (kv.Length != 2) continue;
                int d, n;
                if (int.TryParse(kv[0], out d) && int.TryParse(kv[1], out n) && n > 0)
                    日常机会[d] = n;
            }
        }

        // 【重要】如果存档里**今天还没发过**机会，补发一次 ——
        // 否则读档后当天永远领不到，玩家会以为坏了
        if (每日机会数 > 0 && !日常机会.ContainsKey(天数))
            日常机会[天数] = 每日机会数;

        清理过期机会();
        时间推进?.Invoke(日内进度);
        机会变化?.Invoke();
    }

    /// <summary>新开局：重置到第 0 天并立刻发第一次机会</summary>
    public void 重置()
    {
        天数 = 0;
        日内进度 = 0f;
        打怪机会 = 0f;
        今日已修炼 = 0;
        今日已修炼归属天数 = 0;
        日常机会.Clear();
        发当日修炼机会();
    }

    // ---- ASCII 别名 ----
    public int Day => 天数;
    public int Year => 年;
    public int Month => 月;
    public int DayOfMonth => 日;
    public int CultivationCharges => 还能修炼几次;
    public bool SpendCultivation() => 消耗一次修炼();
}
