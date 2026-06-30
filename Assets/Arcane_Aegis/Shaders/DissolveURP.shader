Shader "Arcane/DissolveURP"
{
    // A lightweight URP lit shader with a noise-driven DISSOLVE + glowing edge, for spawn ("materialize in") and
    // despawn ("dissolve out") of mobs/pets/mounts. DissolveEffect swaps a model's materials to instances of this,
    // copies the base map/color, animates _DissolveAmount, then restores (in) or destroys (out). The noise is supplied
    // by the component (procedural), so no texture asset is required.
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _NoiseTex ("Dissolve Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 1
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0.001,0.5)) = 0.08
        [HDR] _EdgeColor ("Edge Color", Color) = (1, 0.5, 0.1, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off // skinned models read better dissolving from both sides

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EdgeColor;
                float  _NoiseScale;
                float  _DissolveAmount;
                float  _EdgeWidth;
            CBUFFER_END

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = p.positionCS;
                OUT.normalWS    = n.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, IN.uv * _NoiseScale).r;
                clip(noise - _DissolveAmount); // burn away where the noise is below the threshold

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Simple URP lighting: main light diffuse + ambient (SH), enough to read during a brief effect.
                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float  ndotl = saturate(dot(N, mainLight.direction));
                float3 ambient = SampleSH(N);
                float3 lit = baseTex.rgb * (ambient + mainLight.color * ndotl);

                // Glowing edge right at the dissolve front (only while dissolving, so the visible state has no rim).
                float dissolving = step(0.001, _DissolveAmount);
                float edge = (1.0 - saturate((noise - _DissolveAmount) / _EdgeWidth)) * dissolving;
                float3 emission = _EdgeColor.rgb * edge;

                half3 color = MixFog(lit + emission, IN.fogFactor);
                return half4(color, baseTex.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
