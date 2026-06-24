using System;
using UnityEngine;

public sealed class PhysicsSystem2D : IDisposable
{
    // ── Constants ────────────────────────────────────────────────────────────
    const int ThreadsPerGroup = 64;
    const int ParticleStride = 40;
    const int SpatialEntryStride = 8;

    // ── Shader property IDs ──────────────────────────────────────────────────
    static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
    static readonly int PaddedCountId = Shader.PropertyToID("_PaddedCount");
    static readonly int KId = Shader.PropertyToID("_K");
    static readonly int JId = Shader.PropertyToID("_J");
    static readonly int DeltaTimeId = Shader.PropertyToID("_DeltaTime");
    static readonly int MassId = Shader.PropertyToID("_Mass");
    static readonly int SmoothingRadiusId = Shader.PropertyToID("_SmoothingRadius");
    static readonly int PressureMultiplierId = Shader.PropertyToID("_PressureMultiplier");
    static readonly int TargetDensityId = Shader.PropertyToID("_TargetDensity");
    static readonly int ParticleRadiusId = Shader.PropertyToID("_ParticleRadius");
    static readonly int CollisionDampingId = Shader.PropertyToID("_CollisionDamping");
    static readonly int BoundsMinId = Shader.PropertyToID("_BoundsMin");
    static readonly int BoundsMaxId = Shader.PropertyToID("_BoundsMax");
    static readonly int ParticlesId = Shader.PropertyToID("_Particles");
    static readonly int SpatialLookupId = Shader.PropertyToID("_SpatialLookup");
    static readonly int StartIndicesId = Shader.PropertyToID("_StartIndices");

    // ── Kernel handles ───────────────────────────────────────────────────────
    readonly ComputeShader _compute;

    readonly int _predictPositionsKernel;
    readonly int _buildSpatialLookupKernel;
    readonly int _clearStartIndicesKernel;
    readonly int _bitonicSortKernel;
    readonly int _buildStartIndicesKernel;
    readonly int _updateDensitiesKernel;
    readonly int _calcPressureKernel;
    readonly int _integrateKernel;

    // ── GPU buffers ──────────────────────────────────────────────────────────
    ComputeBuffer _particlesBuffer;
    ComputeBuffer _spatialLookupBuffer;
    ComputeBuffer _startIndicesBuffer;

    // ── State ────────────────────────────────────────────────────────────────
    int _paddedCount;

    // ── Public API ───────────────────────────────────────────────────────────
    public ComputeBuffer ParticleBuffer => _particlesBuffer;
    public int ParticleCount { get; private set; }

    // ── Constructor ──────────────────────────────────────────────────────────
    public PhysicsSystem2D(ComputeShader computeShader)
    {
        _compute = computeShader;
        _predictPositionsKernel = _compute.FindKernel("PredictPositions");
        _buildSpatialLookupKernel = _compute.FindKernel("BuildSpatialLookup");
        _clearStartIndicesKernel = _compute.FindKernel("ClearStartIndices");
        _bitonicSortKernel = _compute.FindKernel("BitonicSort");
        _buildStartIndicesKernel = _compute.FindKernel("BuildStartIndices");
        _updateDensitiesKernel = _compute.FindKernel("UpdateDensities");
        _calcPressureKernel = _compute.FindKernel("CalculatePressureForces");
        _integrateKernel = _compute.FindKernel("Integrate");
    }

    // ── Initialization ───────────────────────────────────────────────────────
    public void Initialize(ParticleSettings settings, ParticleData2D[] particles)
    {
        DisposeBuffers();

        ParticleCount = particles.Length;
        if (ParticleCount == 0) return;

        _paddedCount = NextPowerOfTwo(ParticleCount);

        _particlesBuffer = new ComputeBuffer(ParticleCount, ParticleStride, ComputeBufferType.Structured);
        _particlesBuffer.SetData(particles);

        _spatialLookupBuffer = new ComputeBuffer(_paddedCount, SpatialEntryStride, ComputeBufferType.Structured);
        _startIndicesBuffer = new ComputeBuffer(_paddedCount, sizeof(uint), ComputeBufferType.Structured);

        // Bind everything ONCE.
        BindStaticUniforms(settings);
        BindAllBuffers();
    }

    // ── Per-frame simulation ─────────────────────────────────────────────────
    public void Simulate(ParticleSettings settings, float deltaTime, Vector2 boundsMin, Vector2 boundsMax)
    {
        if (_particlesBuffer == null || ParticleCount == 0) return;

        // Only update uniforms that change per frame
        _compute.SetFloat(DeltaTimeId, deltaTime);
        _compute.SetVector(BoundsMinId, boundsMin);
        _compute.SetVector(BoundsMaxId, boundsMax);

        int realGroups = Mathf.CeilToInt(ParticleCount / (float)ThreadsPerGroup);
        int paddedGroups = Mathf.CeilToInt(_paddedCount / (float)ThreadsPerGroup);

        // 0. Predict positions on GPU:
        _compute.Dispatch(_predictPositionsKernel, realGroups, 1, 1);

        // 1. Build spatial lookup completely on GPU
        _compute.Dispatch(_buildSpatialLookupKernel, paddedGroups, 1, 1);

        // 2. GPU bitonic sort
        DispatchBitonicSort(paddedGroups);

        // 3. Clear and build start indices completely on GPU
        _compute.Dispatch(_clearStartIndicesKernel, paddedGroups, 1, 1);
        _compute.Dispatch(_buildStartIndicesKernel, paddedGroups, 1, 1);

        // 4. Physics kernels
        _compute.Dispatch(_updateDensitiesKernel, realGroups, 1, 1);
        _compute.Dispatch(_calcPressureKernel, realGroups, 1, 1);
        _compute.Dispatch(_integrateKernel, realGroups, 1, 1);
    }

    // ── IDisposable ──────────────────────────────────────────────────────────
    public void Dispose() => DisposeBuffers();

    // ── Private helpers ──────────────────────────────────────────────────────
    public void BindStaticUniforms(ParticleSettings settings)
    {
        _compute.SetInt(ParticleCountId, ParticleCount);
        _compute.SetInt(PaddedCountId, _paddedCount);
        _compute.SetFloat(MassId, settings.mass);
        _compute.SetFloat(SmoothingRadiusId, settings.smoothingRadius);
        _compute.SetFloat(PressureMultiplierId, settings.pressureMultiplier);
        _compute.SetFloat(TargetDensityId, settings.targetDensity);
        _compute.SetFloat(ParticleRadiusId, settings.radius);
        _compute.SetFloat(CollisionDampingId, settings.collisionDamping);
    }

    void BindAllBuffers()
    {
        int[] kernels = {
            _predictPositionsKernel,
            _buildSpatialLookupKernel, _clearStartIndicesKernel,
            _bitonicSortKernel, _buildStartIndicesKernel,
            _updateDensitiesKernel, _calcPressureKernel, _integrateKernel
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