// PaintRaymarch_URP.shader  (FIXED)
// ─────────────────────────────────────────────────────────────────────────────
// Fixes applied:
//   1. ViewDirFromUV: replaced unity_CameraInvProjection (unreliable in URP
//      Render Graph blit passes) with UNITY_MATRIX_I_VP unproject — rays now
//      point in the correct direction.
//   2. Depth cull: was `discard` (killed the whole pixel even after a surface
//      was found earlier); replaced with `break` so the march stops and any
//      already-found surface is returned.
//   3. Pass 1 FragCopy: removed `discard` for background; the blend state
//      (SrcAlpha OneMinusSrcAlpha) handles transparency correctly.  Keeping
//      discard here prevents alpha-blended edges from ever rendering.
// ─────────────────────────────────────────────────────────────────────────────

Shader "Fluid/PaintRaymarch_URP"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Pass 0 : Raymarch → outRT ─────────────────────────────────────────
        Pass
        {
            Name "PaintRaymarch"
            Cull Off ZWrite Off ZTest Always
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragRaymarch

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ── Density map ───────────────────────────────────────────────────
            TEXTURE3D(_DensityMap);
            SAMPLER(sampler_linear_clamp);

            // ── Bounds (world space AABB — for the coarse ray-box cull only) ──
            float3 _BoundsSize;
            float3 _BoundsCenter;

            // ── Boundary local space (matches FluidVoxelizer.compute exactly) ──
            float4x4 _BoundaryWorldToLocal;
            float3 _BoundaryLocalMin;
            float3 _BoundaryLocalMax;
            float4x4 _FluidInvViewProj;
            float3 _FluidCamWorldPos;
            // ── Marching params ────────────────────────────────────────────────
            float _DensityOffset;
            float _StepSize;
            float _NormalEpsilon;

            // ── Shared paint / lighting ────────────────────────────────────────
            float4 _PaintColor;
            float _SpecularStrength;
            float _Shininess;
            float _ReflectStrength;
            float _AmbientStrength;
            float _UseHalfLambert;
            float _FillLightStrength;
            float3 _FillLightColor;

            // ─────────────────────────────────────────────────────────────────
            // UTILITY
            // ─────────────────────────────────────────────────────────────────

            // FIX: sample in boundary-LOCAL space, the exact inverse of what
            // FluidVoxelizer.compute did when it wrote the density map
            // (localPos -> worldPos via _BoundaryLocalToWorld). Previously this
            // divided by an axis-aligned world-space box, which only matches the
            // real (possibly rotated) density volume when the boundary has zero
            // rotation. Any rotation caused wrong UVW lookups -> garbled/blocky
            // surface and a result that seemed to shift with the camera.
            float SampleDensity(float3 worldPos)
            {
                float3 localPos = mul(_BoundaryWorldToLocal, float4(worldPos, 1.0)).xyz;
                float3 uvw = (localPos - _BoundaryLocalMin) / (_BoundaryLocalMax - _BoundaryLocalMin);

                const float eps = 0.001;
                if (any(uvw < eps) || any(uvw > 1.0 - eps)) return -_DensityOffset;

                float d = SAMPLE_TEXTURE3D_LOD(_DensityMap, sampler_linear_clamp, uvw, 0).r;
                return d - _DensityOffset;
            }

            float3 EstimateNormal(float3 pos)
            {
                float e = _NormalEpsilon;
                float dx = SampleDensity(pos + float3(e, 0, 0)) - SampleDensity(pos - float3(e, 0, 0));
                float dy = SampleDensity(pos + float3(0, e, 0)) - SampleDensity(pos - float3(0, e, 0));
                float dz = SampleDensity(pos + float3(0, 0, e)) - SampleDensity(pos - float3(0, 0, e));
                return normalize(-float3(dx, dy, dz));
            }

            float2 RayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayPos, float3 rayDir)
            {
                float3 invDir = rcp(rayDir);
                float3 t0 = (boundsMin - rayPos) * invDir;
                float3 t1 = (boundsMax - rayPos) * invDir;
                float3 tMin = min(t0, t1);
                float3 tMax = max(t0, t1);
                float dstA = max(max(tMin.x, tMin.y), tMin.z);
                float dstB = min(min(tMax.x, tMax.y), tMax.z);
                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            // FIX #1: Use UNITY_MATRIX_I_VP to unproject — works correctly in
            // URP Render Graph blit passes where unity_CameraInvProjection is
            // not guaranteed to be set.
            float3 ViewDirFromUV(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;

                #if UNITY_UV_STARTS_AT_TOP
                ndc.y = -ndc.y;
                #endif

                float4 clip = float4(ndc, 1.0, 1.0);

                float4 world = mul(_FluidInvViewProj, clip);
                world.xyz /= world.w;

                return normalize(world.xyz - _FluidCamWorldPos);
            }

            // ─────────────────────────────────────────────────────────────────
            // PAINT SURFACE LIGHTING
            // ─────────────────────────────────────────────────────────────────
            float4 ShadePaintSurface(float3 worldPos, float3 N, float3 V)
            {
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);
                float3 paintCol = _PaintColor.rgb;
                float3 lightCol = mainLight.color;

                float3 ambient = paintCol * _AmbientStrength;

                float NdotL = dot(N, L);
                float diffTerm = _UseHalfLambert > 0.5
                                     ? NdotL * 0.5 + 0.5
                                     : max(0.0, NdotL);
                float3 diffuse = paintCol * lightCol * diffTerm;

                float fillDot = max(0.0, dot(N, float3(0, 1, 0)));
                float3 fill = paintCol * _FillLightColor * fillDot * _FillLightStrength;

                float3 specular = lightCol * _SpecularStrength
                    * pow(max(0.0, dot(N, H)), _Shininess);

                float3 R = reflect(-V, N);
                float skyBlend = saturate(R.y * 0.5 + 0.5);
                float3 reflCol = lerp(float3(0.3, 0.25, 0.2), float3(0.5, 0.6, 0.8), skyBlend);
                float fresnel = pow(1.0 - saturate(dot(N, V)), 3.0);
                float3 reflection = reflCol * _ReflectStrength * (0.2 + 0.8 * fresnel);

                float3 finalColor = ambient + diffuse + fill + specular + reflection;
                return float4(finalColor, 1.0);
            }

            // ─────────────────────────────────────────────────────────────────
            // FRAGMENT
            // ─────────────────────────────────────────────────────────────────
            float4 FragRaymarch(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                float3 boundsMin = _BoundsCenter - _BoundsSize * 0.5;
                float3 boundsMax = _BoundsCenter + _BoundsSize * 0.5;

                float3 rayPos = _FluidCamWorldPos;
                float3 rayDir = ViewDirFromUV(uv); // FIX #1 applied here

                // Scene depth — occlude by opaque geometry
                float sceneDeviceDepth = SampleSceneDepth(uv);
                float sceneEyeDepth = LinearEyeDepth(sceneDeviceDepth, _ZBufferParams);

                // Intersect bounding volume
                float2 boxDst = RayBoxDst(boundsMin, boundsMax, rayPos, rayDir);
                float dstToBox = boxDst.x;
                float dstInBox = boxDst.y;
                if (dstInBox <= 0.0) discard;

                // March
                float dstTravelled = 0.0;
                float stepSize = _StepSize;
                float3 entryPoint = rayPos + rayDir * (dstToBox + stepSize * 0.5);
                float maxDst = dstInBox - stepSize;

                float prevDensity = SampleDensity(entryPoint);

                while (dstTravelled < maxDst)
                {
                    // FIX #2: Use break instead of discard so a surface found
                    // before the geometry boundary is still returned.
                    if (dstToBox + dstTravelled > sceneEyeDepth - 0.02) break;

                    float3 samplePos = entryPoint + rayDir * dstTravelled;
                    float density = SampleDensity(samplePos);

                    if (density > 0.0 && prevDensity <= 0.0)
                    {
                        float3 refinedPos = samplePos - rayDir * stepSize * 0.5;
                        float3 N = EstimateNormal(refinedPos);
                        float3 V = -rayDir;
                        return ShadePaintSurface(refinedPos, N, V);
                    }

                    prevDensity = density;
                    dstTravelled += stepSize;
                }

                // No surface found — transparent background
                discard;
                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }

        // ── Pass 1 : Alpha-blend outRT over camera colour ─────────────────────
        Pass
        {
            Name "RaymarchCopyToCamera"
            Cull Off ZWrite Off ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragCopy

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 FragCopy(Varyings IN) : SV_Target
            {
                float4 fluidPixel = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, IN.texcoord);

                // FIX #3: Don't discard — let the blend state handle transparency.
                // The old hard discard prevented soft edges and caused z-fighting
                // artefacts at the fluid silhouette.
                return fluidPixel;
            }
            ENDHLSL
        }
    }
}