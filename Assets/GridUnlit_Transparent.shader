Shader "Custom/GridUnlitBuiltin_Transparent"
{
    Properties
    {
        _LineColor ("Line Color", Color) = (1,1,1,0.9)
        _FillColor ("Fill Color", Color) = (1,1,1,0.0)
        _Tiling ("Grid Cells per Meter", Float) = 2.0
        _LineWidth ("Line Width (0-1)", Range(0.001, 0.2)) = 0.02
        _Opacity ("Overall Opacity", Range(0,1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            fixed4 _LineColor;
            fixed4 _FillColor;
            float  _Tiling;
            float  _LineWidth;
            float  _Opacity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * _Tiling; // ARPlaneMeshVisualizer supplies UVs ~ meters
                return o;
            }

            // returns 1 on grid lines, 0 elsewhere
            float gridMask(float2 uv, float width)
            {
                float2 g = abs(frac(uv) - 0.5);
                float edge = step(0.5 - width, max(g.x, g.y));
                return 1.0 - edge;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float mask = gridMask(i.uv, _LineWidth);
                fixed4 col = lerp(_FillColor, _LineColor, mask);
                col.a *= _Opacity;
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
