Shader "Fluid/ParticleCircle3D"
{
    Properties
    {
        _ParticleRadius ("Particle Radius", Float) = 0.1
        _VelocityMax ("Velocity Max", Float) = 5.0
    }

    SubShader
    {
      
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        ZWrite On
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            StructuredBuffer<float3> _Position;
            StructuredBuffer<float3> _Velocity;
            Texture2D<float4> _ColourMap;
            SamplerState linear_clamp_sampler;

            float _ParticleRadius;
            float _VelocityMax;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 color : TEXCOORD0;
                float3 normal : NORMAL; 
            };

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                o.normal = v.normal; 

                float3 position = _Position[instanceID];
                float3 velocity = _Velocity[instanceID];

             
                float3 objectVertPos = v.vertex.xyz * _ParticleRadius;
                float4 viewPos = mul(UNITY_MATRIX_V, float4(position, 1.0)) + float4(objectVertPos, 0.0);
                o.pos = mul(UNITY_MATRIX_P, viewPos);

              
                float speed = length(velocity);
                float speedT = saturate(speed / max(_VelocityMax, 0.0001));
                o.color = _ColourMap.SampleLevel(linear_clamp_sampler, float2(speedT, 0.5), 0).rgb;
                
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
               
                float shading = saturate(dot(_WorldSpaceLightPos0.xyz, normalize(i.normal)));
                shading = (shading + 0.6) / 1.4;
                
                return fixed4(i.color * shading, 1.0);
            }
            ENDCG
        }
    }
}