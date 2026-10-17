Shader "Cultivation/WorldAuthoring/PlacementPreview"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white"{}
        _BaseColor("Source tint",Color)=(1,1,1,1)
        _GhostTint("Placement state",Color)=(.72,1,.8,.62)
        _Cutoff("Source alpha clip",Float)=0
        _Cull("Source cull",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+40" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull [_Cull]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST,_BaseColor,_GhostTint;float _Cutoff,_Cull;
            CBUFFER_END
            struct Attributes{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings{float4 position:SV_POSITION;float3 normal:TEXCOORD0;float2 uv:TEXCOORD1;};
            Varyings Vert(Attributes input){Varyings o;o.position=TransformObjectToHClip(input.position.xyz);o.normal=TransformObjectToWorldNormal(input.normal);o.uv=TRANSFORM_TEX(input.uv,_BaseMap);return o;}
            half4 Frag(Varyings i):SV_Target{half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(tex.a-_Cutoff);float light=.55+.45*saturate(dot(normalize(i.normal),normalize(float3(-.4,1,-.3))));return half4(tex.rgb*_BaseColor.rgb*_GhostTint.rgb*light,_GhostTint.a);}
            ENDHLSL
        }
    }
}
