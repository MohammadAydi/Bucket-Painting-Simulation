Shader "Fluid/ParticleDepth3D"
{
    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
        }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct ParticleData
            {
                float4 position;
                float4 velocity;
                float4 force;
                float density;
                float pressure;
            };

            StructuredBuffer<ParticleData> _Particles;
            float _ParticleScale;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWorld : TEXCOORD1;
            };

            v2f vert(appdata_base v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float3 centre = _Particles[instanceID].position.xyz;
                float3 offset = v.vertex.xyz * _ParticleScale * 2;

                float3 camUp = unity_CameraToWorld._m01_m11_m21;
                float3 camRight = unity_CameraToWorld._m00_m10_m20;
                float3 worldPos = centre + camRight * offset.x + camUp * offset.y;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1));
                o.posWorld = worldPos;
                o.uv = v.texcoord;
                return o;
            }

            float LinearDepthToUnityDepth(float linearDepth)
            {
                float depth01 = (linearDepth - _ProjectionParams.y)
                    / (_ProjectionParams.z - _ProjectionParams.y);
                return (1.0 - depth01 * _ZBufferParams.y) / (depth01 * _ZBufferParams.x);
            }

            float4 frag(v2f i, out float Depth : SV_Depth) : SV_Target
            {
                float2 centreOffset = (i.uv - 0.5) * 2;
                float sqrDst = dot(centreOffset, centreOffset);
                if (sqrDst > 1) discard;

                float z = sqrt(1 - sqrDst);
                float d = abs(mul(unity_MatrixV, float4(i.posWorld, 1)).z);
                float dcam = length(i.posWorld - _WorldSpaceCameraPos);
                float linearDepth = dcam - z * _ParticleScale;
                Depth = LinearDepthToUnityDepth(linearDepth);

                return linearDepth;
            }
            ENDCG
        }
    }
}