using System.Collections.Generic;
using UnityEngine;

/// <summary>洞府局部网格A*，每次出发计算一次，不重建场景导航网格。</summary>
public static class HomeFarmPath
{
    const float spacing=.8f;
    static Vector2Int Cell(Vector3 p)=>new Vector2Int(Mathf.RoundToInt(p.x/spacing),Mathf.RoundToInt(p.z/spacing));
    static Vector3 Point(Vector2Int c)=>new Vector3(c.x*spacing,0,c.y*spacing);
    static bool Clear(Vector3 p,Transform worker)
    {
        if(!HomeWorld.取().包含(p,.38f))return false;
        foreach(var c in Physics.OverlapSphere(p+Vector3.up*.7f,.32f,~0,QueryTriggerInteraction.Ignore))
        {
            if(c is CharacterController||c.GetComponentInParent<HomeSurface>()||c.GetComponentInParent<灵田地块>())continue;
            if(worker&&c.transform.IsChildOf(worker))continue;
            var station=c.GetComponentInParent<HomePuppetStation>();if(station&&station.可行走踏板(c))continue;
            return false;
        }
        return true;
    }
    public static List<Vector3> 查找(Vector3 from,Vector3 to,Transform worker)
    {
        var start=Cell(from);var goal=Cell(to);
        var open=new List<Vector2Int>{start};var closed=new HashSet<Vector2Int>();
        var cost=new Dictionary<Vector2Int,float>{{start,0}};
        var parent=new Dictionary<Vector2Int,Vector2Int>();
        var passable=new Dictionary<Vector2Int,bool>();
        bool Pass(Vector2Int c){if(passable.TryGetValue(c,out bool ok))return ok;return passable[c]=Clear(Point(c),worker);}
        for(int iteration=0;open.Count>0&&iteration<7000;iteration++)
        {
            int best=0;float score=float.MaxValue;
            for(int i=0;i<open.Count;i++){float f=cost[open[i]]+Vector2Int.Distance(open[i],goal);if(f<score){best=i;score=f;}}
            var current=open[best];open.RemoveAt(best);
            if(current==goal)
            {
                var result=new List<Vector3>{to};
                while(current!=start){result.Add(Point(current));current=parent[current];}
                result.Reverse();return result;
            }
            closed.Add(current);
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
            {
                if(x==0&&z==0)continue;var next=current+new Vector2Int(x,z);
                if(closed.Contains(next)||!Pass(next))continue;
                if(x!=0&&z!=0&&(!Pass(current+new Vector2Int(x,0))||!Pass(current+new Vector2Int(0,z))))continue;
                float g=cost[current]+(x!=0&&z!=0?1.4142f:1);
                if(cost.TryGetValue(next,out float old)&&old<=g)continue;
                cost[next]=g;parent[next]=current;if(!open.Contains(next))open.Add(next);
            }
        }
        return null;
    }
}
