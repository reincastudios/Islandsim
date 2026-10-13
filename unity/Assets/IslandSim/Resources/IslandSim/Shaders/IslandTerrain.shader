// The ground: seven texture layers (sand, grass, forest, rock, reef, soil, path) blended by per-vertex weights, with
// triplanar rock on cliffs, ragged road edges, waves washing up the beach and caustics under water (the web game's terrain).
Shader "IslandSim/Terrain"
{
    Properties
    {
        _Alb ("Albedo layers", 2DArray) = "" {}
        _Nor ("Normal layers", 2DArray) = "" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "IslandCommon.hlsl"
        #include "IslandShore.hlsl"
        TEXTURE2D_ARRAY(_Alb); SAMPLER(sampler_Alb);
        TEXTURE2D_ARRAY(_Nor); SAMPLER(sampler_Nor);
        CBUFFER_START(UnityPerMaterial)
            float _Dummy;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            float4 splat0 : TEXCOORD0;
            float4 splat1 : TEXCOORD1;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma require 2darray
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half3 tint : TEXCOORD2;
                float4 s0 : TEXCOORD3;
                float4 s1 : TEXCOORD4;
                float fog : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tint = v.color.rgb; o.s0 = v.splat0; o.s1 = v.splat1;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // texture coordinates and their screen derivatives, worked out before the layer loop so it can skip empty layers
            struct UVs { float2 uv, dx, dy, px, pxx, pxy, py, pyx, pyy, pz, pzx, pzy; };
            #define ARR(T, s, uv, l, dx, dy) SAMPLE_TEXTURE2D_ARRAY_GRAD(T, s, uv, l, dx, dy)
            // rock: triplanar so cliffs are not smeared; everything else: two scales blended to hide repetition
            half4 LayAlb(int l, UVs u, float3 b)
            {
                if (l == 3) return ARR(_Alb, sampler_Alb, u.px, 3, u.pxx, u.pxy) * b.x + ARR(_Alb, sampler_Alb, u.py, 3, u.pyx, u.pyy) * b.y + ARR(_Alb, sampler_Alb, u.pz, 3, u.pzx, u.pzy) * b.z;
                return lerp(ARR(_Alb, sampler_Alb, u.uv, l, u.dx, u.dy), ARR(_Alb, sampler_Alb, u.uv * 0.29 + float2(0.31, 0.17), l, u.dx * 0.29, u.dy * 0.29), 0.35);
            }
            half3 LayNor(int l, UVs u, float3 b)
            {
                if (l == 3) return UnpackNormal(ARR(_Nor, sampler_Nor, u.px, 3, u.pxx, u.pxy)) * b.x + UnpackNormal(ARR(_Nor, sampler_Nor, u.py, 3, u.pyx, u.pyy)) * b.y + UnpackNormal(ARR(_Nor, sampler_Nor, u.pz, 3, u.pzx, u.pzy)) * b.z;
                return lerp(UnpackNormal(ARR(_Nor, sampler_Nor, u.uv, l, u.dx, u.dy)), UnpackNormal(ARR(_Nor, sampler_Nor, u.uv * 0.29 + float2(0.31, 0.17), l, u.dx * 0.29, u.dy * 0.29)), 0.35);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 wp = i.positionWS; float2 xz = TileXZ(wp);
                float wts[7];
                wts[0] = i.s0.x; wts[1] = i.s0.y; wts[2] = i.s0.z; wts[3] = i.s0.w; wts[4] = i.s1.x; wts[5] = i.s1.y;
                wts[6] = smoothstep(0.28, 0.6, i.s1.z + (SAMPLE_TEXTURE2D_ARRAY(_Alb, sampler_Alb, xz * 0.9, 0).r - 0.75) * 0.5); // roads: crisp, slightly ragged edges
                [unroll] for (int l0 = 0; l0 < 6; l0++) wts[l0] *= 1.0 - wts[6];
                float RGH[7] = { 0.92, 0.96, 0.97, 0.82, 0.78, 0.95, 0.94 };
                float3 gWN = normalize(i.normalWS); float2 gUV = xz * 0.25;
                half3 gAlb = 0; half3 gTN = 0; float gRough = 0; float wsum = 0;
                [unroll] for (int l1 = 0; l1 < 7; l1++) wsum += wts[l1];
                UVs u;
                u.uv = gUV; u.dx = ddx(gUV); u.dy = ddy(gUV);
                u.px = float2(-wp.z, wp.y) * 0.22; u.pxx = ddx(u.px); u.pxy = ddy(u.px);
                u.py = xz * 0.22; u.pyx = ddx(u.py); u.pyy = ddy(u.py);
                u.pz = wp.xy * 0.22; u.pzx = ddx(u.pz); u.pzy = ddy(u.pz);
                float3 tb = pow(abs(gWN), 4.0); tb /= tb.x + tb.y + tb.z;
                [loop] for (int l = 0; l < 7; l++)
                {
                    float w = wts[l] / max(wsum, 1e-4); if (w < 0.02) continue;
                    gAlb += LayAlb(l, u, tb).rgb * w;
                    half3 tn = LayNor(l, u, tb); tn.y = -tn.y; gTN += tn * w; gRough += RGH[l] * w;
                }
                // swash: the sea ends at one moving edge, shared with the water. As each wave arrives the edge runs up the
                // sand as a sheet of the same water (same ripples, reflection and glint, same foam fringe), then drains
                // back down; between waves it draws back a little below the still waterline
                float sd = ShoreD(xz); float4 wf = WaveFront(xz, _IsTime); float jit = SwashJit(xz, _IsTime) * (1.0 - smoothstep(0.0, 0.8, sd));
                float rel = sd - SwashEdge(wf) + jit;   // rel > 0: under the sea
                float near = (1.0 - smoothstep(0.35, 0.6, wp.y)) * (1.0 - smoothstep(1.0, 1.6, -sd)) * (1.0 - smoothstep(1.5, 2.5, sd));
                float dpt = IS_WATER_Y - wp.y;                                                   // depth under the still sea
                float onBeach = near * smoothstep(0.005, -0.005, dpt);                           // above it: the beach's own
                float cov = near * smoothstep(-0.01, 0.05, rel);
                float sheet = cov * SheetFade(dpt);                                              // the water takes over as it deepens
                float edge = near * (1.0 - smoothstep(0.0, 0.03, dpt)) * EdgeFoam(xz, _IsTime, rel, wf);
                // as the sheet drains it leaves a bubbly line where it stopped, foam streaks pulled back toward the sea,
                // and a band of glistening sand between the line and the retreating water
                float R = WaveReach(xz), top = sd + R + jit;                                    // 0 at the highest reach
                float drain = smoothstep(0.55, 0.64, wf.x) * (1.0 - smoothstep(0.78, 1.0, wf.x));
                float bub = smoothstep(0.3, 0.7, vn(xz * 22.0) * 0.7 + vn(xz * 9.0) * 0.3);
                float mark = onBeach * drain * exp(-top * top / 0.0012) * bub;
                float streak = sheet * smoothstep(0.62, 0.7, wf.x) * (1.0 - smoothstep(0.85, 1.0, wf.x));
                [branch] if (streak > 0.001)   // only the draining sheet pays for the extra shore samples
                {
                    float2 ge = float2(0.2, 0.0);
                    float2 gd = float2(ShoreD(xz + ge.xy) - ShoreD(xz - ge.xy), ShoreD(xz + ge.yx) - ShoreD(xz - ge.yx)); gd /= max(length(gd), 1e-3);
                    streak *= smoothstep(0.62, 0.85, vn(float2(dot(xz, float2(-gd.y, gd.x)) * 11.0, dot(xz, gd) * 1.6 + wf.x * 5.0)));
                }
                float drained = onBeach * step(rel, 0.0) * smoothstep(-0.03, 0.02, top) * drain;
                float wet = max(cov, onBeach * (1.0 - smoothstep(0.0, 0.95, -sd)) * 0.55);   // sand stays damp where the sea last reached
                wet = max(wet, drained * 0.85);
                gAlb *= lerp(1.0, half3(0.80, 0.75, 0.66), wet);
                float net = 0.0;
                [branch] if (sheet > 0.001) net = sheet * FoamNet(xz, _IsTime) * FoamFresh(wf) * 0.6;   // the shallows' foam lace rides up with the sheet
                float froth = saturate(edge + mark * 0.55 + streak * 0.35 + net);
                gAlb = lerp(gAlb, half3(0.95, 0.97, 0.98), froth); if (wp.y < -0.05) gAlb *= 0.92;
                half3 albedo = gAlb * i.tint;
                float rough = lerp(lerp(gRough, 0.5, wet), 0.26, drained);
                // the layers' normal maps on a tangent frame along +x
                float3 Tg = normalize(float3(1, 0, 0) - gWN * gWN.x); float3 Bg = -cross(Tg, gWN);
                float3 tn = normalize(float3(gTN.xy * 1.15, max(gTN.z, 0.2)));
                float3 n = normalize(Tg * tn.x + Bg * tn.y + gWN * tn.z);
                // caustics play on the lagoon floor
                half boost = 1;
                if (wp.y < 0.02) { float dpt = 0.02 - wp.y; float c = Caustic(xz * 0.9, _IsTime * 0.55); boost = 1.0 + c * 2.2 * exp(-dpt * 0.9) * smoothstep(0.0, 0.15, dpt); }
                half3 col = IslandShade(wp, n, albedo, 0, 1.0 - rough, GetNormalizedScreenSpaceUV(i.positionCS), boost);
                // the sheet on the sand is the sea's own surface: its reflection hands over to the water's as it deepens
                [branch] if (sheet * (1.0 - froth) > 0.001)
                {
                    float3 V = normalize(GetCameraPositionWS() - wp);
                    half3 w = WaterSurface(col * half3(0.91, 0.98, 0.99), Ripples(xz, _IsTime) * 0.066, V, 1.0, CloudShadow(xz, _IsTime));
                    col = lerp(col, w, sheet * (1.0 - froth));
                }
                return half4(MixFog(col, i.fog), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection; float3 _LightPosition;
            float4 vert(Attributes v) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(v.positionOS.xyz); float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 ld = normalize(_LightPosition - p);
                #else
                    float3 ld = _LightDirection;
                #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(p, n, ld));
                #if UNITY_REVERSED_Z
                    c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(Attributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half frag(float4 p : SV_POSITION) : SV_Target { return p.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V vert(Attributes v) { V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(V i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
