#ifndef FLUID_MATH_HLSL
#define FLUID_MATH_HLSL

static const float PI = 3.1415926;

// Kernel normalization constants
float K_Poly6;
float K_SpikyGradient;
float K_ViscosityLaplacian;
float K_Poly6Gradient;
float K_Poly6Laplacian;

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

int3 PositionToCellCoord(float3 pos, float radius) { return int3(floor(pos / radius)); }
uint GetKey(uint hash, uint tableSize) { return hash % tableSize; }

uint HashCell(int3 cell)
{
    const uint blockSize = 50;
    uint3 ucell = (cell + blockSize / 2);
    uint3 localCell = ucell % blockSize;
    uint3 blockID = ucell / blockSize;
    uint blockHash = blockID.x * hashK1 + blockID.y * hashK2 + blockID.z * hashK3;
    return localCell.x + blockSize * (localCell.y + blockSize * localCell.z) + blockHash;
}

float DensityToPressure(float density, float targetDensity, float pressureMultiplier)
{
    return (density - targetDensity) * pressureMultiplier;
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
    if (dst <= radius) { float v = radius - dst; return -v * v * K_SpikyPow3Grad; }
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
    return Math_Poly6(dst, radius); 
}

float CalculatePressureWeight(float dst, float radius)
{
    return Math_SpikyGradient(dst, radius);
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