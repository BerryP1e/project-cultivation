Shader "Cultivation/VoxelSurfaceDiffuse"
{
    Properties
    {
        _Detail ("Cut detail",2D)="white" {}
        _DetailScale ("Detail scale",Float)=4
        _SourceTex ("Source surface",2D)="white" {}
        _SourceTint ("Source tint",Color)=(1,1,1,1)
        _UseSourceUV ("Preserve source UV",Float)=0
    }
    SubShader
    {
        Tags {"RenderType"="Opaque"}
        CGPROGRAM
        #pragma surface surf Lambert fullforwardshadows
        #pragma target 3.0
        sampler2D _Detail,_SourceTex;
        half4 _SourceTint;half _UseSourceUV;float _DetailScale;
        struct Input {float4 color:COLOR;float3 worldPos;float3 worldNormal;float2 uv_SourceTex;};
        void surf(Input IN,inout SurfaceOutput o)
        {
            float3 w=abs(IN.worldNormal);w/=max(w.x+w.y+w.z,.0001);
            float3 p=IN.worldPos*_DetailScale;
            half3 detail=tex2D(_Detail,p.yz).rgb*w.x+tex2D(_Detail,p.xz).rgb*w.y+tex2D(_Detail,p.xy).rgb*w.z;
            o.Albedo=lerp(IN.color.rgb*detail*1.4,tex2D(_SourceTex,IN.uv_SourceTex).rgb*_SourceTint.rgb,_UseSourceUV*IN.color.a);
            o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
