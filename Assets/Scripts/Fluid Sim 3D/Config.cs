
using UnityEngine;
class Config
{
    public const int ThreadsPerGroup = 256;
    // const int ParticleStride = 72; // 4x Vector4 + 2x float
    // const int SpatialEntryStride = 8; // 2x uint

    public static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    // static readonly int PaddedCountId = Shader.PropertyToID("_PaddedCount");
    // static readonly int KId = Shader.PropertyToID("_K");
    // static readonly int JId = Shader.PropertyToID("_J");
    public static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    public static readonly int MassId = Shader.PropertyToID("_Mass");
    public static readonly int GravityId = Shader.PropertyToID("_Gravity");
    public static readonly int SmoothingRadiusId = Shader.PropertyToID("_SmoothingRadius");
    public static readonly int PressureMultiplierId = Shader.PropertyToID("_PressureMultiplier");
    public static readonly int TargetDensityId = Shader.PropertyToID("_TargetDensity");
    public static readonly int PressureSolverModeId = Shader.PropertyToID("_PressureSolverMode");
    public static readonly int NearPressureMultiplierId = Shader.PropertyToID("_NearPressureMultiplier");
    public static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    public static readonly int CollisionDampingId = Shader.PropertyToID("_CollisionDamping");

    public static readonly int BoundaryLocalMinId = Shader.PropertyToID("_BoundaryLocalMin");
    public static readonly int BoundaryLocalMaxId = Shader.PropertyToID("_BoundaryLocalMax");
    public static readonly int BoundaryWorldToLocalId = Shader.PropertyToID("_BoundaryWorldToLocal");
    public static readonly int BoundaryLocalToWorldId = Shader.PropertyToID("_BoundaryLocalToWorld");

    public static readonly int InteractionInputPosId = Shader.PropertyToID("_InteractionInputPos");
    public static readonly int InteractionRadiusId = Shader.PropertyToID("_InteractionRadius");
    public static readonly int InteractionStrengthId = Shader.PropertyToID("_InteractionStrength");

    public static readonly int ViscosityCoeffId = Shader.PropertyToID("_ViscosityCoeff");
    public static readonly int SurfaceTensionCoeffId = Shader.PropertyToID("_SurfaceTensionCoeff");
    public static readonly int SurfaceTensionThresholdId = Shader.PropertyToID("_SurfaceTensionThreshold");

    public static readonly int Poly6Id = Shader.PropertyToID("K_Poly6");
    public static readonly int SpikyGradientId = Shader.PropertyToID("K_SpikyGradient");
    public static readonly int ViscosityLaplacianId = Shader.PropertyToID("K_ViscosityLaplacian");
    public static readonly int Poly6GradientId = Shader.PropertyToID("K_Poly6Gradient");
    public static readonly int Poly6LaplacianId = Shader.PropertyToID("K_Poly6Laplacian");
    public static readonly int CubicSplineId = Shader.PropertyToID("K_CubicSpline");

    public static readonly int SpikyPow2Id = Shader.PropertyToID("K_SpikyPow2");
    public static readonly int SpikyPow3Id = Shader.PropertyToID("K_SpikyPow3");
    public static readonly int SpikyPow2GradId = Shader.PropertyToID("K_SpikyPow2Grad");
    public static readonly int SpikyPow3GradId = Shader.PropertyToID("K_SpikyPow3Grad");



    // static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    // static readonly int SpatialLookupId = Shader.PropertyToID("_SpatialLookup");

    public static readonly int PositionsId = Shader.PropertyToID("_Positions");
    public static readonly int PredictedPositionsId = Shader.PropertyToID("_PredictedPositions");
    public static readonly int VelocitiesId = Shader.PropertyToID("_Velocities");
    public static readonly int DensitiesId = Shader.PropertyToID("_Densities");
    public static readonly int CellKeysId = Shader.PropertyToID("SpatialKeys");
    public static readonly int ParticleIndicesId = Shader.PropertyToID("SortedIndices");
    public static readonly int StartIndicesId = Shader.PropertyToID("SpatialOffsets");

    public static readonly int SortTarget_PositionsId = Shader.PropertyToID("SortTarget_Positions");
    public static readonly int SortTarget_PredictedPositionsId = Shader.PropertyToID("SortTarget_PredictedPositions");
    public static readonly int SortTarget_VelocitiesId = Shader.PropertyToID("SortTarget_Velocities");

}

public enum ViscositySolverMethod
{
    Standard = 0,
}

public enum PressureSolverMethod
{
    Standard = 0,
    NearPressure = 1,
}

public enum SurfaceTensionSolverMethod
{
    Standard = 0,
}
