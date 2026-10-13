// The sea: three scrolling layers of an ocean normal map, sky reflection, refraction of the lagoon floor with light
// absorbed by the water column, sun glints, the shared wave cycle rolling in and breaking into foam on the beach, and
// surf on the reef crest (the web game's water material).
Shader "IslandSim/Water"
{
    Properties
    {
        _WaterNormal ("Normal map", 2D) = "bump" {}
        _UseRefr ("Refraction", Float) = 1
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
                float _UseRefr, _SrcBlend, _DstBlend, _ZWrite;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(v.positionOS.xyz); float2 p = TileXZ(w);
                // a slow swell out at sea, calming toward the coast so the water's edge stays where the waves put it
                float calm = smoothstep(2.0, 12.0, ShoreD(p));
                w.y += (sin(p.x * 0.31 + _IsTime * 1.1) * 0.03 + cos(p.y * 0.27 + _IsTime * 0.9) * 0.03 + sin((p.x + p.y) * 0.8 + _IsTime * 1.9) * 0.012) * calm;
                o.positionWS = w; o.positionCS = TransformWorldToHClip(w);
                return o;
            }

            float DAt(float2 xz) { float2 uv = xz / _IsMapSize.xy; return (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) ? 1.0 : SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, uv, 0).r; }

            half4 frag(Varyings i) : SV_Target
            {
                float3 vW = i.positionWS; float2 p = TileXZ(vW); float2 uv = p / _IsMapSize.xy;
                bool outside = uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0;
                float4 dt = SAMPLE_TEXTURE2D_LOD(_IsDepth, sampler_IsDepth, saturate(uv), 0);
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
                // three scrolling layers of a real ocean normal map (the same ripples the sheet on the sand carries); rougher
                // over open water, glassy where it thins
                float chop = lerp(0.22, 0.55, smoothstep(0.2, 1.0, depth)) * lerp(0.3, 1.0, smoothstep(0.0, 0.3, thin));
                float2 sl = Ripples(p, _IsTime) * chop;
                // the swell of the shared wave cycle rolls in and steepens as the water shoals; faint ripples further out
                float sd = ShoreD(p); float4 wf = WaveFront(p, _IsTime); float jit = SwashJit(p, _IsTime) * (1.0 - smoothstep(0.0, 0.8, sd));
                float rel = sd - wf.y + jit;                 // the incoming swell's crest
                float relE = sd - SwashEdge(wf) + jit;       // the sea's moving edge: no water landward of it (relE < 0)
                float cover = smoothstep(-0.01, 0.05, relE);
                float2 ge = float2(0.25, 0.0);
                float2 gd = float2(ShoreD(p + ge.xy) - ShoreD(p - ge.xy), ShoreD(p + ge.yx) - ShoreD(p - ge.yx)); gd /= max(length(gd), 1e-3);
                float crest = wf.y > 0.0 ? exp(-rel * rel / 0.05) * (1.0 - smoothstep(1.4, 2.4, wf.y)) : 0.0;
                sl += gd * (-2.0 * rel / 0.05) * crest * 0.05;
                float ph = sd / 1.3 + _IsTime * 0.19 + WaveNz(p) * 0.9;
                sl += gd * sin(6.2832 * ph) * smoothstep(1.2, 2.2, sd) * (1.0 - smoothstep(3.0, 5.0, sd)) * 0.12;
                float3 n = normalize(float3(sl.x, 1.0, -sl.y));                 // tile-space slope, as a Unity normal
                float3 V = normalize(GetCameraPositionWS() - vW); float NV = max(dot(n, V), 0.0);
                float F = (0.02 + 0.98 * pow(1.0 - NV, 5.0)) * (1.0 - SheetFade(thin));   // at the very edge the sheet on the sand takes over the reflection
                float3 R = reflect(-V, n); R.y = abs(R.y);
                float3 refl = SAMPLE_TEXTURECUBE_LOD(_IsEnvCube, sampler_IsEnvCube, R, 0).rgb;
                // light absorbed by the water column (red first), plus in-scattered turquoise
                float ma = meters * meters / (meters + 0.35);   // a hand's depth over sand stays clear; the turquoise builds as it deepens
                float3 absorb = exp(-ma * float3(1.55, 0.36, 0.24));
                float3 scatter = float3(0.02, 0.20, 0.26) * (1.0 - absorb.g) + float3(0.03, 0.12, 0.16) * smoothstep(1.5, 5.0, meters);
                float3 under; float alpha = 1.0;
                float3 floorC = _UseRefr > 0.5 ? SampleSceneColor(suv0) : 0;
                if (_UseRefr > 0.5) under = SampleSceneColor(suv0 + sl * 0.012 * min(meters, 1.5)) * absorb + scatter;
                else
                {
                    // blended over the floor: pale and nearly see-through where it thins over the sand shelf
                    under = lerp(float3(0.09, 0.50, 0.52), float3(0.02, 0.15, 0.23), smoothstep(0.4, 5.0, meters));
                    float sh = 1.0 - smoothstep(0.0, 0.6, meters);
                    under = lerp(under, float3(0.45, 0.80, 0.78), sh * 0.6);
                    alpha = saturate(0.40 + 0.60 * (1.0 - absorb.b)) * smoothstep(0.0, 0.5, thin);   // fades to nothing at the waterline
                }
                float cs = CloudShadow(p, _IsTime);
                float3 col = lerp(under, refl, F * (_UseRefr > 0.5 ? 1.0 : 0.7));
                float3 H = normalize(_IsSunDir + V); float nh = max(dot(n, H), 0.0);
                float spec = pow(nh, 420.0) * 1.6 + pow(nh, 60.0) * 0.06;
                col += _IsSunCol * spec * cs * (1.0 - SheetFade(thin));
                // shoreline and reef-crest surf
                float g = length(float2(DAt(p + float2(0.7, 0)) - DAt(p - float2(0.7, 0)), DAt(p + float2(0, 0.7)) - DAt(p - float2(0, 0.7))));
                float fn = vn(p * 2.6 + _IsTime * 0.5) * 0.6 + vn(p * 6.3 - _IsTime * 0.35) * 0.4;
                // the foam fringe on the sea's moving edge (the beach draws the same fringe once the edge is up the sand)
                float edge = EdgeFoam(p, _IsTime, relE, wf) * smoothstep(0.0, 0.03, thin);
                float lace = smoothstep(0.38, 0.78, fn);
                float band = wf.x < 0.45 ? smoothstep(-0.03, 0.0, rel) * (1.0 - smoothstep(0.0, 0.1 + 0.35 * wf.z, rel)) * wf.z * lace : 0.0;   // the swell tips over into foam as it reaches the beach
                float remnant = 0.0;   // spent foam: a lacy net drifting over the shallows, the same net the beach sheet carries
                [branch] if (sd < 1.8) remnant = cover * (1.0 - SheetFade(thin)) * (1.0 - smoothstep(0.4, 1.8, sd)) * FoamNet(p, _IsTime) * FoamFresh(wf) * 0.6;
                float reefC = smoothstep(0.12, 0.35, g) * (1.0 - smoothstep(0.35, 0.7, depth)) * smoothstep(0.5, 0.85, fn * 0.6 + (0.5 + 0.5 * sin(depth * 40.0 - _IsTime * 1.6)) * 0.5);
                col += float3(0.05, 0.07, 0.07) * crest * (1.0 - band) * (0.5 + 0.5 * cs);   // glassy sheen on the swell face
                // landward of the moving edge the sea has drawn back: just the wet sand shows
                if (_UseRefr > 0.5) col = lerp(floorC, col, cover); else alpha *= cover;
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
