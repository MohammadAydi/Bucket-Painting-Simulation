// Shaders/BilateralFilter1D_URP.shader
// ──────────────────────────────────────────────────────────────────────────────
// Two-pass 1-D bilateral filter (Pass 0 = horizontal, Pass 1 = vertical).
// URP port of Sebastian's Hidden/BilateralFilter1D.
//
// Note: We keep the "Hidden/" prefix convention for blit shaders in URP too,
// which just means they won't appear in the material shader picker.
// ──────────────────────────────────────────────────────────────────────────────
Shader "Hidden/BilateralFilter1D_URP"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always

        // ── Pass 0: Horizontal ─────────────────────────────────────────────
        Pass
        {
            Name "BilateralH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../HLSL/BilateralPass_URP.hlsl"

            float4 fragH(Varyings IN) : SV_Target
            {
                return BilateralBlur1D(IN.texcoord, float2(1, 0));
            }
            ENDHLSL
        }

        // ── Pass 1: Vertical ───────────────────────────────────────────────
        Pass
        {
            Name "BilateralV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragV

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../HLSL/BilateralPass_URP.hlsl"

            float4 fragV(Varyings IN) : SV_Target
            {
                return BilateralBlur1D(IN.texcoord, float2(0, 1));
            }
            ENDHLSL
        }
    }
}
