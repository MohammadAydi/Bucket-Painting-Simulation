
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

           
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

          
            StructuredBuffer<float3> Positions; 
            
            StructuredBuffer<float4> _Pigments;
            float scale;

            
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
                float4 pigment    : TEXCOORD2; 
            };

            
            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float3 worldCentre = Positions[instanceID];

              float3 camRight = UNITY_MATRIX_I_V._m00_m10_m20;
                float3 camUp = UNITY_MATRIX_I_V._m01_m11_m21;

                
                float3 offset = IN.positionOS * scale * 2.0;
                float3 worldPos = worldCentre
                    + camRight * offset.x
                    + camUp * offset.y;

                OUT.positionCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                OUT.positionWS = worldPos;
                OUT.uv         = IN.uv;
               
                OUT.pigment    = _Pigments[instanceID];
                return OUT;
            }

            
            float LinearDepthToClipDepth(float linearDepth)
            {
              
                float depth01 = (linearDepth - _ProjectionParams.y)
                    / (_ProjectionParams.z - _ProjectionParams.y);
               
                return (1.0 - depth01 * _ZBufferParams.y) / (depth01 * _ZBufferParams.x);
            }

           
            struct FragOutput
            {
                float4 depth   : SV_Target0; 
                float4 pigment : SV_Target1;
            };

          
            FragOutput frag(Varyings IN, out float outDepth : SV_Depth)
            {
               
                float2 centreOffset = (IN.uv - 0.5) * 2.0;
                float  sqrDst       = dot(centreOffset, centreOffset);
                clip(1.0 - sqrDst); 

               
                float z    = sqrt(1.0 - sqrDst);
                float dcam = length(IN.positionWS - _WorldSpaceCameraPos);

                float3 viewPos = mul(UNITY_MATRIX_V, float4(IN.positionWS, 1.0)).xyz;
                float eyeDepth = -viewPos.z - z * scale;
                outDepth = LinearDepthToClipDepth(eyeDepth);

                FragOutput o;
                o.depth   = float4(eyeDepth, 0, 0, eyeDepth);
               
                o.pigment = float4(IN.pigment.rgb, IN.pigment.a);
                return o;
            }
            ENDHLSL
        }
    }
}