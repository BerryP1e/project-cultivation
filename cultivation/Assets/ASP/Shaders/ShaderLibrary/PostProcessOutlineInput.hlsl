#ifndef ANIME_SHADING_POST_PROCESS_OUTLINE_INPUT_INCLUDED
#define ANIME_SHADING_POST_PROCESS_OUTLINE_INPUT_INCLUDED

float _OutlineWidth;
float _OuterLineToggle;

float _MaterialThreshold;
float _MaterialBias;
float _MaterialWeight;

float _LumaThreshold;
float _LumaBias;
float _LumaWeight;

float _DepthThreshold;
float _DepthBias;
float _DepthWeight;

float _NormalsThreshold;
float _NormalsBias;
float _NormalWeight;

float _EnableColorDistanceFade;
float _EnableWeightDistanceFade;
float2 _ColorWeightFadeDistanceStartEnd;

float _EnableWidthDistanceFade;
float2 _WidthFadeDistanceStartEnd;

float _DebugEdgeType;
float _DebugType;
half3 _DebugBackgroundColor;

float _AAType;
half4 _OutlineColor;
#endif
