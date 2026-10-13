Shader "Cultivation/UI/CharacterCard"
{
    Properties {
        [PerRendererData] _MainTex("Texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
        _Quality("Quality",Color)=(.8,.77,.44,1)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
    }
    SubShader {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Stencil {Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 edge:TEXCOORD1;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;float2 edge:TEXCOORD2;fixed4 color:COLOR;};
            fixed4 _Color,_Quality;float4 _Size,_ClipRect,_FadeBounds;float _Selected,_Hover,_Fade;
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex;o.uv=v.uv;o.edge=v.edge;o.color=v.color*_Color;return o;}
            fixed4 frag(v2f i):SV_Target {
                float2 size=max(_Size.xy,1),uv=i.uv;
                float radius=min(size.x,size.y)*.07;
                float2 p=abs((uv-.5)*size)-size*.5+radius;
                float d=length(max(p,0))+min(max(p.x,p.y),0)-radius;
                float alpha=saturate(.5-d),border=smoothstep(-2,-.8,d);
                float footer=1-smoothstep(.17,.19,uv.y);
                float glow=exp(-abs(uv.y-.20)*24)*(1-footer*.72);
                float3 base=lerp(float3(.14,.15,.125),float3(.21,.22,.18),uv.y)*(.88+.12*_Hover);
                base=lerp(base,float3(.09,.14,.16),footer*.65);
                // 自制水纹徽记保持低对比，图标主体仍是视觉重心。
                float wave=.5+.5*sin(length((uv-float2(.2,.85))*float2(1,1.3))*75);
                base+=pow(wave,10)*.022*(1-footer);
                base+=_Quality.rgb*glow*.22;
                base=lerp(base,lerp(float3(.34,.43,.45),_Quality.rgb*.88,_Selected),border*(.65+_Selected*.3));
                float stripe=1-smoothstep(1,2,abs(uv.y-.19)*size.y);
                base=lerp(base,_Quality.rgb*.86,stripe*.75);
                fixed4 result=fixed4(GammaToLinearSpace(base),alpha*(.91+_Selected*.07))*i.color;
                if(_Fade>.5)result.a*=smoothstep(2,_FadeBounds.z,min(i.edge.y-_FadeBounds.x,_FadeBounds.y-i.edge.y));
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
