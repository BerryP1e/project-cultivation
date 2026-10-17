#ifndef ASP_LIT_INPUT_INCLUDED
#define ASP_LIT_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

SamplerState my_point_clamp_sampler;
TEXTURE2D(_BaseMap);                 SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);                 SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);             SAMPLER(sampler_EmissionMap);
TEXTURE2D(_RampMap);                 SAMPLER(sampler_RampMap);

#ifdef _ALPHATEST_ON
TEXTURE2D(_ClipMap);    SAMPLER(sampler_ClipMap);
#endif

#ifdef _HAIRMAP
TEXTURE2D(_HairHighlightMaskMap);    SAMPLER(sampler_HairHighlightMaskMap);
#endif

#ifdef _MATCAP_HIGHLIGHT_MAP
TEXTURE2D(_MatCapReflectionMap);     SAMPLER(sampler_MatCapReflectionMap);
TEXTURE2D(_MatCapReflectionMaskMap);
#endif

#ifdef _STYLE_STYLIZEPBR
    #ifdef _METALLICSPECGLOSSMAP
    TEXTURE2D(_OSMTexture);              SAMPLER(sampler_OSMTexture);
    #endif
#endif

#ifdef _FACESHADOW
TEXTURE2D(_FaceShadowMap);           SAMPLER(sampler_FaceShadowMap);
#endif

#ifdef _RAMP_OCCLUSION_MAP
TEXTURE2D(_RampOcclusionMap);        SAMPLER(sampler_RampOcclusionMap);
#endif

#ifndef _STYLE_STYLIZEPBR
TEXTURE2D(_SpecularMaskMap);         SAMPLER(sampler_SpecularMaskMap);
#endif
#ifdef _SSSMAP
TEXTURE2D(_SSSRampMap);              SAMPLER(sampler_SSSRampMap);
#endif

TEXTURE2D(_PupilMask);               SAMPLER(sampler_PupilMask);

#ifdef _RIMLIGHTING_ON
TEXTURE2D(_RimLightMaskMap);         SAMPLER(sampler_RimLightMaskMap);
#endif

#ifdef _EYE_HIGHLIGHT_MAP
TEXTURE2D(_EyeHighlightMap1);         SAMPLER(sampler_EyeHighlightMap1);
#endif

//TEXTURE2D(_StandardBrdfLut);         SAMPLER(sampler_StandardBrdfLut);

CBUFFER_START(UnityPerMaterial)
    half _MaterialID;
    half3 _FaceFrontDirection;
    half3 _FaceRightDirection;
    float3 _CharacterCenterWS;
    half _CharacterCenterCubeSize;
    half _FOVShiftX;

    //texture uv channel selector
    half4 _FaceUVCtrl;
    half4 _HairUVCtrl;
    half4 _AlphaClipUVCtrl;
    half4 _SurfaceUVCtrl;
    half4 _SpecUVCtrl;
    half4 _RimMaskUVCtrl;

    //for outline pass
    half _OutlineWidth;
    half _ScaleAsScreenSpaceOutline;
    half2 _OutlineDistancFade;
    half4 _OutlineColor;

    //surface
    float4 _BaseMap_ST;
    float4 _HairHightlightPatternMap_ST;
    half4 _BaseColor;
    half _BumpScale;
    half _Cutoff;
    half _ClipFactor;
    half _Dithering;
    half _DitherTexelSize;

    // PBR params
    half _PBRSmoothness;
    half _PBRMetallic;
    half _PBROcclusionStrength;
    half _PBRInfluenceDirectLighting;
    half _BakeGISource;
    half _FlattenAdditionalLighting;
    half _WorkflowSpace;
    // NPR params
    half _RampOcclusionStrength;

    half _ShadowColorMode;
    half3 _ReceivedShadowColor;
    // Eyes
    half _LambertShadowColorMode;
    half3 _LambertReceivedShadowColor;
    //
    half3 _OverrideGIColor;
    half4 _EmissionColor;

    // Specular 
    float3 _SpecularColor;
    float3 _SpecularFallOffColor;
    half _SpecularFalloff;
    half _SpecularSize;

    // Rim lighting
    half _DepthRimLightStrength;
    half4 _DepthRimLightColor;
    half _RimLightAlign;
    half _RimLightSmoothness;
    half _RimLightOverShadow;
    half _RimLightStrength;
    half4 _RimLightColor;

    //face direction
    half _FaceShadowMapPow;
    half _FaceShadowSmoothness;

    half _OverrideLightDirToggle;
    half3 _FakeLightEuler;

    half _OverrideLightColorIntensityToggle;
    half3 _FakeLightColor;
    half _FakeLightIntensity;
    //Toon Skin Shading

    //Hair
    half4 _HairLightColor;
    half _HairLightCameraRollInfluence;
    half _HairLightFresnelMaskPower;
    half _HairLightStrength;
    half _HairUVSideCut;

    //Shadow
    half _OffsetShadowDistance;

    //Matcap
    half _MatCapReflectionStrength;
    half _MatCapRollStabilize;

    //Eyes
    half _UseParallaxEffect;
    half _ParallaxHeight;
    half _UsePupilMask;
    half _HighLightAlphaClip;
    half3 _EyeHighlightColor;
    half3 _SelfUnlitAreaColor;
    half _EyeHighlightRotateDegree;
    half _HighlightDarken;

    //Debug
    half _DebugGI;
    int _UseSimpleAABBCutOffForCharacterShadow;
