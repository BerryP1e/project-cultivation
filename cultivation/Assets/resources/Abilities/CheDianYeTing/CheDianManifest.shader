Shader "Cultivation/CheDianManifest"
{
    Properties { _MainTex("Dragon skin",2D)="white"{} _Color("Color",Color)=(1,1,1,1) _Manifest("Head-first formation",Range(0,1))=0 _Dissolve("Particle return",Range(0,1))=0 _HeadWS("Head",Vector)=(0,0,0,0) _BackWS("Body direction and extent",Vector)=(0,0,-1,5) }
    SubShader {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        sampler2D _MainTex;fixed4 _Color;float _Manifest,_Dissolve;float4 _HeadWS,_BackWS;
        struct Input {float2 uv_MainTex;float3 worldPos;float revealOrder;};
        void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.revealOrder=v.color.r;}
        void surf(Input i,inout SurfaceOutputStandard o){
            float3 p=i.worldPos-_HeadWS.xyz;
            float grain=frac(sin(dot(floor(p*19),float3(12.9898,78.233,39.425)))*43758.5453);
            float front=min(_Manifest,1-_Dissolve)-i.revealOrder;
            clip(front+(grain-.5)*.025);
            fixed4 c=tex2D(_MainTex,i.uv_MainTex)*_Color;
            o.Albedo=c.rgb;o.Metallic=.08;o.Smoothness=.24;
            o.Emission=float3(.035,.22,.65)*(1-saturate(abs(front)*40));
            o.Alpha=c.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
