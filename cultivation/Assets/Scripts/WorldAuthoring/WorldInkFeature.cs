using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Cultivation.WorldAuthoring
{
    /// <summary>Environment ink contours for explicitly marked map objects and authoring cameras.</summary>
    public sealed class WorldInkFeature : ScriptableRendererFeature
    {
        public Shader maskShader;
        public Shader compositeShader;
        InkPass pass;
        static bool Eligible(Camera camera)=>camera.GetComponent<WorldInkCamera>()!=null || (camera.cameraType==CameraType.SceneView && Object.FindObjectOfType<WorldMapRenderer>());

        public override void Create()
        {
            pass?.Dispose();
            if (maskShader && compositeShader) pass = new InkPass(maskShader, compositeShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass != null && Eligible(data.cameraData.camera))
                renderer.EnqueuePass(pass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data)
        {
            if (pass != null && Eligible(data.cameraData.camera))
                pass.source = renderer.cameraColorTargetHandle;
        }

        protected override void Dispose(bool disposing) => pass?.Dispose();

        sealed class InkPass : ScriptableRenderPass
        {
            public RTHandle source;
            readonly Material mask, occluder, composite;
            readonly Dictionary<(Material,bool),Material> cutoutMasks=new Dictionary<(Material,bool),Material>();
            readonly Plane[] planes=new Plane[6];
            RenderTexture normalTexture,idTexture;
            readonly int normals = Shader.PropertyToID("_StudyInkNormals");
            readonly int ids = Shader.PropertyToID("_StudyInkIds");
            readonly int copy = Shader.PropertyToID("_StudyInkCopy");
            WorldInkTarget[] subjects;

            public InkPass(Shader maskShader, Shader compositeShader)
            {
                renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing-2);
                mask = CoreUtils.CreateEngineMaterial(maskShader);
                occluder = CoreUtils.CreateEngineMaterial(maskShader);occluder.SetFloat("_ID",0);
                composite = CoreUtils.CreateEngineMaterial(compositeShader);
                mask.SetFloat("_ID", 0.25f);
                composite.SetFloat("_PlayerID", 0.25f);
                composite.SetColor("_PlayerColor", new Color(.025f, .031f, .039f));
                composite.SetFloat("_Radius", 1.45f);
                composite.SetFloat("_Soft", .45f);
                composite.SetFloat("_Strength", .88f);
                composite.SetFloat("_DepthThr", .02f);
                composite.SetFloat("_NormalThr", .40f);
                composite.SetFloat("_IDThr", .02f);
                composite.SetFloat("_WidthByDepth", .38f);
                composite.SetFloat("_DryBrush", .10f);
                composite.SetFloat("_InkFade", .22f);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                var descriptor = data.cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                cmd.GetTemporaryRT(copy, descriptor, FilterMode.Bilinear);
                descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
                descriptor.depthBufferBits = 24;
                normalTexture=RenderTexture.GetTemporary(descriptor);normalTexture.filterMode=FilterMode.Bilinear;
                descriptor.colorFormat = RenderTextureFormat.ARGB32;
                descriptor.depthBufferBits = 0;
                idTexture=RenderTexture.GetTemporary(descriptor);idTexture.filterMode=FilterMode.Point;
                // The study scene is small and static; cache per camera, never scan the game actors.
                subjects = new List<WorldInkTarget>(WorldInkTarget.Active).ToArray();
                if(subjects.Length==0)subjects=Object.FindObjectsOfType<WorldInkTarget>();
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (source == null) return;
                var cmd = CommandBufferPool.Get("Rendering study: original ink outline");
                try
                {
                    cmd.SetRenderTarget(idTexture);
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    cmd.SetRenderTarget(normalTexture);
                    cmd.ClearRenderTarget(true, true, new Color(.5f, .5f, .5f, 1));
                    cmd.SetRenderTarget(new RenderTargetIdentifier[] {normalTexture,idTexture},normalTexture);
                    mask.SetFloat("_CamFar", data.cameraData.camera.farClipPlane);
                    occluder.SetFloat("_CamFar",data.cameraData.camera.farClipPlane);
                    GeometryUtility.CalculateFrustumPlanes(data.cameraData.camera,planes);
                    foreach (var subject in subjects)
                    {
                        if(!subject.isActiveAndEnabled)continue;
                        var renderers=subject.Renderers;var sources=subject.材质列表;
                        for(int ri=0;ri<renderers.Length;ri++)
                        {
                            var renderer=renderers[ri];if (!renderer||!renderer.enabled || renderer is ParticleSystemRenderer||(data.cameraData.camera.cullingMask&(1<<renderer.gameObject.layer))==0||!GeometryUtility.TestPlanesAABB(planes,renderer.bounds)) continue;
                            var materials=sources[ri];for (int submesh = 0; submesh < materials.Length; ++submesh)
                            {
                                var sourceMaterial=materials[submesh];bool onlyDepth=subject.仅遮挡(sourceMaterial);var draw=onlyDepth?occluder:mask;
                                if(sourceMaterial && sourceMaterial.HasProperty("_Cutoff") && sourceMaterial.GetFloat("_Cutoff")>0)
                                {
                                    var key=(sourceMaterial,onlyDepth);if(!cutoutMasks.TryGetValue(key,out draw))
                                    {
                                        draw=new Material(onlyDepth?occluder:mask);draw.SetTexture("_BaseMap",sourceMaterial.GetTexture("_BaseMap"));draw.SetFloat("_Cutoff",sourceMaterial.GetFloat("_Cutoff"));draw.SetFloat("_Cull",sourceMaterial.GetFloat("_Cull"));draw.SetTextureScale("_BaseMap",sourceMaterial.GetTextureScale("_BaseMap"));draw.SetTextureOffset("_BaseMap",sourceMaterial.GetTextureOffset("_BaseMap"));cutoutMasks[key]=draw;
                                    }
                                    draw.SetFloat("_CamFar",data.cameraData.camera.farClipPlane);
                                }
                                cmd.DrawRenderer(renderer,draw,submesh,0);
                            }
                        }
                    }
                    // Material texture properties override globals, including black defaults.
                    composite.SetTexture("_MaskTex",normalTexture);
                    composite.SetTexture("_IDTex",idTexture);
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
                if(normalTexture)RenderTexture.ReleaseTemporary(normalTexture);normalTexture=null;
                if(idTexture)RenderTexture.ReleaseTemporary(idTexture);idTexture=null;
                cmd.ReleaseTemporaryRT(copy);
            }

            public void Dispose()
            {
                CoreUtils.Destroy(mask);
                CoreUtils.Destroy(occluder);
                CoreUtils.Destroy(composite);
                foreach(var material in cutoutMasks.Values)CoreUtils.Destroy(material);cutoutMasks.Clear();
            }
        }
    }
}
