// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Shader created with Shader Forge v1.30 
// Shader Forge (c) Neat Corporation / Joachim Holmer - http://www.acegikmo.com/shaderforge/
// Note: Manually altering this data may prevent you from opening it in Shader Forge
/*SF_DATA;ver:1.30;sub:START;pass:START;ps:flbk:,iptp:0,cusa:False,bamd:0,lico:1,lgpr:1,limd:3,spmd:0,trmd:0,grmd:1,uamb:True,mssp:True,bkdf:False,hqlp:False,rprd:False,enco:False,rmgx:True,rpth:0,vtps:0,hqsc:True,nrmq:1,nrsp:0,vomd:0,spxs:False,tesm:0,olmd:1,culm:0,bsrc:3,bdst:7,dpts:2,wrdp:False,dith:0,rfrpo:True,rfrpn:Refraction,coma:15,ufog:True,aust:True,igpj:True,qofs:0,qpre:3,rntp:2,fgom:False,fgoc:False,fgod:False,fgor:False,fgmd:0,fgcr:0.7132353,fgcg:0.8457404,fgcb:1,fgca:1,fgde:0.01,fgrn:60,fgrf:87.36,stcl:False,stva:128,stmr:255,stmw:255,stcp:6,stps:0,stfa:0,stfz:0,ofsf:0,ofsu:0,f2p0:False,fnsp:False,fnfb:False;n:type:ShaderForge.SFN_Final,id:9361,x:36721,y:32897,varname:node_9361,prsc:2|diff-7851-OUT,spec-8807-OUT,gloss-8694-OUT,normal-8494-OUT,amspl-522-OUT,alpha-5241-OUT,refract-1798-OUT,voffset-7600-OUT;n:type:ShaderForge.SFN_DepthBlend,id:7261,x:32160,y:32389,varname:node_7261,prsc:2|DIST-163-OUT;n:type:ShaderForge.SFN_Slider,id:163,x:31782,y:32387,ptovrint:False,ptlb:Rim Size,ptin:_RimSize,varname:_RimSize,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:2,max:4;n:type:ShaderForge.SFN_Color,id:3503,x:33629,y:32114,ptovrint:False,ptlb:Water Color,ptin:_WaterColor,varname:_WaterColor,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:0,c2:0.503546,c3:1,c4:1;n:type:ShaderForge.SFN_Color,id:6509,x:32540,y:32256,ptovrint:False,ptlb:Rim Color,ptin:_RimColor,varname:_RimColor,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:1,c2:1,c3:1,c4:1;n:type:ShaderForge.SFN_Lerp,id:6015,x:35468,y:32676,cmnt:Add rim color to water,varname:node_6015,prsc:2|A-691-OUT,B-6509-RGB,T-457-OUT;n:type:ShaderForge.SFN_Power,id:949,x:32393,y:32509,varname:node_949,prsc:2|VAL-7261-OUT,EXP-3193-OUT;n:type:ShaderForge.SFN_Tex2dAsset,id:4756,x:31572,y:33572,ptovrint:False,ptlb:Shadermap,ptin:_Shadermap,varname:_Shadermap,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:True,tagnrm:False,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:2,isnm:False;n:type:ShaderForge.SFN_FragmentPosition,id:5725,x:30368,y:33402,varname:node_5725,prsc:2;n:type:ShaderForge.SFN_ComponentMask,id:4686,x:30592,y:33402,varname:node_4686,prsc:2,cc1:0,cc2:2,cc3:-1,cc4:-1|IN-5725-XYZ;n:type:ShaderForge.SFN_Multiply,id:5510,x:30993,y:33472,varname:node_5510,prsc:2|A-8213-OUT,B-6834-OUT;n:type:ShaderForge.SFN_Slider,id:3045,x:30435,y:33607,ptovrint:False,ptlb:Tiling,ptin:_Tiling,varname:_Tiling,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0.1,cur:0.9,max:1;n:type:ShaderForge.SFN_Set,id:8104,x:33753,y:34104,varname:Refraction,prsc:2|IN-5220-OUT;n:type:ShaderForge.SFN_Get,id:1798,x:33823,y:33217,varname:node_1798,prsc:2|IN-8104-OUT;n:type:ShaderForge.SFN_Multiply,id:5220,x:33485,y:34100,varname:node_5220,prsc:2|A-8903-OUT,B-4086-OUT;n:type:ShaderForge.SFN_Slider,id:4086,x:33147,y:34354,ptovrint:False,ptlb:Refraction Amount,ptin:_RefractionAmount,varname:_RefractionAmount,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.1,max:0.2;n:type:ShaderForge.SFN_Time,id:8305,x:30807,y:33883,varname:node_8305,prsc:2;n:type:ShaderForge.SFN_Slider,id:832,x:33548,y:33073,ptovrint:False,ptlb:Transparency,ptin:_Transparency,varname:_Transparency,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.75,max:1;n:type:ShaderForge.SFN_Tex2dAsset,id:2935,x:31554,y:34618,ptovrint:False,ptlb:Normals,ptin:_Normals,varname:_Normals,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:True,tagnrm:True,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:3,isnm:True;n:type:ShaderForge.SFN_Tex2d,id:4911,x:32792,y:34406,varname:node_4911,prsc:0,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:0,isnm:False|UVIN-9360-UVOUT,TEX-2935-TEX;n:type:ShaderForge.SFN_Panner,id:9360,x:32282,y:34337,varname:WavePanningV,prsc:0,spu:0,spv:1|UVIN-5129-OUT,DIST-8956-OUT;n:type:ShaderForge.SFN_Tex2d,id:9198,x:32792,y:34590,varname:node_49111,prsc:0,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:0,isnm:False|UVIN-1488-UVOUT,TEX-2935-TEX;n:type:ShaderForge.SFN_Panner,id:1488,x:32282,y:34536,varname:WavePanningU,prsc:0,spu:0.9,spv:0|UVIN-5129-OUT,DIST-8956-OUT;n:type:ShaderForge.SFN_Slider,id:6342,x:30397,y:34086,ptovrint:False,ptlb:Waves speed,ptin:_Wavesspeed,varname:_Wavesspeed,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.75,max:10;n:type:ShaderForge.SFN_Multiply,id:3981,x:30998,y:34037,varname:node_3981,prsc:2|A-8305-T,B-107-OUT;n:type:ShaderForge.SFN_Blend,id:409,x:33334,y:34782,varname:node_409,prsc:2,blmd:6,clmp:True|SRC-4911-R,DST-9198-G;n:type:ShaderForge.SFN_Multiply,id:4832,x:33691,y:34785,varname:node_4832,prsc:2|A-409-OUT,B-1805-OUT;n:type:ShaderForge.SFN_Multiply,id:6243,x:31634,y:32930,varname:rimTiling,prsc:0|A-2721-OUT,B-5878-OUT;n:type:ShaderForge.SFN_Slider,id:1805,x:33584,y:34957,ptovrint:False,ptlb:Glossiness,ptin:_Glossiness,varname:_Glossiness,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:1,max:1;n:type:ShaderForge.SFN_Set,id:2341,x:31222,y:33472,varname:Tiling,prsc:0|IN-5510-OUT;n:type:ShaderForge.SFN_Get,id:2721,x:31423,y:32849,varname:node_2721,prsc:2|IN-2341-OUT;n:type:ShaderForge.SFN_Get,id:5129,x:31947,y:34371,varname:mTiling,prsc:0|IN-2341-OUT;n:type:ShaderForge.SFN_Set,id:2137,x:33778,y:34501,varname:Normals,prsc:2|IN-3950-OUT;n:type:ShaderForge.SFN_Get,id:8494,x:36388,y:32984,varname:node_8494,prsc:2|IN-2137-OUT;n:type:ShaderForge.SFN_Tex2d,id:1028,x:32271,y:33116,varname:rimTexR,prsc:2,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:0,isnm:False|UVIN-1318-UVOUT,TEX-4756-TEX;n:type:ShaderForge.SFN_Panner,id:1318,x:31958,y:32988,varname:rimPanningU,prsc:0,spu:1,spv:0|UVIN-6243-OUT,DIST-7911-OUT;n:type:ShaderForge.SFN_Multiply,id:8846,x:32801,y:33114,varname:node_8846,prsc:2|A-1028-B,B-6468-B;n:type:ShaderForge.SFN_Panner,id:6391,x:31958,y:33244,varname:rimPanningV,prsc:0,spu:0,spv:1|UVIN-6243-OUT,DIST-7911-OUT;n:type:ShaderForge.SFN_Tex2d,id:6468,x:32271,y:33296,varname:rimTexB,prsc:2,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:0,isnm:False|UVIN-6391-UVOUT,TEX-4756-TEX;n:type:ShaderForge.SFN_Lerp,id:1704,x:34489,y:33020,varname:node_1704,prsc:2|A-832-OUT,B-6197-OUT,T-9324-OUT;n:type:ShaderForge.SFN_Vector1,id:6197,x:33897,y:32840,varname:node_6197,prsc:2,v1:1;n:type:ShaderForge.SFN_Append,id:8903,x:33147,y:34165,varname:node_8903,prsc:2|A-4911-R,B-9198-G;n:type:ShaderForge.SFN_Vector1,id:8807,x:36745,y:32827,varname:node_8807,prsc:2,v1:0;n:type:ShaderForge.SFN_Multiply,id:7600,x:33696,y:33510,varname:node_7600,prsc:2|A-2699-OUT,B-4911-R,C-3758-OUT;n:type:ShaderForge.SFN_Slider,id:3758,x:33023,y:33606,ptovrint:False,ptlb:Waves strength,ptin:_Wavesstrength,varname:_Wavesstrength,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.66,max:1;n:type:ShaderForge.SFN_Multiply,id:107,x:30807,y:34121,varname:node_107,prsc:2|A-6342-OUT,B-8626-OUT;n:type:ShaderForge.SFN_Vector1,id:8626,x:30554,y:34243,varname:node_8626,prsc:2,v1:0.1;n:type:ShaderForge.SFN_NormalVector,id:8130,x:32337,y:31836,prsc:2,pt:False;n:type:ShaderForge.SFN_Fresnel,id:3544,x:32545,y:31836,varname:node_3544,prsc:2|NRM-8130-OUT;n:type:ShaderForge.SFN_Power,id:807,x:32848,y:31846,varname:node_807,prsc:2|VAL-3544-OUT,EXP-1906-OUT;n:type:ShaderForge.SFN_OneMinus,id:3646,x:33259,y:31856,varname:invertFresnel,prsc:0|IN-2221-OUT;n:type:ShaderForge.SFN_Lerp,id:5570,x:35858,y:32791,varname:lerpFresnel,prsc:2|A-1369-RGB,B-6015-OUT,T-3646-OUT;n:type:ShaderForge.SFN_Slider,id:9211,x:33963,y:31680,ptovrint:False,ptlb:Depth,ptin:_Depth,varname:_Depth,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:30,max:30;n:type:ShaderForge.SFN_DepthBlend,id:4175,x:34344,y:31680,varname:depth,prsc:2|DIST-9211-OUT;n:type:ShaderForge.SFN_Set,id:1450,x:31228,y:34037,varname:WaveSpeed,prsc:2|IN-3981-OUT;n:type:ShaderForge.SFN_Get,id:8956,x:31947,y:34517,varname:mWaveSpeed,prsc:0|IN-1450-OUT;n:type:ShaderForge.SFN_Get,id:7911,x:31634,y:33136,varname:node_7911,prsc:0|IN-1450-OUT;n:type:ShaderForge.SFN_Blend,id:1465,x:34850,y:32980,varname:node_1465,prsc:2,blmd:10,clmp:True|SRC-4175-OUT,DST-1704-OUT;n:type:ShaderForge.SFN_Cubemap,id:8056,x:35882,y:33259,ptovrint:False,ptlb:Reflection,ptin:_Reflection,varname:_Reflection,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,pvfc:0|DIR-798-OUT;n:type:ShaderForge.SFN_ViewReflectionVector,id:798,x:35605,y:33258,varname:node_798,prsc:2;n:type:ShaderForge.SFN_Multiply,id:2523,x:34705,y:32121,varname:node_2523,prsc:0|A-3503-RGB,B-4919-OUT;n:type:ShaderForge.SFN_Slider,id:9486,x:33963,y:31874,ptovrint:False,ptlb:Depth darkness,ptin:_Depthdarkness,varname:_Depthdarkness,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.5,max:1;n:type:ShaderForge.SFN_Lerp,id:691,x:34990,y:31890,cmnt:Final water color,varname:node_691,prsc:2|A-3503-RGB,B-2523-OUT,T-4175-OUT;n:type:ShaderForge.SFN_OneMinus,id:4919,x:34344,y:31876,varname:node_4919,prsc:0|IN-9486-OUT;n:type:ShaderForge.SFN_ValueProperty,id:5878,x:31444,y:33042,ptovrint:False,ptlb:Rim tiling,ptin:_Rimtiling,varname:_Rimtiling,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:2;n:type:ShaderForge.SFN_SwitchProperty,id:8213,x:30785,y:33336,ptovrint:False,ptlb:Worldspace tiling,ptin:_Worldspacetiling,varname:_Worldspacetiling,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,on:True|A-9523-OUT,B-4686-OUT;n:type:ShaderForge.SFN_TexCoord,id:2490,x:30457,y:33168,varname:node_2490,prsc:2,uv:0;n:type:ShaderForge.SFN_Slider,id:3193,x:31782,y:32590,ptovrint:False,ptlb:Rim falloff,ptin:_Rimfalloff,varname:_Rimfalloff,prsc:0,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.25,max:5;n:type:ShaderForge.SFN_Multiply,id:6870,x:33113,y:32845,varname:node_6870,prsc:2|A-7451-OUT,B-6303-OUT,C-6509-A;n:type:ShaderForge.SFN_OneMinus,id:8217,x:32581,y:32509,cmnt:Intersection mask,varname:rimDepthInvert,prsc:0|IN-949-OUT;n:type:ShaderForge.SFN_OneMinus,id:6303,x:33024,y:33114,varname:node_6303,prsc:2|IN-8846-OUT;n:type:ShaderForge.SFN_Add,id:9324,x:33326,y:32812,varname:AddRimTextureToMask,prsc:2|A-7451-OUT,B-6870-OUT;n:type:ShaderForge.SFN_Slider,id:790,x:33539,y:33714,ptovrint:False,ptlb:Surface Highlight,ptin:_SurfaceHighlight,varname:_SurfaceHighlight,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0.05,max:1;n:type:ShaderForge.SFN_Multiply,id:1881,x:33967,y:33694,varname:node_1881,prsc:2|A-8963-OUT,B-790-OUT;n:type:ShaderForge.SFN_Lerp,id:7851,x:36275,y:32577,cmnt:Add surface highlights,varname:node_7851,prsc:2|A-5570-OUT,B-4005-OUT,T-758-OUT;n:type:ShaderForge.SFN_Vector1,id:4005,x:36087,y:32752,varname:node_4005,prsc:2,v1:1;n:type:ShaderForge.SFN_Clamp01,id:758,x:34203,y:33677,varname:ClampHighlight,prsc:2|IN-1881-OUT;n:type:ShaderForge.SFN_Subtract,id:2610,x:32983,y:34005,varname:node_2610,prsc:2|A-5469-R,B-8808-R;n:type:ShaderForge.SFN_Slider,id:4493,x:32872,y:33794,ptovrint:False,ptlb:Surface hightlight size,ptin:_Surfacehightlightsize,varname:_Surfacehightlightsize,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,min:0,cur:0,max:1;n:type:ShaderForge.SFN_Tex2d,id:5469,x:32720,y:33919,varname:node_5469,prsc:2,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:0,isnm:False|UVIN-1986-OUT,TEX-4756-TEX;n:type:ShaderForge.SFN_Tex2d,id:8808,x:32722,y:34094,varname:node_8808,prsc:2,tex:e3d849a71cf1896458f0932f079b66a3,ntxv:0,isnm:False|UVIN-5631-OUT,TEX-4756-TEX;n:type:ShaderForge.SFN_Multiply,id:5631,x:32485,y:34094,varname:HightlightPanningU,prsc:2|A-1488-UVOUT,B-3883-OUT;n:type:ShaderForge.SFN_ValueProperty,id:3883,x:32106,y:34013,ptovrint:False,ptlb:Surface Hightlight tiling,ptin:_SurfaceHightlighttiling,varname:_SurfaceHightlighttiling,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:0.25;n:type:ShaderForge.SFN_Multiply,id:1986,x:32485,y:33919,varname:HighlightPanningV,prsc:2|A-9360-UVOUT,B-3883-OUT;n:type:ShaderForge.SFN_ValueProperty,id:1906,x:32611,y:31993,ptovrint:False,ptlb:Fresnel exponent,ptin:_Fresnelexponent,varname:_Fresnelexponent,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,v1:4;n:type:ShaderForge.SFN_Multiply,id:9523,x:30618,y:33129,varname:node_9523,prsc:2|A-1592-OUT,B-2490-UVOUT;n:type:ShaderForge.SFN_Vector1,id:1592,x:30457,y:33090,varname:node_1592,prsc:2,v1:-20;n:type:ShaderForge.SFN_OneMinus,id:6834,x:30790,y:33551,varname:node_6834,prsc:2|IN-3045-OUT;n:type:ShaderForge.SFN_Clamp01,id:457,x:33630,y:32693,cmnt:Rim final,varname:node_457,prsc:2|IN-9324-OUT;n:type:ShaderForge.SFN_Multiply,id:522,x:36293,y:33317,varname:node_522,prsc:2|A-8056-RGB,B-1805-OUT;n:type:ShaderForge.SFN_Multiply,id:7451,x:32745,y:32737,varname:RimAllphaMultiply,prsc:2|A-8217-OUT,B-6509-A;n:type:ShaderForge.SFN_Color,id:1369,x:32870,y:32064,ptovrint:False,ptlb:Fresnel Color,ptin:_FresnelColor,varname:_FresnelColor,prsc:2,glob:False,taghide:False,taghdr:False,tagprd:False,tagnsco:False,tagnrm:False,c1:1,c2:1,c3:1,c4:0.5019608;n:type:ShaderForge.SFN_Multiply,id:2221,x:33065,y:31856,varname:node_2221,prsc:2|A-807-OUT,B-1369-A;n:type:ShaderForge.SFN_Step,id:8963,x:33617,y:33884,varname:node_8963,prsc:2|A-4493-OUT,B-2610-OUT;n:type:ShaderForge.SFN_Add,id:5241,x:35139,y:33038,varname:node_5241,prsc:2|A-1465-OUT,B-758-OUT;n:type:ShaderForge.SFN_NormalVector,id:2699,x:33197,y:33307,prsc:2,pt:False;n:type:ShaderForge.SFN_NormalBlend,id:3950,x:33263,y:34494,varname:node_3950,prsc:2|BSE-4911-RGB,DTL-9198-RGB;n:type:ShaderForge.SFN_Set,id:7540,x:34208,y:34599,varname:Roughness,prsc:2|IN-4832-OUT;n:type:ShaderForge.SFN_Get,id:8694,x:36391,y:32856,varname:node_8694,prsc:2|IN-7540-OUT;proporder:3503-6509-1369-1906-832-1805-790-4493-3883-9211-9486-163-3193-8213-3045-5878-4086-6342-3758-2935-4756-8056;pass:END;sub:END;*/

