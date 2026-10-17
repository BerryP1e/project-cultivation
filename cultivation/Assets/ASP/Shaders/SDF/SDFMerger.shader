Shader "Hidden/SDFMerger"
{
    Properties
    {
        _MainTex0 ("Texture", 2D) = "white" {}
        _MainTex1 ("Texture", 2D) = "white" {}
        _MainTex2 ("Texture", 2D) = "white" {}
        _MainTex3 ("Texture", 2D) = "white" {}
        _MainTex4 ("Texture", 2D) = "white" {}
        _MainTex5 ("Texture", 2D) = "white" {}
        _MainTex6 ("Texture", 2D) = "white" {}
        _MainTex7 ("Texture", 2D) = "white" {}
        _MainTex8 ("Texture", 2D) = "white" {}
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
                float2 uv : TEXCOORD0;
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
            
             TEXTURE2D(_MainTex0);            SAMPLER(sampler_MainTex0);
             TEXTURE2D(_MainTex1);            SAMPLER(sampler_MainTex1);
             TEXTURE2D(_MainTex2);            SAMPLER(sampler_MainTex2);
             TEXTURE2D(_MainTex3);            SAMPLER(sampler_MainTex3);
             TEXTURE2D(_MainTex4);            SAMPLER(sampler_MainTex4);
             TEXTURE2D(_MainTex5);            SAMPLER(sampler_MainTex5);
             TEXTURE2D(_MainTex6);            SAMPLER(sampler_MainTex6);
             TEXTURE2D(_MainTex7);            SAMPLER(sampler_MainTex7);
             TEXTURE2D(_MainTex8);            SAMPLER(sampler_MainTex8);
            
			float _thread;
			float _delta;
            uint _imgNum;

            float4 frag (v2f i) : SV_Target
            {
				float4 col0 = SAMPLE_TEXTURE2D(_MainTex0, sampler_MainTex0, i.uv);
				float4 col1 = SAMPLE_TEXTURE2D(_MainTex1, sampler_MainTex1, i.uv);
                float4 col2 = SAMPLE_TEXTURE2D(_MainTex2, sampler_MainTex2, i.uv);
                float4 col3 = SAMPLE_TEXTURE2D(_MainTex3, sampler_MainTex3, i.uv);
                float4 col4 = SAMPLE_TEXTURE2D(_MainTex4, sampler_MainTex4, i.uv);
                float4 col5 = SAMPLE_TEXTURE2D(_MainTex5, sampler_MainTex5, i.uv);
                float4 col6 = SAMPLE_TEXTURE2D(_MainTex6, sampler_MainTex6, i.uv);
                float4 col7 = SAMPLE_TEXTURE2D(_MainTex7, sampler_MainTex7, i.uv);
                float4 col8 = SAMPLE_TEXTURE2D(_MainTex8, sampler_MainTex8, i.uv);

                float cols[9];

                cols[0] = col0.r;
                cols[1] = col1.r;
                cols[2] = col2.r;
                cols[3] = col3.r;
                cols[4] = col4.r;
                cols[5] = col5.r;
                cols[6] = col6.r;
                cols[7] = col7.r;
                cols[8] = col8.r;
                
                uint imgNum = max(_imgNum, 2);
                uint imgIndexMax = imgNum-1;

                float4 color2 = float4(0, 0, 0, 1);
                for (float j = 1; j <= 256.0; j++)
                {
                    //_thread2 range = 0 ~ 1
                    float _thread2 = j / 256.0;
                    int index = min(floor(_thread2 * imgIndexMax), imgIndexMax-1);

                    float r = lerp(cols[index], cols[index+1], _thread2 * imgIndexMax - index);
                    r = smoothstep(0.5 - 0.005,0.5 + 0.005, r);
                    float4 tmp_color = float4(r, r, r, 1);
                    color2 = ( (j-1) * color2 + tmp_color ) / j;
                }
                return float4(color2.rgb, 1);
            }
            ENDHLSL
        }
    }
}