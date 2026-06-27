// =============================================================================
// MMOToonCharacter_Shared.hlsl
// -----------------------------------------------------------------------------
// Shared plumbing for every pass: includes, textures, CBUFFER, structs,
// the vertex shader and the fragment shaders.
// You normally DON'T need to edit this file to change the look -> edit
// MMOToonCharacter_Lighting.hlsl instead.
//
// Target: Unity 6 (6000.x) + URP 17. Modern syntax only:
//   TEXTURE2D/SAMPLER + SAMPLE_TEXTURE2D, #pragma target 4.5,
//   _CLUSTER_LIGHT_LOOP (Forward+) with LIGHT_LOOP_BEGIN/END.
// =============================================================================
#pragma once

// Core brings the SRP shader library, space-conversion helpers, fog, packing
// (UnpackNormalScale lives here), instancing macros, etc.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Lighting brings GetMainLight/GetAdditionalLight, shadows, GI, the Forward+
// cluster light loop (LIGHT_LOOP_BEGIN/END), InputData, SSAO helpers, ...
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// note: suffix OS=object, WS=world, VS=view, CS=clip space

// -----------------------------------------------------------------------------
// Vertex input / output
// -----------------------------------------------------------------------------
struct Attributes
{
    float3 positionOS   : POSITION;
    half3  normalOS     : NORMAL;
    half4  tangentOS    : TANGENT;
    float2 uv           : TEXCOORD0;

    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                       : TEXCOORD0;
    float4 positionWSAndFogFactor   : TEXCOORD1; // xyz = positionWS, w = fog factor
    half3  normalWS                 : TEXCOORD2;
    half4  tangentWS                : TEXCOORD3; // xyz = tangent, w = sign (for normal map)
    float4 positionCS               : SV_POSITION;

    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// -----------------------------------------------------------------------------
// Textures (samplers declared the modern way, outside the CBUFFER)
// -----------------------------------------------------------------------------
TEXTURE2D(_BaseMap);                SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);                SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);            SAMPLER(sampler_EmissionMap);
TEXTURE2D(_OcclusionMap);           SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_OutlineZOffsetMaskTex);  SAMPLER(sampler_OutlineZOffsetMaskTex);

// -----------------------------------------------------------------------------
// Per-material uniforms. ALL of them must live in this single CBUFFER, or the
// SRP Batcher will refuse to batch this shader.
// -----------------------------------------------------------------------------
CBUFFER_START(UnityPerMaterial)
    // high level
    float  _IsFace;

    // base
    float4 _BaseMap_ST;
    half4  _BaseColor;

    // normal map
    half   _BumpScale;

    // alpha clip
    half   _Cutoff;

    // cel / direct light
    half   _CelMidPoint;
    half   _CelSoftness;
    half   _RampSteps;
    half4  _ShadowTint;
    half   _DirectLightMultiplier;
    half   _AdditionalLightMultiplier;

    // indirect light
    half4  _IndirectLightMinColor;
    half   _IndirectLightMultiplier;

    // light clamp (lilToon-style): caps how bright/dark the lit term can get,
    // prevents bright albedo patches from blowing out to white
    half   _LightMinLimit;
    half   _LightMaxLimit;

    // shadow mapping
    half   _ReceiveShadowMappingAmount;
    float  _ReceiveShadowMappingPosOffset;

    // rim light
    half4  _RimColor;
    half   _RimMin;
    half   _RimMax;
    half   _RimAlignLight;

    // toon specular
    half4  _SpecularColor;
    half   _SpecularSize;
    half   _SpecularSoftness;

    // emission
    half4  _EmissionColor;
    half   _EmissionMulByBaseColor;
    half3  _EmissionMapChannelMask;

    // occlusion
    half   _OcclusionStrength;
    half4  _OcclusionMapChannelMask;

    // outline
    float  _OutlineWidth;
    half4  _OutlineColor;
    half   _OutlineColorMulBaseColor;
    float  _OutlineZOffset;
    float  _OutlineZOffsetMaskRemapStart;
    float  _OutlineZOffsetMaskRemapEnd;
    float  _OutlineFadeStart; // camera distance (m) where outline starts shrinking
    float  _OutlineFadeEnd;   // camera distance (m) where outline reaches 0 width
CBUFFER_END

// used only by the ShadowCaster pass bias fix; not a per-material value
float3 _LightDirection;

