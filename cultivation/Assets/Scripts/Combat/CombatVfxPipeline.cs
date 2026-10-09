using UnityEngine;

/// <summary>统一命中表现入口；结算成功后由攻击调用，保留独立的地表落点事件。</summary>
public static class CombatVfxPipeline
{
    public const string 太虚剑命中="taixu_sword_hit",火斩命中="fire_slash_hit",太虚炼气命中="remote_hit",雷命中="thunder_hit",火球落地="meteor_ground";
    public static readonly string[] 雷四式={"thunder_a1","thunder_a2","thunder_a3","thunder_a4"};
    static CombatVfxCatalog catalog;
    public static CombatVfxCatalog 配置=>catalog?catalog:(catalog=Resources.Load<CombatVfxCatalog>("Combat/CombatVfxCatalog"));

    public static GameObject 播放(string id,Vector3 point,Vector3 direction,float size=1f)
    {
        var entry=配置?配置.查找(id):null;
        if(entry==null||!entry.预制体){Debug.LogWarning("[战斗特效] 缺少配置："+id);return null;}
        var rotation=Quaternion.Euler(entry.旋转欧拉);
        if(entry.跟随来向&&direction.sqrMagnitude>.00001f)rotation=Quaternion.LookRotation(direction.normalized)*rotation;
        // 保留资源原生缩放/旋转，忽略资源包演示场景的根位置。
        var go=Object.Instantiate(entry.预制体,point,rotation*entry.预制体.transform.localRotation);
        go.name="CombatVfx_"+id;
        go.transform.localScale=entry.预制体.transform.localScale*Mathf.Max(.001f,entry.缩放*size);
        var adapter=go.AddComponent<ExtremeVfxAdapter>();adapter.散布半径=entry.散布半径*size;adapter.关闭场景灯光=entry.关闭场景灯光;
        adapter.灯光强度倍率=entry.灯光强度倍率;adapter.灯光最大半径=entry.灯光最大半径;
        adapter.初始化(0,Mathf.Abs(go.transform.lossyScale.x));
        Object.Destroy(go,Mathf.Max(.1f,entry.最长存活));
        return go;
    }

    public static GameObject 命中(string id,AttackResult result,Vector3 point,Vector3 direction,float size=1f)
        =>result.命中?播放(id,point,direction,size*(result.暴击?1.15f:1f)):null;
}
