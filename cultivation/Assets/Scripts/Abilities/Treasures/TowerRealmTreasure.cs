using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>镇妖阁法宝的入口及返回点；刷怪与层数仍由 TowerController 管理。</summary>
public static class TowerRealmTreasure
{
    public const string 法宝id = "treasure_tower_realm";
    public const string 塔场景 = "Demon-Suppressing Tower";
    public const string 家园场景 = "3C_Testbed";
    static TeleportBookmark returnPoint;
    static bool returning;
    public static bool 在塔内 => SceneManager.GetActiveScene().name == 塔场景;
    public static void 清除返回点() { returnPoint = null; returning = false; }
    public static TeleportBookmark 导出返回点() => returnPoint?.Copy();
    public static void 恢复返回点(TeleportBookmark point) => returnPoint = point?.Copy();
    public static bool 使用(GameObject player)
    {
        if (!player || 黑幕字幕.过渡进行中) return false;
        if (在塔内)
        {
            var tower = Object.FindObjectOfType<TowerController>();
            if (!tower) return false;
            tower.退到塔外(); return true;
        }
        var data = Object.FindObjectOfType<UIPanelData>();
        if (!data || data.当前法宝 == null || data.当前法宝.法宝id != 法宝id || !data.已拥有(data.当前法宝)) return false;
        if (player.scene.name != 家园场景) { data.ShowHint("请在洞府中展开镇妖阁"); return false; }
        returnPoint = new TeleportBookmark { 场景 = player.scene.name, 名称 = "镇妖阁施放处", 位置 = player.transform.position, 朝向 = player.transform.eulerAngles.y };
        黑幕字幕.开始场景过渡(塔场景, "镇妖阁", Vector3.zero, "TeleportTarget_入塔");
        return true;
    }
    public static void 返回现实()
    {
        if (黑幕字幕.过渡进行中) return;
        var point = returnPoint;
        returning = true;
        if (point != null && point.场景 == 家园场景)
            黑幕字幕.开始场景过渡(家园场景, "个人洞府", point.位置, null, 有坐标落点: true);
        else
            黑幕字幕.开始场景过渡(家园场景, "个人洞府");
    }
    public static void 完成返回(Transform player)
    {
        if (!returning || !player || player.gameObject.scene.name != 家园场景) return;
        if (returnPoint != null) player.rotation = Quaternion.Euler(0, returnPoint.朝向, 0);
        returning = false; returnPoint = null;
    }
}
