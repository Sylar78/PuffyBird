// Shader éclairé stylisé de PuffyBird (URP).
// - Éclairage physique URP complet : soleil avec ombres douces, lumières ponctuelles,
//   lumière ambiante, reflets de sonde, SSAO si activé, brouillard.
// - Liseré lumineux (rim light) pour détacher les volumes du décor.
// - Couleur = texture × couleur × couleur de sommet (un seul matériau pour un objet multicolore).
//   L'alpha de la couleur de sommet sert de masque à l'émission des sommets (fenêtres, lanternes).
// - Paillettes : points brillants qui scintillent, fixés à la surface de l'objet.
// - Déformations de sommets partagées par toutes les passes (les ombres suivent) :
//   vent (végétation), gonflement (nuages, oiseau), vibration d'impact (tuyaux),
//   flexion (ailes).
Shader "PuffyBird/StylizedLit"
{
    Properties
    {
        _BaseColor ("Couleur", Color) = (1, 1, 1, 1)
        _BaseMap ("Texture", 2D) = "white" {}
        _EmissionColor ("Émission", Color) = (0, 0, 0, 1)
        _VertexEmission ("Émission des couleurs de sommet", Range(0, 4)) = 0
        _Metallic ("Métal", Range(0, 1)) = 0
        _Smoothness ("Lissé", Range(0, 1)) = 0.35
        _RimColor ("Couleur du liseré", Color) = (1, 1, 1, 1)
        _RimStrength ("Force du liseré", Range(0, 2)) = 0.35
        _RimPower ("Finesse du liseré", Range(0.5, 8)) = 3
        _WindStrength ("Vent : amplitude", Float) = 0
        _WindFrequency ("Vent : fréquence", Float) = 1.5
        _WindHeight ("Vent : hauteur de référence", Float) = 1
        _WindSpread ("Vent : décalage de phase dans l'objet", Float) = 0
        _BreathStrength ("Gonflement", Float) = 0
        _WobbleAmount ("Vibration d'impact", Float) = 0
        _WobbleFrequency ("Vibration : fréquence", Float) = 30
        _BendAmount ("Flexion", Float) = 0
        _ScrollOffset ("Défilement des UV", Vector) = (0, 0, 0, 0)
        _Glitter ("Paillettes", Range(0, 4)) = 0
        _GlitterScale ("Paillettes : densité", Float) = 60
        _GlitterColor ("Paillettes : couleur", Color) = (1, 0.95, 0.7, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _EmissionColor;
            half _VertexEmission;
            half _Metallic;
            half _Smoothness;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
            float _WindStrength;
            float _WindFrequency;
            float _WindHeight;
            float _WindSpread;
            float _BreathStrength;
            float _WobbleAmount;
            float _WobbleFrequency;
            float _BendAmount;
            float4 _ScrollOffset;
            half _Glitter;
            float _GlitterScale;
            half4 _GlitterColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Déformation en espace objet, identique dans toutes les passes.
        float3 PuffyDisplace(float3 positionOS, float3 normalOS)
        {
            float t = _Time.y;
            float3 origin = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
            float phase = origin.x * 0.7 + origin.z * 0.31;
            float3 p = positionOS;
            // Objets faits de nombreux éléments (touffes d'herbe) : chacun ondule à son rythme.
            float windPhase = phase + (positionOS.x * 1.7 + positionOS.z * 2.3) * _WindSpread;

            // Vent : balancement plus fort vers le haut de l'objet.
            float h = saturate(positionOS.y / max(_WindHeight, 1e-3));
            float sway = h * h * _WindStrength;
            p.x += sin(t * _WindFrequency + windPhase) * sway;
            p.z += cos(t * _WindFrequency * 0.73 + windPhase) * sway * 0.4;

            // Gonflement : respiration le long de la normale.
            p += normalOS * (sin(t * 2.7 + phase + positionOS.y * 7.0) * 0.5 + 0.5) * _BreathStrength;

            // Vibration d'impact : onde qui parcourt l'objet.
            p += normalOS * sin(t * _WobbleFrequency - positionOS.y * 6.0) * _WobbleAmount;

            // Flexion : le bout (loin de l'axe z = 0) fléchit, pour les plumes des ailes.
            p.y += _BendAmount * positionOS.z * positionOS.z;
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half4 fogAndVertexLight : TEXCOORD4;
                float3 positionOS : TEXCOORD5;
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD6;
            #endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionOS = PuffyDisplace(input.positionOS.xyz, input.normalOS);
                VertexPositionInputs position = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS);

                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap) + _ScrollOffset.xy;
                output.color = input.color;
                output.positionOS = input.positionOS.xyz;

                half3 vertexLight = VertexLighting(position.positionWS, normal.normalWS);
                half fogFactor = ComputeFogFactor(position.positionCS.z);
                output.fogAndVertexLight = half4(fogFactor, vertexLight);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(position);
            #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = texel.rgb * _BaseColor.rgb * input.color.rgb;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = input.fogAndVertexLight.x;
                inputData.vertexLighting = input.fogAndVertexLight.yzw;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.alpha = 1.0;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.specular = half3(0, 0, 0);
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1.0;
                surface.emission = _EmissionColor.rgb + albedo * (_VertexEmission * input.color.a);

                // Paillettes : une cellule sur six environ porte un éclat qui s'allume et s'éteint
                // à son propre rythme ; les cellules sont fixes sur l'objet (elles suivent l'oiseau).
                if (_Glitter > 0.0)
                {
                    float3 cell = floor(input.positionOS * _GlitterScale);
                    float h = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                    float speed = 5.0 + frac(h * 17.13) * 9.0;
                    float twinkle = pow(saturate(sin(_Time.y * speed + h * 40.0)), 10.0);
                    float3 local = frac(input.positionOS * _GlitterScale) - 0.5;
                    float dotShape = saturate(1.0 - length(local) * 2.4);
                    surface.emission += _GlitterColor.rgb * (step(0.83, h) * twinkle * dotShape * _Glitter * 6.0);
                }

                half4 color = UniversalFragmentPBR(inputData, surface);

                // Liseré : les bords tournés à l'opposé de la caméra s'illuminent.
                half fresnel = pow(1.0h - saturate(dot(inputData.normalWS, inputData.viewDirectionWS)), _RimPower);
                Light mainLight = GetMainLight();
                color.rgb += _RimColor.rgb * fresnel * _RimStrength * (0.35h + 0.65h * mainLight.color);

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 positionOS = PuffyDisplace(input.positionOS.xyz, input.normalOS);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
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

            half4 ShadowFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 DepthVert(DepthAttributes input) : SV_POSITION
            {
                return TransformObjectToHClip(PuffyDisplace(input.positionOS.xyz, input.normalOS));
            }

            half DepthFrag(float4 positionCS : SV_POSITION) : SV_Target
            {
                return positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                output.positionCS = TransformObjectToHClip(PuffyDisplace(input.positionOS.xyz, input.normalOS));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(DepthNormalsVaryings input) : SV_Target
            {
            #if defined(_GBUFFER_NORMALS_OCT)
                float3 normalWS = normalize(input.normalWS);
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                return half4(packedNormalWS, 0.0);
            #else
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
