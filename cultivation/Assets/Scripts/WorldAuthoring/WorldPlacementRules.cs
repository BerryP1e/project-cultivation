using UnityEngine;

namespace Cultivation.WorldAuthoring
{
    public static class WorldPlacementRules
    {
        public static Vector2 Footprint(WorldPaletteEntry entry,float scale)
        {
            var size=entry.footprint;
            if(size.x<=0 || size.y<=0)size=new Vector2(entry.bounds.size.x,entry.bounds.size.z);
            return size*scale;
        }

        public static bool OnPondBank(WorldMapData data,float x,float z,float margin=.40f)
        {
            if(data.Sample(data.water,x,z)>.012f)return true;
            for(int i=0;i<8;i++)
            {
                float angle=i*Mathf.PI*.25f;
                if(data.Sample(data.water,x+Mathf.Cos(angle)*margin,z+Mathf.Sin(angle)*margin)>.012f)return true;
            }
            return false;
        }

        public static bool Contains(WorldPaletteEntry entry,MapObjectData item,float x,float z,float margin=0)
        {
            if(item.exactTransform)
            {
                var scaled=item.AxisScale;var localExact=Quaternion.Inverse(item.Orientation)*(new Vector3(x,item.position.y,z)-item.position);localExact=new Vector3(localExact.x/scaled.x,localExact.y/scaled.y,localExact.z/scaled.z);var offset=entry.FootprintOffset;
                return Mathf.Abs(localExact.x-offset.x)<entry.footprint.x*.5f+margin/Mathf.Abs(scaled.x)&&Mathf.Abs(localExact.z-offset.y)<entry.footprint.y*.5f+margin/Mathf.Abs(scaled.z);
            }
            var size=Footprint(entry,item.scale)*.5f+Vector2.one*margin;
            var local=Quaternion.Euler(0,-item.yaw,0)*(new Vector3(x,0,z)-new Vector3(item.position.x,0,item.position.z));
            if(entry.module)return entry.module.DistanceToBody(local/item.scale)<margin/item.scale;
            local-=new Vector3(entry.FootprintOffset.x,0,entry.FootprintOffset.y)*item.scale;
            return Mathf.Abs(local.x)<size.x && Mathf.Abs(local.z)<size.y;
        }

        public static Vector3 JoinNearbyRock(WorldMapData data,WorldMapPalette palette,WorldPaletteEntry entry,Vector3 pointer,float yaw,float scale,string exclude=null)
        {
            if(entry.category!="岩石")return pointer;
            Vector3 result=pointer;float best=.48f;
            foreach(var item in data.objects)
            {
                if(item.instanceId==exclude)continue;
                int index=palette.Find(item.paletteId);if(index<0||palette.entries[index].category!="岩石")continue;
                var direction=Vector3.ProjectOnPlane(pointer-item.position,Vector3.up);float distance=direction.magnitude;if(distance<.05f)continue;direction/=distance;
                float own=ProjectedHalfWidth(entry,scale,yaw,direction),other=ProjectedHalfWidth(palette.entries[index],item.scale,item.yaw,direction);
                float gap=distance-own-other;
                if(gap<0||gap>best)continue;best=gap;
                result=item.position+direction*(own+other-Mathf.Min(.18f,Mathf.Min(own,other)*.12f));result.y=pointer.y;
            }
            return result;
        }
        static float ProjectedHalfWidth(WorldPaletteEntry entry,float scale,float yaw,Vector3 direction)
        {
            var local=Quaternion.Euler(0,-yaw,0)*direction;var half=Footprint(entry,scale)*.5f;
            return Mathf.Abs(local.x)*half.x+Mathf.Abs(local.z)*half.y;
        }

