Shader "BOKS/Bubble3D BuiltIn"
{
    Properties { 
        _Tint("Membrane tint", Color) = (0.76,0.92,0.96,1)
        _CenterAlpha("Center opacity", Range(0,0.25)) = 0.025
        _RimAlpha("Rim opacity", Range(0,1)) = 0.48
        _RimPower("Rim tightness", Range(1,8)) = 3.2
        _Highlight("Highlight strength", Range(0,1)) = 0.85
        _Iridescence("Iridescence", Range(0,1)) = 0.16
        _Opacity("Overall opacity", Range(0,1)) = 1
 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"  }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings vert(Attributes i) { Varyings o; o.positionWS = mul(unity_ObjectToWorld,i.positionOS).xyz; o.positionCS = UnityObjectToClipPos(i.positionOS); o.normalWS = UnityObjectToWorldNormal(i.normalOS); return o; }
            float3 ViewNormal(float3 n) { return mul((float3x3)UNITY_MATRIX_V,n); }

            float4 _Tint; float _CenterAlpha, _RimAlpha, _RimPower, _Highlight, _Iridescence, _Opacity;
            
            float ellipse(float2 p, float2 c, float2 size)
            {
                float2 d = (p - c) / size;
                return exp(-dot(d,d) * 2.0);
            }
            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.positionWS);
                float rim = pow(1.0 - saturate(dot(n,v)), _RimPower);
                float3 nv = normalize(ViewNormal(n));
                float2 p = nv.xy;
                float shift = sin(_Time.y * 0.55) * 0.025;
                float h = ellipse(p, float2(-0.36 + shift,0.56),float2(0.10,0.20));
                h += ellipse(p, float2(0.52,0.28 + shift),float2(0.045,0.13)) * 0.8;
                h += ellipse(p, float2(-0.16,-0.58),float2(0.07,0.12)) * 0.35;
                // Procedural softbox reflections, deliberately independent of scene lights.
                float broad = ellipse(p, float2(0.3,0.82),float2(0.44,0.1));
                h = saturate((h + broad * 0.65) * _Highlight);
                float3 rainbow = 0.5 + 0.5 * cos(p.y * 5.0 + p.x * 2.0 + float3(0,2.1,4.2) + _Time.y * 0.15);
                float3 tint = lerp(_Tint.rgb, rainbow, _Iridescence * rim);
                float alpha = saturate(_CenterAlpha + rim * _RimAlpha + h * 0.9) * _Opacity;
                float3 rgb = lerp(tint, float3(1,1,1), saturate(h + rim * 0.25));
                return half4(rgb,alpha);
            }

            ENDCG
        }
    }
    Fallback Off
}
