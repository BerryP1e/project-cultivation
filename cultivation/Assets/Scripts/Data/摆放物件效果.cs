using UnityEngine;

/// <summary>
/// **「用背包里的道具摆下一件东西」的通用效果** —— 灵田开拓令、练功木桩都走它。
///
/// 物品表里靠这两列驱动：
/// ```
/// 效果类型   = 摆放物件
/// 效果参数id = 摆放物库 的 id（lingtian / muzhuang）
/// ```
///
/// 【为什么要泛化】原来只有一个 `放置灵田效果`，木桩要么再抄一份、要么写成
/// "这个效果只会摆灵田"。泛化之后**加一种可摆放物 = 在 `摆放物库` 加一条 + 物品表加一行**，
/// 效果脚本一行都不用改（和 `服丹效果` / `属性增益效果` 一个路子）。
///
/// ⚠️ **道具是在这一步就被扣掉的**（`物品使用器.使用()`：效果返回 true → 扣一件），
/// 而"真的摆下去"还在后头。所以摆放态里按右键取消时，
/// <see cref="灵田摆放器.取消"/> 会**把道具还回背包** —— 不然点一下使用就白亏一张。
/// </summary>
[CreateAssetMenu(fileName = "摆放物件效果", menuName = "修仙/物品效果/摆放物件")]
public class 摆放物件效果 : 物品使用效果
{
    [Tooltip("要摆的是哪种物件（`摆放物库` 的 id，例：lingtian / muzhuang）")]
    public string 摆放物id = "";

    摆放物定义 定义 => 摆放物库.取(摆放物id);

    public override bool 能使用(物品使用请求 请求)
    {
        if (定义 == null) return false;
        if (!摆放校验.当前场景可摆放) return false;
        if (灵田摆放器.正在摆放) return false;
        return true;
    }

    public override string 不能用原因(物品使用请求 请求)
    {
        if (定义 == null)
            return "这件物品没配好：摆放物id「" + 摆放物id + "」在 摆放物库 里不存在";
        if (!摆放校验.当前场景可摆放)
            return (定义.名) + "只能在个人洞府里摆放（当前在「"
                 + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "」）";
        if (灵田摆放器.正在摆放) return "已经在摆放东西了";
        return "";
    }

    public override bool 使用(物品使用请求 请求)
    {
        if (灵田摆放器.开始摆(定义, out string 原因)) return true;   // 进摆放态 = 用掉了
        Debug.LogWarning("[摆放] 用不了：" + 原因);
        return false;                                                // false → 框架不扣道具
    }
}
