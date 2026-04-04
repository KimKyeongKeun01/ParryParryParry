#ifndef TRIANGLE_SDF_INCLUDED
#define TRIANGLE_SDF_INCLUDED
float SegmentDistance(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}

void TriangleSDF_float(float2 UV, float2 A, float2 B, float2 C, out float Dist)
{
    float e1 = (UV.x - A.x) * (B.y - A.y) - (UV.y - A.y) * (B.x - A.x);
    float e2 = (UV.x - B.x) * (C.y - B.y) - (UV.y - B.y) * (C.x - B.x);
    float e3 = (UV.x - C.x) * (A.y - C.y) - (UV.y - C.y) * (A.x - C.x);

    float allPositive = step(0.0, e1) * step(0.0, e2) * step(0.0, e3);
    float allNegative = step(e1, 0.0) * step(e2, 0.0) * step(e3, 0.0);
    float inside = saturate(allPositive + allNegative);

    float d1 = SegmentDistance(UV, A, B);
    float d2 = SegmentDistance(UV, B, C);
    float d3 = SegmentDistance(UV, C, A);

    float minDist = min(d1, min(d2, d3));

    Dist = lerp(minDist, -minDist, inside);
}
#endif