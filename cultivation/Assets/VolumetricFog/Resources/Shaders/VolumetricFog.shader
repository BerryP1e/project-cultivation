Shader "VolumetricFogAndMist/VolumetricFog" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_NoiseTex ("Noise (RGB)", 2D) = "white" {}
		//_FogDownsampled ("Downsampled Depth", 2D) = "white" {}
		//_BlurTex ("Blur (RGB)", 2D) = "white" {}
		_FogAlpha ("Alpha", Range (0, 1)) = 1
		_FogDistance ("Distance", Vector) = (0, 1, 1000, 0) 
		_FogData("Fog Data", Vector) = (0,1,1,1)
		_Color ("Fog Color", Color) = (0.9,0.9,0.9)
		_FogSkyColor ("Sky Color", Color) = (0.9,0.9,0.9,0.8)
		_FogSkyData ("Sky Haze Data", Vector) = (50, 0, 0.3, 0.999)
		_FogWindDir ("Wind Direction", Vector) = (1,0,0)	
		_FogStepping ("Fog andStepping", Vector) = (0.0833333, 1, 0.0005)
		_FogVoidPosition("Fog Void Position", Vector) = (0,0,0)
		_FogVoidData("Fog Void Data", Vector) = (0,0,0,1) // xyz = size, w = falloff
		_FogAreaPosition("Fog Area Position", Vector) = (0,0,0)
		_FogAreaData("Fog Area Data", Vector) = (0,0,0,1) // xyz = size, w = falloff
		_FogOfWarCenter("Fog Of War Center", Vector) = (0,0,0)
		_FogOfWarSize("Fog Of War Size", Vector) = (1,1,1)
		_FogOfWar ("Fog of War Mask", 2D) = "white" {}
		_FogPointLightPosition0("Point Light 1 Position", Vector) = (0,0,0)
		_FogPointLightColor0("Point Light 1 Color", Vector) = (1,1,0,1)
		_FogPointLightPosition1("Point Light 2 Position", Vector) = (0,0,0)
		_FogPointLightColor1("Point Light 2 Color", Vector) = (1,1,0,1)
		_FogPointLightPosition2("Point Light 3 Position", Vector) = (0,0,0)
		_FogPointLightColor2("Point Light 3 Color", Vector) = (1,1,0,1)
		_FogPointLightPosition3("Point Light 4 Position", Vector) = (0,0,0)
		_FogPointLightColor3("Point Light 4 Color", Vector) = (1,1,0,1)
		_FogPointLightPosition4("Point Light 5 Position", Vector) = (0,0,0)
		_FogPointLightColor4("Point Light 5 Color", Vector) = (1,1,0,1)
		_FogPointLightPosition5("Point Light 6 Position", Vector) = (0,0,0)
		_FogPointLightColor5("Point Light 6 Color", Vector) = (1,1,0,1)
		_SunPosition("Sun Position", Vector) = (0,0,0)
		_SunDir("Sun Direction", Vector) = (0,0,0)
		_SunColor("Sun Color", Color) = (1,1,1)
		_ClipDir("Camera View Dir", Vector) = (0,0,1)
		_Jitter("Jittering", Float) = 0
	}
	
	SubShader {
       	ZTest Always Cull Off ZWrite Off
       	Fog { Mode Off }
       	
		Pass { // 0 general fog render
	        CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragBackFog
// Shader compile options - remove or set explicitly to reduce compilation time and unwanted shader variants
#pragma multi_compile __ FOG_DISTANCE_ON
#pragma multi_compile __ FOG_AREA_SPHERE FOG_AREA_BOX
#pragma multi_compile __ FOG_VOID_SPHERE FOG_VOID_BOX FOG_OF_WAR_ON
#pragma multi_compile __ FOG_HAZE_ON FOG_USE_XY_PLANE
#pragma multi_compile __ FOG_SCATTERING_ON
#pragma multi_compile __ FOG_BLUR_ON
#pragma multi_compile __ FOG_POINT_LIGHT0 FOG_POINT_LIGHT1 FOG_POINT_LIGHT2 FOG_POINT_LIGHT3 FOG_POINT_LIGHT4 FOG_POINT_LIGHT5
#pragma multi_compile __ FOG_SUN_SHADOWS_ON
#pragma multi_compile __ FOG_COMPUTE_DEPTH
#pragma fragmentoption ARB_precision_hint_fastest
	#pragma target 3.0
			#include "VolumetricFog.cginc"
			ENDCG
        }
		Pass { // 1 downsampled, with render targets
	        CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragGetFog
// Shader compile options - remove or set explicitly to reduce compilation time and unwanted shader variants
#pragma multi_compile __ FOG_DISTANCE_ON
#pragma multi_compile __ FOG_AREA_SPHERE FOG_AREA_BOX
#pragma multi_compile __ FOG_VOID_SPHERE FOG_VOID_BOX FOG_OF_WAR_ON
#pragma multi_compile __ FOG_USE_XY_PLANE
#pragma multi_compile __ FOG_POINT_LIGHT0 FOG_POINT_LIGHT1 FOG_POINT_LIGHT2 FOG_POINT_LIGHT3 FOG_POINT_LIGHT4 FOG_POINT_LIGHT5
#pragma multi_compile __ FOG_SUN_SHADOWS_ON
#pragma multi_compile __ FOG_COMPUTE_DEPTH
#define FOG_DIFFUSION 1
   	#pragma fragmentoption ARB_precision_hint_fastest
	#pragma target 3.0
			#include "VolumetricFog.cginc"
			ENDCG
        }        
		Pass { // 2 compose, upsample fog buffer
	        CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragApplyFog
#pragma multi_compile __ FOG_HAZE_ON
#pragma multi_compile __ FOG_SCATTERING_ON
#pragma multi_compile __ FOG_BLUR_ON
#pragma multi_compile __ FOG_COMPUTE_DEPTH
   	#pragma fragmentoption ARB_precision_hint_fastest
	#pragma target 3.0
			#include "VolumetricFog.cginc"
			ENDCG
        }   
		Pass { // 3 downsampled, get fog buffer
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragGetJustFog
			// Shader compile options - remove or set explicitly to reduce compilation time and unwanted shader variants
#pragma multi_compile __ FOG_DISTANCE_ON
#pragma multi_compile __ FOG_AREA_SPHERE FOG_AREA_BOX
#pragma multi_compile __ FOG_VOID_SPHERE FOG_VOID_BOX FOG_OF_WAR_ON
#pragma multi_compile __ FOG_USE_XY_PLANE
#pragma multi_compile __ FOG_POINT_LIGHT0 FOG_POINT_LIGHT1 FOG_POINT_LIGHT2 FOG_POINT_LIGHT3 FOG_POINT_LIGHT4 FOG_POINT_LIGHT5
#pragma multi_compile __ FOG_SUN_SHADOWS_ON
#pragma multi_compile __ FOG_COMPUTE_DEPTH
#define FOG_DIFFUSION 1
				#pragma fragmentoption ARB_precision_hint_fastest
				#pragma target 3.0
						#include "VolumetricFog.cginc"
						ENDCG
		}
		Pass { // 4 downsampled, get depth buffer
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragGetJustDepth
   	#pragma fragmentoption ARB_precision_hint_fastest
	#pragma target 3.0
#pragma multi_compile __ FOG_COMPUTE_DEPTH
			#include "VolumetricFog.cginc"
			ENDCG
		}

	}
	FallBack Off
}	
