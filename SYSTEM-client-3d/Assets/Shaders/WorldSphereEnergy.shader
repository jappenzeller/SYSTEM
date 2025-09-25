Shader "SYSTEM/WorldSphereEnergy"
{
    Properties
    {
        [Header(Base Layer)]
        _BaseColor ("Base Color", Color) = (0.05, 0.1, 0.2, 1)
        _EmissionColor ("Emission Color", Color) = (0.1, 0.3, 0.5, 1)
        _PulseSpeed ("Pulse Speed", Range(0.1, 2)) = 0.5
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.3

        [Header(Quantum Grid)]
        _GridColor ("Grid Line Color", Color) = (0.2, 0.8, 1.0, 0.3)
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.05)) = 0.01
        _LongitudeLines ("Longitude Lines", Int) = 12
        _LatitudeLines ("Latitude Lines", Int) = 8
        _StateMarkerColor ("State Marker Color", Color) = (1.0, 0.5, 0.0, 0.8)
        _StateMarkerSize ("State Marker Size", Range(0.01, 0.1)) = 0.03
        _EquatorIntensity ("Equator Highlight", Range(1, 3)) = 2
        _PoleIntensity ("Pole Highlight", Range(1, 3)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        // Pass 1: Base pulsing layer
        Pass
        {
            Name "BaseLayer"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _PulseSpeed;
                float _PulseIntensity;
                float4 _GridColor;
                float _GridLineWidth;
                int _LongitudeLines;
                int _LatitudeLines;
                float4 _StateMarkerColor;
                float _StateMarkerSize;
                float _EquatorIntensity;
                float _PoleIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Simple pulsing effect
                float pulse = (sin(_Time.y * _PulseSpeed) + 1) * 0.5;

                // Blend between base and emission color
                float3 color = lerp(_BaseColor.rgb, _EmissionColor.rgb, pulse * _PulseIntensity);

                return half4(color, 1);
            }
            ENDHLSL
        }

        // Pass 2: Quantum grid overlay with Bloch sphere markers
        Pass
        {
            Name "QuantumGrid"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _PulseSpeed;
                float _PulseIntensity;
                float4 _GridColor;
                float _GridLineWidth;
                int _LongitudeLines;
                int _LatitudeLines;
                float4 _StateMarkerColor;
                float _StateMarkerSize;
                float _EquatorIntensity;
                float _PoleIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Convert to spherical coordinates
                float3 normalizedPos = normalize(input.positionOS);
                float theta = acos(normalizedPos.y); // 0 at north pole, π at south pole
                float phi = atan2(normalizedPos.z, normalizedPos.x); // Azimuth angle

                // Create longitude lines
                float longitudeGrid = sin(phi * _LongitudeLines);
                float longitudeLines = smoothstep(_GridLineWidth, 0, abs(longitudeGrid));

                // Create latitude lines
                float latitudeGrid = sin(theta * _LatitudeLines);
                float latitudeLines = smoothstep(_GridLineWidth, 0, abs(latitudeGrid));

                // Combine grid lines
                float gridIntensity = max(longitudeLines, latitudeLines);

                // Highlight equator (theta = π/2)
                float equatorDistance = abs(theta - 1.5708); // π/2 ≈ 1.5708
                float equatorHighlight = smoothstep(0.1, 0, equatorDistance) * _EquatorIntensity;

                // Highlight poles (theta near 0 or π)
                float northPoleDistance = theta;
                float southPoleDistance = abs(theta - 3.14159);
                float poleHighlight = (smoothstep(0.2, 0, northPoleDistance) +
                                      smoothstep(0.2, 0, southPoleDistance)) * _PoleIntensity;

                // Define Bloch sphere state positions
                float3 statePositions[6] = {
                    float3(0, 1, 0),    // |0⟩ north pole
                    float3(0, -1, 0),   // |1⟩ south pole
                    float3(1, 0, 0),    // |+⟩ positive x
                    float3(-1, 0, 0),   // |-⟩ negative x
                    float3(0, 0, 1),    // |+i⟩ positive z
                    float3(0, 0, -1)    // |-i⟩ negative z
                };

                // Calculate state marker intensity
                float markerIntensity = 0;
                for (int i = 0; i < 6; i++)
                {
                    float dist = distance(normalizedPos, statePositions[i]);
                    float marker = smoothstep(_StateMarkerSize * 1.5, _StateMarkerSize * 0.5, dist);
                    markerIntensity = max(markerIntensity, marker);
                }

                // Apply highlights to grid
                gridIntensity = gridIntensity * (1 + equatorHighlight + poleHighlight);

                // Combine grid and markers
                float3 gridColor = _GridColor.rgb * gridIntensity;
                float3 markerColor = _StateMarkerColor.rgb * markerIntensity * 2; // Extra glow

                // Final color with alpha
                float3 finalColor = gridColor + markerColor;
                float alpha = saturate((gridIntensity + markerIntensity) * _GridColor.a);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}