// =============================================================================
// MMO Toon Character shader
// -----------------------------------------------------------------------------
// A from-scratch stylized (toon / cel-shaded) character shader for
// Unity 6 (URP 17). Structure inspired by ColinLeung-NiloCat's example,
// fully rewritten with modern syntax and extra features:
//   - stepped shadow ramp (1..N bands)
//   - toon specular highlight
//   - rim / fresnel light
//   - normal map support
//   - inverted-hull outline (FOV/distance stable + ZOffset)
//
// Passes:
//   0 ForwardLit        -> the visible shaded color
//   1 Outline           -> inverted hull outline (rendered as SRPDefaultUnlit)
//   2 ShadowCaster      -> URP shadow maps
//   3 DepthOnly         -> URP depth prepass / _CameraDepthTexture
//   4 DepthNormalsOnly  -> URP depth+normals prepass (SSAO etc.)
//
// Edit MMOToonCharacter_Lighting.hlsl to change the look.
// =============================================================================
Shader "MMO/Toon Character"
{
    Properties
    {
        [Header(High Level Setting)][Space(4)]
        [ToggleUI]_IsFace("Is Face? (face / eye / mouth)", Float) = 0

        [Header(Base Color)][Space(4)]
        [MainTexture]_BaseMap("Base Map", 2D) = "white" {}
        [HDR][MainColor]_BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Normal Map)][Space(4)]
        [Toggle(_NORMALMAP)]_UseNormalMap("Enable?", Float) = 0
        [NoScaleOffset][Normal]_BumpMap("    Normal Map", 2D) = "bump" {}
        _BumpScale("    Scale", Range(0,2)) = 1

        [Header(Alpha Clipping)][Space(4)]
        [Toggle(_USEALPHACLIPPING)]_UseAlphaClipping("Enable?", Float) = 0
        _Cutoff("    Cutoff", Range(0,1)) = 0.5

        [Header(Direct Light  Cel Shading)][Space(4)]
        _RampSteps("Shadow Steps (1 = 2 tone)", Range(1,5)) = 1
        _CelMidPoint("MidPoint", Range(-1,1)) = -0.2
        _CelSoftness("Softness", Range(0,1)) = 0.05
        [HDR]_ShadowTint("Shadow Color", Color) = (0.7,0.6,0.65,1)
        _DirectLightMultiplier("Brightness", Range(0,2)) = 1
        _AdditionalLightMultiplier("Additional Light Brightness", Range(0,2)) = 0.5

        [Header(Indirect Light)][Space(4)]
        _IndirectLightMinColor("Min Color", Color) = (0.1,0.1,0.1,1)
        _IndirectLightMultiplier("Multiplier", Range(0,2)) = 1

        [Header(Light Clamp)][Space(4)]
        _LightMinLimit("Min Limit (never darker than)", Range(0,1)) = 0.05
        _LightMaxLimit("Max Limit (never brighter than)", Range(0,2)) = 1

        [Header(Shadow Mapping)][Space(4)]
        _ReceiveShadowMappingAmount("Strength", Range(0,1)) = 0.65
        _ReceiveShadowMappingPosOffset("    Depth Bias", Float) = 0

        [Header(Rim Light)][Space(4)]
        [Toggle(_RIMLIGHT)]_UseRimLight("Enable?", Float) = 0
        [HDR]_RimColor("    Color", Color) = (1,1,1,1)
        _RimMin("    Min", Range(0,1)) = 0.5
        _RimMax("    Max", Range(0,1)) = 0.85
        _RimAlignLight("    Follow Light Dir", Range(0,1)) = 0.5

        [Header(Toon Specular)][Space(4)]
        [Toggle(_TOONSPECULAR)]_UseToonSpecular("Enable?", Float) = 0
        [HDR]_SpecularColor("    Color", Color) = (1,1,1,1)
        _SpecularSize("    Size", Range(0,1)) = 0.1
        _SpecularSoftness("    Softness", Range(0,1)) = 0.05

        [Header(Emission)][Space(4)]
        [Toggle(_EMISSION)]_UseEmission("Enable?", Float) = 0
        [HDR]_EmissionColor("    Color", Color) = (0,0,0,1)
        _EmissionMulByBaseColor("    Mul Base Color", Range(0,1)) = 0
        [NoScaleOffset]_EmissionMap("    Emission Map", 2D) = "white" {}
        _EmissionMapChannelMask("        Channel Mask", Vector) = (1,1,1,0)

        [Header(Occlusion)][Space(4)]
        [Toggle(_OCCLUSION)]_UseOcclusion("Enable?", Float) = 0
        _OcclusionStrength("    Strength", Range(0,1)) = 1
        [NoScaleOffset]_OcclusionMap("    Occlusion Map", 2D) = "white" {}
        _OcclusionMapChannelMask("        Channel Mask", Vector) = (1,0,0,0)

        [Header(Outline)][Space(4)]
        _OutlineWidth("Width", Range(0,4)) = 1
        _OutlineColor("Color", Color) = (0.1,0.1,0.1,1)
        _OutlineColorMulBaseColor("    Tint By Base Color", Range(0,1)) = 0.3

        [Header(Outline Distance Fade)][Space(4)]
        _OutlineFadeStart("    Fade Start (m)", Float) = 20
        _OutlineFadeEnd("    Fade End (m)", Float) = 40

        [Header(Outline ZOffset)][Space(4)]
        _OutlineZOffset("ZOffset (View Space)", Range(0,1)) = 0.0001
        [NoScaleOffset]_OutlineZOffsetMaskTex("    Mask (black = apply ZOffset)", 2D) = "black" {}
        _OutlineZOffsetMaskRemapStart("    Remap Start", Range(0,1)) = 0
        _OutlineZOffsetMaskRemapEnd("    Remap End", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"        = "UniversalPipeline"
            "RenderType"            = "Opaque"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector"       = "True"
            "Queue"                 = "Geometry"
        }
        LOD 300

        // shared keywords for every pass
        HLSLINCLUDE
        #pragma shader_feature_local_fragment _USEALPHACLIPPING
        #pragma shader_feature_local          _NORMALMAP
        ENDHLSL

        // ---------------------------------------------------------------------
        // [#0] ForwardLit
        // ---------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex   VertexShaderWork
            #pragma fragment ShadeFinalColor

            // material features
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _OCCLUSION
            #pragma shader_feature_local_fragment _RIMLIGHT
            #pragma shader_feature_local_fragment _TOONSPECULAR

            // URP lighting keywords (Unity 6 / URP 17)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog

            // GPU instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "MMOToonCharacter_Shared.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // [#1] Outline  (no LightMode -> URP draws it as SRPDefaultUnlit)
        // ---------------------------------------------------------------------
        Pass
        {
            Name "Outline"

            Blend One Zero
            ZWrite On
            Cull Front
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex   VertexShaderWork
            #pragma fragment ShadeOutlineColor

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define ToonShaderIsOutline
            #include "MMOToonCharacter_Shared.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // [#2] ShadowCaster
        // ---------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex   VertexShaderWork
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_vertex   _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #define ToonShaderApplyShadowBiasFix
            #include "MMOToonCharacter_Shared.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // [#3] DepthOnly
        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex   VertexShaderWork
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // outline area must also write depth so it occludes correctly
            #define ToonShaderIsOutline
            #include "MMOToonCharacter_Shared.hlsl"
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        // [#4] DepthNormalsOnly  (used by SSAO etc.)
        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            ZTest LEqual
            ColorMask RGBA
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex   VertexShaderWork
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "MMOToonCharacter_Shared.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
