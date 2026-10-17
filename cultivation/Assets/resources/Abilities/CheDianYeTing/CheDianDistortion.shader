Shader "Cultivation/CheDianDistortion"
{
    Properties { _NoiseTex("Flow noise",2D)="gray"{} _Phase("Flow time",Float)=0 _Dissolve("Whole dissolution",Range(0,1))=0 _Strength("Refraction",Float)=.003 }
    SubShader {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha Cull Off ZWrite Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _CheDianSceneColour,_NoiseTex;float4 _CheDianSceneColour_TexelSize;float _CheDianSceneColourReady,_Phase,_Strength,_Dissolve;
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct v2f {float4 pos:SV_POSITION;float4 grab:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;};
            v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.grab=ComputeScreenPos(o.pos);o.uv=v.uv;o.color=v.color;return o;}
            float4 frag(v2f i):SV_Target{
                float2 n=float2(tex2D(_NoiseTex,i.uv*3+float2(_Phase*.8,-_Phase*.2)).r,tex2D(_NoiseTex,i.uv.yx*2-float2(_Phase*.3,_Phase*.4)).r)-.5;
                float edge=pow(saturate(1-abs(i.uv.y*2-1)),2)*i.color.a;
                float2 uv=i.grab.xy/max(i.grab.w,.0001)+n*_Strength*edge;
                uv=clamp(uv,_CheDianSceneColour_TexelSize.xy*.5,1-_CheDianSceneColour_TexelSize.xy*.5);
                float3 colour=tex2D(_CheDianSceneColour,uv).rgb;
                return float4(colour,saturate(edge*(1-_Dissolve))*_CheDianSceneColourReady);
            }
            ENDCG
        }
    }
}
