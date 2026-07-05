// Kenya road sand (lit cutout with a granular dissolve edge): the shoulder-sand shader for RoadEdgeSand.
// The problem it solves: a plain alpha-cutout snaps from sand to asphalt in a hard line, however ragged the
// mask shape is. This shader keeps the cutout (opaque, lit, no transparency sorting against the road) but adds
// per-grain NOISE to the alpha before the clip, so the boundary dissolves into individual grains of sand
// scattering onto the tarmac — the mask's density gradient turns into a speckle density, which is exactly how
// wind-blown sand actually thins out across a road.
//
// The noise is hashed from the SAME scrolled UV space as the mask, so the grains scroll with the sand instead
// of shimmering in place. Lighting mirrors KenyaBaseDust: main-light Lambert with shadows + warm skybox ambient
// (SH) + URP fog, so the sand shades with the day cycle like the rest of the ground.
//
// Lives in a Resources folder ON PURPOSE: RoadEdgeSand finds it with Shader.Find at runtime, and shaders that
// are only referenced from code get stripped from device builds unless they are in Resources or the Always
// Included list. RoadEdgeSand falls back to URP/Lit cutout if this shader is ever missing.
Shader "KenyaScooter/RoadSand"
{
    Properties
    {
        _BaseMap ("Sand Map (A = density mask)", 2D) = "white" {}
        _BaseColor ("Sand Color (laterite)", Color) = (0.66, 0.40, 0.26, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _NoiseCells ("Dissolve Grain Cells (x = across, y = along)", Vector) = (110, 240, 0, 0)
        _NoiseStrength ("Dissolve Strength", Range(0, 1)) = 0.38
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        LOD 150
        Cull Off // the strips are runtime quads; double-sided is the safety net for winding

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Cutoff;
            float4 _NoiseCells;
            float _NoiseStrength;
            float _AmbientBoost;
        CBUFFER_END

        // Cheap hash noise on a UV grid cell. Because it keys off the scrolled UV, the grain pattern travels
        // with the sand texture; a world-space hash would make the dissolve crawl against the scroll.
        float GrainNoise(float2 uv)
        {
            float2 cell = floor(uv * _NoiseCells.xy);
            return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
        }

        // Shared alpha logic: mask density + grain jitter, clipped. Returns the sampled texture for the lit pass.
        half4 SampleSandClipped(float2 uv)
        {
            half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            float grain = GrainNoise(uv);
            float a = tex.a + (grain - 0.5) * _NoiseStrength;
            clip(a - _Cutoff);
            return tex;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogCoord : TEXCOORD3;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = normals.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SampleSandClipped(IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                // Same simple lit model as KenyaScooter/BaseDust: Lambert + shadows + warm SH ambient.
                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 diffuse = mainLight.color
                              * (mainLight.distanceAttenuation * mainLight.shadowAttenuation)
                              * saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS) * _AmbientBoost;

                half3 color = albedo * (diffuse + ambient);
                color = MixFog(color, IN.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // Depth for depth-based effects, clipped with the SAME dissolve so depth matches the visible sand.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            struct AttributesD { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct VaryingsD { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            VaryingsD depthVert(AttributesD IN)
            {
                VaryingsD OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 depthFrag(VaryingsD IN) : SV_Target
            {
                SampleSandClipped(IN.uv);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
