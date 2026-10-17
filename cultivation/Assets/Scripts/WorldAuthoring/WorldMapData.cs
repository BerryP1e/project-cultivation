using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cultivation.WorldAuthoring
{
    public enum WorldBrush { Ground, EraseGround, Raise, Lower, Smooth, Grass, Pond, Place, EraseObject, Spawn, EraseGrass, FillPond, RestoreGround, Surface, EraseSurface, SelectObject }

    [Serializable]
    public sealed class MapObjectData
    {
        public string instanceId, paletteId;
        public Vector3 position;
        public float yaw, scale = 1;
        public bool naturalContact;
        public float contactHeight;
        public int sizingVersion;
        public bool exactTransform;
        public Vector3 rotation,axisScale=Vector3.one;
        public string featureId;
        public Quaternion Orientation=>exactTransform?Quaternion.Euler(rotation.x,yaw,rotation.z):Quaternion.Euler(0,yaw,0);
        public Vector3 AxisScale=>exactTransform?axisScale*scale:Vector3.one*scale;
    }

    [Serializable]
    public sealed class WorldMapData
    {
        public int version = 1, resolution = 129, seed = 20261015;
        public string name = "青岚试作";
        public float cellSize = .5f, floor = -5;
        public float[] land, height, grass, water, waterHeight, dryHeight;
        public float[] surfaceIndex,surfaceBlend;
        public List<string> surfaceIds=new List<string>();
        public float fogHeight=-.8f,fogSpeed=1,fogDensity=1;
        public bool bottomFogOnly;
        public float fogBaseHeight;
        public float grassDensity=.55f;
        public int authoringVersion;
        public const int MaxResolution=1025;
        public Vector3 spawn = new Vector3(0, 2, 0);
        public bool rockBoundary = true, cloudSea = true;
        public List<MapObjectData> objects = new List<MapObjectData>();
        public string sourceScene;
        public List<WorldFeatureData> features=new List<WorldFeatureData>();
        public float Width => (resolution-1)*cellSize;
        public int Index(int x,int z) => z*resolution+x;
        public Vector3 Position(int x,int z) => new Vector3(x*cellSize-Width*.5f,height[Index(x,z)],z*cellSize-Width*.5f);

        public static WorldMapData Empty(string name = "新地图")
        {
            var data = new WorldMapData { name = name };
            int count=data.resolution*data.resolution;
            data.land=new float[count];data.height=new float[count];data.grass=new float[count];data.water=new float[count];data.waterHeight=new float[count];data.dryHeight=new float[count];
            data.surfaceIndex=new float[count];data.surfaceBlend=new float[count];
            return data;
        }

        public WorldMapData Clone()
        {
            var copy=(WorldMapData)MemberwiseClone();
            copy.land=(float[])land.Clone();copy.height=(float[])height.Clone();copy.grass=(float[])grass.Clone();copy.water=(float[])water.Clone();copy.waterHeight=(float[])waterHeight.Clone();
            copy.dryHeight=dryHeight!=null?(float[])dryHeight.Clone():(float[])height.Clone();
            copy.surfaceIndex=surfaceIndex!=null?(float[])surfaceIndex.Clone():new float[height.Length];copy.surfaceBlend=surfaceBlend!=null?(float[])surfaceBlend.Clone():new float[height.Length];copy.surfaceIds=new List<string>(surfaceIds??new List<string>());
            copy.objects=new List<MapObjectData>(objects.Count);
            foreach(var item in objects) copy.objects.Add(new MapObjectData {instanceId=item.instanceId,paletteId=item.paletteId,position=item.position,yaw=item.yaw,scale=item.scale,naturalContact=item.naturalContact,contactHeight=item.contactHeight,sizingVersion=item.sizingVersion,exactTransform=item.exactTransform,rotation=item.rotation,axisScale=item.axisScale,featureId=item.featureId});
            copy.features=new List<WorldFeatureData>();foreach(var feature in features??new List<WorldFeatureData>())copy.features.Add(feature.Clone());
            return copy;
        }

        public void Validate()
        {
            if(authoringVersion<2){fogHeight=-.8f;fogSpeed=1;fogDensity=1;}
            if(authoringVersion<3){grassDensity=.55f;authoringVersion=3;}
            if(version!=1 || resolution<17 || resolution>MaxResolution || !Finite(cellSize) || cellSize<.2f || cellSize>4 || !Finite(floor)) throw new InvalidDataException("地图版本或尺寸无效。");
            int count=resolution*resolution;
            foreach(var values in new[]{land,height,grass,water,waterHeight})
            {
                if(values==null || values.Length!=count)throw new InvalidDataException("地图地形数据不完整。");
                foreach(var value in values)if(!Finite(value))throw new InvalidDataException("地图含无效数值。");
            }
            if(dryHeight==null || dryHeight.Length!=count)
            {
                dryHeight=(float[])height.Clone();
                for(int i=0;i<count;i++)if(water[i]>.001f)dryHeight[i]=Mathf.Max(height[i],waterHeight[i]+.35f);
            }
            foreach(float value in dryHeight)if(!Finite(value)||value<floor+.3f||value>80)throw new InvalidDataException("地图原地表高度无效。");
            if(surfaceIndex==null||surfaceIndex.Length!=count)surfaceIndex=new float[count];if(surfaceBlend==null||surfaceBlend.Length!=count)surfaceBlend=new float[count];if(surfaceIds==null)surfaceIds=new List<string>();
            if(surfaceIds.Count>255)throw new InvalidDataException("地表图层过多。");
            for(int i=0;i<count;i++)if(!Finite(surfaceBlend[i])||surfaceBlend[i]<0||surfaceBlend[i]>1||!Finite(surfaceIndex[i])||surfaceIndex[i]<0||surfaceIndex[i]>surfaceIds.Count)throw new InvalidDataException("地表笔刷数据无效。");
            if(!Finite(fogHeight)||!Finite(fogSpeed)||!Finite(fogDensity)||!Finite(fogBaseHeight))throw new InvalidDataException("雾设置无效。");
            if(!Finite(grassDensity)||grassDensity<0||grassDensity>1)throw new InvalidDataException("草密度无效。");
            for(int i=0;i<count;i++)
                if(land[i]<0 || land[i]>1 || grass[i]<0 || grass[i]>1 || water[i]<0 || water[i]>1 || height[i]<floor+.3f || height[i]>80)
                    throw new InvalidDataException("地图地形数值超出范围。");
            if(objects==null || objects.Count>10000)throw new InvalidDataException("地图物件数量无效。");
            var ids=new HashSet<string>();
            foreach(var item in objects)
                if(item==null || string.IsNullOrEmpty(item.instanceId) || !ids.Add(item.instanceId) || string.IsNullOrEmpty(item.paletteId) || !Finite(item.scale) || item.scale<.05f || item.scale>20 || !Finite(item.yaw) || !Finite(item.position.x) || !Finite(item.position.y) || !Finite(item.position.z) || !Finite(item.contactHeight))
                    throw new InvalidDataException("地图物件数据无效。");
            foreach(var item in objects)if(item.exactTransform&&(!Finite(item.axisScale.x)||!Finite(item.axisScale.y)||!Finite(item.axisScale.z)||!Finite(item.rotation.x)||!Finite(item.rotation.y)||!Finite(item.rotation.z)||Mathf.Abs(item.axisScale.x)<.00001f||Mathf.Abs(item.axisScale.y)<.00001f||Mathf.Abs(item.axisScale.z)<.00001f))throw new InvalidDataException("地图物件变换无效。");
            if(features==null)features=new List<WorldFeatureData>();ids.Clear();foreach(var feature in features)if(feature==null||string.IsNullOrEmpty(feature.id)||!ids.Add(feature.id)||string.IsNullOrEmpty(feature.templateId)||!Finite(feature.position.x)||!Finite(feature.position.y)||!Finite(feature.position.z))throw new InvalidDataException("地图功能模块身份或坐标无效。");
            if(!Finite(spawn.x) || !Finite(spawn.y) || !Finite(spawn.z))throw new InvalidDataException("出生点无效。");
        }

        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

        public float Sample(float[] values,float x,float z)
        {
            float gx=(x+Width*.5f)/cellSize,gz=(z+Width*.5f)/cellSize;
            if(gx<0 || gz<0 || gx>resolution-1 || gz>resolution-1)return 0;
            int ix=Mathf.Min(resolution-2,Mathf.FloorToInt(gx)),iz=Mathf.Min(resolution-2,Mathf.FloorToInt(gz));
            float a=Mathf.Lerp(values[Index(ix,iz)],values[Index(ix+1,iz)],gx-ix);
            float b=Mathf.Lerp(values[Index(ix,iz+1)],values[Index(ix+1,iz+1)],gx-ix);
            return Mathf.Lerp(a,b,gz-iz);
        }
        public bool HasGround(float x,float z)=>Sample(land,x,z)>.5f;
        public bool IsWet(float x,float z)=>Sample(water,x,z)>.05f && Sample(height,x,z)<Sample(waterHeight,x,z)-.03f;

        public void SmoothRiverBanks(float radius=2)
        {
            int size=resolution,n=land.Length,r=Mathf.Clamp(Mathf.CeilToInt(radius/cellSize),1,6);var original=(float[])water.Clone();var horizontal=new float[n];var filtered=new float[n];var levels=(float[])waterHeight.Clone();
            for(int z=0;z<size;z++)for(int x=0;x<size;x++){float sum=0,total=0;for(int d=-r;d<=r;d++){int nx=Mathf.Clamp(x+d,0,size-1);float w=Mathf.Exp(-2f*d*d/(r*r));sum+=Mathf.Clamp01(original[Index(nx,z)])*w;total+=w;}horizontal[Index(x,z)]=sum/total;}
            for(int z=0;z<size;z++)for(int x=0;x<size;x++){float sum=0,total=0;for(int d=-r;d<=r;d++){int nz=Mathf.Clamp(z+d,0,size-1);float w=Mathf.Exp(-2f*d*d/(r*r));sum+=horizontal[Index(x,nz)]*w;total+=w;}filtered[Index(x,z)]=sum/total;}
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
            {
                int i=Index(x,z);float coverage=filtered[i];if(land[i]<.5f||coverage<.008f){if(original[i]>.001f){height[i]=dryHeight[i];water[i]=0;waterHeight[i]=0;}continue;}
                float level=0,weight=0;for(int dz=-r;dz<=r;dz++)for(int dx=-r;dx<=r;dx++){int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=size||nz>=size)continue;int j=Index(nx,nz);float w=original[j]/(1+dx*dx+dz*dz);level+=levels[j]*w;weight+=w;}if(weight<.001f)continue;level/=weight;
                water[i]=coverage;waterHeight[i]=level;height[i]=Mathf.Lerp(dryHeight[i],level-1.2f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.86f,coverage)));if(height[i]<level-.03f)grass[i]=0;
            }
        }

        public RectInt Paint(WorldBrush brush,Vector3 centre,float radius,float amount,float targetHeight,float pondDepth,float pondLevel,bool subtract,string surfaceId=null)
        {
            int layer=0;
            if(brush==WorldBrush.Surface&&!string.IsNullOrEmpty(surfaceId)){layer=surfaceIds.IndexOf(surfaceId)+1;if(layer==0){surfaceIds.Add(surfaceId);layer=surfaceIds.Count;}}
            radius=Mathf.Clamp(radius,.4f,16);
            int x0=Mathf.Clamp(Mathf.FloorToInt((centre.x-radius+Width*.5f)/cellSize),0,resolution-1),z0=Mathf.Clamp(Mathf.FloorToInt((centre.z-radius+Width*.5f)/cellSize),0,resolution-1);
            int x1=Mathf.Clamp(Mathf.CeilToInt((centre.x+radius+Width*.5f)/cellSize),0,resolution-1),z1=Mathf.Clamp(Mathf.CeilToInt((centre.z+radius+Width*.5f)/cellSize),0,resolution-1);
            var original=brush==WorldBrush.Smooth?(float[])height.Clone():null;
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                int i=Index(x,z);var position=Position(x,z);
                float distance=Vector2.Distance(new Vector2(position.x,position.z),new Vector2(centre.x,centre.z));
                if(distance>radius+cellSize)continue;
                float t=Mathf.Clamp01(1-distance/radius),weight=t*t*(3-2*t);
                if(x==0 || z==0 || x==resolution-1 || z==resolution-1){land[i]=0;continue;}
                if(brush==WorldBrush.Ground)
                {
                    float coverage=Mathf.Clamp01(.5f+(radius-distance)/cellSize);
                    if(coverage>land[i] && land[i]<.5f)height[i]=dryHeight[i]=targetHeight;
                    land[i]=Mathf.Max(land[i],coverage);
                }
                else if(brush==WorldBrush.EraseGround) {land[i]=Mathf.Min(land[i],Mathf.Clamp01(.5f+(distance-radius)/cellSize));if(land[i]<.5f){grass[i]=0;water[i]=0;surfaceBlend[i]=0;surfaceIndex[i]=0;}}
                else if(land[i]>.5f)
                {
                    if(brush==WorldBrush.Raise || brush==WorldBrush.Lower){height[i]=Mathf.Clamp(height[i]+weight*amount*(brush==WorldBrush.Raise?1:-1),floor+.3f,80);if(water[i]<.001f)dryHeight[i]=height[i];}
                    if(brush==WorldBrush.Smooth)
                    {
                        float sum=0;int count=0;
                        for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++) {int n=Index(Mathf.Clamp(x+dx,0,resolution-1),Mathf.Clamp(z+dz,0,resolution-1));if(land[n]>.5f){sum+=original[n];count++;}}
                        if(count>0){height[i]=Mathf.Lerp(height[i],sum/count,weight*Mathf.Clamp01(amount));if(water[i]<.001f)dryHeight[i]=height[i];}
                    }
                    if(brush==WorldBrush.Grass){grass[i]=water[i]>.015f?0:Mathf.Clamp01(grass[i]+weight*amount*(subtract?-1:1));if(!subtract)surfaceBlend[i]*=1-Mathf.Clamp01(weight*amount);}
                    if(brush==WorldBrush.Surface && layer>0 && water[i]<.015f)
                    {
                        float paint=Mathf.Clamp01(weight*amount);
                        if(subtract)surfaceBlend[i]=Mathf.Max(0,surfaceBlend[i]-paint);
                        else{if(surfaceIndex[i]!=layer){surfaceBlend[i]=Mathf.Max(0,surfaceBlend[i]-paint);if(surfaceBlend[i]<.03f)surfaceIndex[i]=layer;}if(surfaceIndex[i]==layer)surfaceBlend[i]=Mathf.Clamp01(surfaceBlend[i]+paint);}
                    }
                    if(brush==WorldBrush.EraseSurface)surfaceBlend[i]=Mathf.Max(0,surfaceBlend[i]-weight*amount);
                    if(brush==WorldBrush.EraseGrass && distance<=radius)grass[i]=0;
                    if((brush==WorldBrush.FillPond || brush==WorldBrush.RestoreGround || (brush==WorldBrush.Pond && subtract)) && distance<=radius)
                    {
                        if(water[i]>.001f){height[i]=dryHeight[i];water[i]=0;waterHeight[i]=0;}
                        if(brush==WorldBrush.RestoreGround){grass[i]=0;surfaceBlend[i]=0;surfaceIndex[i]=0;}
                    }
                    if(brush==WorldBrush.Pond)
                    {
                        if(!subtract)
                        {
                            // A target basin profile produces a soft bank; repeated stamping never excavates forever.
                            if(water[i]<.001f)dryHeight[i]=height[i];
                            float basin=Mathf.Lerp(dryHeight[i],pondLevel-pondDepth,weight);
                            height[i]=Mathf.Min(height[i],basin);
                            water[i]=Mathf.Max(water[i],weight);waterHeight[i]=pondLevel;
                            if(height[i]<pondLevel-.03f)grass[i]=0;
                        }
                    }
                }
            }
            return new RectInt(x0,z0,x1-x0+1,z1-z0+1);
        }

        // Expand symmetrically without resampling or moving any existing world position.
        public WorldMapData Expanded(int requestedResolution)
        {
            int next=Mathf.Clamp(requestedResolution,resolution,MaxResolution);if((next-resolution)%2!=0)next--;
            if(next<=resolution)return Clone();
            var copy=Clone();copy.resolution=next;int length=next*next,offset=(next-resolution)/2;
            var sources=new[]{land,height,grass,water,waterHeight,dryHeight,surfaceIndex,surfaceBlend};var arrays=new float[sources.Length][];
            for(int a=0;a<sources.Length;a++){arrays[a]=new float[length];if(a==1||a==5)for(int i=0;i<length;i++)arrays[a][i]=Mathf.Max(0,floor+.3f);for(int z=0;z<resolution;z++)Array.Copy(sources[a],z*resolution,arrays[a],(z+offset)*next+offset,resolution);}
            copy.land=arrays[0];copy.height=arrays[1];copy.grass=arrays[2];copy.water=arrays[3];copy.waterHeight=arrays[4];copy.dryHeight=arrays[5];copy.surfaceIndex=arrays[6];copy.surfaceBlend=arrays[7];return copy;
        }

        public static WorldMapData NewWorkspace(string name="新地图",int width=256)
        {
            int resolution=width>=512?1025:513;var data=Empty(name).Expanded(resolution);data.cellSize=width/(float)(resolution-1);return data;
        }

        public static WorldMapData SampleMap()
        {
            var data=Empty("青岚试作");
            data.Paint(WorldBrush.Ground,new Vector3(-6,0,0),10,1,.7f,2,0,false);
            data.Paint(WorldBrush.Ground,new Vector3(5,0,3),11,1,.7f,2,0,false);
            data.Paint(WorldBrush.Ground,new Vector3(0,0,-7),8,1,.7f,2,0,false);
            data.Paint(WorldBrush.Grass,new Vector3(-9,0,1),7,1,.7f,2,0,false);
            data.Paint(WorldBrush.Grass,new Vector3(7,0,6),7,1,.7f,2,0,false);
            data.Paint(WorldBrush.Grass,new Vector3(5,0,-7),5,1,.7f,2,0,false);
            data.Paint(WorldBrush.Pond,new Vector3(-5,0,5),4.5f,1,.7f,1.8f,.35f,false);
            data.spawn=new Vector3(0,1.5f,-2);return data;
        }
    }

    public sealed class WorldMapHistory
    {
        readonly List<WorldMapData> undo=new List<WorldMapData>(),redo=new List<WorldMapData>();
        public int UndoCount=>undo.Count;
        public int RedoCount=>redo.Count;
        public void Push(WorldMapData data){undo.Add(data.Clone());redo.Clear();long bytes=0;foreach(var item in undo)bytes+=(long)item.land.Length*8*4;while(undo.Count>1&&(undo.Count>32||bytes>96L*1024*1024)){bytes-=(long)undo[0].land.Length*8*4;undo.RemoveAt(0);}}
        public WorldMapData Undo(WorldMapData current){if(undo.Count==0)return current;redo.Add(current.Clone());var data=undo[undo.Count-1];undo.RemoveAt(undo.Count-1);return data;}
        public WorldMapData Redo(WorldMapData current){if(redo.Count==0)return current;undo.Add(current.Clone());var data=redo[redo.Count-1];redo.RemoveAt(redo.Count-1);return data;}
        public void Clear(){undo.Clear();redo.Clear();}
    }

    public static class WorldMapStorage
    {
        public static string SessionPath=>Path.Combine(DirectoryPath,".last-session.json");
        public static void SaveSession(WorldMapData data)
        {
            data.Validate();Directory.CreateDirectory(DirectoryPath);var temporary=SessionPath+".writing";
            File.WriteAllText(temporary,JsonUtility.ToJson(data));
            if(File.Exists(SessionPath))File.Replace(temporary,SessionPath,SessionPath+".bak");else File.Move(temporary,SessionPath);
        }
        public static WorldMapData LastSession()
        {
            if(!File.Exists(SessionPath))return null;
            var data=JsonUtility.FromJson<WorldMapData>(File.ReadAllText(SessionPath));data.Validate();return data;
        }
        public static string DirectoryPath
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath,"../../maps/source"));
#else
                return Path.Combine(Application.persistentDataPath,"WorldMaps");
