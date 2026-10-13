// Shared by the water and the beach: the sea's ripples, how far up the sand each wave runs, where the incoming swell's
// crest is, and the foam fringe on the water's edge. The water draws every wave itself (the shore grid in WaterView); the
// beach only reads the same numbers to darken the sand the waves wet.
#ifndef ISLAND_SHORE_INCLUDED
#define ISLAND_SHORE_INCLUDED

TEXTURE2D(_IsWaterNormal); SAMPLER(sampler_IsWaterNormal);
TEXTURECUBE(_IsEnvCube); SAMPLER(sampler_IsEnvCube);
float3 _IsSunDir; float3 _IsSunCol;
// The sea level right now (World.WATER_Y plus the tide), set each frame by the game, and the still sea it swings round
float _IsSeaY;
#define IS_WATER_Y _IsSeaY
#define IS_STILL_Y 0.02

float2 WaterNrm(float2 uv) { return UnpackNormal(SAMPLE_TEXTURE2D(_IsWaterNormal, sampler_IsWaterNormal, uv)).xy; }
// the sea's ripples: three scrolling layers of the ocean normal map, as a tile-space slope
float2 Ripples(float2 p, float t)
{
    float2 n1 = WaterNrm(p * 0.041 + float2(t * 0.010, t * 0.007));
    float2 n2 = WaterNrm(p * 0.097 - float2(t * 0.017, -t * 0.012));
    float2 n3 = WaterNrm(p * 0.29 + float2(-t * 0.031, t * 0.022));
    return n1 * 0.3 + n2 * 0.4 + n3 * 0.3;
}
// The shore works in heights, not distances, so the water always meets the ground where it really is, at any tide and on
// any slope. RunUpH is how far above the sea the wave's edge stands right now (world height): it draws back a little as
// the swell gathers, then the broken wave runs up the sand and drains away. A given height runs a long way up a gentle
// beach and only a short way up a steep one, as real swash does. Heights are turned into rough beach tiles (/ 0.25) where
// the foam wants a distance.
float RunMax(float2 p) { return 0.16 * (0.7 + 0.6 * vn(p * 0.6 + 7.0)); }
float RunUpH(float2 p, float4 wf)
{
    float R = RunMax(p);
    if (wf.x < 0.45) return -0.03 * sin(wf.x / 0.45 * 3.14159);
    if (wf.x < 0.62) return R * sin((wf.x - 0.45) / 0.17 * 1.5708);
    return R * (1.0 - smoothstep(0.62, 1.0, wf.x));
}
float JitH(float2 p, float t) { return SwashJit(p, t) * 0.25; }
// The incoming swell's crest, as the water depth it has reached: a wave travels at the square root of the depth, so on a
// sloping floor it slows as it shoals, its front bunching up as it nears the beach, and breaks in the last few hands of
// water.
float CrestDepth(float4 wf) { float k = wf.x / 0.45; return wf.x < 0.45 ? 0.6 * (1.0 - k) * (1.0 - k) : 0.0; }
// the lacy foam fringe on that edge: strongest as the wave runs up, a faint one while the sea waits
float EdgeFoam(float2 p, float t, float rel, float4 wf)
{
    float k = wf.x < 0.45 ? 0.2 : wf.z;
    float lace = smoothstep(0.35, 0.75, vn(p * 7.0 + float2(t * 0.3, 0)) * 0.6 + vn(p * 16.0) * 0.4);
    return smoothstep(-0.03, 0.0, rel) * (1.0 - smoothstep(0.0, 0.06 + 0.2 * k, rel)) * lace * (0.35 + 0.65 * k);
}
#endif
