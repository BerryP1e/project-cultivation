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
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace ASP
{
    public static class ASPMainLightShadowConstantBuffer
    {
        public static int _WorldToShadow;
        public static int _ShadowParams;
        public static int _CascadeCount;
        public static int _CascadeShadowSplitSpheres0;
        public static int _CascadeShadowSplitSpheres1;
        public static int _CascadeShadowSplitSpheres2;
        public static int _CascadeShadowSplitSpheres3;
        public static int _CascadeShadowSplitSphereRadii;
        public static int _ShadowOffset0;
        public static int _ShadowOffset1;
        public static int _ShadowmapSize;
    }

    #region UNITY2021

#if UNITY_2021
    public class ASPShadowMapFeature : ScriptableRendererFeature
    {

        [Tooltip("Expensive, but can prevent shadow missing when object outside camera view")]
        private bool _performExtraCull = true;
        public RenderQueueRange RenderQueueRange = RenderQueueRange.all;
        private LayerMask _layerMask = -1;
        private string _customBufferName = "_ASPShadowMap";
        [FormerlySerializedAs("m_characterShadowMapResolution")] [SerializeField]
        private CharacterShadowMapResolution _characterShadowMapResolution = CharacterShadowMapResolution.SIZE_2048;
        
        public float ClipDistance = 50;
        [Range(1,4)]
        public int CascadeCount = 1;
        /// Main light last cascade shadow fade border.
        /// Value represents the width of shadow fade that ranges from 0 to 1.
        /// Where value 0 is used for no shadow fade.
        ///
        [FormerlySerializedAs("LastBorder")]
        [Range(0, 1)]
        [Tooltip("Shadow fade out ratio on last cascade, set to 0 means no fading")]
        public float ShadowFadeRatio = 0.2f;
        public bool UseScreenSpaceShadowPass = false;
        public Color ScreenSpaceShadowColor = new Color(0, 0, 0, 0.3f);
        
        //[RenderingLayerMask]
        private int _renderingLayerMask = -1;
        private ASPShadowRenderPass _scriptablePass;
        private ASPShadowData _shadowData;

        private Matrix4x4[] _customWorldToShadowMatrices;
        private List<Plane[]> _cascadeCullPlanes;
        private Matrix4x4[] _lightViewMatrices;
        private Matrix4x4[] _lightProjectionMatrices;
        private ShadowSliceData[] _shadowSliceDatas;
        private ScriptableCullingParameters _cullingParameters;
        private Vector4[] _cascadeSplitDistances;
        private Light _mainShadowLight;
        private static bool HasRenderPassEnqueued;
        
        private Material _screenSpaceShadowMapMat;
        private ASPFullScreenRenderPass _fullScreenPass;
        private void ClearData()
        {
            _customWorldToShadowMatrices = new Matrix4x4[4 + 1];
            _cullingParameters = new ScriptableCullingParameters();
            _cascadeSplitDistances = new Vector4[4];
        
            _lightViewMatrices = new Matrix4x4[4];
            for (int i = 0; i < 4; i++)
            {
                _lightViewMatrices[i] = Matrix4x4.identity;
            }

            _lightProjectionMatrices = new Matrix4x4[4];
            for (int i = 0; i < 4; i++)
            {
                _lightProjectionMatrices[i] = Matrix4x4.identity;
            }

            _shadowSliceDatas = new ShadowSliceData[4];

            _cascadeCullPlanes = new List<Plane[]>();
            for (int i = 0; i < 4; i++)
            {
                _cascadeCullPlanes.Add(new Plane[6]);
            }
        }
        
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var maxLightIntensity = -10000.0f;
            Light selectedLight = null;
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                {
                    if (light.intensity > maxLightIntensity)
                    {
                        selectedLight = light;
                        maxLightIntensity = light.intensity;
                    }
                }
            }

            if (selectedLight != null)
            {
                _mainShadowLight = selectedLight;
            }
        }
        
        /// <inheritdoc/>
        public override void Create()
        {
#if UNITY_EDITOR
            EditorUtil.AddAlwaysIncludedShader("Hidden/ASP/ScreenSpaceShadows");
#endif
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var maxLightIntensity = -10000.0f;
            Light selectedLight = null;
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                {
                    if (light.intensity > maxLightIntensity)
                    {
                        selectedLight = light;
                        maxLightIntensity = light.intensity;
                    }
                }
            }
            _mainShadowLight = selectedLight;
        
            ClearData();
            _scriptablePass = new ASPShadowRenderPass((uint)_renderingLayerMask, _customBufferName,
                RenderPassEvent.AfterRenderingShadows, RenderQueueRange, _layerMask, _shadowData);
            if (!isActive)
            {
                _scriptablePass.IsNotActive = true;
                _scriptablePass.DrawEmptyShadowMap();
            }
            else
            {
                _scriptablePass.IsNotActive = false;
                Shader.SetGlobalInt("_ASPShadowMapValid", 0);
            }

            if (!HasRenderPassEnqueued)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
                HasRenderPassEnqueued = true;
            }
            
            _fullScreenPass = new ASPFullScreenRenderPass(name);
        }

        protected override void Dispose(bool disposing)
        {
            if(HasRenderPassEnqueued)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
                HasRenderPassEnqueued = false;
            }
            _scriptablePass.Dispose();
            _fullScreenPass.Dispose();
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (UseScreenSpaceShadowPass)
            {
                if ((renderingData.cameraData.targetTexture != null && renderingData.cameraData.targetTexture.format == RenderTextureFormat.Depth) ||
                    renderingData.cameraData.cameraType == CameraType.Preview ||
                    renderingData.cameraData.cameraType == CameraType.Reflection)
                {
                    return;
                }

  
                _fullScreenPass.renderPassEvent = (RenderPassEvent)RenderPassEvent.AfterRenderingTransparents;
                _fullScreenPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                if (_screenSpaceShadowMapMat == null)
                {
                    _screenSpaceShadowMapMat = new Material(Shader.Find("Hidden/ASP/ScreenSpaceShadows"));
                    _screenSpaceShadowMapMat.hideFlags = HideFlags.DontSave;
                }
                _screenSpaceShadowMapMat.SetColor("_BaseColor", ScreenSpaceShadowColor);
                _fullScreenPass.SetupMembers(_screenSpaceShadowMapMat, 0, false, true);
                renderer.EnqueuePass(_fullScreenPass);
            }
        }

        private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (_mainShadowLight == null || _mainShadowLight.lightmapBakeType == LightmapBakeType.Baked)
                return;