#endif
            }
        }
        public static string SafeName(string name)
        {
            name=(name??"").Trim();
            if(string.IsNullOrEmpty(name) || name.Length>64 || name=="." || name==".." || name.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || name.Contains("/") || name.Contains("\\"))throw new InvalidDataException("地图名须为 1～64 字，不能含路径或特殊文件名字符。");
            return name;
        }
        public static string PathFor(string name)=>Path.Combine(DirectoryPath,SafeName(name)+".worldmap.json");
        public static string Save(WorldMapData data)
        {
            data.Validate();var path=PathFor(data.name);Directory.CreateDirectory(DirectoryPath);
            var temporary=path+".writing";File.WriteAllText(temporary,JsonUtility.ToJson(data));
            if(File.Exists(path))File.Replace(temporary,path,path+".bak");else File.Move(temporary,path);
            return path;
        }
        public static WorldMapData Load(string name)
        {
            var data=JsonUtility.FromJson<WorldMapData>(File.ReadAllText(PathFor(name)));
            if(data==null)throw new InvalidDataException("地图文件为空。");data.Validate();return data;
        }
        public static string Trash(string name)
        {
            var path=PathFor(name);if(!File.Exists(path))throw new FileNotFoundException("地图文件已不存在。",path);
            var folder=Path.Combine(DirectoryPath,".deleted",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            var destination=Path.Combine(folder,Path.GetFileName(path));File.Move(path,destination);
            try{if(File.Exists(path+".bak"))File.Move(path+".bak",destination+".bak");}
            catch{File.Move(destination,path);throw;}
            return destination;
        }
        public static string RestoreLastDeleted()
        {
            var root=Path.Combine(DirectoryPath,".deleted");if(!Directory.Exists(root))throw new InvalidDataException("没有可恢复的地图。");
            var folders=Directory.GetDirectories(root);Array.Sort(folders,StringComparer.Ordinal);
            for(int i=folders.Length-1;i>=0;i--)foreach(var file in Directory.GetFiles(folders[i],"*.worldmap.json"))
            {
                var name=SafeName(Path.GetFileName(file).Replace(".worldmap.json",""));var target=PathFor(name);
                if(File.Exists(target))throw new InvalidDataException("同名地图已存在，无法覆盖恢复："+name);
                File.Move(file,target);try{if(File.Exists(file+".bak"))File.Move(file+".bak",target+".bak");}catch{File.Move(target,file);throw;}
                return name;
            }
            throw new InvalidDataException("没有可恢复的地图。");
        }
        public static string[] List()
        {
            if(!Directory.Exists(DirectoryPath))return Array.Empty<string>();
            var files=Directory.GetFiles(DirectoryPath,"*.worldmap.json");Array.Sort(files,StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<files.Length;i++)files[i]=Path.GetFileName(files[i]).Replace(".worldmap.json","");return files;
        }
    }
}
