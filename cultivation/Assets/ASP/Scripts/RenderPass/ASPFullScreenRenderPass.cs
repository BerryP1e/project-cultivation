using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ASP
{
#if UNITY_2021
    public class ASPFullScreenRenderPass : ScriptableRenderPass
    {
        public static readonly int blitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int blitScaleBias = Shader.PropertyToID("_BlitScaleBias");

        private Material m_Material;
        private int m_PassIndex;
        private bool m_CopyActiveColor;
        private bool m_BindDepthStencilAttachment;
        private RenderTexture m_copiedColor;

        private static MaterialPropertyBlock s_SharedPropertyBlock = new MaterialPropertyBlock();

        public ASPFullScreenRenderPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);
        }

        public void SetupMembers(Material material, int passIndex, bool copyActiveColor,
            bool bindDepthStencilAttachment)
        {
            m_Material = material;
            m_PassIndex = passIndex;
            m_CopyActiveColor = copyActiveColor;
            m_BindDepthStencilAttachment = bindDepthStencilAttachment;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
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
        }
        
        public void ExecuteCopyColorPass(CommandBuffer cmd, RenderTargetIdentifier sourceTexture, RenderTargetIdentifier target)
        {
            Blit(cmd, sourceTexture, target);
        }

        private static void ExecuteMainPass(CommandBuffer cmd, RTHandle sourceTexture, Material material, int passIndex)
        {
            s_SharedPropertyBlock.Clear();
            if (sourceTexture != null)
                s_SharedPropertyBlock.SetTexture(blitTexture, sourceTexture);

            // We need to set the "_BlitScaleBias" uniform for user materials with shaders relying on core Blit.hlsl to work
            s_SharedPropertyBlock.SetVector(blitScaleBias, new Vector4(1, 1, 0, 0));

            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1,
                s_SharedPropertyBlock);
        }
        
        private void DrawQuad(CommandBuffer cmd, Material material, int shaderPass)
        {
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
                    ExecuteCopyColorPass(cmd, cameraData.renderer.cameraColorTarget, m_copiedColor);
                }

                if (m_BindDepthStencilAttachment)
                {
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget,
                        cameraData.renderer.cameraDepthTarget);
                }
                else
                {
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);
                }

                m_Material.SetVector("_BlitScaleBias", new Vector4(1,1,0,0));
                m_Material.SetTexture("_BaseMap", m_copiedColor);
                DrawQuad(cmd, m_Material, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
#endif
#if UNITY_2022_1_OR_NEWER
    public class ASPFullScreenRenderPass : ScriptableRenderPass
    {
        public static readonly int blitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int blitScaleBias = Shader.PropertyToID("_BlitScaleBias");

        private Material m_Material;
        private int m_PassIndex;
        private bool m_CopyActiveColor;
        private bool m_BindDepthStencilAttachment;
        private RTHandle m_CopiedColor;

        private static MaterialPropertyBlock s_SharedPropertyBlock = new MaterialPropertyBlock();

        public ASPFullScreenRenderPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);
        }

        public void SetupMembers(Material material, int passIndex, bool copyActiveColor,
            bool bindDepthStencilAttachment)
        {
            m_Material = material;
            m_PassIndex = passIndex;
            m_CopyActiveColor = copyActiveColor;
            m_BindDepthStencilAttachment = bindDepthStencilAttachment;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // FullScreenPass manages its own RenderTarget.
            // ResetTarget here so that ScriptableRenderer's active attachement can be invalidated when processing this ScriptableRenderPass.
            ResetTarget();

            if (m_CopyActiveColor)
                ReAllocate(renderingData.cameraData.cameraTargetDescriptor);
        }

        internal void ReAllocate(RenderTextureDescriptor desc)
        {
            desc.msaaSamples = 1;
            desc.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateIfNeeded(ref m_CopiedColor, desc, name: "_FullscreenPassColorCopy");
        }

        public void Dispose()
        {
            m_CopiedColor?.Release();
        }

        private static void ExecuteCopyColorPass(CommandBuffer cmd, RTHandle sourceTexture)
        {
            Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
        }
        

        private static void ExecuteMainPass(CommandBuffer cmd, RTHandle sourceTexture, Material material, int passIndex)
        {
            s_SharedPropertyBlock.Clear();
            if (sourceTexture != null)
                s_SharedPropertyBlock.SetTexture(blitTexture, sourceTexture);

            // We need to set the "_BlitScaleBias" uniform for user materials with shaders relying on core Blit.hlsl to work
            s_SharedPropertyBlock.SetVector(blitScaleBias, new Vector4(1, 1, 0, 0));

            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1,
                s_SharedPropertyBlock);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            if (cameraData.renderer.cameraColorTargetHandle == null)
                return;
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler))
            {
                if (m_CopyActiveColor)
                {
                    CoreUtils.SetRenderTarget(cmd, m_CopiedColor);
                    ExecuteCopyColorPass(cmd, cameraData.renderer.cameraColorTargetHandle);
                }

                if (m_BindDepthStencilAttachment)
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle,
                        cameraData.renderer.cameraDepthTargetHandle);
                else
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);

                ExecuteMainPass(cmd, m_CopyActiveColor ? m_CopiedColor : null, m_Material, m_PassIndex);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

