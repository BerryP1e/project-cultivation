#ifndef ASP_LIGHTING_INCLUDED
#define ASP_LIGHTING_INCLUDED
/*
 * Copyright (C) Eric Hu - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Eric Hu (Shu Yuan, Hu) March, 2024
*/

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "ASPLitInput.hlsl"
#include "Quaternion.hlsl"
#include "ASPCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "ASPShadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

void RemapFloat(float In, half2 InMinMax, half2 OutMinMax, out float Out)
{
    Out = OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

real FaceShadowMapAttenuation(float2 uv, ASPInputData inputData, Light light)
{
    #ifndef _STYLE_FACE
    return 1.0;
    #endif

    float3 lightDir = light.direction.xyz;
    float3 front = inputData.frontDirectionWS;
    float3 right = inputData.rightDirectionWS;
#ifdef _FACESHADOW
        float faceShadowValue = SAMPLE_TEXTURE2D_LOD(_FaceShadowMap, sampler_FaceShadowMap, float2(-uv.x, uv.y), 0).r;
        float faceShadowValue2 = SAMPLE_TEXTURE2D_LOD(_FaceShadowMap, sampler_FaceShadowMap, float2(uv.x, uv.y), 0).r;
    #else
    float faceShadowValue = 1;
    float faceShadowValue2 = 1;
#endif
    bool switchShadow = (dot(normalize(right.xz), normalize(lightDir.xz))) > 0;
    float flippedFaceShadow = switchShadow ? faceShadowValue : faceShadowValue2;
    float lightAngleHorizontal = acos(dot(normalize(front.xz),  normalize(lightDir.xz)));
    float threshold = lightAngleHorizontal / 3.141592653;
    threshold = pow(threshold, max(1 / inputData.faceShadowPow, 0));

    float lightAttenuation = saturate(smoothstep(threshold - inputData.faceShadowSmoothness,
                                                 threshold + inputData.faceShadowSmoothness, flippedFaceShadow));
    return lightAttenuation;
}

real StepFeatherToon(real term, real maxTerm, real step, real feather)
{
    return saturate((term / maxTerm - max(0.0001, step)) / max(0.05, feather)) * maxTerm;
}

// directSpecular = brdf.specular * ToonifyDirectBRDFSpecular();
half ToonifyDirectBRDFSpecular(BRDFData brdfData, ASPInputData inputData, half3 lightDirectionWS)
{
    float3 lightDirectionWSFloat3 = float3(lightDirectionWS);
    //Unity BRDF Direct Specular
    half specularTerm = DirectBRDFSpecular(brdfData, inputData.normalWS, lightDirectionWS, inputData.viewDirectionWS);
    specularTerm = StepFeatherToon(specularTerm, 30, (1.0-inputData.specularFalloff) * 0.01, 1.0-inputData.specularSize);
    return specularTerm;
}

float3 LightingSpecularToon(float3 lightDir, float3 normal, float3 viewDir, float3 specular, float3 fallOffColor, float size,
                            float smoothness)
{
    float3 halfVec = SafeNormalize(float3(lightDir) + float3(viewDir));
    half NdotH = saturate(dot(normal, halfVec));
    float spec = saturate(pow(NdotH, 5));
    float delta = 0.5;
    spec = smoothstep((1.0 - size) , (1.0 - size)  + smoothness, spec);
    float3 specularReflection = lerp(fallOffColor, specular, spec) * spec;
    return specularReflection;
}

half3 ToonAdditionalLighting(Light light, ASPInputData inputData, BRDFData brdfData)
{
    half NdotL = saturate(dot(inputData.normalWS, light.direction));
    NdotL = lerp(NdotL, 1, inputData.flattenAdditionalLighting);
    half3 radiance = NdotL * light.color * (light.shadowAttenuation * light.distanceAttenuation);

    half3 brdf = brdfData.diffuse;
    half3 specularLightColor = 0;
    #ifdef _SPECULAR_LIGHTING_ON
    #ifdef _STYLE_STYLIZEPBR
            specularLightColor = NdotL * brdfData.specular * ToonifyDirectBRDFSpecular(brdfData, inputData, light.direction);
    #endif
    #endif
    brdf += specularLightColor;
    return brdf * radiance;
}

half3 ToonEyeLighting(Light light, ASPInputData inputData, BRDFData brdfData)
{
    half NdotL = 0;
    half3 rampShadowedColor = half3(1,1,1);
    #ifdef _STYLE_LAMBERTLIGHTING
    rampShadowedColor = 0;
    NdotL = saturate(dot(inputData.normalWS, light.direction));
    #else
    rampShadowedColor = inputData.selfUnlitAreaColor * inputData.albedo.rgb;
    NdotL = saturate(dot(inputData.frontDirectionWS, light.direction) );
    #endif
    
    half lightRadiance = NdotL * inputData.offsetShadowAttenuation;
   
    half3 outColor = lerp(rampShadowedColor, brdfData.albedo, NdotL);

    // Shadow color == ramp dark color * albedo
    // received shadow color
    half3 customShadowColor = inputData.receivedShadowColor;

    // todo eye should be able to have occlusion as well
    //outColor = lerp(rampShadowedColor, outColor, inputData.rampOcclusion);

    // Apply Shadow
    customShadowColor = lerp(rampShadowedColor, customShadowColor, inputData.shadowColorMode);
    outColor = lerp(customShadowColor, outColor, light.shadowAttenuation * light.distanceAttenuation);
    outColor.rgb *= light.color;

    return outColor;
}

half3 ToonLighting(Light light, ASPInputData inputData, BRDFData brdfData)
{
    // half lightAttenuation = light.distanceAttenuation * light.shadowAttenuation;
    half3 specularLightColor = half3(0, 0, 0);
    half3 outColor = inputData.albedo.rgb;
    half NdotL = saturate(dot(inputData.normalWS, light.direction));
    half lightRadiance = NdotL;
    half3 pbrDirectDiffuse = light.color * brdfData.diffuse * lightRadiance;

    half rampNdotL = lerp(NdotL, pow(NdotL, 1.0 / 2.2), inputData.workflowSpace) * inputData.offsetShadowAttenuation;

    // Shadow color == ramp dark color * albedo
    half3 rampShadowedColor = SAMPLE_TEXTURE2D_LOD(_RampMap, my_point_clamp_sampler, float2(0,0.5), 0).rgb;
    rampShadowedColor = lerp(rampShadowedColor, half3(0, 0, 0), inputData.pbrInfluenceDirectLighting);
    rampShadowedColor *= inputData.albedo.rgb;

    // cel/stylize pbr/skin base shading
    half NdotV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS) * 0.5 + 0.5);
    half3 rampColor = SAMPLE_TEXTURE2D_LOD(_RampMap, sampler_RampMap, float2(rampNdotL, 0.5), 0).rgb;
    outColor *= rampColor;
    outColor = lerp(outColor, pbrDirectDiffuse, inputData.pbrInfluenceDirectLighting);

    //apply occlusion that take into account of the ramp lighting
    outColor = lerp(rampShadowedColor, outColor, inputData.rampOcclusion);

    // Face shading
    #if _STYLE_FACE
    half faceShadowAttenuation = 1.0;
    faceShadowAttenuation *= FaceShadowMapAttenuation(inputData.faceShaowMapUV, inputData, light) * inputData.offsetShadowAttenuation;;
    // TODO, find a better way to handle face special rule => ramp shadow color won't affect 
    outColor = lerp(rampShadowedColor, inputData.albedo.rgb, faceShadowAttenuation);
    #endif
    
    // Skin shading
    #ifdef _SSSMAP
    half3 sssColor = SAMPLE_TEXTURE2D_LOD(_SSSRampMap, sampler_SSSRampMap, float2((rampNdotL*0.5+0.5) * NdotV, 0.5), 0).rgb;
    outColor = min(outColor, outColor * sssColor);
    #endif
    
    // Apply Shadow
    half shadowDarkenFactor = step(2, inputData.shadowColorMode);
    half3 darkenModeShaded = inputData.receivedShadowColor * outColor;
    half3 colorModeShaded = inputData.receivedShadowColor * inputData.albedo.rgb;
    half3 darkestShadowColor = lerp(colorModeShaded, darkenModeShaded, shadowDarkenFactor);
    half shadowModeFactor = step(1, inputData.shadowColorMode);
    half3 shadowColor = lerp(rampShadowedColor, darkestShadowColor, shadowModeFactor);
    outColor = lerp(shadowColor, outColor, light.shadowAttenuation * light.distanceAttenuation);

    // Apply Specular
    #ifdef _SPECULAR_LIGHTING_ON
    #ifdef _STYLE_STYLIZEPBR
            specularLightColor = NdotL * brdfData.specular * ToonifyDirectBRDFSpecular(brdfData, inputData, light.direction);
    #else
            specularLightColor = LightingSpecularToon(light.direction, inputData.normalWS, inputData.viewDirectionWS, inputData.specularColor, inputData.specularFallOffColor, inputData.specularSize, inputData.specularFalloff );
    #endif
    #endif
    specularLightColor *= light.shadowAttenuation * light.distanceAttenuation;
    outColor += specularLightColor;

    outColor.rgb *= light.color;
    return outColor;
}
/*
half3 EnvironmentBRDF_Lut(BRDFData brdfData, ASPInputData inputData, half3 indirectDiffuse, float3 indirectSpecular,
                          float NdotV)
{
    float3 f0 = 0.04 * (1.0 - inputData.metallic) + brdfData.albedo * inputData.metallic;
    float3 prefilteredColor = indirectSpecular;
    float2 indirectBRDF = SAMPLE_TEXTURE2D_LOD(_StandardBrdfLut, sampler_StandardBrdfLut,
                                               float2(NdotV, saturate(brdfData.roughness)), 0).rg;
    float3 specularIBL = prefilteredColor * lerp(indirectBRDF.xxx, indirectBRDF.yyy, f0);
    float3 compensation = 1.0 + f0 * (1.0 / indirectBRDF.y - 1.0);
    //specularIBL *= compensation;

    half3 c = indirectDiffuse * brdfData.diffuse;
    c += specularIBL;
    return c;
}

real3 BRDF_IBL(BRDFData brdfData, ASPInputData inputData, float occlusion)
{
    half3 reflectVector = reflect(-inputData.viewDirectionWS, inputData.normalWS);
    half NoV = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
    half3 indirectDiffuse = inputData.bakedGI; // todo , blend with flatten GI
    half3 indirectSpecular = GlossyEnvironmentReflection(reflectVector, brdfData.perceptualRoughness, occlusion);

    real3 color = EnvironmentBRDF_Lut(brdfData, inputData, indirectDiffuse, indirectSpecular, NoV);
    return color * occlusion;
}
*/

