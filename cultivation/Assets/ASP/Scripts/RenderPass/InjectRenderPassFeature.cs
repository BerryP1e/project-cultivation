using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace ASP
{
    public class InjectRenderPassFeature : ScriptableRendererFeature
    {
        [FormerlySerializedAs("mask")] public LayerMask m_mask;

        // [RenderingLayerMask]
        private int m_renderingLayerMask;
        public RenderPassEvent Event;
        public RenderQueueRange Range = RenderQueueRange.opaque;
        public string InjectPassLightModeTag = "MyCustomLightModeTag";
        private InjectCustomPass m_injectPass;

        public override void Create()
        {
            m_injectPass = new InjectCustomPass(name, InjectPassLightModeTag, Event, Range, (uint)m_renderingLayerMask,
                m_mask, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_injectPass);
        }

        public class InjectCustomPass : ScriptableRenderPass
        {
            private FilteringSettings m_filteringSettings;
            private RenderStateBlock m_renderStateBlock;
            private ShaderTagId m_shaderTagId;
            private string m_profilerTag;

            public InjectCustomPass(string profilerTag, string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, LayerMask layerMask,
                StencilState stencilState, int stencilReference)
            {
                m_profilerTag = profilerTag;
                renderPassEvent = evt;
                m_filteringSettings = new FilteringSettings(renderQueueRange);
                m_filteringSettings.layerMask = layerMask.value;
                //m_filteringSettings.renderingLayerMask = renderingLayerMask;
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                m_shaderTagId = new ShaderTagId(shaderTagId);
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("Inject Render Pass")))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = SortingCriteria.CommonOpaque;
                    var sortingSettings = new SortingSettings(renderingData.cameraData.camera);
                    sortingSettings.criteria = sortFlags;
                    var drawSettings = new DrawingSettings(m_shaderTagId, sortingSettings);
                    drawSettings.perObjectData = PerObjectData.None;

                    context.DrawRenderers(renderingData.cullResults, ref drawSettings,
                        ref m_filteringSettings, ref m_renderStateBlock);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
}