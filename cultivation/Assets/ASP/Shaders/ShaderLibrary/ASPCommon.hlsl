#ifndef ASP_COMMON_INCLUDED
#define ASP_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

#define ASP_DEPTH_EYE_BIAS  0.005

#define ASP_OFFSET_SHADOW_EYE_BIAS  0.001

void SetupSurround8UVs(float2 uvCenter, inout float2 uvs[8], float2 uvStep)
{
    uvs[0] = uvCenter + uvStep * float2(-1, 1);
    uvs[1] = uvCenter + uvStep * float2(0, 1);
    uvs[2] = uvCenter + uvStep * float2(1, 1);
    uvs[3] = uvCenter + uvStep * float2(-1, 0);
    uvs[4] = uvCenter + uvStep * float2(1, 0);
    uvs[5] = uvCenter + uvStep * float2(-1, -1);
    uvs[6] = uvCenter + uvStep * float2(0, -1);
    uvs[7] = uvCenter + uvStep * float2(1, -1);
}

void SetupSurroundCrossUVs(float2 uvCenter, inout float2 uvs[4], float2 uvStep)
{
    uvs[0] = uvCenter + uvStep * float2(-1, 1);
    uvs[1] = uvCenter + uvStep * float2(-1, -1);
    uvs[2] = uvCenter + uvStep * float2(1, 1);
    uvs[3] = uvCenter + uvStep * float2(1, -1);
}

void SetupSurroundLRTDUVs(float2 uvCenter, inout float2 uvs[4], float2 uvStep)
{
    uvs[0] = uvCenter + uvStep * float2(-1, 0);
    uvs[1] = uvCenter + uvStep * float2(1, 0);
    uvs[2] = uvCenter + uvStep * float2(0, 1);
    uvs[3] = uvCenter + uvStep * float2(0, -1);
}

void Unity_Dither(float In, float2 ScreenPosition, float2 _ScreenParamsXY, half ditherPixelSize, out float Out)
{
    float2 uv = ScreenPosition.xy * _ScreenParamsXY / ditherPixelSize;
    float DITHER_THRESHOLDS[16] =
    {
        1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
        4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
    };
    uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
    Out = In - DITHER_THRESHOLDS[index];
}

float2 RotateUVDeg(float2 UV, float2 Center, float Rotation)
{
    float2 uv = UV;
    Rotation = Rotation * (3.1415926f/180.0f);
    uv -= Center;
    float s = sin(Rotation);
    float c = cos(Rotation);
    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;
    uv.xy = mul(uv.xy, rMatrix);
    uv += Center;
    return uv;
}

float4 TransformHClipToViewPortPos(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o / o.w;
}

half3 hash31(float p)
{
    half3 p3 = frac(half3(p, p, p) * half3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx+33.33);
    return frac((p3.xxy+p3.yzz)*p3.zyx); 
}

half hash11(float p)
{
    p = frac(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

TEXTURE2D(_ASPMaterialTexture);
SAMPLER(sampler_ASPMaterialTexture);

TEXTURE2D(_ASPMaterialDepthTexture);
SamplerState asp_point_clamp_sampler;

TEXTURE2D(_ASPDepthOffsetShadowTexture);

SamplerState asp_linear_clamp_sampler;

half3 DecodeMaterialIDToColor(half value)
{
    // Calculate a color based on the material ID,
    // hash31()  provides a pseudo-random color based on the input
    half materialID = value;
    materialID *= 255.0;
    float factor = step(1, materialID);
    return factor * hash31(materialID) + (1.0 - factor) * float3(0,0,0);
}

half DecodeMaterialIDToFloat(half value)
{
    half materialID = value;
    materialID *= 255.0;
    return step(1, materialID);
}

half4 SampleMateriaPass(float2 uv)
{
  return SAMPLE_TEXTURE2D_X(_ASPMaterialTexture, asp_point_clamp_sampler, uv).rgba;
}

float DecodeMateriaPassID(float4 value)
{
    return value.r;
}

half3 DecodeMateriaPassAlbedoLuminance(float4 value)
{
    return value.ggg;
}

float SampleCharacterSceneDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_ASPMaterialDepthTexture, asp_point_clamp_sampler, UnityStereoTransformScreenSpaceTex(uv)).r;
}

float SampleCharacterDepthOffsetShadow(float2 uv)
{
    return SAMPLE_TEXTURE2D_X(_ASPDepthOffsetShadowTexture, asp_point_clamp_sampler, UnityStereoTransformScreenSpaceTex(uv)).r;
}

float Remapfloat(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

float3 GetFOVAdjustedPositionOS(float3 positionOS, float3 objectCenterWS, float shift)
{
    
    // Adjusts object-space position based on field-of-view and a shift factor. 
    // use to perspective distortion. 
    float3 objectCenterVS = TransformWorldToView(objectCenterWS);
    float3 fovAdjustedPositionVS = mul(UNITY_MATRIX_MV, float4(positionOS.xyz, 1)).xyz;
    fovAdjustedPositionVS.z = (fovAdjustedPositionVS.z - objectCenterVS.z)/(shift + 1) + objectCenterVS.z;
    return mul(Inverse(UNITY_MATRIX_MV), float4(fovAdjustedPositionVS, 1)).xyz;
}
#endif