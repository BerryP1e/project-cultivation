Shader "Cultivation/WorldAuthoring/InkMask"
{
    Properties
    {
        _BaseMap("Alpha source",2D)="white"{}
        _Cutoff("Alpha clip",Float)=0
        _Cull("Cull",Float)=2
        _ID("Object group",Float)=.25
        _CamFar("Camera far",Float)=100
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline"}
        Pass
        {
            ZWrite On ZTest LEqual Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;float _Cutoff,_Cull,_ID,_CamFar;
            CBUFFER_END
            struct Attributes{float4 position:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings{float4 position:SV_POSITION;float3 normal:TEXCOORD0;float distance:TEXCOORD1;float2 uv:TEXCOORD2;};
            struct Mask{half4 normals:SV_Target0;half4 id:SV_Target1;};
            Varyings Vert(Attributes input){Varyings o;float3 world=TransformObjectToWorld(input.position.xyz);o.position=TransformWorldToHClip(world);o.normal=TransformObjectToWorldNormal(input.normal);o.distance=length(world-GetCameraPositionWS());o.uv=TRANSFORM_TEX(input.uv,_BaseMap);return o;}
            Mask Frag(Varyings i){clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-_Cutoff);Mask o;float3 normal=mul((float3x3)UNITY_MATRIX_V,normalize(i.normal));o.normals=half4(normal*.5+.5,saturate(i.distance/max(.001,_CamFar)));o.id=half4(_ID,0,0,1);return o;}
            ENDHLSL
        }
    }
    SubShader
    {
        Pass
        {
            ZWrite On ZTest LEqual Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            sampler2D _BaseMap;float4 _BaseMap_ST;float _Cutoff,_ID,_CamFar;
            float _RootHeight,_RootFade;
            struct Input{float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct Output{float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float distance:TEXCOORD1;float2 uv:TEXCOORD2;float worldY:TEXCOORD3;};
            struct Mask{half4 normals:SV_Target0;half4 id:SV_Target1;};
            Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);float3 world=mul(unity_ObjectToWorld,v.vertex).xyz;o.worldY=world.y;o.normal=UnityObjectToWorldNormal(v.normal);o.distance=-mul(UNITY_MATRIX_MV,v.vertex).z;o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
            Mask frag(Output i){clip(tex2D(_BaseMap,i.uv).a-_Cutoff);Mask o;float3 normal=mul((float3x3)UNITY_MATRIX_V,normalize(i.normal));o.normals=half4(normal*.5+.5,saturate(i.distance/max(.001,_CamFar)));o.id=half4(_ID,_RootFade>0?smoothstep(0,_RootFade,i.worldY-_RootHeight):1,0,1);return o;}
            ENDCG
        }
    }
}
