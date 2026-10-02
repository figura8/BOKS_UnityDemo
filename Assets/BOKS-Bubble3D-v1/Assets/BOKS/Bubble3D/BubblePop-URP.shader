Shader "BOKS/BubblePop URP"
{
    Properties { _Color("Color",Color) = (0.88,0.96,1,0.65) _Opacity("Opacity",Range(0,1)) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            Varyings vert(Attributes i) { Varyings o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            float3 ViewNormal(float3 n) { return TransformWorldToViewDir(n); }

            float4 _Color; float _Opacity;
            half4 frag(Varyings i) : SV_Target { return half4(_Color.rgb, _Color.a * _Opacity); }
            ENDHLSL
        }
    }
    Fallback Off
}
