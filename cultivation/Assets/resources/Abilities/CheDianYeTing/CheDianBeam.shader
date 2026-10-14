Shader "Cultivation/CheDianBeam"
{
    Properties { _TintColor("Energy color",Color)=(.8,1.1,1.6,.8) _Phase("Flow time",Float)=0 _Dissolve("Whole dissolution",Range(0,1))=0 }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _TintColor;float _Phase,_Dissolve;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            float4 frag(v2f i):SV_Target{
                float edge=exp(-pow((i.uv.y-.5)*5.3,2));
                float energy=.82+.18*sin(i.uv.x*45-_Phase*31);
                return float4(i.color.rgb*_TintColor.rgb*1.7,edge*energy*i.color.a*_TintColor.a*(1-_Dissolve)*smoothstep(_Dissolve-.08,_Dissolve+.12,.2+.8*frac(sin(dot(i.uv,float2(127.1,311.7)))*43758.5453)));
            }
            ENDCG
        }
    }
}
