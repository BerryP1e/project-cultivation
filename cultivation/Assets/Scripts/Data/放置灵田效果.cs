using UnityEngine;

/// <summary>
/// **「灵田开拓令」这类道具的使用效果**：从背包里点「使用」→ 进入**灵田摆放态**。
///
/// 用户 2026-10-01 的用法原文：
/// > 「在背包中找到**灵田开拓令**，假如是在洞府中使用的灵田开拓令
/// >   （**不在洞府场景无法使用开拓令**），取消面板，鼠标会变成一块待摆放的半透明的灵田，
/// >   可以在整个洞府中有地面的有空间的地方任意的摆放。」
///
/// 所以这个效果本身很薄，只做两件事：
///   1. **把关**：不在洞府 / 已经在摆放中 → <see cref="能使用"/> 直接 false，
///      背包里的「使用」按钮就是灰的，<see cref="不能用原因"/> 给玩家一句人话；
///   2. **交棒**：<see cref="使用"/> 把控制权交给 <see cref="灵田摆放器"/>。
///
/// ⚠️ **道具是在这一步就被扣掉的**（`物品使用器.使用()` 的流程：效果返回 true → 扣一件），
/// 而"真的摆下去"还在后头。所以玩家在摆放态按右键取消时，
/// <see cref="灵田摆放器.取消"/> 会**把开拓令还回背包** —— 不然点一下使用就白亏一张。
/// </summary>
[CreateAssetMenu(fileName = "放置灵田效果", menuName = "修仙/物品效果/放置灵田")]
public class 放置灵田效果 : 物品使用效果
{
    public override bool 能使用(物品使用请求 请求)
    {
        var 田 = 灵田.取();
        if (田 == null) return false;
        if (!田.当前场景可摆放) return false;
        if (灵田摆放器.正在摆放) return false;
        return true;
    }

    public override string 不能用原因(物品使用请求 请求)
    {
        var 田 = 灵田.取();
        if (田 == null) return "找不到灵田系统";
        if (!田.当前场景可摆放)
            return "灵田只能在个人洞府里开垦（当前在「"
                 + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "」）";
        if (灵田摆放器.正在摆放) return "已经在摆放灵田了";
        return "";
    }

    public override bool 使用(物品使用请求 请求)
    {
        if (!灵田摆放器.开始开新地(out string 原因))
        {
            Debug.LogWarning("[灵田] 用不了开拓令：" + 原因);
            return false;                 // 返回 false → 框架不会扣道具
        }
        return true;                      // 进入摆放态就算"用掉了"；取消时摆放器会退还
    }
}