#endif
            var camera = Camera.main;
            if (camera == null || _mainShadowLight == null)
            {
                Shader.SetGlobalInt("_ASPShadowMapValid", 0);
                return;
            }

            _shadowData = ASPShadowUtil.SetupCascadesData((int)_characterShadowMapResolution, CascadeCount,
                ClipDistance, ShadowFadeRatio);

            var renderTargetWidth = _shadowData.mainLightShadowmapWidth;
            var renderTargetHeight = (_shadowData.mainLightShadowCascadesCount == 2)
                ? _shadowData.mainLightShadowmapHeight >> 1
                : _shadowData.mainLightShadowmapHeight;
            var shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                _shadowData.mainLightShadowmapWidth,
                _shadowData.mainLightShadowmapHeight, _shadowData.mainLightShadowCascadesCount);
            var isEmptyShadowMap = (_mainShadowLight.type != LightType.Directional ||
                                    _mainShadowLight.shadows == LightShadows.None);

            var cullingParameters = new ScriptableCullingParameters();
            camera.TryGetCullingParameters(out cullingParameters);
            cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
            cullingParameters.isOrthographic = true;

            for (int i = 0; i < _shadowData.mainLightShadowCascadesCount; i++)
            {
                _shadowSliceDatas[i].splitData.shadowCascadeBlendCullingFactor = 1.0f;

                var planes = _cascadeCullPlanes[i];
                bool success = ASPShadowUtil.ComputeDirectionalShadowMatricesAndCullingSphere(camera, ref _shadowData,
                    i, _mainShadowLight, shadowResolution, _shadowData.cascadeSplitArray, out Vector4 cullingSphere, out
                    Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref planes, out float zDistance);
                _lightViewMatrices[i] = viewMatrix;
                _lightProjectionMatrices[i] = projMatrix;
                _cascadeCullPlanes[i] = planes;
                _cascadeSplitDistances[i] = cullingSphere;
                if (!success)
                {
                    isEmptyShadowMap = true;
                }

                _customWorldToShadowMatrices[i] =
                    ASPShadowUtil.GetShadowTransform(_lightProjectionMatrices[i],
                        _lightViewMatrices[i]);

                // Handle shadow slices
                var offsetX = (i % 2) * shadowResolution;
                var offsetY = (i / 2) * shadowResolution;

                ASPShadowUtil.ApplySliceTransform(ref _customWorldToShadowMatrices[i], offsetX, offsetY,
                    shadowResolution,
                    renderTargetWidth, renderTargetHeight);
                _shadowSliceDatas[i].projectionMatrix = _lightProjectionMatrices[i];
                _shadowSliceDatas[i].viewMatrix = _lightViewMatrices[i];
                _shadowSliceDatas[i].offsetX = offsetX;
                _shadowSliceDatas[i].offsetY = offsetY;
                _shadowSliceDatas[i].resolution = shadowResolution;
                _shadowSliceDatas[i].shadowTransform =
                    _customWorldToShadowMatrices[i];
                _shadowSliceDatas[i].splitData.shadowCascadeBlendCullingFactor = 1.0f;
            }
            
            var cullResults = new CullingResults[_shadowData.mainLightShadowCascadesCount];
            for (int i = 0; i < _shadowData.mainLightShadowCascadesCount; i++)
            {
                cullingParameters.cullingMatrix = _lightProjectionMatrices[i] * _lightViewMatrices[i];
                for (int cullPlaneIndex = 0; cullPlaneIndex < 6; cullPlaneIndex++)
                {
                    cullingParameters.SetCullingPlane(cullPlaneIndex, _cascadeCullPlanes[i][cullPlaneIndex]);
                }
                cullResults[i] = context.Cull(ref cullingParameters);
            }
            _scriptablePass.CullResults = cullResults;
            _scriptablePass.UseInjectCullResult = true;
            _scriptablePass._IsEmptyShdaowMap = isEmptyShadowMap;
            _scriptablePass._aspShadowData = _shadowData;
            _scriptablePass._customWorldToShadowMatrices = _customWorldToShadowMatrices;
            _scriptablePass._cascadeCullPlanes = _cascadeCullPlanes;
            _scriptablePass._lightViewMatrices = _lightViewMatrices;
            _scriptablePass._lightProjectionMatrices = _lightProjectionMatrices;
            _scriptablePass._cascadeSplitDistances = _cascadeSplitDistances;
            _scriptablePass._shadowSliceDatas = _shadowSliceDatas;
            pipeline.scriptableRenderer.EnqueuePass(_scriptablePass);
        }

        class ASPShadowRenderPass : ScriptableRenderPass
        {
            public CullingResults[] CullResults;
            public bool UseInjectCullResult;
            public bool PerformExtraCull;
            public bool IsNotActive;
            private RenderTexture _shadowMapTexture;
            private RenderTexture _emptyLightShadowmapTexture;

            private FilteringSettings _filteringSettings;
            private RenderStateBlock _renderStateBlock;
            private ShaderTagId _shaderTagId = new ShaderTagId("ASPShadowCaster");
            private string _customBufferName;

            public ASPShadowData _aspShadowData;
            public bool _IsEmptyShdaowMap;

            //Shadow map-related data
            public Matrix4x4[] _customWorldToShadowMatrices;
            public List<Plane[]> _cascadeCullPlanes;
            public Matrix4x4[] _lightViewMatrices;
            public Matrix4x4[] _lightProjectionMatrices;
            public ShadowSliceData[] _shadowSliceDatas;
            public Vector4[] _cascadeSplitDistances;
            private ScriptableCullingParameters _cullingParameters = new ScriptableCullingParameters();
            private const int k_ShadowmapBufferBits = 16;
            private void ClearData()
            {
                _customWorldToShadowMatrices = new Matrix4x4[4 + 1];
                for (int i = 0; i < _customWorldToShadowMatrices.Length; i++)
                {
                    _customWorldToShadowMatrices[i] = Matrix4x4.identity;
                }

                _lightViewMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _lightViewMatrices[i] = Matrix4x4.identity;
                }

                _lightProjectionMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _lightProjectionMatrices[i] = Matrix4x4.identity;
                }

                _shadowSliceDatas = new ShadowSliceData[4];
                
                _cascadeCullPlanes = new List<Plane[]>();
                for (int i = 0; i < 4; i++)
                {
                    _cascadeCullPlanes.Add(new Plane[6]);
                }
            }

            public void DrawEmptyShadowMap()
            {
                if (_emptyLightShadowmapTexture == null)
                {
                    _emptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
                }
                Shader.SetGlobalInt("_ASPShadowMapValid", 0);
                Shader.SetGlobalTexture(_customBufferName, _emptyLightShadowmapTexture);
            }

            void SetupForEmptyRendering()
            {
                _IsEmptyShdaowMap = true;
                _emptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
            }

            public ASPShadowRenderPass(uint renderingLayerMask, string customBufferName,
                RenderPassEvent passEvent, RenderQueueRange queueRange, LayerMask layerMask, ASPShadowData ShadowData)
            {
                profilingSampler = new ProfilingSampler("ASP Shadow Render Pass");
                ClearData();

                renderPassEvent = passEvent;
                _customBufferName = customBufferName;
                _filteringSettings = new FilteringSettings(RenderQueueRange.all);
                _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                _aspShadowData = ShadowData;
                _cascadeSplitDistances = new Vector4[4];
                ASPMainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_ASPMainLightWorldToShadow");
                ASPMainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_ASPMainLightShadowParams");
                ASPMainLightShadowConstantBuffer._CascadeCount = Shader.PropertyToID("_ASPCascadeCount");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 =
 Shader.PropertyToID("_ASPCascadeShadowSplitSpheres0");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 =
 Shader.PropertyToID("_ASPCascadeShadowSplitSpheres1");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 =
 Shader.PropertyToID("_ASPCascadeShadowSplitSpheres2");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 =
 Shader.PropertyToID("_ASPCascadeShadowSplitSpheres3");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii =
 Shader.PropertyToID("_ASPCascadeShadowSplitSphereRadii");
                ASPMainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_ASPMainLightShadowOffset0");
                ASPMainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_ASPMainLightShadowOffset1");
                ASPMainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_ASPMainLightShadowmapSize");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderTargetWidth = _aspShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (_aspShadowData.mainLightShadowCascadesCount == 2)
                    ? _aspShadowData.mainLightShadowmapHeight >> 1
                    : _aspShadowData.mainLightShadowmapHeight;
                
                int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
                    _aspShadowData.mainLightShadowmapWidth,
                    _aspShadowData.mainLightShadowmapHeight, _aspShadowData.mainLightShadowCascadesCount);
                
                int shadowLightIndex = renderingData.lightData.mainLightIndex;

                if (IsNotActive)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!renderingData.shadowData.supportsMainLightShadows)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (shadowLightIndex == -1)
                {
                    SetupForEmptyRendering();
                    return;
                }
                
                VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
                Light light = shadowLight.light;
                if (shadowLight.lightType != LightType.Directional)
                {
                    SetupForEmptyRendering();
                     return;
                }

                if (light.shadows == LightShadows.None)
                {
                    SetupForEmptyRendering();
                    return;
                }

                if (!_IsEmptyShdaowMap)
                {
                    for (int cascadeIndex = 0; cascadeIndex < _aspShadowData.mainLightShadowCascadesCount; ++cascadeIndex)
                    {
                        _shadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                        var planes = _cascadeCullPlanes[cascadeIndex];
                        
                        var success =
 ASPShadowUtil.ComputeDirectionalShadowMatricesAndCullingSphere(ref renderingData.cameraData, ref _aspShadowData, 
                            cascadeIndex, shadowLight.light, shadowResolution, _aspShadowData.cascadeSplitArray, out Vector4 cullingSphere, out 
                            Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref planes, out float zDistance);
                        
                        _lightViewMatrices[cascadeIndex] = viewMatrix;
                        _lightProjectionMatrices[cascadeIndex] = projMatrix;
                        _cascadeCullPlanes[cascadeIndex] = planes;
                        _cascadeSplitDistances[cascadeIndex] = cullingSphere;
                        
                        if (!success)
                        {
                            SetupForEmptyRendering();
                            ConfigureTarget(_emptyLightShadowmapTexture);
                            ConfigureClear(ClearFlag.Depth, Color.black);
                            return;
                        }

                        _customWorldToShadowMatrices[cascadeIndex] =
                            ASPShadowUtil.GetShadowTransform(_lightProjectionMatrices[cascadeIndex],
                                _lightViewMatrices[cascadeIndex]);

                        // Handle shadow slices
                        var offsetX = (cascadeIndex % 2) * shadowResolution;
                        var offsetY = (cascadeIndex / 2) * shadowResolution;

                        ASPShadowUtil.ApplySliceTransform(ref _customWorldToShadowMatrices[cascadeIndex], offsetX, offsetY,
                            shadowResolution,
                            renderTargetWidth, renderTargetHeight);
                        _shadowSliceDatas[cascadeIndex].projectionMatrix = _lightProjectionMatrices[cascadeIndex];
                        _shadowSliceDatas[cascadeIndex].viewMatrix = _lightViewMatrices[cascadeIndex];
                        _shadowSliceDatas[cascadeIndex].offsetX = offsetX;
                        _shadowSliceDatas[cascadeIndex].offsetY = offsetY;
                        _shadowSliceDatas[cascadeIndex].resolution = shadowResolution;
                        _shadowSliceDatas[cascadeIndex].shadowTransform =
                            _customWorldToShadowMatrices[cascadeIndex];
                        _shadowSliceDatas[cascadeIndex].splitData.shadowCascadeBlendCullingFactor = 1.0f;
                    }
                    _shadowMapTexture = ShadowUtils.GetTemporaryShadowTexture(renderTargetWidth, renderTargetHeight,
                        k_ShadowmapBufferBits);
                    ConfigureTarget(_shadowMapTexture);
                    ConfigureClear(ClearFlag.Depth, Color.black);
                }
                else
                {
                    _emptyLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(1, 1, k_ShadowmapBufferBits);
                    ConfigureTarget(_emptyLightShadowmapTexture);
                    ConfigureClear(ClearFlag.Depth, Color.black);
                }
            }
            
            private void RenderEmpty(ScriptableRenderContext context)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.SetGlobalInt("_ASPShadowMapValid", 0);
                cmd.SetGlobalTexture(_customBufferName, _emptyLightShadowmapTexture);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_IsEmptyShdaowMap)
                {
                    _IsEmptyShdaowMap = false;
                    RenderEmpty(context);
                    return;
                }

                if (_shadowMapTexture == null)
                {
                    _IsEmptyShdaowMap = false;
                    SetupForEmptyRendering();
                    RenderEmpty(context);
                    return;
                }

                
                CommandBuffer cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var prevViewMatrix = renderingData.cameraData.camera.worldToCameraMatrix;
                var prevProjMatrix = renderingData.cameraData.camera.projectionMatrix;
                var visibleLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];
                using (new ProfilingScope(cmd, new ProfilingSampler("ASP ShadowMap Pass")))
                {
                    for (int i = 0; i < _aspShadowData.mainLightShadowCascadesCount; i++)
                    {
                        // Handle drawing 
                        var drawSettings =
                            CreateDrawingSettings(_shaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                        drawSettings.perObjectData = PerObjectData.None;
                        
                        // Need to start by setting the Camera position as that is not set for passes executed before normal rendering
                       //  cmd.SetGlobalVector("_WorldSpaceCameraPos", renderingData.cameraData.worldSpaceCameraPos);

                        // TODO handle empty rendering
                        cmd.SetGlobalDepthBias(1.0f, 3.5f); 
                        cmd.SetViewport(new Rect(_shadowSliceDatas[i].offsetX, _shadowSliceDatas[i].offsetY,
                            _shadowSliceDatas[i].resolution, _shadowSliceDatas[i].resolution));
                        cmd.SetViewProjectionMatrices(_lightViewMatrices[i], _lightProjectionMatrices[i]);
                        Vector4 shadowBias = ShadowUtils.GetShadowBias(ref visibleLight,
                            renderingData.lightData.mainLightIndex, ref renderingData.shadowData,
                            _lightProjectionMatrices[i], _shadowSliceDatas[i].resolution);
                        // ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref visibleLight, shadowBias);
                        cmd.SetGlobalVector("_ASPShadowBias", shadowBias);

                        // Light direction is currently used in shadow caster pass to apply shadow normal offset (normal bias).
                        Vector3 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                        cmd.SetGlobalVector("_ASPLightDirection",
                            new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

                        // For punctual lights, computing light direction at each vertex position provides more consistent results (shadow shape does not change when "rotating the point light" for example)
                        Vector3 lightPosition = visibleLight.localToWorldMatrix.GetColumn(3);
                        cmd.SetGlobalVector("_ASPLightPosition",
                            new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        context.DrawRenderers(UseInjectCullResult ? CullResults[i] : renderingData.cullResults, ref drawSettings, ref _filteringSettings,
                            ref _renderStateBlock);
                        cmd.DisableScissorRect();
                        cmd.SetGlobalDepthBias(0.0f, 0.0f); 
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                }
                
                cmd.SetViewProjectionMatrices(prevViewMatrix, prevProjMatrix);
                cmd.SetGlobalInt("_ASPShadowMapValid", 1);
                cmd.SetGlobalTexture(_customBufferName, _shadowMapTexture);
                ASPShadowUtil.SetupASPMainLightShadowReceiverConstants(cmd, ref visibleLight, ref renderingData, _aspShadowData, _customWorldToShadowMatrices, _cascadeSplitDistances);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                if (_emptyLightShadowmapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_emptyLightShadowmapTexture);
                    _emptyLightShadowmapTexture = null;
                }

                if (_shadowMapTexture != null)
                {
                    RenderTexture.ReleaseTemporary(_shadowMapTexture);
                    _shadowMapTexture = null;
                }
            }

            public void Dispose()
            {
                Shader.SetGlobalInt("_ASPShadowMapValid", 0);
            }
        }
    }
