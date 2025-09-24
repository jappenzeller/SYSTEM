Shader "SYSTEM/WorldSphereEnergy"
{
    Properties
    {
        _BaseEnergyColor ("Base Energy Color", Color) = (0.05, 0.0, 0.15, 1.0)
        _WaveColor1 ("Wave Color 1 (Cyan)", Color) = (0.0, 1.0, 1.0, 1.0)
        _WaveColor2 ("Wave Color 2 (Magenta)", Color) = (1.0, 0.0, 1.0, 1.0)
        _WaveSpeed1 ("Wave Speed 1", Float) = 2.0
        _WaveSpeed2 ("Wave Speed 2", Float) = 1.5
        _WaveFrequency ("Wave Frequency (Ripple Density)", Float) = 10.0
        _InterferenceScale ("Interference Scale", Float) = 1.0
        _EmissionStrength ("Emission Strength", Float) = 2.0
        _PulseSpeed ("Pulse Speed", Float) = 1.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            // Shader properties
            fixed4 _BaseEnergyColor;
            fixed4 _WaveColor1;
            fixed4 _WaveColor2;
            float _WaveSpeed1;
            float _WaveSpeed2;
            float _WaveFrequency;
            float _InterferenceScale;
            float _EmissionStrength;
            float _PulseSpeed;
            float _PulseAmount;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Normalize position on sphere surface
                float3 spherePos = normalize(i.worldPos);

                // Define wave sources at poles
                float3 waveSource1 = float3(0, 1, 0);  // North pole
                float3 waveSource2 = float3(0, -1, 0); // South pole

                // Calculate distances from wave sources
                float dist1 = distance(spherePos, waveSource1);
                float dist2 = distance(spherePos, waveSource2);

                // Create animated waves
                float time = _Time.y;
                float wave1 = sin((dist1 * _WaveFrequency - time * _WaveSpeed1) * 3.14159);
                float wave2 = sin((dist2 * _WaveFrequency - time * _WaveSpeed2) * 3.14159);

                // Calculate interference pattern
                float interference = (wave1 + wave2) * 0.5 * _InterferenceScale;
                interference = interference * 0.5 + 0.5; // Normalize to 0-1

                // Create secondary interference for more complex patterns
                float wave3 = sin((dist1 * _WaveFrequency * 0.7 - time * _WaveSpeed1 * 1.3) * 3.14159);
                float wave4 = sin((dist2 * _WaveFrequency * 0.7 - time * _WaveSpeed2 * 0.8) * 3.14159);
                float interference2 = (wave3 + wave4) * 0.25;
                interference2 = interference2 * 0.5 + 0.5;

                // Combine interference patterns
                float finalInterference = lerp(interference, interference2, 0.3);

                // Add radial waves from equator
                float equatorDist = abs(spherePos.y);
                float equatorWave = sin((equatorDist * _WaveFrequency * 1.5 - time * _WaveSpeed1 * 0.5) * 3.14159);
                equatorWave = equatorWave * 0.2 * (1.0 - equatorDist);
                finalInterference += equatorWave;

                // Clamp and enhance contrast
                finalInterference = saturate(finalInterference);
                finalInterference = pow(finalInterference, 1.5);

                // Calculate pulse effect
                float pulse = 1.0 + sin(time * _PulseSpeed * 3.14159) * _PulseAmount;

                // Mix colors based on interference pattern
                fixed4 waveColor = lerp(_WaveColor1, _WaveColor2, finalInterference);

                // Create energy bands
                float bands = sin(finalInterference * 20.0) * 0.5 + 0.5;
                bands = pow(bands, 3.0);

                // Combine base color with wave colors
                fixed4 energyColor = lerp(_BaseEnergyColor, waveColor, finalInterference * 0.8 + bands * 0.2);

                // Add emission glow effect
                float emission = finalInterference * _EmissionStrength * pulse;
                energyColor.rgb *= (1.0 + emission);

                // Add edge glow based on viewing angle
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = 1.0 - saturate(dot(i.worldNormal, viewDir));
                fresnel = pow(fresnel, 2.0);

                // Apply fresnel edge glow with wave colors
                fixed4 edgeGlow = lerp(_WaveColor1, _WaveColor2, sin(time * 2.0) * 0.5 + 0.5);
                energyColor.rgb += edgeGlow.rgb * fresnel * _EmissionStrength * 0.5;

                // Ensure bright neon appearance
                energyColor.rgb = saturate(energyColor.rgb * 1.5);

                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, energyColor);

                return energyColor;
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}