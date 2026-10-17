using System;
using System.Collections.Generic;
using System.Linq;
namespace Cultivation.WorldAuthoring
{
    public static class WorldMapConsistency
    {
        public static List<string> Check(WorldMapData data,WorldMapPalette palette,IEnumerable<string> externalBindings=null)
        {
            var issues=new List<string>();try{data.Validate();}catch(Exception error){issues.Add(error.Message);return issues;}
            var available=new HashSet<string>(data.features.Select(x=>x.id));if(externalBindings!=null)available.UnionWith(externalBindings);
            foreach(var item in data.objects){if(palette.Find(item.paletteId)<0)issues.Add("缺少外观素材："+item.instanceId);if(!string.IsNullOrEmpty(item.featureId)&&!data.features.Exists(x=>x.id==item.featureId&&x.objectId==item.instanceId))issues.Add("外观的功能挂载丢失："+item.instanceId);}
            foreach(var feature in data.features)
            {
                var template=palette.Feature(feature.templateId);if(!template){issues.Add("功能模板缺失："+feature.label);continue;}
                if(!template.prefab)issues.Add("功能预制体缺失："+feature.label);
                if(!string.IsNullOrEmpty(feature.objectId)&&!data.objects.Exists(x=>x.instanceId==feature.objectId))issues.Add("功能对应的外观已删除："+feature.label);
                foreach(var reference in template.references)if(!available.Contains(reference.targetKey))issues.Add("需要场景绑定："+feature.label+"."+reference.propertyPath+" → "+reference.targetKey);
            }
            if(!data.HasGround(data.spawn.x,data.spawn.z))issues.Add("出生点不在地面上");if(data.IsWet(data.spawn.x,data.spawn.z))issues.Add("出生点位于水中");return issues;
        }
    }
}
