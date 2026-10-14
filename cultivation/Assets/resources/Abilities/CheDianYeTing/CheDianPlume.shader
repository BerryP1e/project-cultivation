Shader "Cultivation/CheDianPlume"
{
    Properties { _MainTex("Cloud mask",2D)="white"{} _NoiseTex("Flow noise",2D)="gray"{} _Glow("Glow",Float)=2 _Phase("Flow time",Float)=0 _Dissolve("Whole dissolution",Range(0,1))=0 }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex,_NoiseTex;float _Glow,_Phase,_Dissolve;
            struct appdata { float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
            float4 frag(v2f i):SV_Target{
                float n=tex2D(_NoiseTex,i.uv*1.8+float2(_Phase*.21,-_Phase*.31)).r;
                float mask=tex2D(_MainTex,i.uv+float2(n-.5,.5-n)*.09).r;
                float flames=saturate((mask*(.6+n)-.04)*1.8);
                return float4(i.color.rgb*_Glow*(.6+mask),flames*i.color.a*(1-_Dissolve)*smoothstep(_Dissolve-.08,_Dissolve+.12,n));
            }
            ENDCG
        }
    }
}
