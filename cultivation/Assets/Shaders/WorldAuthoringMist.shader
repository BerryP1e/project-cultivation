Shader "Cultivation/WorldAuthoring/ShoreMist"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline"}
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_ShoreField);SAMPLER(sampler_ShoreField);
            TEXTURE3D(_NoiseVolume);SAMPLER(sampler_NoiseVolume);
            float WorldMistSampleNoise(float3 p){return SAMPLE_TEXTURE3D_LOD(_NoiseVolume,sampler_NoiseVolume,p,0).r;}
            float2 WorldMistSampleField(float2 uv){return SAMPLE_TEXTURE2D_LOD(_ShoreField,sampler_ShoreField,uv,0).rg;}
            #include "WorldMistVolume.hlsl"
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.texcoord;
                half4 scene=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 end=ComputeWorldSpacePosition(uv,depth,_MistInverseViewProjection);
                return WorldMistComposite(scene,end,input.positionCS.xy);
            }
            ENDHLSL
        }
    }
}
