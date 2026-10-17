#ifndef UNITY_LIT_META_PASS_KEYWORDS_INCLUDED
#define UNITY_LIT_META_PASS_KEYWORDS_INCLUDED
#if UNITY_VERSION >= 202101 && UNITY_VERSION < 202201
                #pragma shader_feature EDITOR_VISUALIZATION
                #pragma shader_feature_local_fragment _SPECULAR_SETUP
                #pragma shader_feature_local_fragment _EMISSION
                #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
                #pragma shader_feature_local_fragment _ALPHATEST_ON
                #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

                #pragma shader_feature_local_fragment _SPECGLOSSMAP
#elif UNITY_VERSION >= 202201 && UNITY_VERSION < 60000001
                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local_fragment _SPECULAR_SETUP
                #pragma shader_feature_local_fragment _EMISSION
                #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
                #pragma shader_feature_local_fragment _ALPHATEST_ON
                #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
                #pragma shader_feature_local_fragment _SPECGLOSSMAP
                #pragma shader_feature EDITOR_VISUALIZATION
#elif UNITY_VERSION >= 60000001
                // -------------------------------------
                // Material Keywords
                #pragma shader_feature_local_fragment _SPECULAR_SETUP
                #pragma shader_feature_local_fragment _EMISSION
                #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
                #pragma shader_feature_local_fragment _ALPHATEST_ON
                #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
                #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
                #pragma shader_feature_local_fragment _SPECGLOSSMAP
                #pragma shader_feature EDITOR_VISUALIZATION
#endif

#endif 
