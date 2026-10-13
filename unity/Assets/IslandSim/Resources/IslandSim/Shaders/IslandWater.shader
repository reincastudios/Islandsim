// The sea: three scrolling layers of an ocean normal map, sky reflection, refraction of the lagoon floor with light
// absorbed by the water column, sun glints and surf on the reef crest (the web game's water material). Round the coasts
// the same water is a fine grid whose vertices carry the waves (see WaterView): the swell rises as it shoals, the sea
// draws back, and the broken wave runs up the sand as a thin sheet of this same surface and drains away.
Shader "IslandSim/Water"
{
    Properties
    {
        _WaterNormal ("Normal map", 2D) = "bump" {}
        _UseRefr ("Refraction", Float) = 1
        [HideInInspector] _Fine ("Shore grid", Float) = 0
        [HideInInspector] _FineLod ("Morph start, end, grid step", Vector) = (60, 80, 0.25, 0)
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 0
        [HideInInspector] _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "IslandCommon.hlsl"
            #include "IslandShore.hlsl"

            TEXTURE2D(_IsDepth); SAMPLER(sampler_IsDepth);
            float3 _IsFogColor; float2 _IsFogRange;
            CBUFFER_START(UnityPerMaterial)
                float _UseRefr, _SrcBlend, _DstBlend, _ZWrite, _Fine;
                float4 _FineLod;
            CBUFFER_END

            // the drawn ground (TerrainView): corner heights and tile-centre heights, read texel by texel
            TEXTURE2D_FLOAT(_IsCornerY); TEXTURE2D_FLOAT(_IsCentreY);
            // which 16-tile blocks the shore grid draws, and how much open-sea swell the plane may show (WaterView)
            TEXTURE2D(_IsFineMask); TEXTURE2D(_IsCalm); SAMPLER(sampler_IsCalm);
            float4 _IsBlocks;   // x = block size in tiles, y, z = blocks across and down

            float CornerAt(int X, int Y) { return LOAD_TEXTURE2D_LOD(_IsCornerY, int2(clamp(X, 0, (int)_IsMapSize.x), clamp(Y, 0, (int)_IsMapSize.y)), 0).r; }
            float CentreAt(int X, int Y) { return LOAD_TEXTURE2D_LOD(_IsCentreY, int2(clamp(X, 0, (int)_IsMapSize.x - 1), clamp(Y, 0, (int)_IsMapSize.y - 1)), 0).r; }
            // the ground's height at any point: the same four triangles round each tile's centre as the terrain mesh
            // (TerrainView.SurfaceY), so the water meets the sand exactly where the sand is drawn
            float GroundAt(float2 p)
            {
                int X = (int)floor(p.x), Y = (int)floor(p.y); float u = p.x - X, v = p.y - Y;
                float a = CornerAt(X, Y), b = CornerAt(X + 1, Y), c = CornerAt(X, Y + 1), d = CornerAt(X + 1, Y + 1), m = CentreAt(X, Y);
                float p0, p1, s, t;
                if (v <= u && v <= 1.0 - u) { p0 = a; p1 = b; s = u; t = v; }
                else if (u >= v && u >= 1.0 - v) { p0 = b; p1 = d; s = v; t = 1.0 - u; }
                else if (v >= u && v >= 1.0 - u) { p0 = c; p1 = d; s = u; t = 1.0 - v; }
                else { p0 = a; p1 = c; s = v; t = u; }
                return p0 * (1.0 - s - t) + p1 * (s - t) + m * 2.0 * t;
            }
            bool FineAt(float2 p)
            {
                int2 b = (int2)floor(p / _IsBlocks.x);
                return b.x >= 0 && b.y >= 0 && b.x < (int)_IsBlocks.y && b.y < (int)_IsBlocks.z && LOAD_TEXTURE2D_LOD(_IsFineMask, b, 0).r > 0.5;
            }

            // The water's surface over the shore, as a height. Out of the shallows it is the still sea. The incoming swell is
            // a hump that slows and grows as the water shoals (Green's law) and is never taller than the water is deep; as
            // it arrives the sea draws back from the sand, then the broken wave runs up the beach as a sheet a few
            // centimetres thick that thins to nothing at its edge, and drains away. Where the ground stands above the
            // wave's reach the surface lies under the sand, so the sand itself draws the water's edge.
            // ed: how far inside the wave's edge (in rough beach tiles, + in the water); d: the still sea's depth here.
            float ShoreLevel(float2 p, out float ed, out float d)
            {
                float G = GroundAt(p); d = IS_WATER_Y - G;
                float sd = ShoreD(p);
                float gate = (1.0 - smoothstep(4.0, 5.0, sd)) * smoothstep(-2.5, -1.5, sd);   // nothing moves at a block's outer edge
                float4 wf = WaveFront(p, _IsTime);
                float cD = CrestDepth(wf), cW = 0.03 + 0.35 * cD;
                float A = 0.045 * clamp(pow(max(cD, 0.04) / 0.6, -0.25), 1.0, 2.0);
                float r = (d - cD) / cW;
                float crest = wf.x < 0.45 ? min(A * exp(-r * r), 0.78 * max(d, 0.0)) : 0.0;
                float run = RunUpH(p, wf);
                float draw = min(run, 0.0) * exp(-max(d, 0.0) / 0.25);   // the sea sucked back off the sand before the wave
                float level = IS_WATER_Y + (crest + draw) * gate;
                float wl = IS_WATER_Y + (run + JitH(p, _IsTime)) * gate;  // how high up the sand the water reaches now
                ed = (wl - G) / 0.25;
                return max(level, min(G + 0.035, wl));
            }

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 shore : TEXCOORD1; };   // shore: the surface's slope (tile space), ed, d

            Varyings vert(Attributes v)
            {
                Varyings o; o.shore = float4(0, 0, 99, -1);
                float3 w;
                [branch] if (_Fine > 0.5)
                {
                    // past the morph range every other vertex slides onto its even neighbour, so this grid turns into the
                    // next coarser one before the block switches to it and blocks of either grid meet without a crack
                    float3 o0 = v.positionOS.xyz; float step = _FineLod.z;
                    float3 w0 = TransformObjectToWorld(o0); w0.y = IS_WATER_Y;
                    float k = saturate((distance(w0, GetCameraPositionWS()) - _FineLod.x) / max(_FineLod.y - _FineLod.x, 1e-3));
                    float2 ij = round(float2(o0.x, -o0.z) / step);
                    ij -= frac(ij * 0.5) * 2.0 * k;
                    w = TransformObjectToWorld(float3(ij.x * step, 0, -ij.y * step));
                    float2 p = TileXZ(w); float ed, d, e1, e2, d1, d2; const float E = 0.12;
                    float h = ShoreLevel(p, ed, d);
                    float hx = ShoreLevel(p + float2(E, 0), e1, d1), hy = ShoreLevel(p + float2(0, E), e2, d2);
                    w.y = h;
                    o.shore = float4(-(hx - h) / E, -(hy - h) / E, ed, d);
                }
                else
                {
                    w = TransformObjectToWorld(v.positionOS.xyz); float2 p = TileXZ(w);
                    // a slow swell out at sea; none in or near the shore blocks, so the plane meets them flat
                    float2 bu = p / (_IsBlocks.x * _IsBlocks.yz);
                    float2 outside = max(max(-p, p - _IsMapSize.xy), 0.0);
                    float calm = max(SAMPLE_TEXTURE2D_LOD(_IsCalm, sampler_IsCalm, bu, 0).r, smoothstep(16.0, 48.0, max(outside.x, outside.y)));
                    w.y += (sin(p.x * 0.31 + _IsTime * 1.1) * 0.03 + cos(p.y * 0.27 + _IsTime * 0.9) * 0.03 + sin((p.x + p.y) * 0.8 + _IsTime * 1.9) * 0.012) * calm;
                }
                o.positionWS = w; o.positionCS = TransformWorldToHClip(w);
                return o;
            }

            float DAt(float2 xz) { float2 uv = xz / _IsMapSize.xy; return (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) ? 1.0 : SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, uv, 0).r; }

            half4 frag(Varyings i) : SV_Target
            {
                float3 vW = i.positionWS; float2 p = TileXZ(vW); float2 uv = p / _IsMapSize.xy;
                bool fine = _Fine > 0.5;
                if (!fine && FineAt(p)) discard;   // the shore grid draws this block
                bool outside = uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0;
                // the depth map has one texel per tile; read it through a small warped blur so the reef's drop-off into
                // the blue curves naturally instead of stepping in tile-sized teeth
                float2 dw = (float2(vn(p * 0.31), vn(p * 0.31 + 7.7)) - 0.5) * 1.2;
                float2 tx = 0.75 / _IsMapSize.xy, uvw = saturate(uv + dw / _IsMapSize.xy);
                float4 dt = (SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, uvw, 0) * 2.0
                    + SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, saturate(uvw + float2(tx.x, 0)), 0) + SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, saturate(uvw - float2(tx.x, 0)), 0)
                    + SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, saturate(uvw + float2(0, tx.y)), 0) + SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, saturate(uvw - float2(0, tx.y)), 0)) / 6.0;
                float depth = outside ? 1.0 : dt.r;
                float meters = depth * 2.2 + (outside ? 6.0 : dt.b * 6.0);   // open ocean (blue channel) is as deep inside the map as beyond it
                // the true thickness of water under this pixel, from the scene depth: over the sand shelf it thins to
                // nothing at the waterline, and the sea turns from turquoise to clear with it, with no line between
                float2 suv0 = GetNormalizedScreenSpaceUV(i.positionCS); float thin;
                float3 V0 = normalize(GetCameraPositionWS() - vW);
                {   // the depth texture is on at every quality, with or without refraction
                    float sz = LinearEyeDepth(SampleSceneDepth(suv0), _ZBufferParams);
                    float wz = -TransformWorldToView(vW).z;
                    float3 fw = -UNITY_MATRIX_V[2].xyz;
                    thin = max(sz - wz, 0.0) / max(dot(-V0, fw), 0.05) * abs(V0.y);
                }
                meters = min(meters, thin);
                float dg = fine ? i.shore.w : thin;   // the still sea's depth over the ground here
                // three scrolling layers of a real ocean normal map (the beach wets to the same waves); rougher
                // over open water, glassy where it thins
                float chop = lerp(0.22, 0.55, smoothstep(0.2, 1.0, depth)) * lerp(0.3, 1.0, smoothstep(0.0, 0.3, thin));
                // the surface's own shape (the shore grid's waves) under the ripples
                float2 sl = Ripples(p, _IsTime) * chop + i.shore.xy;
                // where the swell's crest is now (the vertices raise it; here it only picks up a sheen and breaks into foam)
                float sd = ShoreD(p); float4 wf = WaveFront(p, _IsTime);
                float2 ge = float2(0.25, 0.0);
                float2 gd = float2(ShoreD(p + ge.xy) - ShoreD(p - ge.xy), ShoreD(p + ge.yx) - ShoreD(p - ge.yx)); gd /= max(length(gd), 1e-3);
                float cD = CrestDepth(wf), cW = 0.03 + 0.35 * cD;   // the crest's depth and its width (in depth), narrower as it slows
                float rel = (dg - cD) / cW;                          // 0 on the crest, + seaward
                float crest = fine && wf.x < 0.45 ? exp(-rel * rel) * smoothstep(0.0, 0.08, dg) : 0.0;
                float ph = sd / 1.3 + _IsTime * 0.19 + WaveNz(p) * 0.9;
                sl += gd * sin(6.2832 * ph) * smoothstep(1.2, 2.2, sd) * (1.0 - smoothstep(3.0, 5.0, sd)) * 0.05 * vn(p * 0.4 + 3.0);   // faint, and only here and there
                float3 n = normalize(float3(sl.x, 1.0, -sl.y));                 // tile-space slope, as a Unity normal
                float3 V = normalize(GetCameraPositionWS() - vW); float NV = max(dot(n, V), 0.0);
                float F = 0.02 + 0.98 * pow(1.0 - NV, 5.0);
                float3 R = reflect(-V, n); R.y = abs(R.y);
                float3 refl = SAMPLE_TEXTURECUBE_LOD(_IsEnvCube, sampler_IsEnvCube, R, 0).rgb;
                // Maldivian water: clear over the white sand at the edge, bright aquamarine over the shelf, turquoise in the
                // lagoon, and the open ocean past the reef a deep royal blue. Red is absorbed first, blue last.
                float ma = meters * meters / (meters + 0.35);   // a hand's depth over sand stays clear; the colour builds as it deepens
                float3 absorb = exp(-ma * float3(1.35, 0.27, 0.15));
                float3 scatter = float3(0.0, 0.14, 0.17) * (1.0 - absorb.g);
                const float3 OCEAN = float3(0.004, 0.04, 0.17);
                float oceanK = smoothstep(2.5, 6.0, meters) * 0.92;
                float3 under; float alpha = 1.0;
                float3 floorC = _UseRefr > 0.5 ? SampleSceneColor(suv0) : 0;
                if (_UseRefr > 0.5) under = lerp(SampleSceneColor(suv0 + sl * 0.012 * min(meters, 1.5)) * absorb + scatter, OCEAN, oceanK);
                else
                {
                    // blended over the floor: pale and nearly see-through where it thins over the sand shelf
                    under = lerp(float3(0.10, 0.62, 0.64), OCEAN, oceanK / 0.92);
                    float sh = 1.0 - smoothstep(0.0, 0.6, meters);
                    under = lerp(under, float3(0.50, 0.86, 0.82), sh * 0.6);
                    alpha = saturate(0.40 + 0.60 * (1.0 - absorb.b)) * smoothstep(0.0, 0.5, thin);   // fades to nothing at the waterline
                }
                float cs = CloudShadow(p, _IsTime);
                float3 col = lerp(under, refl, F * (_UseRefr > 0.5 ? 1.0 : 0.7));
                float3 H = normalize(_IsSunDir + V); float nh = max(dot(n, H), 0.0);
                float spec = pow(nh, 420.0) * 1.6 + pow(nh, 60.0) * 0.06;
                col += _IsSunCol * spec * cs;
                // shoreline and reef-crest surf
                float g = length(float2(DAt(p + float2(0.7, 0)) - DAt(p - float2(0.7, 0)), DAt(p + float2(0, 0.7)) - DAt(p - float2(0, 0.7))));
                float fn = vn(p * 2.6 + _IsTime * 0.5) * 0.6 + vn(p * 6.3 - _IsTime * 0.35) * 0.4;
                // the lacy fringe along the water's edge on the sand
                float edge = fine ? EdgeFoam(p, _IsTime, i.shore.z, wf) : 0.0;
                float lace = smoothstep(0.38, 0.78, fn);
                // the swell tips over into foam once it is taller than the water is deep, broken along the shore, never a ruled line
                float band = fine && wf.x < 0.45 ? smoothstep(0.16, 0.05, cD) * smoothstep(-1.2, 0.0, rel) * (1.0 - smoothstep(0.0, 1.0 + 1.5 * wf.z, rel)) * lace * smoothstep(0.3, 0.6, vn(p * 0.8 + _IsTime * 0.05)) * smoothstep(0.0, 0.05, thin) : 0.0;
                float remnant = 0.0;   // spent foam: a lacy net drifting over the shallows and up the sand with each wave
                [branch] if (fine && thin < 0.45) remnant = (1.0 - smoothstep(0.08, 0.45, max(dg, thin))) * FoamNet(p, _IsTime) * FoamFresh(wf) * 0.6;
                // surf on the reef crests, away from the beaches: patches that flare and fade as sets break (no rings)
                float reefC = smoothstep(0.12, 0.35, g) * (1.0 - smoothstep(0.35, 0.7, depth)) * smoothstep(1.2, 2.5, sd)
                            * smoothstep(0.55, 0.85, fn) * (0.45 + 0.55 * sin(_IsTime * 0.9 + WaveNz(p) * 6.2832));
                col += float3(0.05, 0.07, 0.07) * crest * (1.0 - band) * (0.5 + 0.5 * cs);   // glassy sheen on the swell face
                // where the water thins to nothing on the sand it melts into it instead of ending on a line
                float contact = smoothstep(0.0, 0.012, thin);
                if (_UseRefr > 0.5) col = lerp(floorC, col, contact); else alpha *= contact;
                float foam = saturate(edge + band * 0.9 + remnant + reefC * 0.8);
                col = lerp(col, float3(0.92, 0.95, 0.97) * (0.75 + 0.35 * cs), foam * 0.9);
                if (_UseRefr < 0.5) alpha = max(alpha, foam * 0.9);
                float fog = smoothstep(_IsFogRange.x, _IsFogRange.y, length(GetCameraPositionWS() - vW));
                col = lerp(col, _IsFogColor, fog);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
