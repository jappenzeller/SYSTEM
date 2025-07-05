Shader "Custom/WorldGradient"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.3, 0.5, 1)
        _TopColor ("Top Color", Color) = (0.4, 0.5, 0.7, 1)
        _GradientPower ("Gradient Power", Range(0.1, 5.0)) = 1.0
        _MainTex ("Texture (Optional)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
     
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex;
        
        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float height;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _BaseColor;
        fixed4 _TopColor;
        float _GradientPower;
        
        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
            o.worldPos = worldPos.xyz;
            
            // Calculate height based on world position
            float3 centerToVertex = normalize(worldPos.xyz);
            o.height = dot(centerToVertex, float3(0, 1, 0)) * 0.5 + 0.5;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Sample texture if provided
            fixed4 texColor = tex2D(_MainTex, IN.uv_MainTex);
            
            // Calculate gradient based on height
            float gradient = pow(IN.height, _GradientPower);
            fixed4 gradientColor = lerp(_BaseColor, _TopColor, gradient);
            
            // Combine texture and gradient
            o.Albedo = texColor.rgb * gradientColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}