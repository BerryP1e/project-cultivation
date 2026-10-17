/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
 */

using System;
using ASPUtil;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ASP
{
    #region UNITY2021

#if UNITY_2021
    [DisallowMultipleRendererFeature("ASP Screen Space Outline")]
    public class ASPScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;
        private Material material;
        public bool UseHalfScale;
        private ASPScreenSpaceOutlinePass m_asplLightShaftPass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_asplLightShaftPass = new ASPScreenSpaceOutlinePass(name, UseHalfScale);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/Outline");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }
                return;
            }

            m_asplLightShaftPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_asplLightShaftPass.ConfigureInput(requirements);
            m_asplLightShaftPass.SetupMembers(material);

            renderer.EnqueuePass(m_asplLightShaftPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_asplLightShaftPass.Dispose();
        }

        public class ASPScreenSpaceOutlinePass : ScriptableRenderPass
        {
            private Material m_outlineEffectMaterial;
            private RenderTexture m_copiedColor;
            private RenderTexture m_outlineInfoRT;
            
            private ASP.ASPSreenSpaceOutline m_screenSpaceOutlineSetting;
            private bool m_UseHalfScale;
            public ASPScreenSpaceOutlinePass(string passName, bool useHalfScale)
            {
                profilingSampler = new ProfilingSampler(passName);
                m_UseHalfScale = useHalfScale;
            }

            public void SetupMembers(Material material)
            {
                m_outlineEffectMaterial = material;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                
                m_screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                
                if (m_copiedColor != null)
                {
                    RenderTexture.ReleaseTemporary(m_copiedColor);
                    m_copiedColor = null;
                }
                m_copiedColor = RenderTexture.GetTemporary(desc);
                
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                
                if (m_outlineInfoRT != null)
                {
                    RenderTexture.ReleaseTemporary(m_outlineInfoRT);
                    m_outlineInfoRT = null;
                }
                m_outlineInfoRT = RenderTexture.GetTemporary(desc);

                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public void Dispose()
            {
            }
            
            private void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }
            
            private void SetKeyword(Material material, string keyword, bool state)
            {
                //UnityEngine.Debug.Log(keyword + " = "+state);
                if (state)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_outlineEffectMaterial == null)
                    return;
                if(!m_screenSpaceOutlineSetting.IsActive())
                    return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP Screen Space Outline Pass")))
                {
                    //fetch current camera Color to copiedColor RT
                    CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    Blit(cmd, cameraData.renderer.cameraColorTarget, m_copiedColor);
                    
                    //fetch color mask
                    CoreUtils.SetRenderTarget(cmd, m_outlineInfoRT);
                    
                    SetKeyword(m_outlineEffectMaterial, "_IS_DEBUG_MODE", m_screenSpaceOutlineSetting.EnableDebugMode.value);
                    m_outlineEffectMaterial.SetColor("_DebugBackgroundColor", m_screenSpaceOutlineSetting.DebugBackground.value);
                    m_outlineEffectMaterial.SetFloat("_DebugEdgeType", (float)((int)m_screenSpaceOutlineSetting.ScreenSpaceOutlineDebugMode.value));
                    
                    m_outlineEffectMaterial.SetFloat("_OutlineWidth", m_screenSpaceOutlineSetting.OutlineWidth.value);
                    m_outlineEffectMaterial.SetFloat("_OuterLineToggle", m_screenSpaceOutlineSetting.EnableOuterline.value ? 1f : 0f);
                    m_outlineEffectMaterial.SetFloat("_MaterialThreshold", m_screenSpaceOutlineSetting.MaterialEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_MaterialBias", (float)m_screenSpaceOutlineSetting.MaterialEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_MaterialWeight", m_screenSpaceOutlineSetting.MaterialEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableMaterialEdge.value));
                    
                    m_outlineEffectMaterial.SetFloat("_LumaThreshold", m_screenSpaceOutlineSetting.AlbedoEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_LumaBias", (float)m_screenSpaceOutlineSetting.AlbedoEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_LumaWeight", m_screenSpaceOutlineSetting.AlbedoEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableAlbedoEdge.value));

                    m_outlineEffectMaterial.SetFloat("_DepthThreshold", m_screenSpaceOutlineSetting.DepthEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_DepthBias", (float)m_screenSpaceOutlineSetting.DepthEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_DepthWeight", m_screenSpaceOutlineSetting.DepthEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableDepthEdge.value));

                    m_outlineEffectMaterial.SetFloat("_NormalsThreshold", m_screenSpaceOutlineSetting.NormalsEdgeThreshold.value);
                    m_outlineEffectMaterial.SetFloat("_NormalsBias", (float)m_screenSpaceOutlineSetting.NormalsEdgeBias.value);
                    m_outlineEffectMaterial.SetFloat("_NormalWeight", m_screenSpaceOutlineSetting.NormalsEdgeWeight.value * Convert.ToInt32(m_screenSpaceOutlineSetting.EnableNormalsEdge.value));
                    
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "MATERIAL_EDGE", m_screenSpaceOutlineSetting.EnableMaterialEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "LUMA_EDGE", m_screenSpaceOutlineSetting.EnableAlbedoEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "NORMAL_EDGE", m_screenSpaceOutlineSetting.EnableNormalsEdge.value);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "DEPTH_EDGE", m_screenSpaceOutlineSetting.EnableDepthEdge.value);

                    m_outlineEffectMaterial.SetFloat("_EnableColorDistanceFade", m_screenSpaceOutlineSetting.FadingColorByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetFloat("_EnableWeightDistanceFade", m_screenSpaceOutlineSetting.FadingWeghtByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetVector("_ColorWeightFadeDistanceStartEnd", m_screenSpaceOutlineSetting.ColorWeightFadingStartEndDistance.value);
                    
                    m_outlineEffectMaterial.SetFloat("_EnableWidthDistanceFade", m_screenSpaceOutlineSetting.FadingWidthByDistance.value ? 1.0f : 0);
                    m_outlineEffectMaterial.SetVector("_WidthFadeDistanceStartEnd", m_screenSpaceOutlineSetting.WidthFadingStartEndDistance.value);

                    m_outlineEffectMaterial.SetVector("_BlitScaleBias", new Vector4(1,1,0,0));
                    DrawTriangle(cmd, m_outlineEffectMaterial, 0);
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);

                    m_outlineEffectMaterial.SetTexture("_ASPOutlineTexture", m_outlineInfoRT);
                    m_outlineEffectMaterial.SetTexture("_BaseMap", m_copiedColor);
                    m_outlineEffectMaterial.SetColor("_OutlineColor", m_screenSpaceOutlineSetting.OutlineColor.value);
                    DrawTriangle(cmd, m_outlineEffectMaterial, 1);
                }
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
    [DisallowMultipleRendererFeature("ASP Screen Space Outline")]
    public class ASPScreenSpaceOutlineFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;
        private Material material;
        public bool UseHalfScale;
        private ASPScreenSpaceOutlinePass m_asplLightShaftPass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_asplLightShaftPass = new ASPScreenSpaceOutlinePass(name, UseHalfScale);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/ASP/PostProcess/Outline");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }

                return;
            }

            m_asplLightShaftPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_asplLightShaftPass.ConfigureInput(requirements);
            m_asplLightShaftPass.SetupMembers(material);

            renderer.EnqueuePass(m_asplLightShaftPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_asplLightShaftPass.Dispose();
        }

        public class ASPScreenSpaceOutlinePass : ScriptableRenderPass
        {
            private Material m_outlineEffectMaterial;
            private RTHandle m_outlineInfoRT;
            private RTHandle m_copiedColor;
            private ASP.ASPSreenSpaceOutline m_screenSpaceOutlineSetting;
            private bool m_UseHalfScale;

            public ASPScreenSpaceOutlinePass(string passName, bool useHalfScale)
            {
                profilingSampler = new ProfilingSampler(passName);
                m_UseHalfScale = useHalfScale;
            }

            public void SetupMembers(Material material)
            {
                m_outlineEffectMaterial = material;
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                m_screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_copiedColor, desc, name: "_CameraColorTexture");
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_outlineInfoRT, desc, name: "_ASPOutlineTexture");
                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
                m_outlineInfoRT?.Release();
            }

            private void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }

            private void SetKeyword(Material material, string keyword, bool state)
            {
                //UnityEngine.Debug.Log(keyword + " = "+state);
                if (state)
                    material.EnableKeyword(keyword);
                else
                    material.DisableKeyword(keyword);
            }

            private void SetupOutlineFirstPassParam(Material mat, ASP.ASPSreenSpaceOutline volumeSetting)
            {
                SetKeyword(mat, "_IS_DEBUG_MODE", volumeSetting.EnableDebugMode.value);
                mat.SetColor("_DebugBackgroundColor", volumeSetting.DebugBackground.value);
                mat.SetFloat("_DebugEdgeType", (float)((int)volumeSetting.ScreenSpaceOutlineDebugMode.value));

                mat.SetFloat("_OutlineWidth", volumeSetting.OutlineWidth.value);
                mat.SetFloat("_OuterLineToggle", volumeSetting.EnableOuterline.value ? 1f : 0f);
                mat.SetFloat("_MaterialThreshold", volumeSetting.MaterialEdgeThreshold.value);
                mat.SetFloat("_MaterialBias", (float)volumeSetting.MaterialEdgeBias.value);
                mat.SetFloat("_MaterialWeight",
                    volumeSetting.MaterialEdgeWeight.value * Convert.ToInt32(volumeSetting.EnableMaterialEdge.value));

                mat.SetFloat("_LumaThreshold", volumeSetting.AlbedoEdgeThreshold.value);
                mat.SetFloat("_LumaBias", (float)volumeSetting.AlbedoEdgeBias.value);
                mat.SetFloat("_LumaWeight",
                    volumeSetting.AlbedoEdgeWeight.value * Convert.ToInt32(volumeSetting.EnableAlbedoEdge.value));

                mat.SetFloat("_DepthThreshold", volumeSetting.DepthEdgeThreshold.value);
                mat.SetFloat("_DepthBias", (float)volumeSetting.DepthEdgeBias.value);
                mat.SetFloat("_DepthWeight",
                    volumeSetting.DepthEdgeWeight.value * Convert.ToInt32(volumeSetting.EnableDepthEdge.value));

                mat.SetFloat("_NormalsThreshold", volumeSetting.NormalsEdgeThreshold.value);
                mat.SetFloat("_NormalsBias", (float)volumeSetting.NormalsEdgeBias.value);
                mat.SetFloat("_NormalWeight",
                    volumeSetting.NormalsEdgeWeight.value * Convert.ToInt32(volumeSetting.EnableNormalsEdge.value));

                CoreUtils.SetKeyword(mat, "MATERIAL_EDGE", volumeSetting.EnableMaterialEdge.value);
                CoreUtils.SetKeyword(mat, "LUMA_EDGE", volumeSetting.EnableAlbedoEdge.value);
                CoreUtils.SetKeyword(mat, "NORMAL_EDGE", volumeSetting.EnableNormalsEdge.value);
                CoreUtils.SetKeyword(mat, "DEPTH_EDGE", volumeSetting.EnableDepthEdge.value);

                mat.SetFloat("_EnableColorDistanceFade", volumeSetting.FadingColorByDistance.value ? 1.0f : 0);
                mat.SetFloat("_EnableWeightDistanceFade", volumeSetting.FadingWeghtByDistance.value ? 1.0f : 0);
                mat.SetVector("_ColorWeightFadeDistanceStartEnd",
                    volumeSetting.ColorWeightFadingStartEndDistance.value);

                mat.SetFloat("_EnableWidthDistanceFade", volumeSetting.FadingWidthByDistance.value ? 1.0f : 0);
                mat.SetVector("_WidthFadeDistanceStartEnd", volumeSetting.WidthFadingStartEndDistance.value);
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!m_screenSpaceOutlineSetting.IsActive())
                    return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP Screen Space Outline Pass")))
                {
                    //fetch current camera Color to copiedColor RT
                    CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, m_copiedColor);

                    //fetch color mask
                    CoreUtils.SetRenderTarget(cmd, m_outlineInfoRT);
                    Vector2 viewportScale = m_copiedColor.useScaling
                        ? new Vector2(m_copiedColor.rtHandleProperties.rtHandleScale.x,
                            m_copiedColor.rtHandleProperties.rtHandleScale.y)
                        : Vector2.one;
                    m_outlineEffectMaterial.SetVector("_BlitScaleBias", viewportScale);
                    // m_outlineEffectMaterial.SetTexture("_BaseMap", m_copiedColor);
                    SetupOutlineFirstPassParam(m_outlineEffectMaterial, m_screenSpaceOutlineSetting);
                    DrawTriangle(cmd, m_outlineEffectMaterial, 0);

                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    CoreUtils.SetKeyword(m_outlineEffectMaterial, "_APPLY_FXAA",
                        m_screenSpaceOutlineSetting.ApplyFXAA.value);
                    viewportScale = m_outlineInfoRT.useScaling
                        ? new Vector2(m_outlineInfoRT.rtHandleProperties.rtHandleScale.x,
                            m_outlineInfoRT.rtHandleProperties.rtHandleScale.y)
                        : Vector2.one;
                    m_outlineEffectMaterial.SetVector("_BlitScaleBias", viewportScale);
                    m_outlineEffectMaterial.SetTexture("_ASPOutlineTexture", m_outlineInfoRT);
                    m_outlineEffectMaterial.SetTexture("_BaseMap", m_copiedColor);
                    m_outlineEffectMaterial.SetColor("_OutlineColor", m_screenSpaceOutlineSetting.OutlineColor.value);

                    DrawTriangle(cmd, m_outlineEffectMaterial, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }
