//UNITY_SHADER_NO_UPGRADE
#ifndef GAUSSIAN_BLUR_SPRITE_INCLUDED
#define GAUSSIAN_BLUR_SPRITE_INCLUDED

void GaussianBlurHorizontal_float(
    UnityTexture2D MainTex,
    float2 UV,
    float BlurRadius,
    float Sigma,
    out float4 OutColor)
{
    float2 texelSize = MainTex.texelSize.xy;

    int radius = (int) round(BlurRadius);
    radius = max(radius, 0);

    float4 accum = float4(0, 0, 0, 0);
    float weightSum = 0.0;

    for (int x = -radius; x <= radius; x++)
    {
        float fx = (float) x;
        float weight = exp(-(fx * fx) / max(2.0 * Sigma * Sigma, 0.0001));

        float2 sampleUV = UV + float2(fx * texelSize.x, 0.0);
        float4 sampleCol = SAMPLE_TEXTURE2D(MainTex.tex, MainTex.samplerstate, sampleUV);

        accum += sampleCol * weight;
        weightSum += weight;
    }

    OutColor = accum / max(weightSum, 0.0001);
}

void GaussianBlurVertical_float(
    UnityTexture2D MainTex,
    float2 UV,
    float BlurRadius,
    float Sigma,
    out float4 OutColor)
{
    float2 texelSize = MainTex.texelSize.xy;

    int radius = (int) round(BlurRadius);
    radius = max(radius, 0);

    float4 accum = float4(0, 0, 0, 0);
    float weightSum = 0.0;

    for (int y = -radius; y <= radius; y++)
    {
        float fy = (float) y;
        float weight = exp(-(fy * fy) / max(2.0 * Sigma * Sigma, 0.0001));

        float2 sampleUV = UV + float2(0.0, fy*fy * texelSize.y);
        float4 sampleCol = SAMPLE_TEXTURE2D(MainTex.tex, MainTex.samplerstate, sampleUV);

        accum += sampleCol * weight;
        weightSum += weight;
    }

    OutColor = accum / max(weightSum, 0.0001);
}

#endif