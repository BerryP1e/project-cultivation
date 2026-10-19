Shader "Cultivation/OutfitDoubleSided"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0
        _Metallic ("Metallic", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        struct Input { float2 uv_MainTex; };
        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 color = tex2D(_MainTex,input.uv_MainTex)*_Color;
            output.Albedo=color.rgb;
            output.Metallic=_Metallic;
            output.Smoothness=_Glossiness;
            output.Alpha=1;
            // Imported thin garments can have degenerate UV tangents. Keep the geometric
            // normal path; assigning a tangent-space normal here enables an invalid basis
            // and can feed NaNs into HDR bloom.
        }
        ENDCG
    }
    FallBack "Diffuse"
}
