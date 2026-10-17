using ASPUtil;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ASP
{
    #region UNITY2021

#if UNITY_2021
        [DisallowMultipleRendererFeature("Screen Space Lens Flare")]
    public class ASPScreenSpaceLensFlareFeature : ScriptableRendererFeature
    {
         static readonly int _LensFlareScreenSpaceBloomMipTexture =
 Shader.PropertyToID("_LensFlareScreenSpaceBloomMipTexture");
         static readonly int _LensFlareScreenSpaceResultTexture =
 Shader.PropertyToID("_LensFlareScreenSpaceResultTexture");
         static readonly int _LensFlareScreenSpaceSpectralLut = Shader.PropertyToID("_LensFlareScreenSpaceSpectralLut");
         static readonly int _LensFlareScreenSpaceStreakTex = Shader.PropertyToID("_LensFlareScreenSpaceStreakTex");
         static readonly int _LensFlareScreenSpaceMipLevel = Shader.PropertyToID("_LensFlareScreenSpaceMipLevel");
         static readonly int _LensFlareScreenSpaceTintColor = Shader.PropertyToID("_LensFlareScreenSpaceTintColor");
         static readonly int _LensFlareScreenSpaceParams1 = Shader.PropertyToID("_LensFlareScreenSpaceParams1");
         static readonly int _LensFlareScreenSpaceParams2 = Shader.PropertyToID("_LensFlareScreenSpaceParams2");
         static readonly int _LensFlareScreenSpaceParams3 = Shader.PropertyToID("_LensFlareScreenSpaceParams3");
         static readonly int _LensFlareScreenSpaceParams4 = Shader.PropertyToID("_LensFlareScreenSpaceParams4");
         static readonly int _LensFlareScreenSpaceParams5 = Shader.PropertyToID("_LensFlareScreenSpaceParams5");
         static readonly int  _Bloom_Texture_ID = Shader.PropertyToID("_Bloom_Texture");
        
        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material material;
        private ASPScreenSpaceLensFlarePass m_aspScrAspScreenSpaceLensFlarePass;

        /// <inheritdoc/>
        public override void Create()
        {
            m_aspScrAspScreenSpaceLensFlarePass = new ASPScreenSpaceLensFlarePass(name);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/URP/BackPort/LensFlareScreenSpace");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }
                return;
            }
            m_aspScrAspScreenSpaceLensFlarePass.Setup(ref renderingData);

            m_aspScrAspScreenSpaceLensFlarePass.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_aspScrAspScreenSpaceLensFlarePass.ConfigureInput(ScriptableRenderPassInput.None);
            m_aspScrAspScreenSpaceLensFlarePass.SetupMembers(material);

            renderer.EnqueuePass(m_aspScrAspScreenSpaceLensFlarePass);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_aspScrAspScreenSpaceLensFlarePass.Dispose();
        }

        public class ASPScreenSpaceLensFlarePass : ScriptableRenderPass
        {
            private Material m_lensFlareSSMaterial;
            private RTHandle m_copiedColor;
            private RenderTargetHandle m_StreakTmpTexture;
            private RenderTargetHandle m_StreakTmpTexture2;
            private RenderTargetHandle m_ScreenSpaceLensFlareResult;
            private ASP.ASPScreenSpaceLensFlare m_lensFlareAspScreenSpace;
            private int m_writeToBloomPass;
            public ASPScreenSpaceLensFlarePass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
                m_StreakTmpTexture.Init("_StreakTmpTexture");
                m_StreakTmpTexture2.Init("_StreakTmpTexture2");
                m_ScreenSpaceLensFlareResult.Init("_LensFlareScreenSpaceResultTexture");
            }

            public void SetupMembers(Material material)
            {
                m_lensFlareSSMaterial = material;
                m_writeToBloomPass = m_lensFlareSSMaterial.FindPass("LensFlareScreenSpace Write to BloomTexture");
            }

            public void Setup(ref RenderingData renderingData)
            {
                if (m_lensFlareAspScreenSpace == null)
                {
                    m_lensFlareAspScreenSpace =
 VolumeManager.instance.stack.GetComponent<ASP.ASPScreenSpaceLensFlare>();
                    
                }
               // Debug.LogError("OnCameraSetup");
                
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                
                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                if (m_copiedColor == null)
                {
                    m_copiedColor =
 RTHandles.Alloc(desc.width, desc.height, 1, DepthBits.None, name:"_CameraColorTexture");
                }
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                //RenderingUtils.ReAllocateIfNeeded(ref m_copiedColor, desc, name: "_CameraColorTexture");
                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);
                
            }
            
            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(m_StreakTmpTexture.id);
                cmd.ReleaseTemporaryRT(m_StreakTmpTexture2.id);
                cmd.ReleaseTemporaryRT(m_ScreenSpaceLensFlareResult.id);
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
            }
            
            private static void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                
                if (m_lensFlareSSMaterial == null)
                    return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get(); 
               
                var desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);
                
                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                
                var m_Bloom = VolumeManager.instance.stack.GetComponent<Bloom>();
                if (!m_Bloom.IsActive())
                {
                    return;
                }

                //int maxBloomMip = Mathf.Clamp(m_lensFlareScreenSpace.bloomMip.value, 0, m_Bloom.maxIterations.value/2);
                
                using (new ProfilingScope(cmd, new ProfilingSampler("Post Process Screen Space Lens Flare")))
                {
                    //fetch current camera Color to copiedColor RT
                    CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    Blit(cmd, cameraData.renderer.cameraColorTarget, m_copiedColor);
                  
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);
                    DoLensFlareScreenSpace(cameraData.camera, cmd,cameraData.renderer.cameraColorTarget, 
                        Shader.GetGlobalTexture("_Bloom_Texture"), Shader.GetGlobalTexture("_SourceTexLowMip"), desc);
                    
                    m_lensFlareSSMaterial.SetVector("_BlitScaleBias", new Vector4(1,1,0,0));
                    UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    ConfigureClear(ClearFlag.Color, Color.black);
                    DrawTriangle(cmd, m_lensFlareSSMaterial, m_writeToBloomPass);
                    
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTarget);
                    Blit(cmd, m_copiedColor, cameraData.renderer.cameraColorTarget);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                CommandBufferPool.Release(cmd);
            }
            
            void DoLensFlareScreenSpace(Camera camera, CommandBuffer cmd, RenderTargetIdentifier source, Texture originalBloomTexture, Texture screenSpaceLensFlareBloomMipTexture, RenderTextureDescriptor m_Descriptor)
            { 
                int ratio = (int)m_lensFlareAspScreenSpace.resolution.value;

                int width = Mathf.Max(1, (int)m_Descriptor.width / ratio);
                int height = Mathf.Max(1, (int)m_Descriptor.height / ratio);
                // var desc = GetCompatibleDescriptor(width, height, GraphicsFormat.R8G8B8A8_SRGB);
                var desc = m_Descriptor;
                desc.depthBufferBits = (int)DepthBits.None;
                desc.msaaSamples = 1;
                desc.width = width;
                desc.height = height;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
                if (m_lensFlareAspScreenSpace.IsStreaksActive())
                {
                    cmd.GetTemporaryRT(m_StreakTmpTexture.id, desc, FilterMode.Bilinear);
                    cmd.GetTemporaryRT(m_StreakTmpTexture2.id, desc, FilterMode.Bilinear);
                 //   RenderingUtils.ReAllocateIfNeeded(ref m_StreakTmpTexture, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_StreakTmpTexture");
                 //   RenderingUtils.ReAllocateIfNeeded(ref m_StreakTmpTexture2, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_StreakTmpTexture2");
                }

                cmd.GetTemporaryRT(m_ScreenSpaceLensFlareResult.id, desc, FilterMode.Bilinear);

              /*  RenderingUtils.ReAllocateIfNeeded(ref m_ScreenSpaceLensFlareResult, desc, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_ScreenSpaceLensFlareResult");*/

                DoLensFlareScreenSpaceCommon(
                    m_lensFlareSSMaterial,
                    camera,
                    (float)m_Descriptor.width,
                    (float)m_Descriptor.height,
                    m_lensFlareAspScreenSpace.tintColor.value,
                    originalBloomTexture,
                    screenSpaceLensFlareBloomMipTexture,
                    null, // We don't have any spectral LUT in URP
                    m_StreakTmpTexture,
                    m_StreakTmpTexture2,
                    new Vector4(
                        m_lensFlareAspScreenSpace.intensity.value,
                        m_lensFlareAspScreenSpace.firstFlareIntensity.value,
                        m_lensFlareAspScreenSpace.secondaryFlareIntensity.value,
                        m_lensFlareAspScreenSpace.warpedFlareIntensity.value),
                    new Vector4(
                        m_lensFlareAspScreenSpace.vignetteEffect.value,
                        m_lensFlareAspScreenSpace.startingPosition.value,
                        m_lensFlareAspScreenSpace.scale.value,
                        0), // Free slot, not used
                    new Vector4(
                        m_lensFlareAspScreenSpace.samples.value,
                        m_lensFlareAspScreenSpace.sampleDimmer.value,
                        m_lensFlareAspScreenSpace.chromaticAbberationIntensity.value,
                        0), // No need to pass a chromatic aberration sample count, hardcoded at 3 in shader
                    new Vector4(
                        m_lensFlareAspScreenSpace.streaksIntensity.value,
                        m_lensFlareAspScreenSpace.streaksLength.value,
                        m_lensFlareAspScreenSpace.streaksOrientation.value,
                        m_lensFlareAspScreenSpace.streaksThreshold.value),
                    new Vector4(
                        ratio,
                        m_lensFlareAspScreenSpace.warpedFlareScale.value.x,
                        m_lensFlareAspScreenSpace.warpedFlareScale.value.y,
                        0), // Free slot, not used
                    cmd,
                    m_ScreenSpaceLensFlareResult,
                    false);
               // cmd.SetGlobalTexture(_Bloom_Texture_ID, originalBloomTexture);
            }
            
            static public void DoLensFlareScreenSpaceCommon(
            Material lensFlareShader,
            Camera cam,
            float actualWidth,
            float actualHeight,
            Color tintColor,
            Texture originalBloomTexture,
            Texture bloomMipTexture,
            Texture spectralLut,
            RenderTargetHandle streakTextureTmp,
            RenderTargetHandle streakTextureTmp2,
            Vector4 parameters1,
            Vector4 parameters2,
            Vector4 parameters3,
            Vector4 parameters4,
            Vector4 parameters5,
            UnityEngine.Rendering.CommandBuffer cmd,
            RenderTargetHandle result,
            bool debugView)
            {
                
                //Multiplying parameters value here for easier maintenance since they are the same numbers between SRPs 
                parameters2.x = Mathf.Pow(parameters2.x, 0.25f);        // Vignette effect
                parameters3.z = parameters3.z / 20f;                    // chromaticAbberationIntensity
                parameters4.y = parameters4.y * 10f;                    // Streak Length                  
                parameters4.z = parameters4.z / 90f;                    // Streak Orientation
                parameters5.y = 1.0f / parameters5.y;                   // WarpedFlareScale X
                parameters5.z = 1.0f / parameters5.z;                   // WarpedFlareScale Y
                
                cmd.SetViewport(new Rect() { width = actualWidth, height = actualHeight });
                if (debugView)
                {
                    // Background pitch black to see only the flares
                    cmd.ClearRenderTarget(false, true, Color.black);
                }

    #if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    // Determine whether the "Flare" checkbox is checked for the current view.
                    for (int i =
 0; i < UnityEditor.SceneView.sceneViews.Count; i++) // Using a foreach on an ArrayList generates garbage ...
                    {
                        var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                        if (sv.camera == cam && !sv.sceneViewState.flaresEnabled)
                        {
                            return;
                        }
                    }
                }
    #endif

                // Multiple scaleX by aspect ratio so that default 1:1 scale for warped flare stays circular (as in data driven lens flare)
                float warpedScaleX = parameters5.y;
                warpedScaleX *= actualWidth / actualHeight;
                parameters5.y = warpedScaleX;

                // This is to make sure the streak length is the same in all resolutions
                float streaksLength = parameters4.y;
                streaksLength *= actualWidth * 0.0005f;
                parameters4.y = streaksLength;

                // List of the passes in LensFlareScreenSpace.shader
                int prefilterPass = lensFlareShader.FindPass("LensFlareScreenSpac Prefilter");
                int downSamplePass = lensFlareShader.FindPass("LensFlareScreenSpace Downsample");
                int upSamplePass = lensFlareShader.FindPass("LensFlareScreenSpace Upsample");
                int compositionPass = lensFlareShader.FindPass("LensFlareScreenSpace Composition");
                int writeToBloomPass = lensFlareShader.FindPass("LensFlareScreenSpace Write to BloomTexture");

                // Setting the input textures
                cmd.SetGlobalTexture(_LensFlareScreenSpaceBloomMipTexture, bloomMipTexture);
                cmd.SetGlobalTexture(_LensFlareScreenSpaceSpectralLut, spectralLut);

                // Setting parameters of the effects
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams1, parameters1);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams2, parameters2);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams3, parameters3);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams4, parameters4);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams5, parameters5);
                cmd.SetGlobalColor(_LensFlareScreenSpaceTintColor, tintColor);

                // We only do the first 3 pass if StreakIntensity (parameters4.x) is set to something above 0 to save costs
                if (parameters4.x > 0)
                {
                    // Prefilter
                    UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, streakTextureTmp.Identifier());
                    DrawTriangle(cmd, lensFlareShader, prefilterPass);

                    int maxLevel = Mathf.FloorToInt(Mathf.Log(Mathf.Max(actualHeight, actualWidth), 2.0f));
                    int maxLevelDownsample = Mathf.Max(1, maxLevel);
                    int maxLevelUpsample = 2;
                    int startIndex = 0;
                    bool even = false;

                    // Downsample
                    for (int i = 0; i < maxLevelDownsample; i++)
                    {
                        even = (i % 2 == 0);
                        cmd.SetGlobalInt(_LensFlareScreenSpaceMipLevel, i);
                        cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex, even ? streakTextureTmp.Identifier() : streakTextureTmp2.Identifier());
                        UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, even ? streakTextureTmp2.Identifier() : streakTextureTmp.Identifier());

                        DrawTriangle(cmd, lensFlareShader, downSamplePass);
                    }

                    //Since we do a ping pong between streakTextureTmp & streakTextureTmp2, we need to know which texture is the last;
                    if (even)
                        startIndex = 1;

                    //Upsample
                    for (int i = startIndex; i < (startIndex + maxLevelUpsample); i++)
                    {
                        even = (i % 2 == 0);
                        cmd.SetGlobalInt(_LensFlareScreenSpaceMipLevel, (i - startIndex));
                        cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex, even ? streakTextureTmp.Identifier() : streakTextureTmp2.Identifier());
                        UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, even ? streakTextureTmp2.Identifier() : streakTextureTmp.Identifier());

                        DrawTriangle(cmd, lensFlareShader, upSamplePass);
                    }

                    cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex, even ? streakTextureTmp2.Identifier() : streakTextureTmp.Identifier());
                }

                // Composition (Flares + Streaks)
                UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, result.Identifier());
                DrawTriangle(cmd, lensFlareShader, compositionPass);

                // Final pass, we add the result of the previous pass to the Original Bloom Texture.
                
                cmd.SetGlobalTexture(_LensFlareScreenSpaceResultTexture, result.Identifier());
                //UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, originalBloomTexture);
                //DrawQuad(cmd, lensFlareShader, writeToBloomPass);
            }
        }
    }