CBUFFER_END

struct ASPInputData
{
    //attributes
    float2 uv;
    float2 specUV;
    float2 rimUV;
    float2 faceShaowMapUV;
    float3 positionWS;
    float4 positionCS;
    float3 positionVS;
    float4 positionNDC;
    float3 normalVS;
    float3 normalWS;
    float3 matCapNormalWS;
    
    //attributes
    half3 viewDirectionWS;
    float4 shadowCoord;
    float4 aspShadowCoord;
    half4 shadowMask;
    float2 normalizedScreenSpaceUV;
    half3x3 TBN;
    half fogCoord;

    half4 albedo;
    half4 baseColor;
    //PBR
    half3 bakedGI;
    float smoothness;     //smothness = 1 - roughness
    float metallic;
    float occlusion;
    half3 emission;
    half pbrInfluenceDirectLighting;
    half flattenAdditionalLighting;
    half workflowSpace;
    
    //NPR
    half rampOcclusion;
    half offsetShadowAttenuation;
    half overrideLightDir;
    half overrideLightColorIntensity;

    half3 fakeLightColor;
    half3 fakeLightEuler;
    half shadowColorMode;
    half3 receivedShadowColor;

    //specular
    float3 specularColor;
    float3 specularFallOffColor;
    half specularFalloff;
    half specularSize;

    //rim lighting
    half rimLightStrength;
    half4 rimLightColor;
    half rimLightSmoothness;
    half rimLightAlign;
    half depthRimLightStrength;
    half4 depthRimLightColor;
    half rimLightOverShadow;

    //face
    half3 frontDirectionWS;
    half3 rightDirectionWS;
    half faceShadowMask;
    half faceShadowPow;
    half faceShadowSmoothness;

    //hair
    float2 hairMaskMapUV;
    half4 hairLightColor;
    half hairLightCameraRollInfluence;
    half hairLightFresnelMaskPower;
    half hairLightStrength;
    half hairUVSideCut;

    //shadow
    half offsetShadowDistance;

    //Matcap
    half matCapReflectionStrength;
    half matCapRollStabilize;

    //line param
    half innerLineVertexWeight;
    half4 outlineColor;
    half outlineWidth;

    //eyes
    half parallaxHeight;
    half usePupilMask;
    half pupilMask;
    half highLightAlphaClip;
    half3 eyeHighlightColor;
    half3 selfUnlitAreaColor;
    half eyeHighlightRotateDegree;
    half highlightDarken;
};

// TODO add bump map scale option
//for URP compatability


half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    #if !defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) && !defined(_GLOSSINESS_FROM_BASE_ALPHA)
    half alpha = albedoAlpha * color.a;
    #else
    half alpha = color.a;
    #endif
    #if UNITY_VERSION > 202201
    alpha = AlphaDiscard(alpha, cutoff);
    #else
    AlphaDiscard(alpha, cutoff);
    #endif
    return alpha;
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return half4(SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv));
}

half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = half(1.0))
{
    #ifdef _NORMALMAP
    half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
    #if BUMP_SCALE_NOT_SUPPORTED
    return UnpackNormal(n);
    #else
    return UnpackNormalScale(n, scale);
    #endif
    #else
    return half3(0.0h, 0.0h, 1.0h);
    #endif
}

half3 SampleEmission(float2 uv, half3 emissionColor, TEXTURE2D_PARAM(emissionMap, sampler_emissionMap))
{
    #ifndef _EMISSION
    return 0;
    #else
    return SAMPLE_TEXTURE2D(emissionMap, sampler_emissionMap, uv).rgb * emissionColor;
    #endif
}

#endif