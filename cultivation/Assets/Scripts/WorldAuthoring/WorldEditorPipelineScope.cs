using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cultivation.WorldAuthoring
{
    [DefaultExecutionOrder(-20000)]
    public sealed class WorldEditorPipelineScope : MonoBehaviour
    {
        public WorldMapPalette palette;
        [Tooltip("仅用于单独验证 URP 兼容性；日常工坊与正式地图共用 Built-in。")]
        public bool experimentalUrp;
        RenderPipelineAsset previous;
        bool linear,temperature,active,batcher;
        int aa;
#if UNITY_EDITOR
        string graphicsPath,qualityPath;
        byte[] graphicsBytes,qualityBytes;
#endif
        void Awake()
        {
            if(!experimentalUrp || !palette || !palette.editorPipeline)return;
            previous=QualitySettings.renderPipeline;linear=GraphicsSettings.lightsUseLinearIntensity;temperature=GraphicsSettings.lightsUseColorTemperature;aa=QualitySettings.antiAliasing;batcher=GraphicsSettings.useScriptableRenderPipelineBatching;
#if UNITY_EDITOR
            graphicsPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../ProjectSettings/GraphicsSettings.asset"));qualityPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../ProjectSettings/QualitySettings.asset"));
            graphicsBytes=File.ReadAllBytes(graphicsPath);qualityBytes=File.ReadAllBytes(qualityPath);
#endif
            active=true;QualitySettings.renderPipeline=palette.editorPipeline;
        }
        void OnDestroy()
        {
            if(!active)return;active=false;QualitySettings.renderPipeline=previous;
            GraphicsSettings.lightsUseLinearIntensity=linear;GraphicsSettings.lightsUseColorTemperature=temperature;QualitySettings.antiAliasing=aa;GraphicsSettings.useScriptableRenderPipelineBatching=batcher;
#if UNITY_EDITOR
            File.WriteAllBytes(graphicsPath,graphicsBytes);File.WriteAllBytes(qualityPath,qualityBytes);
            // After Play exits, dispose the preview pipeline before the engine serializes settings again.
            var savedGraphics=graphicsBytes;var savedQuality=qualityBytes;var gp=graphicsPath;var qp=qualityPath;
            var savedLinear=linear;var savedTemperature=temperature;var savedAA=aa;var savedBatcher=batcher;
            UnityEditor.EditorApplication.delayCall+=()=>
            {
                if(UnityEditor.EditorApplication.isPlaying)return;
                GraphicsSettings.lightsUseLinearIntensity=savedLinear;GraphicsSettings.lightsUseColorTemperature=savedTemperature;QualitySettings.antiAliasing=savedAA;GraphicsSettings.useScriptableRenderPipelineBatching=savedBatcher;
                File.WriteAllBytes(gp,savedGraphics);File.WriteAllBytes(qp,savedQuality);
            };
#endif
        }
    }
}
