float4 _MapRect,_HeightRange,_MistSettings,_FogPlane;
float4x4 _MistInverseViewProjection;
float3 _MistCameraPosition;
float _MistMaxDistance;
float WorldMistNoise(float3 p)
{
    float t=_MistSettings.w*_MistSettings.y;
    float3 drift=float3(t*.009,sin(t*.13)*.018,-t*.006);
    return WorldMistSampleNoise(p*.035+drift)*.70+WorldMistSampleNoise(p*.10-drift*1.6)*.30;
}
half4 WorldMistComposite(half4 scene,float3 end,float2 pixel)
{
    float3 origin=_MistCameraPosition,ray=normalize(end-origin);
    float maxT=min(length(end-origin),_MistMaxDistance);
    if(abs(ray.y)<.001)return scene;
    float a=(_HeightRange.x-origin.y)/ray.y,b=(_HeightRange.y-origin.y)/ray.y;
    float start=max(0,min(a,b)),stop=min(maxT,max(a,b));if(stop<=start)return scene;
    float stepLength=(stop-start)/36;
    float jitter=frac(52.9829189*frac(dot(pixel,float2(.06711056,.00583715))));
    float transmittance=1;float3 scattering=0;
    [loop]for(int k=0;k<36;k++)
    {
        float3 p=origin+ray*(start+(k+jitter)*stepLength);
        float2 field=WorldMistSampleField((p.xz-_MapRect.xy)*_MapRect.zw);
        // 固定底层云海连续铺满；地面由真实深度遮挡，不按投影到下层的陆地遮罩挖洞。
        float outside=lerp(1-smoothstep(-.4,.8,field.r),1,_FogPlane.x);
        float shoreHeight=lerp(field.g,_FogPlane.y,_FogPlane.x);
        float noise=WorldMistNoise(p),relative=p.y-shoreHeight-_MistSettings.x;
        float top=.20+(noise-.5)*1.4;
        float vertical=(1-smoothstep(top-1.0,top+.55,relative))*smoothstep(-4,-2.3,relative);
        float density=outside*vertical*smoothstep(.12,.72,noise)*1.50*_MistSettings.z;
        float alpha=1-exp(-density*stepLength);
        float lighting=saturate(.60+(relative+1)*.12+(noise-.5)*.32);
        float3 colour=lerp(float3(.27,.33,.36),float3(.68,.72,.71),lighting);
        scattering+=transmittance*alpha*colour;transmittance*=1-alpha;if(transmittance<.015)break;
    }
    return half4(scene.rgb*transmittance+scattering,scene.a);
}
