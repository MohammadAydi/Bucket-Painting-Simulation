using System;
using UnityEngine;

public sealed class PhysicsSystem3D : IDisposable
{
    const int ThreadsPerGroup = 64;
    const int ParticleStride = 72; // 4x Vector4 + 2x float
    const int SpatialEntryStride = 8; // 2x uint

    static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    static readonly int PaddedCountId = Shader.PropertyToID("_PaddedCount");
    static readonly int KId = Shader.PropertyToID("_K");
    static readonly int JId = Shader.PropertyToID("_J");
    static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    static readonly int MassId = Shader.PropertyToID("_Mass");
    static readonly int GravityId = Shader.PropertyToID("_Gravity");
    static readonly int SmoothingRadiusId = Shader.PropertyToID("_SmoothingRadius");
    static readonly int PressureMultiplierId = Shader.PropertyToID("_PressureMultiplier");
    static readonly int TargetDensityId = Shader.PropertyToID("_TargetDensity");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int CollisionDampingId = Shader.PropertyToID("_CollisionDamping");
    
    static readonly int BoundaryLocalMinId = Shader.PropertyToID("_BoundaryLocalMin");
    static readonly int BoundaryLocalMaxId = Shader.PropertyToID("_BoundaryLocalMax");
    static readonly int BoundaryWorldToLocalId = Shader.PropertyToID("_BoundaryWorldToLocal");
    static readonly int BoundaryLocalToWorldId = Shader.PropertyToID("_BoundaryLocalToWorld");

    static readonly int InteractionInputPosId = Shader.PropertyToID("_InteractionInputPos");
    static readonly int InteractionRadiusId = Shader.PropertyToID("_InteractionRadius");
    static readonly int InteractionStrengthId = Shader.PropertyToID("_InteractionStrength");

    static readonly int ViscosityCoeffId = Shader.PropertyToID("_ViscosityCoeff");
    static readonly int SurfaceTensionCoeffId = Shader.PropertyToID("_SurfaceTensionCoeff");
    static readonly int SurfaceTensionThresholdId = Shader.PropertyToID("_SurfaceTensionThreshold");

    static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    static readonly int SpatialLookupId = Shader.PropertyToID("_SpatialLookup");
    static readonly int StartIndicesId = Shader.PropertyToID("_StartIndices");

    readonly ComputeShader _compute;
    
    readonly int _predictPositionsKernel;
    readonly int _buildSpatialLookupKernel;
    readonly int _clearStartIndicesKernel;
    readonly int _bitonicSortKernel;
    readonly int _buildStartIndicesKernel;
    readonly int _updateDensitiesKernel;
    readonly int _calcPressureKernel;
    readonly int _calcViscosityKernel;
    readonly int _calcSurfaceTensionKernel;
    readonly int _integrateKernel;

    ComputeBuffer _particlesBuffer;
    ComputeBuffer _spatialLookupBuffer;
    ComputeBuffer _startIndicesBuffer;

    int _paddedCount;

    public ComputeBuffer ParticleBuffer => _particlesBuffer;
    public int ParticleCount { get; private set; }

    public PhysicsSystem3D(ComputeShader computeShader)
    {
        _compute = computeShader;
        _predictPositionsKernel = _compute.FindKernel("PredictPositions");
        _buildSpatialLookupKernel = _compute.FindKernel("BuildSpatialLookup");
        _clearStartIndicesKernel = _compute.FindKernel("ClearStartIndices");
        _bitonicSortKernel = _compute.FindKernel("BitonicSort");
        _buildStartIndicesKernel = _compute.FindKernel("BuildStartIndices");
        _updateDensitiesKernel = _compute.FindKernel("UpdateDensities");
        _calcPressureKernel = _compute.FindKernel("CalculatePressureForces");
        _calcViscosityKernel = _compute.FindKernel("CalculateViscosityForces");
        _calcSurfaceTensionKernel = _compute.FindKernel("CalculateSurfaceTension");
        _integrateKernel = _compute.FindKernel("Integrate");
    }

    public void Initialize(ParticleSettings settings, ParticleData3D[] particles)
    {
        DisposeBuffers();

        ParticleCount = particles.Length;
        if (ParticleCount == 0) return;

        _paddedCount = NextPowerOfTwo(ParticleCount);

        _particlesBuffer = new ComputeBuffer(ParticleCount, ParticleStride, ComputeBufferType.Structured);
        _particlesBuffer.SetData(particles);

        _spatialLookupBuffer = new ComputeBuffer(_paddedCount, SpatialEntryStride, ComputeBufferType.Structured);
        _startIndicesBuffer = new ComputeBuffer(_paddedCount, sizeof(uint), ComputeBufferType.Structured);

        BindStaticUniforms(settings);
        BindAllBuffers();
    }

