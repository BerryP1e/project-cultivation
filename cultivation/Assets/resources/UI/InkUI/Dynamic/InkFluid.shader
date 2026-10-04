Shader "Cultivation/UI/InkFluid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Clock ("Unscaled Clock", Float) = 0
        _Seed ("Seed", Float) = 0
        _Deform ("UV Deformation", Float) = 0.012
        _Density ("Density", Float) = 1
        _Reveal ("Reveal", Float) = 1
        _Ripple ("Ripple Center Progress", Vector) = (0.5,0.5,1,0)
        _InkState ("Density State", 2D) = "black" {}
        _HasState ("Has State", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; };
            sampler2D _MainTex;
            sampler2D _InkState;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect, _Ripple;
            float _Clock, _Seed, _Deform, _Density, _Reveal;
            float _HasState;
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 a=floor(p), b=frac(p); b=b*b*(3-2*b);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);
            }
            v2f vert(appdata v) { v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv; return o; }
            fixed4 frag(v2f i):SV_Target {
                float2 p=i.uv*3+_Seed;
                float2 d=float2(noise(p+float2(_Clock*.14,0)),noise(p+float2(8,_Clock*.11)))-.5;
                fixed4 ink=tex2D(_MainTex,i.uv+d*_Deform)+_TextureSampleAdd;
                fixed4 c=ink*i.color;
                c.rgb=i.color.rgb;
                c.a*=1-dot(ink.rgb,float3(.2126,.7152,.0722))*.72;
                if(_HasState>.5) c.a=tex2D(_InkState,i.uv+d*_Deform).r*i.color.a;
                c.a*=.82+.18*noise(p*2+float2(_Clock*.35,_Clock*.22));
                c.a=saturate(c.a*_Density);
                float revealNoise=noise(i.uv*9+_Seed)*.09;
                c.a*=smoothstep(i.uv.x-revealNoise-.04,i.uv.x-revealNoise+.04,_Reveal*1.14);
                float radius=.025+_Ripple.z*.55;
                float ring=1-smoothstep(.014,.065,abs(length(i.uv-_Ripple.xy)-radius));
                float wet=ring*_Ripple.w*(1-_Ripple.z);
                c.rgb=lerp(c.rgb,float3(.80,.82,.73),wet*.7);
                c.a=saturate(c.a+wet*.5);
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
