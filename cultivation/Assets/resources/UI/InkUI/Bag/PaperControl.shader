Shader "Cultivation/UI/PaperControl"
{
    Properties
    {
        [PerRendererData] _MainTex("Texture",2D)="white"{}
        _DetailTex("Reference ornament",2D)="gray"{}
        _Color("Color",Color)=(1,1,1,1)
        _Tint("Item tone",Color)=(.7,.77,.71,1)
        _Size("Size",Vector)=(110,38,0,0)
        _Kind("0 category, 1 cell, 2 shadow",Float)=0
        _Hover("Hover",Float)=0
        _Pressed("Pressed",Float)=0
        _Selected("Selected",Float)=0
        _Occupied("Occupied",Float)=0
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
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
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;fixed4 color:COLOR;};
            fixed4 _Color,_Tint;
            sampler2D _DetailTex;
            float4 _Size,_ClipRect;
            float _Kind,_Hover,_Pressed,_Selected,_Occupied;
            v2f vert(appdata v){v2f o;o.local=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;}
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=saturate(i.uv),p=uv*_Size.xy;
                float sides=smoothstep(0,2.6,min(p.x,_Size.x-p.x));
                float3 rgb;float alpha;
                if(_Kind>3.5)
                {
                    float radius=length((uv-.5)*2);
                    float pixelRadius=min(_Size.x,_Size.y)*.5;
                    float edge=(1-radius)*pixelRadius;
                    rgb=lerp(float3(.66,.77,.69),float3(.49,.65,.56),_Hover*.5+_Pressed*.4);
                    float rim=1-smoothstep(.8,2.5,edge);
                    rgb=lerp(rgb,float3(.32,.49,.41),rim*.55);
                    alpha=(.72+_Hover*.12)*smoothstep(-.5,1.1,edge);
                }
                else if(_Kind>2.5)
                {
                    // 阴影只画在原格子矩形外，不再在物品底色内做暗角。
                    float2 pixelCoord=uv*(_Size.xy+float2(12,11))-float2(5,7);
                    float2 exterior=max(-pixelCoord,pixelCoord-_Size.xy);
                    float outside=step(0,max(exterior.x,exterior.y));
                    float2 shifted=abs(pixelCoord-_Size.xy*.5-float2(1.5,-2))-_Size.xy*.5;
                    float distance=length(max(shifted,0));
                    float shade=pow(1-smoothstep(0,6,distance),1.5)*outside;
                    float glow=(1-smoothstep(0,4,length(max(exterior,0))))*outside;
                    rgb=lerp(float3(.17,.25,.21),float3(.95,.83,.51),_Selected);
                    alpha=_Occupied*shade*.28*(1-_Selected)+glow*_Selected*.20;
                }
                else if(_Kind>1.5)
                {
                    rgb=float3(.18,.28,.25);
                    alpha=smoothstep(0,7,min(p.x,_Size.x-p.x))*pow(saturate(sin(uv.y*3.14159265)),1.5)*(.10+.16*_Selected+.15*_Pressed);
                }
                else if(_Kind<.5)
                {
                    float active=max(_Selected,_Pressed);
                    float detailY=saturate((p.y-5)/max(1,_Size.y-5));
                    float3 ornament=tex2D(_DetailTex,float2(.008+uv.x*.984,.146+detailY*.709)).rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    ornament=LinearToGammaSpace(ornament);
                    #endif
                    rgb=lerp(float3(.45,.65,.58),ornament,active);
                    rgb+=_Hover*.025;
                    rgb*=1-_Pressed*.12;
                    rgb*=.94+.06*uv.y;
                    alpha=min(.97,.65+.08*_Hover+.25*_Selected+.12*_Pressed)*smoothstep(0,8,min(p.x,_Size.x-p.x));
                    alpha*=smoothstep(0,7,p.y)*smoothstep(0,1.1,_Size.y-p.y);
                }
                else
                {
                    rgb=lerp(float3(.61,.69,.63),_Tint.rgb,_Occupied);
                    float hover=_Hover*(1-_Selected)*_Occupied;
                    rgb=lerp(rgb,float3(.88,.94,.84),hover*.16);
                    rgb*=1-_Pressed*.12;
                    alpha=lerp(.15,.85,_Occupied);
                    float edge=min(min(p.x,_Size.x-p.x),min(p.y,_Size.y-p.y));
                    float rim=1-smoothstep(1.7,3.8,edge);
                    float glow=1-smoothstep(3,9.5,edge);
                    rgb=lerp(rgb,float3(.83,.91,.74),hover*rim*.65);
                    float gold=_Selected*max(rim*.98,glow*.23);
                    rgb=lerp(rgb,float3(.99,.86,.50),saturate(gold));
                    alpha=max(alpha,max(gold*.97,hover*rim*.94));
                    alpha*=smoothstep(0,.75,edge);
                }
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb=GammaToLinearSpace(saturate(rgb));
                #endif
                fixed4 color=fixed4(rgb,alpha)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
