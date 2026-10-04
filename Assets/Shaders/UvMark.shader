// Invisible-ink marks (Walt's notes): only glow inside the UV torch cone.
Shader "AfterHours/UvMark"
{
    Properties
    {
        _MainTex ("Mask", 2D) = "white" {}
        _Color ("Glow", Color) = (0.45, 0.9, 1.0, 1)
        _Intensity ("Intensity", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+15" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "UvMark"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Offset -2, -2
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Intensity;
            CBUFFER_END
            float4 _AH_UvPos;
            float4 _AH_UvDir;
            float _AH_UvRange;

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 ws : TEXCOORD1; };

            V Vert(A i)
            {
                V o;
                o.ws = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.ws);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                half m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float3 d = i.ws - _AH_UvPos.xyz;
                float dist = length(d);
                half cone = smoothstep(_AH_UvDir.w, _AH_UvDir.w + 0.05, dot(d / max(dist, 1e-3), _AH_UvDir.xyz));
                half lit = _AH_UvPos.w * cone * saturate(1 - dist / max(_AH_UvRange, 0.01));
                return half4(_Color.rgb * m * lit * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
