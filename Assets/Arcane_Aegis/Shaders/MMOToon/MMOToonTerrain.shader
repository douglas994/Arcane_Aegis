// =============================================================================
// MMO Toon Terrain shader
// -----------------------------------------------------------------------------
// A stylized (toon / cel-shaded) terrain shader for Unity 6 (URP 17) that needs
// NO textures: it colours the ground PROCEDURALLY by world height and slope, so
// you get grass in the valleys, rock on cliffs and high ground, and snow on the
// peaks — all flat, cel-shaded colours that match the MMO toon characters.
//
// Change the 4 colours + the height/slope thresholds to re-skin a whole biome
// (Veridia green, Khazgor snow, Rugnor volcanic, ...).
//
// HOW TO USE:
//   1. Create a Material with this shader (MMO/Toon Terrain).
//   2. Select your Terrain -> Terrain Settings (gear icon) -> Material -> assign it.
//   3. If it looks wrong, turn OFF "Draw Instanced" in the same Terrain Settings
//      (this shader is a normal mesh shader, not a splatmap TerrainLit shader).
//
// Lighting matches our character look: stepped cel ramp, tinted shadows,
// soft ambient, main light + shadows. Additional lights kept simple for perf.
// =============================================================================
Shader "MMO/Toon Terrain"
{
    Properties
    {
        [Header(Layer Colors)][Space(4)]
        _GrassColor("Grass (low / flat)", Color) = (0.35,0.55,0.25,1)
        _RockColor ("Rock (slopes / high)", Color) = (0.45,0.40,0.35,1)
        _CliffColor("Cliff (steepest)",     Color) = (0.30,0.27,0.24,1)
        _SnowColor ("Snow (peaks)",         Color) = (0.92,0.93,0.97,1)

        [Header(Height Bands  (world meters))][Space(4)]
        _GrassEnd  ("Grass ends at Y",  Float) = 40
        _RockEnd   ("Rock ends at Y",   Float) = 120
        _SnowStart ("Snow starts at Y", Float) = 150
        _HeightBlend("Band blend (m)",  Float) = 12

        [Header(Slope to Rock_Cliff)][Space(4)]
        _SlopeRock ("Slope -> rock (0..1)",  Range(0,1)) = 0.35
        _SlopeCliff("Slope -> cliff (0..1)", Range(0,1)) = 0.65
        _SlopeBlend("Slope blend",           Range(0.01,0.5)) = 0.12

        [Header(Toon Lighting)][Space(4)]
        _RampSteps ("Shadow Steps (1 = 2 tone)", Range(1,5)) = 2
        _CelMidPoint("MidPoint", Range(-1,1)) = -0.1
        _CelSoftness("Softness", Range(0,1)) = 0.08
        [HDR]_ShadowTint("Shadow Color", Color) = (0.6,0.62,0.72,1)
        _IndirectLightMinColor("Ambient Min", Color) = (0.25,0.27,0.32,1)
        _ReceiveShadowStrength("Shadow Map Strength", Range(0,1)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry-100"   // terrain renders before geometry
            "TerrainCompatible" = "True"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GrassColor, _RockColor, _CliffColor, _SnowColor;
                float _GrassEnd, _RockEnd, _SnowStart, _HeightBlend;
                half  _SlopeRock, _SlopeCliff, _SlopeBlend;
                half  _RampSteps, _CelMidPoint, _CelSoftness, _ReceiveShadowStrength;
                half4 _ShadowTint, _IndirectLightMinColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            // stepped toon ramp (same idea as the character shader)
            half ToonRamp(half NoL)
            {
                half t      = saturate((NoL - _CelMidPoint) * 0.5 + 0.5);
                half steps  = max(1.0, _RampSteps);
                half scaled = t * steps;
                half band   = floor(scaled);
                half f      = scaled - band;
                half soft   = smoothstep(0.5 - _CelSoftness * 0.5,
                                         0.5 + _CelSoftness * 0.5, f);
                return saturate((band + soft) / steps);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);

                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS   = n.normalWS;
                OUT.fogFactor  = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            // pick the base (unlit) ground colour from height + slope
            half3 GroundColor(float3 positionWS, float3 normalWS)
            {
                float y = positionWS.y;

                // ---- height blend: grass -> rock -> snow ----
                half toRock = smoothstep(_GrassEnd - _HeightBlend, _GrassEnd + _HeightBlend, y);
                half toSnow = smoothstep(_SnowStart - _HeightBlend, _SnowStart + _HeightBlend, y);

                half3 col = lerp(_GrassColor.rgb, _RockColor.rgb, toRock);
                col = lerp(col, _SnowColor.rgb, toSnow);

                // ---- slope: flatter ground keeps grass, steep becomes rock/cliff ----
                // slope01 = 0 on flat ground, 1 on a vertical wall
                half slope01 = 1.0 - saturate(dot(normalWS, half3(0,1,0)));

                half rockMask  = smoothstep(_SlopeRock  - _SlopeBlend, _SlopeRock  + _SlopeBlend, slope01);
                half cliffMask = smoothstep(_SlopeCliff - _SlopeBlend, _SlopeCliff + _SlopeBlend, slope01);

                // steep areas override the height colour with rock, then cliff.
                // (don't override snow peaks fully, so high cliffs still read snowy-ish)
                col = lerp(col, _RockColor.rgb,  rockMask * (1.0 - toSnow * 0.5));
                col = lerp(col, _CliffColor.rgb, cliffMask);

                return col;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                float3 N = normalize(IN.normalWS);
                half3 albedo = GroundColor(IN.positionWS, N);

                // main light + shadows
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half NoL = dot(N, mainLight.direction);
                half lit = ToonRamp(NoL);
                lit *= lerp(1.0, mainLight.shadowAttenuation, _ReceiveShadowStrength);

                half3 litColor = lerp(_ShadowTint.rgb, half3(1,1,1), lit);
                half3 direct   = saturate(mainLight.color) * litColor;

                // soft ambient (flat, keeps the toon look), never fully black
                half3 ambient = max(_IndirectLightMinColor.rgb, SampleSH(N));

                half3 color = albedo * max(ambient, direct);

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // shadow casting so the terrain casts shadows on itself & objects
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            V shadowVert(A IN)
            {
                V OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS   = TransformObjectToWorldNormal(IN.normalOS);
                float4 cs    = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, cs.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = cs;
                return OUT;
            }

            half4 shadowFrag(V IN) : SV_TARGET { return 0; }
            ENDHLSL
        }

        // depth for URP depth prepass / SSAO
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };

            V depthVert(A IN)
            {
                V OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half depthFrag(V IN) : SV_TARGET { return IN.positionCS.z; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
