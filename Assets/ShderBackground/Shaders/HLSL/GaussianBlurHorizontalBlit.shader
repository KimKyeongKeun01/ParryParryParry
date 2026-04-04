Shader "Custom/Blur/GaussianHorizontalBlit"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BlurRadius ("Blur Radius", Float) = 5
        _Sigma ("Sigma", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "GaussianHorizontalBlit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _BlurRadius;
                float _Sigma;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                uint width, height;
                _MainTex.GetDimensions(width, height);

                float2 texelSize = float2(1.0 / width, 1.0 / height);

                int radius = max((int)round(_BlurRadius), 0);
                float sigma2 = max(_Sigma * _Sigma, 0.0001);

                float4 accum = float4(0, 0, 0, 0);
                float weightSum = 0.0;

                for (int x = -radius; x <= radius; x++)
                {
                    float fx = (float)x;
                    float weight = exp(-(fx * fx) / (2.0 * sigma2));

                    float2 sampleUV = IN.uv + float2(fx * texelSize.x, 0.0);
                    float4 sampleCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);

                    accum += sampleCol * weight;
                    weightSum += weight;
                }

                return accum / max(weightSum, 0.0001);
            }
            ENDHLSL
        }
    }
}