#endif

    #endregion

    #region UNITY2022

#if UNITY_2022_1_OR_NEWER
    [DisallowMultipleRendererFeature("ASP ShadowMap")]
    public class ASPShadowMapFeature : ScriptableRendererFeature
    {
        [Tooltip("Expensive, but can prevent shadow missing when object outside camera view")]
        private bool PerformExtraCull = true;

        public RenderQueueRange m_renderQueueRange = RenderQueueRange.all;
        private LayerMask m_layerMask = -1;
        private string m_CustomBufferName = "_ASPShadowMap";
        static readonly int s_aspShadowMapId = Shader.PropertyToID("_ASPShadowMap");

        [SerializeField]
        private CharacterShadowMapResolution m_characterShadowMapResolution = CharacterShadowMapResolution.SIZE_2048;

        [FormerlySerializedAs("ShadowDistance")]
        public float ClipDistance = 50;

        [Range(1, 4)] public int CascadeCount = 1;

        /// Main light last cascade shadow fade border.
        /// Value represents the width of shadow fade that ranges from 0 to 1.
        /// Where value 0 is used for no shadow fade.
        ///
        [FormerlySerializedAs("LastBorder")]
        [Range(0, 1)]
        [Tooltip("Shadow fade out ratio on last cascade, set to 0 means no fading")]
        public float ShadowFadeRatio = 0.2f;

        public bool UseScreenSpaceShadowPass = false;

        public Color ScreenSpaceShadowColor = new Color(0, 0, 0, 0.3f);
        //[RenderingLayerMask]
        private int m_renderingLayerMask = -1;
        private ASPShadowRenderPass _scriptablePass;
        private ASPShadowData _shadowData;

        private Matrix4x4[] _customWorldToShadowMatrices;
        private List<Plane[]> _cascadeCullPlanes;
        private Matrix4x4[] _lightViewMatrices;
        private Matrix4x4[] _lightProjectionMatrices;
        private ShadowSliceData[] _shadowSliceDatas;
        private ScriptableCullingParameters _cullingParameters;
        private Vector4[] _cascadeSplitDistances;
        private Light _mainShadowLight;
        private static bool HasRenderPassEnqueued;
        private Material _screenSpaceShadowMapMat;
        
        private ASPFullScreenRenderPass _fullScreenPass;
        
        private void ClearData()
        {
            _customWorldToShadowMatrices = new Matrix4x4[4 + 1];
            _cullingParameters = new ScriptableCullingParameters();
            _cascadeSplitDistances = new Vector4[4];
        
            _lightViewMatrices = new Matrix4x4[4];
            for (int i = 0; i < 4; i++)
            {
                _lightViewMatrices[i] = Matrix4x4.identity;
            }

            _lightProjectionMatrices = new Matrix4x4[4];
            for (int i = 0; i < 4; i++)
            {
                _lightProjectionMatrices[i] = Matrix4x4.identity;
            }

            _shadowSliceDatas = new ShadowSliceData[4];

            _cascadeCullPlanes = new List<Plane[]>();
            for (int i = 0; i < 4; i++)
            {
                _cascadeCullPlanes.Add(new Plane[6]);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var maxLightIntensity = -10000.0f;
            Light selectedLight = null;
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                {
                    if (light.intensity > maxLightIntensity)
                    {
                        selectedLight = light;
                        maxLightIntensity = light.intensity;
                    }
                }
            }

            if (selectedLight != null)
            {
                _mainShadowLight = selectedLight;
            }
        }

        /// <inheritdoc/>
        public override void Create()
        {
#if UNITY_EDITOR
            EditorUtil.AddAlwaysIncludedShader("Hidden/ASP/ScreenSpaceShadows");
#endif
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var maxLightIntensity = -10000.0f;
            Light selectedLight = null;
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional && light.shadows != LightShadows.None)
                {
                    if (light.intensity > maxLightIntensity)
                    {
                        selectedLight = light;
                        maxLightIntensity = light.intensity;
                    }
                }
            }
            _mainShadowLight = selectedLight;
        
            ClearData();
            _scriptablePass = new ASPShadowRenderPass((uint)m_renderingLayerMask, m_CustomBufferName,
                RenderPassEvent.AfterRenderingShadows, m_renderQueueRange, m_layerMask, _shadowData);
            
            if (!isActive)
            {
                _scriptablePass.IsNotActive = true;
                _scriptablePass.DrawEmptyShadowMap();
            }
            else
            {
                _scriptablePass.IsNotActive = false;
                Shader.SetGlobalInt("_ASPShadowMapValid", 0);
            }

            if (!HasRenderPassEnqueued)
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
                HasRenderPassEnqueued = true;
            }
            
            _fullScreenPass = new ASPFullScreenRenderPass(name);

        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (UseScreenSpaceShadowPass)
            {
#if UNITY_6000_0_OR_NEWER
                if (UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData) ||
                    renderingData.cameraData.cameraType == CameraType.Preview ||
                    renderingData.cameraData.cameraType == CameraType.Reflection)
                {
                    return;
                }
