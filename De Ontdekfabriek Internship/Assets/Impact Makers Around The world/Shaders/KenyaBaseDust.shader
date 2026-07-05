// Kenya base dust (lit): a URP shader that tints anything below a world-height threshold toward laterite
// dust, so props and buildings look like dust has settled at their foot. The blend is by WORLD Y, so ONE
// material on many props gives every one a dusty base for free, no per-asset painting.
//
// It is LIT: it responds to the day-cycle sun (with shadows) and picks up the warm skybox ambient, so a
// dusted prop still shades and reacts to the time of day like everything else, and it sits in the URP fog/haze.
// Flat-shaded low-poly art looks right with this simple Lambert model. Keeps a ShadowCaster + DepthOnly pass so
// props still cast shadows and work with depth-based effects. Tune _DustTop/_DustBottom (world Y) and _DustStrength.
Shader "KenyaScooter/BaseDust"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _DustColor ("Dust Color (laterite)", Color) = (0.62, 0.40, 0.28, 1)
        _DustTop ("Dust Top (world Y, clean above)", Float) = 0.5
        _DustBottom ("Dust Bottom (world Y, full dust below)", Float) = 0.0
        _DustStrength ("Dust Strength", Range(0, 1)) = 0.85
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1.0
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _DustColor;
                float _DustTop;
                float _DustBottom;
                float _DustStrength;
                float _AmbientBoost;
            CBUFFER_END

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
                half4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // How dusty this fragment is: 0 at/above the top line, 1 at/below the bottom line.
                float denom = max(0.0001, _DustTop - _DustBottom);
                float dust = saturate((_DustTop - IN.positionWS.y) / denom) * _DustStrength;
                half3 albedo = lerp(baseCol.rgb, _DustColor.rgb, dust);

                // Simple lit model: main-light Lambert with shadows, plus warm skybox ambient (SH).
                float3 normalWS = normalize(IN.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 diffuse = mainLight.color
                              * (mainLight.distanceAttenuation * mainLight.shadowAttenuation)
                              * saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS) * _AmbientBoost;

                half3 color = albedo * (diffuse + ambient);
                color = MixFog(color, IN.fogCoord);
                return half4(color, baseCol.a);
            }
            ENDHLSL
        }

        // Props still cast shadows.
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