Shader "StylizedWater/Desktop" {
    Properties {
        _WaterColor ("Water Color", Color) = (0,0.503546,1,1)
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _FresnelColor ("Fresnel Color", Color) = (1,1,1,0.5019608)
        _Fresnelexponent ("Fresnel exponent", Float ) = 4
        _Transparency ("Transparency", Range(0, 1)) = 0.75
        _Glossiness ("Glossiness", Range(0, 1)) = 1
        _SurfaceHighlight ("Surface Highlight", Range(0, 1)) = 0.05
        _Surfacehightlightsize ("Surface hightlight size", Range(0, 1)) = 0
        _SurfaceHightlighttiling ("Surface Hightlight tiling", Float ) = 0.25
        _Depth ("Depth", Range(0, 30)) = 30
        _Depthdarkness ("Depth darkness", Range(0, 1)) = 0.5
        _RimSize ("Rim Size", Range(0, 4)) = 2
        _Rimfalloff ("Rim falloff", Range(0, 5)) = 0.25
        [MaterialToggle] _Worldspacetiling ("Worldspace tiling", Float ) = 0
        _Tiling ("Tiling", Range(0.1, 1)) = 0.9
        _Rimtiling ("Rim tiling", Float ) = 2
        _RefractionAmount ("Refraction Amount", Range(0, 0.2)) = 0.1
        _Wavesspeed ("Waves speed", Range(0, 10)) = 0.75
        _Wavesstrength ("Waves strength", Range(0, 1)) = 0.66
        [NoScaleOffset][Normal]_Normals ("Normals", 2D) = "bump" {}
        [NoScaleOffset]_Shadermap ("Shadermap", 2D) = "black" {}
        _Reflection ("Reflection", Cube) = "_Skybox" {}
        [HideInInspector]_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
    SubShader {
        Tags {
            "IgnoreProjector"="True"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }
        GrabPass{ }
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
            #include "UnityCG.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma exclude_renderers gles3 metal d3d11_9x xbox360 xboxone ps3 ps4 psp2 
            #pragma target 3.0
            #pragma glsl
            uniform sampler2D _GrabTexture;
            uniform sampler2D _CameraDepthTexture;
            uniform float4 _TimeEditor;
            uniform fixed _RimSize;
            uniform fixed4 _WaterColor;
            uniform fixed4 _RimColor;
            uniform sampler2D _Shadermap;
            uniform fixed _Tiling;
            uniform float _RefractionAmount;
            uniform float _Transparency;
            uniform sampler2D _Normals;
            uniform fixed _Wavesspeed;
            uniform float _Glossiness;
            uniform float _Wavesstrength;
            uniform fixed _Depth;
            uniform samplerCUBE _Reflection;
            uniform fixed _Depthdarkness;
            uniform fixed _Rimtiling;
            uniform fixed _Worldspacetiling;
            uniform fixed _Rimfalloff;
            uniform float _SurfaceHighlight;
            uniform float _Surfacehightlightsize;
            uniform float _SurfaceHightlighttiling;
            uniform float _Fresnelexponent;
            uniform float4 _FresnelColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 bitangentDir : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                float4 projPos : TEXCOORD6;
                UNITY_FOG_COORDS(7)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                float4 node_8305 = _Time + _TimeEditor;
                float WaveSpeed = (node_8305.g*(_Wavesspeed*0.1));
                fixed mWaveSpeed = WaveSpeed;
                fixed2 Tiling = (lerp( ((-20.0)*o.uv0), mul(unity_ObjectToWorld, v.vertex).rgb.rb, _Worldspacetiling )*(1.0 - _Tiling));
                fixed2 mTiling = Tiling;
                fixed2 WavePanningV = (mTiling+mWaveSpeed*float2(0,1));
                fixed3 node_4911 = UnpackNormal(tex2Dlod(_Normals,float4(WavePanningV,0.0,0)));
                v.vertex.xyz += (v.normal*node_4911.r*_Wavesstrength);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos(v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                o.projPos = ComputeScreenPos (o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                o.screenPos = o.pos;
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                #if UNITY_UV_STARTS_AT_TOP
                    float grabSign = -_ProjectionParams.x;
                #else
                    float grabSign = _ProjectionParams.x;
                #endif
                i.normalDir = normalize(i.normalDir);
                i.screenPos = float4( i.screenPos.xy / i.screenPos.w, 0, 0 );
                i.screenPos.y *= _ProjectionParams.x;
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float4 node_8305 = _Time + _TimeEditor;
                float WaveSpeed = (node_8305.g*(_Wavesspeed*0.1));
                fixed mWaveSpeed = WaveSpeed;
                fixed2 Tiling = (lerp( ((-20.0)*i.uv0), i.posWorld.rgb.rb, _Worldspacetiling )*(1.0 - _Tiling));
                fixed2 mTiling = Tiling;
                fixed2 WavePanningV = (mTiling+mWaveSpeed*float2(0,1));
                fixed3 node_4911 = UnpackNormal(tex2D(_Normals,WavePanningV));
                fixed2 WavePanningU = (mTiling+mWaveSpeed*float2(0.9,0));
                fixed3 node_49111 = UnpackNormal(tex2D(_Normals,WavePanningU));
                float3 node_3950_nrm_base = node_4911.rgb + float3(0,0,1);
                float3 node_3950_nrm_detail = node_49111.rgb * float3(-1,-1,1);
                float3 node_3950_nrm_combined = node_3950_nrm_base*dot(node_3950_nrm_base, node_3950_nrm_detail)/node_3950_nrm_base.z - node_3950_nrm_detail;
                float3 node_3950 = node_3950_nrm_combined;
                float3 Normals = node_3950;
                float3 normalLocal = Normals;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float3 viewReflectDirection = reflect( -viewDirection, normalDirection );
                float sceneZ = max(0,LinearEyeDepth (UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)))) - _ProjectionParams.g);
                float partZ = max(0,i.projPos.z - _ProjectionParams.g);
                float2 Refraction = (float2(node_4911.r,node_49111.g)*_RefractionAmount);
                float2 sceneUVs = float2(1,grabSign)*i.screenPos.xy*0.5+0.5 + Refraction;
                float4 sceneColor = tex2D(_GrabTexture, sceneUVs);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = 1;
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float Roughness = (saturate((1.0-(1.0-node_4911.r)*(1.0-node_49111.g)))*_Glossiness);
                float gloss = 1.0 - Roughness; // Convert roughness to gloss
                float specPow = exp2( gloss * 10.0+1.0);
/////// GI Data:
                UnityLight light;
                #ifdef LIGHTMAP_OFF
                    light.color = lightColor;
                    light.dir = lightDirection;
                    light.ndotl = LambertTerm (normalDirection, light.dir);
                #else
                    light.color = half3(0.f, 0.f, 0.f);
                    light.ndotl = 0.0f;
                    light.dir = half3(0.f, 0.f, 0.f);
                #endif
                UnityGIInput d;
                d.light = light;
                d.worldPos = i.posWorld.xyz;
                d.worldViewDir = viewDirection;
                d.atten = attenuation;
                Unity_GlossyEnvironmentData ugls_en_data;
                ugls_en_data.roughness = 1.0 - gloss;
                ugls_en_data.reflUVW = viewReflectDirection;
                UnityGI gi = UnityGlobalIllumination(d, 1, normalDirection, ugls_en_data );
                lightDirection = gi.light.dir;
                lightColor = gi.light.color;
////// Specular:
                float NdotL = max(0, dot( normalDirection, lightDirection ));
                float LdotH = max(0.0,dot(lightDirection, halfDirection));
                float node_8807 = 0.0;
                float3 specularColor = float3(node_8807,node_8807,node_8807);
                float specularMonochrome;
                float depth = saturate((sceneZ-partZ)/_Depth);
                float RimAllphaMultiply = ((1.0 - pow(saturate((sceneZ-partZ)/_RimSize),_Rimfalloff))*_RimColor.a);
                fixed node_7911 = WaveSpeed;
                fixed2 rimTiling = (Tiling*_Rimtiling);
                fixed2 rimPanningU = (rimTiling+node_7911*float2(1,0));
                float4 rimTexR = tex2D(_Shadermap,rimPanningU);
                fixed2 rimPanningV = (rimTiling+node_7911*float2(0,1));
                float4 rimTexB = tex2D(_Shadermap,rimPanningV);
                float AddRimTextureToMask = (RimAllphaMultiply+(RimAllphaMultiply*(1.0 - (rimTexR.b*rimTexB.b))*_RimColor.a));
                float node_4005 = 1.0;
                float2 HighlightPanningV = (WavePanningV*_SurfaceHightlighttiling);
                float4 node_5469 = tex2D(_Shadermap,HighlightPanningV);
                float2 HightlightPanningU = (WavePanningU*_SurfaceHightlighttiling);
                float4 node_8808 = tex2D(_Shadermap,HightlightPanningU);
                float ClampHighlight = saturate((step(_Surfacehightlightsize,(node_5469.r-node_8808.r))*_SurfaceHighlight));
                float3 diffuseColor = lerp(lerp(_FresnelColor.rgb,lerp(lerp(_WaterColor.rgb,(_WaterColor.rgb*(1.0 - _Depthdarkness)),depth),_RimColor.rgb,saturate(AddRimTextureToMask)),(1.0 - (pow((1.0-max(0,dot(i.normalDir, viewDirection))),_Fresnelexponent)*_FresnelColor.a))),float3(node_4005,node_4005,node_4005),ClampHighlight); // Need this for specular when using metallic
                diffuseColor = EnergyConservationBetweenDiffuseAndSpecular(diffuseColor, specularColor, specularMonochrome);
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = max(0.0,dot( normalDirection, viewDirection ));
                float NdotH = max(0.0,dot( normalDirection, halfDirection ));
                float VdotH = max(0.0,dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, 1.0-gloss );
                float normTerm = max(0.0, GGXTerm(NdotH, 1.0-gloss));
                float specularPBL = (NdotL*visTerm*normTerm) * (UNITY_PI / 4);
                if (IsGammaSpace())
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                specularPBL = max(0, specularPBL * NdotL);
                float3 directSpecular = (floor(attenuation) * _LightColor0.xyz)*specularPBL*FresnelTerm(specularColor, LdotH);
                half grazingTerm = saturate( gloss + specularMonochrome );
                float3 indirectSpecular = (0 + (texCUBE(_Reflection,viewReflectDirection).rgb*_Glossiness));
                indirectSpecular *= FresnelLerp (specularColor, grazingTerm, NdotV);
                float3 specular = (directSpecular + indirectSpecular);
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                float3 indirectDiffuse = float3(0,0,0);
                indirectDiffuse += UNITY_LIGHTMODEL_AMBIENT.rgb; // Ambient Light
                diffuseColor *= 1-specularMonochrome;
                float3 diffuse = (directDiffuse + indirectDiffuse) * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse + specular;
                fixed4 finalRGBA = fixed4(lerp(sceneColor.rgb, finalColor,(saturate(( lerp(_Transparency,1.0,AddRimTextureToMask) > 0.5 ? (1.0-(1.0-2.0*(lerp(_Transparency,1.0,AddRimTextureToMask)-0.5))*(1.0-depth)) : (2.0*lerp(_Transparency,1.0,AddRimTextureToMask)*depth) ))+ClampHighlight)),1);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "FORWARD_DELTA"
            Tags {
                "LightMode"="ForwardAdd"
            }
            Blend One One
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #ifndef UNITY_PASS_FORWARDADD
            #define UNITY_PASS_FORWARDADD
            #endif
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma multi_compile_fwdadd
            #pragma multi_compile_fog
            #pragma exclude_renderers gles3 metal d3d11_9x xbox360 xboxone ps3 ps4 psp2 
            #pragma target 3.0
            #pragma glsl
            uniform sampler2D _GrabTexture;
            uniform sampler2D _CameraDepthTexture;
            uniform float4 _TimeEditor;
            uniform fixed _RimSize;
            uniform fixed4 _WaterColor;
            uniform fixed4 _RimColor;
            uniform sampler2D _Shadermap;
            uniform fixed _Tiling;
            uniform float _RefractionAmount;
            uniform float _Transparency;
            uniform sampler2D _Normals;
            uniform fixed _Wavesspeed;
            uniform float _Glossiness;
            uniform float _Wavesstrength;
            uniform fixed _Depth;
            uniform fixed _Depthdarkness;
            uniform fixed _Rimtiling;
            uniform fixed _Worldspacetiling;
            uniform fixed _Rimfalloff;
            uniform float _SurfaceHighlight;
            uniform float _Surfacehightlightsize;
            uniform float _SurfaceHightlighttiling;
            uniform float _Fresnelexponent;
            uniform float4 _FresnelColor;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 bitangentDir : TEXCOORD4;
                float4 screenPos : TEXCOORD5;
                float4 projPos : TEXCOORD6;
                LIGHTING_COORDS(7,8)
                UNITY_FOG_COORDS(9)
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                o.tangentDir = normalize( mul( unity_ObjectToWorld, float4( v.tangent.xyz, 0.0 ) ).xyz );
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);
                float4 node_8305 = _Time + _TimeEditor;
                float WaveSpeed = (node_8305.g*(_Wavesspeed*0.1));
                fixed mWaveSpeed = WaveSpeed;
                fixed2 Tiling = (lerp( ((-20.0)*o.uv0), mul(unity_ObjectToWorld, v.vertex).rgb.rb, _Worldspacetiling )*(1.0 - _Tiling));
                fixed2 mTiling = Tiling;
                fixed2 WavePanningV = (mTiling+mWaveSpeed*float2(0,1));
                fixed3 node_4911 = UnpackNormal(tex2Dlod(_Normals,float4(WavePanningV,0.0,0)));
                v.vertex.xyz += (v.normal*node_4911.r*_Wavesstrength);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                float3 lightColor = _LightColor0.rgb;
                o.pos = UnityObjectToClipPos(v.vertex );
                UNITY_TRANSFER_FOG(o,o.pos);
                o.projPos = ComputeScreenPos (o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
                o.screenPos = o.pos;
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                #if UNITY_UV_STARTS_AT_TOP
                    float grabSign = -_ProjectionParams.x;
                #else
                    float grabSign = _ProjectionParams.x;
                #endif
                i.normalDir = normalize(i.normalDir);
                i.screenPos = float4( i.screenPos.xy / i.screenPos.w, 0, 0 );
                i.screenPos.y *= _ProjectionParams.x;
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);
                float4 node_8305 = _Time + _TimeEditor;
                float WaveSpeed = (node_8305.g*(_Wavesspeed*0.1));
                fixed mWaveSpeed = WaveSpeed;
                fixed2 Tiling = (lerp( ((-20.0)*i.uv0), i.posWorld.rgb.rb, _Worldspacetiling )*(1.0 - _Tiling));
                fixed2 mTiling = Tiling;
                fixed2 WavePanningV = (mTiling+mWaveSpeed*float2(0,1));
                fixed3 node_4911 = UnpackNormal(tex2D(_Normals,WavePanningV));
                fixed2 WavePanningU = (mTiling+mWaveSpeed*float2(0.9,0));
                fixed3 node_49111 = UnpackNormal(tex2D(_Normals,WavePanningU));
                float3 node_3950_nrm_base = node_4911.rgb + float3(0,0,1);
                float3 node_3950_nrm_detail = node_49111.rgb * float3(-1,-1,1);
                float3 node_3950_nrm_combined = node_3950_nrm_base*dot(node_3950_nrm_base, node_3950_nrm_detail)/node_3950_nrm_base.z - node_3950_nrm_detail;
                float3 node_3950 = node_3950_nrm_combined;
                float3 Normals = node_3950;
                float3 normalLocal = Normals;
                float3 normalDirection = normalize(mul( normalLocal, tangentTransform )); // Perturbed normals
                float sceneZ = max(0,LinearEyeDepth (UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)))) - _ProjectionParams.g);
                float partZ = max(0,i.projPos.z - _ProjectionParams.g);
                float2 Refraction = (float2(node_4911.r,node_49111.g)*_RefractionAmount);
                float2 sceneUVs = float2(1,grabSign)*i.screenPos.xy*0.5+0.5 + Refraction;
                float4 sceneColor = tex2D(_GrabTexture, sceneUVs);
                float3 lightDirection = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz,_WorldSpaceLightPos0.w));
                float3 lightColor = _LightColor0.rgb;
                float3 halfDirection = normalize(viewDirection+lightDirection);