    public void Simulate(
        ParticleSettings settings, 
        float deltaTime, 
        Vector3 boundsMin, 
        Vector3 boundsMax, 
        Matrix4x4 worldToLocal, 
        Matrix4x4 localToWorld,
        Vector3 interactionPos, 
        float interactionStrength)
    {
        if (_particlesBuffer == null || ParticleCount == 0) return;

        _compute.SetFloat(DeltaTimeId, deltaTime);
        _compute.SetVector(BoundaryLocalMinId, boundsMin);
        _compute.SetVector(BoundaryLocalMaxId, boundsMax);
        _compute.SetMatrix(BoundaryWorldToLocalId, worldToLocal);
        _compute.SetMatrix(BoundaryLocalToWorldId, localToWorld);
        
        _compute.SetVector(InteractionInputPosId, interactionPos);
        _compute.SetFloat(InteractionStrengthId, interactionStrength);

        int realGroups = Mathf.CeilToInt(ParticleCount / (float)ThreadsPerGroup);
        int paddedGroups = Mathf.CeilToInt(_paddedCount / (float)ThreadsPerGroup);

        _compute.Dispatch(_predictPositionsKernel, realGroups, 1, 1);
        _compute.Dispatch(_buildSpatialLookupKernel, paddedGroups, 1, 1);
        DispatchBitonicSort(paddedGroups);
        _compute.Dispatch(_clearStartIndicesKernel, paddedGroups, 1, 1);
        _compute.Dispatch(_buildStartIndicesKernel, paddedGroups, 1, 1);
        _compute.Dispatch(_updateDensitiesKernel, realGroups, 1, 1);
        _compute.Dispatch(_calcPressureKernel, realGroups, 1, 1);
        _compute.Dispatch(_calcViscosityKernel, realGroups, 1, 1);
        _compute.Dispatch(_calcSurfaceTensionKernel, realGroups, 1, 1);
        _compute.Dispatch(_integrateKernel, realGroups, 1, 1);
    }

    public void Dispose() => DisposeBuffers();

    public void BindStaticUniforms(ParticleSettings settings)
    {
        _compute.SetInt(ParticleCountId, ParticleCount);
        _compute.SetInt(PaddedCountId, _paddedCount);
        _compute.SetFloat(MassId, settings.mass);
        _compute.SetFloat(GravityId, settings.gravity);
        _compute.SetFloat(SmoothingRadiusId, settings.smoothingRadius);
        _compute.SetFloat(PressureMultiplierId, settings.pressureMultiplier);
        _compute.SetFloat(TargetDensityId, settings.targetDensity);
        _compute.SetFloat(ParticleRadiusId, settings.radius);
        _compute.SetFloat(CollisionDampingId, settings.collisionDamping);
        _compute.SetFloat(InteractionRadiusId, settings.interactionRadius);
        _compute.SetFloat(ViscosityCoeffId, settings.viscosityCoeff);
        _compute.SetFloat(SurfaceTensionCoeffId, settings.surfaceTensionCoeff);
        _compute.SetFloat(SurfaceTensionThresholdId, settings.surfaceTensionThreshold);
    }

    void BindAllBuffers()
    {
        int[] kernels = {
            _predictPositionsKernel, _buildSpatialLookupKernel, _clearStartIndicesKernel,
            _bitonicSortKernel, _buildStartIndicesKernel, _updateDensitiesKernel, 
            _calcPressureKernel, _calcViscosityKernel, _calcSurfaceTensionKernel, _integrateKernel
        };

        foreach (int k in kernels)
        {
            _compute.SetBuffer(k, ParticlesId, _particlesBuffer);
            _compute.SetBuffer(k, SpatialLookupId, _spatialLookupBuffer);
            _compute.SetBuffer(k, StartIndicesId, _startIndicesBuffer);
        }
    }

    void DispatchBitonicSort(int paddedGroups)
    {
        for (int k = 2; k <= _paddedCount; k *= 2)
        {
            for (int j = k / 2; j > 0; j /= 2)
            {
                _compute.SetInt(KId, k);
                _compute.SetInt(JId, j);
                _compute.Dispatch(_bitonicSortKernel, paddedGroups, 1, 1);
            }
        }
    }

    static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p *= 2;
        return p;
    }

    void DisposeBuffers()
    {
        _particlesBuffer?.Release();
        _particlesBuffer = null;
        _spatialLookupBuffer?.Release();
        _spatialLookupBuffer = null;
        _startIndicesBuffer?.Release();
        _startIndicesBuffer = null;
        ParticleCount = 0;
        _paddedCount = 0;
    }
}