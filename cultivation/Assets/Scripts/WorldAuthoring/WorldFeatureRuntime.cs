using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Cultivation.WorldAuthoring
{
    [Serializable]
    public sealed class WorldSceneBinding
    {
        public string key;
        public GameObject target;
        public bool managedFeature;
    }

    // Existing scenes retain their serialized component references. New maps instantiate the same
    // functional templates inactive, bind references, then activate all modules together.
    public sealed class WorldFeatureRuntime : IDisposable
    {
        readonly List<GameObject> owned=new List<GameObject>();
        public static void Synchronize(WorldMapData data)
        {
            foreach(var feature in data.features)
            {
                if(string.IsNullOrEmpty(feature.objectId))continue;
                var item=data.objects.Find(x=>x.instanceId==feature.objectId);if(item==null)continue;
                feature.position=item.position;feature.rotation=item.Orientation.eulerAngles;feature.scale=item.AxisScale;
            }
        }
        public void Build(WorldMapData data,WorldMapPalette palette,Transform parent,WorldSceneBinding[] bindings)
        {
            Synchronize(data);var targets=new Dictionary<string,GameObject>(StringComparer.Ordinal);
            foreach(var binding in bindings??Array.Empty<WorldSceneBinding>())if(binding!=null&&binding.target)targets[binding.key]=binding.target;
            foreach(var binding in bindings??Array.Empty<WorldSceneBinding>())if(binding!=null&&binding.target&&binding.managedFeature&&!data.features.Exists(x=>x.id==binding.key))binding.target.SetActive(false);
            var pending=new List<Tuple<GameObject,WorldFeatureData,WorldFeatureTemplate>>();
            var blocked=new HashSet<GameObject>();
            foreach(var feature in data.features)
            {
                var template=palette.Feature(feature.templateId);if(!template){Debug.LogError("地图功能模板缺失："+feature.label);continue;}
                bool created=!targets.TryGetValue(feature.id,out var root)||!root;
                if(created){if(!template.prefab){Debug.LogError("地图功能预制体缺失："+template.label);continue;}root=UnityEngine.Object.Instantiate(template.prefab,parent,false);root.SetActive(false);root.name=feature.label;owned.Add(root);}
                root.transform.SetPositionAndRotation(feature.position,Quaternion.Euler(feature.rotation));root.transform.localScale=feature.scale;
                ApplyRole(root,template.role);
                targets[feature.id]=root;if(created)pending.Add(Tuple.Create(root,feature,template));else root.SetActive(feature.enabled);
            }
            foreach(var item in pending)foreach(var reference in item.Item3.references)
            {
                if(!targets.TryGetValue(reference.targetKey,out var target)||!target){blocked.Add(item.Item1);Debug.LogError("功能引用缺失："+item.Item2.label+" → "+reference.targetKey);continue;}
                var source=Resolve(item.Item1,reference.ownerPath,reference.ownerType,reference.ownerIndex);
                var destination=Resolve(target,reference.targetPath,reference.targetType,reference.targetIndex);
                if(source==null||destination==null||!SetProperty(source,reference.propertyPath,destination)){blocked.Add(item.Item1);Debug.LogError("功能引用无法绑定："+item.Item2.label+"."+reference.propertyPath);}
            }
            foreach(var item in pending)item.Item1.SetActive(item.Item2.enabled&&!blocked.Contains(item.Item1));
        }
        static UnityEngine.Object Resolve(GameObject root,string path,string type,int index)
        {
            var node=string.IsNullOrEmpty(path)?root.transform:root.transform.Find(path);if(!node)return null;
            if(type==typeof(GameObject).FullName)return node.gameObject;
            var components=node.GetComponents<Component>();int count=0;foreach(var component in components)if(component&&component.GetType().FullName==type){if(count++==index)return component;}return null;
        }
        static void ApplyRole(GameObject root,WorldFeatureRole role)
        {
            if(role==WorldFeatureRole.Generic)return;var station=root.GetComponent<StationInteractable>()??root.AddComponent<StationInteractable>();
            if(role==WorldFeatureRole.Commission){if(!root.GetComponent<执事阁界面>())root.AddComponent<执事阁界面>();station.显示名="执事阁";return;}
            var exchange=root.GetComponent<功德堂兑换>()??root.AddComponent<功德堂兑换>();exchange.传法阁=role==WorldFeatureRole.TechniqueExchange;station.显示名=exchange.传法阁?"传法阁":"功德堂";
        }
        static FieldInfo Field(Type type,string name)
        {
            for(var current=type;current!=null;current=current.BaseType){var field=current.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);if(field!=null)return field;}return null;
        }
        static bool SetProperty(object owner,string path,UnityEngine.Object value)
        {
            var parts=path.Replace(".Array.data[","[").Split('.');
            bool Set(object current,int depth)
            {
                if(current==null)return false;string part=parts[depth];int bracket=part.IndexOf('[');string name=bracket<0?part:part.Substring(0,bracket);var field=Field(current.GetType(),name);if(field==null)return false;
                if(bracket>=0){var list=field.GetValue(current) as IList;int end=part.IndexOf(']');if(list==null||!int.TryParse(part.Substring(bracket+1,end-bracket-1),out int index)||index<0||index>=list.Count)return false;if(depth==parts.Length-1){list[index]=value;return true;}var child=list[index];if(!Set(child,depth+1))return false;list[index]=child;return true;}
                if(depth==parts.Length-1){field.SetValue(current,value);return true;}var nested=field.GetValue(current);if(!Set(nested,depth+1))return false;field.SetValue(current,nested);return true;
            }
            try{return Set(owner,0);}catch(Exception error){Debug.LogError("绑定 "+path+"："+error.Message);return false;}
        }
        public void Dispose(){foreach(var root in owned)if(root){root.SetActive(false);if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);}owned.Clear();}
    }
}
