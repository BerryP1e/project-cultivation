using UnityEngine;
namespace Cultivation.WorldAuthoring
{
    [DefaultExecutionOrder(-9000)]
    public sealed class WorldMapLoader : MonoBehaviour
    {
        public WorldMapAsset map;
        WorldMapRenderer renderer3d;
        public WorldSceneBinding[] bindings=System.Array.Empty<WorldSceneBinding>();
        public bool homeMap;
        WorldFeatureRuntime features;
        public WorldMapData Data {get;private set;}
        public void Build()
        {
            if(!map || !map.palette)return;
            Data=JsonUtility.FromJson<WorldMapData>(map.sourceJson);Data.Validate();
            renderer3d=GetComponent<WorldMapRenderer>()??gameObject.AddComponent<WorldMapRenderer>();
            renderer3d.Configure(map.palette,Data);renderer3d.RebuildAll();
            features?.Dispose();features=new WorldFeatureRuntime();features.Build(Data,map.palette,transform,bindings);
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var camera in root.GetComponentsInChildren<Camera>(true))if(camera.CompareTag("MainCamera")){var contours=camera.GetComponent<WorldMapInk>()??camera.gameObject.AddComponent<WorldMapInk>();contours.world=renderer3d;var effect=camera.GetComponent<WorldMapCamera>()??camera.gameObject.AddComponent<WorldMapCamera>();effect.world=renderer3d;var ink=camera.GetComponent<WorldInkCamera>()??camera.gameObject.AddComponent<WorldInkCamera>();ink.world=renderer3d;WorldMapLook.Apply(map.palette,camera,gameObject.scene.name!="Demon-Suppressing Tower");}
        }
        public WorldMapRenderer Renderer=>renderer3d;
        public void ResetTerrainData(){Data=JsonUtility.FromJson<WorldMapData>(map.sourceJson);Data.Validate();}
        public void Rebuild(){renderer3d.Configure(map.palette,Data);renderer3d.RebuildAll();}
        void Awake(){Build();}
        void OnDestroy(){features?.Dispose();}
    }
}
