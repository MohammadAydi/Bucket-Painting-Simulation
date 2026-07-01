#ifndef FLUID_MATH_HLSL
#define FLUID_MATH_HLSL

static const float PI = 3.1415926;

// ==============================================================================
// SOLVER MODES
// ==============================================================================
// Each physics term (pressure, viscosity, ...) can have multiple interchangeable
// solver implementations. The active mode is sent from C# as a plain int uniform
// (e.g. _PressureSolverMode) and selected via switch() in the relevant kernel.
//
// These #defines MUST stay in sync with their C# enum counterparts in
// ParticleSettings.cs (the int value of the enum is sent directly as-is).
//
// HOW TO ADD A NEW PRESSURE SOLVER MODE:
//   1. Add a new enum value to ParticleSettings.PressureSolverMode (C#).
//   2. Add a matching #define PRESSURE_MODE_XXX below with the same int value.
//   3. Write a Math_XXX() kernel function (if needed) + a Compute_XXX_PressureForce()
//      contribution function further down in this file.
//   4. Add a `case PRESSURE_MODE_XXX:` branch in CalculatePressureForces in
//      FluidCompute3D.compute that calls your new contribution function.
//   Nothing else needs to change — the spatial hash neighbour-search loop,
//   density buffer layout, and integration step are all solver-mode agnostic.
//
// The same pattern (mode #defines + per-neighbour contribution function +
// switch in the kernel) is intended to be reused for future solver families,
// e.g. VISCOSITY_MODE_STANDARD / VISCOSITY_MODE_XSPH / etc.
// ------------------------------------------------------------------------------

// Pressure solver modes — keep numerically in sync with
// ParticleSettings.PressureSolverMode in ParticleSettings.cs
#define PRESSURE_MODE_STANDARD      0
#define PRESSURE_MODE_NEAR_PRESSURE 1

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

// ─────────────────────────────────────────────────────────────
// SPATIAL HASHING & UTILITIES
// ─────────────────────────────────────────────────────────────
static const uint hashK1 = 15823;
static const uint hashK2 = 9737333;
static const uint hashK3 = 440817757;

int3 PositionToCellCoord(float3 pos, float radius) { return (int3) (floor(pos / radius)); }
uint GetKey(uint hash, uint tableSize) { return hash % tableSize; }

uint HashCell(int3 cell)
{
    const uint blockSize = 50;
    uint3 ucell = (uint3) (cell + blockSize / 2);
    uint3 localCell = ucell % blockSize;
    uint3 blockID = ucell / blockSize;
    uint blockHash = blockID.x * hashK1 + blockID.y * hashK2 + blockID.z * hashK3;
    return localCell.x + blockSize * (localCell.y + blockSize * localCell.z) + blockHash;
}

float DensityToPressure(float density, float targetDensity, float pressureMultiplier)
{
    return (density - targetDensity) * pressureMultiplier;
}

// Near-pressure has no target/rest value: near-density is purely a measure of
// "too closely packed", so any near-density at all produces a (positive,
// purely repulsive) near-pressure. Used by PRESSURE_MODE_NEAR_PRESSURE to
// stop particle clustering that plain density/pressure alone can let through.
float NearDensityToPressure(float nearDensity, float nearPressureMultiplier)
{
    return nearDensity * nearPressureMultiplier;
}

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
    if (dst <= radius) { float v = radius - dst; return - v * K_SpikyPow2Grad; }
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

// ==============================================================================
// PRESSURE SOLVERS (per-neighbour force contributions)
// ==============================================================================
// One function per pressure solver mode. Each takes everything it needs for a
// single (particle, neighbour) pair and returns that neighbour's contribution
// to the particle's pressure force. The kernel's neighbour-search loop stays
// identical no matter how many of these exist — see CalculatePressureForces
// in FluidCompute3D.compute for the switch() that dispatches to them.
// Mass is shared across modes since it's a per-particle property, not part of
// the kernel/solver itself.

// PRESSURE_MODE_STANDARD: classic SPH pressure force using the symmetric
// (Pi + Pj) / 2 averaged-pressure formulation with the spiky-gradient kernel.
float3 ComputeStandardPressureForce(float dst, float3 dir, float pressure, float otherDensity, float otherPressure, float radius, float mass)
{
    float slope = CalculatePressureWeight(dst, radius);
    float sharedPressure = (pressure + otherPressure) * 0.5;
    return dir * (sharedPressure * slope * mass) / otherDensity;
}

// PRESSURE_MODE_NEAR_PRESSURE adds this on top of ComputeStandardPressureForce:
// a second, steeper pressure term driven by "near density" (a cubic kernel
// that spikes hard at short range). This produces strong, purely-repulsive
// short-range forces that keep particles from clumping/overlapping in dense
// regions — something the standard pressure term alone tends to under-resolve.
float3 ComputeNearPressureForce(float dst, float3 dir, float nearPressure, float otherNearDensity, float otherNearPressure, float radius, float mass)
{
    float safeOtherNearDensity = otherNearDensity;
    float nearSlope = CalculateNearPressureWeight(dst, radius);
    float sharedNearPressure = (nearPressure + otherNearPressure) * 0.5;
    return dir * (sharedNearPressure * nearSlope * mass) / safeOtherNearDensity;
}

#endif // FLUID_MATH_HLSL