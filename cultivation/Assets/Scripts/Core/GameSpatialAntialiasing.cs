using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;

/// <summary>直接调用官方SMAA三趟着色器；不创建时域历史、投影抖动或随机抖色。</summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class GameSpatialAntialiasing : MonoBehaviour
{
    public bool 启用=true;
    public SubpixelMorphologicalAntialiasing.Quality 质量=SubpixelMorphologicalAntialiasing.Quality.High;
    PropertySheetFactory 材质工厂;
    PostProcessResources 资源;
    CommandBuffer 命令;
    static readonly int 边缘=Shader.PropertyToID("_CultivationSmaaEdges"), 权重=Shader.PropertyToID("_CultivationSmaaWeights");

    void OnEnable()
    {
        if(GraphicsSettings.currentRenderPipeline)return;
        资源=Resources.Load<PostProcessResources>("Rendering/SpatialAAResources");
        if(!资源||!资源.shaders.subpixelMorphologicalAntialiasing||!资源.shaders.subpixelMorphologicalAntialiasing.isSupported)return;
        材质工厂=new PropertySheetFactory();命令=new CommandBuffer{name="Official SMAA (no temporal jitter)"};
    }

    void OnRenderImage(RenderTexture source,RenderTexture destination)
    {
        if(!启用||命令==null||GraphicsSettings.currentRenderPipeline||RuntimeUtilities.isSinglePassStereoEnabled)
        {Graphics.Blit(source,destination);return;}
        var sheet=材质工厂.Get(资源.shaders.subpixelMorphologicalAntialiasing);
        sheet.properties.SetTexture("_AreaTex",资源.smaaLuts.area);
        sheet.properties.SetTexture("_SearchTex",资源.smaaLuts.search);
        var output=RenderTexture.GetTemporary(source.descriptor);
        try
        {
            命令.Clear();命令.BeginSample("Cultivation SMAA");
            命令.GetTemporaryRT(边缘,source.width,source.height,0,FilterMode.Bilinear,source.format,RenderTextureReadWrite.Linear);
            命令.GetTemporaryRT(权重,source.width,source.height,0,FilterMode.Bilinear,source.format,RenderTextureReadWrite.Linear);
            命令.BlitFullscreenTriangle(source,边缘,sheet,(int)质量,true);
            命令.BlitFullscreenTriangle(边缘,权重,sheet,3+(int)质量);
            命令.SetGlobalTexture("_BlendTex",权重);
            命令.BlitFullscreenTriangle(source,output,sheet,6);
            命令.ReleaseTemporaryRT(边缘);命令.ReleaseTemporaryRT(权重);命令.EndSample("Cultivation SMAA");
            Graphics.ExecuteCommandBuffer(命令);Graphics.Blit(output,destination);
        }
        finally{RenderTexture.ReleaseTemporary(output);}
    }

    void OnDisable(){命令?.Release();命令=null;材质工厂?.Release();材质工厂=null;资源=null;}
}
