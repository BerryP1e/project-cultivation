#ifndef ASP_EYELIT_FORWARD_PASS_INCLUDED
#define ASP_EYELIT_FORWARD_PASS_INCLUDED

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

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS : NORMAL;
    //vetext color r : (maybe shadow occlusion?), g : inner edge line, b : outline width
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
    float4 uv              : TEXCOORD0;
    float3 vertexSH        : TEXCOORD01;
    float4 normalWS        : TEXCOORD2;
    float4 tangentWS       : TEXCOORD3;
    float4 bitangent       : TEXCOORD4;
    float3 positionWS      : TEXCOORD5;
    float4 positionSS      : TEXCOORD6;
    float4 positionNDC     : TEXCOORD7;
    float3 viewDirectionWS : TEXCOORD8;
    float4 shadowCoord     : TEXCOORD9;
    float3 vertexColor     : TEXCOORD10;
    
    //TODO handle VR
    UNITY_VERTEX_OUTPUT_STEREO
    
};

Varyings ASPEyeLitVert(Attributes IN)
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
    OUT.uv.xy = IN.uv0 * _SurfaceUVCtrl.x +
                IN.uv1 * _SurfaceUVCtrl.y +
                IN.uv2 * _SurfaceUVCtrl.z +
                IN.uv3 * _SurfaceUVCtrl.w;
    OUT.uv.zw = IN.uv0 * _AlphaClipUVCtrl.x +
                IN.uv1 * _AlphaClipUVCtrl.y +
                IN.uv2 * _AlphaClipUVCtrl.z +
                IN.uv3 * _AlphaClipUVCtrl.w;
        
    OUT.vertexColor = IN.color;
    OUT.positionNDC = vertexInput.positionNDC;
    OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);

    return OUT;
}