#if UNITY_6000_0_OR_NEWER
            private void DrawTriangle(RasterCommandBuffer cmd, Material material, int shaderPass, Vector2 blitScale)
            {
                material.SetVector("_BlitScaleBias", blitScale);
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }
            
            private class CopyPassData
            {
                public TextureHandle Source;
            }
            
            private class OutlinePassData
            {
                public TextureHandle CameraColor;
                public TextureHandle Source;
                public TextureHandle Destination;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_outlineEffectMaterial == null)
                    return;
                
                m_screenSpaceOutlineSetting = VolumeManager.instance.stack.GetComponent<ASP.ASPSreenSpaceOutline>();
                if (m_screenSpaceOutlineSetting == null)
                    return;
                if(!m_screenSpaceOutlineSetting.IsActive())
                    return;
                var postProcessingData = frameData.Get<UniversalPostProcessingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                var colorCopyDescriptor = cameraData.cameraTargetDescriptor;
                colorCopyDescriptor.msaaSamples = 1;
                colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
                
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    cameraData.cameraTargetDescriptor.width,
                    cameraData.cameraTargetDescriptor.height);
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.depthBufferBits = 0;

                var outlineEdgeOutput = TextureHandle.nullHandle;
                outlineEdgeOutput =
 UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ASPOutlineTexture", false);
                //pass 0
                using (var builder =
                       renderGraph.AddRasterRenderPass<OutlinePassData>("ASP Screen Space Outline Edge Pass", out var passData))
                {
                    builder.UseAllGlobalTextures(true);
                    passData.Source = resourceData.activeColorTexture;
                    passData.Destination = outlineEdgeOutput;
                    passData.Material = m_outlineEffectMaterial;

                    builder.UseTexture(passData.Source, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((OutlinePassData data, RasterGraphContext rgContext) =>
                    {
                        SetupOutlineFirstPassParam(data.Material, m_screenSpaceOutlineSetting);
                        DrawTriangle(rgContext.cmd, data.Material, 0, Vector2.one);
                    });
                }
                
                //copy current frame content to another RT, since we need to blit the RT to current frame content in the next step.  
                var copiedColorRT = TextureHandle.nullHandle;
                copiedColorRT = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorCopyDescriptor,
                        "_FullscreenPassColorCopy", false);
                {

                    using (var builder =
 renderGraph.AddRasterRenderPass<CopyPassData>("ASP Screen Space Outline Pass Copy Color",
                               out var passData))
                    {
                        passData.Source = resourceData.activeColorTexture;
                        builder.UseTexture(resourceData.activeColorTexture, AccessFlags.Read);

                        //copiedColorRT is now the blit destination
                        builder.SetRenderAttachment(copiedColorRT, 0, AccessFlags.Write);
                        builder.SetRenderFunc((CopyPassData data, RasterGraphContext rgContext) =>
                        {
                            Blitter.BlitTexture(rgContext.cmd, data.Source.IsValid() ? data.Source : null,
                                new Vector4(1, 1, 0, 0), 0.0f, false);
                        });
                    }
                }
                
                //pass 1
                using (var builder =
                       renderGraph.AddRasterRenderPass<OutlinePassData>("ASP Screen Space Outline Apply Pass", out var passData))
                {
                    builder.UseAllGlobalTextures(true);
                    passData.CameraColor = copiedColorRT;
                    passData.Source = outlineEdgeOutput;
                    passData.Destination = resourceData.activeColorTexture;
                    passData.Material = m_outlineEffectMaterial;

                    builder.UseTexture(passData.CameraColor, AccessFlags.Read);
                    builder.UseTexture(passData.Source, AccessFlags.Read);
                    builder.SetRenderAttachment(passData.Destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((OutlinePassData data, RasterGraphContext rgContext) =>
                    {
                        CoreUtils.SetKeyword(data.Material, "_APPLY_FXAA", m_screenSpaceOutlineSetting.ApplyFXAA.value);
                        data.Material.SetTexture("_ASPOutlineTexture", data.Source);
                        data.Material.SetTexture("_BaseMap", data.CameraColor);
                        data.Material.SetColor("_OutlineColor", m_screenSpaceOutlineSetting.OutlineColor.value);
                        SetupOutlineFirstPassParam(data.Material, m_screenSpaceOutlineSetting);
                        DrawTriangle(rgContext.cmd, data.Material, 1, Vector2.one);
                    });
                }
            }
#endif
        }
    }
#endif

    #endregion
}