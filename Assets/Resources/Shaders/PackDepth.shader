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

            // C# binds depthRt via _mat.SetTexture("Depth", depthRT) — sample it by name.
            // Do NOT use _BlitTexture here: the scaleOffset-only Blitter overload never sets it.
            TEXTURE2D(Depth);
            SAMPLER(sampler_Depth);

            float4 frag(Varyings IN) : SV_Target
            {
                float depth = SAMPLE_TEXTURE2D(Depth, sampler_Depth, IN.texcoord).r;

                // Background pixels in depthRt were cleared to 10_000_000 by ParticleDepthPass.
                // Preserve that sentinel in .a so the composite shader can detect background.
                // .r = smoothable depth, .a = raw reference (never touched by blur)
                return float4(depth, 0.0, 0.0, depth);
            }
            ENDHLSL
        }
    }
}