void InitializeEyeInputData(Varyings IN, out ASPInputData inputData, float isFacing)
{
    //half fogCoord;
    //half4 shadowMask;
    inputData = (ASPInputData)0;
    inputData.uv = IN.uv.xy;
    inputData.baseColor = _BaseColor;
    inputData.positionWS = IN.positionWS;
    inputData.positionCS = IN.positionHCS;
    inputData.positionVS = TransformWorldToView(IN.positionWS);
    inputData.positionNDC = IN.positionNDC;
    inputData.TBN = half3x3(normalize(IN.tangentWS.xyz), normalize(IN.bitangent.xyz), normalize(IN.normalWS.xyz));
    inputData.normalWS = normalize(IN.normalWS.xyz);
    inputData.normalVS = normalize(TransformWorldToViewDir((inputData.normalWS)));
    #ifdef _NORMALMAP
        real3 normalTSFromTex = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv.xy));
        inputData.matCapNormalWS = lerp(inputData.normalWS.xyz, TransformTangentToWorld(normalTSFromTex, inputData.TBN), _BumpScale);
    #endif

    inputData.viewDirectionWS = normalize(IN.viewDirectionWS);
    inputData.shadowCoord = IN.shadowCoord;
    inputData.aspShadowCoord  = IN.shadowCoord;
    #if !defined(_MAIN_LIGHT_SHADOWS_SCREEN) || defined(_SURFACE_TYPE_TRANSPARENT)
    inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
    #endif
    //ASP shadow map does not use screen space shader
    inputData.aspShadowCoord = TransformASPWorldToShadowCoord(IN.positionWS);
    
    //select GI color based on _BakeGISource, 0 == sh9, 1 == flatten sh9, 2 = custom GI color
    half3 normalSH9 = lerp(inputData.normalWS.xyz, float3(0,1,0), saturate(_BakeGISource));
    half3 sh9Color = SampleSHPixel(IN.vertexSH, normalSH9);
    half3 giColor = step(2, _BakeGISource) * _OverrideGIColor + (1 - step(2, _BakeGISource)) * sh9Color;
    inputData.bakedGI = giColor;
    inputData.normalizedScreenSpaceUV =  GetNormalizedScreenSpaceUV(IN.positionHCS);
    inputData.occlusion = 1.0;
  
    // PBR
    inputData.pbrInfluenceDirectLighting = _PBRInfluenceDirectLighting;
    inputData.flattenAdditionalLighting = _FlattenAdditionalLighting;

    // Emission
    float3 emissionColor = _EmissionColor.rgb;
    #ifdef _EMISSION
    emissionColor *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv.xy).rgb;
    inputData.emission = emissionColor;
    #else
    inputData.emission = 0;
    #endif
    // Emission
    
    #if defined(SHADOWS_SHADOWMASK) && defined(LIGHTMAP_ON)
    half4 shadowMask = half4(1, 1, 1, 1);
    #elif !defined(LIGHTMAP_ON)
    half4 shadowMask = unity_ProbesOcclusion;
    #endif
    inputData.shadowMask = shadowMask;

    inputData.frontDirectionWS = half3(0,0,1);
    inputData.rightDirectionWS = half3(1,0,0);
    inputData.frontDirectionWS = _FaceFrontDirection;
    inputData.rightDirectionWS = _FaceRightDirection;

    
    inputData.offsetShadowAttenuation = 1;
    inputData.overrideLightDir = _OverrideLightDirToggle;
    inputData.fakeLightEuler = _FakeLightEuler;
    
    inputData.overrideLightColorIntensity = _OverrideLightColorIntensityToggle;
    inputData.fakeLightColor = _FakeLightColor * _FakeLightIntensity;
    #ifdef _STYLE_LAMBERTLIGHTING
    inputData.shadowColorMode = saturate(_LambertShadowColorMode);
    inputData.receivedShadowColor = _LambertReceivedShadowColor;
    #else
    inputData.shadowColorMode = saturate(_ShadowColorMode);
    inputData.receivedShadowColor = _ReceivedShadowColor;
    #endif

    //matcap
    inputData.matCapReflectionStrength = _MatCapReflectionStrength;
    inputData.matCapRollStabilize = _MatCapRollStabilize;
    
    //shadow
    inputData.offsetShadowDistance = _OffsetShadowDistance;
    inputData.innerLineVertexWeight = IN.vertexColor.g;

    //eyes
    inputData.parallaxHeight = _ParallaxHeight;
    inputData.usePupilMask = _UsePupilMask;
    float2 maskCenter = IN.uv.xy - float2(0.5, 0.5);
    inputData.pupilMask = SAMPLE_TEXTURE2D(_PupilMask, sampler_PupilMask, inputData.uv).r;
    inputData.highLightAlphaClip = _HighLightAlphaClip;
    inputData.selfUnlitAreaColor = _SelfUnlitAreaColor;
    inputData.eyeHighlightColor = _EyeHighlightColor;
    inputData.eyeHighlightRotateDegree = _EyeHighlightRotateDegree;
    inputData.highlightDarken = _HighlightDarken;
}


real4 ASPEyeLitFrag(Varyings IN, bool IsFacing : SV_IsFrontFace) : SV_Target
{
    #ifdef _ALPHATEST_ON
    float ditherOut = 1;
    Unity_Dither((1.0 - _Dithering), IN.positionSS.xy / IN.positionSS.w, _ScreenParams.xy, _DitherTexelSize, ditherOut);
    clip(ditherOut);
    
    half alpha = _BaseColor.a * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv.xy).a;
    clip(alpha - _Cutoff);
    
    float clipMapValue = SAMPLE_TEXTURE2D(_ClipMap, sampler_ClipMap, IN.uv.zw).r;
    clip(clipMapValue - _Cutoff);
    #endif
    
    ASPInputData inputData;
    InitializeEyeInputData(IN, inputData, IsFacing);
    //return float4(inputData.normalWS, 1);
    real4 finalColor = UniversalFragmentToonEyeLit(inputData, _BakeGISource > 1);

    real fogCood = InitializeInputDataFog(float4(inputData.positionWS, 1.0), 0);
    finalColor.rgb = MixFog(finalColor.rgb, fogCood);
    return finalColor;
}

#endif
