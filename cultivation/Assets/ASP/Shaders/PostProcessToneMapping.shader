/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

Shader "Hidden/ASP/PostProcess/ToneMapping"
{
	SubShader
    {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
            LOD 100

            ZWrite Off
            Cull Off
            ZTest Always
        Pass
        {
            Name "GT ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            
            static const float e = 2.71828;

			float W_f(float x,float e0,float e1) {
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				float a = (x - e0) / (e1 - e0);
				return a * a*(3 - 2 * a);
			}
			float H_f(float x, float e0, float e1) {
				if (x <= e0)
					return 0;
				if (x >= e1)
					return 1;
				return (x - e0) / (e1 - e0);
			}

			float GranTurismoTonemapper(float x) {
				float P = 1;
				float a = 1;
				float m = 0.22;
				float l = 0.4;
				float c = 1.33;
				float b = 0;
				float l0 = (P - m)*l / a;
				float L0 = m - m / a;
				float L1 = m + (1 - m) / a;
				float L_x = m + a * (x - m);
				float T_x = m * pow(x / m, c) + b;
				float S0 = m + l0;
				float S1 = m + a * l0;
				float C2 = a * P / (P - S1);
				float S_x = P - (P - S1)*pow(e,-(C2*(x-S0)/P));
				float w0_x = 1 - W_f(x, 0, m);
				float w2_x = H_f(x, m + l0, m + l0);
				float w1_x = 1 - w0_x - w2_x;
				float f_x = T_x * w0_x + L_x * w1_x + S_x * w2_x;
				return f_x;
			}
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float _IgnoreCharacterPixels;

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(sceneDepth, characterDepth);
            	
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return col;
            	}

                float r = GranTurismoTonemapper(col.r);
				float g = GranTurismoTonemapper(col.g);
				float b = GranTurismoTonemapper(col.b);
				half4 toneMappedCol = half4(r,g,b,col.a);
                return toneMappedCol;
            }
            ENDHLSL
        }

		Pass
        {
            Name "Filmic ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            
            static const float e = 2.71828;

			float3 reinhard_jodie(float3 v)
			{
			    float l = Luminance(v);
			    float3 tv = v / (1.0f + v);
			    return lerp(v / (1.0f + l), tv, tv);
			}
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

           float3 F(float3 x)
			{
				const float A = 0.22f;
				const float B = 0.30f;
				const float C = 0.10f;
				const float D = 0.20f;
				const float E = 0.01f;
				const float F = 0.30f;
			 
				return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
			}

			float3 Uncharted2ToneMapping(float3 color, float adapted_lum)
			{
				const float WHITE = 11.2f;
				return F(1.6f * adapted_lum * color) / F(WHITE);
			}

            float _IgnoreCharacterPixels;
			float _Exposure;
            float _ToneMapLowerBound;
            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(LinearEyeDepth(characterDepth, _ZBufferParams), ASP_DEPTH_EYE_BIAS + LinearEyeDepth(sceneDepth, _ZBufferParams));
            	
				half4 toneMappedCol = half4(Uncharted2ToneMapping(col.rgb, _Exposure),col.a);
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return lerp(col, toneMappedCol, pow(saturate(_ToneMapLowerBound*1.2), 0.5));
            	}
            	
                return toneMappedCol;
            }
            ENDHLSL
        }

		Pass
        {
            Name "Neurtral ToneMapping"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "ShaderLibrary/ASPCommon.hlsl"
            
            #pragma vertex Vert
            #pragma fragment frag
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            float3 PBRNeutralToneMapping( float3 color ) {
			  const float startCompression = 0.8 - 0.04;
			  const float desaturation = 0.15;

			  float x = min(color.r, min(color.g, color.b));
			  float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
			  color -= offset;

			  float peak = max(color.r, max(color.g, color.b));
			  if (peak < startCompression) return color;

			  const float d = 1. - startCompression;
			  float newPeak = 1. - d * d / (peak + d - startCompression);
			  color *= newPeak / peak;

			  float g = 1. - 1. / (desaturation * (peak - newPeak) + 1.);
			  return lerp(color, newPeak * float3(1, 1, 1), g);
			}

            float _IgnoreCharacterPixels;
			float _Exposure;
            float _ToneMapLowerBound;
            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.texcoord);
            	float characterDepth = SampleCharacterSceneDepth(input.texcoord);
            	float sceneDepth = SampleSceneDepth(input.texcoord);
            	float isSkipToneMapCharacter = step(0.1, _IgnoreCharacterPixels) * step(LinearEyeDepth(characterDepth, _ZBufferParams), ASP_DEPTH_EYE_BIAS + LinearEyeDepth(sceneDepth, _ZBufferParams));
            	
				half4 toneMappedCol = half4(PBRNeutralToneMapping(col.rgb * _Exposure), col.a);
            	if(isSkipToneMapCharacter * SampleMateriaPass(input.texcoord).r > 0)
            	{
            		return lerp(col, toneMappedCol, pow(saturate(_ToneMapLowerBound*1.2), 0.5));
            	}
            	
                return toneMappedCol;
            }
            ENDHLSL
        }
    }
}