Shader "Cultivation/UI/PaperWash"
{
    Properties {
        [PerRendererData] _MainTex("Paper",2D)="white"{}
        _Color("Color",Color)=(1,1,1,1)
        _PaperBounds("Canvas bounds",Vector)=(0,0,1920,1080)
    }
    SubShader {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True"}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1;fixed4 color:COLOR;};
            sampler2D _MainTex;fixed4 _Color;float4 _PaperBounds;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;o.screen=ComputeScreenPos(o.vertex);return o;}
            fixed4 frag(v2f i):SV_Target {
                fixed4 pixel=tex2D(_MainTex,i.uv)*i.color;
                float2 screen=i.screen.xy/i.screen.w*_ScreenParams.xy;
                float2 paper=(screen-_PaperBounds.xy)/max(_PaperBounds.zw,float2(.001,.001));
                float weight=smoothstep(.12,.88,saturate(paper.x*.25+paper.y*.75));
                float3 target=lerp(float3(227,237,222)/255,float3(149,180,166)/255,weight);
                float3 basis=float3(228,234,219)/255;
                #ifndef UNITY_COLORSPACE_GAMMA
                target=GammaToLinearSpace(target);basis=GammaToLinearSpace(basis);
                #endif
                pixel.rgb=saturate(pixel.rgb*target/max(basis,.001));return pixel;
            }
            ENDCG
        }
    }
}
