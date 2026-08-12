Shader "ComicVFX/ToonOutlineScreen"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.04, 0.04, 0.07, 1.0)
        _Thickness ("Thickness (Pixels)", Range(0.5, 5.0)) = 1.25
        _DepthThreshold ("Depth Threshold", Range(0.001, 0.2)) = 0.015
        _DepthSensitivity ("Depth Sensitivity", Range(0.1, 10.0)) = 1.5
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

                // Roberts Cross samples
                float2 uv0 = input.uv + float2(-texel.x, -texel.y);
                float2 uv1 = input.uv + float2( texel.x,  texel.y);
                float2 uv2 = input.uv + float2( texel.x, -texel.y);
                float2 uv3 = input.uv + float2(-texel.x,  texel.y);

                float d0 = SampleDepthLinear(uv0);
                float d1 = SampleDepthLinear(uv1);
                float d2 = SampleDepthLinear(uv2);
                float d3 = SampleDepthLinear(uv3);

                // Calculate depth differences
                float diff1 = d1 - d0;
                float diff2 = d3 - d2;
                float depthEdge = sqrt(diff1 * diff1 + diff2 * diff2);

                float minD = min(min(d0, d1), min(d2, d3));
                float normalizedEdge = depthEdge / max(minD, 0.0001);

                // Compensate for surface depth slope (prevents flat surfaces like tables and floors from outlining)
                float ddxD = ddx(d0);
                float ddyD = ddy(d0);
                float slope = sqrt(ddxD * ddxD + ddyD * ddyD) / max(d0, 0.0001);

                // Dynamic threshold: scales up on slanted planes (tables, ground) to ignore surface gradient
                float dynamicThreshold = _DepthThreshold + slope * 4.0;

                // Step function for sharp comic outline
                float isOutline = step(dynamicThreshold, normalizedEdge * _DepthSensitivity);

                // Do not draw outline on skybox (depth near 1.0 in Linear01)
                if (minD > 0.98) isOutline = 0.0;

                return lerp(color, _OutlineColor, isOutline * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
