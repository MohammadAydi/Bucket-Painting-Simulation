// HLSL/BilateralPass_URP.hlsl
// ──────────────────────────────────────────────────────────────────────────────
// Shared bilateral filter logic — a URP port of Sebastian's BilateralPass.hlsl.
//
// Changes from the Built-in version:
//   • Include uses Core.hlsl and Blit.hlsl instead of UnityCG.cginc
//   • Texture/sampler declarations use URP macros (TEXTURE2D / SAMPLER)
//   • Matrix access unchanged — UNITY_MATRIX_P._m00 is available in Core.hlsl
//   • unity_CameraInvProjection is available via Core.hlsl as well
//
// The .a channel is ALWAYS preserved as the bilateral edge-stop reference.
// smoothMask controls which of .r / .g / .b get blurred (pass (1,1,0) from C#
// to blur R and G, leave B alone — same as Sebastian).
// ──────────────────────────────────────────────────────────────────────────────

#ifndef BILATERAL_PASS_URP_INCLUDED
#define BILATERAL_PASS_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
float4 _MainTex_TexelSize;   // (1/w, 1/h, w, h)

float  worldRadius;
int    maxScreenSpaceRadius;
float  strength;
float  diffStrength;
float3 smoothMask;

// ── Gaussian kernel weight for a 1-D kernel at offset x ──────────────────────
float Gaussian1D(int x, float sigma)
{
    float c = 2.0 * sigma * sigma;
    return exp(-(float)(x * x) / c);
}

// ── Gaussian kernel weight for a 2-D kernel at offset (x, y) ─────────────────
float Gaussian2D(int x, int y, float sigma)
{
    float c = 2.0 * sigma * sigma;
    return exp(-(float)(x * x + y * y) / c);
}

// ── Screen-space radius from world radius at a given depth ────────────────────
// Thanks to Freya Holmér (as credited in Sebastian's original)
float ScreenSpaceRadius(float depth, int imageWidth)
{
    float widthScale  = UNITY_MATRIX_P._m00;  // smaller = wider FOV
    float pxPerMeter  = (imageWidth * widthScale) / (2.0 * depth);
    return abs(pxPerMeter) * worldRadius;
}

// ── Reconstruct view-space position from UV + linear depth ───────────────────
float3 ViewPos(float2 uv, float depth)
{
    // unity_CameraInvProjection maps clip → view.  Using float4 with w=-1
    // gives the direction vector, which we then scale by depth.
    float3 viewDir = mul(unity_CameraInvProjection, float4(uv * 2.0 - 1.0, 0, -1)).xyz;
    return normalize(viewDir) * depth;
}

// ── 1-D bilateral blur along the given texel direction ───────────────────────
float4 BilateralBlur1D(float2 uv, float2 dir)
{
    float4 centre = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    float  depth  = centre.a;   // .a is always the raw depth reference

    // Compute radius in screen space
    float rFloat  = ScreenSpaceRadius(depth, (int)_MainTex_TexelSize.z);
    int   radius  = (int)ceil(rFloat);
    if (radius <= 1 && worldRadius > 0) radius = 2;
    radius = min(maxScreenSpaceRadius, radius);

    // Fractional radius smooths the discrete jump when the integer changes
    float fR    = max(0.0, radius - rFloat);
    float sigma = max(1e-7, (radius - fR) / (6.0 * max(0.001, strength)));

    float4 sum  = 0.0;
    float  wSum = 0.0;
    float2 step = _MainTex_TexelSize.xy * dir;

    UNITY_LOOP
    for (int x = -radius; x <= radius; x++)
    {
        float  w      = Gaussian1D(x, sigma);
        float2 uv2    = uv + step * (float)x;
        float4 s      = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv2, 0);

        // Bilateral weight: penalise samples that differ in depth from the centre
        float  dDiff  = centre.a - s.a;
        float  dW     = exp(-dDiff * dDiff * diffStrength);

        float  sw     = w * dW;
        sum  += s  * sw;
        wSum += sw;
    }

    if (wSum > 0.0) sum /= wSum;

    // Apply smooth mask: only lerp channels that are set to 1
    // Always preserve .a exactly (depth reference for downstream passes)
    return float4(lerp(centre.rgb, sum.rgb, smoothMask), depth);
}

// ── 2-D bilateral blur (full neighbourhood) ───────────────────────────────────
float4 BilateralBlur2D(float2 uv)
{
    float4 centre = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    float  depth  = centre.a;

    float3 viewP  = ViewPos(uv, depth);
    int    radius = (int)round(ScreenSpaceRadius(depth, (int)_MainTex_TexelSize.z));
    radius = min(maxScreenSpaceRadius, radius);
    float sigma   = max(1e-7, radius * strength);

    float4 sum  = 0.0;
    float  wSum = 0.0;

    UNITY_LOOP
    for (int dx = -radius; dx <= radius; dx++)
    {
        UNITY_LOOP
        for (int dy = -radius; dy <= radius; dy++)
        {
            float2 uv2 = uv + float2(dx, dy) * _MainTex_TexelSize.xy;
            float4 s   = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv2, 0);

            float  w   = Gaussian2D(dx, dy, sigma);
            float  dD  = centre.a - s.a;
            float  dW  = exp(-dD * dD * diffStrength);

            float  sw  = w * dW;
            sum  += s  * sw;
            wSum += sw;
        }
    }

    if (wSum > 0.0) sum /= wSum;

    return float4(lerp(centre.rgb, sum.rgb, smoothMask), depth);
}

#endif // BILATERAL_PASS_URP_INCLUDED
