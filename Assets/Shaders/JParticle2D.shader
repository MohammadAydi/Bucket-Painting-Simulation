Shader "Instanced/Particle2D"
{

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            StructuredBuffer<float2> Positions2D;
            StructuredBuffer<float2> Velocities;
            float scale;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_full v, uint instanceID : SV_InstanceID)
            {
                float3 centerWorld = float3(Positions2D[instanceID], 0);
                float3 worldVertPos = centerWorld + mul(unity_ObjectToWorld, v.vertex * scale);
                float3 objectVertPos = mul(unity_WorldToObject, float4(worldVertPos.xyz, 1));
                v2f o;
                o.uv = v.texcoord;
                o.pos = UnityObjectToClipPos(objectVertPos);
                return o;
                
            }
            float4 frag (v2f i) : SV_Target
            {
                float2 centerOffset = (i.uv.xy - 0.5) * 2;
                float sqrDSt = dot(centerOffset , centerOffset);
                float delta = fwidth(sqrt(sqrDSt));
                float alpha = 1 - smoothstep(1 - delta , 1 + delta , sqrDSt);
                return float4(0.0f , 0.0f , 0.5f , alpha);
            }
            ENDCG
        }
    }
}