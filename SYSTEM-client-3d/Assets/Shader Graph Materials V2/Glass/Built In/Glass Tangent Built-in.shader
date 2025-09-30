Shader "Glass Tangent"
{
    Properties
    {
        [NoScaleOffset]Texture2D_4D11925F("Albedo", 2D) = "white" {}
        Color_A94873D1("Color Tint", Color) = (1, 1, 1, 0)
        [Normal][NoScaleOffset]_BumpMap2("Normal", 2D) = "bump" {}
        Vector1_55516576("Refraction Normal Strengh", Range(0, 1)) = 0
        Vector1_C0B9FE29("Tiling", Float) = 1
        Vector1_FCA6D949("IOR", Range(-1, 1)) = 0
        Vector1_70323E7F("Reflection Normal Strengh", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0
        _Metalic("Metalic", Range(0, 1)) = 0
        [HideInInspector]_BUILTIN_QueueOffset("Float", Float) = 0
        [HideInInspector]_BUILTIN_QueueControl("Float", Float) = -1
    }
    SubShader
    {
        Tags
        {
            // RenderPipeline: <None>
            "RenderType"="Transparent"
            "BuiltInMaterialType" = "Lit"
            "Queue"="Transparent"
            "ShaderGraphShader"="true"
            "ShaderGraphTargetId"="BuiltInLitSubTarget"
        }
        GrabPass { "_GrabTexture" }
        Pass
        {
            Name "BuiltIn Forward"
            Tags
            {
                "LightMode" = "ForwardBase"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        sampler2D _GrabTexture;
        // Pragmas
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma multi_compile_fwdbase
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
        #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_VIEWDIRECTION_WS
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_FORWARD
        #define BUILTIN_TARGET_API 1
        #define REQUIRE_OPAQUE_TEXTURE
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
             float4 shadowCoord;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 TangentSpaceNormal;
             float3 WorldSpaceTangent;
             float3 WorldSpaceBiTangent;
             float3 WorldSpaceViewDirection;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 lightmapUV : INTERP0;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP1;
            #endif
             float4 tangentWS : INTERP2;
             float4 texCoord0 : INTERP3;
             float4 fogFactorAndVertexLight : INTERP4;
             float4 shadowCoord : INTERP5;
             float3 positionWS : INTERP6;
             float3 normalWS : INTERP7;
             float3 viewDirectionWS : INTERP8;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.lightmapUV = input.lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
            output.shadowCoord.xyzw = input.shadowCoord;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            output.viewDirectionWS.xyz = input.viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.lightmapUV = input.lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
            output.shadowCoord = input.shadowCoord.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            output.viewDirectionWS = input.viewDirectionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        
        void ref_float(float3 View, float3 Normal, float IOR, out float3 Out){
            Out = refract(normalize(View), normalize(Normal),IOR);
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        void Unity_NormalStrength_float(float3 In, float Strength, out float3 Out)
        {
            Out = float3(In.rg * Strength, lerp(1, In.b, saturate(Strength)));
        }
        
        void Unity_NormalBlend_float(float3 A, float3 B, out float3 Out)
        {
            Out = SafeNormalize(float3(A.rg + B.rg, A.b * B.b));
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_Fraction_float3(float3 In, out float3 Out)
        {
            Out = frac(In);
        }
        
        void Unity_SceneColor_float(float4 UV, out float3 Out)
        {
            Out = tex2D(_GrabTexture, UV.xy);
        }
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0 = float4(IN.ScreenPosition.xy / IN.ScreenPosition.w, 0, 0);
            float _Property_5e9feef53403ff82a4f805564a16526d_Out_0 = Vector1_FCA6D949;
            float3 _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3;
            ref_float(IN.WorldSpaceViewDirection, IN.WorldSpaceNormal, _Property_5e9feef53403ff82a4f805564a16526d_Out_0, _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3);
            UnityTexture2D _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0 = UnityBuildTexture2DStructNoScale(_BumpMap2);
            float _Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0 = Vector1_C0B9FE29;
            float2 _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3;
            Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0.xx), float2 (0, 0), _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3);
            float4 _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0 = SAMPLE_TEXTURE2D(_Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.tex, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.samplerstate, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.rgb = UnpackNormal(_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0);
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_R_4 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.r;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_G_5 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.g;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_B_6 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.b;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_A_7 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.a;
            float _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0 = Vector1_55516576;
            float3 _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2;
            Unity_NormalStrength_float((_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.xyz), _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2);
            float3 _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2;
            Unity_NormalBlend_float(_refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2, _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2);
            float3x3 Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World = float3x3(IN.WorldSpaceTangent, IN.WorldSpaceBiTangent, IN.WorldSpaceNormal);
            float3 _Transform_725e07a855717388a7017a8649f5b178_Out_1 = TransformWorldToTangent(_NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2.xyz, Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World);
            float3 _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2;
            Unity_Add_float3((_ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0.xyz), _Transform_725e07a855717388a7017a8649f5b178_Out_1, _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2);
            float3 _Fraction_ed92e5f93e43422da9a672e710830197_Out_1;
            Unity_Fraction_float3(_Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2, _Fraction_ed92e5f93e43422da9a672e710830197_Out_1);
            float3 _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1;
            Unity_SceneColor_float((float4(_Fraction_ed92e5f93e43422da9a672e710830197_Out_1, 1.0)), _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1);
            UnityTexture2D _Property_32706928df59ee8882d7d526643bcfe1_Out_0 = UnityBuildTexture2DStructNoScale(Texture2D_4D11925F);
            float4 _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0 = SAMPLE_TEXTURE2D(_Property_32706928df59ee8882d7d526643bcfe1_Out_0.tex, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.samplerstate, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_R_4 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.r;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_G_5 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.g;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_B_6 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.b;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_A_7 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.a;
            float4 _Property_c736fb481737548b88b8cdcf41d451e1_Out_0 = Color_A94873D1;
            float4 _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2;
            Unity_Multiply_float4_float4(_SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0, _Property_c736fb481737548b88b8cdcf41d451e1_Out_0, _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2);
            float3 _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            Unity_Multiply_float3_float3(_SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1, (_Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2.xyz), _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2);
            float _Property_bb74afcff94a268993901b0e1f8e0c9b_Out_0 = Vector1_70323E7F;
            float3 _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2;
            Unity_NormalStrength_float((_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.xyz), _Property_bb74afcff94a268993901b0e1f8e0c9b_Out_0, _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2);
            float _Property_1afba98f86e842ebae5b25828f0e8b95_Out_0 = _Metalic;
            float _Property_553bc721ae62477c9db9ca21cd5e82ae_Out_0 = _Smoothness;
            surface.BaseColor = _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            surface.NormalTS = _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = _Property_1afba98f86e842ebae5b25828f0e8b95_Out_0;
            surface.Smoothness = _Property_553bc721ae62477c9db9ca21cd5e82ae_Out_0;
            surface.Occlusion = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
            // use bitangent on the fly like in hdrp
            // IMPORTANT! If we ever support Flip on double sided materials ensure bitangent and tangent are NOT flipped.
            float crossSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0)* GetOddNegativeScale();
            float3 bitang = crossSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
            // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
            // This is explained in section 2.2 in "surface gradient based bump mapping framework"
            output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;
            output.WorldSpaceBiTangent = renormFactor * bitang;
        
            output.WorldSpaceViewDirection = normalize(input.viewDirectionWS);
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.texcoord   = attributes.uv0;
            result.texcoord1  = attributes.uv1;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            result.worldPos = varyings.positionWS;
            result.worldNormal = varyings.normalWS;
            result.viewDir = varyings.viewDirectionWS;
            // World Tangent isn't an available input on v2f_surf
        
            result._ShadowCoord = varyings.shadowCoord;
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            result.sh = varyings.sh;
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            result.lmap.xy = varyings.lightmapUV;
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            result.positionWS = surfVertex.worldPos;
            result.normalWS = surfVertex.worldNormal;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
            result.shadowCoord = surfVertex._ShadowCoord;
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            result.sh = surfVertex.sh;
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            result.lightmapUV = surfVertex.lmap.xy;
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardPass.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "BuiltIn ForwardAdd"
            Tags
            {
                "LightMode" = "ForwardAdd"
            }
        
        // Render State
        Blend SrcAlpha One, One One
        ZWrite Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        sampler2D _GrabTexture;
        
        // Pragmas
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma multi_compile_fog
        #pragma multi_compile_fwdadd_fullshadows
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _ADDITIONAL_OFF
        #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ SHADOWS_SHADOWMASK
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_VIEWDIRECTION_WS
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_FORWARD_ADD
        #define BUILTIN_TARGET_API 1
        #define REQUIRE_OPAQUE_TEXTURE
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
             float4 shadowCoord;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 TangentSpaceNormal;
             float3 WorldSpaceTangent;
             float3 WorldSpaceBiTangent;
             float3 WorldSpaceViewDirection;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 lightmapUV : INTERP0;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP1;
            #endif
             float4 tangentWS : INTERP2;
             float4 texCoord0 : INTERP3;
             float4 fogFactorAndVertexLight : INTERP4;
             float4 shadowCoord : INTERP5;
             float3 positionWS : INTERP6;
             float3 normalWS : INTERP7;
             float3 viewDirectionWS : INTERP8;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.lightmapUV = input.lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
            output.shadowCoord.xyzw = input.shadowCoord;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            output.viewDirectionWS.xyz = input.viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.lightmapUV = input.lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
            output.shadowCoord = input.shadowCoord.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            output.viewDirectionWS = input.viewDirectionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        
        void ref_float(float3 View, float3 Normal, float IOR, out float3 Out){
            Out = refract(normalize(View), normalize(Normal),IOR);
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        void Unity_NormalStrength_float(float3 In, float Strength, out float3 Out)
        {
            Out = float3(In.rg * Strength, lerp(1, In.b, saturate(Strength)));
        }
        
        void Unity_NormalBlend_float(float3 A, float3 B, out float3 Out)
        {
            Out = SafeNormalize(float3(A.rg + B.rg, A.b * B.b));
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_Fraction_float3(float3 In, out float3 Out)
        {
            Out = frac(In);
        }
        
        void Unity_SceneColor_float(float4 UV, out float3 Out)
        {
            Out = tex2D(_GrabTexture, UV.xy);
        }
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0 = float4(IN.ScreenPosition.xy / IN.ScreenPosition.w, 0, 0);
            float _Property_5e9feef53403ff82a4f805564a16526d_Out_0 = Vector1_FCA6D949;
            float3 _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3;
            ref_float(IN.WorldSpaceViewDirection, IN.WorldSpaceNormal, _Property_5e9feef53403ff82a4f805564a16526d_Out_0, _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3);
            UnityTexture2D _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0 = UnityBuildTexture2DStructNoScale(_BumpMap2);
            float _Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0 = Vector1_C0B9FE29;
            float2 _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3;
            Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0.xx), float2 (0, 0), _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3);
            float4 _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0 = SAMPLE_TEXTURE2D(_Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.tex, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.samplerstate, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.rgb = UnpackNormal(_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0);
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_R_4 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.r;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_G_5 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.g;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_B_6 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.b;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_A_7 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.a;
            float _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0 = Vector1_55516576;
            float3 _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2;
            Unity_NormalStrength_float((_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.xyz), _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2);
            float3 _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2;
            Unity_NormalBlend_float(_refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2, _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2);
            float3x3 Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World = float3x3(IN.WorldSpaceTangent, IN.WorldSpaceBiTangent, IN.WorldSpaceNormal);
            float3 _Transform_725e07a855717388a7017a8649f5b178_Out_1 = TransformWorldToTangent(_NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2.xyz, Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World);
            float3 _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2;
            Unity_Add_float3((_ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0.xyz), _Transform_725e07a855717388a7017a8649f5b178_Out_1, _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2);
            float3 _Fraction_ed92e5f93e43422da9a672e710830197_Out_1;
            Unity_Fraction_float3(_Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2, _Fraction_ed92e5f93e43422da9a672e710830197_Out_1);
            float3 _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1;
            Unity_SceneColor_float((float4(_Fraction_ed92e5f93e43422da9a672e710830197_Out_1, 1.0)), _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1);
            UnityTexture2D _Property_32706928df59ee8882d7d526643bcfe1_Out_0 = UnityBuildTexture2DStructNoScale(Texture2D_4D11925F);
            float4 _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0 = SAMPLE_TEXTURE2D(_Property_32706928df59ee8882d7d526643bcfe1_Out_0.tex, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.samplerstate, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_R_4 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.r;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_G_5 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.g;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_B_6 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.b;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_A_7 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.a;
            float4 _Property_c736fb481737548b88b8cdcf41d451e1_Out_0 = Color_A94873D1;
            float4 _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2;
            Unity_Multiply_float4_float4(_SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0, _Property_c736fb481737548b88b8cdcf41d451e1_Out_0, _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2);
            float3 _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            Unity_Multiply_float3_float3(_SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1, (_Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2.xyz), _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2);
            float _Property_bb74afcff94a268993901b0e1f8e0c9b_Out_0 = Vector1_70323E7F;
            float3 _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2;
            Unity_NormalStrength_float((_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.xyz), _Property_bb74afcff94a268993901b0e1f8e0c9b_Out_0, _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2);
            float _Property_1afba98f86e842ebae5b25828f0e8b95_Out_0 = _Metalic;
            float _Property_553bc721ae62477c9db9ca21cd5e82ae_Out_0 = _Smoothness;
            surface.BaseColor = _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            surface.NormalTS = _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = _Property_1afba98f86e842ebae5b25828f0e8b95_Out_0;
            surface.Smoothness = _Property_553bc721ae62477c9db9ca21cd5e82ae_Out_0;
            surface.Occlusion = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
            // use bitangent on the fly like in hdrp
            // IMPORTANT! If we ever support Flip on double sided materials ensure bitangent and tangent are NOT flipped.
            float crossSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0)* GetOddNegativeScale();
            float3 bitang = crossSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
            // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
            // This is explained in section 2.2 in "surface gradient based bump mapping framework"
            output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;
            output.WorldSpaceBiTangent = renormFactor * bitang;
        
            output.WorldSpaceViewDirection = normalize(input.viewDirectionWS);
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.texcoord   = attributes.uv0;
            result.texcoord1  = attributes.uv1;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            result.worldPos = varyings.positionWS;
            result.worldNormal = varyings.normalWS;
            result.viewDir = varyings.viewDirectionWS;
            // World Tangent isn't an available input on v2f_surf
        
            result._ShadowCoord = varyings.shadowCoord;
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            result.sh = varyings.sh;
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            result.lmap.xy = varyings.lightmapUV;
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            result.positionWS = surfVertex.worldPos;
            result.normalWS = surfVertex.worldNormal;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
            result.shadowCoord = surfVertex._ShadowCoord;
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            result.sh = surfVertex.sh;
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            result.lightmapUV = surfVertex.lmap.xy;
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardAddPass.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "BuiltIn Deferred"
            Tags
            {
                "LightMode" = "Deferred"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        sampler2D _GrabTexture;
        
        // Pragmas
        #pragma target 4.5
        #pragma multi_compile_instancing
        #pragma exclude_renderers nomrt
        #pragma multi_compile_prepassfinal
        #pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile _ LIGHTMAP_ON
        #pragma multi_compile _ DIRLIGHTMAP_COMBINED
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _SHADOWS_SOFT
        #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
        #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
        #pragma multi_compile _ _GBUFFER_NORMALS_OCT
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_VIEWDIRECTION_WS
        #define VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEFERRED
        #define BUILTIN_TARGET_API 1
        #define REQUIRE_OPAQUE_TEXTURE
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if defined(LIGHTMAP_ON)
             float2 lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh;
            #endif
             float4 fogFactorAndVertexLight;
             float4 shadowCoord;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 TangentSpaceNormal;
             float3 WorldSpaceTangent;
             float3 WorldSpaceBiTangent;
             float3 WorldSpaceViewDirection;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if defined(LIGHTMAP_ON)
             float2 lightmapUV : INTERP0;
            #endif
            #if !defined(LIGHTMAP_ON)
             float3 sh : INTERP1;
            #endif
             float4 tangentWS : INTERP2;
             float4 texCoord0 : INTERP3;
             float4 fogFactorAndVertexLight : INTERP4;
             float4 shadowCoord : INTERP5;
             float3 positionWS : INTERP6;
             float3 normalWS : INTERP7;
             float3 viewDirectionWS : INTERP8;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.lightmapUV = input.lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.fogFactorAndVertexLight.xyzw = input.fogFactorAndVertexLight;
            output.shadowCoord.xyzw = input.shadowCoord;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            output.viewDirectionWS.xyz = input.viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if defined(LIGHTMAP_ON)
            output.lightmapUV = input.lightmapUV;
            #endif
            #if !defined(LIGHTMAP_ON)
            output.sh = input.sh;
            #endif
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.fogFactorAndVertexLight = input.fogFactorAndVertexLight.xyzw;
            output.shadowCoord = input.shadowCoord.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            output.viewDirectionWS = input.viewDirectionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        
        void ref_float(float3 View, float3 Normal, float IOR, out float3 Out){
            Out = refract(normalize(View), normalize(Normal),IOR);
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        void Unity_NormalStrength_float(float3 In, float Strength, out float3 Out)
        {
            Out = float3(In.rg * Strength, lerp(1, In.b, saturate(Strength)));
        }
        
        void Unity_NormalBlend_float(float3 A, float3 B, out float3 Out)
        {
            Out = SafeNormalize(float3(A.rg + B.rg, A.b * B.b));
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_Fraction_float3(float3 In, out float3 Out)
        {
            Out = frac(In);
        }
        
        void Unity_SceneColor_float(float4 UV, out float3 Out)
        {
            Out = tex2D(_GrabTexture, UV.xy);
        }
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 NormalTS;
            float3 Emission;
            float Metallic;
            float Smoothness;
            float Occlusion;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0 = float4(IN.ScreenPosition.xy / IN.ScreenPosition.w, 0, 0);
            float _Property_5e9feef53403ff82a4f805564a16526d_Out_0 = Vector1_FCA6D949;
            float3 _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3;
            ref_float(IN.WorldSpaceViewDirection, IN.WorldSpaceNormal, _Property_5e9feef53403ff82a4f805564a16526d_Out_0, _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3);
            UnityTexture2D _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0 = UnityBuildTexture2DStructNoScale(_BumpMap2);
            float _Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0 = Vector1_C0B9FE29;
            float2 _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3;
            Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0.xx), float2 (0, 0), _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3);
            float4 _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0 = SAMPLE_TEXTURE2D(_Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.tex, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.samplerstate, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.rgb = UnpackNormal(_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0);
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_R_4 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.r;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_G_5 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.g;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_B_6 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.b;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_A_7 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.a;
            float _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0 = Vector1_55516576;
            float3 _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2;
            Unity_NormalStrength_float((_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.xyz), _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2);
            float3 _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2;
            Unity_NormalBlend_float(_refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2, _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2);
            float3x3 Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World = float3x3(IN.WorldSpaceTangent, IN.WorldSpaceBiTangent, IN.WorldSpaceNormal);
            float3 _Transform_725e07a855717388a7017a8649f5b178_Out_1 = TransformWorldToTangent(_NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2.xyz, Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World);
            float3 _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2;
            Unity_Add_float3((_ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0.xyz), _Transform_725e07a855717388a7017a8649f5b178_Out_1, _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2);
            float3 _Fraction_ed92e5f93e43422da9a672e710830197_Out_1;
            Unity_Fraction_float3(_Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2, _Fraction_ed92e5f93e43422da9a672e710830197_Out_1);
            float3 _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1;
            Unity_SceneColor_float((float4(_Fraction_ed92e5f93e43422da9a672e710830197_Out_1, 1.0)), _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1);
            UnityTexture2D _Property_32706928df59ee8882d7d526643bcfe1_Out_0 = UnityBuildTexture2DStructNoScale(Texture2D_4D11925F);
            float4 _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0 = SAMPLE_TEXTURE2D(_Property_32706928df59ee8882d7d526643bcfe1_Out_0.tex, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.samplerstate, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_R_4 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.r;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_G_5 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.g;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_B_6 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.b;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_A_7 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.a;
            float4 _Property_c736fb481737548b88b8cdcf41d451e1_Out_0 = Color_A94873D1;
            float4 _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2;
            Unity_Multiply_float4_float4(_SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0, _Property_c736fb481737548b88b8cdcf41d451e1_Out_0, _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2);
            float3 _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            Unity_Multiply_float3_float3(_SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1, (_Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2.xyz), _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2);
            float _Property_bb74afcff94a268993901b0e1f8e0c9b_Out_0 = Vector1_70323E7F;
            float3 _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2;
            Unity_NormalStrength_float((_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.xyz), _Property_bb74afcff94a268993901b0e1f8e0c9b_Out_0, _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2);
            float _Property_1afba98f86e842ebae5b25828f0e8b95_Out_0 = _Metalic;
            float _Property_553bc721ae62477c9db9ca21cd5e82ae_Out_0 = _Smoothness;
            surface.BaseColor = _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            surface.NormalTS = _NormalStrength_9aa90ce43fdbb482b701f658017b4460_Out_2;
            surface.Emission = float3(0, 0, 0);
            surface.Metallic = _Property_1afba98f86e842ebae5b25828f0e8b95_Out_0;
            surface.Smoothness = _Property_553bc721ae62477c9db9ca21cd5e82ae_Out_0;
            surface.Occlusion = 1;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
            // use bitangent on the fly like in hdrp
            // IMPORTANT! If we ever support Flip on double sided materials ensure bitangent and tangent are NOT flipped.
            float crossSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0)* GetOddNegativeScale();
            float3 bitang = crossSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
            output.TangentSpaceNormal = float3(0.0f, 0.0f, 1.0f);
        
            // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
            // This is explained in section 2.2 in "surface gradient based bump mapping framework"
            output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;
            output.WorldSpaceBiTangent = renormFactor * bitang;
        
            output.WorldSpaceViewDirection = normalize(input.viewDirectionWS);
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.texcoord   = attributes.uv0;
            result.texcoord1  = attributes.uv1;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            result.worldPos = varyings.positionWS;
            result.worldNormal = varyings.normalWS;
            result.viewDir = varyings.viewDirectionWS;
            // World Tangent isn't an available input on v2f_surf
        
            result._ShadowCoord = varyings.shadowCoord;
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            result.sh = varyings.sh;
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            result.lmap.xy = varyings.lightmapUV;
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            result.positionWS = surfVertex.worldPos;
            result.normalWS = surfVertex.worldNormal;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
            result.shadowCoord = surfVertex._ShadowCoord;
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            result.sh = surfVertex.sh;
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            result.lightmapUV = surfVertex.lmap.xy;
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRDeferredPass.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        sampler2D _GrabTexture;
        
        // Pragmas
        #pragma target 3.0
        #pragma multi_compile_shadowcaster
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_SHADOWCASTER
        #define BUILTIN_TARGET_API 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            // World Tangent isn't an available input on v2f_surf
        
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShadowCasterPass.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }
        
        // Render State
        Cull Back
        Blend One Zero
        ZTest LEqual
        ZWrite On
        ColorMask 0
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        
        // Pragmas
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_DEPTHONLY
        #define BUILTIN_TARGET_API 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            // World Tangent isn't an available input on v2f_surf
        
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags
            {
                "LightMode" = "Meta"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        sampler2D _GrabTexture;
        
        // Pragmas
        #pragma target 3.0
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        #pragma shader_feature _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define ATTRIBUTES_NEED_TEXCOORD0
        #define ATTRIBUTES_NEED_TEXCOORD1
        #define ATTRIBUTES_NEED_TEXCOORD2
        #define VARYINGS_NEED_POSITION_WS
        #define VARYINGS_NEED_NORMAL_WS
        #define VARYINGS_NEED_TANGENT_WS
        #define VARYINGS_NEED_TEXCOORD0
        #define VARYINGS_NEED_VIEWDIRECTION_WS
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SHADERPASS_META
        #define BUILTIN_TARGET_API 1
        #define REQUIRE_OPAQUE_TEXTURE
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
             float4 uv0 : TEXCOORD0;
             float4 uv1 : TEXCOORD1;
             float4 uv2 : TEXCOORD2;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
             float3 positionWS;
             float3 normalWS;
             float4 tangentWS;
             float4 texCoord0;
             float3 viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
             float3 WorldSpaceNormal;
             float3 WorldSpaceTangent;
             float3 WorldSpaceBiTangent;
             float3 WorldSpaceViewDirection;
             float3 WorldSpacePosition;
             float4 ScreenPosition;
             float4 uv0;
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
             float4 tangentWS : INTERP0;
             float4 texCoord0 : INTERP1;
             float3 positionWS : INTERP2;
             float3 normalWS : INTERP3;
             float3 viewDirectionWS : INTERP4;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            output.tangentWS.xyzw = input.tangentWS;
            output.texCoord0.xyzw = input.texCoord0;
            output.positionWS.xyz = input.positionWS;
            output.normalWS.xyz = input.normalWS;
            output.viewDirectionWS.xyz = input.viewDirectionWS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            output.tangentWS = input.tangentWS.xyzw;
            output.texCoord0 = input.texCoord0.xyzw;
            output.positionWS = input.positionWS.xyz;
            output.normalWS = input.normalWS.xyz;
            output.viewDirectionWS = input.viewDirectionWS.xyz;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        
        void ref_float(float3 View, float3 Normal, float IOR, out float3 Out){
            Out = refract(normalize(View), normalize(Normal),IOR);
        }
        
        void Unity_TilingAndOffset_float(float2 UV, float2 Tiling, float2 Offset, out float2 Out)
        {
            Out = UV * Tiling + Offset;
        }
        
        void Unity_NormalStrength_float(float3 In, float Strength, out float3 Out)
        {
            Out = float3(In.rg * Strength, lerp(1, In.b, saturate(Strength)));
        }
        
        void Unity_NormalBlend_float(float3 A, float3 B, out float3 Out)
        {
            Out = SafeNormalize(float3(A.rg + B.rg, A.b * B.b));
        }
        
        void Unity_Add_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A + B;
        }
        
        void Unity_Fraction_float3(float3 In, out float3 Out)
        {
            Out = frac(In);
        }
        
        void Unity_SceneColor_float(float4 UV, out float3 Out)
        {
            Out = tex2D(_GrabTexture, UV.xy);
        }
        
        void Unity_Multiply_float4_float4(float4 A, float4 B, out float4 Out)
        {
            Out = A * B;
        }
        
        void Unity_Multiply_float3_float3(float3 A, float3 B, out float3 Out)
        {
            Out = A * B;
        }
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
            float3 BaseColor;
            float3 Emission;
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            float4 _ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0 = float4(IN.ScreenPosition.xy / IN.ScreenPosition.w, 0, 0);
            float _Property_5e9feef53403ff82a4f805564a16526d_Out_0 = Vector1_FCA6D949;
            float3 _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3;
            ref_float(IN.WorldSpaceViewDirection, IN.WorldSpaceNormal, _Property_5e9feef53403ff82a4f805564a16526d_Out_0, _refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3);
            UnityTexture2D _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0 = UnityBuildTexture2DStructNoScale(_BumpMap2);
            float _Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0 = Vector1_C0B9FE29;
            float2 _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3;
            Unity_TilingAndOffset_float(IN.uv0.xy, (_Property_861fe0fa7035b18d8880f1b3c32c999d_Out_0.xx), float2 (0, 0), _TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3);
            float4 _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0 = SAMPLE_TEXTURE2D(_Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.tex, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.samplerstate, _Property_5ccc3f70b10f098897026e9057aa6ddf_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.rgb = UnpackNormal(_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0);
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_R_4 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.r;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_G_5 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.g;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_B_6 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.b;
            float _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_A_7 = _SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.a;
            float _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0 = Vector1_55516576;
            float3 _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2;
            Unity_NormalStrength_float((_SampleTexture2D_e87f69f6f100758392a7ba6d6f5f1f05_RGBA_0.xyz), _Property_f2ae9fffb1daf48cb1eb2cca1c8bc004_Out_0, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2);
            float3 _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2;
            Unity_NormalBlend_float(_refCustomFunction_75203ece03f12e89a478124a145a2aeb_Out_3, _NormalStrength_467d4d23003c47888caf3559751c7dc9_Out_2, _NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2);
            float3x3 Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World = float3x3(IN.WorldSpaceTangent, IN.WorldSpaceBiTangent, IN.WorldSpaceNormal);
            float3 _Transform_725e07a855717388a7017a8649f5b178_Out_1 = TransformWorldToTangent(_NormalBlend_400afeef1029938493ee5dad2baa3cec_Out_2.xyz, Transform_725e07a855717388a7017a8649f5b178_tangentTransform_World);
            float3 _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2;
            Unity_Add_float3((_ScreenPosition_b381b40acd7be18fbccd592d9fa51676_Out_0.xyz), _Transform_725e07a855717388a7017a8649f5b178_Out_1, _Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2);
            float3 _Fraction_ed92e5f93e43422da9a672e710830197_Out_1;
            Unity_Fraction_float3(_Add_9fb13b3d39989f8fa9ba95e66ef20148_Out_2, _Fraction_ed92e5f93e43422da9a672e710830197_Out_1);
            float3 _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1;
            Unity_SceneColor_float((float4(_Fraction_ed92e5f93e43422da9a672e710830197_Out_1, 1.0)), _SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1);
            UnityTexture2D _Property_32706928df59ee8882d7d526643bcfe1_Out_0 = UnityBuildTexture2DStructNoScale(Texture2D_4D11925F);
            float4 _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0 = SAMPLE_TEXTURE2D(_Property_32706928df59ee8882d7d526643bcfe1_Out_0.tex, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.samplerstate, _Property_32706928df59ee8882d7d526643bcfe1_Out_0.GetTransformedUV(_TilingAndOffset_cdbb1436da3fd983a78132ebd3ea7258_Out_3));
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_R_4 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.r;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_G_5 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.g;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_B_6 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.b;
            float _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_A_7 = _SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0.a;
            float4 _Property_c736fb481737548b88b8cdcf41d451e1_Out_0 = Color_A94873D1;
            float4 _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2;
            Unity_Multiply_float4_float4(_SampleTexture2D_d28e332b95918f80b07e03b2f6eeee0f_RGBA_0, _Property_c736fb481737548b88b8cdcf41d451e1_Out_0, _Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2);
            float3 _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            Unity_Multiply_float3_float3(_SceneColor_0063e866a17ee98bb221c40898b87d80_Out_1, (_Multiply_7305b8c6cb94b686962bd2883e1d1e86_Out_2.xyz), _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2);
            surface.BaseColor = _Multiply_32b4fbaf7f4b5d8397960b901b754fa3_Out_2;
            surface.Emission = float3(0, 0, 0);
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
            // must use interpolated tangent, bitangent and normal before they are normalized in the pixel shader.
            float3 unnormalizedNormalWS = input.normalWS;
            const float renormFactor = 1.0 / length(unnormalizedNormalWS);
        
            // use bitangent on the fly like in hdrp
            // IMPORTANT! If we ever support Flip on double sided materials ensure bitangent and tangent are NOT flipped.
            float crossSign = (input.tangentWS.w > 0.0 ? 1.0 : -1.0)* GetOddNegativeScale();
            float3 bitang = crossSign * cross(input.normalWS.xyz, input.tangentWS.xyz);
        
            output.WorldSpaceNormal = renormFactor * input.normalWS.xyz;      // we want a unit length Normal Vector node in shader graph
        
            // to preserve mikktspace compliance we use same scale renormFactor as was used on the normal.
            // This is explained in section 2.2 in "surface gradient based bump mapping framework"
            output.WorldSpaceTangent = renormFactor * input.tangentWS.xyz;
            output.WorldSpaceBiTangent = renormFactor * bitang;
        
            output.WorldSpaceViewDirection = normalize(input.viewDirectionWS);
            output.WorldSpacePosition = input.positionWS;
            output.ScreenPosition = ComputeScreenPos(TransformWorldToHClip(input.positionWS), _ProjectionParams.x);
            output.uv0 = input.texCoord0;
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.texcoord   = attributes.uv0;
            result.texcoord1  = attributes.uv1;
            result.texcoord2  = attributes.uv2;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            result.worldPos = varyings.positionWS;
            result.worldNormal = varyings.normalWS;
            result.viewDir = varyings.viewDirectionWS;
            // World Tangent isn't an available input on v2f_surf
        
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            result.positionWS = surfVertex.worldPos;
            result.normalWS = surfVertex.worldNormal;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LightingMetaPass.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "SceneSelectionPass"
            Tags
            {
                "LightMode" = "SceneSelectionPass"
            }
        
        // Render State
        Cull Off
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        sampler2D _GrabTexture;
        
        // Pragmas
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS SceneSelectionPass
        #define BUILTIN_TARGET_API 1
        #define SCENESELECTIONPASS 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            // World Tangent isn't an available input on v2f_surf
        
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"
        
        ENDHLSL
        }
        Pass
        {
            Name "ScenePickingPass"
            Tags
            {
                "LightMode" = "Picking"
            }
        
        // Render State
        Cull Back
        
        // Debug
        // <None>
        
        // --------------------------------------------------
        // Pass
        
        HLSLPROGRAM
        sampler2D _GrabTexture;
        
        // Pragmas
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma vertex vert
        #pragma fragment frag
        
        // DotsInstancingOptions: <None>
        // HybridV1InjectedBuiltinProperties: <None>
        
        // Keywords
        // PassKeywords: <None>
        // GraphKeywords: <None>
        
        // Defines
        #define _NORMALMAP 1
        #define _NORMAL_DROPOFF_TS 1
        #define ATTRIBUTES_NEED_NORMAL
        #define ATTRIBUTES_NEED_TANGENT
        #define FEATURES_GRAPH_VERTEX
        /* WARNING: $splice Could not find named fragment 'PassInstancing' */
        #define SHADERPASS ScenePickingPass
        #define BUILTIN_TARGET_API 1
        #define SCENEPICKINGPASS 1
        /* WARNING: $splice Could not find named fragment 'DotsInstancingVars' */
        #ifdef _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #define _SURFACE_TYPE_TRANSPARENT _BUILTIN_SURFACE_TYPE_TRANSPARENT
        #endif
        #ifdef _BUILTIN_ALPHATEST_ON
        #define _ALPHATEST_ON _BUILTIN_ALPHATEST_ON
        #endif
        #ifdef _BUILTIN_AlphaClip
        #define _AlphaClip _BUILTIN_AlphaClip
        #endif
        #ifdef _BUILTIN_ALPHAPREMULTIPLY_ON
        #define _ALPHAPREMULTIPLY_ON _BUILTIN_ALPHAPREMULTIPLY_ON
        #endif
        
        
        // custom interpolator pre-include
        /* WARNING: $splice Could not find named fragment 'sgci_CustomInterpolatorPreInclude' */
        
        // Includes
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"
        
        // --------------------------------------------------
        // Structs and Packing
        
        // custom interpolators pre packing
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPrePacking' */
        
        struct Attributes
        {
             float3 positionOS : POSITION;
             float3 normalOS : NORMAL;
             float4 tangentOS : TANGENT;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : INSTANCEID_SEMANTIC;
            #endif
        };
        struct Varyings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        struct SurfaceDescriptionInputs
        {
        };
        struct VertexDescriptionInputs
        {
             float3 ObjectSpaceNormal;
             float3 ObjectSpaceTangent;
             float3 ObjectSpacePosition;
        };
        struct PackedVaryings
        {
             float4 positionCS : SV_POSITION;
            #if UNITY_ANY_INSTANCING_ENABLED
             uint instanceID : CUSTOM_INSTANCE_ID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
             uint stereoTargetEyeIndexAsBlendIdx0 : BLENDINDICES0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
             uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
             FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC;
            #endif
        };
        
        PackedVaryings PackVaryings (Varyings input)
        {
            PackedVaryings output;
            ZERO_INITIALIZE(PackedVaryings, output);
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        Varyings UnpackVaryings (PackedVaryings input)
        {
            Varyings output;
            output.positionCS = input.positionCS;
            #if UNITY_ANY_INSTANCING_ENABLED
            output.instanceID = input.instanceID;
            #endif
            #if (defined(UNITY_STEREO_MULTIVIEW_ENABLED)) || (defined(UNITY_STEREO_INSTANCING_ENABLED) && (defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE)))
            output.stereoTargetEyeIndexAsBlendIdx0 = input.stereoTargetEyeIndexAsBlendIdx0;
            #endif
            #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
            output.stereoTargetEyeIndexAsRTArrayIdx = input.stereoTargetEyeIndexAsRTArrayIdx;
            #endif
            #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
            output.cullFace = input.cullFace;
            #endif
            return output;
        }
        
        
        // --------------------------------------------------
        // Graph
        
        // Graph Properties
        CBUFFER_START(UnityPerMaterial)
        float4 Texture2D_4D11925F_TexelSize;
        float4 Color_A94873D1;
        float4 _BumpMap2_TexelSize;
        float Vector1_55516576;
        float Vector1_C0B9FE29;
        float Vector1_FCA6D949;
        float Vector1_70323E7F;
        float _Metalic;
        float _Smoothness;
        CBUFFER_END
        
        // Object and Global properties
        SAMPLER(SamplerState_Linear_Repeat);
        TEXTURE2D(Texture2D_4D11925F);
        SAMPLER(samplerTexture2D_4D11925F);
        TEXTURE2D(_BumpMap2);
        SAMPLER(sampler_BumpMap2);
        
        // -- Property used by ScenePickingPass
        #ifdef SCENEPICKINGPASS
        float4 _SelectionID;
        #endif
        
        // -- Properties used by SceneSelectionPass
        #ifdef SCENESELECTIONPASS
        int _ObjectId;
        int _PassValue;
        #endif
        
        // Graph Includes
        // GraphIncludes: <None>
        
        // Graph Functions
        // GraphFunctions: <None>
        
        // Custom interpolators pre vertex
        /* WARNING: $splice Could not find named fragment 'CustomInterpolatorPreVertex' */
        
        // Graph Vertex
        struct VertexDescription
        {
            float3 Position;
            float3 Normal;
            float3 Tangent;
        };
        
        VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
        {
            VertexDescription description = (VertexDescription)0;
            description.Position = IN.ObjectSpacePosition;
            description.Normal = IN.ObjectSpaceNormal;
            description.Tangent = IN.ObjectSpaceTangent;
            return description;
        }
        
        // Custom interpolators, pre surface
        #ifdef FEATURES_GRAPH_VERTEX
        Varyings CustomInterpolatorPassThroughFunc(inout Varyings output, VertexDescription input)
        {
        return output;
        }
        #define CUSTOMINTERPOLATOR_VARYPASSTHROUGH_FUNC
        #endif
        
        // Graph Pixel
        struct SurfaceDescription
        {
        };
        
        SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
        {
            SurfaceDescription surface = (SurfaceDescription)0;
            return surface;
        }
        
        // --------------------------------------------------
        // Build Graph Inputs
        
        VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
        {
            VertexDescriptionInputs output;
            ZERO_INITIALIZE(VertexDescriptionInputs, output);
        
            output.ObjectSpaceNormal =                          input.normalOS;
            output.ObjectSpaceTangent =                         input.tangentOS.xyz;
            output.ObjectSpacePosition =                        input.positionOS;
        
            return output;
        }
        SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
        {
            SurfaceDescriptionInputs output;
            ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
        
            
        
        
        
        
        
        #if defined(SHADER_STAGE_FRAGMENT) && defined(VARYINGS_NEED_CULLFACE)
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN output.FaceSign =                    IS_FRONT_VFACE(input.cullFace, true, false);
        #else
        #define BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        #endif
        #undef BUILD_SURFACE_DESCRIPTION_INPUTS_OUTPUT_FACESIGN
        
                return output;
        }
        
        void BuildAppDataFull(Attributes attributes, VertexDescription vertexDescription, inout appdata_full result)
        {
            result.vertex     = float4(attributes.positionOS, 1);
            result.tangent    = attributes.tangentOS;
            result.normal     = attributes.normalOS;
            result.vertex     = float4(vertexDescription.Position, 1);
            result.normal     = vertexDescription.Normal;
            result.tangent    = float4(vertexDescription.Tangent, 0);
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
        }
        
        void VaryingsToSurfaceVertex(Varyings varyings, inout v2f_surf result)
        {
            result.pos = varyings.positionCS;
            // World Tangent isn't an available input on v2f_surf
        
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogCoord = varyings.fogFactorAndVertexLight.x;
                COPY_TO_LIGHT_COORDS(result, varyings.fogFactorAndVertexLight.yzw);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(varyings, result);
        }
        
        void SurfaceVertexToVaryings(v2f_surf surfVertex, inout Varyings result)
        {
            result.positionCS = surfVertex.pos;
            // viewDirectionWS is never filled out in the legacy pass' function. Always use the value computed by SRP
            // World Tangent isn't an available input on v2f_surf
        
            #if UNITY_ANY_INSTANCING_ENABLED
            #endif
            #if !defined(LIGHTMAP_ON)
            #if UNITY_SHOULD_SAMPLE_SH
            #endif
            #endif
            #if defined(LIGHTMAP_ON)
            #endif
            #ifdef VARYINGS_NEED_FOG_AND_VERTEX_LIGHT
                result.fogFactorAndVertexLight.x = surfVertex.fogCoord;
                COPY_FROM_LIGHT_COORDS(result.fogFactorAndVertexLight.yzw, surfVertex);
            #endif
        
            DEFAULT_UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(surfVertex, result);
        }
        
        // --------------------------------------------------
        // Main
        
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/Varyings.hlsl"
        #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/DepthOnlyPass.hlsl"
        
        ENDHLSL
        }
    }
    CustomEditorForRenderPipeline "UnityEditor.Rendering.BuiltIn.ShaderGraph.BuiltInLitGUI" ""
    CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
    FallBack "Hidden/Shader Graph/FallbackError"
}