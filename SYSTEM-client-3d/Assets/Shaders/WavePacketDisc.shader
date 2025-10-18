Shader "SYSTEM/WavePacketDisc"
{
    Properties
    {
        _EmissionStrength ("Emission Strength", Range(0, 2)) = 0.5
        _Alpha ("Alpha", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0

            // WebGL compatibility - ensure proper precision
            #pragma glsl

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float3 worldNormal : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            float _EmissionStrength;
            float _Alpha;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Use vertex colors
                fixed4 col = i.color;

                // Discard transparent pixels to avoid grey artifacts
                if (col.a < 0.1)
                {
                    discard;
                }

                // Better lighting with multiple light directions for 3D depth
                float3 worldNormal = normalize(i.worldNormal);

                // Main light from above-front
                float3 mainLight = normalize(float3(0.3, 0.8, 0.5));
                float mainDiffuse = max(0.0, dot(worldNormal, mainLight));

                // Rim light from below for edge definition
                float3 rimLight = normalize(float3(0, -1, 0));
                float rimDiffuse = max(0.0, dot(worldNormal, rimLight)) * 0.3;

                // Ambient light to prevent pure black
                float ambient = 0.4;

                // Combine lighting
                float lighting = ambient + mainDiffuse * 0.8 + rimDiffuse;
                col.rgb *= lighting;

                // Add emission glow
                col.rgb += i.color.rgb * _EmissionStrength;

                // Render as opaque (no alpha blending)
                col.a = 1.0;

                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);

                return col;
            }
            ENDCG
        }
    }

    // Fallback for WebGL and older hardware
    Fallback "Unlit/Color"
}
