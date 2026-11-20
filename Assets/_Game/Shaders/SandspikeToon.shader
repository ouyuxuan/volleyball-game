// Sandspike soft toon: one shader for the whole court (people, props, sand).
// The lit side takes the warm sun colour; the shaded side and cast shadows fall to a cool blue tint
// (the art bible's Shadow colour, set as a global by ToonGlobals.cs) instead of going grey or black.
// The step between them is soft, so faceted low-poly models read rounder, like the concept art.
Shader "Sandspike/Toon"
{
    Properties
    {
        [MainColor] _BaseColor ("Base colour", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        _ShadeStart ("Where the shade begins (0 = facing away, 1 = facing the sun)", Range(0, 1)) = 0.45
        _Softness ("Softness of the light-to-shade step", Range(0.01, 0.5)) = 0.22
        _ShadowDepth ("How far the shade falls toward the shadow tint", Range(0, 1)) = 0.4
        _ShadeDarken ("Base colour kept in the shade", Range(0, 1)) = 0.85
        _Ambient ("Sky light added everywhere", Range(0, 1)) = 0.15
        [HDR] _EmissionColor ("Glow", Color) = (0, 0, 0, 0)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha clip", Float) = 0
        [Toggle(_EMISSION)] _Emission ("Glow on", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Per-material values in one buffer, so the SRP Batcher can batch every toon object.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _BaseMap_ST;
            float _ShadeStart, _Softness, _ShadowDepth, _ShadeDarken, _Ambient, _Cutoff;
            float4 _EmissionColor;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        half4 BaseColour(float2 uv)
        {
            half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(c.a - _Cutoff);
            #endif
            return c;
        }
        ENDHLSL

        // ---- the colour pass ----
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _SandspikeShadowTint;    // global, from Palette.Shadow

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 base = BaseColour(i.uv);
                float3 n = normalize(i.normalWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));

                // Half-Lambert (0 = facing away, 1 = facing the sun), stepped softly into light or shade.
                half wrap = dot(n, sun.direction) * 0.5h + 0.5h;
                half lit = smoothstep(_ShadeStart - _Softness, _ShadeStart + _Softness, wrap) * sun.shadowAttenuation;

                half ao = 1.0h;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS)).directAmbientOcclusion;
                #endif

                // Shade blends TOWARD the blue tint (not multiplied by it): warm sand times blue would go olive, not blue.
                half3 shade = lerp(base.rgb * _ShadeDarken, _SandspikeShadowTint.rgb, _ShadowDepth);
                half3 colour = lerp(shade, base.rgb * sun.color, lit) * ao;
                colour += base.rgb * SampleSH(n) * _Ambient * ao;
                #if defined(_EMISSION)
                    colour += _EmissionColor.rgb;
                #endif
                colour = MixFog(colour, i.fog);
                return half4(colour, 1.0h);
            }
            ENDHLSL
        }

        // ---- casting shadows on everything else ----
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 toLight = normalize(_LightPosition - positionWS);
                #else
                    float3 toLight = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, toLight));
                cs = ApplyShadowClamping(cs);
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { BaseColour(i.uv); return 0; }
            ENDHLSL
        }

        // ---- depth, for the depth texture ----
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target { BaseColour(i.uv); return 0; }
            ENDHLSL
        }

        // ---- depth and normals, for the ambient occlusion effect ----
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                BaseColour(i.uv);
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }
}
