/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ASP
{
    #region UNITY2021

#if UNITY_2021
        [DisallowMultipleRendererFeature("ASP Depth-Offset Shadow")]
    public class ASPDepthOffsetShadowFeature : ScriptableRendererFeature
    {
        //un-comment below line to use rendering layer mask to filter out objects
        [SingleLayerMask]
        public int m_layer;
        [RenderingLayerMask]
        public int m_renderingLayerMask;
        public RenderQueueRange Range = RenderQueueRange.opaque;
        private RenderPassEvent Event = RenderPassEvent.BeforeRenderingOpaques;
        private string MaterialPassShaderTag = "DepthOffsetShadow";
        private ASPDepthOffsetShadowPass m_ASPDepthOffsetShadowPass;
        
        public int GetLayer()
        {
            return m_renderingLayerMask;
        }

        public void SetLayer(int value)
        {
            m_renderingLayerMask = value;
        }

        public override void Create()
        {
            m_ASPDepthOffsetShadowPass = new ASPDepthOffsetShadowPass(MaterialPassShaderTag, Event, Range,
                (uint)m_renderingLayerMask, 1 << m_layer, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_ASPDepthOffsetShadowPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(m_ASPDepthOffsetShadowPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_ASPDepthOffsetShadowPass.Dispose();
        }

        public class ASPDepthOffsetShadowPass : ScriptableRenderPass
        {
            private RenderTargetHandle m_depthTextureHandle;
            private FilteringSettings m_filteringSettings;
            private RenderStateBlock m_renderStateBlock;
            private ShaderTagId m_shaderTagId;

            public ASPDepthOffsetShadowPass(string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, int layerMask, StencilState stencilState,
                int stencilReference)
            {
                renderPassEvent = evt;
                m_filteringSettings = new FilteringSettings(renderQueueRange);
                m_filteringSettings.layerMask = layerMask;
                //un-comment below line to use rendering layer mask to filter out objects
                m_filteringSettings.renderingLayerMask = renderingLayerMask;
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                m_shaderTagId = new ShaderTagId(shaderTagId);
                m_depthTextureHandle.Init("_ASPDepthOffsetShadowTexture");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.Depth;
                desc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                cmd.GetTemporaryRT(m_depthTextureHandle.id, desc);
                
                ConfigureTarget(m_depthTextureHandle.Identifier());
                ConfigureClear(ClearFlag.Depth, Color.black);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(m_depthTextureHandle.id);
            }
            
            public void Dispose()
            {
               
            }
            
            /// <inheritdoc/>
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("ASP DepthOffsetShadow Pass")))
                {
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
                cmd.SetGlobalTexture("_ASPDepthOffsetShadowTexture", m_depthTextureHandle.Identifier());
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
#endif

    #endregion

    #region UNITY2022

#if UNITY_2022_1_OR_NEWER
    [DisallowMultipleRendererFeature("ASP Depth-Offset Shadow")]
    public class ASPDepthOffsetShadowFeature : ScriptableRendererFeature
    {
        [SingleLayerMask] public int m_layer;
        [RenderingLayerMask] public int m_renderingLayerMask;
        private RenderQueueRange Range = RenderQueueRange.all;
        private RenderPassEvent Event = RenderPassEvent.BeforeRenderingOpaques;
        private string MaterialPassShaderTag = "DepthOffsetShadow";
        private ASPDepthOffsetShadowPass m_ASPDepthOffsetShadowPass;

        public int GetLayer()
        {
            return m_renderingLayerMask;
        }

        public void SetLayer(int value)
        {
            m_renderingLayerMask = value;
        }

        public override void Create()
        {
            m_ASPDepthOffsetShadowPass = new ASPDepthOffsetShadowPass(MaterialPassShaderTag, Event, Range,
                (uint)m_renderingLayerMask, 1 << m_layer, StencilState.defaultValue, 0);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_ASPDepthOffsetShadowPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(m_ASPDepthOffsetShadowPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_ASPDepthOffsetShadowPass.Dispose();
        }

        public class ASPDepthOffsetShadowPass : ScriptableRenderPass
        {
            private RTHandle m_detphTarget;
            private FilteringSettings m_filteringSettings;
            private RenderStateBlock m_renderStateBlock;
            private ShaderTagId m_shaderTagId;

            public ASPDepthOffsetShadowPass(string shaderTagId, RenderPassEvent evt,
                RenderQueueRange renderQueueRange, uint renderingLayerMask, int layerMask, StencilState stencilState,
                int stencilReference)
            {
                renderPassEvent = evt;
                m_filteringSettings = new FilteringSettings(renderQueueRange);
                m_filteringSettings.layerMask = layerMask;
                //un-comment below line to use rendering layer mask to filter out objects
                m_filteringSettings.renderingLayerMask = renderingLayerMask;
                m_renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                m_shaderTagId = new ShaderTagId(shaderTagId);
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.Depth;
                desc.depthBufferBits = renderingData.cameraData.cameraTargetDescriptor.depthBufferBits;
                RenderingUtils.ReAllocateIfNeeded(ref m_detphTarget, desc, name: "_ASPDepthOffsetShadowTexture");

                ConfigureTarget(m_detphTarget);
                ConfigureClear(ClearFlag.Depth, Color.black);
            }

            public void Dispose()
            {
                m_detphTarget?.Release();
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler("ASP DepthOffsetShadow Pass")))
                {
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
                cmd.SetGlobalTexture("_ASPDepthOffsetShadowTexture", m_detphTarget);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#if UNITY_6000_0_OR_NEWER
            private class DepthOffsetShadowPassData
            {
                public TextureHandle DesinationDepth;
                public RendererListHandle RendererListHandle;
            }
            
            static readonly int s_depthOffsetShadowTexture = Shader.PropertyToID("_ASPDepthOffsetShadowTexture");

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder =
 renderGraph.AddRasterRenderPass<DepthOffsetShadowPassData>(passName, out var passData, 
                           new ProfilingSampler("ASP Depth Offset Shadow Pass RG")))
                {
                    // Access the relevant frame data from the Universal Render Pipeline
                    UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    UniversalLightData lightData = frameData.Get<UniversalLightData>();
                    
                    var sortFlags = SortingCriteria.CommonOpaque;
                    DrawingSettings drawSettings =
 RenderingUtils.CreateDrawingSettings(m_shaderTagId, universalRenderingData, cameraData, lightData, sortFlags);

                    var param =
 new RendererListParams(universalRenderingData.cullResults, drawSettings, m_filteringSettings);
                    passData.RendererListHandle = renderGraph.CreateRendererList(param);
                    
                    RenderTextureDescriptor desc = new RenderTextureDescriptor(
                        cameraData.cameraTargetDescriptor.width,
                        cameraData.cameraTargetDescriptor.height);
                    desc.colorFormat = RenderTextureFormat.Depth;
                    desc.depthBufferBits = cameraData.cameraTargetDescriptor.depthBufferBits;
                    TextureHandle destinationDepth =
 UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ASPDepthOffsetShadowTexture", false);
                    passData.DesinationDepth = destinationDepth;
                    
                    builder.UseRendererList(passData.RendererListHandle);
                    builder.SetRenderAttachmentDepth(passData.DesinationDepth, AccessFlags.Write);
                    builder.AllowPassCulling(true);
                    builder.SetRenderFunc((DepthOffsetShadowPassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(RTClearFlags.Depth, Color.clear, 1, 0);
                        context.cmd.DrawRendererList(data.RendererListHandle); 
                    });
                    builder.SetGlobalTextureAfterPass(passData.DesinationDepth, s_depthOffsetShadowTexture);
                }
            }
#endif
        }
    }
#endif

    #endregion
}