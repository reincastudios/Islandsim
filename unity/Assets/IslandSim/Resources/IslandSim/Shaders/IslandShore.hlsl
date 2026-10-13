// Shared by the water and the beach so the sea and each wave on the sand are one body of water: the same ripples, the
// same sky reflection and sun glint, one moving edge that both cut the sea off at, and one foam fringe riding on it.
#ifndef ISLAND_SHORE_INCLUDED
#define ISLAND_SHORE_INCLUDED

TEXTURE2D(_IsWaterNormal); SAMPLER(sampler_IsWaterNormal);
TEXTURECUBE(_IsEnvCube); SAMPLER(sampler_IsEnvCube);
float3 _IsSunDir; float3 _IsSunCol;
// The sea level right now (World.WATER_Y plus the tide), and how far the tide has moved the water's edge in tiles of the
// shoreline field (+ = up the beach), both set each frame by the game
float _IsSeaY; float _IsTideSd;
#define IS_WATER_Y _IsSeaY
// Near the edge the beach draws the water's surface and the sea fades in over it as the water deepens, by these weights,
// so between them the surface is always drawn once.
float SheetFade(float depthBelowSea) { return 1.0 - smoothstep(0.0, 0.15, depthBelowSea); }

float2 WaterNrm(float2 uv) { return UnpackNormal(SAMPLE_TEXTURE2D(_IsWaterNormal, sampler_IsWaterNormal, uv)).xy; }
// the sea's ripples: three scrolling layers of the ocean normal map, as a tile-space slope
float2 Ripples(float2 p, float t)
{
    float2 n1 = WaterNrm(p * 0.041 + float2(t * 0.010, t * 0.007));
    float2 n2 = WaterNrm(p * 0.097 - float2(t * 0.017, -t * 0.012));
    float2 n3 = WaterNrm(p * 0.29 + float2(-t * 0.031, t * 0.022));
    return n1 * 0.3 + n2 * 0.4 + n3 * 0.3;
}
// Where the sea ends right now, in tiles from the coastline (+ = out to sea). While a swell rolls in the water draws back
// a little; then the wave pushes the edge up the sand and drains back down. The tide moves the whole edge up the beach
// at high water and out over the sand shelf at low water. The water and the beach both stop the sea
// here, so the edge you see is the wave itself.
float SwashEdge(float4 wf) { return (wf.x < 0.45 ? 0.22 * sin(wf.x / 0.45 * 3.14159) : wf.y) - _IsTideSd; }
// the lacy foam fringe on that edge: strongest as the wave runs up, a faint one while the sea waits
float EdgeFoam(float2 p, float t, float rel, float4 wf)
{
    float k = wf.x < 0.45 ? 0.2 : wf.z;
    float lace = smoothstep(0.35, 0.75, vn(p * 7.0 + float2(t * 0.3, 0)) * 0.6 + vn(p * 16.0) * 0.4);
    return smoothstep(-0.03, 0.0, rel) * (1.0 - smoothstep(0.0, 0.06 + 0.2 * k, rel)) * lace * (0.35 + 0.65 * k);
}
// the sea's surface over whatever lies under it: sky reflection by Fresnel and the sun's glint
float3 WaterSurface(float3 under, float2 sl, float3 V, float reflK, float cs)
{
    float3 n = normalize(float3(sl.x, 1.0, -sl.y));
    float F = (0.02 + 0.98 * pow(1.0 - max(dot(n, V), 0.0), 5.0)) * reflK;
    float3 R = reflect(-V, n); R.y = abs(R.y);
    float3 col = lerp(under, SAMPLE_TEXTURECUBE_LOD(_IsEnvCube, sampler_IsEnvCube, R, 0).rgb, F);
    float nh = max(dot(n, normalize(_IsSunDir + V)), 0.0);
    return col + _IsSunCol * (pow(nh, 420.0) * 1.6 + pow(nh, 60.0) * 0.06) * cs * reflK;
}

#endif
