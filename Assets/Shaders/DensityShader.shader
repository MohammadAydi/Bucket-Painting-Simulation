Shader "Fluid/DensityField"
{
    Properties
    {
        _TargetDensity ("Target Density", Float) = 2.0
        _PressureMultiplier ("Pressure Multiplier" , Float) = 2.0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Background" "RenderType"="Opaque"
        }
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // particle data passed from C#
            float2 _Positions[700];
            float _ParticleProperties[700];
            float _Densities[500];

            int _ParticleCount;
            float _Mass;
            float _SmoothingRadius;
            float _TargetDensity;
            float _PressureMultiplier;
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
                float2 worldPos : TEXCOORD0;
            };

            // bounds passed from C# to convert UV → world position
            float2 _BoundsMin;
            float2 _BoundsMax;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // convert UV (0..1) to world space using bounds
                o.worldPos = lerp(_BoundsMin, _BoundsMax, v.uv);
                return o;
            }


            float SmoothingKernel(float radius, float dst)
            {
                if (dst >= radius) return 0.0;
                float volume = UNITY_PI * pow(radius, 8) / 4.0;
                float value = radius * radius - dst * dst;
                return (value * value * value) / volume;
            }

            float SmoothingKernelDerivative(float dst, float radius)
            {
                if (dst >= radius) return 0;
                float f = radius * radius - dst * dst;
                float scale = -24 / (UNITY_PI * pow(radius, 8));
                return scale * dst * f * f;
            }


            float CalculateDensity(float2 samplePoint)
            {
                float density = 0.0;
                for (int i = 0; i < _ParticleCount; i++)
                {
                    float dst = length(_Positions[i] - samplePoint);
                    float influence = SmoothingKernel(_SmoothingRadius, dst);
                    density += influence; // mass = 1
                }
                return density;
                return cos(samplePoint.y - 3 + sin(samplePoint.x));
            }

            fixed4 DensityToColor(float density)
            {
                if (density < _TargetDensity)
                {
                    float t = density / _TargetDensity;
                    return lerp(_LowColor, _TargetColor, t);
                }
                else
                {
                    float t = saturate((density - _TargetDensity) / _TargetDensity);
                    return lerp(_TargetColor, _HighColor, t);
                }
            }

            float CalculateProperty(float2 samplePoint)
            {
                float property = 0;
                for (int i = 0; i < _ParticleCount; i++)
                {
                    float dst = length(_Positions[i] - samplePoint);
                    float influence = SmoothingKernel(_SmoothingRadius, dst);
                    float density = _Densities[i]; // pre-computed, not recalculated
                    property += _ParticleProperties[i] * influence * _Mass / density;
                }
                return property;
            }

            float ConvertDensityToPressure(float density)
            {
                float densityError = density - _TargetDensity;
                float pressure = densityError * _PressureMultiplier;
                return pressure;
            }
            
            float2 CalculatePressureForce (float2 samplePoint)
            {
                float2 pressureForce = float2(0, 0);
                for (int i = 0; i < _ParticleCount; i++)
                {
                    float dst = length(_Positions[i] - samplePoint);
                    float2 dir = (_Positions[i] - samplePoint) / dst;
                    float slope = SmoothingKernelDerivative(dst, _SmoothingRadius);
                    float density = _Densities[i];
                    pressureForce += -ConvertDensityToPressure(density) * dir * slope * _Mass / density; // mass = 1
                }
                return pressureForce;
                // const float stepSize = 0.001f;
                // float deltaX = CalculateProperty(samplePoint + float2(0, 1) * stepSize) - CalculateProperty(samplePoint);
                // float deltaY = CalculateProperty(samplePoint + float2(1, 0) * stepSize) - CalculateProperty(samplePoint);
                //
                // float2 gradient = float2(deltaX,deltaY) / stepSize;
                // return gradient;
            }


            // fixed4 frag(v2f i) : SV_Target
            // {
            //     float property = CalculateProperty(i.worldPos);
            //     return DensityToColor(property);
            // }

            fixed4 frag(v2f i) : SV_Target
            {
                float density = CalculateDensity(i.worldPos);
                return DensityToColor(density);
            }
            ENDCG
        }
    }
}