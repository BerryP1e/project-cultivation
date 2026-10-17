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

namespace ASP
{
    #region UNITY2021

#if UNITY_2021
     public class ASPBlitRendererFeature : ScriptableRendererFeature
    {

        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;
        public Material passMaterial;
        public bool UseHalfScale;
        public string OutputTextureName;
        public bool bindDepthStencilAttachment = false;
        private bool fetchColorBuffer = true;

        private FullScreenRenderPass m_FullScreenPass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_FullScreenPass = new FullScreenRenderPass(name, OutputTextureName, UseHalfScale);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (passMaterial == null)
                return;

            m_FullScreenPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_FullScreenPass.ConfigureInput(requirements);
            m_FullScreenPass.SetupMembers(passMaterial, fetchColorBuffer, bindDepthStencilAttachment);

            renderer.EnqueuePass(m_FullScreenPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_FullScreenPass.Dispose();
        }

        public class FullScreenRenderPass : ScriptableRenderPass
        {
            private string m_OutputTextureName;
            private Material m_Material;
            private bool m_CopyActiveColor;
            private bool m_BindDepthStencilAttachment;
            private RenderTexture m_copiedColor;
            private RenderTexture m_outputRT;
            private bool m_UseHalfScale;

            public FullScreenRenderPass(string passName, string outputTextureName, bool useHalfScale)
            {
                m_OutputTextureName = outputTextureName;
                profilingSampler = new ProfilingSampler(passName);
                m_UseHalfScale = useHalfScale;
            }

            public void SetupMembers(Material material, bool copyActiveColor, bool bindDepthStencilAttachment)
            {
                m_Material = material;
                m_CopyActiveColor = copyActiveColor;
                m_BindDepthStencilAttachment = bindDepthStencilAttachment;
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
			    // FullScreenPass manages its own RenderTarget.
                // ResetTarget here so that ScriptableRenderer's active attachement can be invalidated when processing this ScriptableRenderPass.
               //ResetTarget();
               
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                
                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;

                if (m_copiedColor != null)
                {
                    RenderTexture.ReleaseTemporary(m_copiedColor);
                    m_copiedColor = null;
                }
                
                m_copiedColor = RenderTexture.GetTemporary(desc);
                
                if (m_OutputTextureName != string.Empty)
                {
                   int width = m_UseHalfScale
                       ? renderingData.cameraData.cameraTargetDescriptor.width / 2
                       : renderingData.cameraData.cameraTargetDescriptor.width;
                   int height = m_UseHalfScale
                       ? renderingData.cameraData.cameraTargetDescriptor.height / 2
                       : renderingData.cameraData.cameraTargetDescriptor.height;
                       desc.width = width;
                       desc.height = height;
               
                   if (m_copiedColor == null)
                   {
                       m_outputRT = RenderTexture.GetTemporary(desc);
                   }
                    ConfigureTarget(m_outputRT);
                }
                else
                {
                    ConfigureTarget(m_copiedColor);
                }

                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public void Dispose()
            {
            }
            
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (m_copiedColor != null)
                {
                    RenderTexture.ReleaseTemporary(m_copiedColor);
                    m_copiedColor = null;
                }
                
                if (m_outputRT != null)
                {
                    RenderTexture.ReleaseTemporary(m_outputRT);
                    m_outputRT = null;
                }
            }

            
            private void DrawQuad(CommandBuffer cmd, Material material, int shaderPass)
            {
                /*if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);*/
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, shaderPass);   
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                
                if (m_Material == null)
                    return;
                
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(null, profilingSampler))
                {
                    if (m_CopyActiveColor)
                    {
                        CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                        Blit(cmd, cameraData.renderer.cameraColorTarget, m_copiedColor);
                        
                    }
                    
                    if (m_BindDepthStencilAttachment)
                    {
                        CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget, cameraData.renderer.cameraDepthTarget);
                    }
                    else
                    {
                        if (m_outputRT != null)
                        {
                            CoreUtils.SetRenderTarget(cmd, m_outputRT);
                        }
                        else
                        {
                            CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);
                        }

                    }
                    m_Material.SetVector("_BlitScaleBias", new Vector4(1,1,0,0));
                    m_Material.SetTexture("_BaseMap", m_copiedColor);
                    DrawQuad(cmd, m_Material, 0);
                    if (m_outputRT != null)
                    {
                        cmd.SetGlobalTexture(m_OutputTextureName, m_outputRT);
                    }

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
    public class ASPBlitRendererFeature : ScriptableRendererFeature
    {
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public ScriptableRenderPassInput requirements = ScriptableRenderPassInput.None;
        public Material passMaterial;
        public bool UseHalfScale;
        public string OutputTextureName;
        public bool bindDepthStencilAttachment = false;
        private bool fetchColorBuffer = true;