// -----------------------------------------------------------------------------
// Data structs passed around the lighting code
// -----------------------------------------------------------------------------
struct ToonSurfaceData
{
    half3 albedo;
    half  alpha;
    half3 emission;
    half  occlusion;
};

struct ToonLightingData
{
    half3  normalWS;            // final normal (already includes the normal map)
    float3 positionWS;
    half3  viewDirectionWS;
    float2 normalizedScreenUV;  // for SSAO + Forward+ cluster loop
};

struct ToonLightResult
{
    half3 diffuse;
    half3 specular;
};

// =============================================================================
// Small self-contained helpers (ported from NiloCat utilities, modernized)
// =============================================================================

half InvLerpClamp(half from, half to, half value)
{
    return saturate((value - from) / (to - from));
}

// Keep the outline a stable width on screen across camera distance & FOV.
// camera FOV in degrees (so the outline width stays stable across FOVs)
float GetCameraFOV()
{
    float t = unity_CameraProjection._m11;
    const float Rad2Deg = 180.0 / 3.14159265;
    return atan(1.0 / t) * 2.0 * Rad2Deg;
}

half GetOutlineCameraFovAndDistanceFixMultiplier(float positionVS_Z)
{
    half cameraMulFix;
    if (unity_OrthoParams.w == 0)
    {
        // perspective: keep similar screen width across distance AND fov
        cameraMulFix  = abs(positionVS_Z);
        cameraMulFix  = saturate(cameraMulFix);
        cameraMulFix *= GetCameraFOV();
    }
    else
    {
        // orthographic (50 = magic number to roughly match perspective width)
        half orthoSize = saturate(abs(unity_OrthoParams.y));
        cameraMulFix = orthoSize * 50.0;
    }
    return cameraMulFix * 0.00005;
}

// Push the outline's clip-space Z toward the camera so it doesn't z-fight / clip
// through the mesh. (perspective-correct trick from NiloCat - kept verbatim)
float4 GetNewClipPosWithZOffset(float4 originalPositionCS, float viewSpaceZOffsetAmount)
{
    if (unity_OrthoParams.w == 0)
    {
        // perspective
        float2 ProjM_ZRow_ZW = UNITY_MATRIX_P[2].zw;
        float modifiedPositionVS_Z = -originalPositionCS.w + -viewSpaceZOffsetAmount; // push imaginary vertex
        float modifiedPositionCS_Z = modifiedPositionVS_Z * ProjM_ZRow_ZW[0] + ProjM_ZRow_ZW[1];
        originalPositionCS.z = modifiedPositionCS_Z * originalPositionCS.w / (-modifiedPositionVS_Z);
        return originalPositionCS;
    }
    else
    {
        // orthographic
        originalPositionCS.z += -viewSpaceZOffsetAmount / _ProjectionParams.z;
        return originalPositionCS;
    }
}

float3 TransformPositionWSToOutlinePositionWS(float3 positionWS, float positionVS_Z, float3 normalWS)
{
    float outlineExpand = _OutlineWidth * GetOutlineCameraFovAndDistanceFixMultiplier(positionVS_Z);

    // Distance fade: shrink the outline to 0 as the character gets far from the
    // camera. When width hits 0 the hull collapses onto the mesh -> the extra
    // outline pixels are killed by ZTest, removing overdraw for distant monsters.
    // (camera distance in perspective ~= abs(view-space Z) in meters)
    float camDistance = abs(positionVS_Z);
    float fade = 1.0 - saturate((camDistance - _OutlineFadeStart) / max(1e-4, _OutlineFadeEnd - _OutlineFadeStart));
    outlineExpand *= fade;

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED) || defined(UNITY_STEREO_DOUBLE_WIDE_ENABLED)
    outlineExpand *= 0.5;
#endif

    return positionWS + normalWS * outlineExpand;
}