real3 MatCapHightlightWithMask(ASPInputData inputData, Light light)
{
    
    #ifdef _MATCAP_HIGHLIGHT_MAP
    float3 matcapUp = mul((float3x3)UNITY_MATRIX_I_V, float3(0, 1, 0));
    float3 matcapRollFixedUp = float3(0, 1, 0);
    float rollStabilizeFactor = 1.0 - saturate(dot(matcapUp, matcapRollFixedUp));
    matcapUp = lerp(matcapUp, matcapRollFixedUp, rollStabilizeFactor * inputData.matCapRollStabilize);

    float3 right = normalize(cross(matcapUp, -inputData.viewDirectionWS));
    matcapUp = cross(-inputData.viewDirectionWS, right);
    float2 matcapUV = mul(float3x3(right, matcapUp, inputData.viewDirectionWS), inputData.matCapNormalWS).xy;
    matcapUV = matcapUV * 0.5 + 0.5;
    float4 matcapRGBA = SAMPLE_TEXTURE2D(_MatCapReflectionMap, sampler_MatCapReflectionMap, matcapUV);
    matcapRGBA *= inputData.matCapReflectionStrength;

    float matcapMask = SAMPLE_TEXTURE2D(_MatCapReflectionMaskMap, sampler_MatCapReflectionMap, inputData.specUV).r;
        return matcapRGBA.rgb * matcapRGBA.a * matcapMask;
    #else
        return real3(0,0,0);
    #endif
}

