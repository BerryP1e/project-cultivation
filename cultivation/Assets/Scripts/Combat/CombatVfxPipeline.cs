using UnityEngine;

/// <summary>统一命中表现入口；结算成功后由攻击调用，保留独立的地表落点事件。</summary>
public static class CombatVfxPipeline
{
    public const string 太虚剑命中="taixu_sword_hit",火斩命中="fire_slash_hit",太虚炼气命中="remote_hit",雷命中="thunder_hit",火球落地="meteor_ground";
    public static readonly string[] 雷四式={"thunder_a1","thunder_a2","thunder_a3","thunder_a4"};
    static CombatVfxCatalog catalog;
    public static CombatVfxCatalog 配置=>catalog?catalog:(catalog=Resources.Load<CombatVfxCatalog>("Combat/CombatVfxCatalog"));

    public static GameObject 播放(string id,Vector3 point,Vector3 direction,float size=1f,Transform target=null)
    {
        var entry=配置?配置.查找(id):null;
        if(entry==null||!entry.预制体){Debug.LogWarning("[战斗特效] 缺少配置："+id);return null;}
        if(entry.模型外侧显示 && target) point=模型外侧位置(point,target,entry.外侧留白);
        var camera=Camera.main;
        if(entry.朝向镜头 && camera) direction=point-camera.transform.position;
        var rotation=Quaternion.Euler(entry.旋转欧拉);
        if(entry.跟随来向&&direction.sqrMagnitude>.00001f)rotation=Quaternion.LookRotation(direction.normalized)*rotation;
        // 保留资源原生缩放/旋转，忽略资源包演示场景的根位置。
        var go=Object.Instantiate(entry.预制体,point,rotation*entry.预制体.transform.localRotation);
        go.name="CombatVfx_"+id;
        go.transform.localScale=entry.预制体.transform.localScale*Mathf.Max(.001f,entry.缩放*size);
        var adapter=go.AddComponent<ExtremeVfxAdapter>();adapter.散布半径=entry.散布半径*size;adapter.关闭场景灯光=entry.关闭场景灯光;
        adapter.灯光强度倍率=entry.灯光强度倍率;adapter.灯光最大半径=entry.灯光最大半径;
        adapter.刀光最大宽度=entry.刀光最大宽度*size;
        adapter.调色=entry.调色;
        adapter.初始化(0,Mathf.Abs(go.transform.lossyScale.x));
        // 根节点的原生PositionOffset不应把闪光重新推回受击模型内部。
        if(entry.模型外侧显示 && target) go.transform.position=point;
        Object.Destroy(go,Mathf.Max(.1f,entry.最长存活));
        return go;
    }

    public static GameObject 命中(string id,AttackResult result,Vector3 point,Vector3 direction,float size=1f,Transform target=null)
        =>result.有效接触?播放(id,point,direction,size*(result.暴击?1.15f:1f),target):null;

    /// <summary>仅沿观察方向移到单位碰撞包围盒外；保留接触点的屏幕位置和正常深度遮挡。</summary>
    public static Vector3 模型外侧位置(Vector3 point,Transform target,float padding=.12f)
    {
        var camera=Camera.main;
        if(!camera || !target)return point;
        Vector3 normal=(camera.transform.position-point).normalized;
        if(normal.sqrMagnitude<.5f)return point;
        Bounds bounds=default;bool found=false;
        foreach(var c in target.GetComponentsInChildren<Collider>()){
            if(!c.enabled || c.isTrigger || CombatTargets.从(c.gameObject)?.根!=target)continue;
            if(!found){bounds=c.bounds;found=true;}else bounds.Encapsulate(c.bounds);
        }
        if(!found)foreach(var r in target.GetComponentsInChildren<SkinnedMeshRenderer>()){
            if(!r.enabled)continue;
            if(!found){bounds=r.bounds;found=true;}else bounds.Encapsulate(r.bounds);
        }
        if(!found)return point;
        var e=bounds.extents;
        float surface=Vector3.Dot(bounds.center-point,normal)+Mathf.Abs(normal.x)*e.x+Mathf.Abs(normal.y)*e.y+Mathf.Abs(normal.z)*e.z;
        return point+normal*Mathf.Max(0,surface+Mathf.Max(0,padding));
    }
}