////// Lighting:
                float attenuation = LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.xyz;
                float Pi = 3.141592654;
                float InvPi = 0.31830988618;
///////// Gloss:
                float Roughness = (saturate((1.0-(1.0-node_4911.r)*(1.0-node_49111.g)))*_Glossiness);
                float gloss = 1.0 - Roughness; // Convert roughness to gloss
                float specPow = exp2( gloss * 10.0+1.0);
////// Specular:
                float NdotL = max(0, dot( normalDirection, lightDirection ));
                float LdotH = max(0.0,dot(lightDirection, halfDirection));
                float node_8807 = 0.0;
                float3 specularColor = float3(node_8807,node_8807,node_8807);
                float specularMonochrome;
                float depth = saturate((sceneZ-partZ)/_Depth);
                float RimAllphaMultiply = ((1.0 - pow(saturate((sceneZ-partZ)/_RimSize),_Rimfalloff))*_RimColor.a);
                fixed node_7911 = WaveSpeed;
                fixed2 rimTiling = (Tiling*_Rimtiling);
                fixed2 rimPanningU = (rimTiling+node_7911*float2(1,0));
                float4 rimTexR = tex2D(_Shadermap,rimPanningU);
                fixed2 rimPanningV = (rimTiling+node_7911*float2(0,1));
                float4 rimTexB = tex2D(_Shadermap,rimPanningV);
                float AddRimTextureToMask = (RimAllphaMultiply+(RimAllphaMultiply*(1.0 - (rimTexR.b*rimTexB.b))*_RimColor.a));
                float node_4005 = 1.0;
                float2 HighlightPanningV = (WavePanningV*_SurfaceHightlighttiling);
                float4 node_5469 = tex2D(_Shadermap,HighlightPanningV);
                float2 HightlightPanningU = (WavePanningU*_SurfaceHightlighttiling);
                float4 node_8808 = tex2D(_Shadermap,HightlightPanningU);
                float ClampHighlight = saturate((step(_Surfacehightlightsize,(node_5469.r-node_8808.r))*_SurfaceHighlight));
                float3 diffuseColor = lerp(lerp(_FresnelColor.rgb,lerp(lerp(_WaterColor.rgb,(_WaterColor.rgb*(1.0 - _Depthdarkness)),depth),_RimColor.rgb,saturate(AddRimTextureToMask)),(1.0 - (pow((1.0-max(0,dot(i.normalDir, viewDirection))),_Fresnelexponent)*_FresnelColor.a))),float3(node_4005,node_4005,node_4005),ClampHighlight); // Need this for specular when using metallic
                diffuseColor = EnergyConservationBetweenDiffuseAndSpecular(diffuseColor, specularColor, specularMonochrome);
                specularMonochrome = 1.0-specularMonochrome;
                float NdotV = max(0.0,dot( normalDirection, viewDirection ));
                float NdotH = max(0.0,dot( normalDirection, halfDirection ));
                float VdotH = max(0.0,dot( viewDirection, halfDirection ));
                float visTerm = SmithJointGGXVisibilityTerm( NdotL, NdotV, 1.0-gloss );
                float normTerm = max(0.0, GGXTerm(NdotH, 1.0-gloss));
                float specularPBL = (NdotL*visTerm*normTerm) * (UNITY_PI / 4);
                if (IsGammaSpace())
                    specularPBL = sqrt(max(1e-4h, specularPBL));
                specularPBL = max(0, specularPBL * NdotL);
                float3 directSpecular = attenColor*specularPBL*FresnelTerm(specularColor, LdotH);
                float3 specular = directSpecular;
