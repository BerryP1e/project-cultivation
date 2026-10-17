/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

Shader "Hidden/ASP/PostProcess/Outline"
{
    Properties
    {
        [Toggle(_OuterLineToggle)] _OuterLineToggle ("Enable Outerline", float) = 0
        [Space(10)]
        _MaterialThreshold ("Material Threshold", Range(0.05, 1)) = 0.1
        _MaterialBias ("Material Bias", float) = 1.0
        _MaterialWeight ("Material Weight", Range(0, 50)) = 1

        [Space(10)]
        _LumaThreshold ("Luma Threshold", Range(0.05, 1)) = 0.1
        _LumaBias ("Luma Bias", float) = 1.0
        _LumaWeight ("Luma Weight", Range(0, 50)) = 1
        [Space(10)]
        _DepthThreshold ("Depth Threshold", Range(0.1, 1)) = 0.1
        _DepthBias ("Depth Bias", float) = 1.0
        _DepthWeight ("Depth Weight", Range(0, 50)) = 1
        [Space(10)]
        _NormalsThreshold ("Normals Threshold", Range(0.1, 1)) = 0.1
        _NormalsBias ("Normals Bias", float) = 1
        _NormalWeight ("Normals Weight", Range(0, 50)) = 1
        _MinWdith ("_Min Wdith", Range(0.1, 0.5)) = 0.5
        _DebugEdgeType ("Debug Edge Type", Float) = 6


        //AA & output
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        [Enum(None, 0, Regular, 1, WithDistanceBlend, 2)] _DebugType ("Debug Type", Float) = 0
        _DebugBackgroundColor("Debug Background Color", Color) = (1,1,1)
        [Enum(None, 0, SSAA_8, 1, SSAA_4, 2)] _AAType ("AA Type", Float) = 0
    }


    SubShader
    {
        Tags
        {
            "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        ZWrite Off
        Cull Off
        ZTest Always
        Pass
        {
            Name "Edge Detection Pass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "ShaderLibrary/PostProcessOutlineInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            #pragma vertex Vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _IS_DEBUG_MODE
            #pragma multi_compile_local MATERIAL_EDGE
            #pragma multi_compile_local LUMA_EDGE
            #pragma multi_compile_local NORMAL_EDGE
            #pragma multi_compile_local DEPTH_EDGE

            static const half horizontalOperator[8] = {-1, 0, 1, -2, 2, -1, 0, 1};
            static const half verticalOperator[8] = {1, 2, 1, 0, 0, -1, -2, -1};

            void SobelEdgeIndex(int index, in half cols[8], inout half gx, inout half gy)
            {
                gx += horizontalOperator[index] * cols[index];
                gy += verticalOperator[index] * cols[index];
            }

            void SobelEdgeIndex(int index, in half3 cols[8], inout half3 gx, inout half3 gy)
            {
                gx += horizontalOperator[index] * cols[index];
                gy += verticalOperator[index] * cols[index];
            }

            float SobelEdgeComposite(in half3 gx, in half3 gy, half threshold)
            {
                float edge = sqrt(dot(gx, gx) + dot(gy, gy));
                edge = 1 - step(threshold, edge);
                return edge;
            }

            float SobelEdgeComposite(in half gx, in half gy, half threshold)
            {
                float edge = sqrt(dot(gx, gx) + dot(gy, gy));
                edge = 1 - step(threshold, edge);
                return edge;
            }

            float SobelEdge(in half3 cols[8], half threshold)
            {
                half3 gx = half3(0, 0, 0);
                half3 gy = half3(0, 0, 0);
                for (int i = 0; i < 8; i++)
                {
                    gx += horizontalOperator[i] * cols[i];
                    gy += verticalOperator[i] * cols[i];
                }
                float edge = sqrt(dot(gx, gx) + dot(gy, gy));
                edge = 1 - step(threshold, edge);
                return edge;
            }

            half FadeByCameraDistance(float sceneDepth, float2 startEnd)
            {
                float eyeDepth = LinearEyeDepth(sceneDepth, _ZBufferParams).r;
                return saturate((eyeDepth - startEnd.x) / (startEnd.y - startEnd.x));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uvs[8];
                float2 uvsOuter[8];
                half4 outerValues[8];
                half4 materialPassValues[8];

                float4 scaleParams = GetScaledScreenParams();

                float4 centerMaterialPassValue = SampleMateriaPass(input.texcoord);
                if (centerMaterialPassValue.r <= 0)
                {
                    return half4(1, 1, 1, 1);
                }

                float sceneDepth = SampleSceneDepth(input.texcoord);
                float characterDepth = SampleCharacterSceneDepth(input.texcoord);
                float linear01Depth = Linear01Depth(sceneDepth, _ZBufferParams);
                float isPartialInner = 0;
                float isTotalInner = 1;
                float canSkip = linear01Depth > 0.99999 ? 1 : 0;
                float isCharcterPixelBlocked = step(LinearEyeDepth(sceneDepth, _ZBufferParams) + ASP_DEPTH_EYE_BIAS, LinearEyeDepth(characterDepth, _ZBufferParams));
                canSkip = max(canSkip, isCharcterPixelBlocked) > 0 ? 1 : 0;

                if (canSkip)
                {
                    return half4(1, 1, 1, 1);
                }

                float widthDistanceBlend = FadeByCameraDistance(sceneDepth, _WidthFadeDistanceStartEnd);
                widthDistanceBlend = lerp(0, widthDistanceBlend, _EnableWidthDistanceFade);

                float2 uvStep = (float2(1.0, 1.0) / scaleParams.xy) * lerp(_OutlineWidth, 0.5, widthDistanceBlend);
                SetupSurround8UVs(input.texcoord, uvs, uvStep);
                SetupSurround8UVs(input.texcoord, uvsOuter, uvStep * 2);

                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    materialPassValues[i] = SampleMateriaPass(uvs[i]);
                    outerValues[i] = SampleMateriaPass(uvsOuter[i]);
                    half id = materialPassValues[i].r;
                    isPartialInner += id > 0 ? 1 : 0;
                    isTotalInner *= id > 0 ? 1 : 0;
                }
                float vertexWeight = centerMaterialPassValue.b;
                isPartialInner = (isPartialInner > 1) * (isPartialInner < 8);

                real edgeMaterial = 1;
                real edgeLuma = 1;
                real edgeDepth = 1;
                real edgeNormals = 1;
                half weightDistanceFade = FadeByCameraDistance(sceneDepth, _ColorWeightFadeDistanceStartEnd);
                weightDistanceFade = lerp(0, weightDistanceFade, _EnableWeightDistanceFade);

                half colorDistanceFade = FadeByCameraDistance(sceneDepth, _ColorWeightFadeDistanceStartEnd);
                colorDistanceFade = lerp(0, colorDistanceFade, _EnableColorDistanceFade);

                float materialWeight = _MaterialWeight;
                float lumaWeight = _LumaWeight;
                float depthWeight = _DepthWeight;
                float normalsWeight = _NormalWeight;

                float materialFactorWeight = lerp(1, 0, weightDistanceFade);
                float lumaFactorWeight = lerp(1, 0, weightDistanceFade);
                float depthFactorWeight = lerp(1, 0, weightDistanceFade);
                float normalsFactorWeight = lerp(1, 0, weightDistanceFade);

                #if MATERIAL_EDGE
                if (_MaterialWeight > 0)
                {
                    // material id edge
                    real3 mC = DecodeMaterialIDToColor(centerMaterialPassValue.r);
                    real3 mL = DecodeMaterialIDToColor(materialPassValues[3].r);
                    real3 mR = DecodeMaterialIDToColor(materialPassValues[4].r);
                    real3 mT = DecodeMaterialIDToColor(materialPassValues[1].r);
                    real3 mD = DecodeMaterialIDToColor(materialPassValues[6].r);
                    real3 m0 = mC - mR;
                    real3 m1 = mC - mL;
                    real3 m2 = mC - mT;
                    real3 m3 = mC - mD;
                    m0 = clamp(m0, -2, 2);
                    m1 = clamp(m1, -2, 2);
                    m2 = clamp(m2, -2, 2);
                    m3 = clamp(m3, -2, 2);
                    real3 mH = m0 + m1;
                    real3 mV = m2 + m3;
                    float factor = length(abs(mH + mV));
                    edgeMaterial = 1.0 - step(_MaterialThreshold,
                        saturate(pow(abs(factor * materialFactorWeight), _MaterialBias) * vertexWeight * materialWeight));
                }
                #endif

                #if LUMA_EDGE
                if (_LumaWeight > 0)
                {
                    // albedo luminance edge
                    real lumaCenter = centerMaterialPassValue.g;
                    real lumaL = materialPassValues[3].g;
                    real lumaR = materialPassValues[4].g;
                    real lumaT = materialPassValues[1].g;
                    real lumaD = materialPassValues[6].g;
                    real luma0 = lumaCenter - lumaR;
                    real luma1 = lumaCenter - lumaL;
                    real luma2 = lumaCenter - lumaT;
                    real luma3 = lumaCenter - lumaD;
                    luma0 = clamp(luma0, -2, 2);
                    luma1 = clamp(luma1, -2, 2);
                    luma2 = clamp(luma2, -2, 2);
                    luma3 = clamp(luma3, -2, 2);
                    real lumaH = luma0 + luma1;
                    real lumaV = luma2 + luma3;
                    float factor = abs(lumaH + lumaV);
                    edgeLuma = 1.0 - step(_LumaThreshold, saturate(pow(abs(factor * lumaFactorWeight), _LumaBias) * vertexWeight * lumaWeight));
                }
                #endif

                #if NORMAL_EDGE
                if (_NormalWeight > 0)
                {
                    float3 nC = SampleSceneNormals(input.texcoord).rgb;
                    float3 nL = SampleSceneNormals(uvs[3]).rgb;
                    float3 nR = SampleSceneNormals(uvs[4]).rgb;
                    float3 nT = SampleSceneNormals(uvs[1]).rgb;
                    float3 nD = SampleSceneNormals(uvs[6]).rgb;
                    float3 n0 = nC - nR;
                    float3 n1 = nC - nL;
                    float3 n2 = nC - nT;
                    float3 n3 = nC - nD;
                    n0 = clamp(n0, -2, 2);
                    n1 = clamp(n1, -2, 2);
                    n2 = clamp(n2, -2, 2);
                    n3 = clamp(n3, -2, 2);
                    float3 nH = n0 + n1;
                    float3 nV = n2 + n3;
                    float factor = length(abs(nH + nV));
                    edgeNormals = 1.0 - step(_NormalsThreshold,
                saturate(pow(abs(factor * normalsFactorWeight), _NormalsBias) * normalsWeight * vertexWeight));
                }
                #endif

                #if DEPTH_EDGE
                if (_DepthWeight > 0)
                {
                    // depth edge
                    float eyeDepthCenter = LinearEyeDepth(sceneDepth, _ZBufferParams);
                    float dL = LinearEyeDepth(SampleSceneDepth(uvs[3]), _ZBufferParams);
                    float dR = LinearEyeDepth(SampleSceneDepth(uvs[4]), _ZBufferParams);
                    float dT = LinearEyeDepth(SampleSceneDepth(uvs[1]), _ZBufferParams);
                    float dD = LinearEyeDepth(SampleSceneDepth(uvs[6]), _ZBufferParams);
                    float d0 = eyeDepthCenter - dR;
                    float d1 = eyeDepthCenter - dL;
                    float d2 = eyeDepthCenter - dT;
                    float d3 = eyeDepthCenter - dD;
                    d0 = clamp(d0, -2, 2);
                    d1 = clamp(d1, -2, 2);
                    d2 = clamp(d2, -2, 2);
                    d3 = clamp(d3, -2, 2);
                    float dH = d0 + d1;
                    float dV = d2 + d3;
                    float factor = length(abs(dH + dV));
                    edgeDepth = 1.0 - step(_DepthThreshold,
                                  saturate(pow(abs(factor * depthFactorWeight), _DepthBias) * depthWeight * vertexWeight));
                }
                #endif
                float edgeOuter = 1;
                if (_OuterLineToggle > 0)
                {
                    //edgeOuter = 1.0 - isPartialInner;
                    real outerCenter = centerMaterialPassValue.r > 0;
                    real outerL = outerValues[3].r > 0;
                    real outerR = outerValues[4].r > 0;
                    real outerT = outerValues[1].r > 0;
                    real outerD = outerValues[6].r > 0;
                    real outer0 = outerCenter - outerR;
                    real outer1 = outerCenter - outerL;
                    real outer2 = outerCenter - outerT;
                    real outer3 = outerCenter - outerD;
                    outer0 = clamp(outer0, -2, 2);
                    outer1 = clamp(outer1, -2, 2);
                    outer2 = clamp(outer2, -2, 2);
                    outer3 = clamp(outer3, -2, 2);
                    real outerH = outer0 + outer1;
                    real outerV = outer2 + outer3;
                    float factor = length(abs(outerH + outerV));
                    edgeOuter = 1.0 - step(0.1, saturate(pow(factor, 1)));
                }

                float debugEdge = 1;
                #ifdef _IS_DEBUG_MODE
            		debugEdge = lerp(1, edgeMaterial, _DebugEdgeType == 0);
            	    debugEdge = lerp(debugEdge, edgeLuma, _DebugEdgeType == 1);
					debugEdge = lerp(debugEdge, edgeDepth, _DebugEdgeType == 2);
					debugEdge = lerp(debugEdge, edgeNormals, _DebugEdgeType == 3);
					debugEdge = lerp(debugEdge, edgeOuter, _DebugEdgeType == 4);
					debugEdge = lerp(debugEdge, 1.0 - centerMaterialPassValue.a, _DebugEdgeType == 5);

            		if(_DebugEdgeType < 5)
            		{
            			float distanceBlend = saturate((LinearEyeDepth(sceneDepth, _ZBufferParams).r - 5) / 30.0);
            			return real4(debugEdge, pow(distanceBlend, 0.35), 1, 1);
            		}
                #endif

                float finalEdge = min(edgeOuter, min(edgeNormals, min(edgeDepth, min(edgeLuma, edgeMaterial))));
                return real4(finalEdge, colorDistanceFade, 0, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline AA Pass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "ShaderLibrary/PostProcessOutlineInput.hlsl"
            #include "ShaderLibrary/FXAA.hlsl"

            #pragma vertex Vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _IS_DEBUG_MODE
            #pragma shader_feature_fragment _APPLY_FXAA

            TEXTURE2D_X(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D_X(_ASPOutlineTexture);
            SAMPLER(sampler_ASPOutlineTexture);

            real4 HandleDebug(float2 uv, real3 baseColor, real3 outlineColor, real hasEdge, real distanceFactor, float channelB)
            {
                real4 output = real4(0,0,0,1);
                float3 debugBlendOutline = _OutlineColor.rgb;
                debugBlendOutline = lerp(debugBlendOutline, _DebugBackgroundColor, distanceFactor);
                if (_DebugEdgeType == 5)
                {
                    if(channelB > 0)
                    {
                        output.rgb = _DebugBackgroundColor.rgb;
                        return output;
                    }
                    float3 debugBaseColor = lerp(float3(1, 1, 0), _DebugBackgroundColor, SampleMateriaPass(uv).b);
                    output.rgb = debugBaseColor;
                    return output;
                }
                
                if (_DebugEdgeType == 6)
                {
                      if(channelB > 0)
                    {
                        return real4(_DebugBackgroundColor, 1);
                    }
                    float3 debugBaseColor = lerp(float3(1, 1, 0), _DebugBackgroundColor, SampleMateriaPass(uv).b);
                    float3 debugColor = lerp(debugBaseColor, debugBlendOutline, hasEdge);
                    output.rgb = debugColor;
                    return output;
                }
                output = real4(lerp(_DebugBackgroundColor, debugBlendOutline, hasEdge), 1);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord).rgb;
                float3 outLineParams = SAMPLE_TEXTURE2D_X(_ASPOutlineTexture, asp_linear_clamp_sampler, input.texcoord).
rgb;
                float3 outlineColor = _OutlineColor.rgb;
                outlineColor = lerp(outlineColor, baseColor, outLineParams.g);
                float4 scaleParams = GetScaledScreenParams();
                #ifndef _IS_DEBUG_MODE
                if (outLineParams.b > 0)
                {
                    return real4(baseColor, 1);
                }
                #endif

                #ifdef _APPLY_FXAA
            	float hasEdge = ApplyOutlineFXAA(_ASPOutlineTexture, input.texcoord, scaleParams.xy);
                #else
                float hasEdge = 1.0 - outLineParams.r;
                #endif

                #ifdef _IS_DEBUG_MODE
            		return HandleDebug(input.texcoord, baseColor, outlineColor, hasEdge, outLineParams.g, outLineParams.b);
                #endif
                
                return real4(lerp(baseColor, outlineColor, hasEdge), 1);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}