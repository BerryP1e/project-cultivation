/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ASP
{
    [DisallowMultipleRendererFeature("ASP Mesh Outline")]
    public class ASPMeshOutlineRendererFeature : ScriptableRendererFeature
    {
        [SingleLayerMask] public int m_layer;
        [RenderingLayerMask] public int m_renderingLayerMask;
        public RenderPassEvent InjectPoint = RenderPassEvent.AfterRenderingSkybox;
        private RenderQueueRange Range = RenderQueueRange.opaque;

        [FormerlySerializedAs("InjectPassLightModeTag")]
        private string lightModeTag = "ASPOutlineObject";

        private MeshOutlinePass m_meshOutlinePass;

        public override void Create()
        {
            m_meshOutlinePass = new MeshOutlinePass(name, lightModeTag, InjectPoint, Range, (uint)m_renderingLayerMask,
                1 << m_layer, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_meshOutlinePass);
        }

        public class MeshOutlinePass : ScriptableRenderPass
        {
            private FilteringSettings m_filteringSettings;
            private RenderStateBlock m_renderStateBlock;
            private ShaderTagId m_shaderTagId;
            private string m_profilerTag;

            public MeshOutlinePass(string profilerTag, string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, int layerMask, StencilState stencilState,
                int stencilReference)
            {
                m_profilerTag = profilerTag;
                renderPassEvent = evt;
                m_filteringSettings = new FilteringSettings(renderQueueRange);
                m_filteringSettings.layerMask = layerMask;
                m_filteringSettings.renderingLayerMask = renderingLayerMask;
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                m_shaderTagId = new ShaderTagId(shaderTagId);
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("Mesh Outline Pass")))
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
#if UNITY_6000_0_OR_NEWER
            private class MeshOutlinePassData
            {
                public TextureHandle Destination;
                public RendererListHandle RendererListHandle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder = renderGraph.AddRasterRenderPass<MeshOutlinePassData>(passName, out var passData, 
                           new ProfilingSampler("ASP MeshOutline Pass RG")))
                {
                    // Access the relevant frame data from the Universal Render Pipeline
                    UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    
                    var sortFlags = SortingCriteria.CommonOpaque;
                    DrawingSettings drawSettings =
 RenderingUtils.CreateDrawingSettings(m_shaderTagId, universalRenderingData, cameraData, lightData, sortFlags);

                    var param =
 new RendererListParams(universalRenderingData.cullResults, drawSettings, m_filteringSettings);
                    passData.RendererListHandle = renderGraph.CreateRendererList(param);
                    
                    passData.Destination = resourceData.activeColorTexture;
                    
                    builder.UseRendererList(passData.RendererListHandle);
                    builder.SetRenderAttachment(passData.Destination, 0);
                    builder.AllowPassCulling(true);
                    builder.SetRenderFunc((MeshOutlinePassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.RendererListHandle); 
                    });
                }
            }
#endif
        }
    }
}