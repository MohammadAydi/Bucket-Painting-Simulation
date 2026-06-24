Shader "Fluid/DensityField3D"
{
    Properties
    {
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Opaque"
        }

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
                float4 position;
                float4 velocity;
                float4 force;
                float density;
                float pressure;
            };

            StructuredBuffer<ParticleData> _Particles;
            int _ParticleCount;
            float _Mass;
            float _SmoothingRadius;
            float _TargetDensity;
            fixed4 _LowColor;
            fixed4 _TargetColor;
            fixed4 _HighColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float SmoothingKernel(float radius, float dst)
            {
                if (dst >= radius)
                    return 0.0;

                float diff = radius * radius - dst * dst;
                float volume = 315.0 / (64.0 * UNITY_PI * pow(radius, 9));
                return diff * diff * diff * volume;
            }

            float CalculateDensity(float3 samplePoint)
            {
                float density = 0.0;

                for (int i = 0; i < _ParticleCount; i++)
                {
                    float dst = distance(_Particles[i].position.xyz, samplePoint);
                    density += _Mass * SmoothingKernel(_SmoothingRadius, dst);
                }

                return density;
            }

            fixed4 DensityToColor(float density)
            {
                if (density < _TargetDensity)
                {
                    float t = saturate(density / _TargetDensity);
                    return lerp(_LowColor, _TargetColor, t);
                }

                float t = saturate((density - _TargetDensity) / _TargetDensity);
                return lerp(_TargetColor, _HighColor, t);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float density = CalculateDensity(i.worldPos);
                return DensityToColor(density);
            }
            ENDCG
        }
    }
}