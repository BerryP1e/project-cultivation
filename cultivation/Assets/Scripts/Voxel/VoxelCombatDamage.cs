using UnityEngine;

/// <summary>Player attack entry points. Mesh/collider work stays in the map's shared frame budget.</summary>
public static class VoxelCombatDamage
{
    static VoxelMapPilot map;
    static VoxelMapPilot Map
    {
        get { if(!map)map=Object.FindObjectOfType<VoxelMapPilot>();return map && map.正在替换?map:null; }
    }
    public static void Sphere(Vector3 point,float radius)
    {var pilot=Map;if(pilot)pilot.排队破坏球(point,radius);}
    public static void Ellipsoid(Vector3 point,float radius,float depth)
    {var pilot=Map;if(pilot)pilot.排队破坏椭球(point,radius,depth);}
    public static bool Active=>Map!=null;
    // Shared by actual area cuts and the cursor guide. Enemy damage keeps its own range.
    public static float AreaRadius(ActiveDivineAbility skill)
    {
        float radius=Mathf.Max(.4f,skill.范围);
        return skill.神通id=="ability_fentian_yanshu"?radius/12f:radius;
    }
    public static bool Ray(Vector3 from,Vector3 to,out RaycastHit hit,bool onlyVoxels=true,Transform owner=null)
    {
        hit=default;var delta=to-from;if(delta.sqrMagnitude<1e-8f)return false;
        var hits=Physics.RaycastAll(from,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));var pilot=Map;
        foreach(var candidate in hits)
        {
            if(owner && candidate.transform.IsChildOf(owner))continue;
            if(onlyVoxels && (!pilot || !pilot.可破坏(candidate.collider)))continue;
            hit=candidate;return true;
        }
        return false;
    }
}
