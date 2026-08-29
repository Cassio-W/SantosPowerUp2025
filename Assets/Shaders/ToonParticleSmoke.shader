Shader "ComicVFX/ToonParticleSmoke"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color (Lit)", Color) = (0.9, 0.9, 0.95, 1.0)
        _ShadowColor ("Shadow Band Color", Color) = (0.4, 0.45, 0.55, 1.0)
        _InkOutlineColor ("Ink Outline Color", Color) = (0.05, 0.05, 0.08, 1.0)

        [MainTexture] _MainTex ("Particle Texture / Noise", 2D) = "white" {}

        [Header(Cel and Step Settings)]
        _AlphaCutoff ("Alpha Cutoff (Borda Seca)", Range(0.01, 0.99)) = 0.4
        _OutlineWidth ("Ink Border Thickness", Range(0.0, 0.2)) = 0.08
        _CelThreshold ("Cel Shadow Threshold", Range(0.0, 1.0)) = 0.5

        [Header(Comic Stepped Time Animation)]
        [Toggle(_ENABLE_STEPPED_TIME)] _EnableSteppedTime ("Enable 12 FPS Stepped Time", Float) = 1
        _FPS ("Stepped FPS Speed", Float) = 12.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "TransparentCutout" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "AlphaTest"
        }
        LOD 200

        Pass
        {
            Name "ForwardPass"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ENABLE_STEPPED_TIME

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShadowColor;
                half4 _InkOutlineColor;
                float4 _MainTex_ST;
                half _AlphaCutoff;
                half _OutlineWidth;
                half _CelThreshold;
                float _FPS;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 animatedUV = input.uv;

                #if defined(_ENABLE_STEPPED_TIME)
                    // Stepped time for 12 FPS traditional comic animation feel
                    float steppedTime = floor(_Time.y * _FPS) / _FPS;
                    animatedUV += float2(steppedTime * 0.05, steppedTime * 0.03);
                #endif

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, animatedUV);
                half alphaVal = texColor.a * input.color.a;

                // Alpha Cutoff (Borda dura de nanquim/gibri)
                if (alphaVal < _AlphaCutoff - _OutlineWidth)
                {
                    discard;
                }

                // Main light for 2-band toon shading
                Light mainLight = GetMainLight();
                float3 normal = normalize(input.normalWS);
                float NdotL = dot(normal, normalize(mainLight.direction)) * 0.5 + 0.5;

                half3 baseColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, step(_CelThreshold, NdotL));
                baseColor *= input.color.rgb;

                // Draw ink border if pixel is in the outline threshold band
                half3 finalColor = baseColor;
                if (alphaVal < _AlphaCutoff)
                {
                    finalColor = _InkOutlineColor.rgb;
                }

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}
