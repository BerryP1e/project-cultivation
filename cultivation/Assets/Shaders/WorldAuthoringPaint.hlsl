#ifndef WORLD_AUTHORING_GROUND_PAINT
#define WORLD_AUTHORING_GROUND_PAINT
float4 WorldPaintTap(float2 cell,float2 uv)
{
    float2 lookup=(cell+.5)/_PaintRect.w;
    float4 control=WORLD_PAINT_CONTROL(lookup);
    if(control.g<.001)return 0;
    float layer=floor(control.r*255+.5);
    float3 colour=WORLD_PAINT_LAYER(uv,layer).rgb;
    return float4(colour*control.g,control.g);
}
float3 WorldGroundPaint(float3 original,float2 world)
{
    if(_PaintEnabled<.5)return original;
    float2 grid=(world-_PaintRect.xy)*_PaintRect.z*(_PaintRect.w-1);
    float2 cell=floor(grid),blend=frac(grid),uv=world*.25;
    // Interpolate colours and weights, never the numeric material IDs.
    float4 lower=lerp(WorldPaintTap(cell,uv),WorldPaintTap(cell+float2(1,0),uv),blend.x);
    float4 upper=lerp(WorldPaintTap(cell+float2(0,1),uv),WorldPaintTap(cell+1,uv),blend.x);
    float4 paint=lerp(lower,upper,blend.y);
    return original*(1-paint.a)+paint.rgb;
}
#endif
