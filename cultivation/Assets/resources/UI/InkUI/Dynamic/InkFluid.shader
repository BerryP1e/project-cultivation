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
        _Pointer ("Pointer UV Hover", Vector) = (-10,-10,0,0)
        _Drag ("Pointer Velocity", Vector) = (0,0,0,0)
        _Bleed ("Silhouette Edge Bleed", Float) = 0
        _FadeEnabled ("Viewport Ink Fade", Float) = 0
        _FadeBounds ("Viewport Bounds Width", Vector) = (0,0,82,0)
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
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float2 edge:TEXCOORD1; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; float2 edge:TEXCOORD2; };
            sampler2D _MainTex;
            sampler2D _InkState;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect, _Ripple;
            float _Clock, _Seed, _Deform, _Density, _Reveal;
            float _HasState;
            float4 _Pointer,_Drag;
            float _Bleed;
            float _FadeEnabled;
            float4 _FadeBounds;
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 a=floor(p), b=frac(p); b=b*b*(3-2*b);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);
            }
            v2f vert(appdata v) { v2f o; o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv; o.edge=v.edge; return o; }
            float densityAt(float2 uv) {
                if(any(uv<0) || any(uv>1)) return 0;
                if(_HasState>.5) return tex2D(_InkState,uv).r;
                float2 source=(uv-.5)*1.4+.5;
                if(any(source<0) || any(source>1)) return 0;
                float4 ink=tex2D(_MainTex,source)+_TextureSampleAdd;
                return ink.a*(1-dot(ink.rgb,float3(.2126,.7152,.0722))*.72);
            }
            fixed4 frag(v2f i):SV_Target {
                float2 p=i.uv*3+_Seed;
                float2 d=float2(noise(p+float2(_Clock*.85,0)),noise(p+float2(8,_Clock*.72)))-.5;
                float2 delta=i.uv-_Pointer.xy;
                float influence=exp(-dot(delta,delta)/.035)*_Pointer.z;
                // Immediate local pull/drag of the silhouette, independent of solver lag.
                float2 uv=i.uv+d*_Deform+(delta*.45-_Drag.xy*.075)*influence;
                float core=densityAt(uv);
                float spreadRadius=.007+_Deform*.1+_Bleed*.070;
                float spread=core;
                [unroll] for(int k=0;k<8;k++) {
                    float angle=k*.785398;
                    float2 direction=float2(cos(angle),sin(angle));
                    float fibre=.65+.35*noise(i.uv*20+direction*2+_Seed);
                    spread=max(spread,densityAt(uv+direction*spreadRadius*fibre));
                }
                float grain=.6+.4*noise(i.uv*45+_Seed);
                float fringe=max(0,spread-core)*grain;
                fixed4 c=float4(i.color.rgb,(core+fringe*(.25+_Bleed*.85))*i.color.a);
                c.rgb=lerp(c.rgb,float3(.48,.53,.49),saturate(fringe*2)*(.25+_Bleed*.5));
                c.a*=.82+.18*noise(p*2+float2(_Clock*.35,_Clock*.22));
                c.a=saturate(c.a*_Density);
                float revealNoise=noise(i.uv*9+_Seed)*.09;
                c.a*=smoothstep(i.uv.x-revealNoise-.04,i.uv.x-revealNoise+.04,_Reveal*1.14);
                float radius=.025+_Ripple.z*.55;
                float ring=1-smoothstep(.014,.065,abs(length(i.uv-_Ripple.xy)-radius));
                // Optional supplementary ring, distinct from ink-silhouette bleed above.
                float wet=ring*_Ripple.w*(1-_Ripple.z)*.35;
                c.rgb=lerp(c.rgb,float3(.80,.82,.73),wet*.7);
                c.a=saturate(c.a+wet*.5);
                if(_FadeEnabled>.5) {
                    float edgeDistance=min(i.edge.y-_FadeBounds.x,_FadeBounds.y-i.edge.y);
                    float fibre=noise(i.edge*.045)*12+noise(i.edge*.16)*5;
                    c.a*=smoothstep(5+fibre,_FadeBounds.z+fibre,edgeDistance);
                }
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
