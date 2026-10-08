Shader "Cultivation/VoxelSurface"
{
    Properties
    {
        _Detail ("Detail", 2D) = "white" {}
        _DetailScale ("Detail Scale", Float) = 4
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _Metallic ("Source metallic",Range(0,1))=0
        _SourceTex ("Source surface", 2D) = "white" {}
        _SourceTint ("Source tint", Color) = (1,1,1,1)
        _UseSourceUV ("Preserve source UV", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _Detail;
        sampler2D _SourceTex;
        half4 _SourceTint;
        half _UseSourceUV;
        float _DetailScale;
        half _Smoothness;
        half _Metallic;
        struct Input { float4 color : COLOR; float3 worldPos; float3 worldNormal; float2 uv_SourceTex; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 w=abs(IN.worldNormal); w/=max(w.x+w.y+w.z,0.0001);
            float3 p=IN.worldPos*_DetailScale;
            half3 detail=tex2D(_Detail,p.yz).rgb*w.x+tex2D(_Detail,p.xz).rgb*w.y+tex2D(_Detail,p.xy).rgb*w.z;
            o.Albedo=lerp(IN.color.rgb*detail*1.4,tex2D(_SourceTex,IN.uv_SourceTex).rgb*_SourceTint.rgb,_UseSourceUV*IN.color.a);
            o.Smoothness=lerp(.08,_Smoothness,IN.color.a); o.Metallic=_Metallic*IN.color.a; o.Alpha=1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