#endif

    #endregion

    #region UNITY2022

#if UNITY_2022
    [DisallowMultipleRendererFeature("Screen Space Lens Flare")]
    public class ASPScreenSpaceLensFlareFeature : ScriptableRendererFeature
    {
        static readonly int _LensFlareScreenSpaceBloomMipTexture =
            Shader.PropertyToID("_LensFlareScreenSpaceBloomMipTexture");

        static readonly int _LensFlareScreenSpaceResultTexture =
            Shader.PropertyToID("_LensFlareScreenSpaceResultTexture");

        static readonly int _LensFlareScreenSpaceSpectralLut = Shader.PropertyToID("_LensFlareScreenSpaceSpectralLut");
        static readonly int _LensFlareScreenSpaceStreakTex = Shader.PropertyToID("_LensFlareScreenSpaceStreakTex");
        static readonly int _LensFlareScreenSpaceMipLevel = Shader.PropertyToID("_LensFlareScreenSpaceMipLevel");
        static readonly int _LensFlareScreenSpaceTintColor = Shader.PropertyToID("_LensFlareScreenSpaceTintColor");
        static readonly int _LensFlareScreenSpaceParams1 = Shader.PropertyToID("_LensFlareScreenSpaceParams1");
        static readonly int _LensFlareScreenSpaceParams2 = Shader.PropertyToID("_LensFlareScreenSpaceParams2");
        static readonly int _LensFlareScreenSpaceParams3 = Shader.PropertyToID("_LensFlareScreenSpaceParams3");
        static readonly int _LensFlareScreenSpaceParams4 = Shader.PropertyToID("_LensFlareScreenSpaceParams4");
        static readonly int _LensFlareScreenSpaceParams5 = Shader.PropertyToID("_LensFlareScreenSpaceParams5");
        static readonly int _Bloom_Texture_ID = Shader.PropertyToID("_Bloom_Texture");

        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        private Material material;
        private ASPScreenSpaceLensFlarePass m_aspScreenSpaceLensFlare;

        /// <inheritdoc/>
        public override void Create()
        {
            m_aspScreenSpaceLensFlare = new ASPScreenSpaceLensFlarePass(name);
        }

        /// <inheritdoc/>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            if (material == null)
            {
                var defaultShader = Shader.Find("Hidden/URP/BackPort/LensFlareScreenSpace");
                if (defaultShader != null)
                {
                    material = new Material(defaultShader);
                }

                return;
            }

            m_aspScreenSpaceLensFlare.renderPassEvent = (RenderPassEvent)injectionPoint;
            m_aspScreenSpaceLensFlare.ConfigureInput(ScriptableRenderPassInput.None);
            m_aspScreenSpaceLensFlare.SetupMembers(material);

            renderer.EnqueuePass(m_aspScreenSpaceLensFlare);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            m_aspScreenSpaceLensFlare.Dispose();
        }

        public class ASPScreenSpaceLensFlarePass : ScriptableRenderPass
        {
            private Material m_lensFlareSSMaterial;
            private RTHandle m_copiedColor;
            private RTHandle m_StreakTmpTexture;
            private RTHandle m_StreakTmpTexture2;
            private RTHandle m_ScreenSpaceLensFlareResult;
            private ASP.ASPScreenSpaceLensFlare m_lensFlareAspScreenSpace;
            private int m_writeToBloomPass;

            public ASPScreenSpaceLensFlarePass(string passName)
            {
                profilingSampler = new ProfilingSampler(passName);
            }

            public void SetupMembers(Material material)
            {
                m_lensFlareSSMaterial = material;
                m_writeToBloomPass = m_lensFlareSSMaterial.FindPass("LensFlareScreenSpace Write to BloomTexture");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                m_lensFlareAspScreenSpace = VolumeManager.instance.stack.GetComponent<ASP.ASPScreenSpaceLensFlare>();

                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref m_copiedColor, desc, name: "_CameraColorTexture");
                ConfigureTarget(m_copiedColor);
                ConfigureClear(ClearFlag.Color, Color.white);
            }

            public void Dispose()
            {
                m_copiedColor?.Release();
                m_StreakTmpTexture?.Release();
                m_StreakTmpTexture2?.Release();
                m_ScreenSpaceLensFlareResult?.Release();
            }

            private static void DrawTriangle(CommandBuffer cmd, Material material, int shaderPass)
            {
                if (SystemInfo.graphicsShaderLevel < 30)
                    cmd.DrawMesh(Util.TriangleMesh, Matrix4x4.identity, material, 0, shaderPass);
                else
                    cmd.DrawProcedural(Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3, 1);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_lensFlareSSMaterial == null)
                    return;
                ref var cameraData = ref renderingData.cameraData;
                CommandBuffer cmd = CommandBufferPool.Get();

                var desc = new RenderTextureDescriptor(
                    renderingData.cameraData.cameraTargetDescriptor.width,
                    renderingData.cameraData.cameraTargetDescriptor.height);

                desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;

                var m_Bloom = VolumeManager.instance.stack.GetComponent<Bloom>();
                if (!m_Bloom.IsActive())
                {
                    return;
                }

                //int maxBloomMip = Mathf.Clamp(m_lensFlareScreenSpace.bloomMip.value, 0, m_Bloom.maxIterations.value/2);

                using (new ProfilingScope(cmd, new ProfilingSampler("Post Process Screen Space Lens Flare")))
                {
                    //fetch current camera Color to copiedColor RT
                    CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    Blitter.BlitCameraTexture(cmd, cameraData.renderer.cameraColorTargetHandle, m_copiedColor);

                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    DoLensFlareScreenSpace(cameraData.camera, cmd, cameraData.renderer.cameraColorTargetHandle,
                        Shader.GetGlobalTexture("_Bloom_Texture"), Shader.GetGlobalTexture("_SourceTexLowMip"), desc);

                    UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, m_copiedColor);
                    ConfigureClear(ClearFlag.Color, Color.black);
                    DrawTriangle(cmd, m_lensFlareSSMaterial, m_writeToBloomPass);
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    Blitter.BlitCameraTexture(cmd, m_copiedColor, cameraData.renderer.cameraColorTargetHandle);
                    // cmd.SetGlobalTexture("_TestLensFlareMap", m_copiedColor);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }

            void DoLensFlareScreenSpace(Camera camera, CommandBuffer cmd, RenderTargetIdentifier source,
                Texture originalBloomTexture, Texture screenSpaceLensFlareBloomMipTexture,
                RenderTextureDescriptor m_Descriptor)
            {
                int ratio = (int)m_lensFlareAspScreenSpace.resolution.value;

                int width = Mathf.Max(1, (int)m_Descriptor.width / ratio);
                int height = Mathf.Max(1, (int)m_Descriptor.height / ratio);
                // var desc = GetCompatibleDescriptor(width, height, GraphicsFormat.R8G8B8A8_SRGB);
                var desc = m_Descriptor;
                desc.depthBufferBits = (int)DepthBits.None;
                desc.msaaSamples = 1;
                desc.width = width;
                desc.height = height;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
                if (m_lensFlareAspScreenSpace.IsStreaksActive())
                {
                    RenderingUtils.ReAllocateIfNeeded(ref m_StreakTmpTexture, desc, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: "_StreakTmpTexture");
                    RenderingUtils.ReAllocateIfNeeded(ref m_StreakTmpTexture2, desc, FilterMode.Bilinear,
                        TextureWrapMode.Clamp, name: "_StreakTmpTexture2");
                }

                RenderingUtils.ReAllocateIfNeeded(ref m_ScreenSpaceLensFlareResult, desc, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_ScreenSpaceLensFlareResult");

                DoLensFlareScreenSpaceCommon(
                    m_lensFlareSSMaterial,
                    camera,
                    (float)m_Descriptor.width,
                    (float)m_Descriptor.height,
                    m_lensFlareAspScreenSpace.tintColor.value,
                    originalBloomTexture,
                    screenSpaceLensFlareBloomMipTexture,
                    null, // We don't have any spectral LUT in URP
                    m_StreakTmpTexture,
                    m_StreakTmpTexture2,
                    new Vector4(
                        m_lensFlareAspScreenSpace.intensity.value,
                        m_lensFlareAspScreenSpace.firstFlareIntensity.value,
                        m_lensFlareAspScreenSpace.secondaryFlareIntensity.value,
                        m_lensFlareAspScreenSpace.warpedFlareIntensity.value),
                    new Vector4(
                        m_lensFlareAspScreenSpace.vignetteEffect.value,
                        m_lensFlareAspScreenSpace.startingPosition.value,
                        m_lensFlareAspScreenSpace.scale.value,
                        0), // Free slot, not used
                    new Vector4(
                        m_lensFlareAspScreenSpace.samples.value,
                        m_lensFlareAspScreenSpace.sampleDimmer.value,
                        m_lensFlareAspScreenSpace.chromaticAbberationIntensity.value,
                        0), // No need to pass a chromatic aberration sample count, hardcoded at 3 in shader
                    new Vector4(
                        m_lensFlareAspScreenSpace.streaksIntensity.value,
                        m_lensFlareAspScreenSpace.streaksLength.value,
                        m_lensFlareAspScreenSpace.streaksOrientation.value,
                        m_lensFlareAspScreenSpace.streaksThreshold.value),
                    new Vector4(
                        ratio,
                        m_lensFlareAspScreenSpace.warpedFlareScale.value.x,
                        m_lensFlareAspScreenSpace.warpedFlareScale.value.y,
                        0), // Free slot, not used
                    cmd,
                    m_ScreenSpaceLensFlareResult,
                    false);
                cmd.SetGlobalTexture(_Bloom_Texture_ID, originalBloomTexture);
            }

            static public void DoLensFlareScreenSpaceCommon(
                Material lensFlareShader,
                Camera cam,
                float actualWidth,
                float actualHeight,
                Color tintColor,
                Texture originalBloomTexture,
                Texture bloomMipTexture,
                Texture spectralLut,
                Texture streakTextureTmp,
                Texture streakTextureTmp2,
                Vector4 parameters1,
                Vector4 parameters2,
                Vector4 parameters3,
                Vector4 parameters4,
                Vector4 parameters5,
                UnityEngine.Rendering.CommandBuffer cmd,
                RTHandle result,
                bool debugView)
            {
                //Multiplying parameters value here for easier maintenance since they are the same numbers between SRPs 
                parameters2.x = Mathf.Pow(parameters2.x, 0.25f); // Vignette effect
                parameters3.z = parameters3.z / 20f; // chromaticAbberationIntensity
                parameters4.y = parameters4.y * 10f; // Streak Length                  
                parameters4.z = parameters4.z / 90f; // Streak Orientation
                parameters5.y = 1.0f / parameters5.y; // WarpedFlareScale X
                parameters5.z = 1.0f / parameters5.z; // WarpedFlareScale Y

                cmd.SetViewport(new Rect() { width = actualWidth, height = actualHeight });
                if (debugView)
                {
                    // Background pitch black to see only the flares
                    cmd.ClearRenderTarget(false, true, Color.black);
                }

#if UNITY_EDITOR
                if (cam.cameraType == CameraType.SceneView)
                {
                    // Determine whether the "Flare" checkbox is checked for the current view.
                    for (int i = 0;
                         i < UnityEditor.SceneView.sceneViews.Count;
                         i++) // Using a foreach on an ArrayList generates garbage ...
                    {
                        var sv = UnityEditor.SceneView.sceneViews[i] as UnityEditor.SceneView;
                        if (sv.camera == cam && !sv.sceneViewState.flaresEnabled)
                        {
                            return;
                        }
                    }
                }
#endif

                // Multiple scaleX by aspect ratio so that default 1:1 scale for warped flare stays circular (as in data driven lens flare)
                float warpedScaleX = parameters5.y;
                warpedScaleX *= actualWidth / actualHeight;
                parameters5.y = warpedScaleX;

                // This is to make sure the streak length is the same in all resolutions
                float streaksLength = parameters4.y;
                streaksLength *= actualWidth * 0.0005f;
                parameters4.y = streaksLength;

                // List of the passes in LensFlareScreenSpace.shader
                int prefilterPass = lensFlareShader.FindPass("LensFlareScreenSpac Prefilter");
                int downSamplePass = lensFlareShader.FindPass("LensFlareScreenSpace Downsample");
                int upSamplePass = lensFlareShader.FindPass("LensFlareScreenSpace Upsample");
                int compositionPass = lensFlareShader.FindPass("LensFlareScreenSpace Composition");
                int writeToBloomPass = lensFlareShader.FindPass("LensFlareScreenSpace Write to BloomTexture");

                // Setting the input textures
                cmd.SetGlobalTexture(_LensFlareScreenSpaceBloomMipTexture, bloomMipTexture);
                cmd.SetGlobalTexture(_LensFlareScreenSpaceSpectralLut, spectralLut);

                // Setting parameters of the effects
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams1, parameters1);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams2, parameters2);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams3, parameters3);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams4, parameters4);
                cmd.SetGlobalVector(_LensFlareScreenSpaceParams5, parameters5);
                cmd.SetGlobalColor(_LensFlareScreenSpaceTintColor, tintColor);

                // We only do the first 3 pass if StreakIntensity (parameters4.x) is set to something above 0 to save costs
                if (parameters4.x > 0)
                {
                    // Prefilter
                    UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, streakTextureTmp);
                    DrawTriangle(cmd, lensFlareShader, prefilterPass);

                    int maxLevel = Mathf.FloorToInt(Mathf.Log(Mathf.Max(actualHeight, actualWidth), 2.0f));
                    int maxLevelDownsample = Mathf.Max(1, maxLevel);
                    int maxLevelUpsample = 2;
                    int startIndex = 0;
                    bool even = false;

                    // Downsample
                    for (int i = 0; i < maxLevelDownsample; i++)
                    {
                        even = (i % 2 == 0);
                        cmd.SetGlobalInt(_LensFlareScreenSpaceMipLevel, i);
                        cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex,
                            even ? streakTextureTmp : streakTextureTmp2);
                        UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd,
                            even ? streakTextureTmp2 : streakTextureTmp);

                        DrawTriangle(cmd, lensFlareShader, downSamplePass);
                    }

                    //Since we do a ping pong between streakTextureTmp & streakTextureTmp2, we need to know which texture is the last;
                    if (even)
                        startIndex = 1;

                    //Upsample
                    for (int i = startIndex; i < (startIndex + maxLevelUpsample); i++)
                    {
                        even = (i % 2 == 0);
                        cmd.SetGlobalInt(_LensFlareScreenSpaceMipLevel, (i - startIndex));
                        cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex,
                            even ? streakTextureTmp : streakTextureTmp2);
                        UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd,
                            even ? streakTextureTmp2 : streakTextureTmp);

                        DrawTriangle(cmd, lensFlareShader, upSamplePass);
                    }

                    cmd.SetGlobalTexture(_LensFlareScreenSpaceStreakTex, even ? streakTextureTmp2 : streakTextureTmp);
                }

                // Composition (Flares + Streaks)
                UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, result);
                DrawTriangle(cmd, lensFlareShader, compositionPass);

                // Final pass, we add the result of the previous pass to the Original Bloom Texture.
                cmd.SetGlobalTexture(_LensFlareScreenSpaceResultTexture, result);
                //UnityEngine.Rendering.CoreUtils.SetRenderTarget(cmd, originalBloomTexture);
                //DrawQuad(cmd, lensFlareShader, writeToBloomPass);
            }
        }
    }
#endif

    #endregion
}