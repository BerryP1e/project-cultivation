#ifndef CULTIVATION_WORLD_SURFACE_INPUT
#define CULTIVATION_WORLD_SURFACE_INPUT
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
TEXTURE2D(_GrassMap);SAMPLER(sampler_GrassMap);
TEXTURE2D(_PaintControl);SAMPLER(sampler_PaintControl);
TEXTURE2D_ARRAY(_TerrainLayers);SAMPLER(sampler_TerrainLayers);
CBUFFER_START(UnityPerMaterial)
float4 _BaseColor,_GrassColor,_BaseMap_ST;
float4 _GrassMap_ST;
float _GrassBlend,_TextureStrength,_NoiseStrength,_Cutoff,_Cull,_WindStrength;
float _TextureContrast,_TextureSaturation,_TextureExposure;
float _DetailSoftness;
float _BoundaryBlend,_RockBlend,_RockRootHeight,_RootMoss;
float _GrassMapExposure,_GrassMapSaturation;
float4 _PaintRect;
float _PaintEnabled;
CBUFFER_END
#define WORLD_PAINT_CONTROL(uv) SAMPLE_TEXTURE2D_LOD(_PaintControl,sampler_PaintControl,uv,0)
#define WORLD_PAINT_LAYER(uv,layer) SAMPLE_TEXTURE2D_ARRAY(_TerrainLayers,sampler_TerrainLayers,uv,layer)
#include "WorldAuthoringPaint.hlsl"
float3 WorldBrushWind(float3 positionOS)
{
    float3 original=TransformObjectToWorld(positionOS);
    positionOS.x+=sin(_Time.y*1.8+original.x*.7+original.z*.4)*max(0,positionOS.y)*_WindStrength;
    return positionOS;
}
#endif
