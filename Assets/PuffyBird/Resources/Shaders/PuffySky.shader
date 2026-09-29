// Ciel de PuffyBird (URP) : dégradé calé sur l'écran, halo et disque du soleil ou de la lune,
// étoiles scintillantes la nuit. Non éclairé, sans brouillard, dessiné en arrière-plan.
Shader "PuffyBird/Sky"
{
    Properties
    {
        _TopColor ("Haut du ciel", Color) = (0.18, 0.6, 0.84, 1)
        _HorizonColor ("Horizon", Color) = (0.66, 0.9, 0.88, 1)
        _SunColor ("Soleil", Color) = (1, 0.96, 0.78, 1)
        _SunPosition ("Position du soleil (écran)", Vector) = (0.25, 0.8, 0, 0)
        _SunSize ("Taille du soleil", Range(0, 0.2)) = 0.05
        _StarDensity ("Densité d'étoiles", Range(0, 0.05)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Background"
        }

        Pass
        {
            Name "Sky"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _SunColor;
                float4 _SunPosition;
                half _SunSize;
                half _StarDensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);

                // Dégradé : horizon vers 30 % de la hauteur de l'écran, zénith en haut.
                float t = saturate((uv.y - 0.28) / 0.72);
                t = t * t * (3.0 - 2.0 * t);
                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, t);

                // Soleil ou lune : disque net et halo large (le bloom fait le reste).
                float2 d = (uv - _SunPosition.xy) * float2(aspect, 1.0);
                float dist = length(d);
                float disc = smoothstep(_SunSize, _SunSize * 0.85, dist);
                float halo = exp(-dist * 6.0) * 0.45 + exp(-dist * 18.0) * 0.35;
                color += _SunColor.rgb * (disc * 1.6 + halo);

                // Étoiles : grille fixe à l'écran, scintillement lent.
                if (_StarDensity > 0.0)
                {
                    float2 grid = uv * float2(90.0 * aspect, 90.0);
                    float2 cell = floor(grid);
                    float n = Hash(cell);
                    if (n < _StarDensity * 10.0)
                    {
                        float2 center = cell + 0.5 + (float2(Hash(cell + 7.1), Hash(cell + 3.7)) - 0.5) * 0.6;
                        float star = smoothstep(0.18, 0.0, length(grid - center));
                        float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.5 + n * 40.0) + n * 100.0);
                        color += star * twinkle * saturate(t * 1.5) * 1.8;
                    }
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
