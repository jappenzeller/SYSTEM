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
        _GridColor ("Grid Line Color", Color) = (0.2, 0.8, 1.0, 1)
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.1)) = 0.01
        _LongitudeLines ("Longitude Lines", Float) = 12
        _LatitudeLines ("Latitude Lines", Float) = 8
        _StateMarkerColor ("State Marker Color", Color) = (1.0, 0.5, 0.0, 1)
        _StateMarkerSize ("State Marker Size", Range(0.01, 0.2)) = 0.03
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
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EmissionColor;
                float _PulseSpeed;
                float _PulseIntensity;
                float4 _GridColor;
                float _GridLineWidth;
                float _LongitudeLines;
                float _LatitudeLines;
                float4 _StateMarkerColor;
                float _StateMarkerSize;
                float _EquatorIntensity;
                float _PoleIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Use proper URP transformation function
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.localPos = input.positionOS.xyz;
                return output;
            }

            #ifndef PI
            #define PI 3.14159265359
            #endif

            half4 frag(Varyings input) : SV_Target
            {
                // Base pulsing effect
                float pulse = (sin(_Time.y * _PulseSpeed) + 1) * 0.5;
                float3 baseColor = lerp(_BaseColor.rgb, _EmissionColor.rgb, pulse * _PulseIntensity);

                // Grid calculation with thin lines
                float3 normalized = normalize(input.localPos);
                float phi = atan2(normalized.z, normalized.x);
                float theta = acos(normalized.y);

                // Very thin lines using adjustable threshold
                float lineThreshold = 1.0 - _GridLineWidth;
                float longLine = step(lineThreshold, abs(sin(phi * _LongitudeLines * 0.5)));
                float latLine = step(lineThreshold, abs(sin(theta * _LatitudeLines)));
                float totalGrid = max(longLine, latLine);

                // Quantum state markers at 6 positions
                float marker = 0;

                // |0⟩ state (north pole)
                float dist0 = distance(normalized, float3(0, 1, 0));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5, _StateMarkerSize, dist0));

                // |1⟩ state (south pole)
                float dist1 = distance(normalized, float3(0, -1, 0));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5, _StateMarkerSize, dist1));

                // |+⟩ state (positive x on equator)
                float distPlus = distance(normalized, float3(1, 0, 0));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5, _StateMarkerSize, distPlus));

                // |-⟩ state (negative x on equator)
                float distMinus = distance(normalized, float3(-1, 0, 0));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5, _StateMarkerSize, distMinus));

                // |+i⟩ state (positive z on equator)
                float distPlusI = distance(normalized, float3(0, 0, 1));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5, _StateMarkerSize, distPlusI));

                // |-i⟩ state (negative z on equator)
                float distMinusI = distance(normalized, float3(0, 0, -1));
                marker = max(marker, 1.0 - smoothstep(_StateMarkerSize * 0.5, _StateMarkerSize, distMinusI));

                // Blend base with grid
                float3 color = lerp(baseColor, _GridColor.rgb, totalGrid * _GridColor.a);

                // Add quantum markers on top
                color = lerp(color, _StateMarkerColor.rgb, marker * _StateMarkerColor.a);

                return half4(color, 1);
            }
            ENDHLSL
        }

    }

    FallBack "Universal Render Pipeline/Unlit"
}