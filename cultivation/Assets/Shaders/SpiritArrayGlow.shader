Shader "Cultivation/SpiritArrayGlow"
{
    Properties {
        _MainTex ("阵纹", 2D) = "white" {}
        _TintColor ("灵光", Color) = (1, .8, .5, 1)
        _Opacity ("显现", Range(0,1)) = 1
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _TintColor;
            float _Opacity;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            half4 frag(v2f i) : SV_Target {
                half4 tex = tex2D(_MainTex, i.uv);
                half edge=smoothstep(0, .16, min(i.uv.y,1-i.uv.y));
                return half4(tex.rgb * _TintColor.rgb * i.color.rgb, tex.a * _Opacity * i.color.a * edge);
            }
            ENDCG
        }
    }
}
