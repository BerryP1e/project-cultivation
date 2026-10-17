Shader "Cultivation/WorldAuthoring/Surface"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white"{}
        _BaseColor("Base tint",Color)=(.75,.73,.68,1)
        _GrassColor("Grass soil",Color)=(.22,.35,.14,1)
        _GrassMap("Painted grass soil texture",2D)="white"{}
        _GrassMapExposure("Grass soil texture exposure",Float)=1
        _GrassMapSaturation("Grass soil texture saturation",Range(0,1))=.65
        _GrassBlend("Use painted grass channel",Float)=0
        _PaintControl("Ground paint control",2D)="black"{}
        _TerrainLayers("Ground paint textures",2DArray)=""{}
        _PaintRect("Ground paint coordinates",Vector)=(0,0,1,1)
        _PaintEnabled("Ground paint enabled",Float)=0
        _TextureStrength("Texture influence",Range(0,1))=1
        _TextureContrast("Texture contrast",Range(0,1))=1
        _TextureSaturation("Texture saturation",Range(0,1))=1
        _TextureExposure("Texture exposure",Range(.1,4))=1
        _DetailSoftness("Painted detail softness",Range(0,4))=0
        _NoiseStrength("Broad dirt variation",Range(0,.3))=.09
        _Cutoff("Cutout",Range(0,1))=0
        _Cull("Cull",Float)=2
        _WindStrength("Wind",Float)=0
        _BoundaryBlend("Soil at stone edge",Range(0,1))=0
        _RockBlend("Dust and moss at stone root",Range(0,1))=0
        _RockRootHeight("Stone root elevation",Float)=-999
        _RootMoss("Local moss",Range(0,1))=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull [_Cull]
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WorldAuthoringSurfaceInput.hlsl"
            struct Input {float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Output {float4 position:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float4 color:COLOR;float fog:TEXCOORD3;};
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Output o;
                v.position.xyz=WorldBrushWind(v.position.xyz);
                o.world=TransformObjectToWorld(v.position.xyz);o.position=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(v.normal);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.color=v.color;
                o.fog=ComputeFogFactor(o.position.z);return o;
            }
            half4 frag(Output i):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D_BIAS(_BaseMap,sampler_BaseMap,i.uv,_DetailSoftness);clip(tex.a-_Cutoff);
                tex.rgb=lerp(dot(tex.rgb,half3(.2126,.7152,.0722)),tex.rgb,_TextureSaturation);
                tex.rgb=lerp(.5,tex.rgb,_TextureContrast)*_TextureExposure;
                float painted=smoothstep(.015,.45,i.color.r)*_GrassBlend;
                half3 grassTex=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,i.world.xz*_GrassMap_ST.xy+_GrassMap_ST.zw).rgb;
                grassTex=lerp(dot(grassTex,half3(.2126,.7152,.0722)),grassTex,_GrassMapSaturation)*_GrassMapExposure;
                half3 base=lerp(_BaseColor.rgb*lerp(1,tex.rgb,_TextureStrength),_GrassColor.rgb*grassTex,painted);
                base=WorldGroundPaint(base,i.world.xz);
                float dirt=sin(i.world.x*.47+sin(i.world.z*.21)*2)*sin(i.world.z*.31)+sin(i.world.x*.93+i.world.z*.69)*.22;
                base*=1+dirt*_NoiseStrength;
                base*=1-i.color.g*_BoundaryBlend*(.08+.025*dirt);
                float root=(1-smoothstep(-.08,.62,i.world.y-_RockRootHeight+dirt*.10))*_RockBlend;
                base=lerp(base,half3(.34,.32,.27),root*.48);
                float moss=root*_RootMoss*saturate(i.normal.y)*smoothstep(-.15,.65,dirt)*.36;
                base=lerp(base,half3(.09,.15,.075),moss);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.world));
                float n=saturate(dot(normalize(i.normal),light.direction));
                float planes=lerp(.22,1,smoothstep(.18,.60,n));
                half3 ambient=max(half3(.10,.12,.15),SampleSH(normalize(i.normal))*.58);
                half3 colour=base*(ambient+light.color*planes*lerp(.27,1,light.shadowAttenuation)*.78);
                return half4(MixFog(colour,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "WorldAuthoringSurfaceInput.hlsl"
            float3 _LightDirection,_LightPosition;
            struct Input{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Output{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);Output o;
                float3 world=TransformObjectToWorld(WorldBrushWind(v.position.xyz));
                float3 normal=TransformObjectToWorldNormal(v.normal);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-world);
                #else
                float3 direction=_LightDirection;
                #endif
                o.position=TransformWorldToHClip(ApplyShadowBias(world,normal,direction));
                #if UNITY_REVERSED_Z
                o.position.z=min(o.position.z,UNITY_NEAR_CLIP_VALUE);
                #else
                o.position.z=max(o.position.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;
            }
            half4 frag(Output i):SV_Target {clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-_Cutoff);return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WorldAuthoringSurfaceInput.hlsl"
            struct Input{float4 position:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Output{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
            Output vert(Input v){UNITY_SETUP_INSTANCE_ID(v);Output o;o.position=TransformObjectToHClip(WorldBrushWind(v.position.xyz));o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
            half4 frag(Output i):SV_Target{clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-_Cutoff);return i.position.z;}
            ENDHLSL
        }
    }
    SubShader
    {
        Tags {"RenderType"="Opaque" "Queue"="Geometry"}
        Cull [_Cull]
        CGPROGRAM
        #pragma surface surf WorldToon fullforwardshadows vertex:move alphatest:_Cutoff addshadow
        #pragma target 3.5
        #pragma multi_compile_instancing
        #include "UnityCG.cginc"
        sampler2D _BaseMap;
        sampler2D _GrassMap;
        sampler2D _PaintControl;
        UNITY_DECLARE_TEX2DARRAY(_TerrainLayers);
        float4 _PaintRect;
        float _PaintEnabled;
        #define WORLD_PAINT_CONTROL(uv) tex2Dlod(_PaintControl,float4(uv,0,0))
        #define WORLD_PAINT_LAYER(uv,layer) UNITY_SAMPLE_TEX2DARRAY(_TerrainLayers,float3(uv,layer))
        #include "WorldAuthoringPaint.hlsl"
        float4 _GrassMap_ST;
        fixed4 _BaseColor,_GrassColor;
        float _GrassBlend,_TextureStrength,_NoiseStrength,_WindStrength;
        float _TextureContrast,_TextureSaturation,_TextureExposure;
        float _DetailSoftness;
        float _BoundaryBlend,_RockBlend,_RockRootHeight,_RootMoss;
        float _GrassMapExposure,_GrassMapSaturation;
        struct Input {float2 uv_BaseMap;float3 worldPos;float4 color:COLOR;};
        void move(inout appdata_full v)
        {
            float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
            v.vertex.x+=sin(_Time.y*1.8+world.x*.7+world.z*.4)*max(0,v.vertex.y)*_WindStrength;
        }
        half4 LightingWorldToon(SurfaceOutput s,half3 lightDir,half atten)
        {
            half n=saturate(dot(s.Normal,lightDir));half planes=lerp(.4,1,smoothstep(.14,.38,n));
            return half4(s.Albedo*_LightColor0.rgb*planes*atten,s.Alpha);
        }
        void surf(Input i,inout SurfaceOutput o)
        {
            fixed4 tex=tex2Dbias(_BaseMap,float4(i.uv_BaseMap,0,_DetailSoftness));
            tex.rgb=lerp(dot(tex.rgb,float3(.2126,.7152,.0722)),tex.rgb,_TextureSaturation);
            tex.rgb=lerp(.5,tex.rgb,_TextureContrast)*_TextureExposure;
            float painted=smoothstep(.015,.45,i.color.r)*_GrassBlend;
            float dirt=sin(i.worldPos.x*.47+sin(i.worldPos.z*.21)*2)*sin(i.worldPos.z*.31)+sin(i.worldPos.x*.93+i.worldPos.z*.69)*.22;
            float3 grassTex=tex2D(_GrassMap,i.worldPos.xz*_GrassMap_ST.xy+_GrassMap_ST.zw).rgb;
            grassTex=lerp(dot(grassTex,float3(.2126,.7152,.0722)),grassTex,_GrassMapSaturation)*_GrassMapExposure;
            o.Albedo=WorldGroundPaint(lerp(_BaseColor.rgb*lerp(1,tex.rgb,_TextureStrength),_GrassColor.rgb*grassTex,painted),i.worldPos.xz)*(1+dirt*_NoiseStrength);
            o.Albedo*=1-i.color.g*_BoundaryBlend*(.08+.025*dirt);
            float root=(1-smoothstep(-.08,.62,i.worldPos.y-_RockRootHeight+dirt*.10))*_RockBlend;
            o.Albedo=lerp(o.Albedo,float3(.34,.32,.27),root*.48);
            o.Alpha=tex.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