#else
                if ((renderingData.cameraData.targetTexture != null && renderingData.cameraData.targetTexture.format == RenderTextureFormat.Depth) ||
                    renderingData.cameraData.cameraType == CameraType.Preview ||
                    renderingData.cameraData.cameraType == CameraType.Reflection)
                {
                    return;
                }
#endif

  
                _fullScreenPass.renderPassEvent = (RenderPassEvent)RenderPassEvent.AfterRenderingOpaques;
                _fullScreenPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                if (_screenSpaceShadowMapMat == null)
                {
                    _screenSpaceShadowMapMat = new Material(Shader.Find("Hidden/ASP/ScreenSpaceShadows"));
                    _screenSpaceShadowMapMat.hideFlags = HideFlags.DontSave;
                }
                _screenSpaceShadowMapMat.SetColor("_BaseColor", ScreenSpaceShadowColor);
                _fullScreenPass.SetupMembers(_screenSpaceShadowMapMat, 0, false, true);
                renderer.EnqueuePass(_fullScreenPass);
            }

        }

        private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
           var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
           if (pipeline == null)
           {
               return;
           }
#if UNITY_EDITOR
            if (_mainShadowLight == null || _mainShadowLight.lightmapBakeType == LightmapBakeType.Baked)
            {
                return;
            }

