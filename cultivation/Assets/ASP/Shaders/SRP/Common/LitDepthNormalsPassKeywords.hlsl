#ifndef UNITY_LIT_DEPTH_NORMAL_PASS_KEYWORDS_INCLUDED
#define UNITY_LIT_DEPTH_NORMAL_PASS_KEYWORDS_INCLUDED
#if UNITY_VERSION >= 202101 && UNITY_VERSION < 202201
                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local _NORMALMAP
                #pragma shader_feature_local _PARALLAXMAP
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
                #pragma shader_feature_local_fragment _ALPHATEST_ON
                #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

                //--------------------------------------
                // GPU Instancing
                #pragma multi_compile_instancing
                #pragma multi_compile _ DOTS_INSTANCING_ON
#elif UNITY_VERSION >= 202201 && UNITY_VERSION < 60000001 
                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local _NORMALMAP
                #pragma shader_feature_local _PARALLAXMAP
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
                #pragma shader_feature_local _ALPHATEST_ON
                #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

                // -------------------------------------
                // Unity defined keywords
                #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

                // -------------------------------------
                // Universal Pipeline keywords
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

                //--------------------------------------
                // GPU Instancing
                #pragma multi_compile_instancing
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#elif UNITY_VERSION >= 60000001
                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local _NORMALMAP
                #pragma shader_feature_local _PARALLAXMAP
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
                #pragma shader_feature_local _ALPHATEST_ON
                #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

                // -------------------------------------
                // Unity defined keywords
                #pragma multi_compile _ LOD_FADE_CROSSFADE

                // -------------------------------------
                // Universal Pipeline keywords
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

                //--------------------------------------
                // GPU Instancing
                #pragma multi_compile_instancing
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
#endif

#endif 
