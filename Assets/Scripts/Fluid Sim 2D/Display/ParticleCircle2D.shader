Shader "Fluid/ParticleCircle2D"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _ParticleRadius ("Particle Radius", Float) = 0.1
        
        // Velocity Mapping Properties
        _UseSpeedColor ("Use Speed Color", Float) = 0
        _VelocityMax ("Velocity Max", Float) = 5
        _GradientTex ("Gradient Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct ParticleData
            {
                float2 position;
                float2 predictedPosition; 
                float2 velocity;
                float2 force;
                float density;
                float pressure;
            };

            StructuredBuffer<ParticleData> _Particles;
            fixed4 _Color;
            float _ParticleRadius;

            // Velocity uniform properties
            float _UseSpeedColor;
            float _VelocityMax;
            sampler2D _GradientTex;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 localPos : TEXCOORD0;
                float speedT : TEXCOORD1; // Pass structural speed ratio down to fragment pass
            };

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                ParticleData particle = _Particles[instanceID];
                float2 worldPos = particle.position + v.vertex.xy * _ParticleRadius;

                v2f o;
                o.pos = UnityWorldToClipPos(float4(worldPos, 0.0, 1.0));
                o.localPos = v.vertex.xy;

                // Process magnitude speed metric
                float speed = length(particle.velocity);
                o.speedT = saturate(speed / _VelocityMax);

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(1.0 - length(i.localPos));

                fixed4 finalColor = _Color;

                // Dynamic selector switch check
                if (_UseSpeedColor > 0.5)
                {
                    finalColor = tex2D(_GradientTex, float2(i.speedT, 0.5));
                }

                return finalColor;
            }
            ENDCG
        }
    }
}