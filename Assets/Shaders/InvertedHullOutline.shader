Shader "ComicVFX/InvertedHullOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.08, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.02
        _ZOffset ("Z Offset", Range(-1, 1)) = -0.001
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Name "InvertedHullOutlinePass"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _ZOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Extrude vertex position along normal in object space
                float3 extrudedPos = input.positionOS.xyz + input.normalOS * _OutlineWidth;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(extrudedPos);
                output.positionCS = vertexInput.positionCS;

                #if defined(UNITY_REVERSED_Z)
                    output.positionCS.z -= _ZOffset * output.positionCS.w;
                #else
                    output.positionCS.z += _ZOffset * output.positionCS.w;
                #endif

                output.color = _OutlineColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
