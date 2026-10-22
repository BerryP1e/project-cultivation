using UnityEngine;
using UnityEngine.Rendering;
namespace Cultivation.WorldAuthoring
{
    [RequireComponent(typeof(Camera))]
    public sealed class WorldMapCamera : MonoBehaviour
    {
        public WorldMapRenderer world;
        Material mist;
        Camera camera3d;
        CommandBuffer commands;
        static readonly int Copy=Shader.PropertyToID("_WorldMistOpaqueCopy");
        void OnPreCull()
        {
            if(!camera3d)camera3d=GetComponent<Camera>();
            // 只在实际有MSAA样本的Forward相机启用alpha覆盖，其他相机退回原裁剪。
            bool coverage=!GraphicsSettings.currentRenderPipeline&&camera3d.allowMSAA&&camera3d.actualRenderingPath==RenderingPath.Forward&&
                (camera3d.targetTexture?camera3d.targetTexture.antiAliasing:QualitySettings.antiAliasing)>1;
            Shader.SetGlobalFloat("_WorldFoliageCoverage",coverage?1:0);
            commands?.Clear();
            if(GraphicsSettings.currentRenderPipeline||!world||world.Data==null||!world.Data.cloudSea||!world.ShoreField)return;
            if(!mist){var shader=world.Palette.builtinMistShader;if(!shader)shader=Shader.Find("Cultivation/WorldAuthoring/ShoreMistBuiltin");if(!shader)return;mist=new Material(shader);}
            if(!camera3d)camera3d=GetComponent<Camera>();
            if(commands==null){commands=new CommandBuffer{name="Map mist before transparent effects"};camera3d.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,commands);}
            var camera=camera3d;float width=world.Data.Width;var range=world.FogHeightRange;
            mist.SetVector("_FogPlane",world.FogPlaneSettings);
            mist.SetTexture("_ShoreField",world.ShoreField);mist.SetTexture("_NoiseVolume",world.Palette.fogVolumeNoise);mist.SetVector("_MapRect",new Vector4(-width*.5f,-width*.5f,1/width,1/width));mist.SetVector("_HeightRange",new Vector4(range.x,range.y,0,0));
            // Blit 的屏幕 UV 已归一化朝向，不再使用渲染到纹理的 Y 翻转矩阵。
            mist.SetMatrix("_MistInverseViewProjection",(GL.GetGPUProjectionMatrix(camera.projectionMatrix,false)*camera.worldToCameraMatrix).inverse);mist.SetVector("_MistCameraPosition",camera.transform.position);mist.SetVector("_MistSettings",new Vector4(world.Data.fogHeight,world.Data.fogSpeed,world.Data.fogDensity,Time.unscaledTime));mist.SetFloat("_MistMaxDistance",Mathf.Max(240,width*2));
            // 在透明粒子和折射 GrabPass 之前合成，不能用不透明深度把前景烟焰一起盖住。
            commands.GetTemporaryRT(Copy,camera.pixelWidth,camera.pixelHeight,0,FilterMode.Bilinear,camera.allowHDR?RenderTextureFormat.DefaultHDR:RenderTextureFormat.Default);
            commands.Blit(BuiltinRenderTextureType.CameraTarget,Copy);commands.Blit(Copy,BuiltinRenderTextureType.CameraTarget,mist);commands.ReleaseTemporaryRT(Copy);
        }
        void OnEnable(){GetComponent<Camera>().depthTextureMode|=DepthTextureMode.Depth;}
        void OnPostRender(){Shader.SetGlobalFloat("_WorldFoliageCoverage",0);}
        void OnDisable(){if(commands!=null){if(camera3d)camera3d.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,commands);commands.Release();commands=null;}if(mist)Destroy(mist);mist=null;}
    }
}
