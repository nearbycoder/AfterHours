// Unlit night city: building faces get a procedural grid of windows (some lit, warm or cool),
// the backdrop (_Mode 1) is a gradient sky with haze and a few stars. _Dawn tints toward morning.
Shader "AfterHours/Skyline"
{
    Properties
    {
        _Mode ("Mode (0 building, 1 sky)", Float) = 0
        _Body ("Body colour", Color) = (0.05, 0.07, 0.11, 1)
        _Seed ("Seed", Float) = 1
        _WindowSize ("Window cell (m)", Vector) = (1.6, 3.2, 0, 0)
        _LitFraction ("Lit fraction", Range(0, 1)) = 0.28
        _Dawn ("Dawn", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Mode;
                float4 _Body;
                float _Seed;
                float4 _WindowSize;
                float _LitFraction;
                float _Dawn;
            CBUFFER_END
            float _AH_Dawn;
            float _AH_Storm;

            struct A { float4 pos : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float2 uv : TEXCOORD2; half fog : TEXCOORD3; };

            float H(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7)) + _Seed * 17.13) * 43758.5453); }

            V Vert(A i)
            {
                V o;
                o.ws = TransformObjectToWorld(i.pos.xyz);
                o.pos = TransformWorldToHClip(o.ws);
                o.n = TransformObjectToWorldNormal(i.normal);
                o.uv = i.uv;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float dawn = saturate(max(_Dawn, _AH_Dawn));
                if (_Mode > 0.5)
                {
                    float y = i.uv.y;
                    float3 top = lerp(float3(0.012, 0.018, 0.04), float3(0.10, 0.16, 0.30), dawn);
                    float3 mid = lerp(float3(0.04, 0.06, 0.11), float3(0.55, 0.42, 0.45), dawn);
                    float3 glow = lerp(float3(0.16, 0.12, 0.10), float3(0.95, 0.62, 0.42), dawn);
                    float3 c = lerp(glow, mid, smoothstep(0.0, 0.35, y));
                    c = lerp(c, top, smoothstep(0.35, 1.0, y));
                    float star = step(0.9975, H(floor(i.uv * float2(900, 300)))) * smoothstep(0.4, 0.9, y) * (1 - dawn);
                    c += star * 0.5;
                    c *= 1 - _AH_Storm * 0.55;
                    return half4(c, 1);
                }
                float3 n = normalize(i.n);
                float2 p = abs(n.x) > 0.5 ? i.ws.zy : (abs(n.z) > 0.5 ? i.ws.xy : i.ws.xz);
                float2 cell = p / _WindowSize.xy;
                float2 id = floor(cell);
                float2 f = frac(cell);
                float win = step(0.18, f.x) * step(f.x, 0.82) * step(0.25, f.y) * step(f.y, 0.78);
                float h = H(id + n.xz * 13.1);
                float lit = step(1 - _LitFraction * (1 - dawn * 0.6), h);
                float warm = H(id * 1.7 + 3.1);
                float3 wc = warm > 0.35 ? float3(1.0, 0.72, 0.38) : float3(0.55, 0.8, 1.0);
                wc *= 0.6 + 0.8 * H(id * 3.3 + 1.0);
                float3 body = _Body.rgb * (abs(n.y) > 0.5 ? 1.4 : (n.x > 0.5 ? 0.8 : 1.0));
                body = lerp(body, body * 3 + float3(0.06, 0.05, 0.07), dawn * 0.6);
                float3 dark = body * 1.5 + 0.01;
                float3 c = lerp(body, lerp(dark, wc * 1.8, lit), win * step(abs(n.y), 0.5));
                // Aircraft warning lights on roofs.
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
