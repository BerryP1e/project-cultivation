Shader "Cultivation/UI/FormationModelBrush"
{
    Properties { _MainTex("Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Reveal("Brush",Range(0,1))=1 _Ground("Ground",Float)=13000 }
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float4 _MainTex_ST,_Color;float _Reveal,_Ground;
            struct app {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct vary {float4 position:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;};
            vary vert(app v){vary o;o.position=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;o.normal=UnityObjectToWorldNormal(v.normal);return o;}
            fixed4 frag(vary i):SV_Target{
                float noise=frac(sin(dot(floor((i.world.xz-_Ground)*22),float2(12.9898,78.233)))*43758.5453);
                if(_Reveal<.999)clip(_Reveal*2.3-(i.world.y-_Ground)-(1-_Reveal)*noise*.24);
                fixed4 c=tex2D(_MainTex,i.uv)*_Color;c.rgb*=.48+.52*saturate(dot(normalize(i.normal),normalize(float3(.4,.8,-.4))));c.a=1;return c;
            }
            ENDCG
        }
    }
}