        private FullScreenRenderPass m_FullScreenPass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_FullScreenPass = new FullScreenRenderPass(name, OutputTextureName, UseHalfScale);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (passMaterial == null)
                return;

            m_FullScreenPass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_FullScreenPass.ConfigureInput(requirements);
            m_FullScreenPass.SetupMembers(passMaterial, fetchColorBuffer, bindDepthStencilAttachment);

            renderer.EnqueuePass(m_FullScreenPass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_FullScreenPass.Dispose();
        }

        public class FullScreenRenderPass : ScriptableRenderPass
        {
            private string m_OutputTextureName;
            private Material m_Material;
            private bool m_CopyActiveColor;
            private bool m_BindDepthStencilAttachment;
            private RTHandle m_CopiedColor;
            private RTHandle m_outputRT;
            private bool m_UseHalfScale;

            public FullScreenRenderPass(string passName, string outputTextureName, bool useHalfScale)
            {
                m_OutputTextureName = outputTextureName;
                profilingSampler = new ProfilingSampler(passName);
                m_UseHalfScale = useHalfScale;
            }

            public void SetupMembers(Material material, bool copyActiveColor, bool bindDepthStencilAttachment)
            {
                m_Material = material;
                m_CopyActiveColor = copyActiveColor;
                m_BindDepthStencilAttachment = bindDepthStencilAttachment;
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                // FullScreenPass manages its own RenderTarget.
                // ResetTarget here so that ScriptableRenderer's active attachement can be invalidated when processing this ScriptableRenderPass.
                //ResetTarget();

                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = RenderTextureFormat.ARGB32;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_CopiedColor, desc, name: "_CameraColorTexture");
                if (m_OutputTextureName != string.Empty)
                {
                    int width = m_UseHalfScale
                        ? renderingData.cameraData.cameraTargetDescriptor.width / 2
                        : renderingData.cameraData.cameraTargetDescriptor.width;
                    int height = m_UseHalfScale
                        ? renderingData.cameraData.cameraTargetDescriptor.height / 2
                        : renderingData.cameraData.cameraTargetDescriptor.height;
                    desc.width = width;
                    desc.height = height;
                    RenderingUtils.ReAllocateIfNeeded(ref m_outputRT, desc, name: m_OutputTextureName);
                    ConfigureTarget(m_outputRT);
                }
                else
                {
                    ConfigureTarget(m_CopiedColor);
                }

                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public void Dispose()
            {
                m_CopiedColor?.Release();
                m_outputRT?.Release();
            }

            private void DrawQuad(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Quads, 4, 1);
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material == null)
                    return;

                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, new ProfilingSampler("Blit Render Pass")))
                {
                    if (m_CopyActiveColor)
                    {
                        CoreUtils.SetRenderTarget(cmd, m_CopiedColor);
                        Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, m_CopiedColor);
                    }

                    if (m_BindDepthStencilAttachment)
                    {
                        CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle,
                            cameraData.renderer.cameraDepthTargetHandle);
                    }
                    else
                    {
                        if (m_outputRT != null)
                        {
                            CoreUtils.SetRenderTarget(cmd, m_outputRT);
                        }
                        else
                        {
                            CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                        }
                    }

                    m_Material.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                    m_Material.SetTexture("_BaseMap", m_CopiedColor);
                    m_Material.SetTexture("_BlitTexture", m_CopiedColor);
                    DrawQuad(cmd, m_Material, 0);
                    if (m_outputRT != null)
                    {
                        cmd.SetGlobalTexture(m_OutputTextureName, m_outputRT);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
#endif

    #endregion
}