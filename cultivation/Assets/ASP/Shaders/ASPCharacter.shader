/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

Shader "ASP/Character"
{
    Properties
    {
        [MaterialSpace(20)]
        [TitleKeywordEnum(StylizePBR, _STYLE_STYLIZEPBR, CelShading, _STYLE_CELSHADING, Face, _STYLE_FACE)] _style ("Toon Shading Style", Float) = 1
        [MaterialSpace(20)]
        [Main(SurfaceOptions , _, on, off)]_Group0 ("Surface Options", float) = 1
        [MaterialSpace(10)]
        [SubToggle(SurfaceOptions, _ALPHATEST_ON)]_AlphaClip ("Alpha Clipping", Float) = 0.0
        [Sub(SurfaceOptions)]_Cutoff ("Alpha Clipping Threshold", Range(0.0, 1.0)) = 0.5
        [ActiveIf(_AlphaClip, Equal, 1)][Tex(SurfaceOptions)]_ClipMap("Clip Map", 2D) = "white" {}
        [UVChannel(SurfaceOptions)]_AlphaClipUVCtrl("Clip Map UV", Vector) = (1,0,0,0)
        [Space(5)]
        [Sub(SurfaceOptions)][ActiveIf(_AlphaClip, Equal, 1)]_Dithering("Dithering Factor", Range(0.0, 1.0)) = 0.0
        [Sub(SurfaceOptions)][ActiveIf(_AlphaClip, Equal, 1)]_DitherTexelSize ("Dither Pixel Size", Range(1, 20)) = 1
        
        [HideInInspector]_OutlineColor ("Outline Color",  Color) = (0,0,0,1)
        [HideInInspector]_ScaleAsScreenSpaceOutline ("Make Outline Scale As Screen Space Outline",  Range(0, 1)) = 0
        [HideInInspector]_OutlineWidth ("Outline Width",  Range(0, 50)) = 0.1
        [HideInInspector]_OutlineDistancFade (" Fade outline with near/far distance ", Vector) = (-25, 50, 0, 0)
        
        [SubEnum(SurfaceOptions, Opaque, 0, Transparent, 1)] _SurfaceType ("Surface Type", float) = 0
        [SubEnum(SurfaceOptions, Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2.0
        [Sub(SurfaceOptions)]_FOVShiftX ("Perspective FOV Offset", Range(0, 1)) = 0

        [NameIf(_style, Equal, 0, Albedo_Normal_Emission_ORM )]
        [NameIf(_style, Equal, 1, Albedo_Normal_Emission )]
        [NameIf(_style, Equal, 2, Albedo_Emission )]
        [MaterialSpace(10)]
        [Main(AlbedoGroup , _, off, off)]_Group1 ("Albedo/Normal/Emission/ORM", float) = 0
        [MaterialSpace(10)]
        [UVChannel(AlbedoGroup)]_SurfaceUVCtrl("Surface Textures UV", Vector) = (1,0,0,0)
        [MaterialSpace(5)]
        [Tex(AlbedoGroup, _BaseColor)] _BaseMap("Albedo", 2D) = "white" {}
        [HideInInspector][Sub(AlbedoGroup)] _BaseColor("Color", Color) = (1,1,1,1)
        
        [ShowIf(_style, NotEqual, 2)][Tex(AlbedoGroup)][Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        [ShowIf(_style, NotEqual, 2)][Sub(AlbedoGroup)] _BumpScale("Normal Map Scale", Range(0, 1.0)) = 0.5
        
        [ShowIf(_style, Equal, 0)][Tex(AlbedoGroup)]_OSMTexture("Occlusion/Smoothness/Metallic Map", 2D) = "white" {}
        [HideInInspector][Sub(AlbedoGroup)] _IsUsingOSMTexture("_IsUsingOSMTexture", Float) = 0
        [ShowIf(_style, Equal, 0)][ShowIf(_IsUsingOSMTexture, Equal, 0)][Sub(AlbedoGroup)]_PBRMetallic("Metallic", Range(0.0, 1.0)) = 0.0
        [ShowIf(_style, Equal, 0)][Sub(AlbedoGroup)]_PBRSmoothness("Smoothness", Range(0.0, 1.0)) = 0
        [ActiveIf(_IsUsingOSMTexture, Equal, 1)][ShowIf(_style, Equal, 0)][Sub(AlbedoGroup)]_PBROcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 0
        [ShowIf(_style, NotEqual, 0)][Tex(AlbedoGroup)]_RampOcclusionMap("Ramp OcclusionMap", 2D) = "white" {}
        [HideInInspector][Sub(AlbedoGroup)] _IsUsingRampOcclusionTexture("_IsUsingRampOcclusionTexture", Float) = 0
        [ActiveIf(_IsUsingRampOcclusionTexture, Equal, 1)][ShowIf(_style, NotEqual, 0)][Sub(AlbedoGroup)]_RampOcclusionStrength("Ramp Occlusion Strength", Range(0.0, 1.0)) = 0
        [SubToggle(AlbedoGroup, _EMISSION)] _EmissionToggle("Enable Emission", Float) = 0
        [ShowIf(_EmissionToggle, Equal, 1)][Tex(AlbedoGroup)]_EmissionMap("Emission", 2D) = "white" {}
        [ShowIf(_EmissionToggle, Equal, 1)][Sub(AlbedoGroup)][HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0)
        [MaterialSpace(10)]
        
        [ShowIf(_style, Equal, 2)][Main(FaceSetup , _, off, off)]_Group5 ("Face Setup", float) = 0
        [MaterialSpace(10)]
        [Tex(FaceSetup)]_FaceShadowMap ("Face Shadow Map", 2D) = "white" { }
        [UVChannel(FaceSetup)]_FaceUVCtrl("Face Shadow Map UV (Default = UV1)", Vector) = (0,1,0,0)
        [Sub(FaceSetup)]_FaceShadowMapPow ("Face Shadow Map Power", range(0.5, 4)) = 0.2
        [Sub(FaceSetup)]_FaceShadowSmoothness ("Face Shadow Smoothness", range(0.0, 0.1)) = 0.0
                [MaterialSpace(10)]
        [Main(Lighting , _, off, off)]_Group2 ("Diffuse / Lighting Behavior", float) = 0
        [MaterialSpace(10)]
        [SubEnum(Lighting, Gamma, 0, Linear, 1)] _WorkflowSpace ("Workflow Space", float) = 1
        [Ramp(Lighting, RampMap, Assets.Textures, 64)] _RampMap ("Ramp Lighting Map", 2D) = "white" { }
        [ShowIf(_style, NotEqual, 0)][SubToggle(Lighting, _EnableSSSRamp)] _EnableSSSRamp ("SSS Ramp Layer", float) = 0
        [ShowIf(_style, NotEqual, 0)][ActiveIf(_EnableSSSRamp, Equal, 1)][Ramp(Lighting, SSSRampMap, Assets.Textures, 64)] _SSSRampMap ("SSS Ramp Map", 2D) = "white" { }
        [SubEnum(Lighting, UseRampEnd, 0, Color, 1, DarkenRampLightByColor, 2)] _ShadowColorMode ("Received Shadow Behavior", float) = 0
        [ShowIf(_ShadowColorMode, NotEqual, 0)][Sub(Lighting)]_ReceivedShadowColor("Received Shadow Color", Color) = (0.8,0.8,0.8)
        [MaterialSpace(10)]
        [ShowIf(_style, Equal, 0)][Sub(Lighting)]_PBRInfluenceDirectLighting ("PBR Influence On Direct Lighting", Range(0.0, 1.0)) = 0
        [SubEnum(Lighting, SampleSH, 0, SampleSH_Flatten, 1, Color, 2)] _BakeGISource ("Indirect GI Source", float) = 0
        [MaterialSpace(10)]
        [ActiveIf(_BakeGISource, Equal, 2)][Sub(Lighting)]_OverrideGIColor("Override GI Color", Color) = (0,0,0)
        [MaterialSpace(10)]
        [SubToggle(Lighting, _FlattenAdditionalLighting)] _FlattenAdditionalLighting("Flatten Additional Lighting", Float) = 0
        
        [HideInInspector][SubToggle(Lighting, _OverrideLightDirToggle)] _OverrideLightDirToggle("Override Light Dir", Float) = 0
        [HideInInspector][ActiveIf(_OverrideLightDirToggle, Equal, 1)][Sub(Lighting)] _FakeLightEuler("Fake Light Euler Angles", Vector) = (0,0,0)
        [HideInInspector][Sub(Lighting)]_OverrideLightColorIntensityToggle("Override Light Color And Intensity Toggle", Float) = 0
        [HideInInspector][Sub(Lighting)]_FakeLightColor("Override Light Color", Color) = (1,1,1)
        [HideInInspector][Sub(Lighting)]_FakeLightIntensity("Override Light Color", Float) = 1.0
        [ShowIf(_style, Equal, 0)][SubToggleOff(Lighting, _ENVIRONMENTREFLECTIONS_OFF)] _EnvironmentReflections("Environment Reflections", Float) = 1.0
        [MaterialSpace(10)]
        
        [Main(SpecularHightlight , _, off, off)]_Group3 ("Specular Hightlight", float) = 0
        [MaterialSpace(10)]
        [SubToggle(SpecularHightlight, _SPECULAR_LIGHTING_ON)] _SpecularHighlights("Enable Specular Highlights", Float) = 0
        [ActiveIf(_SpecularHighlights, Equal, 1)][ShowIf(_style, NotEqual, 0)][Tex(SpecularHightlight)]_SpecularMaskMap("Specular Mask", 2D) = "white" {}
        [ActiveIf(_SpecularHighlights, Equal, 1)][UVChannel(SpecularHightlight)]_SpecUVCtrl("Specular Mask UV", Vector) = (1,0,0,0)
        [ActiveIf(_SpecularHighlights, Equal, 1)][ShowIf(_style, NotEqual, 0)][HDR][Sub(SpecularHightlight)]_SpecularColor("Specular Color", Color) = (0.2, 0.2, 0.2)
        [ActiveIf(_SpecularHighlights, Equal, 1)][ShowIf(_style, NotEqual, 0)][Sub(SpecularHightlight)]_SpecularFallOffColor("Specular FallOff Color", Color) = (0.2, 0.2, 0.2)
        [ActiveIf(_SpecularHighlights, Equal, 1)][Sub(SpecularHightlight)]_SpecularSize ("Specular Size", Range(0.0, 1.0)) = 0.25
        [ActiveIf(_SpecularHighlights, Equal, 1)][Sub(SpecularHightlight)]_SpecularFalloff ("Specular Falloff", Range(0.0, 1.0)) = 0.5
        [MaterialSpace(10)]
        
        [Main(RimLight , _, off, off)]_Group4 ("Rim Lighting", float) = 0
        [MaterialSpace(10)]
        [SubToggle(RimLight, _RIMLIGHTING_ON)] _RimLightOn("Enable Fresnel Rim lighting", Float) = 0
        [ActiveIf(_RimLightOn, Equal, 1)][Tex(RimLight)]_RimLightMaskMap("Rim Mask Map", 2D) = "white" {}
        [ActiveIf(_RimLightOn, Equal, 1)][UVChannel(RimLight)]_RimMaskUVCtrl("Rim Mask UV", Vector) = (1,0,0,0)
        [ActiveIf(_RimLightOn, Equal, 1)][Sub(RimLight)]_RimLightStrength("Fresnel Rim Strength", Range(0, 1)) = 0.35
        [ActiveIf(_RimLightOn, Equal, 1)][Sub(RimLight)]_RimLightAlign("Fresnel Rim Light Align", Range(-1, 1)) = 0
        [ActiveIf(_RimLightOn, Equal, 1)][Sub(RimLight)]_RimLightSmoothness("Fresnel Rim Light Smoothness", Range(0, 1)) = 0.2
        [ActiveIf(_RimLightOn, Equal, 1)][Sub(RimLight)]_RimLightColor("Fresnel Rim Light Color", Color) = (1,1,1,1)
        
        [SubToggle(RimLight, _DEPTH_RIMLIGHTING_ON)] _DepthRimLightOn("Enable Depth-Based Rim lighting", Float) = 0
        [ActiveIf(_DepthRimLightOn, Equal, 1)][Sub(RimLight)]_DepthRimLightColor("Depth Rim Light Color", Color) = (1,1,1,1)
        [ActiveIf(_DepthRimLightOn, Equal, 1)][Sub(RimLight)]_DepthRimLightStrength("Depth Rim Light Strength", Range(0, 0.3)) = 0.1
        [Sub(RimLight)]_RimLightOverShadow ("Rim Light Over Shadow", Range(0.0, 1.0)) = 1.0
        
        [MaterialSpace(10)]
        [ShowIf(_style, Equal, 1)][Main(Hair , _, off, off)]_Group6 ("Hair Ring Hightlight", float) = 0
        [MaterialSpace(10)]
        [Tex(Hair)]_HairHighlightMaskMap ("Hair Hightlight Mask", 2D) = "black" { }
        [UVChannel(Hair)]_HairUVCtrl("Hair Hightlight Map UV (Default = UV2)", Vector) = (0,0,1,0)
        [Sub(Hair)]_HairLightColor ("Hair Hightlight Color", Color) = (1,1,1,1)
        [Sub(Hair)]_HairLightFresnelMaskPower ("Hair Hightlight Fresnel Mask Power", range(0.0, 4)) = 1.0
        [Sub(Hair)]_HairLightStrength ("Hair Hightlight Strength", range(0.0, 1.0)) = 1.0
        [Sub(Hair)]_HairLightCameraRollInfluence ("Hair Hightlight Camera Roll Influence", range(0.0, 0.3)) = 0.0
        [Sub(Hair)]_HairUVSideCut ("Cut Offset From UV Sides", range(0.0, 0.5)) = 0.0
        [MaterialSpace(10)]
        
        [ShowIf(_style, NotEqual, 2)][Main(Matcap , _, off, off)]_GroupMatCap ("Matcap Reflection", float) = 0
        [HideInInspector]_IsUsingMatcapReflectMap("_IsUsingMatcapReflectMap", float) = 0
        [Tex(Matcap)] _MatCapReflectionMap("Matcap Reflection Map", 2D) = "Black" {}
        [Tex(Matcap)] _MatCapReflectionMaskMap("Matcap Mask", 2D) = "White" {}
        [ActiveIf(_IsUsingMatcapReflectMap, Equal, 1)]
        [Sub(Matcap)] _MatCapReflectionStrength("Matcap Reflection Strength", Range(0,2)) = 1
        [ActiveIf(_IsUsingMatcapReflectMap, Equal, 1)]
        [Sub(Matcap)] _MatCapRollStabilize("Matcap Camera Roll Stabilize", Range(0,1)) = 1
        // Blending state
        [MaterialSpace(10)]
        
        [Main(Shadow , _, on, off)]_Group7 ("Shadow", float) = 0
        [MaterialSpace(10)]
        [SubToggle(Shadow, _ReceiveShadows)] _ReceiveShadows("Receive Built-In Shadows", Float) = 1.0
        [SubToggle(Shadow, _RECEIVE_ASP_SHADOW)] _ReceiveASPShadow ("Receive Extra Shadow From ASP ShadowMap", float) = 0
        [SubToggle(Shadow, _RECEIVE_OFFSETED_SHADOW_ON)] _ReceiveOffsetedDepthMap ("Receive Extra Shadow From Offsetted Depth Map", float) = 0
        [ActiveIf(_ReceiveOffsetedDepthMap, Equal, 1)][Sub(Shadow)]_OffsetShadowDistance("Depth Offset Shadow Pixel Distance", Range(5, 30)) = 15
        [MaterialSpace(10)]
        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0
        
        [MaterialSpace(10)]
        [Main(Advance , _, on, off)]_AdvanceGroup ("Advance Setup (ZTest/Stencil", float) = 0
        [Sub(Advance)]_Stencil ("Stencil ID", Float) = 0
        [SubEnum(Advance, UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare Function", Float) = 4 // 4 is LEqual
        [SubEnum(Advance, UnityEngine.Rendering.StencilOp)]_StencilOp ("Stencil Pass Operation", Float) = 0
        [SubEnum(Advance, UnityEngine.Rendering.StencilOp)]_StencilFailOp ("Stencil Fail Operation", Float) = 0
        [SubToggle(Advance, _OverrideZTest)] _OverrideZTest("Override ZTest", Float) = 0
        [ActiveIf(_OverrideZTest, Equal, 1)][SubEnum(Advance, UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        
        [HideInInspector] _CharacterCenterCubeSize("_CharacterCenterCubeSize", Float) = 1.0
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
        [HideInInspector] _DebugGI("_DebugGI", Float) = 0.0
        // Editmode props
        //_QueueOffset("Queue offset", Float) = 0.0
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
                Fail [_StencilFailOp]
            }

            HLSLPROGRAM
            #pragma target 4.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex ASPLitVert
            #pragma fragment ASPLitFrag

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _FACESHADOW
            #pragma shader_feature_local _SSSMAP
            #pragma shader_feature_local _STYLE_STYLIZEPBR _STYLE_CELSHADING _STYLE_FACE
            #pragma shader_feature_local _HAIRMAP
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _MATCAP_HIGHLIGHT_MAP
            #pragma shader_feature_local_fragment _RAMP_OCCLUSION_MAP
            #pragma shader_feature_local_fragment _RECEIVE_ASP_SHADOW
            #pragma shader_feature_local_fragment _RECEIVE_OFFSETED_SHADOW_ON
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SPECULAR_LIGHTING_ON
            #pragma shader_feature_local_fragment _RIMLIGHTING_ON
            #pragma shader_feature_local_fragment _DEPTH_RIMLIGHTING_ON
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            
            //#pragma multi_compile _ MAIN_LIGHT_CALCULATE_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #if UNITY_VERSION >= 202201
                #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #else
                #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #endif
            
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            // we don't really need box projection for anime character in real world use case...
            // #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            // no light cookies & decal buffer on characters
            // #pragma multi_compile_fragment _ _LIGHT_COOKIES
            // #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
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
            
            #include "ShaderLibrary/ASPLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "ASPOutlineObject" "RenderType" = "Opaque" "Queue" = "Geometry" }
            
            ZTest [_ZTest]
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                Fail [_StencilFailOp]
            }
            
            Cull Front
            HLSLPROGRAM

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma shader_feature_local _USE_BAKED_NORAML
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
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
           // Cull Back
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOffsetShadow"
            Tags
            {
                "LightMode" = "DepthOffsetShadow"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]
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
            #if UNITY_VERSION > 202201
                #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #endif
            // -------------------------------------
            // Unity defined keywords
            //#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // -------------------------------------
            // Includes
            #include "ShaderLibrary/ASPLitInput.hlsl"
            #include "ShaderLibrary/ASPDepthNormalsPass.hlsl"
            ENDHLSL
        }

        /*Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }*/
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "LWGUI.ASP.ASPCharacterGUI"
}
