Shader "Fluid/OldDensityField"
{
    Properties
    {
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
                float volume = UNITY_PI * pow(radius, 4) / 6.0;
                return (radius - dst) * (radius - dst) / volume;
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
            
            fixed4 frag(v2f i) : SV_Target
            {
                float density = CalculateDensity(i.worldPos);
                return DensityToColor(density);
            }
            ENDCG
        }
    }
}