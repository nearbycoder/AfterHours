// Procedural particle sprites (no textures): soft dot, sparkle star, confetti square, ring.
// Shape comes from the material's _Shape; colour from vertex colour.
Shader "AfterHours/FxParticle"
{
    Properties
    {
        _Shape ("Shape (0 dot,1 star,2 square,3 ring,4 bubble)", Float) = 0
        _Intensity ("Intensity", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Fx"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Shape;
                float _Intensity;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct A { float4 pos : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };

            V Vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float r = length(p);
                half a;
                if (_Shape < 0.5) a = saturate(1 - r) * saturate(1 - r);
                else if (_Shape < 1.5)
                {
                    float star = max(saturate(1 - abs(p.x) * 6 - abs(p.y) * 0.9), saturate(1 - abs(p.y) * 6 - abs(p.x) * 0.9));
                    a = saturate(star + saturate(1 - r * 2.6) * 0.9);
                }
                else if (_Shape < 2.5) a = step(max(abs(p.x), abs(p.y)), 0.8);
                else if (_Shape < 3.5) a = saturate(1 - abs(r - 0.78) * 9);
                else a = saturate(1 - abs(r - 0.82) * 7) * 0.9 + saturate(1 - length(p - float2(-0.3, 0.35)) * 5) * 0.8;
                half4 c = i.color;
                c.rgb *= _Intensity;
                c.a *= a;
                // Additive materials use One/One; premultiply so both blend modes look right.
                if (_DstBlend < 1.5) c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }
}
