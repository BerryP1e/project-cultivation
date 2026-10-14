Shader "UI/CheDianFocusShade"
{
    Properties {
        [PerRendererData] _MainTex("UI texture",2D)="white"{}
        _DragonEllipse("Dragon spotlight",Vector)=(.5,.5,.23,.31)
        _CueEllipse("Cue spotlight",Vector)=(.6,.5,.08,.12)
        _Strength("World dimming",Range(0,1))=0
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _DragonEllipse,_CueEllipse;float _Strength;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float4 screen:TEXCOORD0;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.screen=ComputeScreenPos(o.pos);return o;}
            float4 frag(v2f i):SV_Target{
                float2 uv=i.screen.xy/i.screen.w;
                float dragon=length((uv-_DragonEllipse.xy)/_DragonEllipse.zw);
                float cue=length((uv-_CueEllipse.xy)/_CueEllipse.zw);
                float dim=smoothstep(.65,1.25,dragon)*smoothstep(.6,1.2,cue);
                return float4(.012,.016,.026,_Strength*dim);
            }
            ENDCG
        }
    }
}
