#ifndef FLUID_MATH_HLSL
#define FLUID_MATH_HLSL

static const float PI = 3.1415926;

// Kernel normalization constants
float K_Poly6;
float K_SpikyGradient;
float K_ViscosityLaplacian;
float K_Poly6Gradient;
float K_Poly6Laplacian;
float K_CubicSpline;

// Custom kernels
float K_SpikyPow2;
float K_SpikyPow3;
float K_SpikyPow2Grad;
float K_SpikyPow3Grad;

// ==============================================================================
// TYPE 2: RAW MATHEMATICAL SMOOTHING KERNELS (The underlying equations)
// ==============================================================================

// Your Original Poly6 (Used for Density originally)
float Math_Poly6(float dst, float radius)
{
    if (dst >= radius) return 0.0;

        float diff = radius * radius - dst * dst;
    return diff * diff * diff * K_Poly6;
}

// Your Original Spiky Gradient (Used for Pressure originally)
float Math_SpikyGradient(float dst, float radius)
{
    if (dst >= radius) return 0.0;

        float diff = radius - dst;
    return diff * diff * K_SpikyGradient;
}

// Your Original Viscosity Laplacian
float Math_ViscosityLaplacian(float dst, float radius)
{
    if (dst >= radius) return 0.0;

        return K_ViscosityLaplacian * (radius - dst);
}

// Your Original Surface Tension Gradient (Poly6)
float Math_Poly6Gradient3D(float dst, float sqrDst, float sqrH, float radius)
{
    if (sqrDst >= sqrH) return 0.0;

        float diff = sqrH - sqrDst;
    return K_Poly6Gradient * dst * diff * diff;
}


// Your Original Surface Tension Laplacian (Poly6)
float Math_Poly6Laplacian3D(float sqrDst, float sqrH, float radius)
{
    if (sqrDst >= sqrH) return 0.0;

        float diff = sqrH - sqrDst;
    return K_Poly6Laplacian * diff * (3.0 * sqrH - 7.0 * sqrDst);
}

//General Kernel, Monaghan Cubic Spline Kernel (support radius = h)
float Math_CubicSpline(float dst, float radius)
{
    if (dst >= radius)
        return 0.0;

    float q = dst / radius;

    if (q <= 0.5)
    {
        return K_CubicSpline * 
        (6.0 * q * q * q - 6.0 * q * q + 1.0);
    }

    float t = 1.0 - q;
    return K_CubicSpline * (2.0 * t * t * t);
}

float Math_CubicSplineGradient(float dst, float radius)
{
    if (dst >= radius || dst <= 1e-6)
        return 0.0;

    float q = dst / radius;

    if (q <= 0.5)
    {
        // dW/dr = σ/h * (18q² - 12q)
        return (K_CubicSpline / radius) * 
        (18.0 * q * q - 12.0 * q);
    }

    float t = 1.0 - q;

    // dW/dr = -6σ/h (1-q)²
    return - (6.0 * K_CubicSpline / radius) * 
    (t * t);
}

// --- Your New Custom Kernels (Stored here for when you want to use them) ---
float Math_SpikyKernelPow3(float dst, float radius) {
    if (dst < radius) { float v = radius - dst; return v * v * v * K_SpikyPow3; }
    return 0;
}
float Math_SpikyKernelPow2(float dst, float radius) {
    if (dst < radius) { float v = radius - dst; return v * v * K_SpikyPow2; }
    return 0;
}
float Math_DerivativeSpikyPow3(float dst, float radius) {
    if (dst <= radius) { float v = radius - dst; return - v * v * K_SpikyPow3Grad; }
    return 0;
}
float Math_DerivativeSpikyPow2(float dst, float radius) {
    if (dst <= radius) { float v = radius - dst; return -v * K_SpikyPow2Grad; }
    return 0;
}


// ==============================================================================
// TYPE 1: PHYSICS PROPERTY WRAPPERS (Used directly in FluidCompute3D.compute)
// ==============================================================================
// Note: These retain your exact original estimations. If you want to change
// how density is calculated later, just change the function this calls.

float CalculateDensityWeight(float dst, float radius)
{
    return Math_SpikyKernelPow2(dst, radius);
}

// Near-density uses a steeper (cubic) kernel than regular density so it grows
// sharply as particles get very close — this is what gives near-pressure its
// "emergency" repulsion behaviour right at contact distance.
float CalculateNearDensityWeight(float dst, float radius)
{
    return Math_SpikyKernelPow3(dst, radius);
}

float CalculatePressureWeight(float dst, float radius)
{
    return Math_DerivativeSpikyPow2(dst, radius);
}

// Gradient counterpart of CalculateNearDensityWeight, used to turn near-pressure
// into a force in CalculatePressureForces.
float CalculateNearPressureWeight(float dst, float radius)
{
    return Math_DerivativeSpikyPow3(dst, radius);
}

float CalculateViscosityWeight(float dst, float radius)
{
    return Math_ViscosityLaplacian(dst, radius);
}

float CalculateSurfaceTensionGradientWeight(float dst, float sqrDst, float sqrH, float radius)
{
    return Math_Poly6Gradient3D(dst, sqrDst, sqrH, radius);
}

float CalculateSurfaceTensionLaplacianWeight(float sqrDst, float sqrH, float radius)
{
    return Math_Poly6Laplacian3D(sqrDst, sqrH, radius);
}
#endif // FLUID_MATH_HLSL