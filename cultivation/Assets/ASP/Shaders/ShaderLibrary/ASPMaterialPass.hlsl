#ifndef ANIME_SHADING_MATERIAL_PASS_INCLUDED
#define ANIME_SHADING_MATERIAL_PASS_INCLUDED

/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float3 color        : COLOR;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
};
struct Varyings
{
    float4 positionHCS   : SV_POSITION;
    float3 viewDirectionWS : TEXCOORD0;
    float3 normalWS     : TEXCOORD1;
    float4 uv : TEXCOORD2;
    float  outlineWidthFactor : TEXCOORD3;
    float4 positionSS   : TEXCOORD4;
    //float3 positionVS   : TEXCOORD0;
    //float eyeDepth : TEXCOORD2;
};

inline float ZBufferDepth( float value )
{
    return (1.0 - value * _ZBufferParams.w) / (value * _ZBufferParams.x);
}

// Tranforms position from object to camera space
inline float3 UnityObjectToViewPos( in float3 pos )
{
    return mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(pos, 1.0))).xyz;
}

Varyings MaterialPassVertex(Attributes IN)
{
    Varyings OUT;
    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
    float3 positionOS = GetFOVAdjustedPositionOS(IN.positionOS.xyz, _CharacterCenterWS, _FOVShiftX);
    VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
    
    OUT.positionHCS = vertexInput.positionCS;
    float3 cameraPositonWS = TransformObjectToWorld(GetFOVAdjustedPositionOS(TransformWorldToObject(GetCameraPositionWS()), _CharacterCenterWS, _FOVShiftX));
    
    OUT.viewDirectionWS = normalize(cameraPositonWS - vertexInput.positionWS);
    OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
    OUT.uv.xy = IN.uv0 * _SurfaceUVCtrl.x +
             IN.uv1 * _SurfaceUVCtrl.y +
             IN.uv2 * _SurfaceUVCtrl.z +
             IN.uv3 * _SurfaceUVCtrl.w;
    OUT.uv.zw = IN.uv0 * _AlphaClipUVCtrl.x +
            IN.uv1 * _AlphaClipUVCtrl.y +
            IN.uv2 * _AlphaClipUVCtrl.z +
            IN.uv3 * _AlphaClipUVCtrl.w;
    OUT.outlineWidthFactor = IN.color.b;
    OUT.positionSS = ComputeScreenPos(OUT.positionHCS);
    return OUT;
}

half4 MaterialPassFragment(Varyings IN) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);

    half3 albedoColor = SAMPLE_TEXTURE2D_X(_BaseMap, sampler_BaseMap, IN.uv.xy).rgb;
    #if defined(_ALPHATEST_ON)
    float ditherOut = 1;
    Unity_Dither((1.0 - _Dithering), IN.positionSS.xy / IN.positionSS.w, _ScreenParams.xy, _DitherTexelSize, ditherOut);
    clip(ditherOut);
    
    half alpha = _BaseColor.a * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv.xy).a;
    clip(alpha - _Cutoff);
    Alpha(SampleAlbedoAlpha(IN.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);

    float clipMapValue = SAMPLE_TEXTURE2D(_ClipMap, sampler_ClipMap, IN.uv.zw).r;
    clip(clipMapValue - _Cutoff);
    #endif
    
    if(_MaterialID <= 0)
    {
        return float4(_MaterialID,0,0,0);
    }
    
    //TODO
    //LUMINANCE SOURCE - ALBEDO OR VERTEX COLOR.r
    //extrude outline Width Factor
    //screen space outline width factor
    //screen space outline weight factor
    return float4(_MaterialID/255.0, Luminance(albedoColor), IN.outlineWidthFactor, 1);
}

/*   // TODO handle non-reverse z
   float viewZ = 0.01;
   #if UNITY_REVERSED_Z 
   viewZ = -TransformWorldToView(input.positionWS).z;
   #else
   viewZ = TransformWorldToView(input.positionWS).z;
   #endif

   float4 clipPos = TransformWorldToHClip(input.positionWS);
   half depth = clipPos.z / viewZ;
*/

#endif