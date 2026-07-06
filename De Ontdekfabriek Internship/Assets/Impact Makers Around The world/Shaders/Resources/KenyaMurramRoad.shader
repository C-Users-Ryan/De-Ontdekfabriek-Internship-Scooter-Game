// Kenya murram road (lit, procedural): turns the Synty toon road model into a red-laterite DIRT (murram) road
// WITHOUT any texture and WITHOUT caring about the model's UVs. That is the whole point: the road model is a
// single mesh whose asphalt / curbs / yellow line all come from one shared ATLAS texture, so a plain colour
// tint floods it flat red and a sand texture smears the atlas UVs into mush. This shader throws the atlas away
// and paints the surface from WORLD POSITION + SURFACE NORMAL instead, so it can never go flat and never needs
// good UVs.
//
// Two ideas do the work:
//   1) TRIPLANAR value-noise grain (sampled from world XZ/ZY/XY, blended by the normal) gives real granular
//      murram detail on every face, independent of UVs.
//   2) NORMAL GRADING: faces that point up (the driving surface and the tops of the raised shoulders you can't
//      remove from the model) read as packed red murram; faces that point sideways (the raised curb walls) read
//      as lighter, looser wind-blown sand. The bit of geometry you were fighting becomes a graded sandy bank.
//
// The road tiles physically scroll toward the fixed player, so a pure world-space pattern would slide under the
// road ("swim"). The global _KenyaRoadScroll (fed by MurramRoadScroll, = metres travelled, 0 in the editor)
// is added back into Z so the grain stays glued to the tarmac. Lighting mirrors KenyaScooter/BaseDust
// (main-light Lambert + shadows + warm SH ambient + URP fog) so it sits in the day cycle like everything else.
//
// Lives in Resources so Shader.Find keeps it in device builds. Apply via
// Tools > Kenya Scooter > Road Look > Make Road Sandy (Murram) on Selection.
Shader "KenyaScooter/MurramRoad"
{
    Properties
    {
        [Header(Colours)]
        _RoadColor ("Packed Murram (driving surface)", Color) = (0.50, 0.30, 0.18, 1)
        _SandColor ("Loose Sand (banks and edges)", Color) = (0.74, 0.57, 0.38, 1)
        _RutColor  ("Wheel Rut / packed dark", Color) = (0.34, 0.19, 0.11, 1)

        [Header(Detail Texture)]
        _DirtTex ("Dirt Detail (tiling, mapped triplanar)", 2D) = "gray" {}
        _DirtScale ("Dirt Tiling (tiles per metre)", Range(0.02, 2)) = 0.35
        _DetailContrast ("Detail Contrast", Range(0, 3)) = 1.3

        [Header(Grain)]
        _GrainScale ("Grain Scale (per metre)", Range(0.2, 12)) = 3.5
        _GrainStrength ("Grain Strength", Range(0, 1)) = 0.35
        _PatchScale ("Weathered Patch Scale", Range(0.02, 2)) = 0.18
        _PatchStrength ("Weathered Patch Strength", Range(0, 1)) = 0.22

        [Header(Raised Edges)]
        _BankStart ("Bank Blend Start (normal.y)", Range(0, 1)) = 0.30
        _BankEnd ("Bank Blend End (normal.y)", Range(0, 1)) = 0.80
        _BankSand ("Bank Sand Amount", Range(0, 1)) = 0.85

        [Header(Wheel ruts)]
        _RutOffset ("Rut Offset from centre (m)", Range(0, 6)) = 1.6
        _RutWidth ("Rut Width (m)", Range(0.05, 3)) = 0.7
        _RutStrength ("Rut Strength", Range(0, 1)) = 0.30

        [Header(Lighting)]
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1.3
        _Flatness ("Cartoon Flatness (0 lit .. 1 flat)", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RoadColor;
                float4 _SandColor;
                float4 _RutColor;
                float _GrainScale;
                float _GrainStrength;
                float _DirtScale;
                float _DetailContrast;
                float _PatchScale;
                float _PatchStrength;
                float _BankStart;
                float _BankEnd;
                float _BankSand;
                float _RutOffset;
                float _RutWidth;
                float _RutStrength;
                float _AmbientBoost;
                float _Flatness;
            CBUFFER_END

            // Global scroll (metres travelled), set by MurramRoadScroll. Unset = 0, so the pattern is simply
            // static in the editor. NOT in the per-material CBUFFER — it is a shader global.
            float _KenyaRoadScroll;

            // ---- Cheap procedural noise (no textures) ----------------------------------------------------
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float VNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i + float2(0, 0));
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 3 octaves keeps it mobile-friendly; drop _GrainScale if a tablet struggles. Each octave ROTATES and
            // scales the domain (iq's trick) so the value-noise grid never lines up into visible axis-aligned
            // blocks or a repeating tiling pattern — the surface reads as organic dust instead of a texture that
            // obviously repeats.
            float Fbm(float2 p)
            {
                // Base rotation so even the dominant first octave sits on a diagonal, never axis-aligned with the
                // (axis-aligned) road and tile grid.
                p = mul(float2x2(0.86, 0.51, -0.51, 0.86), p);
                float sum = 0.0, amp = 0.5;
                const float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6); // ~37 deg rotate + 2x scale per octave
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    sum += amp * VNoise(p);
                    p = mul(m, p);
                    amp *= 0.5;
                }
                return sum; // ~0 .. ~0.875
            }

            // Triplanar grain so the detail lands on every face regardless of UVs.
            float TriplanarGrain(float3 wp, float3 n, float scale)
            {
                float3 w = pow(abs(n), 4.0);
                w /= (w.x + w.y + w.z + 1e-5);
                float nx = Fbm(wp.zy * scale);
                float ny = Fbm(wp.xz * scale);
                float nz = Fbm(wp.xy * scale);
                return nx * w.x + ny * w.y + nz * w.z;
            }

            // Real tiling dirt/gravel texture, sampled TRIPLANAR in world space so it needs no UVs and wraps the
            // raised banks. This provides the actual surface DETAIL (grains, pebbles, clumps); the road's COLOUR
            // still comes from the palette, this only drives how light/dark each spot is.
            TEXTURE2D(_DirtTex);
            SAMPLER(sampler_DirtTex);

            float3 TriplanarTex(float3 wp, float3 n, float scale)
            {
                float3 w = pow(abs(n), 4.0);
                w /= (w.x + w.y + w.z + 1e-5);
                float3 cx = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, wp.zy * scale).rgb;
                float3 cy = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, wp.xz * scale).rgb;
                float3 cz = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, wp.xy * scale).rgb;
                return cx * w.x + cy * w.y + cz * w.z;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(IN.normalOS);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = normals.normalWS;
                OUT.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float up = saturate(n.y);

                // Glue the along-road pattern to the moving road (see header): add metres travelled back into Z.
                float3 wp = IN.positionWS;
                wp.z += _KenyaRoadScroll;

                // REAL surface detail: sample the tiling dirt/gravel texture triplanar (world space, so it ignores
                // the model's atlas UVs and wraps the banks) and take its luminance — this is where the grains,
                // pebbles and clumps come from, far richer than procedural noise.
                float3 texRGB = TriplanarTex(wp, n, _DirtScale);
                float detail = dot(texRGB, float3(0.299, 0.587, 0.114)); // 0 dark crevice .. 1 bright grain

                // Large-scale drift (rotated fbm) so the tiling texture never reads as a repeating pattern across
                // many tiles — lighter and darker stretches roll over the road.
                float macro = Fbm(wp.xz * _PatchScale) - 0.5;

                // The road COLOUR is the cartoony red palette, its light/dark driven by the real texture detail
                // (contrast-boosted) plus the macro drift. Biased bright/sandy; dark tone only in the deepest spots.
                float tone = saturate(0.5 + (detail - 0.5) * _DetailContrast + macro * (0.4 + _PatchStrength * 1.2));
                float3 col = lerp(_RutColor.rgb, _RoadColor.rgb, saturate(tone * 1.7));
                col = lerp(col, _SandColor.rgb, saturate((tone - 0.58) * 1.7));

                // Faint packed wheel tracks.
                float d = abs(abs(wp.x) - _RutOffset);
                float rut = (1.0 - smoothstep(0.0, _RutWidth, d)) * _RutStrength * up;
                col = lerp(col, _RutColor.rgb, rut * 0.5);

                // Raised edges -> lighter loose sand.
                float bank = (1.0 - smoothstep(_BankStart, _BankEnd, up)) * _BankSand;
                col = lerp(col, _SandColor.rgb, bank);
                col = max(col, 0.0);

                // CARTOON-FLAT lighting: half-Lambert so the surface never crushes to a dark muddy tone, warm SH
                // ambient, then BLEND toward the pure albedo (_Flatness) so it reads as a flat toon colour matching
                // the Synty assets instead of a heavily shaded, dirty-looking surface.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float ndl = saturate(dot(n, mainLight.direction) * 0.5 + 0.5); // half-Lambert wrap
                half3 sun = mainLight.color * mainLight.shadowAttenuation * ndl;
                half3 ambient = SampleSH(n) * _AmbientBoost;
                half3 lit = col * (sun + ambient);
                half3 outColor = lerp(lit, col, saturate(_Flatness)); // toward flat toon albedo
                outColor = MixFog(outColor, IN.fogCoord);
                return half4(outColor, 1);
            }
            ENDHLSL
        }

        // The road still casts shadows.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct AttributesS { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct VaryingsS { float4 positionHCS : SV_POSITION; };

            float4 GetShadowPositionHClip(AttributesS IN)
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            VaryingsS shadowVert(AttributesS IN)
            {
                VaryingsS OUT;
                OUT.positionHCS = GetShadowPositionHClip(IN);
                return OUT;
            }

            half4 shadowFrag(VaryingsS IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth prepass / depth-based effects.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AttributesD { float4 positionOS : POSITION; };
            struct VaryingsD { float4 positionHCS : SV_POSITION; };

            VaryingsD depthVert(AttributesD IN)
            {
                VaryingsD OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag(VaryingsD IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
