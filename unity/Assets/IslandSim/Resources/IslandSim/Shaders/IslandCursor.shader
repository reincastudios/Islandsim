// The brush: a soft ring draped over the ground, with the tiles it will touch lightly tinted and the ones it would refuse
// in red (the web game's cursor material).
Shader "IslandSim/Cursor"
{
    Properties
    {
        _Col ("Colour", Color) = (0.95, 0.9, 0.77, 1)
        _C ("Centre (tile space)", Vector) = (0, 0, 0, 0)
        _R ("Radius", Float) = 1
        _Bad ("Refused tiles", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+30" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _IsTime;
            TEXTURE2D(_Bad); SAMPLER(sampler_Bad);
            CBUFFER_START(UnityPerMaterial)
                half4 _Col; float4 _C; float _R; float4 _Bad_ST;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; };
            V vert(A v) { V o; float3 w = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(w); o.p = float2(w.x, -w.z); return o; }
            half4 frag(V i) : SV_Target
            {
                float2 vP = i.p; float d = length(vP - _C.xy), w = fwidth(d);
                float ring = 1.0 - smoothstep(0.06, 0.06 + w * 1.5, abs(d - _R));                        // crisp ring at the brush edge
                float glow = (1.0 - smoothstep(0.0, 0.55, abs(d - _R))) * 0.35 * (0.75 + 0.25 * sin(_IsTime * 3.0)); // soft breathing halo around it
                float2 cell = floor(vP) + 0.5; float inside = step(length(cell - _C.xy), _R + 0.005);     // tiles the brush will change
                float bad = inside * SAMPLE_TEXTURE2D_LOD(_Bad, sampler_Bad, (cell - _C.xy + 5.5) / 11.0, 0).r; // ...and the ones it would refuse
                float2 f = abs(frac(vP) - 0.5); float grid = smoothstep(0.47, 0.5, max(f.x, f.y)) * inside;
                float a = max(ring * 0.95, max(glow, inside * 0.10 + grid * 0.22 + bad * 0.32));
                clip(a - 0.01);
                return half4(lerp(_Col.rgb * (1.0 + ring * 0.4), half3(1.0, 0.33, 0.28), bad * (1.0 - ring)), a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