real3 MatCapHightlight(ASPInputData inputData, Light light)
{
    #ifdef _MATCAP_HIGHLIGHT_MAP
    float3 matcapUp = mul((float3x3)UNITY_MATRIX_I_V, float3(0, 1, 0));
    float3 matcapRollFixedUp = float3(0, 1, 0);
    float rollStabilizeFactor = 1.0 - saturate(dot(matcapUp, matcapRollFixedUp));
    matcapUp = lerp(matcapUp, matcapRollFixedUp, rollStabilizeFactor * inputData.matCapRollStabilize);

    float3 right = normalize(cross(matcapUp, -inputData.viewDirectionWS));
    matcapUp = cross(-inputData.viewDirectionWS, right);
    float2 matcapUV = mul(float3x3(right, matcapUp, inputData.viewDirectionWS), inputData.matCapNormalWS).xy;
    matcapUV = matcapUV * 0.5 + 0.5;
    float4 matcapRGBA = SAMPLE_TEXTURE2D(_MatCapReflectionMap, sampler_MatCapReflectionMap, matcapUV);
    //TODO add option to handle shadowed matcap highlight
    matcapRGBA *= inputData.matCapReflectionStrength;
    return matcapRGBA.rgb * matcapRGBA.a;
    #else
    return real3(0,0,0);
    #endif
}