#endif
        var camera = Camera.main;
        if (camera == null || _mainShadowLight == null)
        {
            Shader.SetGlobalInt("_ASPShadowMapValid", 0);
            return;
        }
        _shadowData = ASPShadowUtil.SetupCascadesData((int)m_characterShadowMapResolution, CascadeCount,
            ClipDistance, ShadowFadeRatio);

        var renderTargetWidth = _shadowData.mainLightShadowmapWidth;
        var renderTargetHeight = (_shadowData.mainLightShadowCascadesCount == 2)
            ? _shadowData.mainLightShadowmapHeight >> 1
            : _shadowData.mainLightShadowmapHeight;
        var shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(
            _shadowData.mainLightShadowmapWidth,
            _shadowData.mainLightShadowmapHeight, _shadowData.mainLightShadowCascadesCount);
        var isEmptyShadowMap = (_mainShadowLight.type != LightType.Directional ||
                            _mainShadowLight.shadows == LightShadows.None);
        
        var cullingParameters = new ScriptableCullingParameters();
        camera.TryGetCullingParameters(out cullingParameters);
        cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
        cullingParameters.isOrthographic = true;
        
        for (int i = 0; i < _shadowData.mainLightShadowCascadesCount; i++)
        {
            _shadowSliceDatas[i].splitData.shadowCascadeBlendCullingFactor = 1.0f;
            
            var planes = _cascadeCullPlanes[i];
            bool success = ASPShadowUtil.ComputeDirectionalShadowMatricesAndCullingSphere(camera, ref _shadowData, 
                i, _mainShadowLight, shadowResolution, _shadowData.cascadeSplitArray, out Vector4 cullingSphere, out 
                Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, ref planes, out float zDistance);
            _lightViewMatrices[i] = viewMatrix;
            _lightProjectionMatrices[i] = projMatrix;
            _cascadeCullPlanes[i] = planes;
            _cascadeSplitDistances[i] = cullingSphere;
            if (!success)
            {
                isEmptyShadowMap = true;
            }
            
            _customWorldToShadowMatrices[i] =
                ASPShadowUtil.GetShadowTransform(_lightProjectionMatrices[i],
                    _lightViewMatrices[i]);

            // Handle shadow slices
            var offsetX = (i % 2) * shadowResolution;
            var offsetY = (i / 2) * shadowResolution;

            ASPShadowUtil.ApplySliceTransform(ref _customWorldToShadowMatrices[i], offsetX, offsetY,
                shadowResolution,
                renderTargetWidth, renderTargetHeight);
            _shadowSliceDatas[i].projectionMatrix = _lightProjectionMatrices[i];
            _shadowSliceDatas[i].viewMatrix = _lightViewMatrices[i];
            _shadowSliceDatas[i].offsetX = offsetX;
            _shadowSliceDatas[i].offsetY = offsetY;
            _shadowSliceDatas[i].resolution = shadowResolution;
            _shadowSliceDatas[i].shadowTransform =
                _customWorldToShadowMatrices[i];
            _shadowSliceDatas[i].splitData.shadowCascadeBlendCullingFactor = 1.0f;
        }

        var cullResults = new CullingResults[_shadowData.mainLightShadowCascadesCount];
        for (int i = 0; i < _shadowData.mainLightShadowCascadesCount; i++)
        {
            cullingParameters.cullingMatrix = _lightProjectionMatrices[i] * _lightViewMatrices[i];
            for (int cullPlaneIndex = 0; cullPlaneIndex < 6; cullPlaneIndex++)
            {
                cullingParameters.SetCullingPlane(cullPlaneIndex, _cascadeCullPlanes[i][cullPlaneIndex]);
            }
            cullResults[i] = context.Cull(ref cullingParameters);
        }
        _scriptablePass.CullResults = cullResults;
        _scriptablePass.UseInjectCullResult = true;
        _scriptablePass._IsEmptyShdaowMap = isEmptyShadowMap;
        _scriptablePass._aspShadowData = _shadowData;
        _scriptablePass._customWorldToShadowMatrices = _customWorldToShadowMatrices;
        _scriptablePass._cascadeCullPlanes = _cascadeCullPlanes;
        _scriptablePass._lightViewMatrices = _lightViewMatrices;
        _scriptablePass._lightProjectionMatrices = _lightProjectionMatrices;
        _scriptablePass._cascadeSplitDistances = _cascadeSplitDistances;
        _scriptablePass._shadowSliceDatas = _shadowSliceDatas;
        pipeline.scriptableRenderer.EnqueuePass(_scriptablePass);
    }

        protected override void Dispose(bool disposing)
        {
            if(HasRenderPassEnqueued)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
                HasRenderPassEnqueued = false;
            }
            _scriptablePass.Dispose();
            _fullScreenPass.Dispose();
        }
        
        public class ASPShadowRenderPass : ScriptableRenderPass
        {
            public CullingResults[] CullResults;
            public bool UseInjectCullResult;
            public bool PerformExtraCull;
            public bool IsNotActive;
            private RTHandle _shadowMapTexture;
            private RTHandle _emptyLightShadowmapTexture;
            private FilteringSettings _filteringSettings;
            private RenderStateBlock _renderStateBlock;
            private ShaderTagId _shaderTagId = new ShaderTagId("ASPShadowCaster");
            private string _customBufferName;
            private ScriptableCullingParameters _cullingParameters = new ScriptableCullingParameters();

            public ASPShadowData _aspShadowData;
            public bool _IsEmptyShdaowMap;

            //Shadow map-related data
            public Matrix4x4[] _customWorldToShadowMatrices;
            public List<Plane[]> _cascadeCullPlanes;
            public Matrix4x4[] _lightViewMatrices;
            public Matrix4x4[] _lightProjectionMatrices;
            public ShadowSliceData[] _shadowSliceDatas;
            public Vector4[] _cascadeSplitDistances;
            
            private void SetupASPMainLightShadowForLegacy()
            {
                var renderTargetWidth = _aspShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (_aspShadowData.mainLightShadowCascadesCount == 2)
                    ? _aspShadowData.mainLightShadowmapHeight >> 1
                    : _aspShadowData.mainLightShadowmapHeight;
                if (IsNotActive)
                {
                    SetupForEmptyRendering();
                    return;
                }
                if (!_IsEmptyShdaowMap)
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _shadowMapTexture, renderTargetWidth, renderTargetHeight,
                        16, name: "_CustomMainLightShadowmapTexture");    
                    ConfigureTarget(_shadowMapTexture);
                    ConfigureClear(ClearFlag.All, Color.black);
                }
                else
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                        name: "_ASPEmptyLightShadowmapTexture");
                    ConfigureTarget(_emptyLightShadowmapTexture);
                    ConfigureClear(ClearFlag.All, Color.black);
                }
            }
            
            private void ClearData()
            {
                _cullingParameters = new ScriptableCullingParameters();
                _cascadeSplitDistances = new Vector4[4];
                _customWorldToShadowMatrices = new Matrix4x4[4 + 1];
                for (int i = 0; i < _customWorldToShadowMatrices.Length; i++)
                {
                    _customWorldToShadowMatrices[i] = Matrix4x4.identity;
                }

                _lightViewMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _lightViewMatrices[i] = Matrix4x4.identity;
                }

                _lightProjectionMatrices = new Matrix4x4[4];
                for (int i = 0; i < 4; i++)
                {
                    _lightProjectionMatrices[i] = Matrix4x4.identity;
                }

                _shadowSliceDatas = new ShadowSliceData[4];

                _cascadeCullPlanes = new List<Plane[]>();
                for (int i = 0; i < 4; i++)
                {
                    _cascadeCullPlanes.Add(new Plane[6]);
                }
            }

            public void DrawEmptyShadowMap()
            {
                if (_emptyLightShadowmapTexture == null)
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                        name: "_ASPEmptyLightShadowmapTexture");
                }

                Shader.SetGlobalTexture(_customBufferName, _emptyLightShadowmapTexture);
            }

            void SetupForEmptyRendering()
            {
                _IsEmptyShdaowMap = true;
                ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                    name: "_ASPEmptyLightShadowmapTexture");
            }

            public ASPShadowRenderPass(uint renderingLayerMask, string customBufferName,
                RenderPassEvent passEvent, RenderQueueRange queueRange, LayerMask layerMask, ASPShadowData shadowData)
            {
                profilingSampler = new ProfilingSampler("ASP Shadow Render Pass");
                ClearData();
                _customBufferName = customBufferName;
                renderPassEvent = passEvent;
                _filteringSettings = new FilteringSettings(RenderQueueRange.all);
                _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
                _aspShadowData = shadowData;
                _cascadeSplitDistances = new Vector4[4];
                ASPMainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_ASPMainLightWorldToShadow");
                ASPMainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_ASPMainLightShadowParams");
                ASPMainLightShadowConstantBuffer._CascadeCount = Shader.PropertyToID("_ASPCascadeCount");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 =
                    Shader.PropertyToID("_ASPCascadeShadowSplitSpheres0");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 =
                    Shader.PropertyToID("_ASPCascadeShadowSplitSpheres1");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 =
                    Shader.PropertyToID("_ASPCascadeShadowSplitSpheres2");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 =
                    Shader.PropertyToID("_ASPCascadeShadowSplitSpheres3");
                ASPMainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii =
                    Shader.PropertyToID("_ASPCascadeShadowSplitSphereRadii");
                ASPMainLightShadowConstantBuffer._ShadowOffset0 = Shader.PropertyToID("_ASPMainLightShadowOffset0");
                ASPMainLightShadowConstantBuffer._ShadowOffset1 = Shader.PropertyToID("_ASPMainLightShadowOffset1");
                ASPMainLightShadowConstantBuffer._ShadowmapSize = Shader.PropertyToID("_ASPMainLightShadowmapSize");
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                SetupASPMainLightShadowForLegacy();
            }

            private void RenderEmpty(ScriptableRenderContext context)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.SetGlobalTexture(_customBufferName, _emptyLightShadowmapTexture);
                cmd.SetGlobalInt("_ASPShadowMapValid", 0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                if (_emptyLightShadowmapTexture != null)
                {
                    Shader.SetGlobalTexture(_customBufferName, _emptyLightShadowmapTexture);
                }

                Shader.SetGlobalInt("_ASPShadowMapValid", 0);
                _shadowMapTexture?.Release();
                _emptyLightShadowmapTexture?.Release();
            }

