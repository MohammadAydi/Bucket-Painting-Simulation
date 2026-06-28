// Shaders/NormalsFromDepth_URP.shader
// ──────────────────────────────────────────────────────────────────────────────
// Reconstructs world-space normals from the bilaterally-smoothed depth.
// Direct URP port of Sebastian's NormalsFromDepth.shader.
//
// Input:  compRt  (.r = smoothed depth, .a = raw depth / background sentinel)
// Output: normalRt (rgb = world-space normal, a = 1 if fluid, 0 if background)
//
// The min-Z-gradient trick (take the shorter of forward/backward derivative)
// preserves sharp silhouette normals at depth discontinuities.
// ──────────────────────────────────────────────────────────────────────────────
Shader "Fluid/NormalsFromDepth_URP"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always

        Pass
        {
            Name "FluidNormals"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            // Reconstruct view-space point from UV + linear depth
            float3 ViewPos(float2 uv)
            {
                float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                // useSmoothedDepth = 1 → read .r (bilateral output)
                //                  = 0 → read .a (raw / debug)
                // We always use smoothed depth here (set from C# via SetGlobalInt).
                float depth = s.r;  // smoothed depth

                float3 viewDir = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 0, -1)).xyz;
                return normalize(viewDir) * depth;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Background check — use .a (raw depth, never blurred)
                float rawDepth = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texcoord).a;
                if (rawDepth >= 9999999.0)
                    return float4(0, 0, 0, 0);  // no fluid here

                float3 centre = ViewPos(IN.texcoord);
                float2 o      = _MainTex_TexelSize.xy;

                // Forward and backward derivatives in X
                float3 ddxF = ViewPos(IN.texcoord + float2(o.x, 0)) - centre;
                float3 ddxB = centre - ViewPos(IN.texcoord - float2(o.x, 0));
                float3 ddx  = abs(ddxF.z) < abs(ddxB.z) ? ddxF : ddxB;

                // Forward and backward derivatives in Y
                float3 ddyF = ViewPos(IN.texcoord + float2(0, o.y)) - centre;
                float3 ddyB = centre - ViewPos(IN.texcoord - float2(0, o.y));
                float3 ddy  = abs(ddyF.z) < abs(ddyB.z) ? ddyF : ddyB;

                // Cross product → view-space normal → world space
                float3 viewNormal  = normalize(cross(ddy, ddx));
                float3 worldNormal = mul(UNITY_MATRIX_I_V, float4(viewNormal, 0)).xyz;

                return float4(worldNormal, 1.0);  // w=1 means "fluid pixel"
            }
            ENDHLSL
        }
    }
}