// =============================================================================
// Vertex shader (shared by all passes)
//   - ToonShaderIsOutline          -> push verts out along normal (outline)
//   - ToonShaderApplyShadowBiasFix -> shadow caster bias fix
// =============================================================================
Varyings VertexShaderWork(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput  = GetVertexPositionInputs(input.positionOS);
    VertexNormalInputs   normalInput  = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    float3 positionWS = vertexInput.positionWS;

#ifdef ToonShaderIsOutline
    positionWS = TransformPositionWSToOutlinePositionWS(positionWS, vertexInput.positionVS.z, normalInput.normalWS);
#endif

    float fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

    output.uv                     = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionWSAndFogFactor = float4(positionWS, fogFactor);
    output.normalWS               = normalInput.normalWS;
    output.tangentWS              = half4(normalInput.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.positionCS             = TransformWorldToHClip(positionWS);

#ifdef ToonShaderIsOutline
    // read the outline ZOffset mask in the vertex stage (mip 0, no derivatives)
    half mask = SAMPLE_TEXTURE2D_LOD(_OutlineZOffsetMaskTex, sampler_OutlineZOffsetMaskTex, input.uv, 0).r;
    mask = 1.0 - mask; // black area = apply ZOffset (common mask convention)
    mask = InvLerpClamp(_OutlineZOffsetMaskRemapStart, _OutlineZOffsetMaskRemapEnd, mask);
    output.positionCS = GetNewClipPosWithZOffset(output.positionCS, _OutlineZOffset * mask + 0.03 * _IsFace);
#endif

#ifdef ToonShaderApplyShadowBiasFix
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, output.normalWS, _LightDirection));
    #if UNITY_REVERSED_Z
        positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
        positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif
    output.positionCS = positionCS;
#endif

    return output;
}

// =============================================================================
// Fragment helpers: build the surface & lighting data structs
// =============================================================================
half4 GetFinalBaseColor(Varyings input)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
}

half3 GetFinalEmissionColor(Varyings input)
{
#ifdef _EMISSION
    return SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb
         * _EmissionMapChannelMask * _EmissionColor.rgb;
#else
    return 0;
#endif
}

half GetFinalOcclusion(Varyings input)
{
    half result = 1;
#ifdef _OCCLUSION
    half4 texValue       = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv);
    half  occlusionValue = dot(texValue, _OcclusionMapChannelMask);
    result = lerp(1.0, occlusionValue, _OcclusionStrength);
#endif
    return result;
}

void DoClipTestToTargetAlphaValue(half alpha)
{
#ifdef _USEALPHACLIPPING
    clip(alpha - _Cutoff);
#endif
}

// resolve the per-pixel world normal (applies the normal map if enabled)
half3 GetFinalNormalWS(Varyings input)
{
#ifdef _NORMALMAP
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
    float sgn       = input.tangentWS.w;
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    half3x3 tbn      = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    half3 normalWS   = TransformTangentToWorld(normalTS, tbn);
#else
    half3 normalWS = input.normalWS;
#endif
    return NormalizeNormalPerPixel(normalWS);
}

ToonSurfaceData InitializeSurfaceData(Varyings input)
{
    ToonSurfaceData output = (ToonSurfaceData)0;

    half4 baseColor = GetFinalBaseColor(input);
    output.albedo   = baseColor.rgb;
    output.alpha    = baseColor.a;
    DoClipTestToTargetAlphaValue(output.alpha);

    output.emission  = GetFinalEmissionColor(input);
    output.occlusion = GetFinalOcclusion(input);

    return output;
}

ToonLightingData InitializeLightingData(Varyings input)
{
    ToonLightingData lightingData;
    lightingData.positionWS        = input.positionWSAndFogFactor.xyz;
    lightingData.viewDirectionWS   = SafeNormalize(GetCameraPositionWS() - lightingData.positionWS);
    lightingData.normalWS          = GetFinalNormalWS(input);
    lightingData.normalizedScreenUV = GetNormalizedScreenSpaceUV(input.positionCS);
    return lightingData;
}

// =============================================================================
// The editable lighting equations
// =============================================================================
#include "MMOToonCharacter_Lighting.hlsl"

