Shader "ComicVFX/ToonOutlineScreen"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.04, 0.04, 0.07, 1.0)
        _Thickness ("Thickness (Pixels)", Range(0.5, 5.0)) = 1.25
        _DepthThreshold ("Depth Threshold", Range(0.001, 0.2)) = 0.015
        _DepthSensitivity ("Depth Sensitivity", Range(0.1, 10.0)) = 2.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100
        ZWrite Off 
        ZTest Always 
        Cull Off

        Pass
        {
            Name "ToonScreenOutlinePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _Thickness;
                float _DepthThreshold;
                float _DepthSensitivity;
                float4 _BlitTexture_TexelSize;
            CBUFFER_END

            Varyings vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                output.uv = GetFullScreenTriangleTexCoord(vertexID);
                return output;
            }

            float SampleDepthLinear(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                return Linear01Depth(rawDepth, _ZBufferParams);
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv);

                float2 texel = _BlitTexture_TexelSize.xy;
                if (texel.x == 0) texel = float2(1.0 / 1920.0, 1.0 / 1080.0);
                texel *= _Thickness;

                float dCenter = SampleDepthLinear(input.uv);

                // 4-point cross depth samples
                float dL = SampleDepthLinear(input.uv - float2(texel.x, 0));
                float dR = SampleDepthLinear(input.uv + float2(texel.x, 0));
                float dD = SampleDepthLinear(input.uv - float2(0, texel.y));
                float dU = SampleDepthLinear(input.uv + float2(0, texel.y));

                // Depth Laplacian (Second Derivative): Zero on any flat or slanted surface (tables, floors), high on object edges!
                float d2x = (dR + dL) - (2.0 * dCenter);
                float d2y = (dU + dD) - (2.0 * dCenter);
                float laplacian = sqrt(d2x * d2x + d2y * d2y);

                // Normalized depth curvature ratio
                float relativeCurvature = laplacian / max(dCenter, 0.0001);

                // Smooth solid continuous ink line transition
                float val = relativeCurvature * _DepthSensitivity;
                float isOutline = smoothstep(_DepthThreshold, _DepthThreshold + 0.005, val);

                // Skybox exclusion
                if (dCenter > 0.98) isOutline = 0.0;

                return lerp(color, _OutlineColor, isOutline * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
