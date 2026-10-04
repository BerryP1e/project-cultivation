Shader "Cultivation/UI/InkPaper"
{
    Properties
    {
        _MainTex ("Paper Grain", 2D) = "gray" {}
        _Tiling ("Tiling", Float) = 1
        _Strength ("Strength", Float) = 0.12
        _Gain ("Gain", Float) = 2
        _Pre ("Pre Normalised", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend DstColor Zero
        ColorMask RGB
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 screen:TEXCOORD0; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Tiling,_Strength,_Gain,_Pre;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.screen=ComputeScreenPos(o.vertex); return o; }
            float lum(float3 c) { return dot(c,float3(.2126,.7152,.0722)); }
            fixed4 frag(v2f i):SV_Target {
                float2 uv=i.screen.xy/i.screen.w*_Tiling;
                float c=lum(tex2D(_MainTex,uv).rgb),dev;
                if(_Pre>.5) dev=(c-.5)*_Gain;
                else {
                    float2 dx=float2(_MainTex_TexelSize.x,0),dy=float2(0,_MainTex_TexelSize.y);
                    dev=(c-(lum(tex2D(_MainTex,uv+dx).rgb)+lum(tex2D(_MainTex,uv-dx).rgb)+lum(tex2D(_MainTex,uv+dy).rgb)+lum(tex2D(_MainTex,uv-dy).rgb))*.25)*_Gain;
                }
                float factor=lerp(1,clamp(1+dev,.2,1.8),_Strength);
                return fixed4(factor,factor,factor,1);
            }
            ENDCG
        }
    }
}
