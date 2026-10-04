Shader "Cultivation/UI/InkViewportFade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        _StencilComp ("Stencil Comparison",Float)=8
        _Stencil ("Stencil ID",Float)=0
        _StencilOp ("Stencil Operation",Float)=0
        _StencilWriteMask ("Stencil Write Mask",Float)=255
        _StencilReadMask ("Stencil Read Mask",Float)=255
        _ColorMask ("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
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
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 edge:TEXCOORD1; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; float2 viewport:TEXCOORD2; };
            sampler2D _MainTex;
            fixed4 _Color,_TextureSampleAdd;
            float4 _ClipRect,_FadeBounds;
            float4x4 _WorldToViewport;
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) {
                float2 a=floor(p),b=frac(p); b=b*b*(3-2*b);
                return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);
            }
            v2f vert(appdata v) {
                v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color*_Color; o.uv=v.uv;
                o.viewport=v.edge; return o;
            }
            fixed4 frag(v2f i):SV_Target {
                fixed4 c=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
                float distance=min(i.viewport.y-_FadeBounds.x,_FadeBounds.y-i.viewport.y);
                float fibre=noise(i.viewport*.045)*12+noise(i.viewport*.16)*5;
                // Zero alpha before the hard clip; broken ink fibres farther inward.
                float fade=smoothstep(5+fibre,_FadeBounds.z+fibre,distance);
                c.a*=fade;
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
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
