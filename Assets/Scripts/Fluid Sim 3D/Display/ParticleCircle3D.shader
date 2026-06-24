Shader "Fluid/ParticleCircle3D"
{
    Properties
    {
        _ParticleRadius ("Particle Radius", Float) = 0.1
        _VelocityMax    ("Velocity Max",    Float) = 5.0
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        ZWrite On
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma target   4.5
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // ── Particle data layout must match ParticleData3D struct ──────────
            struct ParticleData
            {
                float4 position;
                float4 velocity;
                float4 force;
                float  density;
                float  pressure;
            };

            StructuredBuffer<ParticleData> _Particles;

            Texture2D<float4>  _ColourMap;
            SamplerState       linear_clamp_sampler;

            float _ParticleRadius;
            float _VelocityMax;

            // ── V2F ──────────────────────────────────────────────────────────
            struct v2f
            {
                float4 pos    : SV_POSITION;
                float3 color  : TEXCOORD0;
                float3 normal : TEXCOORD1;  // world-space normal for diffuse shading
            };

            // ── Vertex shader ─────────────────────────────────────────────────
            // Each instance is one particle. The mesh is a unit sphere, so we
            // scale every vertex by _ParticleRadius and offset it to world space.
            v2f vert(appdata_full v, uint instanceID : SV_InstanceID)
            {
                ParticleData p = _Particles[instanceID];

                // Place scaled sphere vertex in world space
                float3 worldPos = p.position.xyz + mul(unity_ObjectToWorld, v.vertex * _ParticleRadius).xyz;
                float3 objPos   = mul(unity_WorldToObject, float4(worldPos, 1)).xyz;

                // Velocity → colour via gradient texture
                float speed  = length(p.velocity.xyz);
                float speedT = saturate(speed / max(_VelocityMax, 0.0001));
                float3 col   = _ColourMap.SampleLevel(linear_clamp_sampler, float2(speedT, 0.5), 0).rgb;

                v2f o;
                o.pos    = UnityObjectToClipPos(objPos);
                o.color  = col;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            // ── Fragment shader ───────────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                // Simple diffuse: dot(normal, light direction)
                // The +0.6/1.4 lift prevents the shadow side going pitch black,
                // giving the same soft ambient look as the instructor's shader.
                float shading = saturate(dot(_WorldSpaceLightPos0.xyz, normalize(i.normal)));
                shading = (shading + 0.6) / 1.4;
                return fixed4(i.color * shading, 1.0);
            }

            ENDCG
        }
    }
}
