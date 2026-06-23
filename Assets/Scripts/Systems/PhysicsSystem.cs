using System;
using UnityEngine;

public sealed class PhysicsSystem : IDisposable
{
    const int ThreadsPerGroup = 64;
    const int ParticleStride = 32;

    static readonly int ParticleCountId = Shader.PropertyToID("_ParticleCount");
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

    readonly ComputeShader _computeShader;
    readonly int _updateDensitiesKernel;
    readonly int _calculatePressureForcesKernel;
    readonly int _integrateKernel;

    ComputeBuffer _particlesBuffer;

    public ComputeBuffer ParticleBuffer => _particlesBuffer;
    public int ParticleCount { get; private set; }

    public PhysicsSystem(ComputeShader computeShader)
    {
        _computeShader = computeShader;
        _updateDensitiesKernel = _computeShader.FindKernel("UpdateDensities");
        _calculatePressureForcesKernel = _computeShader.FindKernel("CalculatePressureForces");
        _integrateKernel = _computeShader.FindKernel("Integrate");
    }

    public void Initialize(ParticleSettings settings, ParticleData[] particles)
    {
        DisposeBuffers();

        ParticleCount = particles.Length;
        if (ParticleCount == 0)
        {
            return;
        }

        _particlesBuffer = new ComputeBuffer(ParticleCount, ParticleStride, ComputeBufferType.Structured);
        _particlesBuffer.SetData(particles);

        BindSharedParameters(settings, 0f, Vector2.zero, Vector2.zero);
        BindBuffers();
    }

    public void Simulate(ParticleSettings settings, float deltaTime, Vector2 boundsMin, Vector2 boundsMax)
    {
        if (_particlesBuffer == null || ParticleCount == 0)
        {
            return;
        }

        BindSharedParameters(settings, deltaTime, boundsMin, boundsMax);
        BindBuffers();

        int groups = Mathf.CeilToInt(ParticleCount / (float)ThreadsPerGroup);
        _computeShader.Dispatch(_updateDensitiesKernel, groups, 1, 1);
        _computeShader.Dispatch(_calculatePressureForcesKernel, groups, 1, 1);
        _computeShader.Dispatch(_integrateKernel, groups, 1, 1);
    }

    public void Dispose()
    {
        DisposeBuffers();
    }

    void BindSharedParameters(ParticleSettings settings, float deltaTime, Vector2 boundsMin, Vector2 boundsMax)
    {
        _computeShader.SetInt(ParticleCountId, ParticleCount);
        _computeShader.SetFloat(DeltaTimeId, deltaTime);
        _computeShader.SetFloat(MassId, settings.mass);
        _computeShader.SetFloat(SmoothingRadiusId, settings.smoothingRadius);
        _computeShader.SetFloat(PressureMultiplierId, settings.pressureMultiplier);
        _computeShader.SetFloat(TargetDensityId, settings.targetDensity);
        _computeShader.SetFloat(ParticleRadiusId, settings.radius);
        _computeShader.SetFloat(CollisionDampingId, settings.collisionDamping);
        _computeShader.SetVector(BoundsMinId, boundsMin);
        _computeShader.SetVector(BoundsMaxId, boundsMax);
    }

    void BindBuffers()
    {
        _computeShader.SetBuffer(_updateDensitiesKernel, ParticlesId, _particlesBuffer);
        _computeShader.SetBuffer(_calculatePressureForcesKernel, ParticlesId, _particlesBuffer);
        _computeShader.SetBuffer(_integrateKernel, ParticlesId, _particlesBuffer);
    }

    void DisposeBuffers()
    {
        if (_particlesBuffer != null)
        {
            _particlesBuffer.Release();
            _particlesBuffer = null;
        }

        ParticleCount = 0;
    }
}