// =============================================================================
// Gather every light and produce the final lit color.
//   Handles main light + additional lights (Forward AND Forward+ cluster).
// =============================================================================
half3 ShadeAllLights(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    // fold screen-space ambient occlusion into the surface occlusion
#if defined(_SCREEN_SPACE_OCCLUSION)
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(lightingData.normalizedScreenUV);
    surfaceData.occlusion = min(surfaceData.occlusion, aoFactor.indirectAmbientOcclusion);
#endif

    half3 indirect = ShadeGI(surfaceData, lightingData);

    float3 positionWS = lightingData.positionWS;
    half4  shadowMask = half4(1, 1, 1, 1); // dynamic characters: no baked shadow mask

    // ---- main light ----------------------------------------------------------
    // offset the shadow test position (helps hide ugly self-shadow on faces)
    float3 shadowTestPosWS = positionWS + GetMainLight().direction * (_ReceiveShadowMappingPosOffset + _IsFace);
    float4 shadowCoord     = TransformWorldToShadowCoord(shadowTestPosWS);
    Light  mainLight       = GetMainLight(shadowCoord, positionWS, shadowMask);

    ToonLightResult mainResult = ShadeSingleLight(surfaceData, lightingData, mainLight, false);

    // ---- additional lights ---------------------------------------------------
    half3 addDiffuse  = 0;
    half3 addSpecular = 0;

    uint pixelLightCount = GetAdditionalLightsCount();

    // InputData is required by the Forward+ cluster LIGHT_LOOP_BEGIN macro
    InputData inputData = (InputData)0;
    inputData.positionWS             = positionWS;
    inputData.normalWS               = lightingData.normalWS;
    inputData.viewDirectionWS        = lightingData.viewDirectionWS;
    inputData.normalizedScreenSpaceUV = lightingData.normalizedScreenUV;

#if USE_CLUSTER_LIGHT_LOOP
    // Forward+ keeps extra directional lights in their own buffer (outside the
    // cluster), so we shade those in a separate small loop.
    for (uint dIndex = 0; dIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dIndex++)
    {
        Light light = GetAdditionalLight(dIndex, positionWS, shadowMask);
        ToonLightResult r = ShadeSingleLight(surfaceData, lightingData, light, true);
        addDiffuse  += r.diffuse;
        addSpecular += r.specular;
    }
#endif

    // LIGHT_LOOP_BEGIN provides a 'uint lightIndex' (works for both classic
    // forward and Forward+ cluster) and reads 'inputData'. We fetch the Light
    // ourselves so we can run our toon equation on it.
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);
        ToonLightResult r = ShadeSingleLight(surfaceData, lightingData, light, true);
        addDiffuse  += r.diffuse;
        addSpecular += r.specular;
    LIGHT_LOOP_END

    // ---- rim + emission ------------------------------------------------------
    half3 rim      = ShadeRimLight(surfaceData, lightingData, mainLight);
    half3 emission = ShadeEmission(surfaceData, lightingData);

    return CompositeAllLightResults(indirect, mainResult, addDiffuse, addSpecular, rim, emission, surfaceData, lightingData);
}

half3 ApplyFog(half3 color, Varyings input)
{
    return MixFog(color, input.positionWSAndFogFactor.w);
}

// =============================================================================
// Fragment: forward lit (used by ForwardLit pass)
// =============================================================================
half4 ShadeFinalColor(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    ToonSurfaceData  surfaceData  = InitializeSurfaceData(input);
    ToonLightingData lightingData = InitializeLightingData(input);

    half3 color = ShadeAllLights(surfaceData, lightingData);
    color = ApplyFog(color, input);

    return half4(color, surfaceData.alpha);
}

// =============================================================================
// Fragment: outline (used by Outline pass) - flat color, no lighting (cheap)
// =============================================================================
half4 ShadeOutlineColor(Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 baseColor = GetFinalBaseColor(input);
    DoClipTestToTargetAlphaValue(baseColor.a);

    half3 outline = _OutlineColor.rgb * lerp(half3(1, 1, 1), baseColor.rgb, _OutlineColorMulBaseColor);
    outline = ApplyFog(outline, input);

    return half4(outline, baseColor.a);
}

// =============================================================================
// Fragments for the depth/shadow passes
// =============================================================================
void AlphaClipAndLODTest(Varyings input)
{
    DoClipTestToTargetAlphaValue(GetFinalBaseColor(input).a);
#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif
}

half DepthOnlyFragment(Varyings input) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    AlphaClipAndLODTest(input);
    return input.positionCS.z;
}

void DepthNormalsFragment(
    Varyings input
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    AlphaClipAndLODTest(input);

    half3 normalWS = GetFinalNormalWS(input);

#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS         = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    half3  packedNormalWS      = PackFloat2To888(remappedOctNormalWS);
    outNormalWS = half4(packedNormalWS, 0.0);
#else
    outNormalWS = half4(normalWS, 0.0);
#endif

#ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
#endif
}
