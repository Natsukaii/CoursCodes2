Shader "Examples/BaseShader"
{
    Properties
    {
        _BaseColor("Base color", Color) = (1,1,1,1)
        _BaseTex("Base Texture", 2D) = "white" {}
        _MettalicTex("Metallic Texture", 2D) = "white" {}
        _SmoothnessTex("Smoothness Texture", 2D) = "white" {}
        _MetallicStrength("Metallic Strength", Float) = 0.5
        _SmoothnessStrength("Smoothness", Float) = 0.5
        [Normal] _NormalTex("Normal Texture", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Float) = 1
        [Toggle(USE_EMISSION_ON)] _EmissionOn("USE_EMISSION_ON", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        _EmissionTex("Emission Texture", 2D) = "white" {}
        _AOtex("Ambient Occlusion Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags{
                "LightMode" = "UniversalForward"
            }

        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Material keyword (toggled per material)
            #pragma shader_feature_local USE_EMISSION_ON

            // URP keywords: global, so URP can switch them on (shadows, extra lights, SSAO, ...)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceData.hlsl"

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 staticLightMapUV : TEXCOORD1;
                float2 dynamicLightMapUV : TEXCOORD2;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 viewDirWS : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                DECLARE_LIGHTMAP_OR_SH(staticLightMapUV, vertexSH, 6);
                float2 dynamicLightmapUV : TEXCOORD7;
                float fogFactor : TEXCOORD8;
            };

            sampler2D _BaseTex;
            sampler2D _MettalicTex;
            sampler2D _SmoothnessTex;
            sampler2D _NormalTex;
            sampler2D _EmissionTex;
            sampler2D _AOtex;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseTex_ST;
                float _MetallicStrength;
                float _SmoothnessStrength;
                float _NormalStrength;
                float4 _EmissionColor;
            CBUFFER_END

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, v.tangentOS);

                o.positionWS = vertexInput.positionWS;
                o.positionCS = vertexInput.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseTex);
                o.normalWS = normalInput.normalWS;
                o.tangentWS = float4(normalInput.tangentWS.xyz, v.tangentOS.w * GetOddNegativeScale());
                o.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                o.shadowCoord = GetShadowCoord(vertexInput);
                o.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                OUTPUT_LIGHTMAP_UV(v.staticLightMapUV, unity_LightmapST, o.staticLightMapUV);
#ifdef DYNAMICLIGHTMAP_ON
                o.dynamicLightmapUV = v.dynamicLightMapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
                OUTPUT_SH(o.normalWS.xyz, o.vertexSH);
                return o;
            }

            SurfaceData createSurfaceData(v2f i)
            {
                SurfaceData surfaceData = (SurfaceData)0;

                float4 albedoSample = tex2D(_BaseTex, i.uv);
                surfaceData.albedo = albedoSample.rgb * _BaseColor.rgb;
                surfaceData.alpha = albedoSample.a * _BaseColor.a;

                // each map now reads its own texture (they all read _BaseTex before)
                surfaceData.metallic = tex2D(_MettalicTex, i.uv).r * _MetallicStrength;
                surfaceData.smoothness = tex2D(_SmoothnessTex, i.uv).r * _SmoothnessStrength;
                surfaceData.normalTS = UnpackNormalScale(tex2D(_NormalTex, i.uv), _NormalStrength);

#if USE_EMISSION_ON
                surfaceData.emission = tex2D(_EmissionTex, i.uv).rgb * _EmissionColor.rgb;
#endif
                surfaceData.occlusion = tex2D(_AOtex, i.uv).r;
                return surfaceData;
            }

            InputData createInputData(v2f i, float3 normalTS)
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;

                float3 bitangent = i.tangentWS.w * cross(i.normalWS, i.tangentWS.xyz);
                inputData.tangentToWorld = float3x3(i.tangentWS.xyz, bitangent, i.normalWS);
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, inputData.tangentToWorld));
                inputData.viewDirectionWS = SafeNormalize(i.viewDirWS);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = i.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
                inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
                inputData.fogCoord = i.fogFactor;

#ifdef DYNAMICLIGHTMAP_ON
                inputData.bakedGI = SAMPLE_GI(i.staticLightMapUV, i.dynamicLightmapUV, i.vertexSH, inputData.normalWS);
#else
                inputData.bakedGI = SAMPLE_GI(i.staticLightMapUV, i.vertexSH, inputData.normalWS);
#endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(i.staticLightMapUV);
                return inputData;
            }

            float4 frag(v2f i) : SV_Target
            {
                SurfaceData surfaceData = createSurfaceData(i);
                InputData inputData = createInputData(i, surfaceData.normalTS);

                float4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
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

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            // needed for point/spot lights to cast shadows correctly
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth passes: needed by the depth prepass, SSAO and soft particles
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }
    }
}
