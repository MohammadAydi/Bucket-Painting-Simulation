Shader "Fluid/ParticleCircle3D"
{
    Properties
    {
        _ParticleRadius ("Particle Radius", Float) = 0.1
        _VelocityMax ("Velocity Max", Float) = 5.0
    }

    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        ZWrite On
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // ── Particle data layout ─────────────────────────────────────────
            // struct ParticleData
            // {
            //     float4 position;
            //     float4 predictedPosition;
            //     float4 velocity;
            //     float4 force;
            //     float density;
            //     float pressure;
            // };
            //
            // StructuredBuffer<ParticleData> _Particles;
            
            StructuredBuffer<float3> _Position;
            StructuredBuffer<float3> _Velocity;
            Texture2D<float4> _ColourMap;
            SamplerState linear_clamp_sampler;

            float _ParticleRadius;
            float _VelocityMax;

            // ── Structs ──────────────────────────────────────────────────────
            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 color : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            // ── Vertex shader ─────────────────────────────────────────────────
            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                float3 position = _Position[instanceID];
                float3 velocity = _Velocity[instanceID];

                // Our C# mesh vertices go from -1 to 1, exactly what we need
                float2 quadOffset = v.vertex.xy;

                // Transform particle center to view space
                float4 viewPos = mul(UNITY_MATRIX_V, float4(position, 1.0));
                
                // Add the billboard offset in view space
                viewPos.xy += quadOffset * _ParticleRadius;

                v2f o;
                o.pos = mul(UNITY_MATRIX_P, viewPos);

                // Velocity colouring
                float speed = length(velocity);
                float speedT = saturate(speed / max(_VelocityMax, 0.0001));
                o.color = _ColourMap.SampleLevel(linear_clamp_sampler, float2(speedT, 0.5), 0).rgb;
                
                o.uv = quadOffset; 
                return o;
            }

            // ── Fragment shader ───────────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                // Circular alpha mask
                float distSq = dot(i.uv, i.uv);
                if (distSq > 1.0) 
                    discard; 

                // Calculate fake Z to reconstruct a spherical normal
                float z = sqrt(1.0 - distSq);
                
                // Construct the normal in view space, then convert to world space
                float3 viewNormal = float3(i.uv.x, i.uv.y, z);
                float3 worldNormal = mul((float3x3)UNITY_MATRIX_I_V, viewNormal);

                // Diffuse shading
                float shading = saturate(dot(_WorldSpaceLightPos0.xyz, normalize(worldNormal)));
                shading = (shading + 0.6) / 1.4;
                
                return fixed4(i.color * shading, 1.0);
            }

            ENDCG
        }
    }
}