#ifndef ANIME_SHADING_OUTLINE_INCLUDED
#define ANIME_SHADING_OUTLINE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "ASPCommon.hlsl"
#include "ASPLitInput.hlsl"

struct Attributes
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float3 bakedNormal : TEXCOORD4;
    float3 color : COLOR;
};
struct Varyings
{
    float4 pos : SV_POSITION;
    float2 uv0 : TEXCOORD0;
    float4 positionSS : TEXCOORD1;
};

float Remap(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

Varyings OutlineVert(Attributes v)
{
    Varyings o = (Varyings)0;
    o.uv0 = v.uv0 * _AlphaClipUVCtrl.x +
            v.uv1 * _AlphaClipUVCtrl.y +
            v.uv2 * _AlphaClipUVCtrl.z +
            v.uv3 * _AlphaClipUVCtrl.w;
    float3 positionOS = GetFOVAdjustedPositionOS(v.vertex.xyz, _CharacterCenterWS, _FOVShiftX);
    VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
    
    float3 normalHCS =  mul((float3x3)UNITY_MATRIX_VP, mul((float3x3)UNITY_MATRIX_M, v.normal));
    
    #if _USE_BAKED_NORAML
    VertexNormalInputs normalInput = GetVertexNormalInputs(v.normal, v.tangent);
    float3x3 tangentToWorldMatrix = float3x3(normalInput.tangentWS, normalInput.bitangentWS, normalInput.normalWS);
    float3 smoothNormalWS = normalize(mul(v.bakedNormal, tangentToWorldMatrix));
    normalHCS =  mul((float3x3)UNITY_MATRIX_VP, mul((float3x3)UNITY_MATRIX_M, TransformWorldToObjectNormal(smoothNormalWS)));
    #endif
    float distanceBlendFactor = saturate((distance(vertexInput.positionWS, _WorldSpaceCameraPos) - _OutlineDistancFade.x) / (_OutlineDistancFade.y - _OutlineDistancFade.x));
    float outlineWidth = lerp(_OutlineWidth, 0, distanceBlendFactor);
    
    o.pos = vertexInput.positionCS;
    //we use vertex color's g channel (range 0-1) to control inverted hull outline width,
    //the b channel are set to be screen space outline's weight
    float scaleFactor = lerp(1, o.pos.w, _ScaleAsScreenSpaceOutline);
    o.pos.xy += normalize(normalHCS.xy) / _ScreenParams.xy * outlineWidth * v.color.g * scaleFactor;
 
    o.positionSS = ComputeScreenPos(o.pos);
    return o;
}

float4 OutlineFrag(Varyings IN) : SV_Target
{
    
    float ditherOut = 1;
    Unity_Dither((1.0 - _Dithering), IN.positionSS.xy / IN.positionSS.w, _ScreenParams.xy, _DitherTexelSize, ditherOut);
    clip(ditherOut);
    if(_OutlineWidth <= 0)
    {
        clip(-1);
    }
    #ifdef _ALPHATEST_ON
        half alpha = _BaseColor.a * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv0.xy).a;
        clip(alpha - _Cutoff);
        float clipMapValue = SAMPLE_TEXTURE2D(_ClipMap, sampler_ClipMap, IN.uv0.xy).r;
        clip(clipMapValue - _Cutoff);
    #endif
    
    return float4(_OutlineColor);
}

#endif