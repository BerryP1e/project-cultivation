Shader "Cultivation/UI/CharacterCurtain"
{
    Properties {
        [PerRendererData] _MainTex("Texture",2D)="white"{}
        _Color("Tint",Color)=(1,1,1,1)
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
            struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
            struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 local:TEXCOORD1;fixed4 color:COLOR;};
            fixed4 _Color;float _Clock,_PanelMode,_HasState;float4 _ClipRect,_Size,_Cursor;float2 _Pointer,_Drag;sampler2D _InkState;
            float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float noise(float2 p){float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(hash(a),hash(a+float2(1,0)),b.x),lerp(hash(a+float2(0,1)),hash(a+1),b.x),b.y);}
            v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.local=v.vertex;o.uv=v.uv;o.color=v.color*_Color;return o;}
            fixed4 frag(v2f i):SV_Target{
                float2 uv=i.uv;
                float2 motion=tex2D(_InkState,uv).gb*_HasState;
                float2 delta=(uv-_Cursor.xy)*float2(1.8,1);
                float influence=exp(-dot(delta,delta)/.13)*_Cursor.z;
                float2 swirl=float2(-delta.y,delta.x)*influence*(.16+.35*length(_Drag));
                float2 stirred=uv+motion*.22+swirl-_Drag*influence*.035;
                float lower=pow(1-uv.y,1.8);
                float lift=lower*(.72+.28*uv.x);
                // 远景缓移、近景展开，波纹透视缩短；不是铺一张平面循环贴图。
                float2 farUv=stirred*float2(3.8,2.4)+_Pointer*.045+float2(-_Clock*.060,_Clock*.022);
                float farCloud=noise(farUv+noise(farUv*.7+_Clock*.02)*1.2);
                float2 nearUv=stirred*float2(6.5,3.2)+motion*.8+_Pointer*.13+float2(-_Clock*.10,-_Clock*.04);
                float nearCloud=noise(nearUv+float2(noise(nearUv*.6+_Clock*.045),noise(nearUv*.8-_Clock*.035))*1.7);
                float ink=smoothstep(.34,.73,nearCloud)*(.35+.65*lower);
                float3 c=lerp(float3(.035,.037,.027),float3(.58,.56,.47),saturate(lift));
                c+=(farCloud-.5)*.15;
                c=lerp(c,c*float3(.58,.59,.49),ink*.60);
                float perspective=1/max(.24,uv.y+.22);
                float2 water=float2((uv.x-.85)*perspective,log(uv.y+.25));
                float d=length(water*float2(.63,.8)+float2(0,1.3));
                float wave=pow(.5+.5*sin(d*42-_Clock*1.6+farCloud*4),12);
                c+=wave*.009*lower;
                // 以层叠云团与被鼠标带动的墨丝为主体，不用密集规则波线覆盖页面。
                float thread=pow(saturate(1-abs(nearCloud-.55)*18),3);
                c+=thread*.036*(.3+.7*lower);
                c+=smoothstep(.62,.84,farCloud)*.065*lower;
                float2 size=max(_Size.xy,1);
                float2 edge=min(uv,1-uv)*size;
                float distance=min(edge.x,edge.y);
                float edgeCloud=noise(uv*float2(9,6)+float2(_Clock*.075,-_Clock*.046));
                float fringe=smoothstep(3+edgeCloud*9,92+edgeCloud*25,distance);
                // 云雾以不同浓淡穿过软边；水纹在边缘逐渐散掉，避免框状描边。
                float mist=smoothstep(.30,.80,nearCloud)*exp(-distance/120);
                c=lerp(c,float3(.56,.55,.46),mist*.19);
                float alpha=lerp(.94,.86,lower)*fringe;
                if(_PanelMode>.5) {
                    float horizontal=smoothstep(0,30+edgeCloud*10,edge.x);
                    float upper=smoothstep(0,24+edgeCloud*8,(1-uv.y)*size.y);
                    float bottom=smoothstep(0,min(115+nearCloud*30,size.y*.38),uv.y*size.y);
                    c=float3(.040,.045,.035)+(farCloud-.5)*.018;
                    alpha=horizontal*upper*bottom*(.82+.16*nearCloud);
                }
                fixed4 result=fixed4(GammaToLinearSpace(max(c,0)),alpha)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
