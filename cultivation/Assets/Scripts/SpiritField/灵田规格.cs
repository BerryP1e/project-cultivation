using UnityEngine;

/// <summary>
/// **灵田的尺寸与摆放几何** —— 所有跟"一块地占多大、离多近算压住、吸附到哪"有关的数字都在这里。
///
/// 【为什么要单独一份】这些数有**三个**消费者，必须完全一致：
///   · <see cref="灵田地块外观"/> —— 照它造土床网格；
///   · <see cref="灵田"/> —— 照它判"这个位置能不能放"；
///   · <see cref="灵田摆放器"/> —— 照它算吸附位置。
/// 分散在三个文件里写数字，早晚会出现"看着没挨着、却提示压住了"这种对不上的 bug。
///
/// 当前取值与它们之间的关系（**动一个就要重算另外两个**）：
/// <code>
///   床边长 1.9            （土床的实际大小）
///   占用边距 0.35         （地周围这一圈也算被占，用户要求"它以及它周围的一部分空间会被标记为不可摆放"）
///   ⇒ 占用半边长 = 0.95 + 0.35 = 1.30
///   ⇒ 任意摆放的最小中心距 = 2 × 1.30 = 2.60
///
///   对齐间隙 0.8          （吸附成一排时，两块地之间的过道宽度）
///   ⇒ 吸附后的中心距 = 1.9 + 0.8 = 2.70   （> 2.60，所以吸附出来的排布一定是合法的）
/// </code>
/// </summary>
public static class 灵田规格
{
    /// <summary>土床边长（米）。正方形</summary>
    public const float 床边长 = 1.9f;

    /// <summary>床面高度（离地）。必须明显小于玩家 <c>CharacterController.stepOffset</c>（0.30）</summary>
    public const float 床顶 = 0.10f;

    /// <summary>垄高（床面之上再鼓起来多少）。太平了就看不出是"畦"</summary>
    public const float 垄高 = 0.08f;

    /// <summary>垄数（作物就种在垄顶）</summary>
    public const int 垄数 = 3;

    /// <summary>垄顶高度 = 作物落脚的高度</summary>
    public const float 垄顶 = 床顶 + 垄高;

    /// <summary>地周围这一圈也算被占用（米）</summary>
    public const float 占用边距 = 0.35f;

    /// <summary>占用半边长 = 半块地 + 边距</summary>
    public const float 占用半边长 = 床边长 * 0.5f + 占用边距;

    /// <summary>吸附成一排时的过道宽度（米）</summary>
    public const float 对齐间隙 = 0.8f;

    /// <summary>吸附后的中心距</summary>
    public const float 吸附中心距 = 床边长 + 对齐间隙;

    /// <summary>离多近才尝试吸附（米）。给得比间隙小，免得"离得老远就被吸走"</summary>
    public const float 吸附距离 = 0.9f;

    /// <summary>可摆放的朝向档数（用户要求 8 个朝向）</summary>
    public const int 朝向档数 = 8;

    /// <summary>一档多少度</summary>
    public const float 每档角度 = 360f / 朝向档数;

    /// <summary>角度 → 最近的一档（0~7）</summary>
    public static int 角度转档(float 角度)
    {
        int 档 = Mathf.RoundToInt(角度 / 每档角度) % 朝向档数;
        return 档 < 0 ? 档 + 朝向档数 : 档;
    }

    /// <summary>档 → 角度</summary>
    public static float 档转角度(int 档) => 档 * 每档角度;

    /// <summary>某个朝向的"本地 X 轴"（世界水平方向）</summary>
    public static Vector2 轴(int 朝向档) => 方向(档转角度(朝向档));

    static Vector2 方向(float 度)
    {
        float r = 度 * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    /// <summary>
    /// 两块地（含各自的占用边距）**压住了没有**。
    ///
    /// 用 **SAT（分离轴定理）** 判两个任意旋转的方块：把两个方块的 4 条法线当候选轴，
    /// 只要在某一根轴上投影不重叠，就一定不冲突。
    /// 【为什么不用圆圈近似】方块转 45° 时外接圆比内切圆大 40%，
    /// 用圆判会把**明明能并排摆下的两块地判成压住**；用户要的正是整齐并排。
    /// </summary>
    public static bool 压住(Vector2 甲, int 甲朝向档, Vector2 乙, int 乙朝向档)
    {
        Vector2 d = 乙 - 甲;
        float h = 占用半边长;

        var 甲轴 = new[] { 轴(甲朝向档), new Vector2(-轴(甲朝向档).y, 轴(甲朝向档).x) };
        var 乙轴 = new[] { 轴(乙朝向档), new Vector2(-轴(乙朝向档).y, 轴(乙朝向档).x) };

        foreach (var a in new[] { 甲轴[0], 甲轴[1], 乙轴[0], 乙轴[1] })
        {
            float 中心距 = Mathf.Abs(Vector2.Dot(d, a));
            float 半径和 = h * 投影半径(甲轴, a) + h * 投影半径(乙轴, a);
            if (中心距 > 半径和) return false;      // 找到分离轴 → 没压住
        }
        return true;                                 // 四根轴都重叠 → 压住了
    }

    /// <summary>一个方块在某根轴上的投影半径（方块半边长 = 1 时的比例）</summary>
    static float 投影半径(Vector2[] 方块轴, Vector2 轴)
        => Mathf.Abs(Vector2.Dot(方块轴[0], 轴)) + Mathf.Abs(Vector2.Dot(方块轴[1], 轴));

    /// <summary>
    /// 如果要把一块地**吸附**到 <paramref name="已有"/> 旁边，它该摆在哪。
    /// 只对**同朝向**的地做吸附（用户要求「同一个朝向的同种物品」）。
    /// 返回 4 个候选（已有地的 ±X / ±Z 两侧），离得太远的已经滤掉。
    /// </summary>
    public static void 吸附候选(Vector2 已有, int 朝向档, Vector2 想放, System.Collections.Generic.List<Vector2> 出)
    {
        出.Clear();
        Vector2 x = 轴(朝向档);
        Vector2 z = new Vector2(-x.y, x.x);

        Vector2[] 四向 = { x, -x, z, -z };
        foreach (var 向 in 四向)
        {
            Vector2 候选 = 已有 + 向 * 吸附中心距;
            if (Vector2.Distance(候选, 想放) <= 吸附距离) 出.Add(候选);
        }
    }
}
