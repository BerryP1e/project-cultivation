#ifndef ASP_SHADOWS_INCLUDED
#define ASP_SHADOWS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

TEXTURE2D_SHADOW(_ASPShadowMap);     SAMPLER_CMP(sampler_ASPShadowMap);

int _ASPShadowMapValid;

#ifndef SHADER_API_GLES3
CBUFFER_START(ASPLightShadows)
#endif

float4x4    _ASPMainLightWorldToShadow[5];
half        _ASPCascadeCount;
float4      _ASPCascadeShadowSplitSpheres0;
float4      _ASPCascadeShadowSplitSpheres1;
float4      _ASPCascadeShadowSplitSpheres2;
float4      _ASPCascadeShadowSplitSpheres3;
float4      _ASPCascadeShadowSplitSphereRadii;

float4      _ASPMainLightShadowOffset0; // xy: offset0, zw: offset1
float4      _ASPMainLightShadowOffset1; // xy: offset2, zw: offset3
float4      _ASPMainLightShadowParams;   // (x: shadowStrength, y: >= 1.0 if soft shadows, 0.0 otherwise, z: main light fade scale, w: main light fade bias)
float4      _ASPMainLightShadowmapSize;  // (xy: 1/width and 1/height, zw: width and height)

#ifndef SHADER_API_GLES3
CBUFFER_END
#endif

half4 GetASPMainLightShadowParams()
{
    return _ASPMainLightShadowParams;
}

ShadowSamplingData GetASPShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;

    // shadowOffsets are used in SampleShadowmapFiltered for low quality soft shadows.
    shadowSamplingData.shadowOffset0 = _ASPMainLightShadowOffset0;
    shadowSamplingData.shadowOffset1 = _ASPMainLightShadowOffset1;

    // shadowmapSize is used in SampleShadowmapFiltered otherwise
    shadowSamplingData.shadowmapSize = _ASPMainLightShadowmapSize;
    #if UNITY_VERSION >= 202201
    shadowSamplingData.softShadowQuality = _ASPMainLightShadowParams.y;
    #endif
    return shadowSamplingData;
}

half GetASPMainLightShadowFade(float3 positionWS)
{
    float3 camToPixel = positionWS - _WorldSpaceCameraPos;
    float distanceCamToPixel2 = dot(camToPixel, camToPixel);

    float fade = saturate(distanceCamToPixel2 * float(_ASPMainLightShadowParams.z) + float(_ASPMainLightShadowParams.w));
    return half(fade);
}

half ComputeASPCascadeIndex(float3 positionWS)
{
    float3 fromCenter0 = positionWS - _ASPCascadeShadowSplitSpheres0.xyz;
    float3 fromCenter1 = positionWS - _ASPCascadeShadowSplitSpheres1.xyz;
    float3 fromCenter2 = positionWS - _ASPCascadeShadowSplitSpheres2.xyz;
    float3 fromCenter3 = positionWS - _ASPCascadeShadowSplitSpheres3.xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

    half4 weights = half4(distances2 < _ASPCascadeShadowSplitSphereRadii);
    weights.yzw = saturate(weights.yzw - weights.xyz);

    return half(4.0) - dot(weights, half4(4, 3, 2, 1));
}

float4 TransformASPWorldToShadowCoord(float3 positionWS)
{
    half cascadeIndex = _ASPCascadeCount > 0 ? ComputeASPCascadeIndex(positionWS) : 0;
    
    float4 shadowCoord = mul(_ASPMainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));

    return float4(shadowCoord.xyz, 0);
}

float SampleASPShadowMap(float3 positionWS)
{
    ShadowSamplingData shadowSamplingData = GetASPShadowSamplingData();
    half4 shadowParams = GetASPMainLightShadowParams();
    float4 shadowCoord = TransformASPWorldToShadowCoord(positionWS);
    half aspShadow = SampleShadowmap(
        TEXTURE2D_ARGS(_ASPShadowMap, sampler_ASPShadowMap), shadowCoord, shadowSamplingData, shadowParams,
        false);
    aspShadow = lerp(aspShadow, 1, GetASPMainLightShadowFade(positionWS));
    
    if(_ASPShadowMapValid > 0)
    {
        return aspShadow;
    }
    return 1;
}

#endif