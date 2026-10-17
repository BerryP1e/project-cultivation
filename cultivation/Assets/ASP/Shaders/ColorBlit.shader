Shader "ColorBlit"
{
    Properties
    {
        _Intensity("_Intensity", Range(0,1)) = 0
    }
    SubShader
{
    Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
    LOD 100
    ZWrite Off Cull Off
    Pass
    {
        Name "ColorBlitPass"
 
        HLSLPROGRAM
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
 
        #pragma vertex Vert
        #pragma fragment frag
 
 
        float _Intensity;
        TEXTURE2D_X(_BaseMap);
        
 
        half4 frag (Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_PointClamp, input.texcoord);
            return float4(1.0 - color.rgb, 1);
        }
        ENDHLSL
    }
}
}