real3 FrenselRimLighting(ASPInputData inputData, Light mainLight)
{
    half NdotL = saturate(dot(mainLight.direction, inputData.normalWS));
    real rimPower = 1.0 - inputData.rimLightStrength;
    real NdotV = dot(inputData.viewDirectionWS, inputData.normalWS);
    real rim = saturate(
        (1.0 - NdotV) * lerp(1, NdotL, saturate(inputData.rimLightAlign)) * lerp(
            1, 1 - NdotL, saturate(-inputData.rimLightAlign)));
    float delta = fwidth(rim);
    real3 rimLighting = smoothstep(rimPower - delta, rimPower + delta + inputData.rimLightSmoothness, rim) * inputData.
        rimLightColor.rgb * inputData.rimLightColor.a;
    return rimLighting * lerp(mainLight.distanceAttenuation * mainLight.shadowAttenuation, 1,
                              inputData.rimLightOverShadow);
}

real3 DepthRimLighting(ASPInputData inputData, Light mainLight, float3 baseColor)
{
    float3 offsetPosVS = float3(inputData.positionVS.xy + inputData.normalVS.xy * inputData.depthRimLightStrength * 0.1,
                                inputData.positionVS.z);
    float4 offsetPosCS = TransformWViewToHClip(offsetPosVS);
    float4 offsetPosVP = TransformHClipToViewPortPos(offsetPosCS);
    float offsetDepth = SampleSceneDepth(offsetPosVP.xy);
    float linearEyeOffsetDepth = LinearEyeDepth(offsetDepth, _ZBufferParams);
    float depth = SampleSceneDepth(inputData.normalizedScreenSpaceUV);
    float linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams);
    float depthDiff = linearEyeOffsetDepth - linearEyeDepth;
    float rimMask = smoothstep(0, 0.9, depthDiff);
    real3 depthRimLighting = rimMask * inputData.depthRimLightColor.rgb * inputData.depthRimLightColor.a;
    depthRimLighting *= lerp(mainLight.distanceAttenuation * mainLight.shadowAttenuation, 1,
                             inputData.rimLightOverShadow);
    return lerp(baseColor, depthRimLighting, rimMask);
}

real3 HairLighting(ASPInputData inputData)
{
    #ifdef _HAIRMAP
        float3 hairViewUp = mul((float3x3)UNITY_MATRIX_I_V, float3(0, 1, 0));
        float cosViewUp = dot(hairViewUp, float3(0, 1, 0));
        float3 hairViewRight = normalize(cross(hairViewUp, mul((float3x3)UNITY_MATRIX_I_V, float3(1, 0, 0))));
        float offsetDir = 1 * step(hairViewRight.y, 0) + -1 * step(0, hairViewRight.y);
        float uvYoffset = offsetDir * saturate(1.0 - cosViewUp) * inputData.hairLightCameraRollInfluence;

        half hairMask = SAMPLE_TEXTURE2D(_HairHighlightMaskMap, sampler_HairHighlightMaskMap,
                                         inputData.hairMaskMapUV + float2(0, uvYoffset)).r;
        half3 hairCol = hairMask * inputData.hairLightColor.rgb * inputData.hairLightStrength;
        float factor = 1;
        float hairFresnelMask = pow(saturate(dot(inputData.viewDirectionWS, inputData.normalWS)),
                                    inputData.hairLightFresnelMaskPower);
        factor *= smoothstep(hairFresnelMask - 0.2, hairFresnelMask + 0.2, 0.4);
        factor *= step(inputData.hairUVSideCut, inputData.hairMaskMapUV.x);
        factor *= step(inputData.hairMaskMapUV.x, 1.0 - inputData.hairUVSideCut);
        return hairCol * factor * inputData.hairLightColor.a;
    #else
        return real3(0,0,0);
    #endif
    
}