/////// Diffuse:
                NdotL = max(0.0,dot( normalDirection, lightDirection ));
                half fd90 = 0.5 + 2 * LdotH * LdotH * (1-gloss);
                float nlPow5 = Pow5(1-NdotL);
                float nvPow5 = Pow5(1-NdotV);
                float3 directDiffuse = ((1 +(fd90 - 1)*nlPow5) * (1 + (fd90 - 1)*nvPow5) * NdotL) * attenColor;
                diffuseColor *= 1-specularMonochrome;
                float3 diffuse = directDiffuse * diffuseColor;
/// Final Color:
                float3 finalColor = diffuse + specular;
                fixed4 finalRGBA = fixed4(finalColor * (saturate(( lerp(_Transparency,1.0,AddRimTextureToMask) > 0.5 ? (1.0-(1.0-2.0*(lerp(_Transparency,1.0,AddRimTextureToMask)-0.5))*(1.0-depth)) : (2.0*lerp(_Transparency,1.0,AddRimTextureToMask)*depth) ))+ClampHighlight),0);
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
            ENDCG
        }
        Pass {
            Name "ShadowCaster"
            Tags {
                "LightMode"="ShadowCaster"
            }
            Offset 1, 1
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #ifndef UNITY_PASS_SHADOWCASTER
            #define UNITY_PASS_SHADOWCASTER
            #endif
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "UnityPBSLighting.cginc"
            #include "UnityStandardBRDF.cginc"
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_fog
            #pragma exclude_renderers gles3 metal d3d11_9x xbox360 xboxone ps3 ps4 psp2 
            #pragma target 3.0
            #pragma glsl
            uniform float4 _TimeEditor;
            uniform fixed _Tiling;
            uniform sampler2D _Normals;
            uniform fixed _Wavesspeed;
            uniform float _Wavesstrength;
            uniform fixed _Worldspacetiling;
            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 texcoord0 : TEXCOORD0;
            };
            struct VertexOutput {
                V2F_SHADOW_CASTER;
                float2 uv0 : TEXCOORD1;
                float4 posWorld : TEXCOORD2;
                float3 normalDir : TEXCOORD3;
            };
            VertexOutput vert (VertexInput v) {
                VertexOutput o = (VertexOutput)0;
                o.uv0 = v.texcoord0;
                o.normalDir = UnityObjectToWorldNormal(v.normal);
                float4 node_8305 = _Time + _TimeEditor;
                float WaveSpeed = (node_8305.g*(_Wavesspeed*0.1));
                fixed mWaveSpeed = WaveSpeed;
                fixed2 Tiling = (lerp( ((-20.0)*o.uv0), mul(unity_ObjectToWorld, v.vertex).rgb.rb, _Worldspacetiling )*(1.0 - _Tiling));
                fixed2 mTiling = Tiling;
                fixed2 WavePanningV = (mTiling+mWaveSpeed*float2(0,1));
                fixed3 node_4911 = UnpackNormal(tex2Dlod(_Normals,float4(WavePanningV,0.0,0)));
                v.vertex.xyz += (v.normal*node_4911.r*_Wavesstrength);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex );
                TRANSFER_SHADOW_CASTER(o)
                return o;
            }
            float4 frag(VertexOutput i) : COLOR {
                i.normalDir = normalize(i.normalDir);
                float3 normalDirection = i.normalDir;
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
