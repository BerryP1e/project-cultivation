using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cultivation.RenderingStudy
{
    /// <summary>Study-only URP adapter for the existing ink mask and composite shaders.</summary>
    public sealed class StudyInkOutlineFeature : ScriptableRendererFeature
    {
        public Shader maskShader;
        public Shader compositeShader;
        InkPass pass;

        public override void Create()
        {
            pass?.Dispose();
            if (maskShader && compositeShader) pass = new InkPass(maskShader, compositeShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass != null && data.cameraData.camera.name == "RenderingStudyCamera")
                renderer.EnqueuePass(pass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data)
        {
            if (pass != null && data.cameraData.camera.name == "RenderingStudyCamera")
                pass.source = renderer.cameraColorTargetHandle;
        }

        protected override void Dispose(bool disposing) => pass?.Dispose();

        sealed class InkPass : ScriptableRenderPass
        {
            public RTHandle source;
            readonly Material mask, composite;
            readonly int normals = Shader.PropertyToID("_StudyInkNormals");
            readonly int ids = Shader.PropertyToID("_StudyInkIds");
            readonly int copy = Shader.PropertyToID("_StudyInkCopy");
            RenderingStudySubject[] subjects;

            public InkPass(Shader maskShader, Shader compositeShader)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                mask = CoreUtils.CreateEngineMaterial(maskShader);
                composite = CoreUtils.CreateEngineMaterial(compositeShader);
                mask.SetFloat("_ID", 0.25f);
                composite.SetFloat("_PlayerID", 0.25f);
                composite.SetColor("_PlayerColor", new Color(.055f, .048f, .070f));
                composite.SetFloat("_Radius", 1.15f);
                composite.SetFloat("_Soft", .45f);
                composite.SetFloat("_Strength", .85f);
                composite.SetFloat("_DepthThr", .02f);
                composite.SetFloat("_NormalThr", .85f);
                composite.SetFloat("_IDThr", .02f);
                composite.SetFloat("_WidthByDepth", .38f);
                composite.SetFloat("_DryBrush", .45f);
                composite.SetFloat("_InkFade", .42f);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                var descriptor = data.cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                cmd.GetTemporaryRT(copy, descriptor, FilterMode.Bilinear);
                descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
                descriptor.depthBufferBits = 24;
                cmd.GetTemporaryRT(normals, descriptor, FilterMode.Bilinear);
                descriptor.colorFormat = RenderTextureFormat.ARGB32;
                descriptor.depthBufferBits = 0;
                cmd.GetTemporaryRT(ids, descriptor, FilterMode.Point);
                // The study scene is small and static; cache per camera, never scan the game actors.
                subjects = Object.FindObjectsOfType<RenderingStudySubject>();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (source == null) return;
                var cmd = CommandBufferPool.Get("Rendering study: original ink outline");
                try
                {
                    cmd.SetRenderTarget(ids);
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    cmd.SetRenderTarget(normals);
                    cmd.ClearRenderTarget(true, true, new Color(.5f, .5f, .5f, 1));
                    cmd.SetRenderTarget(new RenderTargetIdentifier[] { normals, ids }, normals);
                    mask.SetFloat("_CamFar", data.cameraData.camera.farClipPlane);
                    foreach (var subject in subjects)
                        foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
                        {
                            if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                            for (int submesh = 0; submesh < renderer.sharedMaterials.Length; ++submesh)
                                cmd.DrawRenderer(renderer, mask, submesh, 0);
                        }
                    cmd.SetGlobalTexture("_MaskTex", normals);
                    cmd.SetGlobalTexture("_IDTex", ids);
                    var d = data.cameraData.cameraTargetDescriptor;
                    cmd.SetGlobalVector("_MaskTex_TexelSize", new Vector4(1f/d.width, 1f/d.height, d.width, d.height));
                    cmd.Blit(source.nameID, copy, composite, 0);
                    cmd.Blit(copy, source.nameID);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally { CommandBufferPool.Release(cmd); }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(normals);
                cmd.ReleaseTemporaryRT(ids);
                cmd.ReleaseTemporaryRT(copy);
            }

            public void Dispose()
            {
                CoreUtils.Destroy(mask);
                CoreUtils.Destroy(composite);
            }
        }
    }
}