void HandleOffsetShadow(inout ASPInputData inputData, Light mainLight)
{
    float4 scaledScreenParams = GetScaledScreenParams();
    float3 viewLightDir = normalize(TransformWorldToViewDir(mainLight.direction)) * (1 / inputData.positionNDC.w);
    float2 samplingPoint = ComputeNormalizedDeviceCoordinates(inputData.positionWS, unity_MatrixVP) + inputData.offsetShadowDistance * viewLightDir.xy /
        scaledScreenParams.xy;
    float animeShadowDepth = SampleCharacterDepthOffsetShadow(samplingPoint).r;
    half offsetMaterialID = SAMPLE_TEXTURE2D_LOD(_ASPMaterialTexture, asp_point_clamp_sampler, samplingPoint, 0).r;
    offsetMaterialID *= 255.0;
    //check if is same material
    half visibleFactor = step(0.002, abs(offsetMaterialID - _MaterialID));
    visibleFactor *= step(0.1   , offsetMaterialID);
    if (LinearEyeDepth(inputData.positionCS.z, _ZBufferParams) - ASP_OFFSET_SHADOW_EYE_BIAS > LinearEyeDepth(animeShadowDepth, _ZBufferParams) && visibleFactor > 0)
    {
        inputData.offsetShadowAttenuation = 0;
    }
}

void HandleASPShadowMap(inout ASPInputData inputData, inout Light mainLight)
{
    ShadowSamplingData shadowSamplingData = GetASPShadowSamplingData();
    half4 shadowParams = GetASPMainLightShadowParams();

    half aspShadow = SampleShadowmap(
        TEXTURE2D_ARGS(_ASPShadowMap, sampler_ASPShadowMap), inputData.aspShadowCoord, shadowSamplingData, shadowParams,
        false);
    aspShadow = lerp(aspShadow, 1, GetASPMainLightShadowFade(inputData.positionWS));
    if(_ASPShadowMapValid > 0)
    {
        mainLight.shadowAttenuation *= aspShadow;
    }
}

void HandleCustomLightDirection(inout ASPInputData inputData, inout Light mainLight)
{
    float fakeLightCtrl = step(0.5, inputData.overrideLightDir);
    float4 fakeLightPitch = rotate_angle_axis(DegToRad(inputData.fakeLightEuler.x), float3(1, 0, 0));
    float4 fakeLightYaw = rotate_angle_axis(DegToRad(inputData.fakeLightEuler.y), float3(0, 1, 0));
    float4 fakeLightRoll = rotate_angle_axis(DegToRad(inputData.fakeLightEuler.z), float3(0, 0, 1));
    float4 rot = qmul(fakeLightRoll, fakeLightYaw);
    rot = qmul(rot, fakeLightPitch);
    float3 fakeLightDir = rotate_vector(float3(0, 0, 1), rot);
    mainLight.direction = lerp(mainLight.direction, fakeLightDir, fakeLightCtrl);
}

void HandleCustomLightColor(inout ASPInputData inputData, inout Light mainLight)
{
    float fakeLightColorToggle = step(0.5, inputData.overrideLightColorIntensity);
    real3 fakeLightColor = lerp(mainLight.color, inputData.fakeLightColor, fakeLightColorToggle);
    mainLight.color =  fakeLightColor;
}

uint GetMeshRenderingLayerCompatible()
{
    #if UNITY_VERSION >= 202201
    return GetMeshRenderingLayer();
    #else
    return GetMeshRenderingLightLayer();
    #endif
}

bool IsPointInsideCube(float3 pointWS, float3 cubeCenter, float cubeSize)
{
    float3 halfSize = float3(cubeSize * 0.5, cubeSize * 0.5, cubeSize * 0.5);
    float3 minBound = cubeCenter - halfSize;
    float3 maxBound = cubeCenter + halfSize;

    return (pointWS.x >= minBound.x && pointWS.x <= maxBound.x) &&
        (pointWS.y >= minBound.y && pointWS.y <= maxBound.y) &&
        (pointWS.z >= minBound.z && pointWS.z <= maxBound.z);
}

