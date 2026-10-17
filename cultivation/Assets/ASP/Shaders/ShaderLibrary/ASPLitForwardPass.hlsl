#ifndef ASP_LIT_FORWARD_PASS_INCLUDED
#define ASP_LIT_FORWARD_PASS_INCLUDED
/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "ShaderLibrary/ASPLitInput.hlsl"
#include "ShaderLibrary/ASPLighting.hlsl"
#include "ASPCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"



//vetext color r : none, g : mesh-based outline width, b : screen space outline weight
struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS : NORMAL;
    float3 color : COLOR0;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
};

struct Varyings
{
    
    float4 positionHCS     : SV_POSITION;
    float4 uv   : TEXCOORD0;
    float3 vertexSH        : TEXCOORD1;
    float4 normalWS        : TEXCOORD2;
    float4 tangentWS       : TEXCOORD3;
    float4 bitangent       : TEXCOORD4;
    float3 positionWS      : TEXCOORD5;
    float4 positionSS      : TEXCOORD6;
    float4 positionNDC     : TEXCOORD7;
    float3 viewDirectionWS : TEXCOORD8;
    float4 shadowCoord     : TEXCOORD9;
    float3 vertexColor     : TEXCOORD10;
    float4 faceHairUV      : TEXCOORD11;
    float4 specRimUV       : TEXCOORD12;
    //TODO handle VR
    UNITY_VERTEX_OUTPUT_STEREO
    
};

Varyings ASPLitVert(Attributes IN)
{
    Varyings OUT;
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
    VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(GetFOVAdjustedPositionOS(IN.positionOS.xyz, _CharacterCenterWS, _FOVShiftX));

    OUT.positionHCS = vertexInput.positionCS;

    half3 viewDirectionWS = normalize(GetCameraPositionWS() - vertexInput.positionWS);

    OUT.normalWS = half4(NormalizeNormalPerVertex(normalInput.normalWS), viewDirectionWS.x);
    OUT.tangentWS = half4(normalInput.tangentWS, viewDirectionWS.y);
    OUT.bitangent = half4(normalInput.bitangentWS, viewDirectionWS.z);
    OUT.positionWS = vertexInput.positionWS;
    OUT.positionSS = ComputeScreenPos(vertexInput.positionCS);
    OUT.viewDirectionWS = viewDirectionWS;
    OUT.shadowCoord = GetShadowCoord(vertexInput);
    OUT.vertexColor = IN.color;
    OUT.uv.xy = IN.uv0 * _SurfaceUVCtrl.x +
                IN.uv1 * _SurfaceUVCtrl.y +
                IN.uv2 * _SurfaceUVCtrl.z +
                IN.uv3 * _SurfaceUVCtrl.w;
    OUT.uv.zw = IN.uv0 * _AlphaClipUVCtrl.x +
                IN.uv1 * _AlphaClipUVCtrl.y +
                IN.uv2 * _AlphaClipUVCtrl.z +
                IN.uv3 * _AlphaClipUVCtrl.w;
    OUT.specRimUV.xy = IN.uv0 * _SpecUVCtrl.x +
                       IN.uv1 * _SpecUVCtrl.y +
                       IN.uv2 * _SpecUVCtrl.z +
                       IN.uv3 * _SpecUVCtrl.w;
    OUT.specRimUV.zw = IN.uv0 * _RimMaskUVCtrl.x +
                       IN.uv1 * _RimMaskUVCtrl.y +
                       IN.uv2 * _RimMaskUVCtrl.z +
                       IN.uv3 * _RimMaskUVCtrl.w;
    float2 hairHightlightUV = IN.uv0 * _HairUVCtrl.x +
                              IN.uv1 * _HairUVCtrl.y +
                              IN.uv2 * _HairUVCtrl.z +
                              IN.uv3 * _HairUVCtrl.w;
    float2 faceShadowMapUV =  IN.uv0 * _FaceUVCtrl.x +
                              IN.uv1 * _FaceUVCtrl.y +
                              IN.uv2 * _FaceUVCtrl.z +
                              IN.uv3 * _FaceUVCtrl.w;
    OUT.faceHairUV = float4(faceShadowMapUV, hairHightlightUV);
    OUT.positionNDC = vertexInput.positionNDC;
    //vertex sh (will use in sampling pixel sh)
    OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);

    return OUT;
}

