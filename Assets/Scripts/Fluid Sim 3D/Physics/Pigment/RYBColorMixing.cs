using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// RYBColorMixing
// From-scratch RYB (Red-Yellow-Blue) pigment color model — no external library.
//
// Mirrors the constants/formula in Physics/Pigment/ColorMixing.hlsl exactly;
// keep both in sync if you tweak the corner colors.
//
// RYBtoRGB: closed-form trilinear interpolation of the 8 RYB cube corners
//           (same formula as the shader).
// RGBtoRYB: no closed-form inverse exists for this cube, so we solve it
//           numerically with damped Newton iterations + a finite-difference
//           Jacobian. Only ever called once per spawn color (a handful of
//           times total), so performance is a non-issue.
// ─────────────────────────────────────────────────────────────────────────────
public static class RYBColorMixing
{
    static readonly Vector3 White  = new Vector3(1.000f, 1.000f, 1.000f);
    static readonly Vector3 Red    = new Vector3(1.000f, 0.000f, 0.000f);
    static readonly Vector3 Yellow = new Vector3(1.000f, 1.000f, 0.000f);
    static readonly Vector3 Blue   = new Vector3(0.163f, 0.373f, 0.600f);
    static readonly Vector3 Orange = new Vector3(1.000f, 0.500f, 0.000f);
    static readonly Vector3 Violet = new Vector3(0.500f, 0.000f, 0.500f);
    static readonly Vector3 Green  = new Vector3(0.000f, 0.660f, 0.200f);
    static readonly Vector3 Black  = new Vector3(0.200f, 0.094f, 0.000f);

    public static Vector3 RYBtoRGB(Vector3 ryb)
    {
        float r = Mathf.Clamp01(ryb.x);
        float y = Mathf.Clamp01(ryb.y);
        float b = Mathf.Clamp01(ryb.z);

        return
            White  * ((1 - r) * (1 - y) * (1 - b)) +
            Red    * (     r  * (1 - y) * (1 - b)) +
            Yellow * ((1 - r) *      y  * (1 - b)) +
            Blue   * ((1 - r) * (1 - y) *      b ) +
            Orange * (     r  *      y  * (1 - b)) +
            Violet * (     r  * (1 - y) *      b ) +
            Green  * ((1 - r) *      y  *      b ) +
            Black  * (     r  *      y  *      b );
    }

    // Finds the (r, y, b) in [0,1]^3 whose RYBtoRGB() best matches targetColor.
    public static Vector3 RGBtoRYB(Color targetColor, int iterations = 40)
    {
        Vector3 target = new Vector3(targetColor.r, targetColor.g, targetColor.b);

        // Start near the white corner and let Newton's method walk downhill.
        Vector3 x = new Vector3(0.001f, 0.001f, 0.001f);
        const float h = 1e-3f; // finite-difference step for the Jacobian

        for (int iter = 0; iter < iterations; iter++)
        {
            Vector3 residual = RYBtoRGB(x) - target;
            if (residual.sqrMagnitude < 1e-8f) break;

            // Finite-difference Jacobian: how RGB changes per RYB axis.
            Vector3 dR = (RYBtoRGB(x + new Vector3(h, 0, 0)) - RYBtoRGB(x - new Vector3(h, 0, 0))) / (2 * h);
            Vector3 dY = (RYBtoRGB(x + new Vector3(0, h, 0)) - RYBtoRGB(x - new Vector3(0, h, 0))) / (2 * h);
            Vector3 dB = (RYBtoRGB(x + new Vector3(0, 0, h)) - RYBtoRGB(x - new Vector3(0, 0, h))) / (2 * h);

            // Solve J * delta = -residual for delta (3x3 system), with a small
            // damping term (Levenberg-Marquardt-lite) so it stays stable near
            // the cube edges where the Jacobian can get close to singular.
            Matrix4x4 J = Matrix4x4.identity;
            J.SetColumn(0, new Vector4(dR.x, dR.y, dR.z, 0));
            J.SetColumn(1, new Vector4(dY.x, dY.y, dY.z, 0));
            J.SetColumn(2, new Vector4(dB.x, dB.y, dB.z, 0));
            J[0, 0] += 1e-4f; J[1, 1] += 1e-4f; J[2, 2] += 1e-4f;

            Vector4 delta4 = J.inverse * new Vector4(-residual.x, -residual.y, -residual.z, 0);
            x += new Vector3(delta4.x, delta4.y, delta4.z);
            x = new Vector3(Mathf.Clamp01(x.x), Mathf.Clamp01(x.y), Mathf.Clamp01(x.z));
        }

        return x;
    }
}