bool IntersectRayWithAABB(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax, out float3 hitPoint)
{
    float3 tMin = (boxMin - rayOrigin) / rayDir;
    float3 tMax = (boxMax - rayOrigin) / rayDir;
    float3 t1 = min(tMin, tMax);
    float3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);

    if (tNear > tFar || tFar < 0)
        return false;

    hitPoint = rayOrigin + rayDir * tNear;
    return true;
}

float SampleShadowFromAABB(float3 pixelWS, float3 lightDir)
{
    float halfExtents = _CharacterCenterCubeSize * 0.5f;
    float3 aabbMin = _CharacterCenterWS - float3(halfExtents,halfExtents,halfExtents);
    float3 aabbMax = _CharacterCenterWS + float3(halfExtents,halfExtents,halfExtents);

    float3 hitPoint;
    bool intersected = IntersectRayWithAABB(pixelWS, -lightDir, aabbMin, aabbMax, hitPoint);

    if (!intersected)
        return 1.0; // No intersection, treat as fully lit
    float4 shadowCoord = TransformWorldToShadowCoord(hitPoint);
    // Sample the shadow map
    float shadow = real(SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, shadowCoord.xyz));
    return shadow;
}

real4 UniversalFragmentToonLit(ASPInputData inputData, bool overrideGIColor)
{
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    if (_UseSimpleAABBCutOffForCharacterShadow > 0)
    {
        // simple self shadow cut out trick 
        mainLight.shadowAttenuation = SampleShadowFromAABB(inputData.positionWS, mainLight.direction);
        // simple self shadow cut out trick end   
    }

    HandleCustomLightDirection(inputData, mainLight);
    HandleCustomLightColor(inputData, mainLight);
    
    #ifdef _RECEIVE_OFFSETED_SHADOW_ON
    HandleOffsetShadow(inputData, mainLight);
    #endif
    
    #ifdef _RECEIVE_ASP_SHADOW
    HandleASPShadowMap(inputData, mainLight);
    #endif

    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV,
                                                                   inputData.occlusion);
    #if defined(_SCREEN_SPACE_OCCLUSION)
        mainLight.color *= aoFactor.directAmbientOcclusion;
    #endif

    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, 1);

    BRDFData brdfData;
    InitializeBRDFData(inputData.albedo.rgb, inputData.metallic, 0, inputData.smoothness, inputData.albedo.a, brdfData);

    half3 finalColor = half3(0, 0, 0);
    
    #ifdef _SSSMAP
    half3 sssColor = SAMPLE_TEXTURE2D_X(
        _SSSRampMap, sampler_SSSRampMap, float2(saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), 0.5)).rgb;
    inputData.bakedGI = sssColor * inputData.bakedGI;
    #endif
    half3 giColor = overrideGIColor
                        ? inputData.bakedGI * inputData.albedo.rgb * inputData.occlusion
                        : GlobalIllumination(brdfData, inputData.bakedGI, inputData.occlusion, inputData.positionWS,
                                             inputData.normalWS, inputData.viewDirectionWS);
    finalColor += giColor;

    uint meshRenderingLayers = GetMeshRenderingLayerCompatible();
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
    #endif
    {
        finalColor += ToonLighting(mainLight, inputData, brdfData);
    }
    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        //handle extra directional light
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            finalColor += ToonAdditionalLighting(light, inputData, brdfData);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        //additional light
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            finalColor += ToonAdditionalLighting(light, inputData, brdfData);
        }
    LIGHT_LOOP_END
    #endif

    #ifdef _RIMLIGHTING_ON
    real rimClip = SAMPLE_TEXTURE2D_X(_RimLightMaskMap, sampler_RimLightMaskMap, inputData.rimUV * float2(20, 1)).r;
    finalColor += FrenselRimLighting(inputData, mainLight) * rimClip;
    #endif

    #ifdef _DEPTH_RIMLIGHTING_ON
    finalColor = DepthRimLighting(inputData, mainLight, finalColor);
    #endif

    finalColor += HairLighting(inputData);

    finalColor += MatCapHightlightWithMask(inputData, mainLight);

    finalColor += inputData.emission;
    return real4(finalColor, inputData.albedo.a * inputData.baseColor.a);
}

