Shader "Cultivation/WorldAuthoring/Foliage"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white"{}
        _BaseColor("Base tint",Color)=(1,1,1,1)
        _Cutoff("Alpha clip",Range(0,1))=.4
        _Cull("Cull",Float)=0
        _WindStrength("Wind",Float)=0
        _TextureStrength("Texture influence",Range(0,1))=1
        _TextureContrast("Texture contrast",Range(0,1))=1
        _TextureSaturation("Texture saturation",Range(0,1))=1
        _TextureExposure("Texture exposure",Range(.1,4))=1
        _DetailSoftness("Mip bias",Range(0,4))=0
        _CoverageWidth("Coverage edge width",Range(.5,2))=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        UsePass "Cultivation/WorldAuthoring/Surface/ForwardLit"
        UsePass "Cultivation/WorldAuthoring/Surface/ShadowCaster"
        UsePass "Cultivation/WorldAuthoring/Surface/DepthOnly"
    }
    SubShader
    {
        Tags {"RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull [_Cull]
        AlphaToMask On
        // 阴影/相机深度仍使用原始裁剪轮廓；不能继承覆盖率或表面着色器的零alpha阴影输出。
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ZTest LEqual AlphaToMask Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _BaseMap;float4 _BaseMap_ST;float _Cutoff,_WindStrength;
            struct Output {V2F_SHADOW_CASTER;float2 uv:TEXCOORD1;};
            Output vert(appdata_base v)
            {
                UNITY_SETUP_INSTANCE_ID(v);Output o;
                float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;
                v.vertex.x+=sin(_Time.y*1.8+world.x*.7+world.z*.4)*max(0,v.vertex.y)*_WindStrength;
                o.uv=TRANSFORM_TEX(v.texcoord,_BaseMap);TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);return o;
            }
            float4 frag(Output i):SV_Target{clip(tex2D(_BaseMap,i.uv).a-_Cutoff);SHADOW_CASTER_FRAGMENT(i)}
            ENDCG
        }
        CGPROGRAM
        #pragma surface surf WorldToon fullforwardshadows vertex:move keepalpha exclude_path:deferred
        #pragma target 3.5
        #pragma multi_compile_instancing
        #include "UnityCG.cginc"
        sampler2D _BaseMap;fixed4 _BaseColor;
        float _Cutoff,_WindStrength,_TextureStrength,_TextureContrast,_TextureSaturation,_TextureExposure,_DetailSoftness;
        float _CoverageWidth,_WorldFoliageCoverage;
        struct Input{float2 uv_BaseMap;};
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
            o.Albedo=_BaseColor.rgb*lerp(1,tex.rgb,_TextureStrength);
            if(_WorldFoliageCoverage>.5)
            {
                float coverage=saturate((tex.a-_Cutoff)/max(fwidth(tex.a)*_CoverageWidth,.0001)+.5);
                clip(coverage-1.0/255);o.Alpha=coverage;
            }
            else{clip(tex.a-_Cutoff);o.Alpha=1;}
        }
        ENDCG
    }
    Fallback Off
}
