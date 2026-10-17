using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cultivation.WorldAuthoring
{
    // Depth-clipped fog in unoccupied map space, with no cloud prefabs or sea mesh.
    public sealed class WorldMistFeature : ScriptableRendererFeature
    {
        public Shader shader;
        MistPass pass;
        public override void Create(){pass?.Dispose();if(shader)pass=new MistPass(shader);}
        static WorldMapRenderer Map(Camera camera)
        {
            var ink=camera.GetComponent<WorldInkCamera>();
            return ink?ink.world:camera.cameraType==CameraType.SceneView?Object.FindObjectOfType<WorldMapRenderer>():null;
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            var map=Map(data.cameraData.camera);
            if(pass==null||!map||map.Data==null||!map.Data.cloudSea||!map.ShoreField)return;
            pass.map=map;renderer.EnqueuePass(pass);
        }
        public override void SetupRenderPasses(ScriptableRenderer renderer,in RenderingData data)
        {if(pass!=null&&Map(data.cameraData.camera))pass.source=renderer.cameraColorTargetHandle;}
        protected override void Dispose(bool disposing){pass?.Dispose();}
        sealed class MistPass:ScriptableRenderPass
        {
            public RTHandle source;
            public WorldMapRenderer map;
            RTHandle copy;
            readonly Material material;
            public MistPass(Shader shader)
            {
                material=CoreUtils.CreateEngineMaterial(shader);
                renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData data)
            {
                var descriptor=data.cameraData.cameraTargetDescriptor;descriptor.depthBufferBits=0;descriptor.msaaSamples=1;
                RenderingUtils.ReAllocateIfNeeded(ref copy,descriptor,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"World mist composite");
            }
            public override void Execute(ScriptableRenderContext context,ref RenderingData data)
            {
                if(source==null||!map||!map.ShoreField)return;
                float width=map.Data.Width;
                material.SetTexture("_ShoreField",map.ShoreField);material.SetTexture("_NoiseVolume",map.Palette.fogVolumeNoise);
                material.SetVector("_MapRect",new Vector4(-width*.5f,-width*.5f,1/width,1/width));
                var range=map.FogHeightRange;material.SetVector("_HeightRange",new Vector4(range.x,range.y,0,0));
                material.SetVector("_FogPlane",map.FogPlaneSettings);
                material.SetMatrix("_MistInverseViewProjection",(data.cameraData.GetGPUProjectionMatrix()*data.cameraData.GetViewMatrix()).inverse);
                material.SetVector("_MistCameraPosition",data.cameraData.camera.transform.position);
                material.SetVector("_MistSettings",new Vector4(map.Data.fogHeight,map.Data.fogSpeed,map.Data.fogDensity,Time.unscaledTime));
                material.SetFloat("_MistMaxDistance",Mathf.Max(240,width*2));
                var cmd=CommandBufferPool.Get("Shore mist volume");
                try{Blitter.BlitCameraTexture(cmd,source,copy,material,0);Blitter.BlitCameraTexture(cmd,copy,source);context.ExecuteCommandBuffer(cmd);}
                finally{CommandBufferPool.Release(cmd);}
            }
            public void Dispose(){copy?.Release();CoreUtils.Destroy(material);}
        }
    }
}
