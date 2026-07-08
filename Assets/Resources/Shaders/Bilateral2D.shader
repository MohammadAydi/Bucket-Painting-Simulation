// Shaders/BilateralFilter2D.shader
// ──────────────────────────────────────────────────────────────────────────────
// Single-pass 2-D bilateral filter.
// URP port of Sebastian's Hidden/BilateralFilter2D.
// ──────────────────────────────────────────────────────────────────────────────
Shader "Fluid/BilateralFilter2D"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always

        Pass
        {
            Name "Bilateral2D"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "./BilateralPass.hlsl"

            float4 frag(Varyings IN) : SV_Target
            {
                return BilateralBlur2D(IN.texcoord);
            }
            ENDHLSL
        }
    }
}