#if UNITY_6000_0_OR_NEWER
                
        private static void ExecuteCopyColorPass(RasterCommandBuffer cmd, RTHandle sourceTexture)
        {
            Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
        }
        
        private static void ExecuteMainPass(RasterCommandBuffer cmd, RTHandle sourceTexture, Material material, int passIndex)
        {
            s_SharedPropertyBlock.Clear();
            if (sourceTexture != null)
                s_SharedPropertyBlock.SetTexture(blitTexture, sourceTexture);

            // We need to set the "_BlitScaleBias" uniform for user materials with shaders relying on core Blit.hlsl to work
            s_SharedPropertyBlock.SetVector(blitScaleBias, new Vector4(1, 1, 0, 0));

            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1,
                s_SharedPropertyBlock);
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourcesData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle source, destination;

            Debug.Assert(resourcesData.cameraColor.IsValid());

            if (m_CopyActiveColor)
            {
                var targetDesc = renderGraph.GetTextureDesc(resourcesData.cameraColor);
                targetDesc.name = "_CameraColorFullScreenPass";
                targetDesc.clearBuffer = false;

                source = resourcesData.activeColorTexture;
                destination = renderGraph.CreateTexture(targetDesc);

                using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("Copy Color Full Screen",
                           out var passData, profilingSampler))
                {
                    passData.inputTexture = source;
                    builder.UseTexture(passData.inputTexture, AccessFlags.Read);

                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                    builder.SetRenderFunc((CopyPassData data, RasterGraphContext rgContext) =>
                    {
                        ExecuteCopyColorPass(rgContext.cmd, data.inputTexture);
                    });
                }

                //Swap for next pass;
                source = destination;
            }
            else
            {
                source = TextureHandle.nullHandle;
            }

            destination = resourcesData.activeColorTexture;


            using (var builder =
                   renderGraph.AddRasterRenderPass<MainPassData>(passName, out var passData, profilingSampler))
            {
                passData.material = m_Material;
                passData.passIndex = m_PassIndex;

                passData.inputTexture = source;

                if (passData.inputTexture.IsValid())
                    builder.UseTexture(passData.inputTexture, AccessFlags.Read);

                bool needsColor = (input & ScriptableRenderPassInput.Color) != ScriptableRenderPassInput.None;
                bool needsDepth = (input & ScriptableRenderPassInput.Depth) != ScriptableRenderPassInput.None;
                bool needsMotion = (input & ScriptableRenderPassInput.Motion) != ScriptableRenderPassInput.None;
                bool needsNormal = (input & ScriptableRenderPassInput.Normal) != ScriptableRenderPassInput.None;

                if (needsColor)
                {
                    Debug.Assert(resourcesData.cameraOpaqueTexture.IsValid());
                    builder.UseTexture(resourcesData.cameraOpaqueTexture);
                }

                if (needsDepth)
                {
                    Debug.Assert(resourcesData.cameraDepthTexture.IsValid());
                    builder.UseTexture(resourcesData.cameraDepthTexture);
                }

                if (needsMotion)
                {
                    Debug.Assert(resourcesData.motionVectorColor.IsValid());
                    builder.UseTexture(resourcesData.motionVectorColor);
                    Debug.Assert(resourcesData.motionVectorDepth.IsValid());
                    builder.UseTexture(resourcesData.motionVectorDepth);
                }

                if (needsNormal)
                {
                    Debug.Assert(resourcesData.cameraNormalsTexture.IsValid());
                    builder.UseTexture(resourcesData.cameraNormalsTexture);
                }

                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                if (m_BindDepthStencilAttachment)
                    builder.SetRenderAttachmentDepth(resourcesData.activeDepthTexture, AccessFlags.Write);

                builder.SetRenderFunc((MainPassData data, RasterGraphContext rgContext) =>
                {
                    ExecuteMainPass(rgContext.cmd, data.inputTexture, data.material, data.passIndex);
                });
            }
        }

        private class CopyPassData
        {
            internal TextureHandle inputTexture;
        }

        private class MainPassData
        {
            internal Material material;
            internal int passIndex;
            internal TextureHandle inputTexture;
        }
#endif
    }
#endif
}