void InitializeInputData(Varyings IN, out ASPInputData inputData, float isFacing)
{
    inputData = (ASPInputData)0;
    inputData.uv = IN.uv.xy;
    inputData.specUV = IN.specRimUV.xy;
    inputData.rimUV = IN.specRimUV.zw;
    inputData.baseColor = _BaseColor;
    inputData.positionWS = IN.positionWS;
    inputData.positionCS = IN.positionHCS;
    inputData.positionVS = TransformWorldToView(IN.positionWS);
    inputData.positionNDC = IN.positionNDC;
    inputData.TBN = half3x3(normalize(IN.tangentWS.xyz), normalize(IN.bitangent.xyz), normalize(IN.normalWS.xyz));
    
    //flip the normal when render back side of a double-side material
    if (isFacing <= 0)
    {
        inputData.TBN[2] = -1 * inputData.TBN[2];
        inputData.normalWS = IN.normalWS.xyz + 2 * inputData.TBN[2] * max(0, -dot(inputData.TBN[2], IN.normalWS.xyz));
        inputData.normalWS = normalize(inputData.normalWS.xyz);
    }
    else
    {
        inputData.normalWS = normalize(IN.normalWS.xyz);
    }
    #ifdef _NORMALMAP
        real3 normalTSFromTex = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv.xy));
        inputData.normalWS = lerp(inputData.normalWS.xyz, TransformTangentToWorld(normalTSFromTex, inputData.TBN), _BumpScale);
    #endif
    inputData.normalVS = normalize(TransformWorldToViewDir(inputData.normalWS));
    inputData.matCapNormalWS = inputData.normalWS;
    inputData.viewDirectionWS = normalize(IN.viewDirectionWS);
    inputData.shadowCoord = IN.shadowCoord;
    #if !defined(_MAIN_LIGHT_SHADOWS_SCREEN) || defined(_SURFACE_TYPE_TRANSPARENT)
    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
    #endif
    //ASP shadow map does not use screen space shader
    inputData.aspShadowCoord = TransformASPWorldToShadowCoord(IN.positionWS);

    //select GI color based on _BakeGISource, 0 == sh9, 1 == flatten sh9, 2 = custom GI color
    half3 normalSH9 = lerp(inputData.normalWS.xyz, float3(0, 1, 0), saturate(_BakeGISource));
    half3 sh9Color = SampleSH(normalSH9);
    half3 giColor = step(2, _BakeGISource) * _OverrideGIColor + (1 - step(2, _BakeGISource)) * sh9Color;
    inputData.bakedGI = giColor;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

    // PBR
    half3 occlusionSmoothnessMetallic = half3(1, 0, 0);
    #ifdef _STYLE_STYLIZEPBR
        #ifdef _METALLICSPECGLOSSMAP
        occlusionSmoothnessMetallic = SAMPLE_TEXTURE2D(_OSMTexture, sampler_OSMTexture, IN.uv.xy).rgb;
        occlusionSmoothnessMetallic.g *= _PBRSmoothness;
        occlusionSmoothnessMetallic.r = LerpWhiteTo( occlusionSmoothnessMetallic.r, _PBROcclusionStrength);
        #else
        occlusionSmoothnessMetallic.g = _PBRSmoothness;
        occlusionSmoothnessMetallic.b = _PBRMetallic;
        #endif
    inputData.occlusion = occlusionSmoothnessMetallic.r;
    inputData.smoothness = occlusionSmoothnessMetallic.g;
    inputData.metallic = occlusionSmoothnessMetallic.b;
    #else
    inputData.occlusion = 1.0;
    inputData.smoothness = 0;
    inputData.metallic = 0;
    #endif
    // PBR
    inputData.pbrInfluenceDirectLighting = _PBRInfluenceDirectLighting;
    inputData.flattenAdditionalLighting = _FlattenAdditionalLighting;
    inputData.workflowSpace = _WorkflowSpace;

    // Emission
    float3 emissionColor = _EmissionColor.rgb;
    #ifdef _EMISSION
    emissionColor *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv.xy).rgb;
    inputData.emission = emissionColor;
    #else
    inputData.emission = 0;
    #endif
    
    // specular lighting
    float3 specularColor = _SpecularColor;
    float3 specularFallOffColor = _SpecularFallOffColor;
    #ifndef _STYLE_STYLIZEPBR
    specularColor *= SAMPLE_TEXTURE2D(_SpecularMaskMap, sampler_SpecularMaskMap, IN.specRimUV.xy).r;
    specularFallOffColor *= SAMPLE_TEXTURE2D(_SpecularMaskMap, sampler_SpecularMaskMap, IN.specRimUV.xy).r;
    #endif

    inputData.specularColor = specularColor;
    inputData.specularFallOffColor = specularFallOffColor;
    inputData.specularFalloff = max(_SpecularFalloff, 0.007);
    inputData.specularSize = _SpecularSize;
    
    // rim lighting
    inputData.rimLightStrength = _RimLightStrength;
    inputData.rimLightColor = _RimLightColor;
    inputData.rimLightSmoothness = _RimLightSmoothness;
    inputData.rimLightAlign = _RimLightAlign;
    inputData.depthRimLightColor = _DepthRimLightColor;
    inputData.depthRimLightStrength = _DepthRimLightStrength;
    inputData.rimLightOverShadow = _RimLightOverShadow;

    //shadow mask
    #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
    half4 shadowMask = half4(1, 1, 1, 1);
    #elif !defined(LIGHTMAP_ON)
    half4 shadowMask = unity_ProbesOcclusion;
    #endif
    inputData.shadowMask = shadowMask;

    // face shadow map related datas
    inputData.frontDirectionWS = half3(0, 0, 1);
    inputData.rightDirectionWS = half3(1, 0, 0);
    inputData.faceShaowMapUV = IN.faceHairUV.xy;
    inputData.frontDirectionWS = _FaceFrontDirection;
    inputData.rightDirectionWS = _FaceRightDirection;
    
    #ifdef _FACESHADOW
    inputData.faceShadowMask = 0;
    inputData.faceShadowPow = _FaceShadowMapPow;
    inputData.faceShadowSmoothness = _FaceShadowSmoothness;
    #endif

    // occlusion data use ramp occlusion map
    inputData.rampOcclusion = 1;
    inputData.offsetShadowAttenuation = 1;
    #ifdef _RAMP_OCCLUSION_MAP
    half rampOcclusionSampled = SAMPLE_TEXTURE2D(_RampOcclusionMap, sampler_RampOcclusionMap, IN.uv.xy).r;
    inputData.rampOcclusion = LerpWhiteTo( rampOcclusionSampled, _RampOcclusionStrength);
    #endif
    // whether override the light direction from asp character panel
    inputData.overrideLightDir = _OverrideLightDirToggle;
    inputData.fakeLightEuler = _FakeLightEuler;

    inputData.overrideLightColorIntensity = _OverrideLightColorIntensityToggle;
    inputData.fakeLightColor = _FakeLightColor * _FakeLightIntensity;
    // received shadow behaviour datas
    inputData.shadowColorMode = _ShadowColorMode;
    inputData.receivedShadowColor = _ReceivedShadowColor;
    // hair highlight params
    inputData.hairMaskMapUV = IN.faceHairUV.zw;
    inputData.hairLightColor = _HairLightColor;
    inputData.hairLightCameraRollInfluence = _HairLightCameraRollInfluence;
    inputData.hairLightFresnelMaskPower = _HairLightFresnelMaskPower;
    inputData.hairLightStrength = _HairLightStrength;
    inputData.hairUVSideCut = _HairUVSideCut;
    // MatCap reflection
    inputData.matCapReflectionStrength = _MatCapReflectionStrength;
    inputData.matCapRollStabilize = _MatCapRollStabilize;

    // shadow
    inputData.offsetShadowDistance = _OffsetShadowDistance;
    inputData.innerLineVertexWeight = IN.vertexColor.g;
}


