Shader "Fluid/PackDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always

        Pass
        {
            Name "FluidPackDepth"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(Depth);
            SAMPLER(sampler_Depth);

            float4 frag(Varyings IN) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D(Depth, sampler_Depth, IN.texcoord).r;

                return float4(depth, 0.0, 0.0, depth);
            }
            ENDHLSL
        }
    }
}