#if UNITY_6000_0_OR_NEWER
[Obsolete("Compatible Mode only", false)]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_IsEmptyShdaowMap)
                {
                    _IsEmptyShdaowMap = false;
                    RenderEmpty(context);
                    return;
                }

                if (_shadowMapTexture == null)
                {
                    _IsEmptyShdaowMap = false;
                    SetupForEmptyRendering();
                    RenderEmpty(context);
                    return;
                }
                
                if (renderingData.lightData.mainLightIndex < 0)
                {
                    _IsEmptyShdaowMap = false;
                    SetupForEmptyRendering();
                    RenderEmpty(context);
                    return;
                }

                var cmd = CommandBufferPool.Get();
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var prevViewMatrix = renderingData.cameraData.camera.worldToCameraMatrix;
                var prevProjMatrix = renderingData.cameraData.camera.projectionMatrix;
                var visibleLight = renderingData.lightData.visibleLights[renderingData.lightData.mainLightIndex];

                var hasValidCullParam = false;
                if (PerformExtraCull && renderingData.cameraData.camera.TryGetCullingParameters(out _cullingParameters))
                {
                    hasValidCullParam = true;
                    _cullingParameters.cullingOptions &= ~CullingOptions.OcclusionCull;
                    _cullingParameters.isOrthographic = true;
                }

                var cullResult = renderingData.cullResults;
                // Handle drawing 
                var drawSettings =
                    CreateDrawingSettings(_shaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                drawSettings.perObjectData = PerObjectData.None;

                using (new ProfilingScope(cmd, new ProfilingSampler("ASP ShadowMap Pass")))
                {
                    for (int i = 0; i < _aspShadowData.mainLightShadowCascadesCount; i++)
                    {
                        // Need to start by setting the Camera position as that is not set for passes executed before normal rendering
                        // cmd.SetGlobalVector("_WorldSpaceCameraPos", renderingData.cameraData.worldSpaceCameraPos);

                        // TODO handle empty rendering
                        // Same as RenderShadowSlice() in URP
                        cmd.SetGlobalDepthBias(1.0f, 3.5f);
                        cmd.SetViewport(new Rect(_shadowSliceDatas[i].offsetX, _shadowSliceDatas[i].offsetY,
                            _shadowSliceDatas[i].resolution, _shadowSliceDatas[i].resolution));
                        cmd.SetViewProjectionMatrices(_lightViewMatrices[i], _lightProjectionMatrices[i]);
                        if (renderingData.lightData.mainLightIndex < 0 || renderingData.lightData.mainLightIndex >= renderingData.shadowData.bias.Count)
                        {
                            return;
                        }
                        Vector4 shadowBias = ShadowUtils.GetShadowBias(ref visibleLight,
                            renderingData.lightData.mainLightIndex, ref renderingData.shadowData,
                            _lightProjectionMatrices[i], _shadowSliceDatas[i].resolution);
                        // ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref visibleLight, shadowBias);
                        cmd.SetGlobalVector("_ASPShadowBias", shadowBias);

                        // Light direction is currently used in shadow caster pass to apply shadow normal offset (normal bias).
                        Vector3 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                        cmd.SetGlobalVector("_ASPLightDirection",
                            new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

                        // For punctual lights, computing light direction at each vertex position provides more consistent results (shadow shape does not change when "rotating the point light" for example)
                        Vector3 lightPosition = visibleLight.localToWorldMatrix.GetColumn(3);
                        cmd.SetGlobalVector("_ASPLightPosition",
                            new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        context.DrawRenderers(UseInjectCullResult ? CullResults[i] : renderingData.cullResults, ref drawSettings, ref _filteringSettings,
                            ref _renderStateBlock);
                        cmd.DisableScissorRect();
                        cmd.SetGlobalDepthBias(0.0f, 0.0f);
                        // RenderShadowSlice() End
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                    }
                }

                cmd.SetViewProjectionMatrices(prevViewMatrix, prevProjMatrix);
/*
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsLow, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsMedium, true);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadowsHigh, false);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.SoftShadows, true);
*/
                cmd.SetGlobalInt("_ASPShadowMapValid", 1);
                cmd.SetGlobalTexture(_customBufferName, _shadowMapTexture);
                ASPShadowUtil.SetupASPMainLightShadowReceiverConstants(cmd, ref visibleLight, ref renderingData,
                    _aspShadowData, _customWorldToShadowMatrices, _cascadeSplitDistances);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
#if UNITY_6000_0_OR_NEWER
            private bool SetupASPMainLightShadowForRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var renderTargetWidth = _aspShadowData.mainLightShadowmapWidth;
                var renderTargetHeight = (_aspShadowData.mainLightShadowCascadesCount == 2)
                    ? _aspShadowData.mainLightShadowmapHeight >> 1
                    : _aspShadowData.mainLightShadowmapHeight;
                
              
                if (!_IsEmptyShdaowMap && !IsNotActive)
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _shadowMapTexture, renderTargetWidth, renderTargetHeight,
                        16, name: "_CustomMainLightShadowmapTexture");    
                    return true;
                }
                else
                {
                    ShadowUtils.ShadowRTReAllocateIfNeeded(ref _emptyLightShadowmapTexture, 1, 1, 16,
                        name: "_ASPEmptyLightShadowmapTexture");
                    return false;
                }
            }

            private class EmptyShadowData
            {
                public TextureHandle EmptyShadowTarget;
            }
            
            private class MainLightShadowData
            {
                public TextureHandle ShadowTexture;
                public RendererListHandle[] RendererListHandle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                //RG resources and datas
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();
                var universalShadowData = frameData.Get<UniversalShadowData>();
                //prevent null cases of lighting
                if(lightData.mainLightIndex < 0)
                    return;
                if(lightData.mainLightIndex >= universalShadowData.bias.Count)
                    return;
                //guard end
                
                var isMainLightShadowReady = SetupASPMainLightShadowForRenderGraph(renderGraph, frameData);
                if (!isMainLightShadowReady)
                {
                    using (var builder = renderGraph.AddRasterRenderPass<EmptyShadowData>("ASP Empty Shadow Pass",
                               out var passData, profilingSampler))
                    {
                        passData.EmptyShadowTarget = renderGraph.ImportTexture(_emptyLightShadowmapTexture);

                        //copiedColorRT is now the blit destination
                        builder.AllowPassCulling(false);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderAttachmentDepth(passData.EmptyShadowTarget, AccessFlags.Write);
                        builder.SetRenderFunc((EmptyShadowData data, RasterGraphContext rgContext) =>
                        {
                            rgContext.cmd.ClearRenderTarget(RTClearFlags.ColorDepth, Color.black,1, 0);
                            rgContext.cmd.SetGlobalInt("_ASPShadowMapValid", 0);
                        });
                        builder.SetGlobalTextureAfterPass(passData.EmptyShadowTarget, s_aspShadowMapId);
                    }
                }
                else
                {
                    using (var builder =
 renderGraph.AddRasterRenderPass<MainLightShadowData>("ASP Character Shadow Pass",
                               out var passData, profilingSampler))
                    {
                        passData.ShadowTexture = renderGraph.ImportTexture(_shadowMapTexture);

                        //copiedColorRT is now the blit destination
                        var prevViewMatrix = cameraData.camera.worldToCameraMatrix;
                        var prevProjMatrix = cameraData.camera.projectionMatrix;
                        var visibleLight = lightData.visibleLights[lightData.mainLightIndex];
                        
                        // Handle drawing 
                        var drawingSettings = RenderingUtils.CreateDrawingSettings(_shaderTagId, renderingData,
                            cameraData, lightData, SortingCriteria.CommonOpaque);
                        drawingSettings.perObjectData = PerObjectData.None;
                        var filteringSettings = new FilteringSettings(RenderQueueRange.all);
                        
                        passData.RendererListHandle =
 new RendererListHandle[_aspShadowData.mainLightShadowCascadesCount];
                        for (int i = 0; i < _aspShadowData.mainLightShadowCascadesCount; i++)
                        {
                            passData.RendererListHandle[i] = renderGraph.CreateRendererList(
                                new RendererListParams(
                                UseInjectCullResult ? CullResults[i] : renderingData.cullResults, drawingSettings,
                                filteringSettings));
                            builder.UseRendererList(passData.RendererListHandle[i]);
                        }
                        
                        builder.AllowPassCulling(false);
                        builder.AllowGlobalStateModification(true);
                        builder.SetRenderAttachmentDepth(passData.ShadowTexture, AccessFlags.Write);
                        builder.SetRenderFunc((MainLightShadowData renderData, RasterGraphContext rgContext) =>
                        {
                            rgContext.cmd.ClearRenderTarget(RTClearFlags.ColorDepth, Color.black,1, 0);
                            for (int i = 0; i < _aspShadowData.mainLightShadowCascadesCount; i++)
                            {
                                rgContext.cmd.SetGlobalDepthBias(1.0f, 3.5f);
                                rgContext.cmd.SetViewport(new Rect(_shadowSliceDatas[i].offsetX, _shadowSliceDatas[i].offsetY,
                                    _shadowSliceDatas[i].resolution, _shadowSliceDatas[i].resolution));
                                rgContext.cmd.SetViewProjectionMatrices(_lightViewMatrices[i], _lightProjectionMatrices[i]);
                                if (lightData.mainLightIndex < 0 || lightData.mainLightIndex >= universalShadowData.bias.Count)
                                {
                                    return;
                                }
                                Vector4 shadowBias = ShadowUtils.GetShadowBias(ref visibleLight,
                                    lightData.mainLightIndex, universalShadowData,
                                    _lightProjectionMatrices[i], _shadowSliceDatas[i].resolution);
                                
                                // ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref visibleLight, shadowBias);
                                rgContext.cmd.SetGlobalVector("_ASPShadowBias", shadowBias);

                                // Light direction is currently used in shadow caster pass to apply shadow normal offset (normal bias).
                                Vector3 lightDirection = -visibleLight.localToWorldMatrix.GetColumn(2);
                                rgContext.cmd.SetGlobalVector("_ASPLightDirection",
                                    new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));

                                // For punctual lights, computing light direction at each vertex position provides more consistent results (shadow shape does not change when "rotating the point light" for example)
                                Vector3 lightPosition = visibleLight.localToWorldMatrix.GetColumn(3);
                                rgContext.cmd.SetGlobalVector("_ASPLightPosition",
                                    new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
                                
                                rgContext.cmd.DrawRendererList(renderData.RendererListHandle[i]);
                                
                                rgContext.cmd.DisableScissorRect();
                                rgContext.cmd.SetGlobalDepthBias(0.0f, 0.0f);
                            }
                            rgContext.cmd.SetViewProjectionMatrices(prevViewMatrix, prevProjMatrix);
                            rgContext.cmd.SetGlobalInt("_ASPShadowMapValid", 1);
                            ASPShadowUtil.SetupASPMainLightShadowReceiverConstants(rgContext.cmd, ref visibleLight, universalShadowData, _aspShadowData, _customWorldToShadowMatrices, _cascadeSplitDistances);
                           // rgContext.cmd.SetGlobalTexture(_CustomBufferName, passData.ShadowTexture);
                            //rgContext.cmd.ClearRenderTarget(RTClearFlags.All, Color.black,0, 0);
                        });
                        builder.SetGlobalTextureAfterPass(passData.ShadowTexture, s_aspShadowMapId);
                    }
                }
            }
#endif
        }
    }

#endif

    #endregion
}