        public static bool Evaluate(WorldMapData data,WorldPaletteEntry entry,Vector3 pointer,float yaw,float scale,bool natural,float heightOffset,out Vector3 position,out float contactHeight,out string reason)
        {
            position=pointer;contactHeight=0;reason="";
            if(entry.IsBridge)return EvaluateBridge(data,entry,pointer,yaw,scale,natural,heightOffset,out position,out contactHeight,out reason);
            var size=Footprint(entry,scale);var rotation=Quaternion.Euler(0,yaw,0);
            int nx=Mathf.Clamp(Mathf.CeilToInt(size.x/.35f),2,80),nz=Mathf.Clamp(Mathf.CeilToInt(size.y/.35f),2,80);
            float minimum=float.MaxValue,maximum=float.MinValue;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float u=x/(float)nx*2-1,v=z/(float)nz*2-1;
                if(entry.category=="岩石" && u*u+v*v>1.10f)continue;
                var p=pointer+rotation*new Vector3(u*size.x*.5f+entry.FootprintOffset.x*scale,0,v*size.y*.5f+entry.FootprintOffset.y*scale);
                if(!data.HasGround(p.x,p.z)){reason="占地超出地面，不能放置";return false;}
                if(OnPondBank(data,p.x,p.z)){reason="水域和岸坡为禁摆区，请向陆地内移动";return false;}
                float height=data.Sample(data.height,p.x,p.z);minimum=Mathf.Min(minimum,height);maximum=Mathf.Max(maximum,height);
            }
            float tolerance=entry.category=="岩石"?Mathf.Max(.45f,entry.bounds.size.y*scale*.45f):entry.category=="树木"?.8f:entry.category=="草花"?.45f:.25f;
            if(natural && maximum-minimum>tolerance){reason="地面高差过大，请先平滑，或选择保留间隙";return false;}
            contactHeight=(minimum+maximum)*.5f;
            float bury=entry.category=="岩石"?Mathf.Max(.10f,entry.bounds.size.y*scale*.22f):entry.category=="树木"?.10f:.035f;
            position.y=natural?minimum-bury+heightOffset:maximum+Mathf.Max(0,heightOffset);
            reason=natural?"贴地融合 · 点击放置":"保留间隙 · 点击放置";
            return true;
        }
        static bool EvaluateBridge(WorldMapData data,WorldPaletteEntry entry,Vector3 pointer,float yaw,float scale,bool natural,float heightOffset,out Vector3 position,out float contactHeight,out string reason)
        {
            position=pointer;contactHeight=0;reason="";var size=Footprint(entry,scale);bool alongX=size.x>=size.y;var rotation=Quaternion.Euler(0,yaw,0);float low=float.MaxValue,high=float.MinValue;
            for(int end=-1;end<=1;end+=2)for(int across=-1;across<=1;across++)
            {
                var local=alongX?new Vector3(end*size.x*.43f,0,across*size.y*.24f):new Vector3(across*size.x*.24f,0,end*size.y*.43f);
                local+=new Vector3(entry.FootprintOffset.x,0,entry.FootprintOffset.y)*scale;var p=pointer+rotation*local;
                if(Mathf.Abs(p.x)>data.Width*.5f||Mathf.Abs(p.z)>data.Width*.5f){reason="桥体超出地图范围";return false;}
                bool supported=data.HasGround(p.x,p.z)&&!data.IsWet(p.x,p.z);
                if(natural&&!supported){reason="桥身可跨水；请让两端桥头落在岸上，或切换保留间隙手动架设";return false;}
                float height=supported?data.Sample(data.height,p.x,p.z):pointer.y;low=Mathf.Min(low,height);high=Mathf.Max(high,height);
            }
            if(natural&&high-low>.75f){reason="两端桥头高差过大，请调整岸坡或使用保留间隙";return false;}
            contactHeight=(low+high)*.5f;position.y=(natural?high:Mathf.Max(pointer.y,high))+heightOffset-entry.bridgeDeckHeight*scale-.025f;
            reason=natural?"跨水架桥 · 两端贴岸":"跨水架设 · 手动高度";return true;
        }
    }
}
