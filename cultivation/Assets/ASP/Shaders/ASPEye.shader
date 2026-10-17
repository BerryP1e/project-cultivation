/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

Shader "ASP/Eye"
{
    Properties
    {
        [MaterialSpace(20)]
        [TitleKeywordEnum(EYE, _STYLE_EYE)] _eyeStyleTitle ("Toon Shading Style", Float) = 0
        [MaterialSpace(20)]
        [Main(SurfaceOptions , _, on, off)]_Group0 ("Surface Options (Eye)", float) = 0
        [MaterialSpace(10)]
        [SubToggle(SurfaceOptions, _ALPHATEST_ON)]_AlphaClip ("Alpha Clipping", Float) = 0.0
        [Sub(SurfaceOptions)]_Cutoff ("Alpha Clipping Threshold", Range(0.0, 1.0)) = 0.5
        [ActiveIf(_AlphaClip, Equal, 1)][Tex(SurfaceOptions)]_ClipMap("Clip Map", 2D) = "white" {}
        [UVChannel(SurfaceOptions)]_AlphaClipUVCtrl("Clip Map UV", Vector) = (1,0,0,0)
        [Space(5)]
        [Sub(SurfaceOptions)][ActiveIf(_AlphaClip, Equal, 1)]_Dithering("Dithering Factor", Range(0.0, 1.0)) = 0.0
        [Sub(SurfaceOptions)][ActiveIf(_AlphaClip, Equal, 1)]_DitherTexelSize ("Dither Pixel Size", Range(1, 20)) = 1
        [HideInInspector][Sub(SurfaceOptions)]_OutlineColor ("Outline Color",  Color) = (0,0,0,1)
        [HideInInspector][Sub(SurfaceOptions)]_ScaleAsScreenSpaceOutline ("Make Outline Scale As Screen Space Outline",  Range(0, 1)) = 0
        [HideInInspector][Sub(SurfaceOptions)]_OutlineWidth ("Outline Width",  Range(0, 50)) = 0.1
        [HideInInspector][Sub(SurfaceOptions)]_OutlineDistancFade (" Fade outline with near/far distance ", Vector) = (-25, 50, 0, 0)
        [SubEnum(SurfaceOptions, Opaque, 0, Transparent, 1)] _SurfaceType ("Surface Type", float) = 0
        [SubEnum(SurfaceOptions, Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2.0
        [Sub(SurfaceOptions)]_FOVShiftX ("Perspective FOV Offset", Range(0, 1)) = 0
        [MaterialSpace(10)]
        [Main(AlbedoGroup , _, off, off)]_Group1 ("Albedo/Pupil/Emission", float) = 0
        [MaterialSpace(5)]
        [UVChannel(AlbedoGroup)]_SurfaceUVCtrl("Surface textures UV", Vector) = (1,0,0,0)
        [MaterialSpace(10)]
        [Tex(AlbedoGroup, _BaseColor)] _BaseMap("Albedo", 2D) = "white" {}
        [HideInInspector][Sub(AlbedoGroup)] _BaseColor("Color", Color) = (1,1,1,1)
        [MaterialSpace(10)]
        [SubToggle(AlbedoGroup, _PARALLAX)] _EnableParallaxEffect("Enable Parallax Effect", Float) = 0.0
        [ActiveIf(_EnableParallaxEffect, Equal, 1)][Tex(AlbedoGroup)] _PupilMask("Pupil Mask", 2D) = "white" {}
        [ActiveIf(_EnableParallaxEffect, Equal, 1)][Sub(AlbedoGroup)]_ParallaxHeight("Parallax Height", Range(-1, 1)) = 0
        [SubToggle(AlbedoGroup, _EMISSION)] _EmissionToggle("Enable Emission", Float) = 0
        [ShowIf(_EmissionToggle, Equal, 1)][Tex(AlbedoGroup)]_EmissionMap("Emission", 2D) = "white" {}
        [ShowIf(_EmissionToggle, Equal, 1)][Sub(AlbedoGroup)][HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0)
        [MaterialSpace(10)]
        [Main(Lighting , _, off, off)]_Group2 ("Diffuse / Lighting Behavior", float) = 0
        [MaterialSpace(10)]
        [SubKeywordEnum(Lighting, LambertLighting, FlatLighting)] _eyeStyle ("Eye Lighting Mode", Float) = 1
        [MaterialSpace(5)]
        [ShowIf(_eyeStyle, Equal, 1)][Sub(Lighting)] _SelfUnlitAreaColor("Darkest Flat Color", Color) = (0, 0, 0)
        [MaterialSpace(5)]
        [ShowIf(_eyeStyle, Equal, 1)][SubEnum(Lighting, Use Darkest Color, 0, Use Custom Color, 1)] _ShadowColorMode ("Received Shadow Behavior", float) = 0
        [MaterialSpace(5)]
        [ShowIf(_eyeStyle, Equal, 0)][SubEnum(Lighting, Use Unity Default, 0, Use Custom Color, 1)] _LambertShadowColorMode("Received Shadow Behavior", float) = 0
        [MaterialSpace(5)]
        [ShowIf(_eyeStyle, Equal, 1)][ShowIf(_ShadowColorMode, Equal, 1)][Sub(Lighting)]_ReceivedShadowColor("Custom Received Shadow Color", Color) = (0,0,0)
        [MaterialSpace(5)]
        [ShowIf(_eyeStyle, Equal, 0)][ShowIf(_LambertShadowColorMode, Equal, 1)][Sub(Lighting)]_LambertReceivedShadowColor("Custom Received Shadow Color", Color) = (0,0,0)
        [MaterialSpace(10)]
        [SubEnum(Lighting, SampleSH, 0, SampleSH_Flatten, 1, Color, 2)] _BakeGISource ("Indirect GI Source", float) = 0
        [ActiveIf(_BakeGISource, Equal, 2)][Sub(Lighting)]_OverrideGIColor("Override GI Color", Color) = (0,0,0)
        [MaterialSpace(10)]
        [SubToggle(Lighting, _FlattenAdditionalLighting)] _FlattenAdditionalLighting("Flatten Additional Lighting", Float) = 0
        [HideInInspector][SubToggle(Lighting, _OverrideLightDirToggle)] _OverrideLightDirToggle("Override Light Dir", Float) = 0
        [HideInInspector][ActiveIf(_OverrideLightDirToggle, Equal, 1)][Sub(Lighting)] _FakeLightEuler("Fake Light Euler Angles", Vector) = (0,0,0)
        [HideInInspector][Sub(Lighting)]_OverrideLightColorIntensityToggle("Override Light Color And Intensity Toggle", Float) = 0
        [HideInInspector][Sub(Lighting)]_FakeLightColor("Override Light Color", Color) = (1,1,1)
        [HideInInspector][Sub(Lighting)]_FakeLightIntensity("Override Light Color", Float) = 1.0
        [SubToggleOff(Lighting, _ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [MaterialSpace(10)]
        
        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0
        
        [MaterialSpace(10)]
        [Main(MatCap , _, off, off)]_GroupMatCap ("MatCap Reflection", float) = 0
        [HideInInspector]_IsUsingMatcapReflectMap("_IsUsingMatcapReflectMap", float) = 0
        [MaterialSpace(10)]
        [Tex(MatCap)] _MatCapReflectionMap("MatCap reflection map", 2D) = "Black" {}
        [MaterialSpace(10)]
        [ActiveIf(_IsUsingMatcapReflectMap, Equal, 1)][SubToggle(MatCap, _UsePupilMask)] _UsePupilMask("Mask MatCap Reflection By Pupil Mask", Float) = 1.0
        [ActiveIf(_IsUsingMatcapReflectMap, Equal, 1)]
        [Sub(MatCap)] _MatCapReflectionStrength("MatCap Reflection Strength", Range(0,2)) = 1
        [ActiveIf(_IsUsingMatcapReflectMap, Equal, 1)]
        [Sub(MatCap)] _MatCapRollStabilize("MatCap Camera Roll Stabilize", Range(0,1)) = 1
        [MaterialSpace(10)]
        [Tex(MatCap)][Normal] _BumpMap("Normal Map For Matcap", 2D) = "bump" {}
        [HideInInspector][Sub(MatCap)]_HasMatCapNormal ("Has MatCap Normal Map", float) = 0
        [ActiveIf(_HasMatCapNormal, Equal, 1)][Sub(MatCap)] _BumpScale("MatCap Normal Map Scale", Range(0, 1.0)) = 0.5
        
        [MaterialSpace(10)]
        [Main(Hightlight1 , _, off, off)]_GroupHighlightLayer1 ("Highlights", float) = 0
        [MaterialSpace(10)]
        [SubToggle(Hightlight1, _HighLightAlphaClip)] _HighLightAlphaClip("Is Highlight Texture Alpha Clip", Float) = 1.0
        [SubToggle(Hightlight1, _HighlightDarken)] _HighlightDarken("Darken Emission along light direction", Float) = 1.0
        [Tex(Hightlight1, _HighLightAlphaClip)] _EyeHighlightMap1("Top Highlight Map", 2D) = "white" {}
        [Sub(Hightlight1)] _EyeHighlightRotateDegree("Rotate Degree Along Half Vector", Range(0, 90)) = 0
        [HDR][Sub(Hightlight1)] _EyeHighlightColor("Top Highlight Color Tint", Color) = (1,1,1,1)
        
        [MaterialSpace(10)]
        [Main(Shadow , _, on, off)]_Group7 ("Shadow", float) = 0
        [MaterialSpace(10)]
        // TODO _RECEIVE_SHADOWS_OFF now integrated now
        [SubToggle(Shadow, _ReceiveShadows)] _ReceiveShadows("Receive Built-In Shadows", Float) = 1.0
        [SubToggle(Shadow, _RECEIVE_ASP_SHADOW)] _ReceiveASPShadow ("Receive Extra Shadow From ASP ShadowMap", float) = 0
        [SubToggle(Shadow, _RECEIVE_OFFSETED_SHADOW_ON)] _ReceiveOffsetedDepthMap ("Receive Extra Shadow From Offsetted Depth Map", float) = 0
        [ActiveIf(_ReceiveOffsetedDepthMap, Equal, 1)][Sub(Shadow)]_OffsetShadowDistance("Depth Offset Shadow Pixel Distance", Range(5, 30)) = 15
        
        // Blending state
        [MaterialSpace(10)]
        [Main(Advance , _, on, off)]_AdvanceGroup ("Advance Setup (ZTest/Stencil", float) = 0
        [Sub(Advance)]_Stencil ("Stencil ID", Float) = 0
        [SubEnum(Advance, UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare Function", Float) = 4 // 4 is LEqual
        [SubEnum(Advance, UnityEngine.Rendering.StencilOp)]_StencilOp ("Stencil Pass Operation", Float) = 0
        [SubEnum(Advance, UnityEngine.Rendering.StencilOp)]_StencilFailOp ("Stencil Fail Operation", Float) = 0
        [SubToggle(Advance, _OverrideZTest)] _OverrideZTest("Override ZTest", Float) = 1.0
        [ActiveIf(_OverrideZTest, Equal, 1)][SubEnum(Advance, UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        
        [HideInInspector] _CharacterCenterCubeSize("_CharacterCenterCubeSize", Float) = 1
        [HideInInspector] _UseSimpleAABBCutOffForCharacterShadow("_UseSimpleAABBCutOffForCharacterShadow", float) = 1.0
        [HideInInspector]_MaterialID("_MaterialID", float) = 0
        [HideInInspector]_FaceFrontDirection("_FaceFrontDirection", Vector) = (1,0,0)
        [HideInInspector]_FaceRightDirection("_FaceRightDirection", Vector) = (0,0,1)
        [HideInInspector]_CharacterCenterWS("_CharacterCenterWS", Vector) = (0,0,1)
        
        [HideInInspector]_StandardBrdfLut("BrdfLut Map", 2D) = "black" {}
        [HideInInspector]_Blend("__blend", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        // Editmode props
       // _QueueOffset("Queue offset", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300
           
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags { "RenderPipeline" = "UniversalPipeline" "LightMode" = "UniversalForwardOnly"}

            // -------------------------------------
            // Render State Commands
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            //AlphaToMask[_AlphaToMask]
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
            }       

            HLSLPROGRAM
            #pragma target 4.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ASPEyeLitVert
            #pragma fragment ASPEyeLitFrag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _STYLE_LAMBERTLIGHTING _STYLE_FLATLIGHTING
            #pragma shader_feature_local_fragment _PARALLAX
            #pragma shader_feature_local_fragment _MATCAP_HIGHLIGHT_MAP
            #pragma shader_feature_local_fragment _EYE_HIGHLIGHT_MAP
            #pragma shader_feature_local_fragment _RECEIVE_ASP_SHADOW
            #pragma shader_feature_local_fragment _RECEIVE_OFFSETED_SHADOW_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            /*
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED*/

            // -------------------------------------
            // Universal Pipeline keywords
            //#pragma multi_compile _ MAIN_LIGHT_CALCULATE_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            
            #if UNITY_VERSION >= 202201
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #else
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #endif
            
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            //   we don't really need box projection for anime character in real world use case...
            //#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #if UNITY_VERSION > 202201
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #endif

            #if UNITY_VERSION >= 202301
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #else
            #include "SRP/FoveatedRenderingKeywords.hlsl"
            #endif

            // -------------------------------------
            // Unity defined keywords
            /*
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            */
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fog

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include "ShaderLibrary/ASPEyeLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "OutlineObject" "RenderType" = "Opaque" }
            
            Cull Front
            HLSLPROGRAM

            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #include "ShaderLibrary/ASPOutline.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ASPShadowCaster"
            Tags
            {
                "LightMode" = "ASPShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex 
            #pragma fragment ShadowPassFragment 

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            //#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Includes
            #include "ShaderLibrary/ASPLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "ShaderLibrary/ASPShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ASPMaterialPass"
            Tags
            {
                "LightMode" = "ASPMaterialPass"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On 
            ZTest LEqual
            //Cull Back
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
            }
            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex MaterialPassVertex 
            #pragma fragment MaterialPassFragment 

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            
            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            //#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Includes
            #include "ShaderLibrary/ASPCommon.hlsl"
            #include "ShaderLibrary/ASPLitInput.hlsl"
            #include "ShaderLibrary/ASPMaterialPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            // -------------------------------------
            // Universal Pipeline keywords

            // -------------------------------------
            // Unity defined keywords
            //#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // -------------------------------------
            // Includes
            #include "ShaderLibrary/ASPLitInput.hlsl"
           // #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
            }
            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            //#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            // -------------------------------------
            // Includes
            #include "ShaderLibrary/ASPLitInput.hlsl"
            #include "ShaderLibrary/ASPDepthOnlyPass.hlsl"
            ENDHLSL
        }

        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags
            {
                "LightMode" = "DepthNormals"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            Cull[_Cull]
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
            }
            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            //#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Includes
            #include "ShaderLibrary/ASPLitInput.hlsl"
            #include "ShaderLibrary/ASPDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "LWGUI.ASP.ASPEyesGUI"
}
