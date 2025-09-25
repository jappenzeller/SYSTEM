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
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.1)) = 0.05
        _LongitudeLines ("Longitude Lines", Float) = 12
        _LatitudeLines ("Latitude Lines", Float) = 8
        _StateMarkerColor ("State Marker Color", Color) = (1.0, 0.5, 0.0, 1)
        _StateMarkerSize ("State Marker Size", Range(0.01, 0.2)) = 0.05
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

        // Pass 2: Grid overlay - DEBUG TEST
        Pass
        {
            Name "GridOverlay"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertGrid
            #pragma fragment fragGrid

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
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

            Varyings vertGrid(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 fragGrid(Varyings input) : SV_Target
            {
                // STEP 1: TEST WITH SOLID YELLOW TO VERIFY PASS IS EXECUTING
                // If you see yellow on the sphere, Pass 2 is working
                // return half4(1, 1, 0, 0.5); // Semi-transparent yellow

                // STEP 2: TEST WITH YELLOW STRIPES
                float stripes = step(0.5, frac(input.uv.x * 10.0));
                return half4(stripes, stripes, 0, stripes * 0.5);

                /* STEP 3: ACTUAL GRID (uncomment after Step 2 works)
                #define PI 3.14159265359

                // Calculate grid lines
                float gridU = abs(sin(input.uv.x * _LongitudeLines * PI));
                float gridV = abs(sin(input.uv.y * _LatitudeLines * PI));

                // Create lines with threshold
                float lineU = step(1.0 - _GridLineWidth, gridU);
                float lineV = step(1.0 - _GridLineWidth, gridV);

                float grid = max(lineU, lineV);

                // Output grid with proper alpha
                return half4(_GridColor.rgb, grid);
                */
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}