real4 UniversalFragmentToonEyeLit(ASPInputData inputData, bool overrideGIColor)
{
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    #ifdef _RECEIVE_OFFSETED_SHADOW_ON
    HandleOffsetShadow(inputData, mainLight);
    #endif

    #ifdef _RECEIVE_ASP_SHADOW
    HandleASPShadowMap(inputData, mainLight);
    #endif

    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV,
                                                                   inputData.occlusion);
    #if defined(_SCREEN_SPACE_OCCLUSION)
    mainLight.color *= aoFactor.directAmbientOcclusion;
    #endif

    //TODO use shader feature to prevent unnecessary calculate
    HandleCustomLightDirection(inputData, mainLight);
    HandleCustomLightColor(inputData, mainLight);
    
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, 1);
    BRDFData brdfData;
    half3 finalColor = half3(0, 0, 0);
    
    half3 viewParallax = abs(normalize(TransformWorldToViewDir(inputData.viewDirectionWS)));
    #ifdef _EYE_HIGHLIGHT_MAP
    half3 lightParallax = normalize(TransformWorldToViewDir(mainLight.direction));
    half3 eyeH = normalize(viewParallax + lightParallax);
    half eyeNdotHFlat = (dot(eyeH, -inputData.frontDirectionWS));
    
    float2 eyeHighlightUV = inputData.uv;
    float maxRotation = inputData.eyeHighlightRotateDegree;
    eyeHighlightUV = RotateUVDeg(eyeHighlightUV, float2(0.5,0.5),min(eyeNdotHFlat.x * maxRotation, maxRotation));
    real4 eyeHighlightCtrl = SAMPLE_TEXTURE2D_X(_EyeHighlightMap1, sampler_EyeHighlightMap1, eyeHighlightUV).rgba;
    real3 eyeHighlightCol = (1.0 - inputData.highLightAlphaClip) * eyeHighlightCtrl.rgb +
        inputData.highLightAlphaClip * eyeHighlightCtrl.rgb * step(0.5, eyeHighlightCtrl.a);
    real eyeNdotLFlat = max(saturate(dot(inputData.frontDirectionWS, mainLight.direction)), 0.1);
    finalColor += eyeHighlightCol * lerp(inputData.eyeHighlightColor.rgb, eyeNdotLFlat * inputData.eyeHighlightColor.rgb, inputData.highlightDarken);

    #endif
    
    #ifdef _PARALLAX
    half3 tangentView = viewParallax;
    float2 parallaxUV = inputData.uv - tangentView.xy * inputData.parallaxHeight;
    float2 originUV = inputData.uv;
    inputData.uv = lerp(originUV, parallaxUV, inputData.pupilMask);
    #endif

    real4 sampledAlbedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, inputData.uv).rgba;
    inputData.albedo.rgb = inputData.baseColor.rgb * sampledAlbedo.rgb;
    inputData.albedo.a = sampledAlbedo.a;
    InitializeBRDFData(inputData.albedo.rgb, inputData.metallic, 0, inputData.smoothness, inputData.albedo.a, brdfData);

    half3 giColor = overrideGIColor
                        ? inputData.bakedGI * inputData.albedo.rgb * inputData.occlusion
                        : GlobalIllumination(brdfData, inputData.bakedGI, inputData.occlusion, inputData.positionWS,
                                             inputData.normalWS, inputData.viewDirectionWS);
    finalColor += giColor;

    uint meshRenderingLayers = GetMeshRenderingLayerCompatible();
    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
    #endif
    {
        finalColor += ToonEyeLighting(mainLight, inputData, brdfData);
    }

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        //handle extra directional light
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            finalColor += ToonAdditionalLighting(light, inputData, brdfData);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        //additional light
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            finalColor += ToonAdditionalLighting(light, inputData, brdfData);
        }
    LIGHT_LOOP_END
    #endif

    finalColor += inputData.emission;

    finalColor += MatCapHightlight(inputData, mainLight) * lerp(1, inputData.pupilMask, inputData.usePupilMask);

    return real4(finalColor, inputData.albedo.a * inputData.baseColor.a);
}

#endif
