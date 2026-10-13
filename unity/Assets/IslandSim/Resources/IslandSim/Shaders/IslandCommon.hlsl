// Shared by every Island Sim shader: the web game's noise, cloud shadows, the coastline field and the wave cycle that
// the beach and the water share, caustics, and the lighting (URP PBR with the clouds dimming the sun).
#ifndef ISLAND_COMMON_INCLUDED
#define ISLAND_COMMON_INCLUDED

float _IsTime;
TEXTURE2D(_IsShore); SAMPLER(sampler_IsShore);
float4 _IsMapSize;          // x = W, y = H (tiles)

// The web game works in tile space (x = column, z = row); Unity's z runs the other way.
float2 TileXZ(float3 positionWS) { return float2(positionWS.x, -positionWS.z); }

float h21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
float vn(float2 p)
{
    float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(h21(i), h21(i + float2(1, 0)), f.x), lerp(h21(i + float2(0, 1)), h21(i + float2(1, 1)), f.x), f.y);
}
float CloudShadow(float2 xz, float t)
{
    float2 p = xz * 0.018 + float2(t * 0.006, t * 0.0035);
    float n = vn(p) * 0.6 + vn(p * 2.3 + 7.1) * 0.3 + vn(p * 5.1 + 3.7) * 0.1;
    return lerp(1.0, 0.58, smoothstep(0.52, 0.72, n));
}

// signed distance to the coastline in tiles (negative on land)
float ShoreD(float2 p)
{
    float2 uv = p / _IsMapSize.xy;
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) return 6.0;
    return SAMPLE_TEXTURE2D_LOD(_IsShore, sampler_IsShore, uv, 0).r * 9.0 - 3.0;
}
float WaveNz(float2 p) { return vn(p * 0.35) * 0.6 + vn(p * 0.9 + 3.1) * 0.4; }
// One wave, start to finish, shared by the water and the beach so they move as one: every ~5 s (offset along the shore)
// a swell rolls in from 2.4 tiles out, breaks at the waterline, runs up the sand as a thin sheet, then drains back.
// x = phase in the cycle, y = where the wave's front is (tiles from the shoreline, + = out to sea),
// z = how much foam rides on the front, w = how much water covers the sand behind it.
// how far up the sand this stretch of beach's waves run (tiles)
float WaveReach(float2 p) { return 0.8 * (0.7 + 0.6 * vn(p * 0.6 + 7.0)); }
// the run-up edge is never a straight line: it scallops into cusps that drift slowly along the beach
float SwashJit(float2 p, float t) { return (vn(p * 1.7 + float2(t * 0.07, -t * 0.05)) - 0.5) * 0.16 + (vn(p * 4.3) - 0.5) * 0.05; }
float4 WaveFront(float2 p, float t)
{
    float a = frac(t * 0.19 + WaveNz(p) * 0.9); float R = WaveReach(p); float f, fs, fl;
    if (a < 0.45) { float k = a / 0.45; f = 2.4 * (1.0 - k); fs = smoothstep(0.45, 1.0, k); fl = 0.25; }
    else if (a < 0.62) { f = -R * sin((a - 0.45) / 0.17 * 1.5708); fs = 1.0; fl = 1.0; }
    else { float k = smoothstep(0.62, 1.0, a); f = -R * (1.0 - k); fs = 1.0 - smoothstep(0.62, 0.92, a); fl = 1.0 - k * 0.8; }
    return float4(a, f, fs, fl);
}
// How fresh the foam in the shallows is: it spreads as each wave lands, then thins out until the next one.
float FoamFresh(float4 wf) { return wf.x > 0.45 ? 1.0 - 0.7 * smoothstep(0.5, 1.0, wf.x) : 0.3; }
float2 h22(float2 p) { return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453); }
// The lacy net of bubbles that covers shallow water after a wave breaks (cell edges of a slowly churning Voronoi
// pattern, torn into patches). The water and the beach both draw it from the same tile coordinates, so the lace runs
// unbroken from the shallows onto the sheet of water sliding up the sand.
float CellEdge(float2 q, float t)
{
    float2 i = floor(q), f = frac(q); float d1 = 8.0, d2 = 8.0;
    [unroll] for (int y = -1; y <= 1; y++)
        [unroll] for (int x = -1; x <= 1; x++)
        {
            float2 g = float2(x, y), o = h22(i + g); o = 0.5 + 0.45 * sin(t * 0.5 + 6.2832 * o);
            float2 r = g + o - f; float d = dot(r, r);
            if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
        }
    return sqrt(d2) - sqrt(d1);
}
float FoamNet(float2 p, float t)
{
    // warped so the cells are never regular, two sizes of cell, strands that thicken and thin, torn into patches
    float2 w = float2(vn(p * 1.7 + float2(t * 0.08, 0)), vn(p * 1.7 + float2(5.2, -t * 0.06))) - 0.5;
    float2 q = p + w * 0.7 + float2(t * 0.03, t * 0.02);
    float big = 1.0 - smoothstep(0.0, 0.06 + 0.1 * vn(p * 3.1 + 2.0), CellEdge(q * 6.0, t));
    float fine = 1.0 - smoothstep(0.0, 0.08 + 0.08 * vn(p * 4.7 + 9.0), CellEdge(q * 14.0 + 3.7, t * 1.3));
    float holes = smoothstep(0.4, 0.75, vn(p * 2.2 + t * 0.04) * 0.7 + vn(p * 5.0) * 0.3);
    return saturate(big * 0.8 + fine * 0.45) * holes * (0.6 + 0.4 * vn(p * 9.0));
}
float Caustic(float2 p, float t)
{
    float2 i = p; float c = 1.0;
    [unroll] for (int n = 0; n < 4; n++)
    {
        float tt = t * (1.0 - 3.5 / float(n + 1));
        i = p + float2(cos(tt - i.x) + sin(tt + i.y), sin(tt - i.y) + cos(tt + i.x));
        c += 1.0 / max(length(float2(p.x / (sin(i.x + tt) / 0.005), p.y / (cos(i.y + tt) / 0.005))), 0.05);
    }
    c /= 4.0; c = 1.17 - pow(abs(c), 1.4);
    return pow(saturate(c), 8.0);
}

