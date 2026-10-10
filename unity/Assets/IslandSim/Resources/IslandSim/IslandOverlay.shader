// Unlit, transparent overlay for the tile cursor, brush area and building placement ghosts.
// _Frame > 0 draws only a border of that width (in UV units), for the tile cursor.
Shader "IslandSim/Overlay"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.5)
        _Frame ("Frame width", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Frame;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float shade : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                float3 n = UnityObjectToWorldNormal(v.normal);
                o.shade = .75 + .25 * saturate(n.y);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 c = _Color;
                c.rgb *= i.shade;
                if (_Frame > 0)
                {
                    float2 e = min(i.uv, 1 - i.uv);
                    float edge = min(e.x, e.y);
                    c.a *= edge < _Frame ? 1 : 0.18;
                }
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
