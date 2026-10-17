using System;
using UnityEngine;

namespace Cultivation.WorldAuthoring
{
    public enum WorldFeatureRole { Generic, MeritExchange, TechniqueExchange, Commission }
    [Serializable]
    public sealed class WorldFeatureData
    {
        public string id,templateId,label,objectId;
        public Vector3 position,rotation,scale=Vector3.one;
        public bool enabled=true;
        public WorldFeatureData Clone()=>(WorldFeatureData)MemberwiseClone();
    }

    [Serializable]
    public sealed class WorldFeatureReference
    {
        public string ownerPath,ownerType,propertyPath,targetKey,targetPath,targetType;
        public int ownerIndex,targetIndex;
    }

    public sealed class WorldFeatureTemplate : ScriptableObject
    {
        public string id,label,kind,sourceScene,sourcePath;
        public GameObject prefab;
        public WorldFeatureRole role;
        public WorldFeatureReference[] references=Array.Empty<WorldFeatureReference>();
    }
}
