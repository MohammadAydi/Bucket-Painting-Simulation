Shader "Custom/BucketWithHoles"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define MAX_HOLES 8

            float4 _BaseColor;
            float _BucketHeight;
            float _BottomRadius;
            float _TopRadius;

            int _HoleCount;
            float _HoleType[MAX_HOLES];
            float _HoleLocation[MAX_HOLES];
            float _HoleRadius[MAX_HOLES];
            float _HoleWidth[MAX_HOLES];
            float _HoleHeight[MAX_HOLES];
            float _HoleAngle[MAX_HOLES];
            float _HoleHeightPos[MAX_HOLES];
            float _HoleOffsetX[MAX_HOLES];
            float _HoleOffsetY[MAX_HOLES];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv1 : TEXCOORD1; // x = surface tag: 0 wall, 1 floor, 2 divider
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float surfaceID : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.surfaceID = IN.uv1.x;
                return OUT;
            }

            bool IsInsideHole(float3 posOS, float surfaceID)
            {
                // Dividers are never cut by any hole
                if (surfaceID > 1.5) return false;

                float angleDeg = degrees(atan2(posOS.z, posOS.x));
                if (angleDeg < 0) angleDeg += 360.0;

                bool isWallSurface = surfaceID < 0.5;

                for (int i = 0; i < _HoleCount; i++)
                {
                    bool isSideHole = _HoleLocation[i] < 0.5;

                    if (isSideHole && isWallSurface)
                    {
                        float angleDelta = abs(angleDeg - _HoleAngle[i]);
                        if (angleDelta > 180.0) angleDelta = 360.0 - angleDelta;

                        float t = saturate(posOS.y / _BucketHeight);
                        float faceRadius = lerp(_BottomRadius, _TopRadius, t);
                        float arcDist = radians(angleDelta) * faceRadius;
                        float heightDist = posOS.y - _HoleHeightPos[i];

                        if (_HoleType[i] < 0.5)
                        {
                            float dist = sqrt(arcDist * arcDist + heightDist * heightDist);
                            if (dist < _HoleRadius[i]) return true;
                        }
                        else
                        {
                            if (arcDist < _HoleWidth[i] * 0.5 && abs(heightDist) < _HoleHeight[i] * 0.5) return true;
                        }
                    }
                    else if (!isSideHole && !isWallSurface)
                    {
                        float dx = posOS.x - _HoleOffsetX[i];
                        float dz = posOS.z - _HoleOffsetY[i];

                        if (_HoleType[i] < 0.5)
                        {
                            if (sqrt(dx * dx + dz * dz) < _HoleRadius[i]) return true;
                        }
                        else
                        {
                            if (abs(dx) < _HoleWidth[i] * 0.5 && abs(dz) < _HoleHeight[i] * 0.5) return true;
                        }
                    }
                }
                return false;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (IsInsideHole(IN.positionOS, IN.surfaceID)) discard;

                Light mainLight = GetMainLight();
                float3 normal = normalize(IN.normalWS);
                float ndotl = saturate(dot(normal, mainLight.direction));
                half3 lighting = _BaseColor.rgb * (ndotl * 0.8 + 0.35);

                return half4(lighting, 1.0);
            }
            ENDHLSL
        }
    }
}