// =============================================================================
// MMOToonCharacter_Lighting.hlsl
// -----------------------------------------------------------------------------
// THIS is the file you edit to change the look of the shader.
// All the "math" that decides the final color lives here. Be creative.
// Structure inspired by ColinLeung-NiloCat/UnityURPToonLitShaderExample,
// rewritten for Unity 6 (URP 17) with stepped shadow ramp, toon specular,
// rim light and normal-map-aware shading.
// =============================================================================
#pragma once

// -----------------------------------------------------------------------------
// Toon ramp: turns a raw N.L (-1..1) into a stepped 0..1 lit factor.
// _RampSteps    : how many shading bands (1 = classic 2-tone, 2+ = posterized)
// _CelMidPoint  : shifts the light/dark balance
// _CelSoftness  : how soft the transition between bands is (0 = hard cut)
// -----------------------------------------------------------------------------
half ToonRamp(half NoL)
{
    half t      = saturate((NoL - _CelMidPoint) * 0.5 + 0.5);
    half steps  = max(1.0, _RampSteps);
    half scaled = t * steps;
    half band   = floor(scaled);
    half f      = scaled - band;                                   // 0..1 inside the band
    half soft   = smoothstep(0.5 - _CelSoftness * 0.5,
                             0.5 + _CelSoftness * 0.5, f);
    return saturate((band + soft) / steps);
}

// -----------------------------------------------------------------------------
// Indirect / ambient light (flat-ish, so the toon look is preserved).
// -----------------------------------------------------------------------------
half3 ShadeGI(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    // sample spherical harmonics (light probes). Keep some directionality but
    // it stays soft, which reads well for stylized characters.
    half3 ambient = SampleSH(lightingData.normalWS);

    // never let indirect go fully black if probes were not baked
    ambient = max(_IndirectLightMinColor.rgb, ambient);

    // indirect gets at most ~50% darkened by occlusion to avoid crushed blacks
    half indirectOcclusion = lerp(1.0, surfaceData.occlusion, 0.5);

    return ambient * indirectOcclusion * _IndirectLightMultiplier;
}

// -----------------------------------------------------------------------------
// One direct light (main directional OR an additional point/spot light).
// Returns diffuse radiance + a separate toon specular term so highlights are
// added on top (not multiplied by albedo).
// -----------------------------------------------------------------------------
ToonLightResult ShadeSingleLight(ToonSurfaceData surfaceData, ToonLightingData lightingData, Light light, bool isAdditionalLight)
{
    half3 N = lightingData.normalWS;
    half3 L = light.direction;
    half3 V = lightingData.viewDirectionWS;

    half NoL = dot(N, L);

    // clamp distance attenuation so a close point/spot light can't blow out
    half distanceAttenuation = min(4.0, light.distanceAttenuation);

    // stepped cel ramp
    half litFactor = ToonRamp(NoL);

    // occlusion darkens the lit area
    litFactor *= surfaceData.occlusion;

    // faces look bad with hard N.L shading -> lift the dark side a bit
    litFactor = _IsFace ? lerp(0.5, 1.0, litFactor) : litFactor;

    // realtime shadow map
    litFactor *= lerp(1.0, light.shadowAttenuation, _ReceiveShadowMappingAmount);

    // tint the shadowed area instead of going to pure black
    half3 litColor = lerp(_ShadowTint.rgb, half3(1.0, 1.0, 1.0), litFactor);

    half lightMul = isAdditionalLight ? _AdditionalLightMultiplier : _DirectLightMultiplier;

    ToonLightResult result;
    result.diffuse  = saturate(light.color) * litColor * distanceAttenuation * lightMul;
    result.specular = 0;

    // ---- toon specular (stepped highlight) -----------------------------------
    // Skip on faces: a hard NoH blob on skin reads as "oily/plastic" and is the
    // classic mistake on stylized characters. Faces get their shine from the
    // eye/lip maps instead, never from this term.
#ifdef _TOONSPECULAR
    if (!_IsFace)
    {
        half3 H        = SafeNormalize(L + V);
        half  NoH      = saturate(dot(N, H));
        half  edge     = 1.0 - _SpecularSize;
        half  specMask = smoothstep(edge, edge + max(1e-4h, _SpecularSoftness), NoH);

        // only show the highlight where the surface is actually lit
        result.specular = _SpecularColor.rgb * specMask * litFactor
                        * saturate(light.color) * distanceAttenuation
                        * (isAdditionalLight ? 0.5 : 1.0);
    }
#endif

    return result;
}

// -----------------------------------------------------------------------------
// Rim / fresnel light along the silhouette (great for anime characters).
// -----------------------------------------------------------------------------
half3 ShadeRimLight(ToonSurfaceData surfaceData, ToonLightingData lightingData, Light mainLight)
{
#ifdef _RIMLIGHT
    half3 N = lightingData.normalWS;
    half3 V = lightingData.viewDirectionWS;

    half fresnel = 1.0 - saturate(dot(N, V));
    half rimMask = smoothstep(_RimMin, _RimMax, fresnel);

    // optionally bias the rim toward the side facing the main light
    half NoL       = dot(N, mainLight.direction);
    half lightSide = lerp(1.0, saturate(NoL), _RimAlignLight);

    return _RimColor.rgb * rimMask * lightSide;
#else
    return 0;
#endif
}

// -----------------------------------------------------------------------------
// Emission.
// -----------------------------------------------------------------------------
half3 ShadeEmission(ToonSurfaceData surfaceData, ToonLightingData lightingData)
{
    return lerp(surfaceData.emission,
                surfaceData.emission * surfaceData.albedo,
                _EmissionMulByBaseColor);
}

// -----------------------------------------------------------------------------
// Final composite of every lighting result.
//   diffuse  -> multiplied by albedo (it is the surface color)
//   specular -> added on top (highlights are not tinted by albedo)
//   rim      -> added on top
//   emission -> added on top
// -----------------------------------------------------------------------------
half3 CompositeAllLightResults(
    half3 indirect,
    ToonLightResult mainLight,
    half3 additionalDiffuse,
    half3 additionalSpecular,
    half3 rim,
    half3 emission,
    ToonSurfaceData surfaceData,
    ToonLightingData lightingData)
{
    // pick the brightest between indirect and direct so it never double-darkens
    half3 diffuseSum  = max(indirect, mainLight.diffuse + additionalDiffuse);
    half3 specularSum = mainLight.specular + additionalSpecular;

    // Light clamp (same idea as lilToon's LightMin/MaxLimit & UTS).
    // Keeps the lit term inside [min,max] so a bright patch in the albedo
    // (e.g. the cheek on a face texture) can't be multiplied past 1.0 and
    // blow out into a white blob. This is what hid that ball in lilToon.
    diffuseSum = clamp(diffuseSum, _LightMinLimit, _LightMaxLimit);

    return surfaceData.albedo * diffuseSum + specularSum + rim + emission;
}
