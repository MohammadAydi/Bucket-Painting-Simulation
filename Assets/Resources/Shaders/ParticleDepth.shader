// Shaders/ParticleDepth3D.shader
// ──────────────────────────────────────────────────────────────────────────────
// Renders one billboard quad per particle.
// Reads position from the ParticleData3D ComputeBuffer (set via C#).
//
// Fragment outputs:
//   SV_Target  = float4(linearDepth, 0, 0, linearDepth)
//                  .r = depth to blur
//                  .a = depth reference (preserved unchanged by all blur passes)
//   SV_Depth   = reconstructed hardware depth (for correct Z-order)
//
// This is a direct URP port of Sebastian's ParticleDepth.shader combined with
// my ParticleDepth3D.shader — logic is identical, only the include changed
// (UnityCG.cginc → Core.hlsl).
// ──────────────────────────────────────────────────────────────────────────────
Shader "Fluid/ParticleDepth3D"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "FluidParticleDepth"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5

            // URP core includes (replaces UnityCG.cginc)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Particle data buffers ──────────────────────────────────────────
            StructuredBuffer<float3> Positions; // set via SetBuffer("Positions", PositionsBuffer)
            // Pigment buffer — float4 per particle. Meaning depends on _PigmentMixingMode:
            //   0 = LinearRGB : already displayable linear RGB
            //   1 = Mixbox    : already displayable linear RGB (the Mixbox diffusion
            //                   kernel converts to/from latent space on the GPU each step)
            // Both modes store displayable RGB directly, so no per-mode decode is
            // needed here anymore (RYB — the one mode that stored non-RGB pigment-
            // amount coordinates — has been removed).
            // Declared as a raw buffer so the shader compiles even when the C#
            // side has not yet set it (e.g. when no PigmentSettings is assigned).
            // The shader falls back to opaque white in that case.
            StructuredBuffer<float4> _Pigments;
            float scale;

            // ── Vertex/fragment structs ────────────────────────────────────────
            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 pigment    : TEXCOORD2; // linear RGBA pigment from buffer
            };

            // ── Vertex shader ──────────────────────────────────────────────────
            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float3 worldCentre = Positions[instanceID];

                // Billboard the quad in camera right / up (world space)
                // UNITY_MATRIX_I_V gives the camera-to-world matrix in URP.
                float3 camRight = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 camUp = UNITY_MATRIX_I_V._m01_m11_m21;

                // scale * 2 because quad verts are in [-0.5, +0.5]
                float3 offset = IN.positionOS * scale * 2.0;
                float3 worldPos = worldCentre
                    + camRight * offset.x
                    + camUp * offset.y;

                OUT.positionCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                OUT.positionWS = worldPos;
                OUT.uv         = IN.uv;
                // Read per-particle pigment color from GPU buffer.
                // Falls back to opaque white when the buffer is not set.
                OUT.pigment    = _Pigments[instanceID];
                return OUT;
            }

            // ── Reconstruct Unity clip-space depth from linear eye depth ───────
            // Identical to Sebastian's helper — translates linear depth to the
            // non-linear value the hardware depth buffer expects.
            float LinearDepthToClipDepth(float linearDepth)
            {
                // Convert linear depth to 0-1 range between near/far planes
                float depth01 = (linearDepth - _ProjectionParams.y)
                    / (_ProjectionParams.z - _ProjectionParams.y);
                // Invert the perspective divide
                return (1.0 - depth01 * _ZBufferParams.y) / (depth01 * _ZBufferParams.x);
            }

            // ── MRT fragment output ────────────────────────────────────────────
            struct FragOutput
            {
                float4 depth   : SV_Target0; // R32_SFloat depth RT (existing)
                float4 pigment : SV_Target1; // RGBA16F  pigment color RT (new)
            };

            // ── Fragment shader ────────────────────────────────────────────────
            FragOutput frag(Varyings IN, out float outDepth : SV_Depth)
            {
                // Circular disc mask — discard corners outside the sphere cross-section
                float2 centreOffset = (IN.uv - 0.5) * 2.0;
                float  sqrDst       = dot(centreOffset, centreOffset);
                clip(1.0 - sqrDst); // discard if outside unit circle

                // Reconstruct sphere front surface depth
                float z    = sqrt(1.0 - sqrDst);
                float dcam = length(IN.positionWS - _WorldSpaceCameraPos);

                float3 viewPos = mul(UNITY_MATRIX_V, float4(IN.positionWS, 1.0)).xyz;
                float eyeDepth = -viewPos.z - z * scale;
                outDepth = LinearDepthToClipDepth(eyeDepth);

                FragOutput o;
                o.depth   = float4(eyeDepth, 0, 0, eyeDepth);
                // Both remaining mixing models (LinearRGB, Mixbox) store the
                // buffer as already-displayable RGB, so no per-mode decode
                // is needed here — RYB was the only mode that required one.
                o.pigment = float4(IN.pigment.rgb, IN.pigment.a);
                return o;
            }
            ENDHLSL
        }
    }
}