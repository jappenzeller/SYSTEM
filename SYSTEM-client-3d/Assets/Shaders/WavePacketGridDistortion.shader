Shader "SYSTEM/WavePacketGridDistortion"
{
    Properties
    {
        _MainTex ("Grid Texture", 2D) = "white" {}
        _GridColor ("Grid Color", Color) = (0.2, 0.3, 0.4, 0.5)
        _DistortionStrength ("Distortion Strength", Range(0, 2)) = 0.5
        _FadeDistance ("Fade Distance", Float) = 20.0
        _WaveSpeed ("Wave Speed", Float) = 2.0
        _WaveFrequency ("Wave Frequency", Float) = 10.0
        _GridScale ("Grid Scale", Float) = 1.0
        _GridLineWidth ("Grid Line Width", Range(0.001, 0.1)) = 0.02
        _EmissionIntensity ("Emission Intensity", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            Name "GridDistortion"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float distortionAmount : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _GridColor;
                float _DistortionStrength;
                float _FadeDistance;
                float _WaveSpeed;
                float _WaveFrequency;
                float _GridScale;
                float _GridLineWidth;
                float _EmissionIntensity;
            CBUFFER_END

            // Up to 32 active wave packets
            float4 _PacketPositions[32];
            int _ActivePacketCount;

            // Create procedural grid pattern
            float CreateGrid(float2 uv)
            {
                float2 scaledUV = uv * _GridScale;
                float2 grid = abs(frac(scaledUV) - 0.5);

                // Create grid lines
                float gridLine = min(grid.x, grid.y);
                float gridPattern = 1.0 - smoothstep(0.0, _GridLineWidth, gridLine);

                return gridPattern;
            }

            // Calculate wave distortion at a world position
            float3 CalculateDistortion(float3 worldPos)
            {
                float3 totalDistortion = float3(0, 0, 0);
                float totalInfluence = 0;

                // Calculate cumulative distortion from all packets
                for (int i = 0; i < _ActivePacketCount; i++)
                {
                    float3 packetPos = _PacketPositions[i].xyz;
                    float amplitude = _PacketPositions[i].w;

                    // Calculate distance on XZ plane (horizontal distance)
                    float dist = distance(worldPos.xz, packetPos.xz);

                    // Wave equation: creates ripples emanating from packet
                    float wave = sin(dist * _WaveFrequency - _Time.y * _WaveSpeed);

                    // Exponential falloff for smooth distortion
                    float falloff = exp(-dist * 0.15);

                    // Vertical displacement (Y axis)
                    float verticalDisplacement = wave * falloff * amplitude * _DistortionStrength;

                    // Radial displacement (push outward from packet center)
                    float2 radialDir = normalize(worldPos.xz - packetPos.xz);
                    float radialDisplacement = wave * falloff * amplitude * _DistortionStrength * 0.3;

                    totalDistortion.y += verticalDisplacement;
                    totalDistortion.xz += radialDir * radialDisplacement;
                    totalInfluence += falloff;
                }

                return totalDistortion;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Get world position
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float3 worldPos = posInputs.positionWS;

                // Calculate and apply distortion
                float3 distortion = CalculateDistortion(worldPos);
                float3 distortedPosition = input.positionOS.xyz + distortion;

                // Recalculate position with distortion
                output.positionCS = TransformObjectToHClip(distortedPosition);
                output.worldPos = TransformObjectToWorld(distortedPosition);

                // UV coordinates with distortion
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.uv += distortion.xz * 0.02; // Slight UV distortion

                // Fog
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                // Store distortion amount for fragment shader
                output.distortionAmount = length(distortion);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample main texture or create procedural grid
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Create procedural grid if no texture
                float gridPattern = CreateGrid(input.uv);

                // Calculate distance-based fade
                float minDistance = _FadeDistance;
                for (int i = 0; i < _ActivePacketCount; i++)
                {
                    float dist = distance(input.worldPos, _PacketPositions[i].xyz);
                    minDistance = min(minDistance, dist);
                }

                float distanceFade = saturate(1.0 - (minDistance / _FadeDistance));

                // Combine texture and grid
                half4 finalColor = _GridColor;
                finalColor.rgb = lerp(finalColor.rgb, texColor.rgb, texColor.a);
                finalColor.rgb = lerp(finalColor.rgb, finalColor.rgb * 2.0, gridPattern);

                // Add emission based on distortion
                float emission = saturate(input.distortionAmount * _EmissionIntensity);
                finalColor.rgb += _GridColor.rgb * emission;

                // Apply distance fade
                finalColor.a *= distanceFade * _GridColor.a;

                // Apply fog
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}