Shader "Fluid/FluidComposite_URP"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off  ZWrite Off  ZTest Always

        // ── Pass 0: Shade fluid pixels into outRT ─────────────────────────────
        // _CompTex and _NormalTex are set directly on the material from C# before
        // the draw — no globals needed.
        // Background pixels (compRT.a >= 9999999) → discard → outRT untouched.
        // Fluid pixels → shaded colour + alpha=1.
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

            TEXTURE2D(_CompTex);   SAMPLER(sampler_CompTex);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);

            float4 _PaintColor;
            float  _SpecularStrength;
            float  _Shininess;
            float  _ReflectStrength;

            float3 WorldViewDir(float2 uv)
            {
                float3 viewDir = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 0, -1)).xyz;
                return normalize(mul(UNITY_MATRIX_I_V, float4(viewDir, 0)).xyz);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                float4 comp     = SAMPLE_TEXTURE2D(_CompTex,  sampler_CompTex,  uv);
                float  rawDepth = comp.a;

                // No fluid here — leave outRT unwritten (alpha stays 0)
                if (rawDepth >= 9999999.0)
                    discard;

                float3 N = normalize(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, uv).xyz);

                Light  mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 V = -WorldViewDir(uv);
                float3 H = normalize(L + V);

                float3 paintCol = _PaintColor.rgb;
                float3 lightCol = mainLight.color;

                float3 ambient   = paintCol * 0.08;
                float3 diffuse   = paintCol * lightCol * max(0.0, dot(N, L));
                float3 specular  = lightCol * _SpecularStrength * pow(max(0.0, dot(N, H)), _Shininess);

                float3 R         = reflect(-V, N);
                float  skyBlend  = saturate(R.y * 0.5 + 0.5);
                float3 reflColor = lerp(float3(0.3, 0.25, 0.2), float3(0.5, 0.6, 0.8), skyBlend);
                float  fresnel   = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 reflection = reflColor * _ReflectStrength * (0.2 + 0.8 * fresnel);

                float3 finalColor = ambient + diffuse + specular + reflection;

                return float4(finalColor, 1.0);  // alpha=1 → fluid pixel for blend pass
            }
            ENDHLSL
        }

        // ── Pass 1: Alpha-blend outRT over camera colour ──────────────────────
        // C# calls Blitter.BlitTexture(cmd, outHandle, scaleOffset, material, 1)
        // which sets _BlitTexture = outRT automatically before drawing.
        // Blend: fluid (alpha=1) → fully writes; background (alpha=0) → no-op.
        Pass
        {
            Name "FluidCopyToCamera"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment fragCopy

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 fragCopy(Varyings IN) : SV_Target
            {
                // _BlitTexture is set by Blitter before this draw call
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, IN.texcoord);
            }
            ENDHLSL
        }
    }
}
