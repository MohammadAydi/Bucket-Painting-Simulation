Shader "Fluid/FluidComposite"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off ZWrite Off ZTest Always

       
        Pass
        {
            Name "FluidComposite"
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_CompTex);
            SAMPLER(sampler_CompTex);
            TEXTURE2D(_NormalTex);
            SAMPLER(sampler_NormalTex);
          

            TEXTURE2D(_PigmentColorTex);
            SAMPLER(sampler_PigmentColorTex);

            
            float _SpecularStrength;
            float _Shininess;
            float _ReflectStrength;

         
            float _AmbientStrength;
            float _UseHalfLambert; 
            float _FillLightStrength;
            float3 _FillLightColor; 

            float3 WorldViewDir(float2 uv)
            {
                float3 viewDir = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 0, -1)).xyz;
                return normalize(mul(UNITY_MATRIX_I_V, float4(viewDir, 0)).xyz);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                float4 comp = SAMPLE_TEXTURE2D(_CompTex, sampler_CompTex, uv);
                float rawDepth = comp.a;

                if (rawDepth >= 9999999.0)
                    discard;

                float3 N = normalize(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv).xyz);

                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 V = -WorldViewDir(uv);
                float3 H = normalize(L + V);

               float4 pigmentSample = SAMPLE_TEXTURE2D(_PigmentColorTex, sampler_PigmentColorTex, uv);
                float3 paintCol = pigmentSample.rgb;
                float3 lightCol = mainLight.color;

                float3 ambient = paintCol * _AmbientStrength;

                float NdotL = dot(N, L);
                float diffTerm = _UseHalfLambert > 0.5
                             ? NdotL * 0.5 + 0.5 
                             : max(0.0, NdotL);
                float3 diffuse = paintCol * lightCol * diffTerm;

                float3 fillDir = float3(0, 1, 0);
                float fillDot = max(0.0, dot(N, fillDir));
                float3 fill = paintCol * _FillLightColor * fillDot * _FillLightStrength;

                 float3 specular = lightCol * _SpecularStrength
                    * pow(max(0.0, dot(N, H)), _Shininess);

               float3 R = reflect(-V, N);
                float skyBlend = saturate(R.y * 0.5 + 0.5);
                float3 reflColor = lerp(float3(0.3, 0.25, 0.2), float3(0.5, 0.6, 0.8), skyBlend);
                float fresnel = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 reflection = reflColor * _ReflectStrength * (0.2 + 0.8 * fresnel);

                float3 finalColor = ambient + diffuse + fill + specular + reflection;

                return float4(finalColor, 1.0);
            }
            ENDHLSL
        }

         Pass
        {
            Name "FluidCopyToCamera"
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragCopy
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_CompTex);
            SAMPLER(sampler_CompTex);

            float4 fragCopy(Varyings IN) : SV_Target
            {
                float rawDepth = SAMPLE_TEXTURE2D(_CompTex, sampler_CompTex, IN.texcoord).a;
                if (rawDepth >= 9999999.0) discard;

                float sceneDeviceDepth = SampleSceneDepth(IN.texcoord);
                float sceneEyeDepth = LinearEyeDepth(sceneDeviceDepth, _ZBufferParams);

                 const float bias = 0.02; 
                if (sceneEyeDepth < rawDepth - bias) discard;

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, IN.texcoord);
            }
            ENDHLSL
        }
    }
}