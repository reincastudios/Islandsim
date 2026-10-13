// The sky: three.js's Sky (the Preetham daylight model) with the web game's settings, as a skybox, so the scene's ambient
// light and reflections come from the same sky the player sees.
Shader "IslandSim/Sky"
{
    Properties
    {
        _SunDir ("Sun direction", Vector) = (-0.55, 0.58, 0.42, 0)
        _Turbidity ("Turbidity", Float) = 3.6
        _Rayleigh ("Rayleigh", Float) = 1.25
        _MieCoef ("Mie coefficient", Float) = 0.0035
        _MieG ("Mie directional g", Float) = 0.82
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _SunDir; float _Turbidity, _Rayleigh, _MieCoef, _MieG;
            CBUFFER_END

            static const float E_ = 2.718281828459045;
            static const float PI_ = 3.141592653589793;
            static const float EE = 1000.0;
            static const float cutoffAngle = 1.6110731556870734;
            static const float steepness = 1.5;
            static const float3 totalRayleigh = float3(5.804542996261093E-6, 1.3562911419845635E-5, 3.0265902468824876E-5);
            static const float3 MieConst = float3(1.8399918514433978E14, 2.7798023919660528E14, 4.0790479543861094E14);
            static const float rayleighZenithLength = 8.4E3;
            static const float mieZenithLength = 1.25E3;
            static const float sunAngularDiameterCos = 0.99995667694644844;
            static const float THREE_OVER_SIXTEENPI = 0.05968310365946075;
            static const float ONE_OVER_FOURPI = 0.07957747154594767;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; float3 sunDir : TEXCOORD1;
                float sunFade : TEXCOORD2; float3 betaR : TEXCOORD3; float3 betaM : TEXCOORD4; float sunE : TEXCOORD5;
            };

            float SunIntensity(float zc) { zc = clamp(zc, -1.0, 1.0); return EE * max(0.0, 1.0 - pow(E_, -((cutoffAngle - acos(zc)) / steepness))); }
            float3 TotalMie(float T) { float c = (0.2 * T) * 10E-18; return 0.434 * c * MieConst; }
            float RayleighPhase(float c) { return THREE_OVER_SIXTEENPI * (1.0 + c * c); }
            float HgPhase(float c, float g) { float g2 = g * g; float inv = 1.0 / pow(abs(1.0 - 2.0 * g * c + g2), 1.5); return ONE_OVER_FOURPI * ((1.0 - g2) * inv); }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                o.sunDir = normalize(_SunDir.xyz);
                o.sunE = SunIntensity(dot(o.sunDir, float3(0, 1, 0)));
                o.sunFade = 1.0 - saturate(1.0 - exp(_SunDir.y / 450000.0));
                o.betaR = totalRayleigh * (_Rayleigh - (1.0 - o.sunFade));
                o.betaM = TotalMie(_Turbidity) * _MieCoef;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir); float3 up = float3(0, 1, 0);
                float zenith = acos(max(0.0, dot(up, d)));
                float inv = 1.0 / (cos(zenith) + 0.15 * pow(abs(93.885 - ((zenith * 180.0) / PI_)), -1.253));
                float sR = rayleighZenithLength * inv, sM = mieZenithLength * inv;
                float3 Fex = exp(-(i.betaR * sR + i.betaM * sM));
                float cosTheta = dot(d, i.sunDir);
                float3 betaRTheta = i.betaR * RayleighPhase(cosTheta * 0.5 + 0.5);
                float3 betaMTheta = i.betaM * HgPhase(cosTheta, _MieG);
                float3 k = i.sunE * ((betaRTheta + betaMTheta) / (i.betaR + i.betaM));
                float3 Lin = pow(abs(k * (1.0 - Fex)), 1.5);
                Lin *= lerp(1.0, pow(abs(k * Fex), 0.5), saturate(pow(abs(1.0 - dot(up, i.sunDir)), 5.0)));
                float3 L0 = 0.1 * Fex;
                float sundisk = smoothstep(sunAngularDiameterCos, sunAngularDiameterCos + 0.00002, cosTheta);
                L0 += (i.sunE * 19000.0 * Fex) * sundisk;
                float3 tex = (Lin + L0) * 0.04 + float3(0.0, 0.0003, 0.00075);
                float3 c = pow(abs(tex), 1.0 / (1.2 + (1.2 * i.sunFade)));
                return half4(min(c, 64.0), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
