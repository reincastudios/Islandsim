// Lagoon water: depth tint from a per-tile depth texture, animated wave normals, fresnel sky
// reflection, sun glint and shoreline foam. Pipeline-agnostic (renders in URP and built-in).
Shader "IslandSim/Water"
{
    Properties
    {
        _DepthTex ("Depth (R) / Land (G)", 2D) = "black" {}
        _MapSize ("Map size", Vector) = (144, 144, 0, 0)
        _Shallow ("Shallow", Color) = (0.30, 0.88, 0.80, 1)
        _Mid ("Mid", Color) = (0.05, 0.58, 0.68, 1)
        _Deep ("Deep", Color) = (0.02, 0.20, 0.38, 1)
        _SkyTint ("Sky reflection", Color) = (0.30, 0.55, 0.85, 1)
        _WaveHeight ("Wave height", Float) = 0.035
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _DepthTex;
            float4 _MapSize;
            float4 _Shallow, _Mid, _Deep, _SkyTint;
            float _WaveHeight;
            // set globally by Atmosphere.cs
            float4 _IsimSunDir;     // xyz: direction toward the sun
            float4 _IsimHorizon;    // linear horizon colour
            float4 _IsimFog;        // x: start, y: end

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 w = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                w.y += sin(w.x * .45 + t * 1.3) * _WaveHeight + cos(w.z * .38 + t * 1.05) * _WaveHeight;
                o.wpos = w;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1));
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3. - 2. * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float waves(float2 p, float t)
            {
                return noise(p * .9 + float2(t * .35, t * .2)) * .5 + noise(p * 2.1 - float2(t * .25, -t * .4)) * .3 + noise(p * 4.7 + float2(-t * .6, t * .3)) * .2;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.wpos.xz / _MapSize.xy;
                bool outside = uv.x < 0. || uv.y < 0. || uv.x > 1. || uv.y > 1.;
                float4 d = tex2D(_DepthTex, saturate(uv));
                float depth = outside ? 1. : d.r, land = outside ? 0. : d.g;

                float2 p = i.wpos.xz; float e = .15, h0 = waves(p, t);
                float3 n = normalize(float3((h0 - waves(p + float2(e, 0), t)) * 2., 1., (h0 - waves(p + float2(0, e), t)) * 2.));
                float3 V = normalize(_WorldSpaceCameraPos - i.wpos);
                float fres = .02 + .98 * pow(1. - max(dot(n, V), 0.), 5.);

                float3 col = lerp(_Shallow.rgb, _Mid.rgb, smoothstep(.05, .45, depth));
                col = lerp(col, _Deep.rgb, smoothstep(.45, .92, depth));
                float3 sky = lerp(_IsimHorizon.rgb, _SkyTint.rgb, .35);
                col = lerp(col, sky, fres * .6);

                float3 L = normalize(_IsimSunDir.xyz);
                float3 R = reflect(-L, n); float rv = max(dot(R, V), 0.);
                col += float3(1., .95, .85) * (pow(rv, 180.) * 2.2 + pow(rv, 18.) * .08);

                float shore = max(1. - smoothstep(0., .10, depth), land * .8);
                float fn = noise(p * 3. + t * .6) * .6 + noise(p * 7. - t * .4) * .4;
                float bands = .5 + .5 * sin(depth * 90. - t * 2.4 + fn * 3.);
                float foam = shore * smoothstep(.45, .8, fn * .7 + bands * .5);
                col = lerp(col, float3(.97, .99, 1.), foam * .85);

                float alpha = lerp(.42, .9, smoothstep(0., .6, depth)) + fres * .2 + foam * .6;
                float fog = smoothstep(_IsimFog.x, max(_IsimFog.y, _IsimFog.x + 1.), length(_WorldSpaceCameraPos - i.wpos));
                col = lerp(col, _IsimHorizon.rgb, fog);
                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
    Fallback Off
}
