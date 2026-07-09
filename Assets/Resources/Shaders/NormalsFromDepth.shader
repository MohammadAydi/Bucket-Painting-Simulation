
Shader "Fluid/NormalsFromDepth"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always

        Pass
        {
            Name "FluidNormals"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float3 ViewPos(float2 uv)
            {
                float4 s = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                 float depth = s.r; 

                float3 viewDir = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 0, -1)).xyz;
                return normalize(viewDir) * depth;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float rawDepth = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.texcoord).a;
                if (rawDepth >= 9999999.0)
                    return float4(0, 0, 0, 0);  

                float3 centre = ViewPos(IN.texcoord);
                float2 o      = _MainTex_TexelSize.xy;

                float3 ddxF = ViewPos(IN.texcoord + float2(o.x, 0)) - centre;
                float3 ddxB = centre - ViewPos(IN.texcoord - float2(o.x, 0));
                float3 ddx  = abs(ddxF.z) < abs(ddxB.z) ? ddxF : ddxB;

                float3 ddyF = ViewPos(IN.texcoord + float2(0, o.y)) - centre;
                float3 ddyB = centre - ViewPos(IN.texcoord - float2(0, o.y));
                float3 ddy  = abs(ddyF.z) < abs(ddyB.z) ? ddyF : ddyB;

               float3 viewNormal  = normalize(cross(ddy, ddx));
                float3 worldNormal = mul(UNITY_MATRIX_I_V, float4(viewNormal, 0)).xyz;

                return float4(worldNormal, 1.0);  
            }
            ENDHLSL
        }
    }
}
