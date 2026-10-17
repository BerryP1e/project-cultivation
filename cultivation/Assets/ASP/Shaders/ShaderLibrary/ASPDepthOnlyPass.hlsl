#ifndef ANIME_SHADING_DEPTH_ONLY_INCLUDED
#define ANIME_SHADING_DEPTH_ONLY_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "ASPCommon.hlsl"


//#if defined(LOD_FADE_CROSSFADE)
//    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
//#endif

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    #if defined(_ALPHATEST_ON)
    float2 uv       : TEXCOORD0;
    #endif
    float4 positionCS   : SV_POSITION;
    float4 positionSS : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    #if defined(_ALPHATEST_ON)
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    #endif
    float4 positionCS = TransformObjectToHClip(GetFOVAdjustedPositionOS(input.position.xyz, _CharacterCenterWS, _FOVShiftX));
    output.positionCS = positionCS;
    output.positionSS = ComputeScreenPos(output.positionCS);
    return output;
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    #if defined(_ALPHATEST_ON)
        float ditherOut = 1;
        Unity_Dither((1.0 - _Dithering), input.positionSS.xy / input.positionSS.w, _ScreenParams.xy, _DitherTexelSize, ditherOut);
        clip(ditherOut);
        Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
        float clipMapValue = SAMPLE_TEXTURE2D(_ClipMap, sampler_ClipMap, input.uv.xy).r;
        clip(clipMapValue - _Cutoff);
    #endif
    //#if defined(LOD_FADE_CROSSFADE)
    //LODFadeCrossFade(input.positionCS);
    //#endif

    return input.positionCS.z;
}
#endif