// URP physically based shading with the sun dimmed by passing clouds (and boosted by caustics under water)
half3 IslandShade(float3 positionWS, half3 normalWS, half3 albedo, half metallic, half smoothness, float2 screenUV, half directBoost)
{
    SurfaceData s = (SurfaceData)0;
    s.albedo = albedo; s.metallic = metallic; s.smoothness = smoothness; s.occlusion = 1; s.alpha = 1; s.normalTS = half3(0, 0, 1);
    BRDFData brdf; InitializeBRDFData(s, brdf);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));
    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(screenUV, 1);
    mainLight.color *= ao.directAmbientOcclusion * CloudShadow(TileXZ(positionWS), _IsTime) * directBoost;
    half3 c = GlobalIllumination(brdf, SampleSH(normalWS), ao.indirectAmbientOcclusion, positionWS, normalWS, viewDirWS);
    c += LightingPhysicallyBased(brdf, mainLight, normalWS, viewDirWS);
    return c;
}
// Leaves: the light scatters through them, so the side away from the sun is never black (wrapped diffuse), sunlight
// glows through when you look toward the sun (transmission, more saturated than the reflected colour), and the canopy's
// own depth darkens the leaves inside it (occ, baked into the model; see FoliageUV).
half3 IslandShadeFoliage(float3 positionWS, half3 normalWS, half3 albedo, half smoothness, float2 screenUV, half occ)
{
    SurfaceData s = (SurfaceData)0;
    s.albedo = albedo; s.metallic = 0; s.smoothness = smoothness; s.occlusion = occ; s.alpha = 1; s.normalTS = half3(0, 0, 1);
    BRDFData brdf; InitializeBRDFData(s, brdf);
    half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));
    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(screenUV, occ);
    half3 sun = mainLight.color * ao.directAmbientOcclusion * CloudShadow(TileXZ(positionWS), _IsTime)
              * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
    // ambient sky light only: the reflected-sky term of GlobalIllumination turns thin leaf cards seen at a glancing
    // angle (most of them, from the game's high camera) a frosted white-blue
    half3 c = SampleSH(normalWS) * brdf.diffuse * ao.indirectAmbientOcclusion * occ;
    half nl = dot(normalWS, mainLight.direction);
    half wrapped = saturate((nl + 0.4) / 1.4);
    c += brdf.diffuse * sun * wrapped * lerp(0.55, 1.0, occ);
    c += brdf.specular * DirectBRDFSpecular(brdf, normalWS, mainLight.direction, viewDirWS) * sun * saturate(nl);
    half toSun = pow(saturate(dot(viewDirWS, -mainLight.direction)), 4.0);
    c += albedo * albedo * 2.0 * sun * (toSun * 0.9 + 0.12) * occ;
    return c;
}

// The game's own plant models carry their canopy occlusion in the whole-number part of u (0 = open .. 15 = deep inside),
// since OBJ has only one UV set; the leaf textures themselves only use 0..1.
float2 FoliageUV(float2 uv, out float occ)
{
    float k = floor(uv.x);
    occ = 1.0 - saturate(k / 15.0) * 0.85;
    return float2(uv.x - k, uv.y);
}
#endif
