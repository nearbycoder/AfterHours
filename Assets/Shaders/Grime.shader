// Lit transparent overlay for every cleanable surface.
//   _PatternTex  RGB dirt colour, A coverage (high-res detail, generated on the CPU)
//   _MaskTex     R dirt remaining, G carpet nap (0.5 neutral), B wetness/foam, A ghost remaining
//   _GhostTex    RGB colour, A alpha of a secret layer. Mode 0: revealed where dirt was cleaned
//                (whiteboard ghosting). Mode 1: foam-resist (finger writing shows in foam).
// Lighting goes through URP's UniversalFragmentPBR with premultiplied alpha, so wet floors and
// foam keep their specular while the dirt alpha-blends over the real surface underneath.
Shader "AfterHours/Grime"
{
    Properties
    {
        _PatternTex ("Pattern", 2D) = "black" {}
        _MaskTex ("Mask", 2D) = "white" {}
        _GhostTex ("Ghost", 2D) = "black" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Size ("Size (m)", Vector) = (1, 1, 0, 0)
        _NoiseScale ("Noise tiles per metre", Float) = 3
        _DirtSmoothness ("Dirt smoothness", Range(0, 1)) = 0.25
        _NapStrength ("Carpet nap strength", Range(0, 1)) = 0
        _WetStrength ("Wet sheen strength", Range(0, 1)) = 0
        _FoamStrength ("Foam strength", Range(0, 1)) = 0
        _GhostMode ("Ghost mode", Float) = 0
        _GleamT ("Gleam progress", Float) = -1
        _Shimmer ("Dirty shimmer", Range(0, 1)) = 0
        _Fade ("Global dirt fade", Range(0, 1)) = 1
        _Scatter ("Light scatter (glass)", Range(0, 1)) = 0
        _Grain ("Particulate grain", Range(0, 1)) = 0.4
        _GrainScale ("Grain tiles per metre", Float) = 22
        _BackScatter ("Back scatter of scene colour (glass)", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _ALPHAPREMULTIPLY_ON 1

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_PatternTex); SAMPLER(sampler_PatternTex);
            TEXTURE2D(_MaskTex);    SAMPLER(sampler_MaskTex);
            TEXTURE2D(_GhostTex);   SAMPLER(sampler_GhostTex);
            TEXTURE2D(_NoiseTex);   SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Size;
                float _NoiseScale;
                float _DirtSmoothness;
                float _NapStrength;
                float _WetStrength;
                float _FoamStrength;
                float _GhostMode;
                float _GleamT;
                float _Shimmer;
                float _Fade;
                float _Scatter;
                float _Grain;
                float _GrainScale;
                float _BackScatter;
            CBUFFER_END

            // UV torch, set globally by the player's torch.
            float4 _AH_UvPos;   // xyz position, w on (0/1)
            float4 _AH_UvDir;   // xyz direction, w cos(half angle)
            float  _AH_UvRange;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                half3  vertexSH   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(input.normalOS);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = nrm.normalWS;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                o.vertexSH = SampleSH(nrm.normalWS);
                return o;
            }

            // Porter-Duff "over" with straight (non-premultiplied) colours.
            void Over(inout half3 col, inout half a, half3 c, half ca)
            {
                half outA = ca + a * (1 - ca);
                col = outA > 1e-4 ? (c * ca + col * a * (1 - ca)) / outA : col;
                a = outA;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 uv = input.uv;
                half4 m = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv);
                half4 p = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, uv);
                half4 g = SAMPLE_TEXTURE2D(_GhostTex, sampler_GhostTex, uv);
                float2 nuv = uv * _Size.xy * _NoiseScale;
                half n = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv).r;
                half n2 = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nuv * 3.7 + 0.31).r;

                // Dirt breaks up in noisy islands as it is removed instead of fading uniformly.
                half edge = n * 0.6;
                half grain = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uv * _Size.xy * _GrainScale).g;
                half dirtA = p.a * smoothstep(edge, edge + 0.4, m.r) * _Fade;
                dirtA *= lerp(1.0, saturate(grain * 1.9 - 0.15), _Grain);

                half3 col = 0;
                half a = 0;
                half smooth = 0.0;

                // Carpet nap: the vacuum leaves lighter / darker stripes by stroke direction.
                half napOffset = (m.g - 0.5) * 2;
                half napA = abs(napOffset) * _NapStrength * (0.75 + 0.5 * n2);
                Over(col, a, napOffset > 0 ? half3(0.92, 0.95, 1.0) : half3(0.0, 0.0, 0.02), napA);

                // Mop: wet floors darken slightly and turn glossy.
                half wetA = m.b * _WetStrength;
                Over(col, a, half3(0.02, 0.025, 0.03), wetA * 0.35);
                smooth = max(smooth, wetA * 0.92);

                // Ghost layer.
                half ghostA = 0;
                if (_GhostMode < 0.5)
                    ghostA = g.a * saturate((1 - m.r) * 1.6) * m.a;
                half foamResist = _GhostMode > 0.5 ? g.a : 0;
                Over(col, a, g.rgb, ghostA);

                // Squeegee foam: soapy white with bubble texture; finger-writing resists it.
                half bubbles = smoothstep(0.35, 0.75, n2) * 0.35 + 0.65;
                half foamA = m.b * _FoamStrength * bubbles * (1 - foamResist * 0.92);
                Over(col, a, half3(0.93, 0.96, 1.0), foamA * 0.85);
                smooth = max(smooth, foamA * 0.55);

                // Dirt on top.
                Over(col, a, p.rgb, dirtA);
                smooth = lerp(smooth, _DirtSmoothness, dirtA);

                // Night 1 onboarding shimmer: dirty areas breathe faintly.
                half shimmer = _Shimmer * dirtA * (0.5 + 0.5 * sin(_Time.y * 3.0 + n * 6.0));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = input.vertexSH;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = col;
                s.alpha = a;
                s.metallic = 0;
                s.specular = 0;
                s.smoothness = smooth;
                s.occlusion = 1;
                s.normalTS = half3(0, 0, 1);

                half4 lit = UniversalFragmentPBR(inputData, s);
                // Fully clean areas must not add a second specular layer over the real surface.
                lit.rgb *= saturate(a * 3.0);

                // UV torch: leftover grime fluoresces, so missed spots are easy to find.
                float3 toUv = input.positionWS - _AH_UvPos.xyz;
                float dist = length(toUv);
                half cone = smoothstep(_AH_UvDir.w, _AH_UvDir.w + 0.04, dot(toUv / max(dist, 1e-3), _AH_UvDir.xyz));
                half uvLit = _AH_UvPos.w * cone * saturate(1 - dist / max(_AH_UvRange, 0.01));
                half3 emission = (dirtA * 1.6 + ghostA * 0.6) * uvLit * half3(0.35, 0.85, 1.0);
                emission += shimmer * half3(0.55, 0.5, 0.35) * 0.25;
                // Glass grime and foam catch stray light (otherwise invisible against the night).
                half3 ambientish = input.vertexSH + 0.06;
                emission += (p.rgb * dirtA + half3(0.9, 0.95, 1.0) * foamA * 0.6) * _Scatter * ambientish * 2.0;
                if (_BackScatter > 0)
                {
                    // Haze on glass diffuses the bright city behind it.
                    float2 suv = inputData.normalizedScreenSpaceUV;
                    half3 behind = 0;
                    const float r = 0.012;
                    behind += SampleSceneColor(suv + float2(r, 0));
                    behind += SampleSceneColor(suv - float2(r, 0));
                    behind += SampleSceneColor(suv + float2(0, r * 1.7));
                    behind += SampleSceneColor(suv - float2(0, r * 1.7));
                    behind += SampleSceneColor(suv) * 2;
                    behind /= 6;
                    emission += behind * (dirtA * 0.9 + foamA * 0.35) * _BackScatter;
                }

                // Completion gleam: a bright diagonal band sweeps across once.
                if (_GleamT >= 0 && _GleamT <= 1.2)
                {
                    half d = (uv.x * 0.7 + uv.y * 0.3) - (_GleamT * 1.6 - 0.3);
                    half band = exp(-d * d * 180.0) * (1 - saturate(_GleamT - 0.9) * 3.3);
                    emission += band * half3(1.0, 0.97, 0.9) * 0.9;
                }

                lit.rgb += emission;
                lit.a = saturate(a + Luminance(emission) * 0.0);
                lit.rgb = MixFog(lit.rgb, inputData.fogCoord);
                return lit;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
