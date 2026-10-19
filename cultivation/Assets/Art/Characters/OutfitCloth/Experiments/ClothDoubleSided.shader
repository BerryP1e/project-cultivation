Shader "Cultivation/Experimental/ClothDoubleSided"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Metallic, _Glossiness;
        struct Input { float2 uv_MainTex; float facing : VFACE; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=albedo.rgb;
            o.Alpha=1;
            o.Metallic=_Metallic;
            o.Smoothness=_Glossiness;
            o.Normal=float3(0,0,IN.facing>=0?1:-1);
        }
        ENDCG
    }
    Fallback "Diffuse"
}
