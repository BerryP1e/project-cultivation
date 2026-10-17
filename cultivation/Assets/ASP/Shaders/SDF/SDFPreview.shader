Shader "ASP/Editor/SDFPreview"
{
    Properties
    {
        _MainTex0 ("Texture", 2D) = "white" {}
        _thread ("Thread", Range(0,1)) = 0.5
	    _delta("delta", Range(0,0.05)) = 0.01

    }
    SubShader
    {

        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag


            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD1;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
             TEXTURE2D(_MainTex0);
             SAMPLER(sampler_MainTex0);
            
			float _thread;
			float _delta;

            float4 frag (v2f i) : SV_Target
            {
				float4 col0 = SAMPLE_TEXTURE2D(_MainTex0, sampler_MainTex0, i.uv);
                float r = 1-smoothstep(col0.r-_delta,col0.r+_delta, _thread);
                return float4(r,r,r, 1);
            }
            ENDHLSL
        }
    }
}