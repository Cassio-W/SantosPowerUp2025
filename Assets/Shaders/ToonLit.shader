Shader "ComicVFX/ToonLit"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base Map (Albedo)", 2D) = "white" {}

        [Header(Cel Shading Settings)]
        _ShadowStep ("Shadow Threshold", Range(0.0, 1.0)) = 0.45
        _ShadowSmoothness ("Shadow Smoothness", Range(0.01, 0.3)) = 0.08
        _ShadowColor ("Shadow Tint Color", Color) = (0.45, 0.45, 0.6, 1.0)

        [Header(Halftone Settings)]
        [Toggle(_ENABLE_HALFTONE)] _EnableHalftone ("Enable Halftone Dots", Float) = 0
        _HalftoneScale ("Halftone Density", Float) = 40.0
        _HalftoneDotSize ("Halftone Dot Size", Range(0.1, 0.9)) = 0.5

        [Header(Specular and Emission)]
        _SpecularColor ("Specular Color (Default Off)", Color) = (0, 0, 0, 0)
        _Smoothness ("Smoothness (Shininess)", Range(0.0, 1.0)) = 0.0
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ENABLE_HALFTONE

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _ShadowStep;
                half _ShadowSmoothness;
                half4 _ShadowColor;
                float _HalftoneScale;
                float _HalftoneDotSize;
                half4 _SpecularColor;
                half _Smoothness;
                half4 _EmissionColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float ComputeHalftonePattern(float2 uv, float scale, float dotSize)
            {
                float2 st = uv * scale;
                float2 gridUV = frac(st) - 0.5;
                float dist = length(gridUV);
                return step(dist, dotSize * 0.5);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Albedo sample
                half4 albedoTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 baseAlbedo = albedoTex.rgb * _BaseColor.rgb;

                // Main light setup
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                
                float3 normal = normalize(input.normalWS);
                float3 lightDir = normalize(mainLight.direction);
                
                // Softened Half-Lambert toon lighting (eliminates backface normal leaks and single-pixel shadow noise)
                float halfLambert = dot(normal, lightDir) * 0.5 + 0.5;
                half shadow = saturate(mainLight.shadowAttenuation);
                float lightFactor = halfLambert * lerp(0.7, 1.0, shadow);

                // Cel shading step with anti-flicker smoothing
                float celAttenuation = smoothstep(_ShadowStep - _ShadowSmoothness, _ShadowStep + _ShadowSmoothness, lightFactor);

                #if defined(_ENABLE_HALFTONE)
                    float halftone = ComputeHalftonePattern(input.uv, _HalftoneScale, _HalftoneDotSize);
                    float shadowMask = 1.0 - celAttenuation;
                    celAttenuation = lerp(celAttenuation, celAttenuation * 0.5, shadowMask * halftone);
                #endif

                // Mix Lit color and Shadow color
                half3 shadowTone = baseAlbedo * _ShadowColor.rgb;
                half3 litTone = baseAlbedo * mainLight.color;
                half3 diffuseResult = lerp(shadowTone, litTone, celAttenuation);

                // Specular (only active if _Smoothness > 0.05 AND _SpecularColor is set)
                half3 specularResult = half3(0, 0, 0);
                if (_Smoothness > 0.05 && (max(_SpecularColor.r, max(_SpecularColor.g, _SpecularColor.b)) > 0.01))
                {
                    float3 viewDir = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                    float3 halfDir = SafeNormalize(lightDir + viewDir);
                    float NdotH = saturate(dot(normal, halfDir));
                    float specularIntensity = pow(NdotH, max(_Smoothness * 32.0, 1.0));
                    float toonSpecular = smoothstep(0.45, 0.55, specularIntensity) * celAttenuation;
                    specularResult = _SpecularColor.rgb * toonSpecular;
                }

                // Ambient light
                half3 ambient = SampleSH(normal) * baseAlbedo * 0.3;

                // Final color
                half3 finalColor = diffuseResult + specularResult + ambient + _EmissionColor.rgb;

                return half4(finalColor, albedoTex.a * _BaseColor.a);
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            float3 _LightDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
