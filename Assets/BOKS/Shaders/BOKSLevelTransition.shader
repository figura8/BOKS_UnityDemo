// Source-faithful BOKS level-transition fill.
// Mirrors the web SVG mask: an opaque --level-transition-fill (#ddd1bb) colour with a
// feathered square "cloud" hole (440px base, Gaussian-blurred edge) cut out of it.
Shader "BOKS/LevelTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Fill Color", Color) = (0.86667, 0.81961, 0.73333, 1.0)  // #DDD1BB
        _HoleCenter ("Hole Center (px)", Vector) = (260, 500, 0, 0)
        _HoleHalf ("Hole Half Size (px)", Float) = 220
        _Rotation ("Hole Rotation (deg)", Float) = 0
        _Feather ("Feather (px)", Float) = 2.5
        _HoleEnabled ("Hole Enabled", Float) = 1
        _ScreenSize ("Screen Size (px)", Vector) = (520, 1000, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            fixed4 _Color;
            float4 _HoleCenter;
            float _HoleHalf;
            float _Rotation;
            float _Feather;
            float _HoleEnabled;
            float4 _ScreenSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Position in screen pixels (bottom-left origin, matching Unity UI uv space).
                float2 pixel = i.uv * _ScreenSize.xy;
                float2 p = pixel - _HoleCenter.xy;

                float rad = _Rotation * 0.0174532925199433;
                float c = cos(rad), s = sin(rad);
                float2 pr = float2(c * p.x - s * p.y, s * p.x + c * p.y);

                // Signed distance to the (square) hole: negative inside, positive outside.
                float2 q = abs(pr) - float2(_HoleHalf, _HoleHalf);
                float d = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0);

                // 0 inside the hole (reveals the level behind), 1 outside (opaque fill), feathered edge.
                float alpha = smoothstep(0.0, _Feather, d);
                alpha = _HoleEnabled > 0.5 ? alpha : 1.0;
                return fixed4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }
}
