using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultivation.WorldAuthoring
{
    [Serializable]
    public sealed class WorldPaletteEntry
    {
        public string id,label,category;
        public GameObject prefab;
        public Texture2D thumbnail;
        public Bounds bounds;
        public float defaultScale=1;
        public float legacyScale=1;
        public string sourcePack;
        public string sourcePath;
        public bool lockScale;
        public bool preservePivot;
        public bool editorOnly;
        public bool libraryHidden;
        public float bridgeDeckHeight;
        public bool IsBridge=>(sourcePath+" "+label).IndexOf("bridge",System.StringComparison.OrdinalIgnoreCase)>=0||(sourcePath+" "+label).IndexOf("qiao",System.StringComparison.OrdinalIgnoreCase)>=0||(label??"").Contains("桥");
        public WorldModuleProfile module;
        public Vector2 FootprintOffset=>module||preservePivot?new Vector2(bounds.center.x,bounds.center.z):Vector2.zero;
        public float boundaryYaw,boundarySpan;
        public Vector2 footprint;
        public Material[] materials;
    }

    [Serializable]
    public sealed class WorldGroundLayer
    {
        public string id,label;
        public Texture2D texture;
    }

    public sealed class WorldMapPalette : ScriptableObject
    {
        public WorldPaletteEntry[] entries=Array.Empty<WorldPaletteEntry>();
        public int[] boundaryRocks=Array.Empty<int>();
        public Mesh grassMesh;
        public Material groundMaterial,cliffMaterial,grassURP,grassBuiltin,waterURP,waterBuiltin,cloudMaterial;
        public Mesh cloudMesh;
        public RenderPipelineAsset editorPipeline;
        public Material skybox;
        public VolumeProfile grading;
        public Texture2D fogNoise;
        public Texture3D fogVolumeNoise;
        public WorldGroundLayer[] groundLayers=Array.Empty<WorldGroundLayer>();
        public Texture2DArray groundTextureArray;
        public GameObject referencePlayer;
        public AnimationClip referenceIdle;
        public float playerHeight=2.6f;
        public float gamePitch=40,gameYaw=45,gameDistance=16,gameFov=40,gameLookHeight=1.2f;
        public WorldFeatureTemplate[] featureTemplates=Array.Empty<WorldFeatureTemplate>();
        public Shader builtinMistShader;
        public Shader inkMaskShader,inkCompositeShader;
        public WorldFeatureTemplate Feature(string id)=>Array.Find(featureTemplates,x=>x&&x.id==id);
        WorldPaletteEntry[] indexedEntries;
        readonly System.Collections.Generic.Dictionary<string,int> entryIndex=new System.Collections.Generic.Dictionary<string,int>(StringComparer.Ordinal);
        public int Find(string id){if(!ReferenceEquals(indexedEntries,entries)){entryIndex.Clear();for(int i=0;i<entries.Length;i++)if(!string.IsNullOrEmpty(entries[i].id))entryIndex[entries[i].id]=i;indexedEntries=entries;}return id!=null&&entryIndex.TryGetValue(id,out int index)?index:-1;}
        public int FindGround(string id){for(int i=0;i<groundLayers.Length;i++)if(groundLayers[i].id==id)return i;return -1;}
    }

}