real4 ASPLitFrag(Varyings IN, bool IsFacing : SV_IsFrontFace) : SV_Target
{
    //apply dither effect if alpha clip enabled
    #ifdef _ALPHATEST_ON
    float ditherOut = 1;
    Unity_Dither((1.0 - _Dithering), IN.positionSS.xy / IN.positionSS.w, _ScreenParams.xy, _DitherTexelSize, ditherOut);
    clip(ditherOut);
    
    half alpha = _BaseColor.a * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv.xy).a;
    clip(alpha - _Cutoff);

    float clipMapValue = SAMPLE_TEXTURE2D(_ClipMap, sampler_ClipMap, IN.uv.zw).r;
    clip(clipMapValue - _Cutoff);
    #endif
    
    //initialize fragment/lighting required datas
    ASPInputData inputData;
    InitializeInputData(IN, inputData, IsFacing);
    if(_DebugGI)
    {
        return float4(inputData.bakedGI, 1);
    }

    real4 sampledAlbedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, inputData.uv).rgba;
    inputData.albedo.rgb = inputData.baseColor.rgb * sampledAlbedo.rgb;
    inputData.albedo.a = sampledAlbedo.a;
    
    #ifdef _DBUFFER
        half3 specular = 0;
        ApplyDecalToBaseColor(inputData.positionCS,
            inputData.albedo.rgb);
    #endif
    
    real4 finalColor = UniversalFragmentToonLit(inputData, _BakeGISource > 1);

    //apply fog
    real fogCood = InitializeInputDataFog(float4(inputData.positionWS, 1.0), 0);
    finalColor.rgb = MixFog(finalColor.rgb, fogCood);
    
    return finalColor;
}
#endif
