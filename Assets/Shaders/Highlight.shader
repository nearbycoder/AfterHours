// "You can use this": a soft additive rim (plus a faint fill) drawn over whatever is under the
// reticle. Drawn by a second renderer that shares the object's meshes, pushed out a hair along
// the normals so it never z-fights the object itself.
Shader "AfterHours/Highlight"
{
    Properties
    {
        _Color ("Rim", Color) = (1.0, 0.78, 0.45, 1)
        _Intensity ("Intensity", Float) = 1.2
        _Fill ("Fill", Float) = 0.08
        _Push ("Push (m)", Float) = 0.003
        _Fade ("Fade", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Highlight"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Intensity;
                float _Fill;
                float _Push;
                float _Fade;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 ws : TEXCOORD1; };

            V Vert(A i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                V o;
                float3 n = TransformObjectToWorldNormal(i.normal);
                o.ws = TransformObjectToWorld(i.pos.xyz) + n * _Push;
                o.n = n;
                o.pos = TransformWorldToHClip(o.ws);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float3 v = normalize(GetWorldSpaceViewDir(i.ws));
                half rim = pow(1.0 - saturate(abs(dot(normalize(i.n), v))), 2.5);
                half pulse = 0.8 + 0.2 * sin(_Time.y * 4.0);
                half k = (rim * _Intensity + _Fill) * pulse * _Fade;
                return half4(_Color.rgb * k, 0);
            }
            ENDHLSL
        }
    }
}
