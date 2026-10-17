using System.Collections.Generic;
using UnityEngine;

namespace Cultivation.WorldAuthoring
{
    public static class WorldModuleSnapping
    {
        struct End{public string owner,family;public Vector3 position,direction;}
        public static bool TrySnap(WorldMapData data,WorldMapPalette palette,WorldPaletteEntry entry,Vector3 pointer,float yaw,float scale,string excluded,out Vector3 position)
        {
            position=pointer;if(!entry.module)return false;var ends=new List<End>();
            foreach(var item in data.objects)
            {
                if(item.instanceId==excluded)continue;int index=palette.Find(item.paletteId);if(index<0)continue;var target=palette.entries[index].module;if(!target)continue;var rotation=Quaternion.Euler(0,item.yaw,0);
                for(int i=0;i<target.points.Length;i++)ends.Add(new End{owner=item.instanceId,family=target.family,position=item.position+rotation*(target.points[i]*item.scale),direction=rotation*target.directions[i]});
            }
            float closest=1.6f*1.6f;bool snapped=false;var orientation=Quaternion.Euler(0,yaw,0);
            foreach(var target in ends)
            {
                if(target.family!=entry.module.family)continue;bool occupied=false;
                foreach(var other in ends)if(other.owner!=target.owner&&(other.position-target.position).sqrMagnitude<.0064f){occupied=true;break;}
                if(occupied)continue;
                for(int port=0;port<entry.module.points.Length;port++)
                {
                    if(Vector3.Dot(orientation*entry.module.directions[port],target.direction)>-.98f)continue;
                    var candidate=target.position-orientation*(entry.module.points[port]*scale);float distance=new Vector2(candidate.x-pointer.x,candidate.z-pointer.z).sqrMagnitude;
                    if(distance>closest)continue;closest=distance;position=candidate;snapped=true;
                }
            }
            return snapped;
        }
    }
}
