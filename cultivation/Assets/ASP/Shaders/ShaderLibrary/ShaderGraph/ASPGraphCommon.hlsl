#ifndef ASP_SG_COMMON_INCLUDED
#define ASP_SG_COMMON_INCLUDED

TEXTURE2D(_ASPMaterialTexture);
SAMPLER(sampler_ASPMaterialTexture);
TEXTURE2D(_ASPMaterialDepthTexture);
SamplerState asp_point_clamp_sampler;
SamplerState asp_linear_clamp_sampler;
//TEXTURE2D_X_FLOAT(_CameraDepthTexture);
//SAMPLER(sampler_CameraDepthTexture);

TEXTURE2D_SHADOW(_ASPShadowMap);     SAMPLER_CMP(sampler_ASPShadowMap);

void IsCharacterPixel_float(float2 uv, float depthTest, out half result)
{
    #if SHADERGRAPH_PREVIEW
    #else
    return;
    #endif
    result = 0;
}

void IsCharacterPixel_half(float2 uv, half depthTest, out half result)
{
    #if SHADERGRAPH_PREVIEW
    #else
    #endif
    result = 0;
}

void GetMainLightShadowAttenSoftArea_float(float3 posWS, out half result)
{
    result = 0;
    #if SHADERGRAPH_PREVIEW
    result = 1;
    #else
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(WorldPos));
    #else
    float4 shadowCoord = TransformWorldToShadowCoord(posWS);
    float4 shaadowask = half4(1,1,1,1);
    #endif
    float shadowAtten = MainLightShadow(shadowCoord, posWS, shaadowask, _MainLightOcclusionProbes);
    if(shadowAtten < 0.99 && shadowAtten > 0.3)
    {
        result = 1;
    }
    return;
    #endif
}

void GetMainLightShadowAttenSoftArea_half(float3 posWS, out half result)
{
    result = 0;
    #if SHADERGRAPH_PREVIEW
    result = 1;
    #else
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(WorldPos));
    #else
    float4 shadowCoord = TransformWorldToShadowCoord(posWS);
    float4 shaadowask = half4(1,1,1,1);
    #endif
    float shadowAtten = MainLightShadow(shadowCoord, posWS, shaadowask, _MainLightOcclusionProbes);
    if(shadowAtten < 0.99 && shadowAtten > 0.3)
    {
        result = 1;
    }
    return;
    #endif
}

half GetASPShadowMapAttenuation(float3 positionOS, float3 positionWS)
{
    half attenuation = 1;
    #if SHADERGRAPH_PREVIEW
    return attenuation;
    #else
    VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
    float4 shadowCoord = GetShadowCoord(vertexInput);
    //float3 positionWS = vertexInput.positionWS;
    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half4 shadowParams = GetMainLightShadowParams();

    half aspShadow = SampleShadowmap(
        TEXTURE2D_ARGS(_ASPShadowMap, sampler_ASPShadowMap), shadowCoord, shadowSamplingData, shadowParams,
        false);
    aspShadow = lerp(aspShadow, 1, GetMainLightShadowFade(positionWS));
    attenuation *= aspShadow;
    return attenuation;
    #endif
}

void ASPShadow_float(float3 positionOS,float3 positionWS, out half result)
{
    #if SHADERGRAPH_PREVIEW
        result = 1;
    #else
        result = GetASPShadowMapAttenuation(positionOS,positionWS).r;
        return;
    #endif
}

void ASPShadow_half(float3 positionOS,float3 positionWS, out half result)
{
    #if SHADERGRAPH_PREVIEW
        result = 1;
    #else
        result = GetASPShadowMapAttenuation(positionOS,positionWS).r;
        return;
    #endif
}
#endif